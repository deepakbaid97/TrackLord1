<script>
  /**
   * @typedef {Object} KpiItem
   * @property {string} label
   * @property {string | number} value
   * @property {string} [icon]
   * @property {string} [color] CSS color class for value/icon
   */

  /**
   * @type {{ items: Array<KpiItem>, columns?: number }}
   */
  let { items = [], columns = 4 } = $props();
</script>

<section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden">
  <div class="grid divide-x divide-outline-variant/40 p-1.5 sm:p-3 text-center" style="grid-template-columns: repeat({columns || items.length || 4}, minmax(0, 1fr));">
    {#each items as item}
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center min-w-0">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider truncate max-w-full">
          {#if item.icon}
            <span class="material-symbols-outlined text-[12px] sm:text-[14px] {item.color || 'text-primary'} hidden min-[380px]:inline shrink-0">{item.icon}</span>
          {/if}
          <span class="truncate">{item.label}</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold font-mono mt-0.5 sm:mt-1 truncate max-w-full {item.color || 'text-on-surface'}">
          {item.value}
        </div>
      </div>
    {/each}
  </div>
</section>
