"""
Thin MCP (Streamable HTTP) adapter that exposes TrackLord.Api (.NET) endpoints
as voice-callable tools for Agora ConvoAI. Zero business logic is duplicated here;
every read/write forwards directly to TrackLord.Api with the caller's JWT.
"""
import difflib
import json
import logging
import os
import re
import time
import uuid
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

import httpx
from fastapi import APIRouter, Header, Request, Response
from fastapi.responses import JSONResponse

logger = logging.getLogger("uvicorn.error")
TRACKLORD_API_URL = os.getenv("TRACKLORD_API_URL", "http://localhost:5217").rstrip("/")

router = APIRouter()

# Event queue so multiple tool calls in a single turn are never dropped by 1.5s polling.
_last_mutation: Dict[str, Any] = {"timestamp": 0.0, "action": None, "summary": None, "isMutation": False}
_event_log: List[Dict[str, Any]] = []
_session_tokens: Dict[str, str] = {}
_latest_token: Optional[str] = None


def set_session_token(session_key: str, token: Optional[str]) -> None:
    global _latest_token
    if token:
        _session_tokens[session_key] = token
        _latest_token = token


def get_last_mutation() -> Dict[str, Any]:
    return _last_mutation


def _record_mutation(
    action: str,
    summary: str,
    is_mutation: bool = True,
    navigate_to: Optional[str] = None,
) -> None:
    now = time.time()
    if _event_log and now <= _event_log[-1]["timestamp"]:
        now = _event_log[-1]["timestamp"] + 0.0001
    entry = {
        "timestamp": now,
        "action": action,
        "summary": summary,
        "isMutation": is_mutation,
        "navigateTo": navigate_to,
    }
    _last_mutation.update(entry)
    _event_log.append(entry)
    if len(_event_log) > 100:
        del _event_log[:-100]


TOOLS: List[Dict[str, Any]] = [
    {
        "name": "get_dashboard_summary",
        "description": "Get today's TrackLord production dashboard overview including KPIs, step WIP counts, and active lots.",
        "inputSchema": {
            "type": "object",
            "properties": {},
        },
    },
    {
        "name": "list_production_lots",
        "description": "List production lots in TrackLord with their lot numbers, clients, quantities, and current status.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "search": {
                    "type": "string",
                    "description": "Optional lot number or client name filter.",
                }
            },
        },
    },
    {
        "name": "list_manufacturing_steps",
        "description": "List all manufacturing steps in sequence order (e.g., Cutting, Welding, Polishing).",
        "inputSchema": {
            "type": "object",
            "properties": {},
        },
    },
    {
        "name": "get_lot_status",
        "description": "Get the current manufacturing step(s) and WIP quantity for a specific production lot number (e.g. '412', 'NEW-001').",
        "inputSchema": {
            "type": "object",
            "properties": {
                "lotNumber": {
                    "type": "string",
                    "description": "The production lot number (e.g. '412', 'NEW-001').",
                }
            },
            "required": ["lotNumber"],
        },
    },
    {
        "name": "move_lot_to_step",
        "description": "Move a production lot from its current manufacturing step to a target step.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "lotNumber": {
                    "type": "string",
                    "description": "The production lot number (e.g. 'LOT-102' or '102').",
                },
                "toStepName": {
                    "type": "string",
                    "description": "Target manufacturing step name (e.g. 'Welding', 'Polishing').",
                },
                "fromStepName": {
                    "type": "string",
                    "description": "Optional source step name. If omitted, uses the step where the lot currently has WIP.",
                },
                "quantity": {
                    "type": "number",
                    "description": "Optional quantity to move. If omitted, moves all available WIP at the source step.",
                },
                "remarks": {
                    "type": "string",
                    "description": "Optional remarks for the movement.",
                },
            },
            "required": ["lotNumber", "toStepName"],
        },
    },
    {
        "name": "list_material_lots",
        "description": "List raw material inventory lots and remaining quantities.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "search": {
                    "type": "string",
                    "description": "Optional material name or lot number filter.",
                }
            },
        },
    },
    {
        "name": "log_material_consumption",
        "description": "Log raw material consumed by a production lot at a manufacturing step.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "lotNumber": {
                    "type": "string",
                    "description": "Production lot number.",
                },
                "materialNameOrLot": {
                    "type": "string",
                    "description": "Material name or material lot number.",
                },
                "quantityConsumed": {
                    "type": "number",
                    "description": "Quantity consumed.",
                },
                "stepName": {
                    "type": "string",
                    "description": "Optional step name. Defaults to the step where the lot has active WIP.",
                },
                "remarks": {
                    "type": "string",
                    "description": "Optional remarks.",
                },
            },
            "required": ["lotNumber", "materialNameOrLot", "quantityConsumed"],
        },
    },
    {
        "name": "create_production_lot",
        "description": "Create a new production lot in TrackLord with an initial quantity, unit, starting step, and optional client.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "lotNumber": {
                    "type": "string",
                    "description": "New unique production lot number (e.g. '501', 'LOT-501').",
                },
                "initialQuantity": {
                    "type": "number",
                    "description": "Initial quantity for the lot (e.g. 200).",
                },
                "unit": {
                    "type": "string",
                    "description": "Unit of measure: 'pieces', 'meters', 'kilograms', 'dozens', 'yards', or 'grams'. Defaults to 'pieces'.",
                },
                "initialStepName": {
                    "type": "string",
                    "description": "Optional starting manufacturing step name (e.g. 'Cutting'). Defaults to the first step in sequence.",
                },
                "clientName": {
                    "type": "string",
                    "description": "Optional client name (e.g. 'Z Square').",
                },
                "remarks": {
                    "type": "string",
                    "description": "Optional remarks.",
                },
            },
            "required": ["lotNumber", "initialQuantity"],
        },
    },
    {
        "name": "reject_lot_items",
        "description": "Log quality-control (QC) rejected or damaged items for a production lot at a manufacturing step.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "lotNumber": {
                    "type": "string",
                    "description": "Production lot number (e.g. '412').",
                },
                "quantityRejected": {
                    "type": "number",
                    "description": "Quantity of items rejected.",
                },
                "reason": {
                    "type": "string",
                    "description": "Reason for rejection (e.g. 'Fabric stain', 'Stitching defect').",
                },
                "stepName": {
                    "type": "string",
                    "description": "Optional step name where rejection occurred. Defaults to the lot's current active step.",
                },
                "targetStepName": {
                    "type": "string",
                    "description": "Optional target step name if items are returned for rework.",
                },
            },
            "required": ["lotNumber", "quantityRejected"],
        },
    },
    {
        "name": "navigate_ui",
        "description": "Navigate the TrackLord web UI on the user's screen to a specific page (dashboard, lots, inventory, materials, steps, clients, or users).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "page": {
                    "type": "string",
                    "description": "Target page name: 'dashboard', 'lots', 'inventory', 'materials', 'steps', 'clients', or 'users'.",
                }
            },
            "required": ["page"],
        },
    },
]


_SPOKEN_PUNCT = re.compile(r"\b(dash|hyphen|minus|slash|underscore|dot|point|hash|pound|lot|number)\b", re.IGNORECASE)
_NON_ALNUM = re.compile(r"[^a-z0-9]")

_UNIT_MAP = {
    1: "pieces",
    2: "meters",
    3: "kilograms",
    4: "dozens",
    5: "yards",
    6: "grams",
}

_UNIT_NAME_TO_INT = {
    "piece": 1,
    "pieces": 1,
    "pcs": 1,
    "pc": 1,
    "meter": 2,
    "meters": 2,
    "mtr": 2,
    "m": 2,
    "kilogram": 3,
    "kilograms": 3,
    "kg": 3,
    "kgs": 3,
    "kilo": 3,
    "dozen": 4,
    "dozens": 4,
    "dz": 4,
    "yard": 5,
    "yards": 5,
    "yd": 5,
    "gram": 6,
    "grams": 6,
    "gm": 6,
    "g": 6,
}

_PAGE_ROUTES = {
    "dashboard": ("/dashboard", "Dashboard"),
    "home": ("/dashboard", "Dashboard"),
    "overview": ("/dashboard", "Dashboard"),
    "lots": ("/lots", "Production Lots"),
    "lot": ("/lots", "Production Lots"),
    "productionlots": ("/lots", "Production Lots"),
    "inventory": ("/inventory", "WIP Inventory"),
    "wip": ("/inventory", "WIP Inventory"),
    "materials": ("/materials", "Raw Materials"),
    "material": ("/materials", "Raw Materials"),
    "rawmaterials": ("/materials", "Raw Materials"),
    "steps": ("/steps", "Manufacturing Steps"),
    "step": ("/steps", "Manufacturing Steps"),
    "clients": ("/clients", "Clients"),
    "client": ("/clients", "Clients"),
    "users": ("/users", "Users & Roles"),
    "user": ("/users", "Users & Roles"),
}


def _parse_unit_int(unit_str: Any) -> int:
    if isinstance(unit_str, int) and 1 <= unit_str <= 6:
        return unit_str
    cleaned = _NON_ALNUM.sub("", str(unit_str or "pieces").lower())
    return _UNIT_NAME_TO_INT.get(cleaned, 1)


def _unit_label(unit_val: Any) -> str:
    if isinstance(unit_val, int) or (isinstance(unit_val, str) and unit_val.isdigit()):
        return _UNIT_MAP.get(int(unit_val), "pieces")
    if isinstance(unit_val, str) and unit_val.strip():
        return unit_val.strip().lower()
    return "pieces"


def _enrich_units(items: Any) -> Any:
    if isinstance(items, list):
        for item in items:
            if isinstance(item, dict) and "unit" in item:
                item["unitName"] = _unit_label(item.get("unit"))
    elif isinstance(items, dict) and "unit" in items:
        items["unitName"] = _unit_label(items.get("unit"))
    return items


def _normalize_token(text: str) -> str:
    """Strip spoken punctuation/fillers and all non-alphanumeric chars for STT-resilient matching."""
    cleaned = _SPOKEN_PUNCT.sub("", text or "")
    return _NON_ALNUM.sub("", cleaned.lower())


def _match_by_name_or_number(items: List[Dict[str, Any]], query: str, keys: List[str]) -> Optional[Dict[str, Any]]:
    raw_q = (query or "").strip().lower()
    norm_q = _normalize_token(query)
    if not norm_q and not raw_q:
        return None

    # 1. Exact raw match
    for item in items:
        for k in keys:
            val = str(item.get(k) or "").strip().lower()
            if val == raw_q:
                return item

    # 2. Exact normalized alphanumeric match (e.g. "new 001" / "new dash 001" -> "NEW-001")
    for item in items:
        for k in keys:
            if _normalize_token(str(item.get(k) or "")) == norm_q:
                return item

    # 3. Substring match on normalized tokens
    for item in items:
        for k in keys:
            norm_val = _normalize_token(str(item.get(k) or ""))
            if norm_val and (norm_q in norm_val or norm_val in norm_q):
                return item

    # 4. Fuzzy similarity fallback (handles slight STT mishearings)
    candidates: List[Tuple[str, Dict[str, Any]]] = []
    for item in items:
        for k in keys:
            nv = _normalize_token(str(item.get(k) or ""))
            if nv:
                candidates.append((nv, item))
    if candidates:
        best = difflib.get_close_matches(norm_q, [c[0] for c in candidates], n=1, cutoff=0.72)
        if best:
            for nv, item in candidates:
                if nv == best[0]:
                    return item

    return None


def _suggest_lots(lots: List[Dict[str, Any]], query: str, limit: int = 3) -> List[str]:
    norm_q = _normalize_token(query)
    norm_map = {_normalize_token(str(l.get("lotNumber") or "")): str(l.get("lotNumber")) for l in lots if l.get("lotNumber")}
    close = difflib.get_close_matches(norm_q, list(norm_map.keys()), n=limit, cutoff=0.4)
    return [norm_map[c] for c in close]


async def _api_request(
    method: str,
    path: str,
    auth_header: Optional[str],
    json_body: Optional[Dict[str, Any]] = None,
) -> Any:
    headers: Dict[str, str] = {"Accept": "application/json"}
    if auth_header:
        headers["Authorization"] = auth_header

    async with httpx.AsyncClient(timeout=15.0) as client:
        resp = await client.request(
            method,
            f"{TRACKLORD_API_URL}{path}",
            headers=headers,
            json=json_body,
        )
        if resp.status_code >= 400:
            detail = resp.text
            try:
                err_json = resp.json()
                detail = err_json.get("detail") or err_json.get("title") or err_json.get("message") or detail
            except Exception:
                pass
            raise RuntimeError(f"TrackLord API {resp.status_code}: {detail}")
        if resp.status_code == 204 or not resp.content:
            return {"success": True}
        return resp.json()


def _utc_now_z() -> str:
    """Format current UTC timestamp with trailing 'Z' so .NET System.Text.Json deserializes DateTimeKind.Utc for Npgsql."""
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


async def _get_active_wips(lot_id: str, auth_header: Optional[str]) -> List[Dict[str, Any]]:
    wip_resp = await _api_request("GET", f"/api/ProductionLots/{lot_id}/wip", auth_header)
    balances = wip_resp.get("currentStepBalances", []) if isinstance(wip_resp, dict) else (wip_resp or [])
    return [w for w in balances if (w.get("quantity") or w.get("currentQuantity") or 0) > 0]


async def _execute_tool(name: str, args: Dict[str, Any], auth_header: Optional[str]) -> Any:
    if name == "get_dashboard_summary":
        overview = await _api_request("GET", "/api/Dashboards/overview", auth_header)
        if isinstance(overview, dict):
            active_count = overview.get("totalActiveLots") or overview.get("activeLotsCount")
            total_wip = overview.get("totalWipQuantity")
            if active_count is not None:
                _record_mutation(
                    "get_dashboard_summary",
                    f"Active Lots: {active_count}" + (f" • Total WIP: {total_wip}" if total_wip is not None else ""),
                    is_mutation=False,
                )
        return overview

    if name == "get_lot_status":
        lot_query = str(args.get("lotNumber") or "")
        lots = await _api_request("GET", "/api/ProductionLots", auth_header)
        lot = _match_by_name_or_number(lots, lot_query, ["lotNumber", "id"])
        if not lot:
            return {"error": f"Production lot '{lot_query}' not found.", "didYouMean": _suggest_lots(lots, lot_query)}
        unit_name = _unit_label(lot.get("unit"))
        active_wips = await _get_active_wips(lot["id"], auth_header)
        step_summary = ", ".join(
            f"{w.get('stepName', 'Unknown')}: {w.get('quantity', 0)} {unit_name}" for w in active_wips
        ) or "No active WIP"
        _record_mutation(
            "get_lot_status",
            f"Lot #{lot.get('lotNumber')} ({lot.get('clientName') or 'No Client'}) → {step_summary}",
            is_mutation=False,
        )
        return {
            "lotNumber": lot.get("lotNumber"),
            "clientName": lot.get("clientName"),
            "unit": unit_name,
            "currentSteps": active_wips,
            "summary": step_summary,
        }

    if name == "list_production_lots":
        lots = await _api_request("GET", "/api/ProductionLots", auth_header)
        total_count = len(lots) if isinstance(lots, list) else 0
        search = (args.get("search") or "").strip()
        if search and isinstance(lots, list):
            norm_s = _normalize_token(search)
            lots = [
                lot
                for lot in lots
                if norm_s in _normalize_token(str(lot.get("lotNumber", "")))
                or search.lower() in str(lot.get("clientName", "")).lower()
                or search.lower() in str(lot.get("remarks", "")).lower()
            ]
        if isinstance(lots, list):
            if len(lots) == 1:
                only = lots[0]
                wips = await _get_active_wips(only["id"], auth_header)
                u = _unit_label(only.get("unit"))
                step_txt = ", ".join(f"{w.get('stepName')}: {w.get('quantity')} {u}" for w in wips) or "No active WIP"
                _record_mutation(
                    "list_production_lots",
                    f"Lot #{only.get('lotNumber')} → {step_txt}",
                    is_mutation=False,
                )
            else:
                _record_mutation(
                    "list_production_lots",
                    f"Active Production Lots: {len(lots)} (Total: {total_count})",
                    is_mutation=False,
                )
        return {
            "totalMatchingLots": len(lots) if isinstance(lots, list) else 0,
            "totalActiveLots": total_count,
            "lots": _enrich_units(lots[:20] if isinstance(lots, list) else lots),
        }

    if name == "list_manufacturing_steps":
        return await _api_request("GET", "/api/ManufacturingSteps", auth_header)

    if name == "list_material_lots":
        mats = await _api_request("GET", "/api/MaterialConsumptions/lots", auth_header)
        search = (args.get("search") or "").strip().lower()
        if search and isinstance(mats, list):
            mats = [
                m
                for m in mats
                if search in str(m.get("materialName", "")).lower()
                or search in str(m.get("lotNumber", "")).lower()
            ]
        return _enrich_units(mats[:20] if isinstance(mats, list) else mats)

    if name == "move_lot_to_step":
        lot_query = str(args["lotNumber"])
        to_step_query = str(args["toStepName"])
        from_step_query = args.get("fromStepName")
        qty = args.get("quantity")

        lots = await _api_request("GET", "/api/ProductionLots", auth_header)
        lot = _match_by_name_or_number(lots, lot_query, ["lotNumber", "id"])
        if not lot:
            suggestions = _suggest_lots(lots, lot_query)
            return {
                "error": f"Production lot '{lot_query}' not found.",
                "didYouMean": suggestions,
            }

        steps = await _api_request("GET", "/api/ManufacturingSteps", auth_header)
        active_wips = await _get_active_wips(lot["id"], auth_header)

        from_step_id = None
        from_step_name = None
        available_qty = None
        if from_step_query:
            from_step = _match_by_name_or_number(steps, str(from_step_query), ["name", "id"])
            if not from_step:
                return {"error": f"Source step '{from_step_query}' not found."}
            from_step_id = from_step["id"]
            from_step_name = from_step.get("name")
            for w in active_wips:
                if w.get("stepId") == from_step_id or w.get("manufacturingStepId") == from_step_id:
                    available_qty = w.get("quantity") or w.get("currentQuantity")
        elif active_wips:
            first_wip = active_wips[0]
            from_step_id = first_wip.get("stepId") or first_wip.get("manufacturingStepId")
            from_step_name = first_wip.get("stepName")
            available_qty = first_wip.get("quantity") or first_wip.get("currentQuantity")

        if not from_step_id:
            return {"error": f"No active WIP found for lot '{lot.get('lotNumber')}'."}

        # Resolve "next" / "next step" / "agla step" automatically using sequenceOrder
        norm_target = _normalize_token(to_step_query)
        to_step = None
        if norm_target in {"next", "nextstep", "agla", "aglastep", "aglistep", "aage", "forward"}:
            ordered_steps = sorted(
                [s for s in (steps or []) if s.get("isActive", True)],
                key=lambda s: s.get("sequenceOrder", 0),
            )
            for idx, s in enumerate(ordered_steps):
                if s.get("id") == from_step_id:
                    if idx + 1 < len(ordered_steps):
                        to_step = ordered_steps[idx + 1]
                    else:
                        unit_name = _unit_label(lot.get("unit"))
                        msg = f"Lot #{lot.get('lotNumber')} is already at the final step ({from_step_name or s.get('name')}: {available_qty} {unit_name})"
                        _record_mutation("move_lot_to_step", msg, is_mutation=False)
                        return {"alreadyAtFinalStep": True, "message": msg}
                    break
        if not to_step:
            to_step = _match_by_name_or_number(steps, to_step_query, ["name", "id"])
        if not to_step:
            return {"error": f"Target step '{to_step_query}' not found."}

        unit_name = _unit_label(lot.get("unit"))

        # Guard against 400 validation error when lot is already at the target step
        if from_step_id == to_step["id"]:
            msg = f"Lot #{lot.get('lotNumber')} is already at {to_step.get('name')} ({available_qty} {unit_name})"
            _record_mutation("move_lot_to_step", msg, is_mutation=False)
            return {
                "alreadyAtStep": True,
                "lotNumber": lot.get("lotNumber"),
                "step": to_step.get("name"),
                "quantity": available_qty,
                "unit": unit_name,
                "message": msg,
            }

        move_qty = qty if qty is not None else available_qty
        if not move_qty or float(move_qty) <= 0:
            return {"error": f"Lot '{lot.get('lotNumber')}' has 0 {unit_name} available to move from {from_step_name or 'current step'}."}

        payload = {
            "productionLotId": lot["id"],
            "fromStepId": from_step_id,
            "toStepId": to_step["id"],
            "quantity": move_qty,
            "movementDate": _utc_now_z(),
            "remarks": args.get("remarks") or "Moved via TrackLord Voice Copilot",
        }
        result = await _api_request("POST", "/api/Movements", auth_header, payload)
        _record_mutation(
            "move_lot_to_step",
            f"Moved {move_qty} {unit_name} of lot {lot.get('lotNumber')} to {to_step.get('name')}",
        )
        return {
            "success": True,
            "lotNumber": lot.get("lotNumber"),
            "toStep": to_step.get("name"),
            "quantity": move_qty,
            "unit": unit_name,
            "result": result,
        }

    if name == "log_material_consumption":
        lot_query = str(args["lotNumber"])
        mat_query = str(args["materialNameOrLot"])
        qty_consumed = float(args["quantityConsumed"])

        lots = await _api_request("GET", "/api/ProductionLots", auth_header)
        lot = _match_by_name_or_number(lots, lot_query, ["lotNumber", "id"])
        if not lot:
            suggestions = _suggest_lots(lots, lot_query)
            return {
                "error": f"Production lot '{lot_query}' not found.",
                "didYouMean": suggestions,
            }

        mats = await _api_request("GET", "/api/MaterialConsumptions/lots", auth_header)
        mat = _match_by_name_or_number(mats, mat_query, ["materialName", "lotNumber", "id"])
        if not mat:
            return {"error": f"Material '{mat_query}' not found."}

        steps = await _api_request("GET", "/api/ManufacturingSteps", auth_header)
        step_id = None
        if args.get("stepName"):
            step = _match_by_name_or_number(steps, str(args["stepName"]), ["name", "id"])
            if step:
                step_id = step["id"]
        if not step_id:
            active_wips = await _get_active_wips(lot["id"], auth_header)
            if active_wips:
                step_id = active_wips[0].get("stepId") or active_wips[0].get("manufacturingStepId")
            elif steps:
                step_id = steps[0]["id"]

        payload = {
            "productionLotId": lot["id"],
            "materialLotId": mat["id"],
            "stepId": step_id,
            "quantityConsumed": qty_consumed,
            "unit": mat.get("unit", 0),
            "consumptionDate": _utc_now_z(),
            "remarks": args.get("remarks") or "Logged via TrackLord Voice Copilot",
        }
        mat_unit_name = _unit_label(mat.get("unit"))
        result = await _api_request("POST", "/api/MaterialConsumptions/consume", auth_header, payload)
        _record_mutation(
            "log_material_consumption",
            f"Logged {qty_consumed} {mat_unit_name} of {mat.get('materialName')} on lot {lot.get('lotNumber')}",
        )
        return {
            "success": True,
            "lotNumber": lot.get("lotNumber"),
            "material": mat.get("materialName"),
            "quantityConsumed": qty_consumed,
            "unit": mat_unit_name,
            "result": result,
        }

    if name == "create_production_lot":
        lot_number = str(args["lotNumber"]).strip()
        initial_qty = float(args["initialQuantity"])
        unit_int = _parse_unit_int(args.get("unit"))
        unit_name = _unit_label(unit_int)

        steps = await _api_request("GET", "/api/ManufacturingSteps", auth_header)
        ordered_steps = sorted(
            [s for s in (steps or []) if s.get("isActive", True)],
            key=lambda s: s.get("sequenceOrder", 0),
        )
        initial_step = None
        if args.get("initialStepName"):
            initial_step = _match_by_name_or_number(steps, str(args["initialStepName"]), ["name", "id"])
        if not initial_step and ordered_steps:
            initial_step = ordered_steps[0]
        if not initial_step:
            return {"error": "No manufacturing steps found to initialize the lot."}

        client_id = None
        client_name = args.get("clientName")
        if client_name:
            try:
                clients = await _api_request("GET", "/api/Clients", auth_header)
                matched_client = _match_by_name_or_number(clients or [], str(client_name), ["name", "id"])
                if matched_client:
                    client_id = matched_client.get("id")
                    client_name = matched_client.get("name")
            except Exception:
                pass

        payload = {
            "lotNumber": lot_number,
            "itemId": str(uuid.uuid4()),
            "initialQuantity": initial_qty,
            "unit": unit_int,
            "initialStepId": initial_step["id"],
            "creationDate": _utc_now_z(),
            "createdBy": "TrackLord Voice Copilot",
            "remarks": args.get("remarks") or "Created via TrackLord Voice Copilot",
            "clientId": client_id,
            "clientName": client_name,
        }
        result = await _api_request("POST", "/api/ProductionLots", auth_header, payload)
        _record_mutation(
            "create_production_lot",
            f"Created Lot #{lot_number} ({initial_qty} {unit_name}) at {initial_step.get('name')}"
            + (f" for {client_name}" if client_name else ""),
            is_mutation=True,
        )
        return {
            "success": True,
            "lotNumber": lot_number,
            "initialQuantity": initial_qty,
            "unit": unit_name,
            "initialStep": initial_step.get("name"),
            "clientName": client_name,
            "result": result,
        }

    if name == "reject_lot_items":
        lot_query = str(args["lotNumber"])
        qty_rejected = float(args["quantityRejected"])
        reason = str(args.get("reason") or "QC Rejection via Voice Copilot").strip()

        lots = await _api_request("GET", "/api/ProductionLots", auth_header)
        lot = _match_by_name_or_number(lots, lot_query, ["lotNumber", "id"])
        if not lot:
            return {"error": f"Production lot '{lot_query}' not found.", "didYouMean": _suggest_lots(lots, lot_query)}

        steps = await _api_request("GET", "/api/ManufacturingSteps", auth_header)
        active_wips = await _get_active_wips(lot["id"], auth_header)

        step_id = None
        step_name = None
        if args.get("stepName"):
            matched_step = _match_by_name_or_number(steps, str(args["stepName"]), ["name", "id"])
            if matched_step:
                step_id = matched_step["id"]
                step_name = matched_step.get("name")
        if not step_id and active_wips:
            step_id = active_wips[0].get("stepId") or active_wips[0].get("manufacturingStepId")
            step_name = active_wips[0].get("stepName")
        if not step_id:
            return {"error": f"No active WIP step found for lot '{lot.get('lotNumber')}'."}

        target_step_id = None
        if args.get("targetStepName"):
            t_step = _match_by_name_or_number(steps, str(args["targetStepName"]), ["name", "id"])
            if t_step:
                target_step_id = t_step["id"]

        unit_name = _unit_label(lot.get("unit"))
        payload = {
            "stepId": step_id,
            "targetStepId": target_step_id,
            "quantityRejected": qty_rejected,
            "reason": reason,
            "rejectionDate": _utc_now_z(),
            "clientId": lot.get("clientId"),
            "createdBy": "TrackLord Voice Copilot",
        }
        result = await _api_request("POST", f"/api/ProductionLots/{lot['id']}/reject", auth_header, payload)
        _record_mutation(
            "reject_lot_items",
            f"Rejected {qty_rejected} {unit_name} of Lot #{lot.get('lotNumber')} at {step_name or 'current step'} ({reason})",
            is_mutation=True,
        )
        return {
            "success": True,
            "lotNumber": lot.get("lotNumber"),
            "step": step_name,
            "quantityRejected": qty_rejected,
            "unit": unit_name,
            "reason": reason,
            "result": result,
        }

    if name == "navigate_ui":
        raw_page = _NON_ALNUM.sub("", str(args.get("page") or "").lower())
        route_info = _PAGE_ROUTES.get(raw_page)
        if not route_info:
            for k, v in _PAGE_ROUTES.items():
                if k in raw_page or raw_page in k:
                    route_info = v
                    break
        if not route_info:
            return {"error": f"Unknown page '{args.get('page')}'. Available: dashboard, lots, inventory, materials, steps, clients, users."}
        route_path, label = route_info
        _record_mutation(
            "navigate_ui",
            f"Opened {label} page ({route_path})",
            is_mutation=False,
            navigate_to=route_path,
        )
        return {"success": True, "page": label, "route": route_path}

    raise ValueError(f"Unknown tool: {name}")


@router.get("/voice_events")
async def voice_events(since: float = 0.0):
    """Returns all voice-triggered events since `since` so rapid multi-tool turns are never dropped."""
    events = [e for e in _event_log if e["timestamp"] > since]
    return {
        **_last_mutation,
        "events": events,
    }


@router.post("/mcp")
@router.post("/mcp/{session_key}")
async def mcp_endpoint(
    request: Request,
    session_key: Optional[str] = None,
    authorization: Optional[str] = Header(default=None),
):
    """MCP Streamable HTTP JSON-RPC 2.0 endpoint for Agora ConvoAI."""
    body = await request.json()
    req_id = body.get("id")
    method = body.get("method")
    params = body.get("params") or {}
    logger.info("MCP rpc method=%s id=%s session=%s", method, req_id, session_key)

    if method == "initialize":
        client_version = params.get("protocolVersion") or "2024-11-05"
        return JSONResponse(
            {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {
                    "protocolVersion": client_version,
                    "capabilities": {"tools": {"listChanged": False}},
                    "serverInfo": {"name": "tracklord-mcp", "version": "1.0.0"},
                },
            }
        )

    if method == "notifications/initialized" or req_id is None:
        return Response(status_code=202)

    if method == "ping":
        return JSONResponse({"jsonrpc": "2.0", "id": req_id, "result": {}})

    if method == "tools/list":
        return JSONResponse({"jsonrpc": "2.0", "id": req_id, "result": {"tools": TOOLS}})

    if method == "tools/call":
        tool_name = params.get("name")
        tool_args = params.get("arguments") or {}
        stored = (_session_tokens.get(session_key) if session_key else None) or _latest_token
        effective_auth = authorization or (f"Bearer {stored}" if stored else None)
        logger.info("MCP tools/call tool=%s session=%s has_auth=%s", tool_name, session_key, bool(effective_auth))
        try:
            data = await _execute_tool(tool_name, tool_args, effective_auth)
            return JSONResponse(
                {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "result": {
                        "content": [{"type": "text", "text": json.dumps(data, default=str)}]
                    },
                }
            )
        except Exception as exc:
            logger.warning("MCP tool error tool=%s err=%s", tool_name, exc)
            return JSONResponse(
                {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "result": {
                        "content": [{"type": "text", "text": json.dumps({"error": str(exc)})}],
                        "isError": True,
                    },
                }
            )

    return JSONResponse(
        {
            "jsonrpc": "2.0",
            "id": req_id,
            "error": {"code": -32601, "message": f"Method not found: {method}"},
        }
    )
