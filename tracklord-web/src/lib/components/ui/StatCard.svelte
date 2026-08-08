<script>
  /** 
   * @type {{ 
   *   label: string, 
   *   value: string | number, 
   *   icon: string, 
   *   variant?: 'primary' | 'info' | 'success' | 'warning', 
   *   subvalue?: string,
   *   href?: string,
   *   title?: string
   * }} 
   */
  let {
    label = '',
    value = 0,
    icon = 'analytics',
    variant = 'primary',
    subvalue = '',
    href = '',
    title = ''
  } = $props();

  const colorClasses = {
    primary: 'bg-primary/10 text-primary group-hover:bg-primary/15',
    info: 'bg-blue-500/10 text-blue-600 group-hover:bg-blue-500/15',
    success: 'bg-emerald-500/10 text-emerald-600 group-hover:bg-emerald-500/15',
    warning: 'bg-amber-500/10 text-amber-600 group-hover:bg-amber-500/15'
  };
</script>

{#if href}
  <a
    {href}
    {title}
    class="bg-surface p-4 md:p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3 md:gap-4 group hover:shadow-md transition-all cursor-pointer hover:border-primary/50"
  >
    <div class="p-3 rounded-xl transition-colors {colorClasses[variant]}">
      <span class="material-symbols-outlined text-[24px] md:text-[28px]">{icon}</span>
    </div>
    <div>
      <div class="text-xs text-on-surface-variant group-hover:text-primary transition-colors uppercase font-mono font-bold tracking-wider">{label}</div>
      <div class="text-2xl md:text-3xl font-bold text-on-surface group-hover:text-primary transition-colors leading-tight">{value}</div>
      {#if subvalue}
        <div class="text-[11px] text-on-surface-variant font-mono">{subvalue}</div>
      {/if}
    </div>
  </a>
{:else}
  <div class="bg-surface p-4 md:p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3 md:gap-4">
    <div class="p-3 rounded-xl {colorClasses[variant]}">
      <span class="material-symbols-outlined text-[24px] md:text-[28px]">{icon}</span>
    </div>
    <div>
      <div class="text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">{label}</div>
      <div class="text-2xl md:text-3xl font-bold text-on-surface leading-tight">{value}</div>
      {#if subvalue}
        <div class="text-[11px] text-on-surface-variant font-mono">{subvalue}</div>
      {/if}
    </div>
  </div>
{/if}
