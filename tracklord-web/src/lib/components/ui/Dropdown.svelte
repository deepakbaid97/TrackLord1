<script>
  /** @type {{ label?: string, icon?: string, items?: Array<{ label: string, icon?: string, action: () => void, danger?: boolean }>, align?: 'left' | 'right', searchable?: boolean, searchPlaceholder?: string }} */
  let {
    label = 'Options',
    icon = 'more_vert',
    items = [],
    align = 'right',
    searchable = true,
    searchPlaceholder = 'Search...'
  } = $props();

  let isOpen = $state(false);
  let searchQuery = $state('');

  let filteredItems = $derived(
    searchQuery.trim() === ''
      ? items
      : items.filter((item) =>
          item.label.toLowerCase().includes(searchQuery.toLowerCase())
        )
  );

  function toggle() {
    isOpen = !isOpen;
    if (!isOpen) {
      searchQuery = '';
    }
  }

  /** @param {any} item */
  function handleSelect(item) {
    isOpen = false;
    searchQuery = '';
    item.action();
  }

  /** @param {HTMLElement} node */
  function handleClickOutside(node) {
    const handle = (/** @type {MouseEvent} */ e) => {
      if (!node.contains(/** @type {Node} */ (e.target))) {
        isOpen = false;
        searchQuery = '';
      }
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
      class="absolute {align === 'right' ? 'right-0' : 'left-0'} mt-2 w-56 rounded-xl bg-white border border-[#c3c6d7] shadow-lg py-1 z-50 animate-in fade-in slide-in-from-top-2 duration-150"
    >
      {#if searchable}
        <div class="px-2 py-1.5 border-b border-[#e1e2ec]">
          <div class="relative flex items-center">
            <span class="material-symbols-outlined absolute left-2 text-[#737686] text-[18px] pointer-events-none">search</span>
            <input
              type="text"
              bind:value={searchQuery}
              placeholder={searchPlaceholder}
              class="w-full bg-[#eff4ff] border border-[#c3c6d7] rounded-md pl-7 pr-2 py-1 text-xs text-[#0b1c30] placeholder-[#737686] outline-none focus:border-[#2563eb] focus:ring-1 focus:ring-[#2563eb]"
            />
          </div>
        </div>
      {/if}

      <div class="max-h-60 overflow-y-auto">
        {#each filteredItems as item}
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
        {:else}
          <div class="px-4 py-2.5 text-xs text-[#737686] text-center">
            No items found
          </div>
        {/each}
      </div>
    </div>
  {/if}
</div>
