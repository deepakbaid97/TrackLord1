<script>
  /** 
   * @type {{ 
   *   columns: Array<{ key: string, label: string, align?: 'left' | 'center' | 'right' }>,
   *   rows: Array<Record<string, any>>,
   *   emptyMessage?: string
   * }} 
   */
  let {
    columns = [],
    rows = [],
    emptyMessage = 'No records found.'
  } = $props();
</script>

<div class="w-full overflow-x-auto rounded-xl border border-[#c3c6d7] bg-white shadow-sm">
  <table class="w-full text-left border-collapse text-sm">
    <thead>
      <tr class="bg-[#eff4ff] border-b border-[#c3c6d7] text-xs font-semibold uppercase tracking-wider text-[#434655] font-['JetBrains_Mono']">
        {#each columns as col}
          <th class="px-4 py-3 text-{col.align || 'left'}">
            {col.label}
          </th>
        {/each}
      </tr>
    </thead>
    <tbody class="divide-y divide-[#c3c6d7]/50 text-[#0b1c30]">
      {#if rows.length === 0}
        <tr>
          <td colspan={columns.length} class="px-4 py-8 text-center text-[#737686]">
            {emptyMessage}
          </td>
        </tr>
      {:else}
        {#each rows as row}
          <tr class="hover:bg-[#eff4ff]/60 transition-colors">
            {#each columns as col}
              <td class="px-4 py-3 text-{col.align || 'left'} whitespace-nowrap">
                {#if row[col.key] !== undefined}
                  {row[col.key]}
                {:else}
                  <span class="text-[#737686]">-</span>
                {/if}
              </td>
            {/each}
          </tr>
        {/each}
      {/if}
    </tbody>
  </table>
</div>
