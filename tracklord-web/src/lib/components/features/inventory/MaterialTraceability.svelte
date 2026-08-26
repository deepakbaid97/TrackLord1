<script>
  let {
    traceability = null,
    onLogMaterialClick,
    onEditConsumptionClick
  } = $props();

  let consumptions = $derived(traceability?.consumptions || []);
  let totalCost = $derived(
    traceability?.totalMaterialCost ??
    consumptions.reduce((/** @type {number} */ a, /** @type {any} */ c) => a + (c.totalPrice ?? (c.quantityConsumed * (c.unitRate || 0))), 0)
  );
</script>

<section class="bg-surface p-4 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3">
  <div class="flex justify-between items-center border-b border-outline-variant/60 pb-2">
    <div class="flex items-center gap-2">
      <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-1.5">
        <span class="material-symbols-outlined text-tertiary text-[18px]">texture</span>
        Raw Materials Logged
      </h2>
      {#if traceability && totalCost > 0}
        <span class="px-2 py-0.5 bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 text-[11px] font-bold rounded-full font-mono">
          ₹{Number(totalCost).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </span>
      {/if}
    </div>
    {#if onLogMaterialClick}
      <button
        type="button"
        onclick={onLogMaterialClick}
        class="text-xs text-primary hover:underline font-bold flex items-center gap-1 cursor-pointer"
      >
        <span class="material-symbols-outlined text-[15px]">add</span>
        Log Material
      </button>
    {/if}
  </div>

  {#if consumptions.length > 0}
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
      {#each consumptions as mat}
        <div class="bg-surface-container-low p-3 rounded-xl border border-outline-variant/60 flex flex-col justify-between relative group">
          <div class="flex justify-between items-start mb-1.5">
            <div class="flex items-center gap-1.5 min-w-0">
              <span class="material-symbols-outlined text-tertiary text-[18px] shrink-0">texture</span>
              <div class="min-w-0">
                <div class="font-bold text-xs text-on-surface font-mono truncate">{mat.materialLotNumber}</div>
                {#if mat.materialName}
                  <div class="text-[10px] text-on-surface-variant truncate max-w-[140px]">{mat.materialName}</div>
                {/if}
              </div>
            </div>
            {#if onEditConsumptionClick}
              <button
                type="button"
                onclick={() => onEditConsumptionClick(mat)}
                title="Edit Material Consumption"
                class="p-1 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer shrink-0"
              >
                <span class="material-symbols-outlined text-[15px]">edit</span>
              </button>
            {/if}
          </div>

          <div class="text-xl font-bold text-primary font-mono">{mat.quantityConsumed} {mat.unit}</div>

          {#if mat.unitRate !== undefined && mat.unitRate !== null}
            {@const itemTotal = mat.totalPrice ?? (mat.quantityConsumed * mat.unitRate)}
            <div class="text-[11px] font-mono font-semibold text-emerald-700 dark:text-emerald-300 mt-1 flex items-center justify-between bg-emerald-50 dark:bg-emerald-950/40 px-2 py-0.5 rounded border border-emerald-200/50 dark:border-emerald-800/50">
              <span>@ ₹{Number(mat.unitRate).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</span>
              <span class="font-bold">₹{Number(itemTotal).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</span>
            </div>
          {/if}
        </div>
      {/each}
    </div>
  {:else}
    <div class="p-6 text-center text-xs text-on-surface-variant font-mono bg-surface-container-low rounded-xl border border-dashed border-outline-variant/60">
      No raw material consumptions recorded for this lot yet.
    </div>
  {/if}
</section>
