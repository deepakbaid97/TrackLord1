<script>
  /** @type {{ isOpen: boolean, title?: string, onclose?: () => void, children?: any }} */
  let {
    isOpen = $bindable(false),
    title = 'Modal',
    onclose,
    children
  } = $props();

  function close() {
    isOpen = false;
    if (onclose) onclose();
  }

  /** @param {any} event */
  function handleKeydown(event) {
    if (event.key === 'Escape') close();
  }
</script>

<svelte:window onkeydown={handleKeydown} />

{#if isOpen}
  <!-- Backdrop (z-[60] to guarantee sitting cleanly above floating bottom nav) -->
  <div
    class="fixed inset-0 bg-[#0b1c30]/50 backdrop-blur-sm z-[60] flex items-center justify-center p-3 sm:p-4 animate-in fade-in duration-200"
    role="button"
    tabindex="0"
    onclick={close}
    onkeydown={(e) => e.key === 'Enter' && close()}
  >
    <!-- Content Box -->
    <div
      class="bg-surface dark:bg-slate-900 rounded-2xl border border-outline-variant/80 shadow-2xl w-full max-w-lg overflow-hidden animate-in zoom-in-95 duration-200 my-auto max-h-[90dvh] flex flex-col"
      role="dialog"
      aria-modal="true"
      tabindex="-1"
      onclick={(e) => e.stopPropagation()}
      onkeydown={(e) => e.stopPropagation()}
    >
      <!-- Modal Header -->
      <div class="flex items-center justify-between px-5 py-3.5 border-b border-outline-variant/60 bg-surface-container-low dark:bg-slate-800 shrink-0">
        <h3 class="text-base sm:text-lg font-bold text-on-surface">{title}</h3>
        <button
          type="button"
          onclick={close}
          class="p-1 rounded-lg text-on-surface-variant hover:bg-surface-container hover:text-on-surface transition-colors cursor-pointer"
        >
          <span class="material-symbols-outlined text-[20px]">close</span>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-4 sm:p-6 space-y-4 overflow-y-auto overscroll-contain flex-1">
        {#if children}
          {@render children()}
        {/if}
      </div>
    </div>
  </div>
{/if}

