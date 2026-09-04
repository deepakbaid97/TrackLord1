<script>
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { getManufacturingSteps, getLotsAtStep } from '$lib/api.js';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import { STEP_ICON_MAP } from '$lib/services/lotService.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';

  let stepId = $derived($page.params.id);

  let loading = $state(true);
  let errorMessage = $state('');

  /** @type {any} */
  let step = $state(null);
  /** @type {any[]} */
  let allSteps = $state([]);
  let lastLoadedStepId = $state('');

  let currentStepIndex = $derived(allSteps.findIndex(s => s.id === stepId));
  let prevStep = $derived(currentStepIndex > 0 ? allSteps[currentStepIndex - 1] : null);
  let nextStep = $derived(currentStepIndex >= 0 && currentStepIndex < allSteps.length - 1 ? allSteps[currentStepIndex + 1] : null);

  function switchStep(newId) {
    if (newId && newId !== stepId) {
      goto(`/steps/${newId}/lots`);
    }
  }

  $effect(() => {
    const currentId = stepId;
    if (allSteps.length > 0 && currentId && currentId !== lastLoadedStepId) {
      lastLoadedStepId = currentId;
      step = allSteps.find(s => s.id === currentId) || null;
      fetchLots();
    }
  });

  /** @type {any[]} */
  let lots = $state([]);
  let totalQuantity = $state(0);
  let totalLots = $state(0);

  // Filter Toolbar States
  // filterMode: 'CURRENT' | 'AS_OF' | 'PERIOD'
  let filterMode = $state('CURRENT');
  let asOfDate = $state(new Date().toISOString().slice(0, 10));
  let fromDate = $state('');
  let toDate = $state(new Date().toISOString().slice(0, 10));
  let searchQuery = $state('');
  let periodPreset = $state('7DAYS'); // 'TODAY' | '7DAYS' | '30DAYS' | 'CUSTOM'

  let searchTimeout;

  usePageHeader(() => ({
    title: step ? `${step.name} (${step.code})` : 'Step Lots',
    subtitle: 'Step Lots & Historical WIP Inspection',
    icon: step ? (STEP_ICON_MAP[step.code] || 'alt_route') : 'alt_route',
    actions: [
      {
        label: 'Back to Steps',
        icon: 'arrow_back',
        onclick: () => goto('/steps')
      }
    ]
  }));

  function initPeriodDates(preset) {
    const today = new Date();
    toDate = today.toISOString().slice(0, 10);

    if (preset === 'TODAY') {
      fromDate = today.toISOString().slice(0, 10);
    } else if (preset === '7DAYS') {
      const d = new Date(today);
      d.setDate(d.getDate() - 7);
      fromDate = d.toISOString().slice(0, 10);
    } else if (preset === '30DAYS') {
      const d = new Date(today);
      d.setDate(d.getDate() - 30);
      fromDate = d.toISOString().slice(0, 10);
    }
  }

  async function loadData() {
    if (!stepId) return;
    loading = true;
    errorMessage = '';

    try {
      // 1. Fetch all steps metadata
      const stepsData = await getManufacturingSteps();
      allSteps = (stepsData || []).slice().sort((a, b) => a.sequenceOrder - b.sequenceOrder);
      lastLoadedStepId = stepId;
      step = allSteps.find(s => s.id === stepId) || null;

      // 2. Fetch lots at step
      await fetchLots();
    } catch (err) {
      errorMessage = err.message || 'Failed to load step details.';
    } finally {
      loading = false;
    }
  }

  async function fetchLots() {
    if (!stepId) return;
    loading = true;
    errorMessage = '';

    try {
      /** @type {Record<string, string>} */
      const params = {};

      if (filterMode === 'AS_OF' && asOfDate) {
        params.asOfDate = new Date(`${asOfDate}T23:59:59Z`).toISOString();
      } else if (filterMode === 'PERIOD') {
        if (fromDate) params.fromDate = new Date(`${fromDate}T00:00:00Z`).toISOString();
        if (toDate) params.toDate = new Date(`${toDate}T23:59:59Z`).toISOString();
      }

      if (searchQuery.trim()) {
        params.search = searchQuery.trim();
      }

      const res = await getLotsAtStep(stepId, params);
      lots = res.lots || [];
      totalQuantity = res.totalQuantity || 0;
      totalLots = res.totalLots || 0;
    } catch (err) {
      errorMessage = err.message || 'Failed to load lots for this step.';
    } finally {
      loading = false;
    }
  }

  function handleModeChange(mode) {
    filterMode = mode;
    if (mode === 'PERIOD' && !fromDate) {
      initPeriodDates(periodPreset);
    }
    fetchLots();
  }

  function handlePresetChange(preset) {
    periodPreset = preset;
    if (preset !== 'CUSTOM') {
      initPeriodDates(preset);
      fetchLots();
    }
  }

  function handleSearchInput(e) {
    searchQuery = e.target.value;
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      fetchLots();
    }, 300);
  }

  function formatDateTime(dtStr) {
    if (!dtStr) return '—';
    const d = new Date(dtStr);
    return `${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
  }

  onMount(() => {
    initPeriodDates('7DAYS');
    loadData();
  });
</script>

<main class="space-y-3 max-w-7xl mx-auto px-3 sm:px-6 py-2 sm:py-3">
  <!-- Navigation Breadcrumb Bar -->
  <div class="flex items-center gap-1.5 text-xs text-on-surface-variant font-medium">
    <a href="/steps" class="hover:text-primary transition-colors flex items-center gap-0.5">
      <span class="material-symbols-outlined text-[15px]">arrow_back</span>
      <span>Manufacturing Steps</span>
    </a>
    <span>/</span>
    <span class="font-bold text-on-surface font-mono">{step?.name || 'Step'} ({step?.code || ''})</span>
  </div>

  <!-- Compact Hero Step Header Card with Step Switcher -->
  <div class="bg-surface rounded-xl border border-outline-variant/70 p-2 sm:p-2.5 shadow-2xs flex flex-col sm:flex-row sm:items-center justify-between gap-2">
    <!-- Left: Icon + Step Dropdown Selector + Prev/Next Steppers -->
    <div class="flex items-center gap-2 min-w-0 flex-1">
      <div class="w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center shrink-0">
        <span class="material-symbols-outlined text-[19px]">{STEP_ICON_MAP[step?.code] || 'alt_route'}</span>
      </div>

      <div class="flex items-center gap-1.5 min-w-0 flex-wrap sm:flex-nowrap">
        <!-- Styled Step Switcher Dropdown -->
        <div class="relative min-w-0">
          <select
            value={stepId}
            onchange={(e) => switchStep(e.currentTarget.value)}
            class="appearance-none bg-surface-container-low hover:bg-surface-container border border-outline-variant/70 hover:border-primary/50 text-on-surface font-bold text-xs sm:text-sm font-sans rounded-lg pl-2.5 pr-7 py-1 cursor-pointer outline-none focus:ring-2 focus:ring-primary/20 transition-all truncate max-w-[200px] sm:max-w-[280px]"
            title="Click to switch manufacturing step"
            aria-label="Switch manufacturing step"
          >
            {#each allSteps as s}
              <option value={s.id}>
                #{s.sequenceOrder} · {s.name} ({s.code}){s.isTerminating ? ' (Terminating)' : ''}
              </option>
            {/each}
          </select>
        </div>

        <!-- Quick Stepper (Previous & Next Step) -->
        <div class="flex items-center gap-0.5 shrink-0">
          <button
            type="button"
            disabled={!prevStep}
            onclick={() => prevStep && switchStep(prevStep.id)}
            class="w-7 h-7 flex items-center justify-center rounded-lg border border-outline-variant/60 text-on-surface-variant hover:text-primary hover:bg-surface-container disabled:opacity-25 disabled:pointer-events-none transition-colors cursor-pointer"
            title={prevStep ? `Previous: #${prevStep.sequenceOrder} ${prevStep.name}` : 'First stage'}
            aria-label="Previous step"
          >
            <span class="material-symbols-outlined text-[16px]">chevron_left</span>
          </button>
          <button
            type="button"
            disabled={!nextStep}
            onclick={() => nextStep && switchStep(nextStep.id)}
            class="w-7 h-7 flex items-center justify-center rounded-lg border border-outline-variant/60 text-on-surface-variant hover:text-primary hover:bg-surface-container disabled:opacity-25 disabled:pointer-events-none transition-colors cursor-pointer"
            title={nextStep ? `Next: #${nextStep.sequenceOrder} ${nextStep.name}` : 'Final stage'}
            aria-label="Next step"
          >
            <span class="material-symbols-outlined text-[16px]">chevron_right</span>
          </button>
        </div>

        {#if step?.isTerminating}
          <span class="hidden md:inline-flex px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 dark:bg-amber-950 text-amber-800 dark:text-amber-300 border border-amber-300 shrink-0">
            Terminating
          </span>
        {/if}
      </div>
    </div>

    <!-- Right: Compact KPI Badges -->
    <div class="flex items-center gap-2 self-start sm:self-center shrink-0">
      <div class="bg-surface-container-low rounded-lg px-2.5 py-1 border border-outline-variant/60 flex items-center gap-1.5 font-mono text-xs">
        <span class="text-on-surface-variant text-[10px] uppercase font-semibold">Active Lots:</span>
        <span class="font-bold text-primary">{totalLots}</span>
      </div>
      <div class="bg-surface-container-low rounded-lg px-2.5 py-1 border border-outline-variant/60 flex items-center gap-1.5 font-mono text-xs">
        <span class="text-on-surface-variant text-[10px] uppercase font-semibold">Total WIP:</span>
        <span class="font-bold text-primary">{totalQuantity.toLocaleString()} Pcs</span>
      </div>
    </div>
  </div>

  <!-- Compact Filter & Date Controls Section -->
  <section class="bg-surface rounded-xl border border-outline-variant/70 p-2.5 sm:p-3 shadow-2xs space-y-2">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
      <!-- Shortened Mode Toggle Buttons -->
      <div class="inline-flex p-0.5 bg-surface-container-low rounded-lg border border-outline-variant/60 text-xs font-semibold w-full sm:w-auto">
        <button
          type="button"
          class="flex-1 sm:flex-initial text-center px-3 py-1.5 rounded-md transition-all cursor-pointer {filterMode === 'CURRENT' ? 'bg-primary text-on-primary shadow-2xs font-bold' : 'text-on-surface-variant hover:text-on-surface'}"
          onclick={() => handleModeChange('CURRENT')}
        >
          Current
        </button>
        <button
          type="button"
          class="flex-1 sm:flex-initial text-center px-3 py-1.5 rounded-md transition-all cursor-pointer {filterMode === 'AS_OF' ? 'bg-primary text-on-primary shadow-2xs font-bold' : 'text-on-surface-variant hover:text-on-surface'}"
          onclick={() => handleModeChange('AS_OF')}
        >
          By Date
        </button>
        <button
          type="button"
          class="flex-1 sm:flex-initial text-center px-3 py-1.5 rounded-md transition-all cursor-pointer {filterMode === 'PERIOD' ? 'bg-primary text-on-primary shadow-2xs font-bold' : 'text-on-surface-variant hover:text-on-surface'}"
          onclick={() => handleModeChange('PERIOD')}
        >
          Period
        </button>
      </div>

      <!-- Search Box -->
      <div class="relative w-full sm:w-64">
        <span class="material-symbols-outlined absolute left-2.5 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[16px]">search</span>
        <input
          type="text"
          placeholder="Filter lots by number or client..."
          value={searchQuery}
          oninput={handleSearchInput}
          class="w-full pl-8 pr-3 py-1.5 bg-surface-container-low border border-outline-variant/70 rounded-lg text-xs text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
        />
      </div>
    </div>

    <!-- Mode Specific Pickers -->
    {#if filterMode === 'AS_OF'}
      <div class="flex flex-wrap items-center gap-2 pt-2 border-t border-dashed border-outline-variant/60 text-xs">
        <span class="font-semibold text-on-surface-variant">As of Date:</span>
        <input
          type="date"
          bind:value={asOfDate}
          onchange={fetchLots}
          class="px-2.5 py-1 bg-surface-container-low border border-outline-variant/70 rounded-lg text-xs font-mono text-on-surface outline-none focus:ring-1 focus:ring-primary cursor-pointer"
        />
        <button
          type="button"
          class="px-2 py-0.5 rounded-md bg-surface hover:bg-surface-container border border-outline-variant/60 text-on-surface-variant font-medium cursor-pointer text-xs"
          onclick={() => { asOfDate = new Date().toISOString().slice(0, 10); fetchLots(); }}
        >
          Today
        </button>
        <button
          type="button"
          class="px-2 py-0.5 rounded-md bg-surface hover:bg-surface-container border border-outline-variant/60 text-on-surface-variant font-medium cursor-pointer text-xs"
          onclick={() => {
            const y = new Date();
            y.setDate(y.getDate() - 1);
            asOfDate = y.toISOString().slice(0, 10);
            fetchLots();
          }}
        >
          Yesterday
        </button>
      </div>
    {:else if filterMode === 'PERIOD'}
      <div class="flex flex-wrap items-center gap-2 pt-2 border-t border-dashed border-outline-variant/60 text-xs">
        <div class="flex items-center gap-1">
          {#each ['TODAY', '7DAYS', '30DAYS', 'CUSTOM'] as p}
            <button
              type="button"
              class="px-2 py-0.5 rounded-md border transition-all cursor-pointer text-xs {periodPreset === p ? 'bg-primary/10 text-primary border-primary font-bold' : 'bg-surface text-on-surface-variant border-outline-variant/60 hover:bg-surface-container'}"
              onclick={() => handlePresetChange(p)}
            >
              {p === 'TODAY' ? 'Today' : p === '7DAYS' ? '7D' : p === '30DAYS' ? '30D' : 'Custom'}
            </button>
          {/each}
        </div>

        <div class="flex items-center gap-1.5">
          <input
            type="date"
            bind:value={fromDate}
            onchange={fetchLots}
            class="px-2 py-1 bg-surface-container-low border border-outline-variant/70 rounded-lg font-mono text-xs text-on-surface outline-none focus:ring-1 focus:ring-primary cursor-pointer"
          />
          <span class="text-on-surface-variant font-mono text-[10px]">to</span>
          <input
            type="date"
            bind:value={toDate}
            onchange={fetchLots}
            class="px-2 py-1 bg-surface-container-low border border-outline-variant/70 rounded-lg font-mono text-xs text-on-surface outline-none focus:ring-1 focus:ring-primary cursor-pointer"
          />
        </div>
      </div>
    {/if}
  </section>

  <!-- Lots Table / Cards Section -->
  <section class="bg-surface rounded-xl border border-outline-variant/70 p-3 sm:p-4 shadow-2xs space-y-2.5">
    <div class="flex items-center justify-between border-b border-outline-variant/60 pb-2.5">
      <h2 class="font-bold text-xs sm:text-sm text-on-surface flex items-center gap-1.5">
        <span>Lots in Stage</span>
        <span class="px-2 py-0.2 rounded-full bg-primary/10 text-primary font-mono text-xs font-bold">
          {lots.length}
        </span>
      </h2>
      <span class="text-xs text-on-surface-variant font-mono">
        Total Quantity: {totalQuantity.toLocaleString()} Pcs
      </span>
    </div>

    {#if loading}
      <div class="py-12">
        <LoadingSpinner message="Calculating lots at this stage..." />
      </div>
    {:else if errorMessage}
      <div class="p-3 bg-error-container/20 border border-error/30 text-error rounded-lg text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[16px]">error</span>
        <span>{errorMessage}</span>
        <button type="button" onclick={fetchLots} class="underline font-semibold ml-auto cursor-pointer">Retry</button>
      </div>
    {:else if lots.length === 0}
      <div class="py-8">
        <EmptyState
          icon="inventory_2"
          title="No lots at this stage"
          message={filterMode === 'CURRENT' ? 'There are currently no active lots at this manufacturing step.' : 'No lots were recorded at this step during the selected date period.'}
        />
      </div>
    {:else}
      <!-- Mobile View: Clean, Compact, Clickable Cards Stack -->
      <div class="block md:hidden space-y-2">
        {#each lots as lot (lot.lotId)}
          <div
            role="button"
            tabindex="0"
            onclick={() => goto(`/inventory?lot=${lot.lotId}`)}
            onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && goto(`/inventory?lot=${lot.lotId}`)}
            class="bg-surface-container-low p-3 rounded-xl border border-outline-variant/60 hover:border-primary/50 shadow-2xs hover:shadow-xs transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
            title="Tap to view lot #{lot.lotNumber} journey"
          >
            <div class="flex items-center justify-between">
              <div class="font-bold text-primary flex items-center gap-1 text-sm font-mono">
                <span class="material-symbols-outlined text-[15px]">tag</span>
                <span>{lot.lotNumber}</span>
              </div>
              <div class="flex items-center gap-1.5">
                <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-100 dark:bg-emerald-950 text-emerald-800 dark:text-emerald-300 border border-emerald-300">
                  {lot.status}
                </span>
                <span class="material-symbols-outlined text-[16px] text-on-surface-variant/40">
                  chevron_right
                </span>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-2 text-xs">
              <div>
                <span class="text-on-surface-variant block text-[9px] uppercase font-mono">Stage Quantity</span>
                <span class="font-bold font-mono text-primary text-xs">{lot.quantityAtStep.toLocaleString()} Pcs</span>
              </div>
              <div>
                <span class="text-on-surface-variant block text-[9px] uppercase font-mono">Entered Step (Created At)</span>
                <span class="font-semibold font-mono text-on-surface text-xs">{formatDateTime(lot.stepCreatedAt)}</span>
              </div>
            </div>

            {#if lot.clientName}
              <div class="text-[11px] flex items-center gap-1 text-on-surface-variant pt-1.5 border-t border-dashed border-outline-variant/40">
                <span class="material-symbols-outlined text-[12px] text-primary">badge</span>
                <span>Client: {lot.clientName}</span>
              </div>
            {/if}
          </div>
        {/each}
      </div>

      <!-- Desktop View: Table -->
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/60 bg-surface">
        <table class="w-full text-left border-collapse text-xs sm:text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-4 py-3">Lot Number</th>
              {#if $tenantConfigStore.clientMode === 'lot'}
                <th class="px-4 py-3">Client</th>
              {/if}
              <th class="px-4 py-3 text-right">Quantity In Step</th>
              <th class="px-4 py-3">
                <div class="flex items-center gap-1 cursor-help" title="Date of the created movement that moved this lot into this step">
                  <span>Entered Step (Created At)</span>
                  <span class="material-symbols-outlined text-[14px] text-primary">info</span>
                </div>
              </th>
              <th class="px-4 py-3 text-center">Status</th>
              <th class="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#each lots as lot (lot.lotId)}
              <tr class="hover:bg-surface-container-low/60 transition-colors">
                <td class="px-4 py-3 whitespace-nowrap">
                  <a
                    href="/inventory?lot={lot.lotId}"
                    class="font-bold text-primary hover:underline flex items-center gap-1.5"
                    title="View complete lot journey"
                  >
                    <span class="material-symbols-outlined text-[16px]">tag</span>
                    <span>{lot.lotNumber}</span>
                  </a>
                </td>

                {#if $tenantConfigStore.clientMode === 'lot'}
                  <td class="px-4 py-3 whitespace-nowrap">
                    {#if lot.clientName}
                      <span class="px-2 py-0.5 rounded-full text-xs font-medium bg-surface-container text-on-surface flex items-center gap-1 border border-outline-variant/50 w-fit">
                        <span class="material-symbols-outlined text-[12px] text-primary">badge</span>
                        {lot.clientName}
                      </span>
                    {:else}
                      <span class="text-xs text-on-surface-variant font-mono">—</span>
                    {/if}
                  </td>
                {/if}

                <td class="px-4 py-3 whitespace-nowrap text-right font-mono font-bold text-primary">
                  {lot.quantityAtStep.toLocaleString()} Pcs
                </td>

                <td class="px-4 py-3 whitespace-nowrap">
                  <div class="flex flex-col">
                    <span class="font-mono font-bold text-on-surface text-xs">
                      {formatDateTime(lot.stepCreatedAt)}
                    </span>
                    <span class="text-[10px] text-on-surface-variant font-sans">
                      Movement into {step?.code || ''}
                    </span>
                  </div>
                </td>

                <td class="px-4 py-3 whitespace-nowrap text-center">
                  <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 dark:bg-emerald-950 text-emerald-800 dark:text-emerald-300 border border-emerald-300">
                    {lot.status}
                  </span>
                </td>

                <td class="px-4 py-3 whitespace-nowrap text-right">
                  <a
                    href="/inventory?lot={lot.lotId}"
                    class="px-2.5 py-1 text-xs font-semibold text-primary hover:bg-primary/10 rounded-lg transition-colors cursor-pointer inline-flex items-center gap-1"
                  >
                    <span>Journey</span>
                    <span class="material-symbols-outlined text-[14px]">timeline</span>
                  </a>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
  </section>
</main>
