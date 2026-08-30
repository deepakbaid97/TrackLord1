<script>
  import { tick } from 'svelte';

  /** 
   * @type {{ 
   *   label?: string, 
   *   value?: any, 
   *   options?: Array<{ label: string, value: any }>, 
   *   placeholder?: string, 
   *   disabled?: boolean, 
   *   icon?: string, 
   *   id?: string, 
   *   required?: boolean, 
   *   searchable?: boolean, 
   *   searchPlaceholder?: string, 
   *   onchange?: (value: any) => void 
   * }} 
   */
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
  let listEl = $state(/** @type {HTMLDivElement | null} */ (null));
  let dropdownStyle = $state('');
  let highlightedIndex = $state(0);

  // Typeahead state for closed select
  let typeaheadBuffer = $state('');
  let typeaheadTimeout = $state(/** @type {any} */ (null));
  let showTypeaheadBadge = $state(false);
  let typeaheadBadgeTimeout = $state(/** @type {any} */ (null));

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

  // When search query changes, reset highlighted index to top match
  $effect(() => {
    if (searchQuery !== undefined) {
      highlightedIndex = 0;
    }
  });

  function updatePosition() {
    if (!buttonEl) return;
    const rect = buttonEl.getBoundingClientRect();
    const viewportHeight = typeof window !== 'undefined' ? window.innerHeight : 800;
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

  /**
   * Scroll highlighted item into view within dropdown menu
   * @param {number} index
   */
  function scrollOptionIntoView(index) {
    if (!listEl) return;
    const optionEls = listEl.querySelectorAll('[data-select-option]');
    if (optionEls[index] && typeof optionEls[index].scrollIntoView === 'function') {
      optionEls[index].scrollIntoView({ block: 'nearest' });
    }
  }

  function openDropdown() {
    if (disabled || isOpen) return;
    updatePosition();
    isOpen = true;
    searchQuery = '';

    // Pre-highlight current selection if possible
    const currentIdx = filteredOptions.findIndex(
      (opt) => String(opt.value) === String(value)
    );
    highlightedIndex = currentIdx >= 0 ? currentIdx : 0;

    setTimeout(async () => {
      updatePosition();
      if (searchable && searchInputEl) {
        searchInputEl.focus();
      }
      await tick();
      scrollOptionIntoView(highlightedIndex);
    }, 30);
  }

  function closeDropdown(refocusButton = false) {
    if (!isOpen) return;
    isOpen = false;
    searchQuery = '';
    if (refocusButton && buttonEl) {
      buttonEl.focus();
    }
  }

  function toggle() {
    if (disabled) return;
    if (isOpen) {
      closeDropdown(false);
    } else {
      openDropdown();
    }
  }

  /** @param {any} newValue */
  function selectOption(newValue, refocusButton = true) {
    value = newValue;
    closeDropdown(refocusButton);
    if (onchange) onchange(value);
  }

  /**
   * Handle fast typeahead search when closed
   * @param {string} char
   */
  function handleTypeahead(char) {
    if (!char || options.length === 0) return;

    if (typeaheadTimeout) clearTimeout(typeaheadTimeout);
    if (typeaheadBadgeTimeout) clearTimeout(typeaheadBadgeTimeout);

    let nextBuffer = (typeaheadBuffer + char).toLowerCase();

    // Search algorithm helper
    const findMatch = (/** @type {string} */ buf) =>
      options.find((opt) => opt.label.toLowerCase().startsWith(buf)) ||
      options.find((opt) =>
        opt.label
          .toLowerCase()
          .split(/[\s(/]+/)
          .some((word) => word.startsWith(buf))
      ) ||
      options.find((opt) => opt.label.toLowerCase().includes(buf));

    let match = findMatch(nextBuffer);

    // If accumulated buffer doesn't match anything, check if starting fresh with single `char` matches
    if (!match && typeaheadBuffer.length > 0) {
      nextBuffer = char.toLowerCase();
      match = findMatch(nextBuffer);
    }

    typeaheadBuffer = nextBuffer;
    showTypeaheadBadge = true;

    typeaheadTimeout = setTimeout(() => {
      typeaheadBuffer = '';
    }, 800);

    typeaheadBadgeTimeout = setTimeout(() => {
      showTypeaheadBadge = false;
    }, 1200);

    if (match) {
      selectOption(match.value, false);
    }
  }

  /**
   * Keyboard handler on main button (when dropdown is closed or button is focused)
   * @param {KeyboardEvent} e
   */
  function handleButtonKeyDown(e) {
    if (disabled) return;

    // Alt + ArrowDown opens dropdown
    if (e.altKey && e.key === 'ArrowDown') {
      e.preventDefault();
      openDropdown();
      return;
    }

    // Direct Arrow navigation when closed
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (options.length === 0) return;

      const currentIndex = options.findIndex(
        (opt) => String(opt.value) === String(value)
      );

      let nextIndex;
      if (e.key === 'ArrowDown') {
        nextIndex = currentIndex < options.length - 1 ? currentIndex + 1 : 0;
      } else {
        nextIndex = currentIndex > 0 ? currentIndex - 1 : options.length - 1;
      }

      selectOption(options[nextIndex].value, false);
      return;
    }

    // Home / End navigation
    if (e.key === 'Home') {
      e.preventDefault();
      if (options.length > 0) selectOption(options[0].value, false);
      return;
    }
    if (e.key === 'End') {
      e.preventDefault();
      if (options.length > 0) selectOption(options[options.length - 1].value, false);
      return;
    }

    // Enter / Space toggles dropdown
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      toggle();
      return;
    }

    // Typeahead typing (single printable character without ctrl/meta/alt)
    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault();
      handleTypeahead(e.key);
    }
  }

  /**
   * Keyboard handler for search input and dropdown list
   * @param {KeyboardEvent} e
   */
  function handleSearchKeyDown(e) {
    if (!isOpen) return;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (filteredOptions.length > 0) {
        highlightedIndex = (highlightedIndex + 1) % filteredOptions.length;
        scrollOptionIntoView(highlightedIndex);
      }
      return;
    }

    if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (filteredOptions.length > 0) {
        highlightedIndex =
          (highlightedIndex - 1 + filteredOptions.length) % filteredOptions.length;
        scrollOptionIntoView(highlightedIndex);
      }
      return;
    }

    if (e.key === 'Home' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      if (filteredOptions.length > 0) {
        highlightedIndex = 0;
        scrollOptionIntoView(highlightedIndex);
      }
      return;
    }

    if (e.key === 'End' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      if (filteredOptions.length > 0) {
        highlightedIndex = filteredOptions.length - 1;
        scrollOptionIntoView(highlightedIndex);
      }
      return;
    }

    if (e.key === 'Enter') {
      e.preventDefault();
      if (filteredOptions.length > 0 && filteredOptions[highlightedIndex]) {
        selectOption(filteredOptions[highlightedIndex].value, true);
      }
      return;
    }

    if (e.key === 'Escape') {
      e.preventDefault();
      closeDropdown(true);
      return;
    }

    if (e.key === 'Tab') {
      // Auto-select highlighted option if active and let focus proceed naturally
      if (filteredOptions.length > 0 && filteredOptions[highlightedIndex]) {
        selectOption(filteredOptions[highlightedIndex].value, false);
      } else {
        closeDropdown(false);
      }
    }
  }

  /** @param {HTMLElement} node */
  function handleClickOutside(node) {
    const handle = (/** @type {MouseEvent} */ e) => {
      const target = /** @type {Node} */ (e.target);
      if (!node.contains(target) && (!menuEl || !menuEl.contains(target))) {
        closeDropdown(false);
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
      closeDropdown(true);
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
      onkeydown={handleButtonKeyDown}
      role="combobox"
      aria-expanded={isOpen}
      aria-haspopup="listbox"
      aria-controls="{id}-listbox"
      class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-2 text-sm font-semibold text-on-surface transition-all duration-150 outline-none flex items-center justify-between text-left focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:opacity-60 disabled:cursor-not-allowed cursor-pointer {icon ? 'pl-10' : ''} pr-10"
    >
      {#if icon}
        <span class="material-symbols-outlined absolute left-3 text-on-surface-variant text-[20px] pointer-events-none">{icon}</span>
      {/if}

      <span class="truncate {selectedOption ? 'text-on-surface' : 'text-on-surface-variant/60'}">
        {selectedOption ? selectedOption.label : placeholder}
      </span>

      <!-- Floating Typeahead Indicator Badge -->
      {#if showTypeaheadBadge && typeaheadBuffer}
        <span class="absolute right-9 top-1/2 -translate-y-1/2 px-2 py-0.5 bg-primary text-white text-[10px] font-mono font-bold rounded-md shadow-xs animate-in fade-in zoom-in-95 duration-100 flex items-center gap-1 z-10 pointer-events-none">
          <span class="material-symbols-outlined text-[12px]">search</span>
          {typeaheadBuffer}
        </span>
      {/if}

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
                onkeydown={handleSearchKeyDown}
                placeholder={searchPlaceholder}
                aria-autocomplete="list"
                class="w-full bg-surface-container-low dark:bg-slate-800 border border-outline-variant/70 rounded-md pl-8 pr-3 py-1.5 text-xs text-on-surface placeholder:text-on-surface-variant/60 outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
            </div>
          </div>
        {/if}

        <div 
          bind:this={listEl}
          id="{id}-listbox"
          role="listbox"
          aria-label={label || placeholder}
          class="overflow-y-auto py-1 divide-y divide-outline-variant/20 flex-1 max-h-[200px]"
          tabindex="-1"
          onkeydown={handleSearchKeyDown}
        >
          {#each filteredOptions as opt, idx}
            {@const isSelected = String(opt.value) === String(value)}
            {@const isHighlighted = idx === highlightedIndex}
            <button
              type="button"
              data-select-option
              role="option"
              aria-selected={isSelected}
              onclick={() => selectOption(opt.value, true)}
              onmouseenter={() => (highlightedIndex = idx)}
              class="w-full px-3 py-2 text-xs sm:text-sm flex items-center justify-between text-left transition-colors cursor-pointer {isSelected ? 'bg-primary/10 font-bold text-primary' : isHighlighted ? 'bg-surface-container-high text-on-surface font-medium ring-1 ring-inset ring-primary/20' : 'text-on-surface hover:bg-surface-container-low dark:hover:bg-slate-800'}"
            >
              <div class="flex items-center gap-1.5 min-w-0">
                {#if isHighlighted && !isSelected}
                  <span class="w-1.5 h-1.5 rounded-full bg-primary shrink-0 animate-pulse"></span>
                {/if}
                <span class="truncate">{opt.label}</span>
              </div>
              {#if isSelected}
                <span class="material-symbols-outlined text-[18px] text-primary shrink-0 ml-2">check</span>
              {/if}
            </button>
          {:else}
            <div class="px-4 py-4 text-xs text-on-surface-variant flex flex-col items-center justify-center gap-1 text-center">
              <span class="material-symbols-outlined text-[20px] text-outline">search_off</span>
              <span>No options found</span>
            </div>
          {/each}
        </div>
      </div>
    {/if}
  </div>
</div>
