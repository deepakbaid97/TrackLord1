<script>
  import { pwaStore } from '../../stores/pwaStore.js';
  import Button from './Button.svelte';

  let isOnline = $derived($pwaStore.isOnline);
  let canInstall = $derived($pwaStore.canInstall);
  let isInstalled = $derived($pwaStore.isInstalled);
  let isIOS = $derived($pwaStore.isIOS);
  let updateAvailable = $derived($pwaStore.updateAvailable);
  let showInstallModal = $derived($pwaStore.showInstallModal);

  let isInstalling = $state(false);

  async function handleInstall() {
    isInstalling = true;
    try {
      await pwaStore.installApp();
    } finally {
      isInstalling = false;
    }
  }

  function handleClose() {
    pwaStore.dismissInstallModal();
  }

  function handleUpdate() {
    pwaStore.refreshApp();
  }
</script>

<!-- 1. Offline Connectivity Warning Banner -->
{#if !isOnline}
  <div class="fixed bottom-20 sm:bottom-6 left-1/2 -translate-x-1/2 z-50 w-[92%] max-w-md bg-amber-500 text-white dark:bg-amber-600 px-4 py-2.5 rounded-2xl shadow-xl flex items-center justify-between gap-3 border border-amber-400 animate-in fade-in slide-in-from-bottom-4 duration-300">
    <div class="flex items-center gap-2.5 min-w-0">
      <span class="material-symbols-outlined text-[20px] animate-pulse shrink-0">wifi_off</span>
      <div class="text-xs font-semibold leading-tight">
        <p class="font-bold">Offline Mode Active</p>
        <p class="text-amber-100 text-[11px] truncate">Using cached application shell.</p>
      </div>
    </div>
    <span class="text-[10px] uppercase font-mono px-2 py-0.5 bg-amber-700/50 rounded-full font-bold">Service Worker</span>
  </div>
{/if}

<!-- 2. App Update Available Toast -->
{#if updateAvailable}
  <div class="fixed top-16 left-1/2 -translate-x-1/2 z-50 w-[92%] max-w-md bg-primary text-white px-4 py-3 rounded-2xl shadow-2xl flex items-center justify-between gap-3 border border-blue-400 animate-in fade-in slide-in-from-top-4 duration-300">
    <div class="flex items-center gap-2.5 min-w-0">
      <span class="material-symbols-outlined text-[22px] shrink-0">update</span>
      <div class="text-xs font-semibold leading-tight">
        <p class="font-bold">Update Available</p>
        <p class="text-blue-100 text-[11px]">A new version of TrackLord is ready.</p>
      </div>
    </div>
    <button
      type="button"
      onclick={handleUpdate}
      class="px-3 py-1.5 bg-white text-primary text-xs font-bold rounded-xl hover:bg-blue-50 transition-all active:scale-95 shadow-sm shrink-0 cursor-pointer"
    >
      Reload Now
    </button>
  </div>
{/if}

<!-- 3. Rich Install & Download Modal Dialog -->
{#if showInstallModal}
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-200">
    <!-- Modal Card -->
    <div class="relative w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden animate-in zoom-in-95 duration-200">
      <!-- Header Pattern -->
      <div class="bg-gradient-to-br from-primary to-blue-700 p-6 text-white text-center relative">
        <button
          type="button"
          onclick={handleClose}
          class="absolute top-4 right-4 text-white/80 hover:text-white p-1 rounded-full hover:bg-white/10 transition-colors"
          aria-label="Close"
        >
          <span class="material-symbols-outlined text-[20px]">close</span>
        </button>

        <div class="w-16 h-16 mx-auto bg-white p-2 rounded-2xl shadow-lg mb-3.5 flex items-center justify-center">
          <img src="/icon.svg" alt="TrackLord Icon" class="w-12 h-12" />
        </div>

        <h3 class="text-xl font-bold tracking-tight">Install TrackLord App</h3>
        <p class="text-xs text-blue-100 mt-1 max-w-xs mx-auto">
          Get the fast, standalone desktop & mobile application experience.
        </p>
      </div>

      <!-- Feature List -->
      <div class="p-6 space-y-4">
        <div class="grid grid-cols-1 gap-3">
          <div class="flex items-start gap-3 p-2.5 rounded-2xl bg-surface-container-low dark:bg-slate-800/50">
            <div class="p-2 rounded-xl bg-blue-100 text-primary dark:bg-blue-900/40 dark:text-blue-300 shrink-0">
              <span class="material-symbols-outlined text-[18px]">bolt</span>
            </div>
            <div>
              <p class="text-xs font-bold text-slate-900 dark:text-white">Instant Launch & Speed</p>
              <p class="text-[11px] text-slate-500 dark:text-slate-400">Pre-cached app shell starts instantly without browser tabs.</p>
            </div>
          </div>

          <div class="flex items-start gap-3 p-2.5 rounded-2xl bg-surface-container-low dark:bg-slate-800/50">
            <div class="p-2 rounded-xl bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 shrink-0">
              <span class="material-symbols-outlined text-[18px]">offline_pin</span>
            </div>
            <div>
              <p class="text-xs font-bold text-slate-900 dark:text-white">Offline Resilience</p>
              <p class="text-[11px] text-slate-500 dark:text-slate-400">Service Worker keeps pages responsive even during weak signal.</p>
            </div>
          </div>

          <div class="flex items-start gap-3 p-2.5 rounded-2xl bg-surface-container-low dark:bg-slate-800/50">
            <div class="p-2 rounded-xl bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300 shrink-0">
              <span class="material-symbols-outlined text-[18px]">desktop_windows</span>
            </div>
            <div>
              <p class="text-xs font-bold text-slate-900 dark:text-white">Standalone Window</p>
              <p class="text-[11px] text-slate-500 dark:text-slate-400">Dedicated taskbar icon, multi-windowing, and full screen view.</p>
            </div>
          </div>
        </div>

        <!-- Action / OS Instructions -->
        {#if isIOS}
          <!-- iOS Instructions -->
          <div class="p-3.5 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900 rounded-2xl text-xs space-y-2">
            <p class="font-bold text-amber-900 dark:text-amber-200 flex items-center gap-1.5">
              <span class="material-symbols-outlined text-[16px]">ios_share</span>
              To install on iPhone or iPad:
            </p>
            <ol class="list-decimal list-inside space-y-1 text-slate-700 dark:text-slate-300 text-[11px]">
              <li>Tap the <span class="font-bold">Share</span> button in Safari at the bottom of the screen.</li>
              <li>Scroll down and select <span class="font-bold">"Add to Home Screen"</span>.</li>
              <li>Tap <span class="font-bold">Add</span> in the top-right corner.</li>
            </ol>
          </div>
          <button
            type="button"
            onclick={handleClose}
            class="w-full py-2.5 px-4 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-bold text-xs hover:bg-slate-200 transition-all cursor-pointer"
          >
            Got It
          </button>
        {:else if canInstall}
          <!-- Native Install Trigger -->
          <div class="flex gap-2 pt-2">
            <button
              type="button"
              onclick={handleClose}
              class="flex-1 py-2.5 px-4 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-bold text-xs hover:bg-slate-50 dark:hover:bg-slate-800 transition-all cursor-pointer"
            >
              Not Now
            </button>
            <button
              type="button"
              onclick={handleInstall}
              disabled={isInstalling}
              class="flex-1 py-2.5 px-4 rounded-xl bg-primary hover:bg-blue-700 text-white font-bold text-xs shadow-md hover:shadow-lg transition-all flex items-center justify-center gap-1.5 active:scale-95 cursor-pointer disabled:opacity-50"
            >
              <span class="material-symbols-outlined text-[16px]">download</span>
              <span>{isInstalling ? 'Installing...' : 'Install Now'}</span>
            </button>
          </div>
        {:else}
          <!-- Already installed or browser doesn't support native prompt -->
          <div class="text-center pt-2">
            {#if isInstalled}
              <p class="text-xs text-emerald-600 dark:text-emerald-400 font-bold flex items-center justify-center gap-1">
                <span class="material-symbols-outlined text-[16px]">check_circle</span>
                TrackLord is already installed!
              </p>
            {:else}
              <p class="text-[11px] text-slate-500">
                To download, use the Install App icon in your browser URL bar.
              </p>
            {/if}
            <button
              type="button"
              onclick={handleClose}
              class="mt-3 w-full py-2.5 px-4 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-bold text-xs hover:bg-slate-200 transition-all cursor-pointer"
            >
              Close
            </button>
          </div>
        {/if}
      </div>
    </div>
  </div>
{/if}
