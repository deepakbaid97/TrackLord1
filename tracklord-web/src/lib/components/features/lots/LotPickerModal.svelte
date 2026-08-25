<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import SearchBox from '$lib/components/ui/SearchBox.svelte';

  let {
    isOpen = $bindable(false),
    lots = [],
    selectedLotId = '',
    onSelect
  } = $props();

  let searchFilter = $state('');

  let filteredLots = $derived(
    lots.filter(l =>
      (l.lotNumber || '').toLowerCase().includes(searchFilter.toLowerCase()) ||
      (l.status || '').toLowerCase().includes(searchFilter.toLowerCase()) ||
      (l.clientName || '').toLowerCase().includes(searchFilter.toLowerCase())
    )
  );

  function handleSelect(lotId) {
    if (onSelect) onSelect(lotId);
    isOpen = false;
  }
</script>

<Modal bind:isOpen title="Select Production Lot">
  <div class="space-y-3 max-h-[75vh] flex flex-col">
    <SearchBox bind:value={searchFilter} placeholder="Search by lot number, status, client..." />

    <div class="overflow-y-auto space-y-1.5 max-h-[50vh] pr-1">
      {#each filteredLots as lot (lot.id)}
        <button
          type="button"
          onclick={() => handleSelect(lot.id)}
          class="w-full p-2.5 rounded-xl border text-left flex items-center justify-between transition-all cursor-pointer {lot.id === selectedLotId ? 'bg-primary/10 border-primary text-primary font-bold' : 'bg-surface hover:bg-surface-container-low border-outline-variant/60 text-on-surface'}"
        >
          <div class="flex items-center gap-2 truncate">
            <span class="material-symbols-outlined text-[20px] {lot.id === selectedLotId ? 'text-primary' : 'text-on-surface-variant'}">inventory_2</span>
            <div class="truncate">
              <div class="font-mono text-sm">{lot.lotNumber}</div>
              {#if lot.clientName}
                <div class="text-[11px] text-on-surface-variant truncate font-sans">{lot.clientName}</div>
              {/if}
            </div>
          </div>
          <div class="flex items-center gap-2 shrink-0 font-mono text-xs">
            <span>{lot.initialQuantity} Pcs</span>
            <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-surface-container text-on-surface-variant">
              {lot.status}
            </span>
          </div>
        </button>
      {/each}
      {#if filteredLots.length === 0}
        <div class="p-6 text-center text-on-surface-variant text-xs font-mono">
          No lots found matching search.
        </div>
      {/if}
    </div>
  </div>
</Modal>
