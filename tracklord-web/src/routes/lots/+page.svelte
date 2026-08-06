<script>
  import { onMount } from 'svelte';
  import {
    getProductionLots,
    getManufacturingSteps,
    getClients,
    getLotJourney
  } from '$lib/api.js';

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

  // Step Icon mapping
  /** @type {Record<string, string>} */
  const stepIconMap = {
    'CUT': 'content_cut',
    'STITCH': 'webhook',
    'WASH': 'local_laundry_service',
    'FIN': 'iron',
    'PACK': 'inventory_2'
  };

  onMount(async () => {
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
              totalWipQty = activeSteps.reduce((/** @type {number} */ acc, /** @type {any} */ b) => acc + b.quantity, 0);
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
            totalWipQty: totalWipQty || lot.initialQuantity,
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
</script>

<!-- TopAppBar -->
<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">reorder</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD ERP</div>
      <div class="text-xs text-on-surface-variant">Production Lots Directory</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
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
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-20">

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
  <section class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm space-y-4">
    <div class="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-4 border-b border-outline-variant pb-4">
      <div class="flex items-center gap-2 font-bold text-primary text-base">
        <span class="material-symbols-outlined">filter_alt</span>
        <span>Filter Production Lots</span>
      </div>

      <!-- Search Input -->
      <div class="relative flex-1 max-w-md">
        <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[20px]">search</span>
        <input
          type="text"
          bind:value={searchQuery}
          placeholder="Search by lot number, client, or item..."
          class="w-full pl-10 pr-4 py-2 bg-surface-container-low border border-outline-variant rounded-lg text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
        />
      </div>
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
      <!-- 1. Step Filter -->
      <div class="space-y-1">
        <label for="filter-step" class="text-xs font-bold text-on-surface-variant uppercase font-mono">Manufacturing Step</label>
        <select
          id="filter-step"
          bind:value={selectedStepId}
          class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-2 text-sm font-semibold text-on-surface outline-none focus:ring-2 focus:ring-primary/20 cursor-pointer"
        >
          <option value="ALL">All Steps (Any active WIP)</option>
          {#each steps as step}
            <option value={step.id}>{step.name} ({step.code})</option>
          {/each}
        </select>
      </div>

      <!-- 2. Client Filter -->
      <div class="space-y-1">
        <label for="filter-client" class="text-xs font-bold text-on-surface-variant uppercase font-mono">Client / Customer</label>
        <select
          id="filter-client"
          bind:value={selectedClientId}
          class="w-full bg-surface-container-low border border-outline-variant rounded-lg px-3 py-2 text-sm font-semibold text-on-surface outline-none focus:ring-2 focus:ring-primary/20 cursor-pointer"
        >
          <option value="ALL">All Clients</option>
          {#each clients as client}
            <option value={client.id}>{client.name} ({client.clientType})</option>
          {/each}
        </select>
      </div>

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
                <tr class="hover:bg-surface-container-low/60 transition-colors">
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
                        <span class="px-2 py-0.5 rounded text-[11px] font-bold font-mono bg-primary-container text-on-primary-container flex items-center gap-1">
                          <span class="material-symbols-outlined text-[12px]">{stepIconMap[step.stepCode] || 'work'}</span>
                          {step.stepName}: {step.quantity} Pcs
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
