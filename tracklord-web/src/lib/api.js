import { authStore } from './stores/authStore.js';

const API_BASE_URL = import.meta.env.VITE_API_BASE || '/api';

/**
 * Custom fetch wrapper that automatically injects Authorization Bearer token header
 * and handles 401 Unauthorized responses.
 * @param {string} url 
 * @param {RequestInit} [options] 
 */
async function authFetch(url, options = {}) {
  const token = authStore.getToken();
  const headers = new Headers(options.headers || {});

  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(url, { ...options, headers });

  if (response.status === 401) {
    authStore.logout();
    authStore.openModal();
  }

  return response;
}

/** @param {Response} response */
async function handleResponse(response) {
  if (!response.ok) {
    let errorMessage = `HTTP Error ${response.status}: ${response.statusText}`;
    try {
      const errorData = await response.json();
      if (errorData.message) errorMessage = errorData.message;
      else if (typeof errorData === 'string') errorMessage = errorData;
    } catch {
      // Use fallback error message if JSON parsing fails
    }
    throw new Error(errorMessage);
  }
  if (response.status === 204) return null;
  return await response.json();
}

// ─── TTL In-Memory Request Cache ───────────────────────────────────────────────
// Prevents redundant re-fetches of identical reference data across page navigations.
// Cache entries are keyed by a string identifier and auto-expire after a configurable TTL.

/** @type {Map<string, { data: any, expiresAt: number, inflight?: Promise<any> }>} */
const _cache = new Map();

/**
 * Returns cached data if still valid, otherwise calls fetchFn, caches the result,
 * and deduplicates concurrent in-flight requests for the same key.
 * @param {string} key - Cache key
 * @param {() => Promise<any>} fetchFn - Function that fetches fresh data
 * @param {number} ttlMs - Time-to-live in milliseconds
 * @returns {Promise<any>}
 */
async function cachedFetch(key, fetchFn, ttlMs) {
  const entry = _cache.get(key);
  if (entry) {
    // Return cached data if not expired
    if (Date.now() < entry.expiresAt) return entry.data;
    // Deduplicate concurrent in-flight requests
    if (entry.inflight) return entry.inflight;
  }

  const promise = fetchFn().then(data => {
    _cache.set(key, { data, expiresAt: Date.now() + ttlMs });
    return data;
  }).catch(err => {
    // Don't cache errors — delete the inflight marker so next call retries
    const current = _cache.get(key);
    if (current?.inflight === promise) _cache.delete(key);
    throw err;
  });

  _cache.set(key, { data: null, expiresAt: 0, inflight: promise });
  return promise;
}

/**
 * Invalidates one or all cache entries.
 * @param {string} [key] - Specific cache key to invalidate. Omit to clear all.
 */
export function invalidateCache(key) {
  if (key) {
    _cache.delete(key);
  } else {
    _cache.clear();
  }
}

// Cache key constants
const CK_STEPS = 'manufacturing_steps';
const CK_LOTS = 'production_lots';
const CK_MATERIALS = 'material_lots';
const CK_CLIENTS = 'clients';

// 1. Fetch Manufacturing Steps (cached 60s)
export async function getManufacturingSteps() {
  return cachedFetch(CK_STEPS, async () => {
    const response = await authFetch(`${API_BASE_URL}/ManufacturingSteps`);
    return await handleResponse(response);
  }, 60_000);
}

// 1b. Create Manufacturing Step
/** @param {any} data */
export async function createManufacturingStep(data) {
  const response = await authFetch(`${API_BASE_URL}/ManufacturingSteps`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_STEPS);
  return result;
}

// 1c. Update Manufacturing Step
/**
 * @param {string} id
 * @param {any} data
 */
export async function updateManufacturingStep(id, data) {
  const response = await authFetch(`${API_BASE_URL}/ManufacturingSteps/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_STEPS);
  return result;
}

// 1d. Reorder Manufacturing Steps
/**
 * @param {Array<{id: string, sequenceOrder: number}>} steps
 */
export async function reorderManufacturingSteps(steps) {
  const response = await authFetch(`${API_BASE_URL}/ManufacturingSteps/reorder`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ steps })
  });
  const result = await handleResponse(response);
  invalidateCache(CK_STEPS);
  return result;
}

// 2. Fetch all Production Lots (cached 15s)
export async function getProductionLots() {
  return cachedFetch(CK_LOTS, async () => {
    const response = await authFetch(`${API_BASE_URL}/ProductionLots`);
    return await handleResponse(response);
  }, 15_000);
}

// 3. Fetch all Raw Material Lots (cached 30s)
export async function getMaterialLots() {
  return cachedFetch(CK_MATERIALS, async () => {
    const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/lots`);
    return await handleResponse(response);
  }, 30_000);
}

// 3b. Fetch Consumption History for a specific Raw Material Lot
/** @param {string} materialLotId */
export async function getMaterialLotHistory(materialLotId) {
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/lots/${materialLotId}/history`);
  return await handleResponse(response);
}

// 4. Fetch Lot WIP summary
/** @param {string} lotId */
export async function getLotWip(lotId) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots/${lotId}/wip`);
  return await handleResponse(response);
}

// 5. Fetch Lot Journey & Movements History
/** @param {string} lotId */
export async function getLotJourney(lotId) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots/${lotId}/journey`);
  return await handleResponse(response);
}

// 6. Fetch Material Traceability for Lot
/** @param {string} lotId */
export async function getMaterialTraceability(lotId) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots/${lotId}/material-traceability`);
  return await handleResponse(response);
}

// 6b. Delete Production Lot (reverses material consumption and deletes movements)
/** @param {string} lotId */
export async function deleteProductionLot(lotId) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots/${lotId}`, {
    method: 'DELETE'
  });
  const result = await handleResponse(response);
  invalidateCache(CK_LOTS);
  invalidateCache(CK_MATERIALS);
  return result;
}

// 7. Create New Production Lot
/** @param {any} data */
export async function createProductionLot(data) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_LOTS);
  return result;
}

// 7b. Import Production Lots
/** @param {any} data */
export async function importProductionLots(data) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots/import`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_LOTS);
  return result;
}

// 8. Move Lot Quantity Between Steps
/** @param {any} data */
export async function moveLot(data) {
  const response = await authFetch(`${API_BASE_URL}/Movements`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_LOTS);
  return result;
}

// 9. Create New Raw Material Lot
/** @param {any} data */
export async function createMaterialLot(data) {
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/lots`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_MATERIALS);
  return result;
}

// 9a. Import Raw Material Lots
/** @param {any} data */
export async function importMaterialLots(data) {
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/lots/import`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_MATERIALS);
  return result;
}

// 9b. Update Raw Material Lot
/**
 * @param {string} id
 * @param {any} data
 */
export async function updateMaterialLot(id, data) {
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/lots/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_MATERIALS);
  return result;
}

// 9c. Delete Raw Material Lot
/** @param {string} id */
export async function deleteMaterialLot(id) {
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/lots/${id}`, {
    method: 'DELETE'
  });
  const result = await handleResponse(response);
  invalidateCache(CK_MATERIALS);
  return result;
}

// 10. Consume Raw Material for Production Lot
/** @param {any} data */
export async function consumeMaterial(data) {
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/consume`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_MATERIALS);
  return result;
}

// 11. Reject Items at Step
/**
 * @param {string} lotId
 * @param {any} data
 */
export async function rejectItems(lotId, data) {
  const response = await authFetch(`${API_BASE_URL}/ProductionLots/${lotId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_LOTS);
  return result;
}

// 12. Reverse Movement
/**
 * @param {string} movementId
 * @param {any} data
 */
export async function reverseMovement(movementId, data) {
  const response = await authFetch(`${API_BASE_URL}/Movements/${movementId}/reverse`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_LOTS);
  return result;
}

// 13. Edit Raw Material Consumption
/**
 * @param {string} consumptionId
 * @param {any} data
 */
export async function editMaterialConsumption(consumptionId, data) {
  if (!consumptionId || consumptionId === 'undefined') {
    throw new Error('Material Consumption ID is missing or invalid.');
  }
  const response = await authFetch(`${API_BASE_URL}/MaterialConsumptions/consume/${consumptionId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 14. Fetch All Clients (cached 60s)
export async function getClients() {
  return cachedFetch(CK_CLIENTS, async () => {
    const response = await authFetch(`${API_BASE_URL}/Clients`);
    return await handleResponse(response);
  }, 60_000);
}

// 15. Create New Client
/** @param {any} data */
export async function createClient(data) {
  const response = await authFetch(`${API_BASE_URL}/Clients`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_CLIENTS);
  return result;
}

// 16. Fetch Dashboard Overview
export async function getDashboardOverview() {
  const response = await authFetch(`${API_BASE_URL}/Dashboards/overview`);
  return await handleResponse(response);
}

// 17. Fetch All Registered Users
export async function getUsers() {
  const response = await authFetch(`${API_BASE_URL}/Auth/users`);
  return await handleResponse(response);
}

// 18. Create New User (Admin)
/** @param {any} data */
export async function createUser(data) {
  const response = await authFetch(`${API_BASE_URL}/Auth/users`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 19. Update User Profile (Name / Role)
/**
 * @param {string} userId
 * @param {any} data
 */
export async function updateUser(userId, data) {
  const response = await authFetch(`${API_BASE_URL}/Auth/users/${userId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 20. Fetch Current Tenant Settings
export async function getTenantSettings() {
  const response = await authFetch(`${API_BASE_URL}/Tenants/current/settings`);
  return await handleResponse(response);
}

// 21. Update Current Tenant Settings
/** @param {{ clientMode?: string, featureOverrides?: Record<string, boolean> }} data */
export async function updateTenantSettings(data) {
  const response = await authFetch(`${API_BASE_URL}/Tenants/current/settings`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 22. Batch Fetch Lot Journeys (eliminates N+1 individual journey calls with seamless fallback)
/** @param {string[]} lotIds */
export async function getBatchLotJourneys(lotIds) {
  if (!lotIds || lotIds.length === 0) return {};
  try {
    const response = await authFetch(`${API_BASE_URL}/ProductionLots/journeys/batch`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ lotIds })
    });
    return await handleResponse(response);
  } catch (err) {
    console.warn('Batch lot journey fetch failed, falling back to individual fetching:', err);
    /** @type {Record<string, any>} */
    const fallbackMap = {};
    await Promise.all(
      lotIds.map(async (id) => {
        try {
          const journey = await getLotJourney(id);
          if (journey) {
            fallbackMap[id] = journey;
            fallbackMap[id.toLowerCase()] = journey;
          }
        } catch {
          // ignore single failure
        }
      })
    );
    return fallbackMap;
  }
}

// ─── Role & Permission Management (RBAC) ──────────────────────────────────────
const CK_ROLES = 'roles';

// 23. Fetch All Roles in Current Tenant (cached 30s)
export async function getRoles() {
  return cachedFetch(CK_ROLES, async () => {
    const response = await authFetch(`${API_BASE_URL}/Roles`);
    return await handleResponse(response);
  }, 30_000);
}

// 24. Fetch Specific Role Details by ID
/** @param {string} roleId */
export async function getRoleById(roleId) {
  const response = await authFetch(`${API_BASE_URL}/Roles/${roleId}`);
  return await handleResponse(response);
}

// 25. Create New Custom Role
/** @param {{ name: string, description?: string, permissionsJson?: string }} data */
export async function createRole(data) {
  const response = await authFetch(`${API_BASE_URL}/Roles`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_ROLES);
  return result;
}

// 26. Update Role Details & Permissions
/**
 * @param {string} roleId
 * @param {{ name: string, description?: string, permissionsJson?: string, isActive?: boolean }} data
 */
export async function updateRole(roleId, data) {
  const response = await authFetch(`${API_BASE_URL}/Roles/${roleId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const result = await handleResponse(response);
  invalidateCache(CK_ROLES);
  return result;
}

// 27. Delete Custom Role
/** @param {string} roleId */
export async function deleteRole(roleId) {
  const response = await authFetch(`${API_BASE_URL}/Roles/${roleId}`, {
    method: 'DELETE'
  });
  const result = await handleResponse(response);
  invalidateCache(CK_ROLES);
  return result;
}

// 28. Get System Permission Features Catalog
export async function getPermissionCatalog() {
  const response = await authFetch(`${API_BASE_URL}/Roles/permissions/catalog`);
  return await handleResponse(response);
}

// 29. Assign Role to User
/** @param {string} userId @param {string} roleId */
export async function assignRole(userId, roleId) {
  const response = await authFetch(`${API_BASE_URL}/Roles/assign`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId, roleId })
  });
  return await handleResponse(response);
}

