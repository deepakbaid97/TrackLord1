"""
Agent

High-level API for managing Agora Conversational AI Agents.
"""
import logging
import os
import time
from typing import Any, Dict, Optional

import httpx
from agora_agent import Area, AsyncAgora
from agora_agent.agentkit import Agent as AgoraAgent
from agora_agent.agentkit.vendors import DeepgramSTT, MiniMaxTTS, OpenAI
from agora_agent.types.mcp_server import McpServer
from tracklord_mcp import set_session_token

logger = logging.getLogger("uvicorn.error")

TRACKLORD_PROMPT = """You are TrackLord AI, a concise, helpful manufacturing shop-floor voice copilot.

You understand English, Hindi (including Devanagari transcripts), and Hinglish (mixed Hindi + English).
CRITICAL RULES:
1. SPOKEN REPLIES: Always write your spoken responses ONLY in English or Romanized Hinglish using standard English/Latin letters (for example: "Lot 412 ke 322 pieces Washing mein move ho gaye hain"). NEVER output Devanagari script in your spoken reply so the TTS voice pronounces every word clearly.
2. TOOL CALL ARGUMENTS: Always pass tool arguments (`lotNumber`, `toStepName`, `fromStepName`, `materialNameOrLot`, `search`) in English/Latin letters! If the user's speech is transcribed in Hindi Devanagari (e.g. "वॉशिंग", "स्टिचिंग", "कटिंग", "कॉटन", "शर्ट", "न्यू 001"), transliterate or translate it to English ("Washing", "Stitching", "Cutting", "Cotton", "shirt", "NEW-001") when calling tools.
3. Always use your TrackLord tools to read live data or perform actions—never guess lot numbers, quantities, or step names.
4. Keep spoken replies to 1 or 2 short, clear sentences suitable for a noisy shop floor.
"""

DEFAULT_GREETING = "TrackLord AI ready. Ask me about your lots, dashboard, or say a step movement."


async def _resolve_public_mcp_url() -> Optional[str]:
    """Return PUBLIC_MCP_URL from env or auto-detect a running local ngrok tunnel."""
    configured = os.getenv("PUBLIC_MCP_URL", "").strip()
    if configured:
        return configured.rstrip("/")
    try:
        async with httpx.AsyncClient(timeout=1.5) as http:
            resp = await http.get("http://127.0.0.1:4040/api/tunnels")
            if resp.status_code == 200:
                for tunnel in resp.json().get("tunnels", []):
                    pub = tunnel.get("public_url", "")
                    if pub.startswith("https://"):
                        return pub.rstrip("/")
    except Exception:
        pass
    return None


class Agent:
    """
    High-level wrapper for Agora Conversational AI Agent operations.
    
    Uses AgentSession for full lifecycle management (start/stop),
    which handles Token007 authentication automatically.
    """
    
    def __init__(self):
        self.app_id = os.getenv("AGORA_APP_ID")
        self.app_certificate = os.getenv("AGORA_APP_CERTIFICATE")
        self.greeting = DEFAULT_GREETING

        if not self.app_id or not self.app_certificate:
            raise ValueError("AGORA_APP_ID and AGORA_APP_CERTIFICATE are required")

        self.client = AsyncAgora(
            area=Area.US,
            app_id=self.app_id,
            app_certificate=self.app_certificate,
        )

        # Track active sessions by agent_id
        self._sessions: Dict[str, Any] = {}

    async def start(
        self,
        channel_name: str,
        agent_uid: int,
        user_uid: int,
        output_audio_codec: Optional[str] = None,
        tracklord_token: Optional[str] = None,
        language: Optional[str] = "hi",
    ) -> Dict[str, Any]:
        """Start agent with TrackLord MCP tools when a public MCP URL is available."""
        if not channel_name or not str(channel_name).strip():
            raise ValueError("channel_name is required and cannot be empty")
        if agent_uid <= 0:
            raise ValueError("agent_uid is required and cannot be empty")
        if user_uid <= 0:
            raise ValueError("user_uid is required and cannot be empty")

        mcp_servers = None
        public_url = await _resolve_public_mcp_url()
        if public_url:
            set_session_token(channel_name, tracklord_token)
            headers = {"Authorization": f"Bearer {tracklord_token}"} if tracklord_token else None
            mcp_servers = [
                McpServer(
                    name="tracklord",
                    endpoint=f"{public_url}/mcp/{channel_name}",
                    transport="streamable_http",
                    headers=headers,
                    timeout_ms=8000,
                )
            ]
            logger.info("Attaching TrackLord MCP server: %s/mcp/%s (token=%s)", public_url, channel_name, bool(tracklord_token))
        else:
            logger.warning("No PUBLIC_MCP_URL or ngrok tunnel detected; starting agent without TrackLord tools.")

        # Default managed path: DeepgramSTT + OpenAI + MiniMaxTTS.
        llm = OpenAI(
            model="gpt-4o-mini",
            greeting_message=self.greeting,
            failure_message="Please wait a moment.",
            max_history=15,
            max_tokens=1024,
            temperature=0.7,
            top_p=0.95,
            mcp_servers=mcp_servers,
        )
        # Deepgram nova-2 supports Hindi/Hinglish ('hi') and Indian English ('en-IN'); nova-3 does not support Hindi.
        lang_code = (language or "hi").strip()
        if lang_code.lower().startswith("hi"):
            stt = DeepgramSTT(model="nova-2", language="hi")
        else:
            stt = DeepgramSTT(model="nova-2", language="en-IN")
        tts = MiniMaxTTS(
            model="speech_2_6_turbo",
            voice_id="English_captivating_female1",
        )

        # Optional BYOK example: replace the STT block above and set DEEPGRAM_API_KEY.
        # stt = DeepgramSTT(api_key=os.getenv("DEEPGRAM_API_KEY"), model="nova-3", language="en")

        # Optional BYOK example: replace the LLM block above and set OPENAI_API_KEY.
        # llm = OpenAI(
        #     api_key=os.getenv("OPENAI_API_KEY"),
        #     model="gpt-4o-mini",
        #     greeting_message="Hello! I am your AI assistant. How can I help you?",
        #     failure_message="I'm sorry, I'm having trouble processing your request.",
        #     max_history=15,
        #     max_tokens=1024,
        #     temperature=0.7,
        #     top_p=0.95,
        # )

        # Optional BYOK example: replace the TTS block above and set ELEVENLABS_API_KEY.
        # from agora_agent.agentkit.vendors import ElevenLabsTTS
        # tts = ElevenLabsTTS(
        #     key=os.getenv("ELEVENLABS_API_KEY"),
        #     model_id="eleven_flash_v2_5",
        #     voice_id=os.getenv("ELEVENLABS_VOICE_ID", "pNInz6obpgDQGcFmaJgB"),
        # )

        parameters = {
            "audio_scenario": "chorus",  # web client → ultra-low-latency chorus profile
            "data_channel": "rtm",
            "enable_error_message": True,
            "enable_metrics": True,
        }
        if isinstance(output_audio_codec, str) and output_audio_codec.strip():
            parameters["output_audio_codec"] = output_audio_codec.strip()

        agora_agent = AgoraAgent(
            client=self.client,
            instructions=TRACKLORD_PROMPT,
            greeting=self.greeting,
            failure_message="Please wait a moment.",
            max_history=50,
            turn_detection={
                "config": {
                    "speech_threshold": 0.5,
                    "start_of_speech": {
                        "mode": "vad",
                        "vad_config": {
                            "interrupt_duration_ms": 160,
                            "prefix_padding_ms": 300,
                        },
                    },
                    "end_of_speech": {
                        "mode": "vad",
                        "vad_config": {
                            "silence_duration_ms": 480,
                        },
                    },
                },
            },
            advanced_features={"enable_rtm": True, "enable_tools": True},
            parameters=parameters,
        )
        
        agora_agent = (
            agora_agent
            .with_stt(stt)
            .with_llm(llm)
            .with_tts(tts)
        )

        session = agora_agent.create_async_session(
            channel=channel_name,
            agent_uid=str(agent_uid),
            remote_uids=[str(user_uid)],
            enable_string_uid=False,
            idle_timeout=30,
            expires_in=3600,
        )

        logger.info(
            "Starting Agora agent channel=%s agent_uid=%s user_uid=%s",
            channel_name,
            agent_uid,
            user_uid,
        )

        try:
            agent_id = await session.start()
        except Exception:
            logger.exception(
                "Failed to start Agora agent channel=%s agent_uid=%s user_uid=%s",
                channel_name,
                agent_uid,
                user_uid,
            )
            raise

        # Save session for later stop
        self._sessions[agent_id] = session

        logger.info(
            "Started Agora agent agent_id=%s channel=%s agent_uid=%s user_uid=%s",
            agent_id,
            channel_name,
            agent_uid,
            user_uid,
        )
        
        return {
            "agent_id": agent_id,
            "channel_name": channel_name,
            "status": "started",
        }

    async def stop(self, agent_id: str) -> None:
        """Stop a running agent. Falls back to the stateless client path."""
        if not agent_id or not str(agent_id).strip():
            raise ValueError("agent_id is required and cannot be empty")

        session = self._sessions.pop(agent_id, None)
        if session:
            try:
                await session.stop()
                logger.info("Stopped Agora agent from active session agent_id=%s", agent_id)
                return
            except Exception:
                # Fall back to the stateless SDK path if the in-memory session is stale.
                logger.warning(
                    "Failed to stop Agora agent from active session; falling back to client.stop_agent agent_id=%s",
                    agent_id,
                    exc_info=True,
                )

        logger.info("Stopping Agora agent through client.stop_agent agent_id=%s", agent_id)
        await self.client.stop_agent(agent_id)
