<script>
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import SearchBox from '$lib/components/ui/SearchBox.svelte';
  import KpiStrip from '$lib/components/ui/KpiStrip.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import MaterialCard from '$lib/components/features/materials/MaterialCard.svelte';
  import CreateMaterialModal from '$lib/components/features/materials/CreateMaterialModal.svelte';
  import EditMaterialModal from '$lib/components/features/materials/EditMaterialModal.svelte';
  import ImportMaterialsModal from '$lib/components/features/materials/ImportMaterialsModal.svelte';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';
  import { getMaterialLots } from '$lib/api.js';

  // App State
  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  /** @type {Array<any>} */
  let materialLots = $state([]);
  let searchQuery = $state('');
  let filterStatus = $state('all'); // 'all', 'available', 'depleted'

  // Modals state
  let isCreateMaterialModalOpen = $state(false);
  let isEditMaterialModalOpen = $state(false);
  let isImportModalOpen = $state(false);
  /** @type {any} */
  let selectedMaterialForEdit = $state(null);

  usePageHeader(() => ({
    title: 'Raw Materials',
    subtitle: 'Stock & Consumption',
    icon: 'texture',
    actions: [
      {
        label: 'Register Material',
        icon: 'add',
        variant: 'primary',
        onclick: () => { isCreateMaterialModalOpen = true; }
      },
      {
        label: 'Import Lots',
        icon: 'file_upload',
        variant: 'outline',
        onclick: () => { isImportModalOpen = true; }
      },
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: 'outline',
        onclick: () => { fetchMaterials(); }
      }
    ]
  }));

  onMount(async () => {
    await fetchMaterials();
  });

  async function fetchMaterials() {
    loading = true;
    errorMessage = '';
    try {
      const data = await getMaterialLots();
      materialLots = data || [];
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load material lots.';
    } finally {
      loading = false;
    }
  }

  // Derived filtered materials
  let filteredMaterials = $derived.by(() => {
    return materialLots.filter(mat => {
      const matchSearch =
        !searchQuery.trim() ||
        (mat.lotNumber && mat.lotNumber.toLowerCase().includes(searchQuery.toLowerCase().trim())) ||
        (mat.materialName && mat.materialName.toLowerCase().includes(searchQuery.toLowerCase().trim()));

      const remaining = mat.remainingQuantity ?? mat.initialQuantity ?? 0;
      let matchStatus = true;
      if (filterStatus === 'available') matchStatus = remaining > 0;
      if (filterStatus === 'depleted') matchStatus = remaining <= 0;

      return matchSearch && matchStatus;
    });
  });

  // Summary statistics
  let totalLotsCount = $derived(materialLots.length);
  let totalAvailableQty = $derived.by(() => {
    return materialLots.reduce((acc, m) => acc + (m.remainingQuantity ?? m.initialQuantity ?? 0), 0);
  });
  let totalValuation = $derived.by(() => {
    return materialLots.reduce((acc, m) => {
      const rem = m.remainingQuantity ?? m.initialQuantity ?? 0;
      return acc + (rem * (m.unitRate || 0));
    }, 0);
  });
  let depletedCount = $derived.by(() => {
    return materialLots.filter(m => (m.remainingQuantity ?? m.initialQuantity ?? 0) <= 0).length;
  });

  let kpiItems = $derived([
    { label: 'Lots', value: totalLotsCount, icon: 'inventory', color: 'text-primary' },
    { label: 'Stock', value: totalAvailableQty.toLocaleString(), icon: 'workspaces', color: 'text-emerald-600 dark:text-emerald-400' },
    { label: 'Valuation', value: `₹${totalValuation.toLocaleString('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`, icon: 'payments', color: 'text-on-surface' },
    { label: 'Depleted', value: depletedCount, icon: 'warning', color: 'text-amber-600 dark:text-amber-400' }
  ]);

  /** @param {any} mat */
  function openEditModal(mat) {
    selectedMaterialForEdit = mat;
    isEditMaterialModalOpen = true;
  }
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  <!-- Unified 4-Metric KPI Strip -->
  <KpiStrip items={kpiItems} />

  <!-- Search & Filtering Bar -->
  <section class="bg-surface p-3.5 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm flex flex-col md:flex-row justify-between items-stretch md:items-center gap-3 md:gap-4">
    <div class="flex-1 max-w-md">
      <SearchBox bind:value={searchQuery} placeholder="Search material lot number or item name..." />
    </div>

    <!-- Status Filters -->
    <div class="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0 shrink-0">
      <span class="text-xs font-semibold text-on-surface-variant mr-1 shrink-0 font-mono">Filter:</span>
      <button
        type="button"
        onclick={() => filterStatus = 'all'}
        class="px-3 py-1.5 rounded-xl text-xs font-bold whitespace-nowrap shrink-0 transition-colors cursor-pointer {filterStatus === 'all' ? 'bg-primary text-on-primary shadow-xs' : 'bg-surface-container-low border border-outline-variant/70 text-on-surface-variant hover:bg-surface-container'}"
      >
        All Materials ({totalLotsCount})
      </button>
      <button
        type="button"
        onclick={() => filterStatus = 'available'}
        class="px-3 py-1.5 rounded-xl text-xs font-bold whitespace-nowrap shrink-0 transition-colors cursor-pointer {filterStatus === 'available' ? 'bg-emerald-600 text-white shadow-xs' : 'bg-surface-container-low border border-outline-variant/70 text-on-surface-variant hover:bg-surface-container'}"
      >
        In Stock ({totalLotsCount - depletedCount})
      </button>
      <button
        type="button"
        onclick={() => filterStatus = 'depleted'}
        class="px-3 py-1.5 rounded-xl text-xs font-bold whitespace-nowrap shrink-0 transition-colors cursor-pointer {filterStatus === 'depleted' ? 'bg-amber-600 text-white shadow-xs' : 'bg-surface-container-low border border-outline-variant/70 text-on-surface-variant hover:bg-surface-container'}"
      >
        Depleted ({depletedCount})
      </button>
    </div>
  </section>

  <!-- Materials Table & Cards -->
  {#if loading}
    <LoadingSpinner message="Fetching materials inventory..." />
  {:else if filteredMaterials.length > 0}
    <!-- Mobile View: Card Stack Column (below md) -->
    <div class="block md:hidden space-y-2">
      {#each filteredMaterials as mat (mat.id)}
        <MaterialCard
          material={mat}
          onClick={() => goto(`/materials/${mat.id}`)}
          onEdit={() => openEditModal(mat)}
        />
      {/each}
    </div>

    <!-- Desktop View: Table (md and up) -->
    <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface shadow-sm">
      <table class="w-full text-left border-collapse text-sm">
        <thead>
          <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
            <th class="px-4 py-3.5">Material Lot Number</th>
            <th class="px-4 py-3.5">Material / Item Name</th>
            <th class="px-4 py-3.5 text-right">Initial Qty</th>
            <th class="px-4 py-3.5 text-right">Remaining Stock</th>
            <th class="px-4 py-3.5">Unit</th>
            <th class="px-4 py-3.5 text-right">Unit Rate (₹)</th>
            <th class="px-4 py-3.5 text-right">Stock Valuation</th>
            <th class="px-4 py-3.5 text-right">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-outline-variant/50 text-on-surface">
          {#each filteredMaterials as mat (mat.id)}
            {@const remaining = mat.remainingQuantity ?? mat.initialQuantity ?? 0}
            {@const initial = mat.initialQuantity ?? 0}
            {@const usagePercent = initial > 0 ? (remaining / initial) * 100 : 0}
            {@const stockValuation = remaining * (mat.unitRate || 0)}
            <tr
              onclick={() => goto(`/materials/${mat.id}`)}
              class="hover:bg-surface-container-low/70 transition-colors cursor-pointer group"
            >
              <td class="px-4 py-3.5 whitespace-nowrap">
                <div class="flex items-center gap-2">
                  <div class="p-1.5 bg-primary/10 text-primary rounded-lg group-hover:bg-primary group-hover:text-white transition-colors">
                    <span class="material-symbols-outlined text-[18px]">texture</span>
                  </div>
                  <span class="font-bold text-primary group-hover:underline font-mono">{mat.lotNumber}</span>
                </div>
              </td>
              <td class="px-4 py-3.5 font-medium text-on-surface">{mat.materialName ?? '—'}</td>
              <td class="px-4 py-3.5 text-right font-mono text-on-surface-variant">{Number(initial).toLocaleString()}</td>
              <td class="px-4 py-3.5 text-right whitespace-nowrap">
                <div class="flex items-center justify-end gap-2">
                  <div class="w-16 bg-surface-variant/60 rounded-full h-1.5 hidden sm:block">
                    <div
                      class="h-1.5 rounded-full transition-all duration-500 {remaining <= 0 ? 'bg-red-500' : usagePercent < 25 ? 'bg-amber-500' : 'bg-emerald-500'}"
                      style="width: {Math.min(Math.max(usagePercent, 0), 100)}%"
                    ></div>
                  </div>
                  <span class="font-bold font-mono {remaining <= 0 ? 'text-red-500' : usagePercent < 25 ? 'text-amber-600' : 'text-emerald-600'}">
                    {Number(remaining).toLocaleString()}
                  </span>
                </div>
              </td>
              <td class="px-4 py-3.5 font-mono text-on-surface-variant">{mat.unit}</td>
              <td class="px-4 py-3.5 text-right font-mono font-semibold text-on-surface">
                {mat.unitRate ? `₹${Number(mat.unitRate).toLocaleString('en-IN', { minimumFractionDigits: 2 })}` : '—'}
              </td>
              <td class="px-4 py-3.5 text-right font-mono font-bold text-emerald-700 dark:text-emerald-400">
                ₹{Number(stockValuation).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </td>
              <td class="px-4 py-3.5 text-right whitespace-nowrap">
                <div class="flex items-center justify-end gap-1.5">
                  <button
                    onclick={(e) => { e.stopPropagation(); openEditModal(mat); }}
                    class="p-1.5 text-on-surface-variant hover:text-primary hover:bg-primary/10 rounded-lg transition-colors cursor-pointer"
                    title="Edit Material Lot"
                  >
                    <span class="material-symbols-outlined text-[18px]">edit</span>
                  </button>
                  <button
                    onclick={(e) => { e.stopPropagation(); goto(`/materials/${mat.id}`); }}
                    class="px-2.5 py-1 text-xs font-semibold bg-primary/10 text-primary hover:bg-primary hover:text-white rounded-lg transition-colors flex items-center gap-1 cursor-pointer"
                    title="View Material Consumption History"
                  >
                    <span class="material-symbols-outlined text-[14px]">history</span>
                    History
                  </button>
                </div>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {:else}
    <EmptyState
      icon="search_off"
      title="No matching raw materials found"
      message="Try adjusting your search query or filter status."
    />
  {/if}
</main>

<!-- Modals -->
<CreateMaterialModal
  bind:isOpen={isCreateMaterialModalOpen}
  onSuccess={(mat) => {
    successMessage = `Raw Material Lot '${mat.lotNumber}' registered successfully!`;
    fetchMaterials();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to register material lot.';
  }}
/>

<EditMaterialModal
  bind:isOpen={isEditMaterialModalOpen}
  material={selectedMaterialForEdit}
  onSuccess={(lotNum) => {
    successMessage = `Raw Material Lot '${lotNum}' updated successfully!`;
    fetchMaterials();
  }}
  onDelete={(lotNum) => {
    successMessage = `Raw Material Lot '${lotNum}' deleted successfully.`;
    fetchMaterials();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Operation failed.';
  }}
/>

<ImportMaterialsModal
  bind:isOpen={isImportModalOpen}
  onSuccess={() => {
    successMessage = 'Materials imported successfully!';
    fetchMaterials();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Import failed.';
  }}
/>
