<script>
  import { onMount } from 'svelte';
  import { getDashboardOverview, getMaterialLots } from '$lib/api.js';
  import FeatureGate from '$lib/components/ui/FeatureGate.svelte';
  import ClientModeGate from '$lib/components/ui/ClientModeGate.svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { STEP_ICON_MAP, STEP_COLOR_MAP } from '$lib/services/lotService.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

  let loading = $state(true);
  let errorMessage = $state('');

  /** @type {any} */
  let overview = $state(null);

  /** Material availability state */
  let materialLoading = $state(true);
  let materialError = $state('');
  let totalMaterialAvailable = $state(0);
  /** @type {any[]} */
  let materialLots = $state([]);

  // Dynamic layout reactivity based on active widget configuration
  let showActiveLotsKpi = $derived(
    $tenantConfigStore ? tenantConfigStore.isFeatureEnabled('dashboard.kpi.activeLots') : true
  );
  let showTotalWipKpi = $derived(
    $tenantConfigStore ? tenantConfigStore.isFeatureEnabled('dashboard.kpi.totalWip') : true
  );
  let hasAnyKpi = $derived(showActiveLotsKpi || showTotalWipKpi);
  let kpiGridClass = $derived(
    showActiveLotsKpi && showTotalWipKpi ? 'grid-cols-1 sm:grid-cols-2' : 'grid-cols-1'
  );

  let showWipByStep = $derived(
    $tenantConfigStore ? tenantConfigStore.isFeatureEnabled('dashboard.sections.wipByStep') : true
  );
  let showClientDistribution = $derived(
    $tenantConfigStore
      ? tenantConfigStore.isFeatureEnabled('dashboard.sections.clientDistribution') &&
        tenantConfigStore.isFeatureEnabled('pages.clients') &&
        tenantConfigStore.isClientMode('any')
      : true
  );
  let hasAnyMidSection = $derived(showWipByStep || showClientDistribution);
  let midGridClass = $derived(
    showWipByStep && showClientDistribution ? 'grid-cols-1 lg:grid-cols-2' : 'grid-cols-1'
  );

  let showMaterialAvailability = $derived(
    $tenantConfigStore ? tenantConfigStore.isFeatureEnabled('dashboard.sections.materialAvailability') : true
  );
  let showActiveLots = $derived(
    $tenantConfigStore ? tenantConfigStore.isFeatureEnabled('dashboard.sections.activeLots') : true
  );

  let hasAnyDashboardWidget = $derived(
    showActiveLotsKpi ||
    showTotalWipKpi ||
    showWipByStep ||
    showClientDistribution ||
    showMaterialAvailability ||
    showActiveLots
  );

  usePageHeader(() => ({
    title: 'Dashboard',
    subtitle: 'Analytics & Floor Overview',
    icon: 'dashboard',
    actions: [
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: 'outline',
        onclick: () => refreshDashboard()
      }
    ]
  }));

  onMount(async () => {
    await refreshDashboard();
  });

  async function refreshDashboard() {
    loading = true;
    materialLoading = true;
    errorMessage = '';
    materialError = '';

    try {
      const [overviewData, fetchedLots] = await Promise.all([
        getDashboardOverview().catch(err => {
          errorMessage = err.message || 'Failed to load dashboard data';
          return null;
        }),
        getMaterialLots().catch(err => {
          materialError = err.message || 'Failed to load material data';
          return [];
        })
      ]);

      if (overviewData) overview = overviewData;
      if (fetchedLots) {
        materialLots = fetchedLots;
        totalMaterialAvailable = fetchedLots.reduce((sum, lot) => {
          const qty = lot.remainingQuantity ?? lot.initialQuantity ?? 0;
          return sum + Number(qty);
        }, 0);
      }
    } finally {
      loading = false;
      materialLoading = false;
    }
  }

  let maxStepWip = $derived(
    overview?.stepSummaries?.length > 0
      ? Math.max(...overview.stepSummaries.map((/** @type {any} */ s) => s.totalWipQuantity), 1)
      : 1
  );
</script>

<Callout bind:message={errorMessage} type="error" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  {#if loading}
    <LoadingSpinner message="Loading dashboard metrics..." />
  {:else if overview}
    {#if !hasAnyDashboardWidget}
      <!-- Empty Dashboard Fallback when all widgets are disabled -->
      <div class="bg-surface rounded-2xl border border-outline-variant p-8 sm:p-12 text-center flex flex-col items-center justify-center gap-4 my-6 shadow-sm">
        <div class="w-16 h-16 rounded-2xl bg-primary/10 text-primary flex items-center justify-center">
          <span class="material-symbols-outlined text-[32px]">tune</span>
        </div>
        <div class="max-w-md">
          <h2 class="text-base sm:text-lg font-bold text-on-surface mb-1 font-['Inter']">All Dashboard Widgets Are Hidden</h2>
          <p class="text-xs sm:text-sm text-on-surface-variant leading-relaxed">
            All KPI cards and analytics sections are currently disabled in Tenant Settings. You can enable them anytime.
          </p>
        </div>
        <Button variant="primary" icon="settings" onclick={() => tenantConfigStore.openModal()}>
          Configure Tenant Settings
        </Button>
      </div>
    {:else}
      <!-- KPI Summary Cards (Auto-adjusts to 1 or 2 columns based on enabled cards) -->
      {#if hasAnyKpi}
        <div class="grid {kpiGridClass} gap-2.5 sm:gap-4">
          <FeatureGate feature="dashboard.kpi.activeLots">
            <a
              href="/lots"
              class="bg-surface p-3.5 sm:p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-2.5 sm:gap-4 group hover:shadow-md transition-all cursor-pointer hover:border-primary/50 min-w-0"
              title="View all lots in directory"
            >
              <div class="p-2 sm:p-3 bg-primary/10 rounded-xl text-primary group-hover:bg-primary/15 transition-colors shrink-0">
                <span class="material-symbols-outlined text-[22px] sm:text-[28px]">inventory_2</span>
              </div>
              <div class="min-w-0">
                <div class="text-[10px] sm:text-xs text-on-surface-variant group-hover:text-primary transition-colors uppercase font-mono font-bold tracking-wider truncate">Active Lots</div>
                <div class="text-xl sm:text-3xl font-bold text-on-surface group-hover:text-primary transition-colors leading-tight truncate">{overview.totalActiveLots}</div>
              </div>
            </a>
          </FeatureGate>

          <FeatureGate feature="dashboard.kpi.totalWip">
            <div class="bg-surface p-3.5 sm:p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-2.5 sm:gap-4 group hover:shadow-md transition-shadow min-w-0">
              <div class="p-2 sm:p-3 bg-blue-500/10 rounded-xl text-blue-600 group-hover:bg-blue-500/15 transition-colors shrink-0">
                <span class="material-symbols-outlined text-[22px] sm:text-[28px]">stacks</span>
              </div>
              <div class="min-w-0">
                <div class="text-[10px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider truncate">Total WIP</div>
                <div class="text-xl sm:text-3xl font-bold text-on-surface leading-tight truncate">{overview.totalActiveQuantity.toLocaleString()}</div>
                <div class="text-[10px] sm:text-[11px] text-on-surface-variant font-mono truncate">Pieces</div>
              </div>
            </div>
          </FeatureGate>
        </div>
      {/if}

      <!-- Unified 2-Column Desktop Grid for all active Dashboard Section Widgets -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-6 items-start">
        <!-- Widget 1: WIP by Manufacturing Step -->
        <FeatureGate feature="dashboard.sections.wipByStep">
          <section class="bg-surface p-4 sm:p-6 rounded-xl border border-outline-variant shadow-sm h-full flex flex-col">
            <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-3 sm:mb-4">
              <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
                <span class="material-symbols-outlined text-primary text-[22px]">factory</span>
                WIP by Step
              </h2>
              <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
                {overview.stepSummaries?.length || 0} steps
              </span>
            </div>

            {#if overview.stepSummaries && overview.stepSummaries.length > 0}
              <div class="space-y-2 sm:space-y-3.5 flex-1">
                {#each overview.stepSummaries as step}
                  {@const barPercent = maxStepWip > 0 ? (step.totalWipQuantity / maxStepWip) * 100 : 0}
                  {@const barColor = STEP_COLOR_MAP[step.stepCode] || 'bg-primary'}
                  <a
                    href="/lots?step={step.stepId}"
                    class="group block p-1.5 sm:p-2 rounded-lg hover:bg-surface-container-low transition-colors cursor-pointer"
                    title="View {step.stepName} lots in directory"
                  >
                    <div class="flex items-center justify-between mb-1 sm:mb-1.5">
                      <div class="flex items-center gap-1.5 sm:gap-2">
                        <span class="material-symbols-outlined text-[16px] sm:text-[18px] text-on-surface-variant group-hover:text-primary transition-colors">{STEP_ICON_MAP[step.stepCode] || 'work'}</span>
                        <span class="font-bold text-xs sm:text-sm text-on-surface group-hover:text-primary transition-colors">{step.stepName}</span>
                        <span class="text-[9px] sm:text-[10px] font-mono text-on-surface-variant bg-surface-container-low px-1.5 py-0.5 rounded">{step.stepCode}</span>
                        {#if step.isActive === false}
                          <span class="text-[9px] font-mono font-bold text-zinc-500 bg-zinc-200 dark:bg-zinc-800 px-1.5 py-0.5 rounded">Inactive</span>
                        {/if}
                      </div>
                      <div class="flex items-center gap-2 sm:gap-3">
                        <span class="text-[11px] sm:text-xs text-on-surface-variant font-mono">{step.activeLotsCount} lots</span>
                        <span class="font-bold text-xs sm:text-sm text-primary font-mono min-w-[50px] sm:min-w-[60px] text-right">{step.totalWipQuantity.toLocaleString()} Pcs</span>
                      </div>
                    </div>
                    <div class="w-full bg-surface-variant/60 rounded-full h-1.5 sm:h-2.5 overflow-hidden">
                      <div
                        class="{barColor} h-1.5 sm:h-2.5 rounded-full transition-all duration-700 ease-out group-hover:opacity-90"
                        style="width: {Math.max(barPercent, step.totalWipQuantity > 0 ? 2 : 0)}%"
                      ></div>
                    </div>
                  </a>
                {/each}
              </div>
            {:else}
              <EmptyState icon="alt_route" title="No manufacturing steps configured" message="Steps will appear here once created." />
            {/if}
          </section>
        </FeatureGate>

        <!-- Widget 2: Client Distribution -->
        <FeatureGate feature="dashboard.sections.clientDistribution">
          <FeatureGate feature="pages.clients">
            <ClientModeGate mode="any">
              <section class="bg-surface p-4 sm:p-6 rounded-xl border border-outline-variant shadow-sm h-full flex flex-col">
                <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-3 sm:mb-4">
                  <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
                    <span class="material-symbols-outlined text-primary text-[22px]">groups</span>
                    Client Distribution
                  </h2>
                  <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
                    {overview.clientSummaries?.length || 0} active
                  </span>
                </div>

                {#if overview.clientSummaries && overview.clientSummaries.length > 0}
                  <div class="overflow-x-auto flex-1">
                    <table class="w-full text-left border-collapse text-xs sm:text-sm">
                      <thead>
                        <tr class="border-b border-outline-variant text-[11px] sm:text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                          <th class="pb-2 pr-3">Client</th>
                          <th class="pb-2 pr-3">Type</th>
                          <th class="pb-2 pr-3 text-right">Lots</th>
                          <th class="pb-2 text-right">Qty</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y divide-outline-variant/50">
                        {#each overview.clientSummaries as client}
                          <tr
                            onclick={() => window.location.href = `/lots?client=${client.clientId}`}
                            class="hover:bg-surface-container-low/70 transition-colors cursor-pointer group"
                            title="View lots associated with {client.clientName}"
                          >
                            <td class="py-2.5 pr-3">
                              <div class="flex items-center gap-1.5">
                                <span class="material-symbols-outlined text-[16px] text-primary/70 group-hover:text-primary transition-colors">badge</span>
                                <span class="font-bold text-on-surface group-hover:text-primary transition-colors truncate max-w-[120px] sm:max-w-none">{client.clientName}</span>
                              </div>
                            </td>
                            <td class="py-2.5 pr-3">
                              <span class="px-1.5 py-0.5 rounded-full text-[9px] sm:text-[10px] font-bold uppercase tracking-wider
                                {client.clientType === 'Buyer' ? 'bg-blue-100 text-blue-800' :
                                 client.clientType === 'Wholesaler' ? 'bg-emerald-100 text-emerald-800' :
                                 client.clientType === 'Job Worker' ? 'bg-amber-100 text-amber-800' :
                                 'bg-purple-100 text-purple-800'}">
                                {client.clientType}
                              </span>
                            </td>
                            <td class="py-2.5 pr-3 text-right font-mono text-on-surface-variant">{client.totalLotsAssociated}</td>
                            <td class="py-2.5 text-right font-bold text-primary font-mono">{client.totalQuantityMoved.toLocaleString()}</td>
                          </tr>
                        {/each}
                      </tbody>
                    </table>
                  </div>
                {:else}
                  <EmptyState icon="groups" title="No client activity recorded yet" message="Movements or lots with assigned clients will appear here." />
                {/if}
              </section>
            </ClientModeGate>
          </FeatureGate>
        </FeatureGate>

        <!-- Widget 3: Material Availability -->
        <FeatureGate feature="dashboard.sections.materialAvailability">
          <section class="bg-surface p-4 sm:p-6 rounded-xl border border-outline-variant shadow-sm h-full flex flex-col">
            <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-3 sm:mb-4">
              <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
                <span class="material-symbols-outlined text-green-600 text-[22px]">inventory_2</span>
                Material Availability
              </h2>
              <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
                {materialLots.length} lots &middot; {totalMaterialAvailable.toLocaleString()}
              </span>
            </div>

            {#if materialLoading}
              <div class="p-6 text-center text-on-surface-variant flex flex-col items-center gap-2 flex-1 justify-center">
                <span class="material-symbols-outlined animate-spin text-[24px] text-green-600">sync</span>
                <span class="text-xs">Loading materials...</span>
              </div>
            {:else if materialError}
              <div class="p-3 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 rounded-lg text-red-700 dark:text-red-300 text-xs">
                {materialError}
              </div>
            {:else if materialLots.length > 0}
              <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface flex-1">
                <table class="w-full text-left border-collapse text-xs sm:text-sm">
                  <thead>
                    <tr class="bg-surface-container-low border-b border-outline-variant text-[11px] sm:text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                      <th class="px-3 py-2.5">Lot Number</th>
                      <th class="px-3 py-2.5">Material</th>
                      <th class="px-3 py-2.5 text-left">Available Stock</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-outline-variant/50 text-on-surface">
                    {#each materialLots as mat}
                      {@const remaining = mat.remainingQuantity ?? mat.initialQuantity ?? 0}
                      {@const initial = mat.initialQuantity ?? 0}
                      {@const usagePercent = initial > 0 ? (remaining / initial) * 100 : 0}
                      <tr class="hover:bg-surface-container-low/60 transition-colors">
                        <td class="px-3 py-2.5 font-bold text-on-surface">
                          <div class="flex items-center gap-1.5">
                            <span class="material-symbols-outlined text-[15px] text-green-600 shrink-0">package_2</span>
                            <span class="truncate max-w-[110px]">{mat.lotNumber}</span>
                          </div>
                        </td>
                        <td class="px-3 py-2.5 text-on-surface truncate max-w-[120px]">{mat.materialName ?? '—'}</td>
                        <td class="px-3 py-2.5 text-left">
                          <div class="flex items-center gap-1.5">
                            <div class="w-12 bg-surface-variant/60 rounded-full h-1.5 hidden sm:block">
                              <div
                                class="bg-green-500 h-1.5 rounded-full transition-all duration-500"
                                style="width: {Math.min(usagePercent, 100)}%"
                              ></div>
                            </div>
                            <span class="font-bold text-green-600 font-mono">{Number(remaining).toLocaleString()}</span>
                            <span class="text-[10px] text-on-surface-variant font-mono">{mat.unit || ''}</span>
                          </div>
                        </td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            {:else}
              <EmptyState icon="inventory_2" title="No material lots" message="Create a material lot from the Inventory page." />
            {/if}
          </section>
        </FeatureGate>

        <!-- Widget 4: Active Production Lots -->
        <FeatureGate feature="dashboard.sections.activeLots">
          <section class="bg-surface p-4 sm:p-6 rounded-xl border border-outline-variant shadow-sm h-full flex flex-col">
            <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-3 sm:mb-4">
              <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
                <span class="material-symbols-outlined text-primary text-[22px]">reorder</span>
                Active Production Lots
              </h2>
              <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
                {overview.activeLotSummaries?.length || 0} lots
              </span>
            </div>

            {#if overview.activeLotSummaries && overview.activeLotSummaries.length > 0}
              <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface flex-1">
                <table class="w-full text-left border-collapse text-xs sm:text-sm">
                  <thead>
                    <tr class="bg-surface-container-low border-b border-outline-variant text-[11px] sm:text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                      <th class="px-3 py-2.5">Lot Number</th>
                      <th class="px-3 py-2.5 text-right">WIP</th>
                      <th class="px-3 py-2.5">Current Step</th>
                      <th class="px-3 py-2.5 text-right">Created</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-outline-variant/50 text-on-surface">
                    {#each overview.activeLotSummaries as lot}
                      <tr class="hover:bg-surface-container-low/60 transition-colors group">
                        <td class="px-3 py-2.5">
                          <a href="/inventory" class="font-bold text-primary hover:underline flex items-center gap-1">
                            <span class="material-symbols-outlined text-[15px] shrink-0">package_2</span>
                            <span class="truncate max-w-[110px]">{lot.lotNumber}</span>
                          </a>
                        </td>
                        <td class="px-3 py-2.5 text-right font-bold text-primary font-mono">
                          {lot.currentWipQuantity.toLocaleString()}
                        </td>
                        <td class="px-3 py-2.5">
                          <span class="px-1.5 py-0.5 rounded text-[10px] font-bold font-mono bg-primary-container text-on-primary-container truncate inline-block max-w-[110px]">
                            {lot.currentStepName}
                          </span>
                        </td>
                        <td class="px-3 py-2.5 text-right text-xs text-on-surface-variant font-mono">
                          {new Date(lot.createdAtUtc).toLocaleDateString(undefined, { month: 'numeric', day: 'numeric' })}
                        </td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            {:else}
              <EmptyState icon="inventory_2" title="No active lots" message="Create a new lot from the Inventory page." />
            {/if}
          </section>
        </FeatureGate>
      </div>
    {/if}
  {/if}
</main>
