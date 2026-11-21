<script>
	import { onDestroy } from 'svelte';
	import { authStore } from '$lib/stores/authStore.js';
	import {
		getVoiceConfig,
		getVoiceEvents,
		startVoiceAgent,
		stopVoiceAgent
	} from '$lib/services/voiceAgentService.js';

	let { onMutation = undefined } = $props();

	/** @type {'idle' | 'connecting' | 'connected' | 'error'} */
	let callStatus = $state('idle');
	/** @type {'idle' | 'listening' | 'thinking' | 'speaking' | 'silent'} */
	let agentState = $state('idle');
	/** @type {'hi' | 'en-IN'} */
	let voiceLang = $state('hi');
	let isMuted = $state(false);
	let isDrawerOpen = $state(false);
	let errorMessage = $state('');
	let mutationToast = $state('');
	/** @type {Array<{ id: string, role: 'user' | 'agent', text: string }>} */
	let messages = $state([]);

	let rtcClient = null;
	let rtmClient = null;
	let voiceAI = null;
	let micTrack = null;
	let activeAgentId = '';
	let pollTimer = null;
	let toastTimer = null;
	let lastMutationTs = Date.now() / 1000;

	const stateLabel = $derived(
		callStatus === 'connecting'
			? 'Connecting...'
			: callStatus === 'error'
				? 'Error'
				: callStatus === 'idle'
					? 'Voice Copilot'
					: agentState === 'speaking'
						? 'Speaking...'
						: agentState === 'thinking'
							? 'Thinking...'
							: 'Listening...'
	);

	function normalizeSpacing(text) {
		return text
			.replace(/([.!?])([A-Za-z])/g, '$1 $2')
			.replace(/,([A-Za-z])/g, ', $1')
			.replace(/\s{2,}/g, ' ')
			.trim();
	}

	async function startCall() {
		if (callStatus === 'connecting' || callStatus === 'connected') return;
		callStatus = 'connecting';
		errorMessage = '';
		isDrawerOpen = true;
		messages = [];

		try {
			const [{ default: AgoraRTC }, { default: AgoraRTM }, { AgoraVoiceAI, AgoraVoiceAIEvents }] =
				await Promise.all([
					import('agora-rtc-sdk-ng'),
					import('agora-rtm'),
					import('agora-agent-client-toolkit')
				]);

			const config = await getVoiceConfig();
			const { app_id: appId, token, uid, channel_name: channelName, agent_uid: agentUid } = config;
			const numericUid = Number.parseInt(uid, 10);
			const numericAgentUid = Number.parseInt(agentUid, 10);

			rtcClient = AgoraRTC.createClient({ mode: 'rtc', codec: 'vp8' });
			try {
				AgoraRTC.setParameter('ENABLE_AUDIO_PTS', true);
			} catch {
				// ignore if unsupported
			}
			rtcClient.on('user-published', async (user, mediaType) => {
				await rtcClient.subscribe(user, mediaType);
				if (mediaType === 'audio') {
					user.audioTrack?.play();
				}
			});

			rtmClient = new AgoraRTM.RTM(appId, String(uid));
			await rtmClient.login({ token });

			voiceAI = await AgoraVoiceAI.init({
				rtcEngine: rtcClient,
				rtmConfig: { rtmEngine: rtmClient }
			});

			voiceAI.on(AgoraVoiceAIEvents.AGENT_STATE_CHANGED, (_agentUserId, event) => {
				if (event?.state) {
					agentState = event.state;
				}
			});

			voiceAI.on(AgoraVoiceAIEvents.TRANSCRIPT_UPDATED, (transcript) => {
				if (!Array.isArray(transcript)) return;
				messages = transcript
					.filter((item) => typeof item.text === 'string' && item.text.trim().length > 0)
					.map((item, idx) => ({
						id: `${item.turn_id ?? idx}-${item.uid}`,
						role: String(item.uid) === '0' || String(item.uid) === String(uid) ? 'user' : 'agent',
						text: normalizeSpacing(item.text)
					}));
			});

			await rtcClient.join(appId, channelName, token, numericUid);
			micTrack = await AgoraRTC.createMicrophoneAudioTrack();
			await rtcClient.publish([micTrack]);

			voiceAI.subscribeMessage(channelName);
			activeAgentId = await startVoiceAgent(
				channelName,
				numericAgentUid,
				numericUid,
				$authStore.token,
				voiceLang
			);
			callStatus = 'connected';
			agentState = 'listening';
			lastMutationTs = Date.now() / 1000;
			startEventPolling();
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to start voice session';
			callStatus = 'error';
			await cleanup();
		}
	}

	function startEventPolling() {
		stopEventPolling();
		pollTimer = setInterval(async () => {
			try {
				const evt = await getVoiceEvents();
				if (evt && evt.timestamp > lastMutationTs) {
					lastMutationTs = evt.timestamp;
					if (evt.summary) {
						mutationToast = evt.summary;
						if (toastTimer) clearTimeout(toastTimer);
						toastTimer = setTimeout(() => {
							mutationToast = '';
						}, 5000);
					}
					onMutation?.(evt);
				}
			} catch {
				// ignore transient poll errors
			}
		}, 1500);
	}

	function stopEventPolling() {
		if (pollTimer) {
			clearInterval(pollTimer);
			pollTimer = null;
		}
	}

	async function toggleMute() {
		if (!micTrack) return;
		isMuted = !isMuted;
		await micTrack.setMuted(isMuted);
	}

	async function cleanup() {
		stopEventPolling();
		const agentIdToStop = activeAgentId;
		activeAgentId = '';

		try {
			if (agentIdToStop) await stopVoiceAgent(agentIdToStop);
		} catch (err) {
			console.warn('Error stopping voice agent:', err);
		}

		try {
			voiceAI?.unsubscribe?.();
		} catch {
			// ignore
		}
		voiceAI = null;

		try {
			micTrack?.stop?.();
			micTrack?.close?.();
		} catch {
			// ignore
		}
		micTrack = null;

		try {
			await rtcClient?.leave?.();
		} catch {
			// ignore
		}
		rtcClient = null;

		try {
			await rtmClient?.logout?.();
		} catch {
			// ignore
		}
		rtmClient = null;
		isMuted = false;
	}

	async function endCall() {
		await cleanup();
		callStatus = 'idle';
		agentState = 'idle';
	}

	onDestroy(() => {
		cleanup();
	});
</script>

<!-- Floating TrackLord Voice Copilot -->
<div class="fixed bottom-24 right-4 z-50 flex flex-col items-end gap-2">
	{#if isDrawerOpen}
		<div
			class="w-80 sm:w-96 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 shadow-2xl overflow-hidden"
		>
			<!-- Drawer Header -->
			<div
				class="flex items-center justify-between px-4 py-3 border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/60"
			>
				<div class="flex items-center gap-2">
					<span
						class="h-2.5 w-2.5 rounded-full {callStatus === 'connected'
							? agentState === 'speaking'
								? 'bg-emerald-500 animate-pulse'
								: 'bg-blue-500 animate-pulse'
							: callStatus === 'connecting'
								? 'bg-amber-500 animate-ping'
								: callStatus === 'error'
									? 'bg-red-500'
									: 'bg-slate-400'}"
					></span>
					<span class="text-sm font-semibold text-slate-900 dark:text-slate-100">
						{stateLabel}
					</span>
				</div>
				<div class="flex items-center gap-1.5">
					<button
						type="button"
						disabled={callStatus === 'connected' || callStatus === 'connecting'}
						onclick={() => (voiceLang = voiceLang === 'hi' ? 'en-IN' : 'hi')}
						title="Switch STT language before starting call"
						class="text-[11px] font-medium px-2 py-0.5 rounded border border-slate-300 dark:border-slate-700 text-slate-700 dark:text-slate-200 disabled:opacity-60"
					>
						{voiceLang === 'hi' ? '🇮🇳 HI/Hinglish' : '🇬🇧 EN-IN'}
					</button>
					<button
						type="button"
						onclick={() => (isDrawerOpen = false)}
						class="text-xs text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 px-2 py-1 rounded"
					>
						Minimize
					</button>
				</div>
			</div>

			<!-- Transcript Area -->
			<div class="h-60 overflow-y-auto p-3 space-y-2 text-sm bg-white dark:bg-slate-900">
				{#if mutationToast}
					<div class="rounded-lg bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800 px-3 py-2 text-emerald-800 dark:text-emerald-200 text-xs font-medium">
						✓ {mutationToast}
					</div>
				{/if}
				{#if errorMessage}
					<div class="rounded-lg bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-800 p-2.5 text-red-700 dark:text-red-300 text-xs">
						{errorMessage}
					</div>
				{:else if messages.length === 0}
					<p class="text-xs text-slate-400 dark:text-slate-500 text-center mt-16">
						{callStatus === 'connected'
							? 'Listening — ask about lots, steps, or inventory...'
							: 'Click Start Voice to talk to TrackLord AI.'}
					</p>
				{:else}
					{#each messages as msg (msg.id)}
						<div class="flex {msg.role === 'user' ? 'justify-end' : 'justify-start'}">
							<div
								class="max-w-[85%] rounded-xl px-3 py-1.5 text-xs leading-relaxed {msg.role === 'user'
									? 'bg-blue-600 text-white'
									: 'bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-100'}"
							>
								{msg.text}
							</div>
						</div>
					{/each}
				{/if}
			</div>

			<!-- Controls -->
			<div
				class="flex items-center justify-between gap-2 px-3 py-2.5 border-t border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40"
			>
				{#if callStatus === 'connected' || callStatus === 'connecting'}
					<button
						type="button"
						onclick={toggleMute}
						disabled={callStatus !== 'connected'}
						class="flex-1 rounded-lg px-3 py-1.5 text-xs font-medium border border-slate-300 dark:border-slate-600 text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 disabled:opacity-50"
					>
						{isMuted ? 'Unmute Mic' : 'Mute Mic'}
					</button>
					<button
						type="button"
						onclick={endCall}
						class="flex-1 rounded-lg px-3 py-1.5 text-xs font-medium bg-red-600 text-white hover:bg-red-700"
					>
						End Call
					</button>
				{:else}
					<button
						type="button"
						onclick={startCall}
						class="w-full rounded-lg px-3 py-2 text-xs font-semibold bg-blue-600 text-white hover:bg-blue-700"
					>
						Start Voice Session
					</button>
				{/if}
			</div>
		</div>
	{/if}

	<!-- Floating Trigger Pill -->
	<button
		type="button"
		onclick={() => {
			if (callStatus === 'idle' || callStatus === 'error') {
				startCall();
			} else {
				isDrawerOpen = !isDrawerOpen;
			}
		}}
		class="flex items-center gap-2 rounded-full px-4 py-2.5 text-sm font-semibold shadow-lg transition-all {callStatus === 'connected'
			? 'bg-emerald-600 hover:bg-emerald-700 text-white'
			: callStatus === 'connecting'
				? 'bg-amber-500 text-white'
				: 'bg-blue-600 hover:bg-blue-700 text-white'}"
	>
		<span class="text-base leading-none">{callStatus === 'connected' ? '🎙️' : '✨'}</span>
		<span>{callStatus === 'connected' ? stateLabel : 'Voice Copilot'}</span>
	</button>
</div>
