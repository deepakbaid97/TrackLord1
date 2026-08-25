<script>
  let {
    value = $bindable(''),
    placeholder = 'Search...',
    className = 'w-full'
  } = $props();
</script>

<div class="relative {className}">
  <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[18px]">search</span>
  <input
    type="text"
    bind:value
    {placeholder}
    class="w-full pl-9 pr-4 py-2 bg-surface-container-low border border-outline-variant/70 rounded-xl text-xs sm:text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20 transition-all font-sans"
  />
  {#if value}
    <button
      type="button"
      onclick={() => value = ''}
      class="absolute right-3 top-2.5 text-on-surface-variant hover:text-on-surface cursor-pointer"
      title="Clear search"
    >
      <span class="material-symbols-outlined text-[18px]">close</span>
    </button>
  {/if}
</div>
