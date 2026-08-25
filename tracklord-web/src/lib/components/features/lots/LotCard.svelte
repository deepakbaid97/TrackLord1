<script>
  import { STEP_ICON_MAP } from '$lib/services/lotService.js';

  let {
    lot,
    steps = [],
    selectedStepId = 'ALL'
  } = $props();

  let displayedQuantity = $derived(
    selectedStepId !== 'ALL'
      ? (lot.activeSteps?.find(s => s.stepId === selectedStepId)?.quantity ?? 0)
      : (lot.totalWipQty || 0)
  );
</script>

<div
  role="button"
  tabindex="0"
  aria-label="View lot {lot.lotNumber} details"
  onclick={() => window.location.href = `/inventory?lot=${lot.id}`}
  onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (window.location.href = `/inventory?lot=${lot.id}`)}
  class="bg-surface p-3 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-xs hover:shadow-sm transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
  title="View lot {lot.lotNumber} details"
>
  <!-- Card Line 1: Lot #, Client, Quantity & Date -->
  <div class="flex items-center justify-between gap-2 text-xs">
    <div class="flex items-center gap-1.5 min-w-0">
      <div class="font-bold text-sm text-primary flex items-center gap-1 shrink-0 font-mono">
        <span class="material-symbols-outlined text-[15px]">tag</span>
        <span>{lot.lotNumber}</span>
      </div>
      {#if lot.clientNamesDisplay}
        <span class="text-on-surface-variant text-[11px] truncate">• {lot.clientNamesDisplay}</span>
      {/if}
    </div>
    <div class="flex items-center gap-1.5 shrink-0 font-mono">
      <span class="font-bold text-xs text-primary">
        {displayedQuantity.toLocaleString()} Pcs
      </span>
      {#if lot.createdDate}
        <span class="text-[10px] text-on-surface-variant">• {lot.createdDate.toLocaleDateString()}</span>
      {/if}
    </div>
  </div>

  <!-- Card Line 2: Active Steps Pill List -->
  {#if lot.activeSteps && lot.activeSteps.length > 0}
    <div class="flex flex-wrap gap-1 items-center pt-1 border-t border-dashed border-outline-variant/50">
      {#each lot.activeSteps as step}
        {@const stepObj = steps.find(s => s.id === step.stepId || s.code === step.stepCode)}
        <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono {stepObj?.isTerminating ? 'bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300' : stepObj?.isActive === false ? 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 border border-zinc-300 dark:border-zinc-700' : 'bg-primary-container text-on-primary-container'} flex items-center gap-1">
          <span class="material-symbols-outlined text-[11px]">{STEP_ICON_MAP[step.stepCode] || 'work'}</span>
          {step.stepName}: {step.quantity} Pcs
          {#if stepObj?.isTerminating}
            <span class="text-[8px] uppercase tracking-wider font-extrabold text-amber-700 dark:text-amber-400 ml-0.5">(Terminated)</span>
          {:else if stepObj?.isActive === false}
            <span class="text-[8px] uppercase tracking-wider font-extrabold text-zinc-500 ml-0.5">(Inactive)</span>
          {/if}
        </span>
      {/each}
    </div>
  {/if}
</div>
