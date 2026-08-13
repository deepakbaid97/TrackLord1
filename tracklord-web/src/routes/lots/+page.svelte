<script>
  import { onMount } from 'svelte';
  import {
    getProductionLots,
    getManufacturingSteps,
    getClients,
    getLotJourney,
    importProductionLots
  } from '$lib/api.js';
  import * as XLSX from 'xlsx';
  import Select from '$lib/components/ui/Select.svelte';

  let loading = $state(true);
  let errorMessage = $state('');

  /** @type {Array<any>} */
  let rawLots = $state([]);
  /** @type {Array<any>} */
  let steps = $state([]);
  /** @type {Array<any>} */
  let clients = $state([]);
  /** @type {Array<any>} */
  let enrichedLots = $state([]);

  // Filter States
  let searchQuery = $state('');
  let selectedStepId = $state('ALL');
  let selectedClientId = $state('ALL');
  let selectedDateRange = $state('ALL'); // ALL, TODAY, 7DAYS, 30DAYS, CUSTOM
  let customStartDate = $state('');
  let customEndDate = $state('');
  let isFilterExpanded = $state(false); // Collapsed by default on mobile

  let activeFilterCount = $derived(
    (searchQuery.trim() ? 1 : 0) +
    (selectedStepId !== 'ALL' ? 1 : 0) +
    (selectedClientId !== 'ALL' ? 1 : 0) +
    (selectedDateRange !== 'ALL' ? 1 : 0)
  );

  function resetFilters() {
    searchQuery = '';
    selectedStepId = 'ALL';
    selectedClientId = 'ALL';
    selectedDateRange = 'ALL';
    customStartDate = '';
    customEndDate = '';
  }

  // Import Modal state
  let isImportModalOpen = $state(false);
  let importFile = $state(/** @type {File|null} */ (null));
  let importPreviewData = $state(/** @type {Array<any>} */ ([]));
  let hasStepColumn = $state(false);
  let selectedGlobalStepId = $state('');
  let selectedGlobalStepName = $state('');
  let isParsing = $state(false);
  let isImporting = $state(false);
  let importErrorMessage = $state('');
  let importSuccessResult = $state(/** @type {any} */ (null));

  // Step Icon mapping
  /** @type {Record<string, string>} */
  const stepIconMap = {
    'CUT': 'content_cut',
    'STITCH': 'webhook',
    'WASH': 'local_laundry_service',
    'FIN': 'iron',
    'PACK': 'inventory_2',
    'IMPORT': 'file_upload'
  };

  import { page } from '$app/stores';

  onMount(async () => {
    // Read URL query parameters for pre-selected step or client filters
    const urlParams = $page.url.searchParams;
    const stepParam = urlParams.get('step');
    const clientParam = urlParams.get('client');

    if (stepParam) selectedStepId = stepParam;
    if (clientParam) selectedClientId = clientParam;

    await refreshData();
  });

  async function refreshData() {
    loading = true;
    errorMessage = '';
    try {
      // 1. Fetch base steps & clients
      const [stepsData, clientsData, lotsData] = await Promise.all([
        getManufacturingSteps(),
        getClients(),
        getProductionLots()
      ]);

      steps = stepsData || [];
      clients = clientsData || [];
      rawLots = lotsData || [];

      // 2. Fetch journeys for each lot to gather active steps, quantities, and client associations
      const enriched = await Promise.all(
        rawLots.map(async (lot) => {
          let journey = null;
          try {
            journey = await getLotJourney(lot.id);
          } catch {
            // Ignore single lot journey failure
          }

          /** @type {Array<any>} */
          let activeSteps = [];
          let totalWipQty = 0;
          let activeLotClientsSet = new Set();
          /** @type {Array<string>} */
          let clientNamesList = [];

          if (journey) {
            // Extract active step balances
            if (journey.activeBalances) {
              activeSteps = journey.activeBalances.filter((/** @type {any} */ b) => b.quantity > 0);

              // Exclude terminating step quantities from total active WIP calculation
              const activeNonTerminatingSteps = activeSteps.filter((/** @type {any} */ b) => {
                const stepObj = steps.find((s) => s.id === b.stepId || s.code === b.stepCode);
                return !stepObj?.isTerminating;
              });

              totalWipQty = activeNonTerminatingSteps.reduce((/** @type {number} */ acc, /** @type {any} */ b) => acc + b.quantity, 0);
            }

            // Extract clients specifically associated with steps that currently hold active WIP quantity
            const activeStepIds = new Set(activeSteps.map((/** @type {any} */ s) => s.stepId));

            if (journey.movementHistory) {
              journey.movementHistory.forEach((/** @type {any} */ m) => {
                if (!m.isReversed && m.toStepName && activeStepIds.size > 0) {
                  // Find if this movement moved items into one of the currently active WIP steps
                  const isMoveToActiveStep = activeSteps.some((/** @type {any} */ s) => s.stepName === m.toStepName);
                  if (isMoveToActiveStep) {
                    if (m.clientId) activeLotClientsSet.add(m.clientId);
                    if (m.clientName) clientNamesList.push(m.clientName);
                  }
                }
              });
            }
          }

          const activeClientIdsArray = Array.from(activeLotClientsSet);
          const uniqueClientNames = Array.from(new Set(clientNamesList));

          return {
            id: lot.id,
            lotNumber: lot.lotNumber,
            itemName: 'Garment Item Lot', // Item default / display name
            initialQuantity: lot.initialQuantity,
            status: lot.status,
            createdAtUtc: lot.createdAtUtc,
            createdDate: new Date(lot.createdAtUtc),
            activeSteps,
            totalWipQty: journey ? totalWipQty : lot.initialQuantity,
            clientIds: activeClientIdsArray,
            clientNamesDisplay: uniqueClientNames.length > 0 ? uniqueClientNames.join(', ') : 'Direct Factory'
          };
        })
      );

      enrichedLots = enriched;
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load production lots data.';
    } finally {
      loading = false;
    }
  }

  // Filter Logic
  let filteredLots = $derived.by(() => {
    return enrichedLots.filter((lot) => {
      // Search Query Filter
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim();
        const matchesNumber = lot.lotNumber.toLowerCase().includes(query);
        const matchesClient = lot.clientNamesDisplay.toLowerCase().includes(query);
        const matchesItem = lot.itemName.toLowerCase().includes(query);
        if (!matchesNumber && !matchesClient && !matchesItem) return false;
      }

      // Step Filter
      if (selectedStepId !== 'ALL') {
        const hasStep = lot.activeSteps.some((/** @type {any} */ s) => s.stepId === selectedStepId);
        if (!hasStep) return false;
      }

      // Client Filter
      if (selectedClientId !== 'ALL') {
        if (selectedClientId === 'NONE') {
          if (lot.clientIds.length > 0) return false;
        } else {
          if (!lot.clientIds.includes(selectedClientId)) return false;
        }
      }

      // Date Range Filter
      if (selectedDateRange !== 'ALL') {
        const lotDate = new Date(lot.createdAtUtc);
        const now = new Date();

        if (selectedDateRange === 'TODAY') {
          const isToday =
            lotDate.getDate() === now.getDate() &&
            lotDate.getMonth() === now.getMonth() &&
            lotDate.getFullYear() === now.getFullYear();
          if (!isToday) return false;
        } else if (selectedDateRange === '7DAYS') {
          const sevenDaysAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
          if (lotDate < sevenDaysAgo) return false;
        } else if (selectedDateRange === '30DAYS') {
          const thirtyDaysAgo = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
          if (lotDate < thirtyDaysAgo) return false;
        } else if (selectedDateRange === 'CUSTOM') {
          if (customStartDate) {
            const start = new Date(customStartDate);
            start.setHours(0, 0, 0, 0);
            if (lotDate < start) return false;
          }
          if (customEndDate) {
            const end = new Date(customEndDate);
            end.setHours(23, 59, 59, 999);
            if (lotDate > end) return false;
          }
        }
      }

      return true;
    });
  });

  // Summary Metrics
  let totalWipSum = $derived(
    filteredLots.reduce((acc, l) => {
      if (selectedStepId !== 'ALL') {
        const stepBalance = l.activeSteps.find((/** @type {any} */ s) => s.stepId === selectedStepId);
        return acc + (stepBalance ? stepBalance.quantity : 0);
      }
      return acc + l.totalWipQty;
    }, 0)
  );
  let totalInitialSum = $derived(filteredLots.reduce((acc, l) => acc + l.initialQuantity, 0));

  function openImportModal() {
    isImportModalOpen = true;
    importFile = null;
    importPreviewData = [];
    hasStepColumn = false;
    selectedGlobalStepId = '';
    selectedGlobalStepName = '';
    importErrorMessage = '';
    importSuccessResult = null;
  }

  function closeImportModal() {
    isImportModalOpen = false;
    importFile = null;
    importPreviewData = [];
    hasStepColumn = false;
    selectedGlobalStepId = '';
    selectedGlobalStepName = '';
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
    hasStepColumn = false;
    selectedGlobalStepId = '';
    selectedGlobalStepName = '';

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

      // Column Header Matching
      const sampleRow = jsonRows[0];
      const keys = Object.keys(sampleRow);

      const findKey = (candidates) => {
        return keys.find(k => {
          const lower = k.toLowerCase().replace(/[^a-z0-9]/g, '');
          return candidates.some(c => lower.includes(c));
        });
      };

      const clientKey = findKey(['client', 'customer', 'buyer', 'name']);
      const qtyKey = findKey(['quantity', 'qty', 'count', 'pcs', 'amount']);
      const lotKey = findKey(['lot', 'batch', 'number', 'code']);
      const stepKey = findKey(['step', 'steps', 'stage', 'process', 'manufacturingstep']);

      const defaultClientKey = clientKey || keys[0];
      const defaultQtyKey = qtyKey || keys[1];
      const defaultLotKey = lotKey || keys[2];

      hasStepColumn = Boolean(stepKey);

      if (!hasStepColumn) {
        // Default global step to 'IMPORT' or first available step
        const defaultStep = steps.find(s => s.code.toUpperCase() === 'IMPORT') || steps[0];
        selectedGlobalStepId = defaultStep ? defaultStep.id : '';
        selectedGlobalStepName = defaultStep ? defaultStep.name : 'Import';
      }

      const parsed = jsonRows.map((row, idx) => {
        const clientName = String(row[defaultClientKey] ?? '').trim();
        const rawQty = row[defaultQtyKey] ?? 0;
        const lotNumber = String(row[defaultLotKey] ?? '').trim();
        const quantity = parseFloat(rawQty) || 0;

        let rowStepId = '';
        let rowStepName = '';

        if (hasStepColumn && stepKey) {
          const rawStep = String(row[stepKey] ?? '').trim();
          if (rawStep) {
            const matchedStep = steps.find(s =>
              s.name.toLowerCase() === rawStep.toLowerCase() ||
              s.code.toLowerCase() === rawStep.toLowerCase()
            );
            if (matchedStep) {
              rowStepId = matchedStep.id;
              rowStepName = matchedStep.name;
            } else {
              rowStepName = rawStep;
            }
          }
        }

        return {
          rowNum: idx + 1,
          clientName: clientName || 'General Client',
          quantity,
          lotNumber,
          stepId: rowStepId,
          stepName: rowStepName,
          isValid: Boolean(lotNumber && quantity > 0)
        };
      }).filter(r => r.lotNumber || r.quantity > 0);

      importPreviewData = parsed;
      if (parsed.length === 0) {
        importErrorMessage = 'Could not extract valid records. Ensure columns: Client Name, Quantity, Lot Number.';
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
      importErrorMessage = 'No valid rows found to import.';
      return;
    }

    isImporting = true;
    importErrorMessage = '';
    importSuccessResult = null;

    try {
      const payload = {
        items: validItems.map(item => ({
          clientName: item.clientName,
          quantity: item.quantity,
          lotNumber: item.lotNumber,
          stepId: item.stepId || (!hasStepColumn ? selectedGlobalStepId : null),
          stepName: item.stepName || (!hasStepColumn ? selectedGlobalStepName : null)
        })),
        createdBy: 'File Import Tool'
      };

      const result = await importProductionLots(payload);
      importSuccessResult = result;

      // Refresh lots list
      await refreshData();
    } catch (err) {
      importErrorMessage = err?.message || 'Import failed.';
    } finally {
      isImporting = false;
    }
  }
</script>

<!-- TopAppBar -->
<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">reorder</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
    <button
      onclick={openImportModal}
      class="px-3.5 py-1.5 rounded-lg bg-primary text-on-primary font-bold text-xs hover:bg-primary/90 flex items-center gap-1.5 shadow-sm transition-all cursor-pointer"
    >
      <span class="material-symbols-outlined text-[18px]">file_upload</span>
      Import Lots
    </button>
    <button
      onclick={() => refreshData()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh
    </button>
  </div>
</header>

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-10">

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

  <!-- Header Info & KPI Summary -->
  <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
      <div class="p-3 bg-primary/10 rounded-lg text-primary">
        <span class="material-symbols-outlined text-[24px]">inventory_2</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Total Lots</div>
        <div class="text-2xl font-bold text-on-surface">{filteredLots.length}</div>
      </div>
    </div>

    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
      <div class="p-3 bg-blue-500/10 rounded-lg text-blue-600">
        <span class="material-symbols-outlined text-[24px]">stacks</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Total WIP Qty</div>
        <div class="text-2xl font-bold text-on-surface">{totalWipSum.toLocaleString()} Pcs</div>
      </div>
    </div>

    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
      <div class="p-3 bg-emerald-500/10 rounded-lg text-emerald-600">
        <span class="material-symbols-outlined text-[24px]">output</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Initial Created Qty</div>
        <div class="text-2xl font-bold text-on-surface">{totalInitialSum.toLocaleString()} Pcs</div>
      </div>
    </div>

    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
      <div class="p-3 bg-amber-500/10 rounded-lg text-amber-600">
        <span class="material-symbols-outlined text-[24px]">groups</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Clients Filtered</div>
        <div class="text-2xl font-bold text-on-surface">
          {new Set(filteredLots.flatMap(l => l.clientIds)).size}
        </div>
      </div>
    </div>
  </div>

  <!-- Filter Toolbar -->
  <section class="bg-surface p-4 sm:p-5 rounded-xl border border-outline-variant shadow-sm space-y-4">
    <!-- Filter Header (Entire header clickable on mobile) -->
    <div
      role="button"
      tabindex="0"
      onclick={() => isFilterExpanded = !isFilterExpanded}
      onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (isFilterExpanded = !isFilterExpanded)}
      class="flex items-center justify-between gap-3 cursor-pointer md:cursor-default select-none {isFilterExpanded ? 'border-b border-outline-variant pb-3 md:pb-4' : 'md:border-b md:border-outline-variant md:pb-4'}"
    >
      <div class="flex items-center gap-2 font-bold text-primary text-base">
        <span class="material-symbols-outlined text-[22px]">filter_alt</span>
        <span>Filter Production Lots</span>
        {#if activeFilterCount > 0}
          <span class="px-2 py-0.5 rounded-full text-xs bg-primary text-on-primary font-mono font-bold">
            {activeFilterCount}
          </span>
        {/if}
      </div>

      <div class="flex items-center gap-2">
        {#if activeFilterCount > 0}
          <button
            type="button"
            onclick={(e) => { e.stopPropagation(); resetFilters(); }}
            class="flex items-center gap-1 px-2.5 py-1 rounded-lg border border-red-200 dark:border-red-800 bg-red-50 hover:bg-red-100 dark:bg-red-950/40 dark:hover:bg-red-900/50 text-red-600 dark:text-red-300 text-xs font-semibold transition-colors cursor-pointer"
            title="Reset all active filters"
          >
            <span class="material-symbols-outlined text-[16px]">restart_alt</span>
            <span>Reset</span>
          </button>
        {/if}
        <!-- Chevron indicator on mobile -->
        <span class="md:hidden material-symbols-outlined text-[20px] text-on-surface-variant transition-transform duration-200 {isFilterExpanded ? 'rotate-180' : ''}">
          expand_more
        </span>
      </div>
    </div>

    <!-- Filter Body: Collapsed by default on mobile, always visible on md+ -->
    <div class="{isFilterExpanded ? 'block' : 'hidden md:block'} space-y-4 pt-1 md:pt-0">
      <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <!-- Search Input -->
        <div class="relative w-full max-w-md">
          <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[20px]">search</span>
          <input
            type="text"
            bind:value={searchQuery}
            placeholder="Search by lot number, client, or item..."
            class="w-full pl-10 pr-4 py-2 bg-surface-container-low border border-outline-variant rounded-lg text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
          />
        </div>

        {#if activeFilterCount > 0}
          <button
            type="button"
            onclick={resetFilters}
            class="hidden sm:flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg border border-red-200 dark:border-red-800 bg-red-50 hover:bg-red-100 dark:bg-red-950/40 text-red-600 dark:text-red-300 text-xs font-semibold transition-colors cursor-pointer shrink-0"
          >
            <span class="material-symbols-outlined text-[16px]">restart_alt</span>
            <span>Clear Filters ({activeFilterCount})</span>
          </button>
        {/if}
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
        <!-- 1. Step Filter -->
        <Select
          label="Manufacturing Step"
          bind:value={selectedStepId}
          options={[
            { label: 'All Steps (Any active WIP)', value: 'ALL' },
            ...steps.map(s => ({ label: `${s.name} (${s.code})`, value: s.id }))
          ]}
        />

        <!-- 2. Client Filter -->
        <Select
          label="Client / Customer"
          bind:value={selectedClientId}
          options={[
            { label: 'All Clients', value: 'ALL' },
            ...clients.map(c => ({ label: `${c.name} (${c.clientType})`, value: c.id }))
          ]}
        />

        <!-- 3. Date Range Filter -->
        <div class="space-y-1">
          <label for="filter-daterange" class="text-xs font-bold text-on-surface-variant uppercase font-mono">Creation Date</label>
          <select
            id="filter-daterange"
            bind:value={selectedDateRange}
            class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-2 text-sm font-semibold text-on-surface outline-none focus:ring-2 focus:ring-primary/20 cursor-pointer"
          >
            <option value="ALL">All Time</option>
            <option value="TODAY">Today</option>
            <option value="7DAYS">Last 7 Days</option>
            <option value="30DAYS">Last 30 Days</option>
            <option value="CUSTOM">Custom Date Range...</option>
          </select>
        </div>
      </div>

      <!-- Custom Date Range Controls -->
      {#if selectedDateRange === 'CUSTOM'}
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2 border-t border-dashed border-outline-variant/60">
          <div class="space-y-1">
            <label for="custom-start" class="text-xs text-on-surface-variant font-mono">Start Date</label>
            <input
              id="custom-start"
              type="date"
              bind:value={customStartDate}
              class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-1.5 text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
            />
          </div>
          <div class="space-y-1">
            <label for="custom-end" class="text-xs text-on-surface-variant font-mono">End Date</label>
            <input
              id="custom-end"
              type="date"
              bind:value={customEndDate}
              class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-1.5 text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
            />
          </div>
        </div>
      {/if}
    </div>
  </section>

  <!-- Production Lots Directory Table -->
  <section class="bg-surface rounded-xl border border-outline-variant shadow-sm overflow-hidden space-y-4 p-6">
    <div class="flex justify-between items-center border-b border-outline-variant pb-3">
      <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
        <span class="material-symbols-outlined text-primary">format_list_bulleted</span>
        Lots Directory ({filteredLots.length})
      </h2>
      <span class="text-xs text-on-surface-variant font-mono">
        Showing {filteredLots.length} of {enrichedLots.length} total lots
      </span>
    </div>

    {#if loading}
      <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
        <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
        <span>Loading production lots...</span>
      </div>
    {:else}
      <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-4 py-3">Lot Number</th>
              <th class="px-4 py-3">Item / Name</th>
              <th class="px-4 py-3 text-right">Current Quantity</th>
              <th class="px-4 py-3">Active Steps & WIP</th>
              <th class="px-4 py-3">Created Date</th>
              <th class="px-4 py-3 text-right">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#if filteredLots.length === 0}
              <tr>
                <td colspan="6" class="px-4 py-12 text-center text-on-surface-variant">
                  <span class="material-symbols-outlined text-[36px] text-on-surface-variant/40 mb-2 block">filter_alt_off</span>
                  No production lots match your filter criteria. Try adjusting the search query or filters above.
                </td>
              </tr>
            {:else}
              {#each filteredLots as lot}
                <tr
                  onclick={() => window.location.href = `/inventory?lot=${lot.id}`}
                  class="hover:bg-surface-container-low/60 transition-colors cursor-pointer group"
                  title="View lot {lot.lotNumber} details"
                >
                  <td class="px-4 py-3 whitespace-nowrap">
                    <div class="font-bold text-primary flex items-center gap-1.5">
                      <span class="material-symbols-outlined text-[16px]">tag</span>
                      {lot.lotNumber}
                    </div>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap font-medium">{lot.itemName}</td>
                  <td class="px-4 py-3 whitespace-nowrap text-right font-mono font-bold text-primary">
                    {lot.totalWipQty.toLocaleString()} Pcs
                  </td>
                  <td class="px-4 py-3">
                    <div class="flex flex-wrap gap-1.5 items-center">
                      {#each lot.activeSteps as step}
                        {@const stepObj = steps.find(s => s.id === step.stepId || s.code === step.stepCode)}
                        <span class="px-2 py-0.5 rounded text-[11px] font-bold font-mono {stepObj?.isTerminating ? 'bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300 dark:border-amber-700' : 'bg-primary-container text-on-primary-container'} flex items-center gap-1">
                          <span class="material-symbols-outlined text-[12px]">{stepIconMap[step.stepCode] || 'work'}</span>
                          {step.stepName}: {step.quantity} Pcs
                          {#if stepObj?.isTerminating}
                            <span class="text-[9px] uppercase tracking-wider font-extrabold text-amber-700 dark:text-amber-400 ml-0.5">(Terminated)</span>
                          {/if}
                        </span>
                      {/each}
                      {#if lot.activeSteps.length === 0}
                        <span class="text-xs text-on-surface-variant italic">No active step</span>
                      {/if}
                    </div>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap text-xs text-on-surface-variant font-mono">
                    {lot.createdDate.toLocaleDateString()}
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap text-right">
                    <a
                      href="/inventory"
                      class="px-2.5 py-1 text-xs bg-primary/10 text-primary hover:bg-primary/20 rounded font-bold transition-colors inline-flex items-center gap-1"
                    >
                      <span class="material-symbols-outlined text-[14px]">visibility</span>
                      View WIP
                    </a>
                  </td>
                </tr>
              {/each}
            {/if}
          </tbody>
        </table>
      </div>
    {/if}
  </section>

</main>

<!-- Import Modal -->
{#if isImportModalOpen}
  <div class="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-fade-in">
    <div class="bg-surface dark:bg-surface-dim w-full max-w-3xl rounded-2xl border border-outline-variant shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
      
      <!-- Modal Header -->
      <div class="px-6 py-4 border-b border-outline-variant flex items-center justify-between bg-surface-container-low/50">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center font-bold">
            <span class="material-symbols-outlined text-[24px]">file_upload</span>
          </div>
          <div>
            <h3 class="font-bold text-lg text-on-surface">Import Production Lots</h3>
            <p class="text-xs text-on-surface-variant">Upload an Excel (.xlsx, .xls) or CSV (.csv) file to import lots</p>
          </div>
        </div>
        <button
          onclick={closeImportModal}
          class="w-8 h-8 rounded-lg border border-outline-variant text-on-surface-variant hover:bg-surface-container flex items-center justify-center transition-colors cursor-pointer"
        >
          <span class="material-symbols-outlined text-[18px]">close</span>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 overflow-y-auto space-y-6 flex-1">
        
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
              Successfully Imported {importSuccessResult.totalImported} Lots!
            </div>
            <p class="text-xs text-emerald-700 dark:text-emerald-300/80">
              All imported lots have been assigned to the manufacturing step <span class="font-mono font-bold px-1.5 py-0.5 bg-emerald-200/60 dark:bg-emerald-900/60 rounded">IMPORT</span> with their associated clients.
            </p>

            <div class="mt-3 max-h-40 overflow-y-auto border border-emerald-200/60 dark:border-emerald-800/60 rounded-lg p-2 bg-white/50 dark:bg-black/20 text-xs font-mono space-y-1">
              {#each importSuccessResult.importedLots as lot}
                <div class="flex justify-between items-center px-2 py-1 hover:bg-emerald-100/50 dark:hover:bg-emerald-900/30 rounded">
                  <span class="font-bold">{lot.finalLotNumber} {#if lot.originalLotNumber !== lot.finalLotNumber}<span class="text-amber-600 text-[11px] font-sans">(auto-incremented from {lot.originalLotNumber})</span>{/if}</span>
                  <span class="text-on-surface-variant font-sans">{lot.clientName}</span>
                  <span class="font-bold text-primary">{lot.quantity} Pcs</span>
                </div>
              {/each}
            </div>
          </div>
        {/if}

        <!-- File Upload Zone -->
        <div class="border-2 border-dashed border-outline-variant hover:border-primary rounded-2xl p-6 text-center bg-surface-container-lowest transition-colors relative group">
          <input
            type="file"
            accept=".xlsx, .xls, .csv"
            onchange={handleFileSelect}
            class="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
          />
          <div class="flex flex-col items-center gap-2">
            <div class="w-12 h-12 rounded-full bg-primary/10 text-primary flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="material-symbols-outlined text-[28px]">upload_file</span>
            </div>
            <div>
              <span class="font-bold text-sm text-primary underline">Click to select file</span>
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
            <span class="material-symbols-outlined text-[16px] text-primary">info</span>
            Expected File Structure
          </div>
          <p class="text-on-surface-variant">
            File headers: <strong class="text-on-surface">Client Name</strong>, <strong class="text-on-surface">Quantity</strong>, <strong class="text-on-surface">Lot Number</strong>, and optional <strong class="text-on-surface">Steps</strong>.
          </p>
          <div class="grid grid-cols-4 gap-2 font-mono text-[11px] text-center pt-1">
            <div class="p-2 bg-surface rounded border border-outline-variant">Client Name</div>
            <div class="p-2 bg-surface rounded border border-outline-variant">Quantity</div>
            <div class="p-2 bg-surface rounded border border-outline-variant">Lot Number</div>
            <div class="p-2 bg-surface rounded border border-outline-variant text-primary font-bold">Steps (Opt)</div>
          </div>
        </div>

        <!-- Parsed Data Preview Table & Step Prompt -->
        {#if isParsing}
          <div class="p-8 text-center text-on-surface-variant flex flex-col items-center gap-2 font-mono text-sm">
            <span class="material-symbols-outlined animate-spin text-primary text-[28px]">sync</span>
            Parsing spreadsheet data...
          </div>
        {:else if importPreviewData.length > 0}
          <div class="space-y-3">
            <!-- Step Prompt Banner when Steps column is absent -->
            {#if !hasStepColumn}
              <div class="p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
                <div class="flex items-center gap-2 text-amber-900 dark:text-amber-200">
                  <span class="material-symbols-outlined text-amber-600 dark:text-amber-400">info</span>
                  <div>
                    <div class="font-bold">No 'Steps' column detected in file</div>
                    <div class="text-on-surface-variant text-[11px]">Select the target step under which all import lines should be added:</div>
                  </div>
                </div>
                <div class="min-w-[220px]">
                  <select
                    value={selectedGlobalStepId}
                    onchange={(e) => {
                      selectedGlobalStepId = e.target.value;
                      const found = steps.find(s => s.id === e.target.value);
                      selectedGlobalStepName = found ? found.name : 'Import';
                    }}
                    class="w-full px-3 py-1.5 rounded-lg border border-outline-variant bg-surface text-on-surface font-bold text-xs focus:ring-2 focus:ring-primary outline-none"
                  >
                    {#each steps as step}
                      <option value={step.id}>{step.name} ({step.code})</option>
                    {/each}
                    <option value="">Default (IMPORT step)</option>
                  </select>
                </div>
              </div>
            {/if}

            <div class="flex items-center justify-between">
              <h4 class="font-bold text-sm text-on-surface flex items-center gap-2">
                Preview Data
                <span class="px-2 py-0.5 bg-primary/10 text-primary font-mono text-xs rounded-full">
                  {importPreviewData.filter(r => r.isValid).length} ready to import
                </span>
              </h4>
              {#if hasStepColumn}
                <span class="text-xs font-mono font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                  <span class="material-symbols-outlined text-[14px]">check_circle</span>
                  Steps column detected in file
                </span>
              {/if}
            </div>

            <div class="border border-outline-variant rounded-xl overflow-hidden max-h-60 overflow-y-auto">
              <table class="w-full text-left text-xs">
                <thead class="bg-surface-container-low font-mono font-bold uppercase text-[11px] border-b border-outline-variant sticky top-0">
                  <tr>
                    <th class="p-3 w-12 text-center">#</th>
                    <th class="p-3">Lot Number</th>
                    <th class="p-3">Client Name</th>
                    <th class="p-3 text-right">Quantity</th>
                    <th class="p-3 text-center">Initial Step</th>
                    <th class="p-3 text-center">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-outline-variant/40">
                  {#each importPreviewData as row}
                    <tr class="hover:bg-surface-container-low/50">
                      <td class="p-3 text-center text-on-surface-variant font-mono">{row.rowNum}</td>
                      <td class="p-3 font-mono font-bold text-primary">{row.lotNumber || '—'}</td>
                      <td class="p-3 font-bold text-on-surface">{row.clientName}</td>
                      <td class="p-3 text-right font-mono font-bold">{row.quantity.toLocaleString()} Pcs</td>
                      <td class="p-3 text-center">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-primary/10 text-primary border border-primary/20">
                          {row.stepName || (!hasStepColumn ? (selectedGlobalStepName || 'IMPORT') : 'IMPORT')}
                        </span>
                      </td>
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
          class="px-5 py-2 rounded-xl bg-primary text-on-primary font-bold text-xs hover:bg-primary/90 disabled:opacity-50 flex items-center gap-2 transition-colors cursor-pointer"
        >
          {#if isImporting}
            <span class="material-symbols-outlined animate-spin text-[16px]">sync</span>
            Importing Lots...
          {:else}
            <span class="material-symbols-outlined text-[16px]">upload_file</span>
            Import {importPreviewData.filter(r => r.isValid).length} Lots
          {/if}
        </button>
      </div>

    </div>
  </div>
{/if}
