<script>
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import Button from '$lib/components/ui/Button.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import { getMaterialLots, createMaterialLot, updateMaterialLot, deleteMaterialLot, importMaterialLots } from '$lib/api.js';
  import * as XLSX from 'xlsx';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

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

  let isEditMaterialModalOpen = $state(false);
  let isDeletingMaterial = $state(false);
  let editMaterialForm = $state({
    id: '',
    lotNumber: '',
    materialName: '',
    initialQuantity: 0,
    unit: 2,
    unitRate: 0
  });

  // Import Modal state
  let isImportModalOpen = $state(false);
  let importFile = $state(/** @type {File|null} */ (null));
  let importPreviewData = $state(/** @type {Array<any>} */ ([]));
  let isParsing = $state(false);
  let isImporting = $state(false);
  let importErrorMessage = $state('');
  let importSuccessResult = $state(/** @type {any} */ (null));

  const unitOptions = [
    { label: 'Meters', value: 2 },
    { label: 'Pieces', value: 1 },
    { label: 'Kilograms', value: 3 },
    { label: 'Yards', value: 5 }
  ];

  function getUnitValue(unitInput) {
    if (typeof unitInput === 'number') return unitInput;
    if (typeof unitInput === 'string') {
      const match = unitOptions.find(o => o.label.toLowerCase() === unitInput.toLowerCase());
      if (match) return match.value;
      const parsed = parseInt(unitInput, 10);
      if (!isNaN(parsed)) return parsed;
    }
    return 2;
  }

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

  /** @param {any} mat */
  function openEditModal(mat) {
    errorMessage = '';
    editMaterialForm = {
      id: mat.id,
      lotNumber: mat.lotNumber || '',
      materialName: mat.materialName || '',
      initialQuantity: mat.initialQuantity || 0,
      unit: getUnitValue(mat.unit),
      unitRate: mat.unitRate || 0
    };
    isEditMaterialModalOpen = true;
  }

  /** @param {SubmitEvent} e */
  async function handleEditMaterialSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        lotNumber: editMaterialForm.lotNumber.trim(),
        materialName: editMaterialForm.materialName.trim(),
        initialQuantity: Number(editMaterialForm.initialQuantity),
        unit: Number(editMaterialForm.unit),
        unitRate: Number(editMaterialForm.unitRate) || 0
      };

      await updateMaterialLot(editMaterialForm.id, payload);
      successMessage = `Raw Material Lot '${editMaterialForm.lotNumber}' updated successfully!`;
      isEditMaterialModalOpen = false;

      await fetchMaterials();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to update material lot.';
    }
  }

  async function handleDeleteMaterial() {
    if (!editMaterialForm.id) return;
    if (!confirm(`Are you sure you want to delete material lot '${editMaterialForm.lotNumber}'?`)) return;

    errorMessage = '';
    isDeletingMaterial = true;
    try {
      await deleteMaterialLot(editMaterialForm.id);
      successMessage = `Raw Material Lot '${editMaterialForm.lotNumber}' deleted successfully.`;
      isEditMaterialModalOpen = false;
      await fetchMaterials();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to delete raw material lot.';
    } finally {
      isDeletingMaterial = false;
    }
  }

  /** @param {string} id */
  function viewMaterialHistory(id) {
    goto(`/materials/${id}`);
  }

  function openImportModal() {
    isImportModalOpen = true;
    importFile = null;
    importPreviewData = [];
    importErrorMessage = '';
    importSuccessResult = null;
  }

  function closeImportModal() {
    isImportModalOpen = false;
    importFile = null;
    importPreviewData = [];
    importErrorMessage = '';
    importSuccessResult = null;
  }

  async function handleFileSelect(event) {
    const file = event.target.files?.[0];
    if (!file) return;
    importFile = file;
    isParsing = true;
    importErrorMessage = '';
    importPreviewData = [];
    importSuccessResult = null;

    try {
      const data = await file.arrayBuffer();
      const workbook = XLSX.read(data, { type: 'array' });
      const firstSheetName = workbook.SheetNames[0];
      if (!firstSheetName) {
        throw new Error('No worksheets found in file.');
      }

      const worksheet = workbook.Sheets[firstSheetName];
      const jsonRows = XLSX.utils.sheet_to_json(worksheet, { defval: '' });

      if (!jsonRows || jsonRows.length === 0) {
        throw new Error('Selected file contains no data rows.');
      }

      const sampleRow = jsonRows[0];
      const keys = Object.keys(sampleRow);

      const findKey = (candidates) => {
        return keys.find(k => {
          const lower = k.toLowerCase().replace(/[^a-z0-9]/g, '');
          return candidates.some(c => lower.includes(c));
        });
      };

      const lotKey = findKey(['lot', 'batch', 'number', 'code', 'materiallot', 'lotnumber']);
      const nameKey = findKey(['material', 'name', 'item', 'description', 'materialname']);
      const qtyKey = findKey(['quantity', 'qty', 'count', 'amount', 'initialquantity']);
      const unitKey = findKey(['unit', 'uom', 'unitofmeasure']);
      const rateKey = findKey(['rate', 'unitrate', 'cost', 'price', 'unitprice']);

      const defaultLotKey = lotKey || keys[0];
      const defaultNameKey = nameKey || keys[1];
      const defaultQtyKey = qtyKey || keys[2];
      const defaultUnitKey = unitKey || keys[3];
      const defaultRateKey = rateKey || keys[4];

      const parseUnit = (val) => {
        if (!val) return { unitValue: 2, unitLabel: 'Meters' };
        const s = String(val).trim().toLowerCase();
        if (s.includes('piece') || s.includes('pcs') || s.includes('pc')) return { unitValue: 1, unitLabel: 'Pieces' };
        if (s.includes('meter') || s.includes('m')) return { unitValue: 2, unitLabel: 'Meters' };
        if (s.includes('kilo') || s.includes('kg')) return { unitValue: 3, unitLabel: 'Kilograms' };
        if (s.includes('yard') || s.includes('yd')) return { unitValue: 5, unitLabel: 'Yards' };
        return { unitValue: 2, unitLabel: 'Meters' };
      };

      const parsed = jsonRows.map((row, idx) => {
        const lotNumber = String(row[defaultLotKey] ?? '').trim();
        const materialName = String(row[defaultNameKey] ?? '').trim() || 'General Material';
        const rawQty = row[defaultQtyKey] ?? 0;
        const initialQuantity = parseFloat(rawQty) || 0;
        const rawRate = row[defaultRateKey] ?? 0;
        const unitRate = parseFloat(rawRate) || 0;
        const { unitValue, unitLabel } = parseUnit(row[defaultUnitKey]);

        return {
          rowNum: idx + 1,
          lotNumber,
          materialName,
          initialQuantity,
          unit: unitValue,
          unitLabel,
          unitRate,
          isValid: Boolean(lotNumber && initialQuantity > 0)
        };
      }).filter(r => r.lotNumber || r.initialQuantity > 0);

      importPreviewData = parsed;
      if (parsed.length === 0) {
        importErrorMessage = 'Could not extract valid material records. Ensure columns: Lot Number, Material Name, Quantity.';
      }
    } catch (err) {
      importErrorMessage = 'Failed to parse file: ' + (err?.message || err);
    } finally {
      isParsing = false;
    }
  }

  async function handleImportSubmit() {
    const validItems = importPreviewData.filter(r => r.isValid);
    if (validItems.length === 0) {
      importErrorMessage = 'No valid material rows found to import.';
      return;
    }

    isImporting = true;
    importErrorMessage = '';
    importSuccessResult = null;

    try {
      const payload = {
        items: validItems.map(item => ({
          lotNumber: item.lotNumber,
          materialName: item.materialName,
          initialQuantity: item.initialQuantity,
          unit: item.unit,
          unitRate: item.unitRate
        }))
      };

      const result = await importMaterialLots(payload);
      importSuccessResult = result;

      await fetchMaterials();
    } catch (err) {
      importErrorMessage = err?.message || 'Import failed.';
    } finally {
      isImporting = false;
    }
  }

  import { onDestroy } from 'svelte';
  import { headerStore } from '$lib/stores/headerStore.js';

  $effect(() => {
    headerStore.setHeader({
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
          onclick: () => { openImportModal(); }
        },
        {
          label: 'Refresh',
          icon: 'refresh',
          variant: 'outline',
          onclick: () => { fetchMaterials(); }
        }
      ]
    });
  });

  onDestroy(() => {
    headerStore.reset();
  });
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">

  <!-- Unified 4-Metric KPI Strip (All 4 cards in a single line on mobile & desktop) -->
  <section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden">
    <div class="grid grid-cols-4 divide-x divide-outline-variant/40 p-1.5 sm:p-3 text-center">
      <!-- 1. Total Lots -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-primary hidden min-[380px]:inline">inventory</span>
          <span>Lots</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-on-surface font-mono mt-0.5 sm:mt-1">
          {totalLotsCount}
        </div>
      </div>

      <!-- 2. Available Stock -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-emerald-600 hidden min-[380px]:inline">workspaces</span>
          <span>Stock</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-emerald-600 dark:text-emerald-400 font-mono mt-0.5 sm:mt-1 truncate max-w-full">
          {totalAvailableQty.toLocaleString()}
        </div>
      </div>

      <!-- 3. Valuation -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-tertiary hidden min-[380px]:inline">payments</span>
          <span>Valuation</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-on-surface font-mono mt-0.5 sm:mt-1 truncate max-w-full">
          ₹{totalValuation.toLocaleString('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
        </div>
      </div>

      <!-- 4. Depleted Lots -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-amber-600 hidden min-[380px]:inline">warning</span>
          <span>Depleted</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-amber-600 dark:text-amber-400 font-mono mt-0.5 sm:mt-1">
          {depletedCount}
        </div>
      </div>
    </div>
  </section>

  <!-- Search & Filtering Bar -->
  <section class="bg-surface p-3.5 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm flex flex-col md:flex-row justify-between items-stretch md:items-center gap-3 md:gap-4">
    <!-- Search Box -->
    <div class="relative flex-1 max-w-md">
      <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[18px]">search</span>
      <input
        type="text"
        bind:value={searchQuery}
        placeholder="Search material lot number or item name..."
        class="w-full pl-9 pr-4 py-2 bg-surface-container-low border border-outline-variant/70 rounded-xl text-xs sm:text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20 transition-all"
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
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span class="text-xs sm:text-sm font-medium">Fetching materials inventory...</span>
    </div>
  {:else if filteredMaterials.length > 0}
    <!-- Mobile View: Card Stack Column (below md) -->
    <div class="block md:hidden space-y-2">
      {#each filteredMaterials as mat}
        {@const remaining = mat.remainingQuantity ?? mat.initialQuantity ?? 0}
        {@const initial = mat.initialQuantity ?? 0}
        {@const usagePercent = initial > 0 ? (remaining / initial) * 100 : 0}
        {@const stockValuation = remaining * (mat.unitRate || 0)}
        <div
          role="button"
          tabindex="0"
          onclick={() => viewMaterialHistory(mat.id)}
          onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && viewMaterialHistory(mat.id)}
          class="bg-surface p-3 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-xs hover:shadow-sm transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
          title="View material {mat.lotNumber} details and consumption history"
        >
          <!-- Card Line 1: Lot #, Material Name, Stock Quantity & Edit Button -->
          <div class="flex items-center justify-between gap-2 text-xs">
            <div class="flex items-center gap-1.5 min-w-0">
              <div class="font-bold text-sm text-primary flex items-center gap-1 shrink-0 font-mono">
                <span class="material-symbols-outlined text-[15px]">tag</span>
                <span>{mat.lotNumber}</span>
              </div>
              {#if mat.materialName}
                <span class="text-on-surface-variant text-[11px] truncate">• {mat.materialName}</span>
              {/if}
            </div>

            <div class="flex items-center gap-1.5 shrink-0 font-mono">
              <span class="font-bold text-xs {remaining <= 0 ? 'text-red-500' : usagePercent < 25 ? 'text-amber-600' : 'text-emerald-600 dark:text-emerald-400'}">
                {Number(remaining).toLocaleString()} {mat.unit}
              </span>
              <button
                type="button"
                onclick={(e) => { e.stopPropagation(); openEditModal(mat); }}
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
            {#if mat.unitRate}
              <span class="px-2 py-0.5 rounded bg-surface-container-low text-on-surface-variant border border-outline-variant/60">
                Rate: ₹{Number(mat.unitRate).toLocaleString('en-IN')}/{mat.unit}
              </span>
            {/if}

            <!-- Initial / Status Pill -->
            {#if remaining <= 0}
              <span class="px-1.5 py-0.5 rounded font-bold bg-red-100 dark:bg-red-950/60 text-red-700 dark:text-red-300 border border-red-300">
                Depleted
              </span>
            {:else}
              <span class="px-1.5 py-0.5 rounded bg-surface-container-low text-on-surface-variant border border-outline-variant/60">
                Initial: {Number(initial).toLocaleString()} {mat.unit}
              </span>
            {/if}
          </div>
        </div>
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
                <div class="flex items-center justify-end gap-1.5">
                  <button
                    onclick={(e) => { e.stopPropagation(); openEditModal(mat); }}
                    class="p-1.5 text-on-surface-variant hover:text-primary hover:bg-primary/10 rounded-lg transition-colors cursor-pointer"
                    title="Edit Material Lot"
                  >
                    <span class="material-symbols-outlined text-[18px]">edit</span>
                  </button>
                  <button
                    onclick={(e) => { e.stopPropagation(); viewMaterialHistory(mat.id); }}
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
      options={unitOptions}
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

<!-- Modal: Edit Raw Material Lot -->
<Modal bind:isOpen={isEditMaterialModalOpen} title="Edit Raw Material Lot">
  <form onsubmit={handleEditMaterialSubmit} class="space-y-4">
    <Input
      label="Material Lot Number"
      bind:value={editMaterialForm.lotNumber}
      placeholder="e.g. FAB-2026-001"
      icon="tag"
      required={true}
    />

    <Input
      label="Product / Material Name"
      bind:value={editMaterialForm.materialName}
      placeholder="e.g. Denim Grey Cloth 300GSM"
      icon="category"
      required={true}
    />

    <Input
      label="Initial Quantity"
      type="number"
      step={Number(editMaterialForm.unit) === 1 ? '1' : 'any'}
      bind:value={editMaterialForm.initialQuantity}
      placeholder={Number(editMaterialForm.unit) === 1 ? 'e.g. 500' : 'e.g. 500.5'}
      icon="numbers"
      required={true}
    />

    <Select
      label="Unit of Measurement"
      bind:value={editMaterialForm.unit}
      options={unitOptions}
      icon="square_foot"
      required={true}
    />

    <Input
      label="Rate per Unit (₹)"
      type="number"
      step="any"
      bind:value={editMaterialForm.unitRate}
      placeholder="e.g. 150.00"
      icon="payments"
      required={false}
    />

    <div class="flex justify-between items-center pt-3 border-t border-outline-variant">
      <Button
        variant="danger"
        icon="delete"
        disabled={isDeletingMaterial}
        onclick={handleDeleteMaterial}
      >
        {isDeletingMaterial ? 'Deleting...' : 'Delete Material'}
      </Button>

      <div class="flex gap-3">
        <Button variant="outline" onclick={() => isEditMaterialModalOpen = false}>
          Cancel
        </Button>
        <Button variant="primary" type="submit" icon="save">
          Save Changes
        </Button>
      </div>
    </div>
  </form>
</Modal>

<!-- Modal: Import Raw Materials -->
{#if isImportModalOpen}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="bg-surface dark:bg-surface-dim border border-outline-variant rounded-3xl shadow-2xl w-full max-w-3xl overflow-hidden flex flex-col max-h-[90vh]">

      <!-- Modal Header -->
      <div class="px-6 py-5 border-b border-outline-variant flex items-center justify-between bg-surface-container-low/50">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-2xl bg-tertiary/10 text-tertiary flex items-center justify-center">
            <span class="material-symbols-outlined text-[24px]">upload_file</span>
          </div>
          <div>
            <h3 class="font-bold text-lg text-on-surface">Import Raw Materials</h3>
            <p class="text-xs text-on-surface-variant">Upload an Excel (.xlsx, .xls) or CSV (.csv) file to bulk register material lots</p>
          </div>
        </div>
        <button
          onclick={closeImportModal}
          class="p-2 rounded-full hover:bg-surface-container-high text-on-surface-variant transition-colors cursor-pointer"
        >
          <span class="material-symbols-outlined text-[20px]">close</span>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 overflow-y-auto space-y-5 flex-1">

        {#if importErrorMessage}
          <div class="p-4 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 rounded-xl text-red-700 dark:text-red-300 text-sm flex items-center gap-2">
            <span class="material-symbols-outlined text-red-500">error</span>
            <span>{importErrorMessage}</span>
          </div>
        {/if}

        {#if importSuccessResult}
          <div class="p-5 bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 rounded-xl text-emerald-900 dark:text-emerald-200 space-y-3">
            <div class="flex items-center gap-2 font-bold text-base text-emerald-800 dark:text-emerald-300">
              <span class="material-symbols-outlined text-emerald-600 dark:text-emerald-400">check_circle</span>
              Successfully Imported {importSuccessResult.totalImported} Raw Material Lots!
            </div>

            <div class="mt-3 max-h-40 overflow-y-auto border border-emerald-200/60 dark:border-emerald-800/60 rounded-lg p-2 bg-white/50 dark:bg-black/20 text-xs font-mono space-y-1">
              {#each importSuccessResult.importedMaterials as mat}
                <div class="flex justify-between items-center px-2 py-1 hover:bg-emerald-100/50 dark:hover:bg-emerald-900/30 rounded">
                  <span class="font-bold">{mat.finalLotNumber} {#if mat.originalLotNumber !== mat.finalLotNumber}<span class="text-amber-600 text-[11px] font-sans">(auto-incremented from {mat.originalLotNumber})</span>{/if}</span>
                  <span class="text-on-surface-variant font-sans">{mat.materialName}</span>
                  <span class="font-bold text-tertiary">{mat.quantity} Pcs</span>
                </div>
              {/each}
            </div>
          </div>
        {/if}

        <!-- File Upload Zone -->
        <div class="border-2 border-dashed border-outline-variant hover:border-tertiary rounded-2xl p-6 text-center bg-surface-container-lowest transition-colors relative group">
          <input
            type="file"
            accept=".xlsx, .xls, .csv"
            onchange={handleFileSelect}
            class="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
          />
          <div class="flex flex-col items-center gap-2">
            <div class="w-12 h-12 rounded-full bg-tertiary/10 text-tertiary flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="material-symbols-outlined text-[28px]">upload_file</span>
            </div>
            <div>
              <span class="font-bold text-sm text-tertiary underline">Click to select file</span>
              <span class="text-xs text-on-surface-variant"> or drag and drop</span>
            </div>
            <div class="flex gap-2 mt-1">
              <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-surface-container-high text-on-surface-variant">.XLSX</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-surface-container-high text-on-surface-variant">.XLS</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-surface-container-high text-on-surface-variant">.CSV</span>
            </div>
            {#if importFile}
              <div class="mt-2 text-xs font-mono font-bold text-emerald-600 flex items-center gap-1">
                <span class="material-symbols-outlined text-[16px]">description</span>
                Selected: {importFile.name}
              </div>
            {/if}
          </div>
        </div>

        <!-- Format Rules Guide -->
        <div class="p-4 bg-surface-container-low rounded-xl text-xs space-y-2 border border-outline-variant/60">
          <div class="font-bold text-on-surface flex items-center gap-1.5">
            <span class="material-symbols-outlined text-[16px] text-tertiary">info</span>
            Expected File Structure
          </div>
          <p class="text-on-surface-variant">
            File headers: <strong class="text-on-surface">Lot Number</strong>, <strong class="text-on-surface">Material Name</strong>, <strong class="text-on-surface">Quantity</strong>, optional <strong class="text-on-surface">Unit</strong>, and optional <strong class="text-on-surface">Unit Rate</strong>.
          </p>
          <div class="grid grid-cols-5 gap-2 font-mono text-[11px] text-center pt-1">
            <div class="p-2 bg-surface rounded border border-outline-variant">Lot Number</div>
            <div class="p-2 bg-surface rounded border border-outline-variant">Material Name</div>
            <div class="p-2 bg-surface rounded border border-outline-variant">Quantity</div>
            <div class="p-2 bg-surface rounded border border-outline-variant text-tertiary">Unit (Opt)</div>
            <div class="p-2 bg-surface rounded border border-outline-variant text-tertiary">Rate (Opt)</div>
          </div>
        </div>

        <!-- Parsed Data Preview Table -->
        {#if isParsing}
          <div class="p-8 text-center text-on-surface-variant flex flex-col items-center gap-2 font-mono text-sm">
            <span class="material-symbols-outlined animate-spin text-tertiary text-[28px]">sync</span>
            Parsing spreadsheet data...
          </div>
        {:else if importPreviewData.length > 0}
          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <h4 class="font-bold text-sm text-on-surface flex items-center gap-2">
                Preview Data
                <span class="px-2 py-0.5 bg-tertiary/10 text-tertiary font-mono text-xs rounded-full">
                  {importPreviewData.filter(r => r.isValid).length} ready to import
                </span>
              </h4>
            </div>

            <div class="border border-outline-variant rounded-xl overflow-hidden max-h-60 overflow-y-auto">
              <table class="w-full text-left text-xs">
                <thead class="bg-surface-container-low font-mono font-bold uppercase text-[11px] border-b border-outline-variant sticky top-0">
                  <tr>
                    <th class="p-3 w-12 text-center">#</th>
                    <th class="p-3">Lot Number</th>
                    <th class="p-3">Material Name</th>
                    <th class="p-3 text-right">Quantity</th>
                    <th class="p-3 text-center">Unit</th>
                    <th class="p-3 text-right">Unit Rate</th>
                    <th class="p-3 text-center">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-outline-variant/40">
                  {#each importPreviewData as row}
                    <tr class="hover:bg-surface-container-low/50">
                      <td class="p-3 text-center text-on-surface-variant font-mono">{row.rowNum}</td>
                      <td class="p-3 font-mono font-bold text-tertiary">{row.lotNumber || '—'}</td>
                      <td class="p-3 font-bold text-on-surface">{row.materialName}</td>
                      <td class="p-3 text-right font-mono font-bold">{row.initialQuantity.toLocaleString()}</td>
                      <td class="p-3 text-center font-mono">{row.unitLabel}</td>
                      <td class="p-3 text-right font-mono">₹{row.unitRate.toFixed(2)}</td>
                      <td class="p-3 text-center">
                        {#if row.isValid}
                          <span class="text-emerald-600 font-bold flex items-center justify-center gap-0.5">
                            <span class="material-symbols-outlined text-[14px]">check</span> Valid
                          </span>
                        {:else}
                          <span class="text-red-500 font-bold flex items-center justify-center gap-0.5">
                            <span class="material-symbols-outlined text-[14px]">warning</span> Missing Qty/Lot
                          </span>
                        {/if}
                      </td>
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
          </div>
        {/if}

      </div>

      <!-- Modal Footer -->
      <div class="px-6 py-4 border-t border-outline-variant bg-surface-container-low/50 flex items-center justify-end gap-3">
        <button
          onclick={closeImportModal}
          class="px-4 py-2 rounded-xl border border-outline-variant hover:bg-surface-container text-on-surface-variant text-xs font-bold transition-colors cursor-pointer"
        >
          Cancel
        </button>
        <button
          onclick={handleImportSubmit}
          disabled={isImporting || isParsing || importPreviewData.filter(r => r.isValid).length === 0}
          class="px-5 py-2 rounded-xl bg-tertiary text-on-tertiary font-bold text-xs hover:bg-tertiary/90 disabled:opacity-50 flex items-center gap-2 transition-colors cursor-pointer"
        >
          {#if isImporting}
            <span class="material-symbols-outlined animate-spin text-[16px]">sync</span>
            Importing Materials...
          {:else}
            <span class="material-symbols-outlined text-[16px]">upload_file</span>
            Import {importPreviewData.filter(r => r.isValid).length} Material Lots
          {/if}
        </button>
      </div>

    </div>
  </div>
{/if}
