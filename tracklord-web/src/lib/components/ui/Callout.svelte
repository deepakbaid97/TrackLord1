<script>
  import { onDestroy } from 'svelte';

  /**
   * @type {{
   *   message?: string,
   *   type?: 'error' | 'success' | 'warning' | 'info',
   *   duration?: number,
   *   onclose?: () => void
   * }}
   */
  let {
    message = $bindable(''),
    type = 'error',
    duration = 10000,
    onclose
  } = $props();

  let timer = null;

  function handleClose() {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
    message = '';
    onclose?.();
  }

  $effect(() => {
    // When message becomes non-empty, start the 10-second auto-dismiss timer
    if (message) {
      if (timer) clearTimeout(timer);
      if (duration > 0) {
        timer = setTimeout(() => {
          handleClose();
        }, duration);
      }
    } else {
      if (timer) {
        clearTimeout(timer);
        timer = null;
      }
    }
  });

  onDestroy(() => {
    if (timer) clearTimeout(timer);
  });
</script>

{#if message}
  <div
    class="fixed top-16 right-4 left-4 sm:left-auto sm:right-6 z-[120] sm:max-w-md flex items-start justify-between gap-3 p-4 rounded-xl border shadow-xl backdrop-blur-md transition-all duration-300 ease-out animate-in fade-in slide-in-from-top-2 {
      type === 'error'
        ? 'bg-red-50/95 dark:bg-red-950/90 border-red-200 dark:border-red-800 text-red-800 dark:text-red-200 shadow-red-500/10'
        : type === 'success'
        ? 'bg-emerald-50/95 dark:bg-emerald-950/90 border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-200 shadow-emerald-500/10'
        : type === 'warning'
        ? 'bg-amber-50/95 dark:bg-amber-950/90 border-amber-200 dark:border-amber-800 text-amber-800 dark:text-amber-200 shadow-amber-500/10'
        : 'bg-blue-50/95 dark:bg-blue-950/90 border-blue-200 dark:border-blue-800 text-blue-800 dark:text-blue-200 shadow-blue-500/10'
    }"
    role="alert"
  >
    <div class="flex items-start gap-2.5 min-w-0 flex-1">
      <span class="material-symbols-outlined text-[22px] shrink-0 mt-0.5 {
        type === 'error'
          ? 'text-red-600 dark:text-red-400'
          : type === 'success'
          ? 'text-emerald-600 dark:text-emerald-400'
          : type === 'warning'
          ? 'text-amber-600 dark:text-amber-400'
          : 'text-blue-600 dark:text-blue-400'
      }">
        {type === 'error' ? 'error' : type === 'success' ? 'check_circle' : type === 'warning' ? 'warning' : 'info'}
      </span>
      <div class="text-sm font-medium leading-snug break-words flex-1 pt-0.5">{message}</div>
    </div>
    <button
      type="button"
      onclick={handleClose}
      class="p-1 rounded-lg hover:bg-black/5 dark:hover:bg-white/10 transition-colors shrink-0 flex items-center justify-center {
        type === 'error'
          ? 'text-red-600 hover:text-red-800 dark:text-red-400'
          : type === 'success'
          ? 'text-emerald-600 hover:text-emerald-800 dark:text-emerald-400'
          : type === 'warning'
          ? 'text-amber-600 hover:text-amber-800 dark:text-amber-400'
          : 'text-blue-600 hover:text-blue-800 dark:text-blue-400'
      }"
      title="Dismiss notification"
      aria-label="Dismiss notification"
    >
      <span class="material-symbols-outlined text-[18px]">close</span>
    </button>
  </div>
{/if}
