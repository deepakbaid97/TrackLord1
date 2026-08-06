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

  function handleKeydown(event) {
    if (event.key === 'Escape') close();
  }
</script>

<svelte:window onkeydown={handleKeydown} />

{#if isOpen}
  <!-- Backdrop -->
  <div
    class="fixed inset-0 bg-[#0b1c30]/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
    role="button"
    tabindex="0"
    onclick={close}
    onkeydown={(e) => e.key === 'Enter' && close()}
  >
    <!-- Content Box -->
    <div
      class="bg-white rounded-2xl border border-[#c3c6d7] shadow-2xl w-full max-w-lg overflow-hidden animate-in zoom-in-95 duration-200"
      role="dialog"
      aria-modal="true"
      tabindex="-1"
      onclick={(e) => e.stopPropagation()}
      onkeydown={(e) => e.stopPropagation()}
    >
      <!-- Modal Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-[#c3c6d7] bg-[#eff4ff]">
        <h3 class="text-lg font-bold text-[#0b1c30]">{title}</h3>
        <button
          type="button"
          onclick={close}
          class="p-1 rounded-lg text-[#737686] hover:bg-[#d3e4fe] hover:text-[#0b1c30] transition-colors"
        >
          <span class="material-symbols-outlined text-[20px]">close</span>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 space-y-4 max-h-[80vh] overflow-y-auto">
        {#if children}
          {@render children()}
        {/if}
      </div>
    </div>
  </div>
{/if}
