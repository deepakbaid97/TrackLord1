<script>
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';

  let {
    movements = [],
    onReverseClick
  } = $props();
</script>

<section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden p-3 sm:p-5 space-y-3">
  <div class="flex items-center justify-between border-b border-outline-variant/60 pb-2">
    <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-1.5">
      <span class="material-symbols-outlined text-primary text-[18px]">history</span>
      Movements & Audit Ledger
    </h2>
    <span class="text-xs font-mono text-on-surface-variant">{movements.length} records</span>
  </div>

  {#if movements.length > 0}
    <!-- Mobile View: Ledger Card Stack -->
    <div class="block md:hidden space-y-2">
      {#each movements as m}
        <div class="p-3 rounded-xl border border-outline-variant/60 transition-all {m.isReversed ? 'bg-red-50/50 dark:bg-red-950/20 opacity-70 border-dashed border-red-300' : 'bg-surface-container-low/40'}">
          <div class="flex items-center justify-between gap-2 text-xs">
            <div class="flex items-center gap-1 font-mono font-bold">
              <span class="text-on-surface">{m.from}</span>
              <span class="material-symbols-outlined text-[13px] text-primary">arrow_forward</span>
              <span class="text-primary">{m.to}</span>
            </div>
            <span class="font-mono font-bold text-primary">{m.qty}</span>
          </div>

          <div class="flex items-center justify-between text-[11px] text-on-surface-variant font-mono mt-1.5 pt-1.5 border-t border-dashed border-outline-variant/40">
            <span>{m.date}</span>
            <div class="flex items-center gap-2">
              {#if m.isReversed}
                <span class="px-1.5 py-0.5 rounded bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-300 font-bold text-[9px] uppercase">
                  Reversed
                </span>
              {:else if onReverseClick}
                <button
                  type="button"
                  onclick={() => onReverseClick(m)}
                  class="px-2 py-0.5 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40 rounded transition-colors"
                >
                  Reverse
                </button>
              {/if}
            </div>
          </div>
        </div>
      {/each}
    </div>

    <!-- Desktop View: Ledger Table -->
    <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/60">
      <table class="w-full text-left border-collapse text-xs">
        <thead>
          <tr class="bg-surface-container-low border-b border-outline-variant/60 font-mono text-on-surface-variant uppercase tracking-wider">
            <th class="px-3.5 py-2.5">Date</th>
            <th class="px-3.5 py-2.5">From Step</th>
            <th class="px-3.5 py-2.5">To Step</th>
            <th class="px-3.5 py-2.5 text-right">Quantity</th>
            {#if $tenantConfigStore.clientMode !== 'none'}
              <th class="px-3.5 py-2.5">Assigned Partner</th>
            {/if}
            <th class="px-3.5 py-2.5">Reference #</th>
            <th class="px-3.5 py-2.5">Logged By</th>
            <th class="px-3.5 py-2.5 text-center">Status</th>
            <th class="px-3.5 py-2.5 text-right">Action</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-outline-variant/40">
          {#each movements as m}
            <tr class="hover:bg-surface-container-low/50 transition-colors {m.isReversed ? 'bg-red-50/30 dark:bg-red-950/10 opacity-70' : ''}">
              <td class="px-3.5 py-2.5 font-mono text-on-surface-variant whitespace-nowrap">{m.date}</td>
              <td class="px-3.5 py-2.5 font-medium text-on-surface">{m.from}</td>
              <td class="px-3.5 py-2.5 font-bold text-primary">{m.to}</td>
              <td class="px-3.5 py-2.5 text-right font-mono font-bold text-on-surface">{m.qty}</td>
              {#if $tenantConfigStore.clientMode !== 'none'}
                <td class="px-3.5 py-2.5">{m.clientName}</td>
              {/if}
              <td class="px-3.5 py-2.5 font-mono text-on-surface-variant">{m.referenceNumber}</td>
              <td class="px-3.5 py-2.5 text-on-surface-variant">{m.user}</td>
              <td class="px-3.5 py-2.5 text-center">
                {#if m.isReversed}
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-300">
                    Reversed
                  </span>
                {:else}
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300">
                    Active
                  </span>
                {/if}
              </td>
              <td class="px-3.5 py-2.5 text-right">
                {#if !m.isReversed && onReverseClick}
                  <button
                    type="button"
                    onclick={() => onReverseClick(m)}
                    class="px-2 py-1 text-[11px] font-bold text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950/40 rounded transition-colors cursor-pointer"
                  >
                    Reverse
                  </button>
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {:else}
    <EmptyState icon="history_toggle_off" title="No movement history" message="Movements recorded for this lot will appear here." />
  {/if}
</section>
