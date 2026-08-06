<script>
  /** @type {{ label?: string, icon?: string, items?: Array<{ label: string, icon?: string, action: () => void, danger?: boolean }>, align?: 'left' | 'right' }} */
  let {
    label = 'Options',
    icon = 'more_vert',
    items = [],
    align = 'right'
  } = $props();

  let isOpen = $state(false);

  function toggle() {
    isOpen = !isOpen;
  }

  function handleSelect(item) {
    isOpen = false;
    item.action();
  }

  function handleClickOutside(node) {
    const handle = (e) => {
      if (!node.contains(e.target)) isOpen = false;
    };
    document.addEventListener('click', handle, true);
    return {
      destroy() {
        document.removeEventListener('click', handle, true);
      }
    };
  }
</script>

<div class="relative inline-block text-left" use:handleClickOutside>
  <button
    type="button"
    onclick={toggle}
    class="flex items-center gap-1.5 p-2 rounded-lg text-[#434655] hover:bg-[#dce9ff] active:bg-[#d3e4fe] transition-colors focus:outline-none"
  >
    {#if icon}
      <span class="material-symbols-outlined text-[20px]">{icon}</span>
    {/if}
    {#if label}
      <span class="text-sm font-medium">{label}</span>
    {/if}
  </button>

  {#if isOpen}
    <div
      class="absolute {align === 'right' ? 'right-0' : 'left-0'} mt-2 w-48 rounded-xl bg-white border border-[#c3c6d7] shadow-lg py-1 z-50 animate-in fade-in slide-in-from-top-2 duration-150"
    >
      {#each items as item}
        <button
          type="button"
          onclick={() => handleSelect(item)}
          class="w-full px-4 py-2.5 text-sm flex items-center gap-2.5 transition-colors text-left {item.danger ? 'text-[#ba1a1a] hover:bg-[#ffdad6]' : 'text-[#0b1c30] hover:bg-[#eff4ff]'}"
        >
          {#if item.icon}
            <span class="material-symbols-outlined text-[18px]">{item.icon}</span>
          {/if}
          <span>{item.label}</span>
        </button>
      {/each}
    </div>
  {/if}
</div>
