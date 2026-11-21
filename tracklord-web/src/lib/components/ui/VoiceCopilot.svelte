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
	let textInput = $state('');
	/** @type {Array<{ kind: 'message', id: string, role: 'user' | 'agent', text: string, ts: number }>} */
	let transcriptMessages = $state([]);
	/** @type {Array<{ kind: 'message', id: string, role: 'user', text: string, ts: number }>} */
	let manualMessages = $state([]);
	/** @type {Array<{ kind: 'card', id: string, text: string, isMutation: boolean, ts: number }>} */
	let toolCards = $state([]);
	let chatContainer = $state(null);

	let rtcClient = null;
	let rtmClient = null;
	let voiceAI = null;
	let micTrack = null;
	let activeAgentId = '';
	let activeAgentUid = '';
	let chatMessageTypes = null;
	let pollTimer = null;
	let toastTimer = null;
	let lastMutationTs = Date.now() / 1000;
	const messageTimestamps = new Map();

	const quickPrompts = [
		{ label: '📊 Summary', text: "Give me today's production summary" },
		{ label: '📦 Lot 412 Status', text: 'Which step is lot 412 in?' },
		{ label: '➡️ Move 412 Next', text: 'Move lot 412 to the next step' }
	];

	const chatTimeline = $derived.by(() => {
		const transcriptUserTexts = new Set(
			transcriptMessages
				.filter((m) => m.role === 'user')
				.map((m) => m.text.trim().toLowerCase())
		);
		const pendingManual = manualMessages.filter(
			(m) => !transcriptUserTexts.has(m.text.trim().toLowerCase())
		);
		return [...transcriptMessages, ...pendingManual, ...toolCards].sort((a, b) => a.ts - b.ts);
	});

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

	function scrollToBottom() {
		setTimeout(() => {
			if (chatContainer) {
				chatContainer.scrollTop = chatContainer.scrollHeight;
			}
		}, 20);
	}

	async function startCall() {
		if (callStatus === 'connecting' || callStatus === 'connected') return;
		callStatus = 'connecting';
		errorMessage = '';
		isDrawerOpen = true;
		transcriptMessages = [];
		manualMessages = [];
		toolCards = [];
		messageTimestamps.clear();

		try {
			const [
				{ default: AgoraRTC },
				{ default: AgoraRTM },
				{
					AgoraVoiceAI,
					AgoraVoiceAIEvents,
					ChatMessagePriority,
					ChatMessageType,
					TranscriptHelperMode
				}
			] = await Promise.all([
				import('agora-rtc-sdk-ng'),
				import('agora-rtm'),
				import('agora-agent-client-toolkit')
			]);
			chatMessageTypes = { ChatMessagePriority, ChatMessageType };

			const config = await getVoiceConfig();
			const { app_id: appId, token, uid, channel_name: channelName, agent_uid: agentUid } = config;
			const numericUid = Number.parseInt(uid, 10);
			const numericAgentUid = Number.parseInt(agentUid, 10);
			activeAgentUid = String(agentUid);

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
			await rtmClient.subscribe(channelName);

			await rtcClient.join(appId, channelName, token, numericUid);
			micTrack = await AgoraRTC.createMicrophoneAudioTrack();
			await rtcClient.publish([micTrack]);

			voiceAI = await AgoraVoiceAI.init({
				rtcEngine: rtcClient,
				rtmEngine: rtmClient,
				rtmConfig: { rtmEngine: rtmClient },
				renderMode: TranscriptHelperMode.TEXT,
				enableLog: false
			});

			voiceAI.on(AgoraVoiceAIEvents.AGENT_STATE_CHANGED, (_agentUserId, event) => {
				if (event?.state) {
					agentState = event.state;
				}
			});

			voiceAI.on(AgoraVoiceAIEvents.TRANSCRIPT_UPDATED, (transcript) => {
				if (!Array.isArray(transcript)) return;
				transcriptMessages = transcript
					.filter((item) => typeof item.text === 'string' && item.text.trim().length > 0)
					.map((item, idx) => {
						const id = `${item.turn_id ?? idx}-${item.uid}`;
						if (!messageTimestamps.has(id)) {
							messageTimestamps.set(id, Date.now() + idx * 0.01);
						}
						return {
							kind: 'message',
							id,
							role: String(item.uid) === '0' || String(item.uid) === String(uid) ? 'user' : 'agent',
							text: normalizeSpacing(item.text),
							ts: messageTimestamps.get(id)
						};
					});
				scrollToBottom();
			});

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
				const resp = await getVoiceEvents(lastMutationTs);
				const newEvents = Array.isArray(resp?.events)
					? resp.events
					: resp && resp.timestamp > lastMutationTs
						? [resp]
						: [];
				for (const evt of newEvents) {
					if (evt.timestamp > lastMutationTs) {
						lastMutationTs = evt.timestamp;
					}
					if (evt.summary) {
						toolCards = [
							...toolCards,
							{
								kind: 'card',
								id: `tool-${evt.timestamp}`,
								text: evt.summary,
								isMutation: Boolean(evt.isMutation),
								ts: Date.now()
							}
						];
						scrollToBottom();
						if (evt.isMutation) {
							mutationToast = evt.summary;
							if (toastTimer) clearTimeout(toastTimer);
							toastTimer = setTimeout(() => {
								mutationToast = '';
							}, 5000);
						}
					}
					if (evt.isMutation) {
						onMutation?.(evt);
					}
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

	async function sendPromptText(rawText) {
		const trimmed = (rawText || '').trim();
		if (!trimmed) return;
		if (callStatus !== 'connected') {
			await startCall();
		}
		if (!voiceAI || !activeAgentUid || !chatMessageTypes) return;
		textInput = '';
		manualMessages = [
			...manualMessages,
			{
				kind: 'message',
				id: `manual-${Date.now()}`,
				role: 'user',
				text: trimmed,
				ts: Date.now()
			}
		];
		scrollToBottom();
		try {
			await voiceAI.sendText(activeAgentUid, {
				messageType: chatMessageTypes.ChatMessageType.TEXT,
				text: trimmed,
				priority: chatMessageTypes.ChatMessagePriority.INTERRUPTED,
				responseInterruptable: true
			});
		} catch (err) {
			console.warn('sendText failed:', err);
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
			voiceAI?.destroy?.();
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
			<div
				bind:this={chatContainer}
				class="h-64 overflow-y-auto p-3 space-y-2 text-sm bg-white dark:bg-slate-900"
			>
				{#if mutationToast}
					<div class="rounded-lg bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800 px-3 py-2 text-emerald-800 dark:text-emerald-200 text-xs font-medium">
						✓ {mutationToast}
					</div>
				{/if}
				{#if errorMessage}
					<div class="rounded-lg bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-800 p-2.5 text-red-700 dark:text-red-300 text-xs">
						{errorMessage}
					</div>
				{:else if chatTimeline.length === 0}
					<p class="text-xs text-slate-400 dark:text-slate-500 text-center mt-10">
						{callStatus === 'connected'
							? 'Listening — speak or tap a quick command below:'
							: 'Click Start Voice or tap a quick command:'}
					</p>
				{:else}
					{#each chatTimeline as item (item.id)}
						{#if item.kind === 'card'}
							<div class="rounded-xl border px-3 py-2 text-xs font-medium {item.isMutation
								? 'bg-emerald-50/80 dark:bg-emerald-950/40 border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-200'
								: 'bg-amber-50/80 dark:bg-amber-950/40 border-amber-200 dark:border-amber-800 text-amber-900 dark:text-amber-200'}">
								{item.isMutation ? '✓ ' : '📌 '}{item.text}
							</div>
						{:else}
							<div class="flex {item.role === 'user' ? 'justify-end' : 'justify-start'}">
								<div
									class="max-w-[85%] rounded-xl px-3 py-1.5 text-xs leading-relaxed {item.role === 'user'
										? 'bg-blue-600 text-white'
										: 'bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-100'}"
								>
									<span class="block text-[10px] font-semibold opacity-70 mb-0.5">
										{item.role === 'user' ? 'You' : 'TrackLord AI'}
									</span>
									{item.text}
								</div>
							</div>
						{/if}
					{/each}
				{/if}
			</div>

			<!-- Quick Prompt Chips -->
			<div class="flex flex-wrap gap-1.5 px-3 py-2 border-t border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900">
				{#each quickPrompts as qp (qp.label)}
					<button
						type="button"
						onclick={() => sendPromptText(qp.text)}
						class="text-[11px] px-2.5 py-1 rounded-full border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-blue-50 dark:hover:bg-slate-700 transition-colors"
					>
						{qp.label}
					</button>
				{/each}
			</div>

			<!-- Text Command Fallback Input -->
			{#if callStatus === 'connected'}
				<form
					onsubmit={(e) => {
						e.preventDefault();
						sendPromptText(textInput);
					}}
					class="flex items-center gap-1.5 px-3 py-2 border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
				>
					<input
						type="text"
						bind:value={textInput}
						placeholder="Type a command (e.g. Where is lot 412?)..."
						class="flex-1 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-2.5 py-1.5 text-xs text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
					/>
					<button
						type="submit"
						class="rounded-lg bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 text-xs font-medium"
					>
						Send
					</button>
				</form>
			{/if}

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
