<script>
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

  let {
    timelineSteps = [],
    totalLotWip = 0,
    onMoveStep
  } = $props();
</script>

<section class="bg-surface p-4 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3">
  <div class="flex justify-between items-center border-b border-outline-variant/60 pb-2">
    <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-1.5">
      <span class="material-symbols-outlined text-primary text-[18px]">bar_chart</span>
      WIP Distribution
    </h2>
    <span class="text-xs font-mono font-bold text-primary">{totalLotWip} Pcs Total</span>
  </div>

  {#if totalLotWip > 0}
    <div class="h-2.5 w-full rounded-full bg-surface-container overflow-hidden flex shadow-inner">
      {#each timelineSteps as step}
        {@const pct = (step.quantity / totalLotWip) * 100}
        {#if pct > 0}
          <div
            class="h-full bg-primary transition-all duration-500 first:rounded-l-full last:rounded-r-full"
            style="width: {pct}%"
            title="{step.name}: {step.quantity} Pcs ({Math.round(pct)}%)"
          ></div>
        {/if}
      {/each}
    </div>
  {/if}

  <div class="space-y-1.5 pt-1">
    {#each timelineSteps as step}
      {@const hasWip = step.quantity > 0}
      <div class="flex items-center justify-between p-2 rounded-xl transition-colors {hasWip ? 'bg-primary/5 border border-primary/20' : 'hover:bg-surface-container-low opacity-60'}">
        <div class="flex items-center gap-2 min-w-0 flex-1">
          <span class="w-2 h-2 rounded-full shrink-0 {hasWip ? 'bg-primary animate-pulse' : 'bg-slate-300 dark:bg-slate-600'}"></span>
          <span class="text-xs sm:text-sm font-semibold {hasWip ? 'text-primary' : 'text-on-surface'} truncate">{step.name}</span>
          <span class="text-[11px] font-mono text-on-surface-variant shrink-0">({step.code})</span>
          {#if $tenantConfigStore.clientMode !== 'none' && step.clientName}
            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-amber-500/10 text-amber-900 dark:text-amber-300 border border-amber-500/25 shrink-0 max-w-[130px] sm:max-w-[200px]">
              <span class="material-symbols-outlined text-[12px] text-amber-600 dark:text-amber-400">badge</span>
              <span class="truncate">{step.clientName}</span>
            </span>
          {/if}
        </div>
        <div class="flex items-center gap-2 shrink-0 pl-2">
          <span class="font-mono text-xs sm:text-sm font-bold {hasWip ? 'text-primary' : 'text-on-surface-variant'}">{step.quantity} Pcs</span>
          {#if hasWip && onMoveStep}
            <button
              type="button"
              onclick={() => onMoveStep(step.id)}
              class="px-2 py-0.5 bg-primary text-white rounded text-[11px] font-bold hover:bg-blue-700 transition-all cursor-pointer"
            >
              Move
            </button>
          {/if}
        </div>
      </div>
    {/each}
  </div>
</section>
