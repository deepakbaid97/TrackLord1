<script>
  /** 
   * @type {{ 
   *   title?: string, 
   *   subtitle?: string, 
   *   icon?: string, 
   *   children?: any 
   * }} 
   */
  let {
    title = 'TRACKLORD ERP',
    subtitle = '',
    icon = 'dashboard',
    children
  } = $props();
</script>

<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">{icon}</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">{title}</div>
      {#if subtitle}
        <div class="text-xs text-on-surface-variant">{subtitle}</div>
      {/if}
    </div>
  </div>

  <div class="flex items-center gap-2">
    {#if children}
      {@render children()}
    {/if}
  </div>
</header>
