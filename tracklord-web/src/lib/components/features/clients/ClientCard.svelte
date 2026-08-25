<script>
  let { client } = $props();

  const typeColorClasses = {
    'Buyer': 'bg-blue-100 text-blue-800 dark:bg-blue-950/60 dark:text-blue-300 border border-blue-200 dark:border-blue-800',
    'Wholesaler': 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800',
    'Job Worker': 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-200 dark:border-amber-800',
    'default': 'bg-purple-100 text-purple-800 dark:bg-purple-950/60 dark:text-purple-300 border border-purple-200 dark:border-purple-800'
  };

  let badgeClass = $derived(typeColorClasses[client.clientType] || typeColorClasses['default']);
</script>

<div
  role="button"
  tabindex="0"
  aria-label="View lots for {client.name}"
  onclick={() => window.location.href = `/lots?client=${client.id}`}
  onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (window.location.href = `/lots?client=${client.id}`)}
  class="bg-surface p-3 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-xs hover:shadow-sm transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
  title="View lots for {client.name}"
>
  <!-- Card Line 1: Icon & Name | Client Type Badge | WIP Qty -->
  <div class="flex items-center justify-between gap-2 text-xs">
    <div class="flex items-center gap-1.5 min-w-0">
      <div class="font-bold text-sm text-primary flex items-center gap-1 shrink-0 font-mono">
        <span class="material-symbols-outlined text-[16px]">badge</span>
        <span>{client.name}</span>
      </div>
      <span class="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider shrink-0 {badgeClass}">
        {client.clientType}
      </span>
    </div>

    <div class="flex items-center gap-1.5 shrink-0 font-mono">
      <span class="font-bold text-xs text-primary">
        {(client.totalWipQuantity || 0).toLocaleString()} Pcs
      </span>
      <span class="material-symbols-outlined text-[16px] text-on-surface-variant/40 group-hover:text-primary transition-colors pointer-events-none">
        chevron_right
      </span>
    </div>
  </div>

  <!-- Card Line 2: Active Lots Count, Phone Number, Registered Date -->
  <div class="flex flex-wrap gap-1 items-center pt-1 border-t border-dashed border-outline-variant/50 text-[10px] font-mono">
    <!-- Active Lots Badge -->
    <span class="px-2 py-0.5 rounded font-bold bg-primary-container text-on-primary-container flex items-center gap-1">
      <span class="material-symbols-outlined text-[11px]">inventory_2</span>
      {client.activeLotsCount} {client.activeLotsCount === 1 ? 'Active Lot' : 'Active Lots'}
    </span>

    <!-- Phone Number Pill -->
    {#if client.phoneNumber}
      <span class="px-2 py-0.5 rounded bg-surface-container-low text-on-surface-variant border border-outline-variant/60 flex items-center gap-0.5">
        <span class="material-symbols-outlined text-[11px]">call</span>
        {client.phoneNumber}
      </span>
    {/if}

    <!-- Registered Date Pill -->
    {#if client.createdAtUtc}
      <span class="px-1.5 py-0.5 rounded bg-surface-container-low text-on-surface-variant/70 border border-outline-variant/60 ml-auto">
        {new Date(client.createdAtUtc).toLocaleDateString()}
      </span>
    {/if}
  </div>
</div>
