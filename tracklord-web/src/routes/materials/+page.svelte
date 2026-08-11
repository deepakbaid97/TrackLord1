<script>
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import Button from '$lib/components/ui/Button.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import { getMaterialLots, createMaterialLot } from '$lib/api.js';

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
  let createMaterialForm = $state({
    lotNumber: '',
    materialName: '',
    initialQuantity: 500,
    unit: 2, // Meters
    unitRate: 0
  });

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

  /** @param {SubmitEvent} e */
  async function handleCreateMaterialSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        lotNumber: createMaterialForm.lotNumber.trim(),
        materialItemId: '00000000-0000-0000-0000-000000000002',
        materialName: createMaterialForm.materialName.trim(),
        initialQuantity: Number(createMaterialForm.initialQuantity),
        unit: Number(createMaterialForm.unit),
        unitRate: Number(createMaterialForm.unitRate) || 0
      };

      await createMaterialLot(payload);
      successMessage = `Raw Material Lot '${createMaterialForm.lotNumber}' registered successfully!`;
      isCreateMaterialModalOpen = false;
      createMaterialForm.lotNumber = '';
      createMaterialForm.materialName = '';
      createMaterialForm.unitRate = 0;

      await fetchMaterials();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to register material lot.';
    }
  }

  /** @param {string} id */
  function viewMaterialHistory(id) {
    goto(`/materials/${id}`);
  }
</script>

<!-- Top App Header -->
<header class="fixed top-0 w-full z-40 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">texture</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD ERP</div>
      <div class="text-xs text-on-surface-variant">Raw Materials Stock & History</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
    <button
      onclick={() => fetchMaterials()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh
    </button>
  </div>
</header>

<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-10">

  <!-- Notification Banner -->
  {#if errorMessage}
    <div class="p-4 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 rounded-xl text-red-700 dark:text-red-300 text-sm flex items-center justify-between shadow-sm">
      <div class="flex items-center gap-2">
        <span class="material-symbols-outlined text-red-500">error</span>
        <span>{errorMessage}</span>
      </div>
      <button onclick={() => errorMessage = ''} class="text-red-500 hover:text-red-700">
        <span class="material-symbols-outlined text-[18px]">close</span>
      </button>
    </div>
  {/if}

  {#if successMessage}
    <div class="p-4 bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 rounded-xl text-emerald-700 dark:text-emerald-300 text-sm flex items-center justify-between shadow-sm">
      <div class="flex items-center gap-2">
        <span class="material-symbols-outlined text-emerald-500">check_circle</span>
        <span>{successMessage}</span>
      </div>
      <button onclick={() => successMessage = ''} class="text-emerald-500 hover:text-emerald-700">
        <span class="material-symbols-outlined text-[18px]">close</span>
      </button>
    </div>
  {/if}

  <!-- Page Title & Primary Action -->
  <section class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-surface p-6 rounded-2xl border border-outline-variant shadow-sm">
    <div>
      <h1 class="text-2xl font-bold text-primary flex items-center gap-2">
        <span class="material-symbols-outlined text-tertiary text-[28px]">inventory_2</span>
        Raw Materials Ledger
      </h1>
      <p class="text-sm text-on-surface-variant mt-1">
        Monitor raw material stock levels, unit rates, and complete consumption audit trails.
      </p>
    </div>

    <Button variant="primary" icon="add" onclick={() => isCreateMaterialModalOpen = true}>
      Register Raw Material
    </Button>
  </section>

  <!-- Summary Cards -->
  <section class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-primary/10 text-primary rounded-xl">
        <span class="material-symbols-outlined text-[28px]">inventory</span>
      </div>
      <div>
        <div class="text-xs font-mono uppercase text-on-surface-variant">Total Material Lots</div>
        <div class="text-2xl font-bold text-on-surface">{totalLotsCount}</div>
      </div>
    </div>

    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 rounded-xl">
        <span class="material-symbols-outlined text-[28px]">workspaces</span>
      </div>
      <div>
        <div class="text-xs font-mono uppercase text-on-surface-variant">Available Stock</div>
        <div class="text-2xl font-bold text-emerald-600 dark:text-emerald-400 font-mono">
          {totalAvailableQty.toLocaleString()}
        </div>
      </div>
    </div>

    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-tertiary/10 text-tertiary rounded-xl">
        <span class="material-symbols-outlined text-[28px]">payments</span>
      </div>
      <div>
        <div class="text-xs font-mono uppercase text-on-surface-variant">Inventory Valuation</div>
        <div class="text-2xl font-bold text-on-surface font-mono">
          ₹{totalValuation.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </div>
      </div>
    </div>

    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-amber-500/10 text-amber-600 dark:text-amber-400 rounded-xl">
        <span class="material-symbols-outlined text-[28px]">warning</span>
      </div>
      <div>
        <div class="text-xs font-mono uppercase text-on-surface-variant">Depleted Lots</div>
        <div class="text-2xl font-bold text-amber-600 dark:text-amber-400">{depletedCount}</div>
      </div>
    </div>
  </section>

  <!-- Search & Filtering Bar -->
  <section class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex flex-col md:flex-row justify-between items-stretch md:items-center gap-4">
    <!-- Search Box -->
    <div class="relative flex-1 max-w-md">
      <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[20px]">search</span>
      <input
        type="text"
        bind:value={searchQuery}
        placeholder="Search material lot number or item name..."
        class="w-full pl-10 pr-4 py-2 bg-surface-container-low border border-outline-variant rounded-xl text-sm text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
      />
      {#if searchQuery}
        <button
          onclick={() => searchQuery = ''}
          class="absolute right-3 top-2.5 text-on-surface-variant hover:text-on-surface"
        >
          <span class="material-symbols-outlined text-[18px]">close</span>
        </button>
      {/if}
    </div>

    <!-- Status Filters -->
    <div class="flex items-center gap-2">
      <span class="text-xs font-semibold text-on-surface-variant mr-1">Filter:</span>
      <button
        onclick={() => filterStatus = 'all'}
        class="px-3 py-1.5 rounded-lg text-xs font-bold transition-colors cursor-pointer {filterStatus === 'all' ? 'bg-primary text-on-primary shadow-sm' : 'bg-surface-container-low border border-outline-variant text-on-surface-variant hover:bg-surface-container'}"
      >
        All Materials ({totalLotsCount})
      </button>
      <button
        onclick={() => filterStatus = 'available'}
        class="px-3 py-1.5 rounded-lg text-xs font-bold transition-colors cursor-pointer {filterStatus === 'available' ? 'bg-emerald-600 text-white shadow-sm' : 'bg-surface-container-low border border-outline-variant text-on-surface-variant hover:bg-surface-container'}"
      >
        In Stock ({totalLotsCount - depletedCount})
      </button>
      <button
        onclick={() => filterStatus = 'depleted'}
        class="px-3 py-1.5 rounded-lg text-xs font-bold transition-colors cursor-pointer {filterStatus === 'depleted' ? 'bg-amber-600 text-white shadow-sm' : 'bg-surface-container-low border border-outline-variant text-on-surface-variant hover:bg-surface-container'}"
      >
        Depleted ({depletedCount})
      </button>
    </div>
  </section>

  <!-- Materials Table & Cards -->
  {#if loading}
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span>Fetching materials inventory...</span>
    </div>
  {:else if filteredMaterials.length > 0}
    <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface shadow-sm">
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
            <th class="px-4 py-3.5 text-right">Consumption History</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-outline-variant/50 text-on-surface">
          {#each filteredMaterials as mat}
            {@const remaining = mat.remainingQuantity ?? mat.initialQuantity ?? 0}
            {@const initial = mat.initialQuantity ?? 0}
            {@const usagePercent = initial > 0 ? (remaining / initial) * 100 : 0}
            {@const stockValuation = remaining * (mat.unitRate || 0)}
            <tr
              onclick={() => viewMaterialHistory(mat.id)}
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
                <button
                  onclick={(e) => { e.stopPropagation(); viewMaterialHistory(mat.id); }}
                  class="px-3 py-1 text-xs font-semibold bg-primary/10 text-primary hover:bg-primary hover:text-white rounded-lg transition-colors flex items-center gap-1 ml-auto cursor-pointer"
                >
                  <span class="material-symbols-outlined text-[14px]">history</span>
                  View History
                </button>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {:else}
    <div class="p-12 text-center text-on-surface-variant bg-surface rounded-xl border border-dashed border-outline-variant">
      <span class="material-symbols-outlined text-[48px] text-on-surface-variant/40 mb-2 block">search_off</span>
      <h3 class="text-base font-bold text-on-surface">No matching raw materials found</h3>
      <p class="text-xs text-on-surface-variant mt-1">Try adjusting your search query or filter status.</p>
    </div>
  {/if}

</main>

<!-- Modal: Register Raw Material Lot -->
<Modal bind:isOpen={isCreateMaterialModalOpen} title="Register Raw Material Lot">
  <form onsubmit={handleCreateMaterialSubmit} class="space-y-4">
    <Input
      label="Material Lot Number"
      bind:value={createMaterialForm.lotNumber}
      placeholder="e.g. FAB-2026-001"
      icon="tag"
      required={true}
    />

    <Input
      label="Product / Material Name"
      bind:value={createMaterialForm.materialName}
      placeholder="e.g. Denim Grey Cloth 300GSM"
      icon="category"
      required={true}
    />

    <Input
      label="Initial Quantity"
      type="number"
      step={Number(createMaterialForm.unit) === 1 ? '1' : 'any'}
      bind:value={createMaterialForm.initialQuantity}
      placeholder={Number(createMaterialForm.unit) === 1 ? 'e.g. 500' : 'e.g. 500.5'}
      icon="numbers"
      required={true}
    />

    <Select
      label="Unit of Measurement"
      bind:value={createMaterialForm.unit}
      options={[
        { label: 'Meters', value: 2 },
        { label: 'Pieces', value: 1 },
        { label: 'Kilograms', value: 3 },
        { label: 'Yards', value: 5 }
      ]}
      icon="square_foot"
      required={true}
    />

    <Input
      label="Rate per Unit (₹)"
      type="number"
      step="any"
      bind:value={createMaterialForm.unitRate}
      placeholder="e.g. 150.00"
      icon="payments"
      required={false}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isCreateMaterialModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save">
        Register Material
      </Button>
    </div>
  </form>
</Modal>
