<script>
  let {
    material,
    onEdit,
    onClick
  } = $props();

  let remaining = $derived(material.remainingQuantity ?? material.initialQuantity ?? 0);
  let initial = $derived(material.initialQuantity ?? 0);
  let usagePercent = $derived(initial > 0 ? (remaining / initial) * 100 : 0);
  let stockValuation = $derived(remaining * (material.unitRate || 0));
</script>

<div
  role="button"
  tabindex="0"
  onclick={onClick}
  onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && onClick && onClick()}
  class="bg-surface p-3 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-xs hover:shadow-sm transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
  title="View material {material.lotNumber} details and consumption history"
>
  <!-- Card Line 1: Lot #, Material Name, Stock Quantity & Edit Button -->
  <div class="flex items-center justify-between gap-2 text-xs">
    <div class="flex items-center gap-1.5 min-w-0">
      <div class="font-bold text-sm text-primary flex items-center gap-1 shrink-0 font-mono">
        <span class="material-symbols-outlined text-[15px]">tag</span>
        <span>{material.lotNumber}</span>
      </div>
      {#if material.materialName}
        <span class="text-on-surface-variant text-[11px] truncate">• {material.materialName}</span>
      {/if}
    </div>

    <div class="flex items-center gap-1.5 shrink-0 font-mono">
      <span class="font-bold text-xs {remaining <= 0 ? 'text-red-500' : usagePercent < 25 ? 'text-amber-600' : 'text-emerald-600 dark:text-emerald-400'}">
        {Number(remaining).toLocaleString()} {material.unit}
      </span>
      <button
        type="button"
        onclick={(e) => { e.stopPropagation(); if (onEdit) onEdit(); }}
        title="Edit Material Lot"
        class="p-1 text-on-surface-variant hover:text-primary rounded-md border border-outline-variant/70 transition-colors cursor-pointer"
      >
        <span class="material-symbols-outlined text-[13px]">edit</span>
      </button>
    </div>
  </div>

  <!-- Card Line 2: Detailed Badges / Pills (Valuation, Rate, Status) -->
  <div class="flex flex-wrap gap-1 items-center pt-1 border-t border-dashed border-outline-variant/50 text-[10px] font-mono">
    <!-- Valuation Pill -->
    <span class="px-2 py-0.5 rounded font-bold bg-primary-container text-on-primary-container flex items-center gap-1">
      <span class="material-symbols-outlined text-[11px]">payments</span>
      Valuation: ₹{Number(stockValuation).toLocaleString('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
    </span>

    <!-- Rate Pill -->
    {#if material.unitRate}
      <span class="px-2 py-0.5 rounded bg-surface-container-low text-on-surface-variant border border-outline-variant/60">
        Rate: ₹{Number(material.unitRate).toLocaleString('en-IN')}/{material.unit}
      </span>
    {/if}

    <!-- Initial / Status Pill -->
    {#if remaining <= 0}
      <span class="px-1.5 py-0.5 rounded font-bold bg-red-100 dark:bg-red-950/60 text-red-700 dark:text-red-300 border border-red-300">
        Depleted
      </span>
    {:else}
      <span class="px-1.5 py-0.5 rounded bg-surface-container-low text-on-surface-variant border border-outline-variant/60">
        Initial: {Number(initial).toLocaleString()} {material.unit}
      </span>
    {/if}
  </div>
</div>
