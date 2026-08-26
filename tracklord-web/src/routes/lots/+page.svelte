<script>
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import {
    getProductionLots,
    getManufacturingSteps,
    getClients,
    getBatchLotJourneys
  } from '$lib/api.js';
  import Select from '$lib/components/ui/Select.svelte';
  import FeatureGate from '$lib/components/ui/FeatureGate.svelte';
  import ClientModeGate from '$lib/components/ui/ClientModeGate.svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import KpiStrip from '$lib/components/ui/KpiStrip.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import LotCard from '$lib/components/features/lots/LotCard.svelte';
  import CreateLotModal from '$lib/components/features/lots/CreateLotModal.svelte';
  import ImportLotsModal from '$lib/components/features/lots/ImportLotsModal.svelte';
  import { STEP_ICON_MAP } from '$lib/services/lotService.js';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  /** @type {Array<any>} */
  let rawLots = $state([]);
  /** @type {Array<any>} */
  let steps = $state([]);
  /** @type {Array<any>} */
  let clients = $state([]);
  /** @type {Array<any>} */
  let enrichedLots = $state([]);

  // Modals state
  let isCreateLotModalOpen = $state(false);
  let isImportModalOpen = $state(false);

  // Filter States
  let searchQuery = $state('');
  let selectedStepId = $state('ALL');
  let selectedClientId = $state('ALL');
  let selectedDateRange = $state('ALL');
  let customStartDate = $state('');
  let customEndDate = $state('');
  let isFilterExpanded = $state(false);

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

  usePageHeader(() => ({
    title: 'Production Lots',
    subtitle: 'Directory & Batches',
    icon: 'reorder',
    actions: [
      {
        label: 'Actions',
        icon: 'bolt',
        variant: 'primary',
        dropdownItems: [
          {
            label: 'New Production Lot',
            icon: 'add_circle',
            onclick: () => { isCreateLotModalOpen = true; }
          },
          {
            label: 'Import Lots (Excel/CSV)',
            icon: 'file_upload',
            onclick: () => { isImportModalOpen = true; }
          },
          {
            label: 'Refresh Lots Data',
            icon: 'refresh',
            onclick: () => { refreshData(); }
          }
        ]
      }
    ]
  }));

  onMount(async () => {
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
      const [stepsData, clientsData, lotsData] = await Promise.all([
        getManufacturingSteps(),
        getClients(),
        getProductionLots()
      ]);

      steps = stepsData || [];
      clients = clientsData || [];
      rawLots = lotsData || [];

      const clientTrackingMode = $tenantConfigStore.clientMode;
      const lotIds = rawLots.map(l => l.id);

      /** @type {Record<string, any>} */
      let journeysMap = {};
      try {
        journeysMap = await getBatchLotJourneys(lotIds) || {};
      } catch {
        // Fallback gracefully
      }

      const enriched = rawLots.map((lot) => {
        const journey = (lot.id ? (journeysMap[lot.id] || journeysMap[lot.id.toLowerCase()] || journeysMap[lot.id.toUpperCase()]) : null) || null;

        /** @type {Array<any>} */
        let activeSteps = [];
        let totalWipQty = 0;
        let activeLotClientsSet = new Set();
        /** @type {Array<string>} */
        let clientNamesList = [];

        if (journey) {
          if (journey.activeBalances) {
            activeSteps = journey.activeBalances.filter((/** @type {any} */ b) => b.quantity > 0);

            const activeNonTerminatingSteps = activeSteps.filter((/** @type {any} */ b) => {
              const stepObj = steps.find((s) => s.id === b.stepId || s.code === b.stepCode);
              return !stepObj?.isTerminating && stepObj?.isActive !== false;
            });

            totalWipQty = activeNonTerminatingSteps.reduce((/** @type {number} */ sum, /** @type {any} */ b) => sum + b.quantity, 0);
          }

          const initialClientId = lot.clientId || journey.clientId;
          const initialClientName = lot.clientName || journey.clientName;

          if (clientTrackingMode === 'lot') {
            if (initialClientId) activeLotClientsSet.add(initialClientId);
            if (initialClientName) clientNamesList.push(initialClientName);
          } else {
            if (journey.movementHistory) {
              const activeStepNames = new Set(activeSteps.map((/** @type {any} */ s) => s.stepName));
              journey.movementHistory.forEach((/** @type {any} */ m) => {
                if (!m.isReversed && m.clientId && m.toStepName && activeStepNames.has(m.toStepName)) {
                  activeLotClientsSet.add(m.clientId);
                  if (m.clientName && !clientNamesList.includes(m.clientName)) {
                    clientNamesList.push(m.clientName);
                  }
                }
              });
            }

            if (activeLotClientsSet.size === 0) {
              if (initialClientId) activeLotClientsSet.add(initialClientId);
              if (initialClientName) clientNamesList.push(initialClientName);
            }
          }
        }

        return {
          ...lot,
          activeSteps,
          totalWipQty,
          clientIds: Array.from(activeLotClientsSet),
          clientNamesDisplay: clientNamesList.length > 0 ? clientNamesList.join(', ') : 'Unassigned',
          createdDate: new Date(lot.createdAtUtc || lot.creationDate || Date.now())
        };
      });

      enrichedLots = enriched;
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to connect to backend server';
    } finally {
      loading = false;
    }
  }

  let filteredLots = $derived(
    enrichedLots.filter(lot => {
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchLot = lot.lotNumber?.toLowerCase().includes(q);
        const matchItem = lot.itemName?.toLowerCase().includes(q);
        const matchClient = lot.clientNamesDisplay?.toLowerCase().includes(q);
        if (!matchLot && !matchItem && !matchClient) return false;
      }

      if (selectedStepId !== 'ALL') {
        const hasStep = lot.activeSteps?.some((/** @type {any} */ s) => s.stepId === selectedStepId);
        if (!hasStep) return false;
      }

      if (selectedClientId !== 'ALL') {
        const hasClient = lot.clientIds?.includes(selectedClientId);
        if (!hasClient) return false;
      }

      if (selectedDateRange !== 'ALL') {
        const lotTime = lot.createdDate.getTime();
        const now = new Date();
        const startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();

        if (selectedDateRange === 'TODAY') {
          if (lotTime < startOfDay) return false;
        } else if (selectedDateRange === '7DAYS') {
          const sevenDaysAgo = startOfDay - 7 * 24 * 60 * 60 * 1000;
          if (lotTime < sevenDaysAgo) return false;
        } else if (selectedDateRange === '30DAYS') {
          const thirtyDaysAgo = startOfDay - 30 * 24 * 60 * 60 * 1000;
          if (lotTime < thirtyDaysAgo) return false;
        } else if (selectedDateRange === 'CUSTOM') {
          if (customStartDate) {
            const start = new Date(customStartDate).getTime();
            if (lotTime < start) return false;
          }
          if (customEndDate) {
            const end = new Date(customEndDate).getTime() + 24 * 60 * 60 * 1000 - 1;
            if (lotTime > end) return false;
          }
        }
      }

      return true;
    })
  );

  let totalWipSum = $derived(
    filteredLots.reduce((acc, l) => {
      if (selectedStepId !== 'ALL') {
        const stepObj = steps.find(s => s.id === selectedStepId);
        if (stepObj?.isActive === false) return acc;
        const stepBalance = l.activeSteps.find((/** @type {any} */ s) => s.stepId === selectedStepId);
        return acc + (stepBalance ? stepBalance.quantity : 0);
      }
      return acc + l.totalWipQty;
    }, 0)
  );

  let totalInitialSum = $derived(filteredLots.reduce((acc, l) => acc + l.initialQuantity, 0));

  let kpiItems = $derived([
    { label: 'Lots', value: filteredLots.length, icon: 'inventory_2', color: 'text-primary' },
    { label: 'WIP Qty', value: `${totalWipSum.toLocaleString()} Pcs`, icon: 'stacks', color: 'text-blue-600 dark:text-blue-400' },
    { label: 'Created', value: `${totalInitialSum.toLocaleString()} Pcs`, icon: 'output', color: 'text-emerald-700 dark:text-emerald-400' },
    { label: 'Clients', value: new Set(filteredLots.flatMap(l => l.clientIds)).size, icon: 'groups', color: 'text-amber-800 dark:text-amber-300' }
  ]);
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  <!-- Unified 4-Metric KPI Strip -->
  <KpiStrip items={kpiItems} />

  <!-- Filter Toolbar -->
  <section class="bg-surface p-3.5 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3.5">
    <div
      role="button"
      tabindex="0"
      onclick={() => isFilterExpanded = !isFilterExpanded}
      onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (isFilterExpanded = !isFilterExpanded)}
      class="flex items-center justify-between gap-3 cursor-pointer md:cursor-default select-none {isFilterExpanded ? 'border-b border-outline-variant/60 pb-3' : 'md:border-b md:border-outline-variant/60 md:pb-3'}"
    >
      <div class="flex items-center gap-2 font-bold text-primary text-sm sm:text-base">
        <span class="material-symbols-outlined text-[20px]">filter_alt</span>
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
            <span class="material-symbols-outlined text-[15px]">restart_alt</span>
            <span>Reset</span>
          </button>
        {/if}
        <span class="md:hidden material-symbols-outlined text-[20px] text-on-surface-variant transition-transform duration-200 {isFilterExpanded ? 'rotate-180' : ''}">
          expand_more
        </span>
      </div>
    </div>

    <!-- Filter Body -->
    <div class="{isFilterExpanded ? 'block' : 'hidden md:block'} space-y-3.5 pt-1 md:pt-0">
      <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <div class="relative w-full max-w-md">
          <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[18px]">search</span>
          <input
            type="text"
            bind:value={searchQuery}
            placeholder="Search by lot number, client, or item..."
            class="w-full pl-9 pr-4 py-2 bg-surface-container-low border border-outline-variant/70 rounded-xl text-xs sm:text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
          />
        </div>

        {#if activeFilterCount > 0}
          <button
            type="button"
            onclick={resetFilters}
            class="hidden sm:flex items-center justify-center gap-1.5 px-3 py-2 rounded-xl border border-red-200 dark:border-red-800 bg-red-50 hover:bg-red-100 dark:bg-red-950/40 text-red-600 dark:text-red-300 text-xs font-semibold transition-colors cursor-pointer shrink-0"
          >
            <span class="material-symbols-outlined text-[16px]">restart_alt</span>
            <span>Clear Filters ({activeFilterCount})</span>
          </button>
        {/if}
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
        <!-- 1. Step Filter -->
        <Select
          label="Manufacturing Step"
          bind:value={selectedStepId}
          options={[
            { label: 'All Steps (Active WIP)', value: 'ALL' },
            ...steps.map(s => ({
              label: `${s.name} (${s.code})${s.isTerminating ? ' (Terminating)' : ''}${s.isActive === false ? ' (Inactive)' : ''}`,
              value: s.id
            }))
          ]}
        />

        <!-- 2. Client Filter -->
        <FeatureGate feature="fields.lots.client">
          <ClientModeGate mode="any">
            <Select
              label="Client / Customer"
              bind:value={selectedClientId}
              options={[
                { label: 'All Clients', value: 'ALL' },
                ...clients.map(c => ({ label: `${c.name} (${c.clientType})`, value: c.id }))
              ]}
            />
          </ClientModeGate>
        </FeatureGate>

        <!-- 3. Date Range Filter -->
        <div class="space-y-1">
          <label for="filter-daterange" class="text-xs font-bold text-on-surface-variant uppercase font-mono">Creation Date</label>
          <select
            id="filter-daterange"
            bind:value={selectedDateRange}
            class="w-full bg-surface-container-low border border-outline-variant/70 rounded-xl px-3 py-2 text-xs sm:text-sm font-semibold text-on-surface outline-none focus:ring-2 focus:ring-primary/20 cursor-pointer"
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
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 border-t border-dashed border-outline-variant/60">
          <div class="space-y-1">
            <label for="custom-start" class="text-xs text-on-surface-variant font-mono">Start Date</label>
            <input
              id="custom-start"
              type="date"
              bind:value={customStartDate}
              class="w-full bg-surface-container-low border border-outline-variant/70 rounded-xl px-3 py-1.5 text-xs sm:text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
            />
          </div>
          <div class="space-y-1">
            <label for="custom-end" class="text-xs text-on-surface-variant font-mono">End Date</label>
            <input
              id="custom-end"
              type="date"
              bind:value={customEndDate}
              class="w-full bg-surface-container-low border border-outline-variant/70 rounded-xl px-3 py-1.5 text-xs sm:text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
            />
          </div>
        </div>
      {/if}
    </div>
  </section>

  <!-- Production Lots Directory Table -->
  <section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden space-y-3.5 p-3.5 sm:p-5">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between border-b border-outline-variant/60 pb-2.5 sm:pb-3 gap-1 sm:gap-2">
      <h2 class="text-sm sm:text-lg font-bold flex items-center gap-1.5 whitespace-nowrap text-on-surface">
        <span>Lots Directory</span>
        <span class="text-xs font-mono font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full ml-0.5">
          {filteredLots.length}
        </span>
      </h2>
      <span class="text-[10px] sm:text-xs text-on-surface-variant font-mono">
        Showing {filteredLots.length} of {enrichedLots.length} total lots
      </span>
    </div>

    {#if loading}
      <LoadingSpinner message="Loading production lots..." />
    {:else}
      <!-- Mobile View: Card Stack Column -->
      <div class="block md:hidden space-y-2">
        {#each filteredLots as lot (lot.id)}
          <LotCard {lot} {steps} {selectedStepId} />
        {/each}
        {#if filteredLots.length === 0}
          <EmptyState icon="filter_alt_off" title="No matching lots" message="Try adjusting your filter criteria." />
        {/if}
      </div>

      <!-- Desktop View: Table -->
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/60 bg-surface">
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
            {#each filteredLots as lot (lot.id)}
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
                  {(selectedStepId !== 'ALL' ? (lot.activeSteps.find(s => s.stepId === selectedStepId)?.quantity ?? 0) : lot.totalWipQty).toLocaleString()} Pcs
                </td>
                <td class="px-4 py-3">
                  <div class="flex flex-wrap gap-1.5 items-center">
                    {#each lot.activeSteps as step}
                      {@const stepObj = steps.find(s => s.id === step.stepId || s.code === step.stepCode)}
                      <span class="px-2 py-0.5 rounded text-[11px] font-bold font-mono {stepObj?.isTerminating ? 'bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300 dark:border-amber-700' : stepObj?.isActive === false ? 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 border border-zinc-300 dark:border-zinc-700' : 'bg-primary-container text-on-primary-container'} flex items-center gap-1">
                        <span class="material-symbols-outlined text-[12px]">{STEP_ICON_MAP[step.stepCode] || 'work'}</span>
                        {step.stepName}: {step.quantity} Pcs
                        {#if stepObj?.isTerminating}
                          <span class="text-[9px] uppercase tracking-wider font-extrabold text-amber-700 dark:text-amber-400 ml-0.5">(Terminated)</span>
                        {:else if stepObj?.isActive === false}
                          <span class="text-[9px] uppercase tracking-wider font-extrabold text-zinc-500 ml-0.5">(Inactive)</span>
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
                    href="/inventory?lot={lot.id}"
                    class="px-2.5 py-1 text-xs bg-primary/10 text-primary hover:bg-primary/20 rounded font-bold transition-colors inline-flex items-center gap-1"
                  >
                    <span class="material-symbols-outlined text-[14px]">visibility</span>
                    View WIP
                  </a>
                </td>
              </tr>
            {/each}
            {#if filteredLots.length === 0}
              <tr>
                <td colspan="6" class="px-4 py-12 text-center text-on-surface-variant">
                  <EmptyState icon="filter_alt_off" title="No matching production lots found" message="Try adjusting the search query or filters above." />
                </td>
              </tr>
            {/if}
          </tbody>
        </table>
      </div>
    {/if}
  </section>
</main>

<CreateLotModal
  bind:isOpen={isCreateLotModalOpen}
  {steps}
  {clients}
  onSuccess={(lot) => {
    successMessage = `Production Lot '${lot.lotNumber}' created successfully!`;
    refreshData();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to create production lot.';
  }}
/>

<ImportLotsModal
  bind:isOpen={isImportModalOpen}
  {steps}
  onSuccess={() => {
    successMessage = 'Lots imported successfully!';
    refreshData();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Import failed.';
  }}
/>
