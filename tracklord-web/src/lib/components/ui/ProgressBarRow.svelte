<script>
  /** 
   * @type {{ 
   *   label: string, 
   *   value: number, 
   *   total?: number, 
   *   unit?: string, 
   *   subtext?: string, 
   *   colorClass?: string, 
   *   href?: string,
   *   title?: string,
   *   icon?: string
   * }} 
   */
  let {
    label = '',
    value = 0,
    total = 0,
    unit = 'Pcs',
    subtext = '',
    colorClass = 'bg-primary',
    href = '',
    title = '',
    icon = ''
  } = $props();

  let percent = $derived(total > 0 ? (value / total) * 100 : 0);
</script>

{#if href}
  <a
    {href}
    {title}
    class="group block p-2 rounded-lg hover:bg-surface-container-low transition-colors cursor-pointer"
  >
    <div class="flex items-center justify-between mb-1.5">
      <div class="flex items-center gap-2">
        {#if icon}
          <span class="material-symbols-outlined text-[18px] text-on-surface-variant group-hover:text-primary transition-colors">{icon}</span>
        {/if}
        <span class="font-bold text-sm text-on-surface group-hover:text-primary transition-colors">{label}</span>
        {#if subtext}
          <span class="text-[10px] font-mono text-on-surface-variant bg-surface-container-low px-1.5 py-0.5 rounded">{subtext}</span>
        {/if}
      </div>
      <div class="flex items-center gap-3">
        <span class="font-bold text-sm text-primary font-mono min-w-[60px] text-right">{value.toLocaleString()} {unit}</span>
      </div>
    </div>
    <div class="w-full bg-surface-variant/60 rounded-full h-3 overflow-hidden">
      <div
        class="{colorClass} h-3 rounded-full transition-all duration-700 ease-out group-hover:opacity-90"
        style="width: {Math.max(percent, value > 0 ? 2 : 0)}%"
      ></div>
    </div>
  </a>
{:else}
  <div>
    <div class="flex justify-between font-stat-label text-stat-label mb-1">
      <span class="font-bold text-on-surface">{label}</span>
      <span class="font-mono text-primary">{value.toLocaleString()} {unit}</span>
    </div>
    <div class="w-full bg-surface-variant rounded-full h-2.5 overflow-hidden">
      <div
        class="{colorClass} h-2.5 rounded-full transition-all duration-500"
        style="width: {Math.max(percent, value > 0 ? 2 : 0)}%"
      ></div>
    </div>
  </div>
{/if}
