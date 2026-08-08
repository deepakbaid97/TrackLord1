<script>
  /** 
   * @type {{ 
   *   value: string, 
   *   placeholder?: string 
   * }} 
   */
  let {
    value = $bindable(''),
    placeholder = 'Search...'
  } = $props();
</script>

<div class="relative flex-1">
  <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[20px]">search</span>
  <input
    type="text"
    bind:value={value}
    {placeholder}
    class="w-full pl-10 pr-4 py-2 bg-surface-container-low border border-outline-variant rounded-lg text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
  />
</div>
