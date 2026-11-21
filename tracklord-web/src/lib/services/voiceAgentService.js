const VOICE_API_BASE = '/voice-api';

/**
 * Fetch RTC/RTM config and tokens from the Agora agent backend.
 * Adapted from agora-agent-reference/web/src/services/api.ts
 */
export async function getVoiceConfig(options = {}) {
	const params = new URLSearchParams();
	if (options.channel) params.set('channel', options.channel);
	if (options.uid !== undefined && options.uid !== '') params.set('uid', String(options.uid));

	const query = params.toString();
	const response = await fetch(`${VOICE_API_BASE}/get_config${query ? `?${query}` : ''}`);
	if (!response.ok) {
		const err = await response.json().catch(() => ({}));
		throw new Error(err.detail || `HTTP ${response.status}`);
	}
	const result = await response.json();
	if (result.code !== 0 || !result.data) {
		throw new Error(result.msg || 'Failed to get voice configuration');
	}
	return result.data;
}

/**
 * Spawn an Agora ConvoAI agent into the given RTC channel.
 */
export async function startVoiceAgent(channelName, rtcUid, userUid, tracklordToken = null, language = 'hi') {
	const response = await fetch(`${VOICE_API_BASE}/startAgent`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({ channelName, rtcUid, userUid, tracklordToken, language })
	});
	if (!response.ok) {
		const err = await response.json().catch(() => ({}));
		throw new Error(err.detail || `HTTP ${response.status}`);
	}
	const result = await response.json();
	if (result.code !== 0 || !result.data?.agent_id) {
		throw new Error(result.msg || 'Failed to start voice agent');
	}
	return result.data.agent_id;
}

/**
 * Stop an active Agora ConvoAI agent.
 */
export async function stopVoiceAgent(agentId) {
	if (!agentId) return;
	const response = await fetch(`${VOICE_API_BASE}/stopAgent`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({ agentId })
	});
	if (!response.ok) {
		const err = await response.json().catch(() => ({}));
		throw new Error(err.detail || `HTTP ${response.status}`);
	}
}

/**
 * Poll the latest voice-triggered TrackLord mutation for live UI refresh.
 */
export async function getVoiceEvents() {
	const response = await fetch(`${VOICE_API_BASE}/voice_events`);
	if (!response.ok) return null;
	return response.json();
}
