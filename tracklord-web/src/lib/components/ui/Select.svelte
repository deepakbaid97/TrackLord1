<script>
  /** @type {{ label?: string, value?: any, options?: Array<{ label: string, value: any }>, placeholder?: string, disabled?: boolean, icon?: string, id?: string, required?: boolean, searchable?: boolean, searchPlaceholder?: string, onchange?: (value: any) => void }} */
  let {
    label = '',
    value = $bindable(''),
    options = [],
    placeholder = 'Select an option...',
    disabled = false,
    icon = '',
    id = `select-${Math.random().toString(36).substring(2, 9)}`,
    required = false,
    searchable = true,
    searchPlaceholder = 'Search...',
    onchange
  } = $props();

  let isOpen = $state(false);
  let openDirection = $state(/** @type {'down' | 'up'} */ ('down'));
  let searchQuery = $state('');
  let searchInputEl = $state(/** @type {HTMLInputElement | null} */ (null));
  let buttonEl = $state(/** @type {HTMLButtonElement | null} */ (null));
  let menuEl = $state(/** @type {HTMLDivElement | null} */ (null));
  let dropdownStyle = $state('');

  let selectedOption = $derived(
    options.find((opt) => String(opt.value) === String(value))
  );

  let filteredOptions = $derived(
    searchQuery.trim() === ''
      ? options
      : options.filter((opt) =>
          opt.label.toLowerCase().includes(searchQuery.toLowerCase())
        )
  );

  function updatePosition() {
    if (!buttonEl) return;
    const rect = buttonEl.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const spaceBelow = viewportHeight - rect.bottom;
    const spaceAbove = rect.top;

    const openUp = spaceBelow < 220 && spaceAbove > spaceBelow;
    openDirection = openUp ? 'up' : 'down';

    if (openUp) {
      const bottom = viewportHeight - rect.top + 6;
      const maxHeight = Math.max(120, Math.min(240, spaceAbove - 24));
      dropdownStyle = `position: fixed; left: ${rect.left}px; width: ${rect.width}px; bottom: ${bottom}px; max-height: ${maxHeight}px; z-index: 9999;`;
    } else {
      const top = rect.bottom + 6;
      const maxHeight = Math.max(120, Math.min(240, spaceBelow - 24));
      dropdownStyle = `position: fixed; left: ${rect.left}px; width: ${rect.width}px; top: ${top}px; max-height: ${maxHeight}px; z-index: 9999;`;
    }
  }

  function toggle() {
    if (disabled) return;
    if (!isOpen) {
      updatePosition();
    }
    isOpen = !isOpen;
    if (isOpen) {
      searchQuery = '';
      setTimeout(() => {
        updatePosition();
        searchInputEl?.focus();
      }, 30);
    }
  }

  /** @param {any} newValue */
  function selectOption(newValue) {
    value = newValue;
    isOpen = false;
    searchQuery = '';
    if (onchange) onchange(value);
  }

  /** @param {HTMLElement} node */
  function handleClickOutside(node) {
    const handle = (/** @type {MouseEvent} */ e) => {
      const target = /** @type {Node} */ (e.target);
      if (!node.contains(target) && (!menuEl || !menuEl.contains(target))) {
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

  /** @param {KeyboardEvent} e */
  function handleWindowKeyDown(e) {
    if (isOpen && e.key === 'Escape') {
      isOpen = false;
      searchQuery = '';
    }
  }

  function handleWindowResizeOrScroll() {
    if (isOpen) {
      updatePosition();
    }
  }
</script>

<svelte:window
  onkeydown={handleWindowKeyDown}
  onresize={handleWindowResizeOrScroll}
  onscroll={handleWindowResizeOrScroll}
/>

<div
  class="flex flex-col gap-1 w-full relative"
  use:handleClickOutside
>
  {#if label}
    <label for={id} class="text-xs font-bold uppercase tracking-wider text-on-surface-variant font-mono">
      {label} {#if required}<span class="text-[#ba1a1a]">*</span>{/if}
    </label>
  {/if}

  <div class="relative flex items-center w-full">
    <button
      {id}
      bind:this={buttonEl}
      type="button"
      {disabled}
      onclick={toggle}
      class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-2 text-sm font-semibold text-on-surface transition-all duration-200 outline-none flex items-center justify-between text-left focus:ring-2 focus:ring-primary/20 disabled:opacity-60 disabled:cursor-not-allowed cursor-pointer {icon ? 'pl-10' : ''} pr-10"
    >
      {#if icon}
        <span class="material-symbols-outlined absolute left-3 text-on-surface-variant text-[20px] pointer-events-none">{icon}</span>
      {/if}

      <span class="truncate {selectedOption ? 'text-on-surface' : 'text-on-surface-variant/60'}">
        {selectedOption ? selectedOption.label : placeholder}
      </span>

      <span class="material-symbols-outlined absolute right-3 text-on-surface-variant text-[20px] pointer-events-none transition-transform duration-200 {isOpen ? (openDirection === 'up' ? '' : 'rotate-180') : ''}">
        {openDirection === 'up' && isOpen ? 'expand_less' : 'unfold_more'}
      </span>
    </button>

    {#if isOpen}
      <div 
        bind:this={menuEl}
        style={dropdownStyle}
        class="bg-surface dark:bg-slate-900 border border-outline-variant/80 rounded-xl shadow-2xl overflow-hidden animate-in fade-in {openDirection === 'up' ? 'slide-in-from-bottom-2' : 'slide-in-from-top-2'} duration-150 flex flex-col"
      >
        {#if searchable}
          <div class="p-2 border-b border-outline-variant/60 bg-surface dark:bg-slate-900 shrink-0">
            <div class="relative flex items-center">
              <span class="material-symbols-outlined absolute left-2.5 text-on-surface-variant text-[18px] pointer-events-none">search</span>
              <input
                bind:this={searchInputEl}
                type="text"
                bind:value={searchQuery}
                placeholder={searchPlaceholder}
                class="w-full bg-surface-container-low dark:bg-slate-800 border border-outline-variant/70 rounded-md pl-8 pr-3 py-1.5 text-xs text-on-surface placeholder:text-on-surface-variant/60 outline-none focus:ring-2 focus:ring-primary/20"
              />
            </div>
          </div>
        {/if}

        <div class="overflow-y-auto py-1 divide-y divide-outline-variant/20 flex-1">
          {#each filteredOptions as opt}
            {@const isSelected = String(opt.value) === String(value)}
            <button
              type="button"
              onclick={() => selectOption(opt.value)}
              class="w-full px-3 py-2 text-xs sm:text-sm flex items-center justify-between text-left transition-colors cursor-pointer {isSelected ? 'bg-primary/10 font-semibold text-primary' : 'text-on-surface hover:bg-surface-container-low dark:hover:bg-slate-800'}"
            >
              <span class="truncate">{opt.label}</span>
              {#if isSelected}
                <span class="material-symbols-outlined text-[18px] text-primary shrink-0 ml-2">check</span>
              {/if}
            </button>
          {:else}
            <div class="px-4 py-3 text-xs text-on-surface-variant text-center">
              No options found
            </div>
          {/each}
        </div>
      </div>
    {/if}
  </div>
</div>


