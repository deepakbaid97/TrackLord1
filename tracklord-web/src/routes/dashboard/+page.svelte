<script>
  import { onMount } from 'svelte';
  import TopAppBar from '$lib/components/ui/TopAppBar.svelte';
  import AlertBanner from '$lib/components/ui/AlertBanner.svelte';
  import StatCard from '$lib/components/ui/StatCard.svelte';
  import ProgressBarRow from '$lib/components/ui/ProgressBarRow.svelte';
  import ClientTypeBadge from '$lib/components/ui/ClientTypeBadge.svelte';

  import { getDashboardOverview, getMaterialLots } from '$lib/api.js';

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

  onMount(async () => {
    await refreshDashboard();
  });

  async function refreshDashboard() {
    loading = true;
    errorMessage = '';
    try {
      overview = await getDashboardOverview();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load dashboard data';
    } finally {
      loading = false;
    }

    materialLoading = true;
    materialError = '';
    try {
      const fetchedLots = await getMaterialLots();
      materialLots = fetchedLots;
      totalMaterialAvailable = fetchedLots.reduce((sum, lot) => {
        const qty = lot.remainingQuantity ?? lot.initialQuantity ?? 0;
        return sum + Number(qty);
      }, 0);
    } catch (/** @type {any} */ err) {
      materialError = err.message || 'Failed to load material data';
    } finally {
      materialLoading = false;
    }
  }

  let maxStepWip = $derived(
    overview?.stepSummaries?.length > 0
      ? Math.max(...overview.stepSummaries.map((/** @type {any} */ s) => s.totalWipQuantity), 1)
      : 1
  );

  /** @type {Record<string, string>} */
  const stepIconMap = {
    'CUT': 'content_cut',
    'STITCH': 'webhook',
    'WASH': 'local_laundry_service',
    'FIN': 'iron',
    'PACK': 'inventory_2'
  };

  /** @type {Record<string, string>} */
  const stepColorMap = {
    'CUT': 'bg-blue-500',
    'STITCH': 'bg-violet-500',
    'WASH': 'bg-cyan-500',
    'FIN': 'bg-amber-500',
    'PACK': 'bg-emerald-500'
  };
</script>

<!-- TopAppBar -->
<TopAppBar title="TRACKLORD ERP" subtitle="Dashboard Overview" icon="dashboard">
  <button
    onclick={() => refreshDashboard()}
    class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
  >
    <span class="material-symbols-outlined text-[16px]">refresh</span>
    Refresh
  </button>
</TopAppBar>

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-10">

  <AlertBanner type="error" message={errorMessage} ondismiss={() => errorMessage = ''} />

  {#if loading}
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span>Loading dashboard metrics...</span>
    </div>
  {:else if overview}

    <!-- WIDGET 1: KPI Summary Cards -->
    <section class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <StatCard
        label="Active Lots"
        value={overview.totalActiveLots}
        icon="inventory_2"
        variant="primary"
        href="/lots"
        title="View all lots in directory"
      />
      <StatCard
        label="Total WIP"
        value={overview.totalActiveQuantity.toLocaleString()}
        icon="stacks"
        variant="info"
        subvalue="Pieces"
      />
    </section>

    <!-- Two Column Grid for Step WIP + Client Distribution -->
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

      <!-- WIDGET 2: WIP by Manufacturing Step -->
      <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
        <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-5">
          <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
            <span class="material-symbols-outlined text-primary text-[22px]">factory</span>
            WIP by Step
          </h2>
          <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
            {overview.stepSummaries?.length || 0} steps
          </span>
        </div>

        {#if overview.stepSummaries && overview.stepSummaries.length > 0}
          <div class="space-y-4">
            {#each overview.stepSummaries as step}
              <ProgressBarRow
                label={step.stepName}
                value={step.totalWipQuantity}
                total={maxStepWip}
                unit="Pcs"
                subtext="{step.stepCode} &middot; {step.activeLotsCount} lots"
                colorClass={stepColorMap[step.stepCode] || 'bg-primary'}
                icon={stepIconMap[step.stepCode] || 'work'}
                href="/lots?step={step.stepId}"
                title="View {step.stepName} lots in directory"
              />
            {/each}
          </div>
        {:else}
          <div class="p-8 text-center text-on-surface-variant text-sm bg-surface-container-low rounded-lg border border-dashed border-outline-variant">
            No manufacturing steps configured.
          </div>
        {/if}
      </section>

      <!-- WIDGET 3: Client Distribution -->
      <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
        <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-5">
          <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
            <span class="material-symbols-outlined text-primary text-[22px]">groups</span>
            Client Distribution
          </h2>
          <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
            {overview.clientSummaries?.length || 0} active
          </span>
        </div>

        {#if overview.clientSummaries && overview.clientSummaries.length > 0}
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                  <th class="pb-2 pr-4">Client</th>
                  <th class="pb-2 pr-4">Type</th>
                  <th class="pb-2 pr-4 text-right">Lots</th>
                  <th class="pb-2 text-right">Qty Moved</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-outline-variant/50">
                {#each overview.clientSummaries as client}
                  <tr
                    onclick={() => window.location.href = `/lots?client=${client.clientId}`}
                    class="hover:bg-surface-container-low/70 transition-colors cursor-pointer group"
                    title="View lots associated with {client.clientName}"
                  >
                    <td class="py-3 pr-4">
                      <div class="flex items-center gap-2">
                        <span class="material-symbols-outlined text-[18px] text-primary/70 group-hover:text-primary transition-colors">badge</span>
                        <span class="font-bold text-on-surface group-hover:text-primary transition-colors">{client.clientName}</span>
                      </div>
                    </td>
                    <td class="py-3 pr-4">
                      <ClientTypeBadge clientType={client.clientType} />
                    </td>
                    <td class="py-3 pr-4 text-right font-mono text-on-surface-variant">{client.totalLotsAssociated}</td>
                    <td class="py-3 text-right font-bold text-primary font-mono">{client.totalQuantityMoved.toLocaleString()} Pcs</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </div>
        {:else}
          <div class="p-8 text-center text-on-surface-variant text-sm bg-surface-container-low rounded-lg border border-dashed border-outline-variant">
            <span class="material-symbols-outlined text-[32px] text-on-surface-variant/50 mb-2 block">groups</span>
            No client activity recorded yet. Movements with assigned clients will appear here.
          </div>
        {/if}
      </section>
    </div>

    <!-- WIDGET: Material Availability -->
    <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
      <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-5">
        <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-green-600 text-[22px]">inventory_2</span>
          Material Availability
        </h2>
        <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
          {materialLots.length} lots &middot; {totalMaterialAvailable.toLocaleString()} total
        </span>
      </div>

      {#if materialLoading}
        <div class="p-8 text-center text-on-surface-variant flex flex-col items-center gap-3">
          <span class="material-symbols-outlined animate-spin text-[28px] text-green-600">sync</span>
          <span class="text-sm">Loading material data...</span>
        </div>
      {:else if materialError}
        <div class="p-4 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 rounded-lg text-red-700 dark:text-red-300 text-sm">
          {materialError}
        </div>
      {:else if materialLots.length > 0}
        <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface">
          <table class="w-full text-left border-collapse text-sm">
            <thead>
              <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                <th class="px-4 py-3">Lot Number</th>
                <th class="px-4 py-3">Material Name</th>
                <th class="px-4 py-3 text-right">Initial Qty</th>
                <th class="px-4 py-3 text-right">Remaining Qty</th>
                <th class="px-4 py-3">Unit</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-outline-variant/50 text-on-surface">
              {#each materialLots as mat}
                {@const remaining = mat.remainingQuantity ?? mat.initialQuantity ?? 0}
                {@const initial = mat.initialQuantity ?? 0}
                {@const usagePercent = initial > 0 ? (remaining / initial) * 100 : 0}
                <tr class="hover:bg-surface-container-low/60 transition-colors">
                  <td class="px-4 py-3">
                    <span class="font-bold text-on-surface flex items-center gap-1.5">
                      <span class="material-symbols-outlined text-[16px] text-green-600">package_2</span>
                      {mat.lotNumber}
                    </span>
                  </td>
                  <td class="px-4 py-3 text-on-surface">{mat.materialName ?? '—'}</td>
                  <td class="px-4 py-3 text-right font-mono text-on-surface-variant">{Number(initial).toLocaleString()}</td>
                  <td class="px-4 py-3 text-right">
                    <div class="flex items-center justify-end gap-2">
                      <div class="w-16 bg-surface-variant/60 rounded-full h-1.5 hidden sm:block">
                        <div
                          class="bg-green-500 h-1.5 rounded-full transition-all duration-500"
                          style="width: {Math.min(usagePercent, 100)}%"
                        ></div>
                      </div>
                      <span class="font-bold text-green-600 font-mono">{Number(remaining).toLocaleString()}</span>
                    </div>
                  </td>
                  <td class="px-4 py-3 font-mono text-on-surface-variant">{mat.unit}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <div class="p-8 text-center text-on-surface-variant text-sm bg-surface-container-low rounded-lg border border-dashed border-outline-variant">
          <span class="material-symbols-outlined text-[32px] text-on-surface-variant/50 mb-2 block">inventory_2</span>
          No material lots found. Create a new material lot from the Inventory page.
        </div>
      {/if}
    </section>

    <!-- WIDGET 4: Active Production Lots Table -->
    <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
      <div class="flex items-center justify-between border-b border-outline-variant pb-3 mb-5">
        <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-primary text-[22px]">reorder</span>
          Active Production Lots
        </h2>
        <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2 py-1 rounded">
          {overview.activeLotSummaries?.length || 0} lots
        </span>
      </div>

      {#if overview.activeLotSummaries && overview.activeLotSummaries.length > 0}
        <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface">
          <table class="w-full text-left border-collapse text-sm">
            <thead>
              <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                <th class="px-4 py-3">Lot Number</th>
                <th class="px-4 py-3 text-right">Initial Qty</th>
                <th class="px-4 py-3 text-right">Current WIP</th>
                <th class="px-4 py-3">Primary Step</th>
                <th class="px-4 py-3">Created</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-outline-variant/50 text-on-surface">
              {#each overview.activeLotSummaries as lot}
                {@const utilizationPercent = lot.initialQuantity > 0 ? (lot.currentWipQuantity / lot.initialQuantity) * 100 : 0}
                <tr class="hover:bg-surface-container-low/60 transition-colors group">
                  <td class="px-4 py-3">
                    <a href="/inventory" class="font-bold text-primary hover:underline flex items-center gap-1.5">
                      <span class="material-symbols-outlined text-[16px]">package_2</span>
                      {lot.lotNumber}
                    </a>
                  </td>
                  <td class="px-4 py-3 text-right font-mono text-on-surface-variant">{lot.initialQuantity.toLocaleString()} Pcs</td>
                  <td class="px-4 py-3 text-right">
                    <div class="flex items-center justify-end gap-2">
                      <div class="w-16 bg-surface-variant/60 rounded-full h-1.5 hidden sm:block">
                        <div
                          class="bg-primary h-1.5 rounded-full transition-all duration-500"
                          style="width: {Math.min(utilizationPercent, 100)}%"
                        ></div>
                      </div>
                      <span class="font-bold text-primary font-mono">{lot.currentWipQuantity.toLocaleString()}</span>
                    </div>
                  </td>
                  <td class="px-4 py-3">
                    <span class="px-2 py-0.5 rounded text-[11px] font-bold font-mono bg-primary-container text-on-primary-container">
                      {lot.currentStepName}
                    </span>
                  </td>
                  <td class="px-4 py-3 text-xs text-on-surface-variant font-mono">{new Date(lot.createdAtUtc).toLocaleDateString()}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <div class="p-8 text-center text-on-surface-variant text-sm bg-surface-container-low rounded-lg border border-dashed border-outline-variant">
          <span class="material-symbols-outlined text-[32px] text-on-surface-variant/50 mb-2 block">inventory_2</span>
          No active production lots found. Create a new lot from the Inventory page.
        </div>
      {/if}
    </section>

  {/if}
</main>
