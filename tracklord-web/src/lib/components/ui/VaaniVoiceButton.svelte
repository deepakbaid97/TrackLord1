<script>
  import { onDestroy } from 'svelte';
  import { Room, RoomEvent, Track } from 'livekit-client';
  import { createVaaniWebRtcSession } from '$lib/api.js';

  const AGENT_ID_KEY = 'vaani_agent_id';

  let isOpen = $state(false);
  let agentId = $state('');
  let isStarting = $state(false);
  let isConnected = $state(false);
  let errorMessage = $state('');
  /** @type {Room | null} */
  let room = null;
  /** @type {Set<HTMLMediaElement>} */
  let audioElements = new Set();

  if (typeof localStorage !== 'undefined') {
    agentId = localStorage.getItem(AGENT_ID_KEY) || '';
  }

  function removeRemoteAudio() {
    for (const audio of audioElements) {
      audio.remove();
    }
    audioElements.clear();
  }

  function cleanupRoom() {
    removeRemoteAudio();
    room?.removeAllListeners();
    room = null;
    isConnected = false;
  }

  async function endCall() {
    if (room) {
      const activeRoom = room;
      room = null;
      await activeRoom.disconnect();
    }
    cleanupRoom();
    isOpen = false;
    errorMessage = '';
  }

  /** @param {SubmitEvent} event */
  async function startCall(event) {
    event.preventDefault();
    errorMessage = '';

    const normalizedAgentId = agentId.trim();
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(normalizedAgentId)) {
      errorMessage = 'Enter a valid Vaani agent UUID.';
      return;
    }

    isStarting = true;
    try {
      const session = await createVaaniWebRtcSession(normalizedAgentId);
      const token = session?.token || session?.access_token;
      const connectionUrl = session?.connection_url || session?.room_url;
      if (!token || !connectionUrl) {
        throw new Error('Vaani did not return the WebRTC connection credentials.');
      }

      localStorage.setItem(AGENT_ID_KEY, normalizedAgentId);

      const activeRoom = new Room();
      activeRoom.on(RoomEvent.TrackSubscribed, (track) => {
        if (track.kind !== Track.Kind.Audio) return;
        const audio = track.attach();
        audio.autoplay = true;
        audioElements.add(audio);
        document.body.appendChild(audio);
      });
      activeRoom.on(RoomEvent.TrackUnsubscribed, (track) => {
        for (const audio of track.detach()) {
          audioElements.delete(audio);
          audio.remove();
        }
      });
      activeRoom.on(RoomEvent.Disconnected, () => {
        cleanupRoom();
        isOpen = false;
      });

      room = activeRoom;
      await activeRoom.connect(connectionUrl, token);
      await activeRoom.localParticipant.setMicrophoneEnabled(true);
      isConnected = true;
    } catch (error) {
      if (room) {
        const activeRoom = room;
        room = null;
        await activeRoom.disconnect();
      }
      cleanupRoom();
      errorMessage = error instanceof Error ? error.message : 'Unable to start the Vaani call.';
    } finally {
      isStarting = false;
    }
  }

  onDestroy(() => {
    if (room) {
      void room.disconnect();
    }
    cleanupRoom();
  });
</script>

{#if isOpen}
  <section
    class="fixed bottom-36 right-4 z-[60] w-[min(22rem,calc(100vw-2rem))] rounded-2xl border border-slate-200 bg-white p-5 shadow-2xl dark:border-slate-700 dark:bg-slate-900"
    aria-labelledby="vaani-call-title"
    aria-live="polite"
  >
    <div class="mb-4 flex items-start justify-between gap-3">
      <div>
        <h2 id="vaani-call-title" class="text-base font-semibold text-slate-900 dark:text-white">
          Vaani voice call
        </h2>
        <p class="mt-1 text-sm text-slate-500 dark:text-slate-400">
          {isConnected ? 'Connected. Speak to your agent.' : 'Start a secure in-browser call.'}
        </p>
      </div>
      <button
        type="button"
        class="rounded-lg p-1 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800"
        aria-label="Close Vaani call panel"
        onclick={() => { if (!isConnected && !isStarting) isOpen = false; }}
        disabled={isConnected || isStarting}
      >
        <span class="material-symbols-outlined text-xl">close</span>
      </button>
    </div>

    {#if isConnected}
      <div class="mb-4 flex items-center gap-2 rounded-xl bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200">
        <span class="h-2 w-2 rounded-full bg-emerald-500"></span>
        Microphone connected
      </div>
      <button
        type="button"
        class="w-full rounded-xl bg-red-600 px-4 py-3 text-sm font-semibold text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500/50"
        onclick={endCall}
      >
        <span class="material-symbols-outlined mr-2 align-middle text-lg">call_end</span>
        End call
      </button>
    {:else}
      <form onsubmit={startCall}>
        <label for="vaani-agent-id" class="mb-1.5 block text-sm font-medium text-slate-700 dark:text-slate-200">
          Vaani agent UUID
        </label>
        <input
          id="vaani-agent-id"
          bind:value={agentId}
          autocomplete="off"
          required
          placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
          class="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-slate-600 dark:bg-slate-800 dark:text-white"
          disabled={isStarting}
        />
        {#if errorMessage}
          <p class="mt-2 text-sm text-red-600 dark:text-red-400" role="alert">{errorMessage}</p>
        {/if}
        <button
          type="submit"
          class="mt-4 flex w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500/50 disabled:cursor-wait disabled:opacity-60"
          disabled={isStarting}
        >
          <span class="material-symbols-outlined text-lg">{isStarting ? 'progress_activity' : 'call'}</span>
          {isStarting ? 'Connecting…' : 'Start voice call'}
        </button>
      </form>
    {/if}
  </section>
{/if}

<button
  type="button"
  class="fixed bottom-24 right-4 z-[55] flex h-14 items-center gap-2 rounded-full bg-blue-600 px-5 text-sm font-semibold text-white shadow-xl shadow-blue-900/20 transition hover:scale-[1.03] hover:bg-blue-700 focus:outline-none focus:ring-4 focus:ring-blue-500/30 active:scale-95"
  aria-label={isConnected ? 'Connected to Vaani; open call controls' : 'Start a Vaani voice call'}
  aria-expanded={isOpen}
  onclick={() => { isOpen = !isOpen; errorMessage = ''; }}
>
  <span class="material-symbols-outlined text-xl">{isConnected ? 'graphic_eq' : 'mic'}</span>
  <span>{isConnected ? 'Vaani connected' : 'Talk to Vaani'}</span>
</button>
