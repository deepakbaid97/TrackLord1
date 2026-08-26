<script>
  import { onMount } from 'svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import SearchBox from '$lib/components/ui/SearchBox.svelte';
  import KpiStrip from '$lib/components/ui/KpiStrip.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import ClientCard from '$lib/components/features/clients/ClientCard.svelte';
  import CreateClientModal from '$lib/components/features/clients/CreateClientModal.svelte';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';
  import { getClients, getProductionLots, getBatchLotJourneys, getManufacturingSteps } from '$lib/api.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');
  let clientSearchQuery = $state('');

  /** @type {Array<any>} */
  let clients = $state([]);
  let isCreateClientModalOpen = $state(false);

  usePageHeader(() => ({
    title: 'Clients Directory',
    subtitle: 'Buyers & Partners',
    icon: 'groups',
    actions: [
      {
        label: 'Add Client',
        icon: 'person_add',
        variant: 'primary',
        onclick: () => { isCreateClientModalOpen = true; }
      },
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: 'outline',
        onclick: () => { refreshClients(); }
      }
    ]
  }));

  onMount(async () => {
    await refreshClients();
  });

  async function refreshClients() {
    loading = true;
    errorMessage = '';
    try {
      const [clientData, rawLots, rawSteps] = await Promise.all([
        getClients(),
        getProductionLots(),
        getManufacturingSteps()
      ]);

      const baseClients = clientData || [];
      const lots = rawLots || [];
      const steps = rawSteps || [];
      const clientTrackingMode = $tenantConfigStore.clientMode;
      const lotIds = lots.map(l => l.id);

      /** @type {Map<string, { activeLotIds: Set<string>, totalWip: number }>} */
      const clientStats = new Map();
      baseClients.forEach((/** @type {any} */ c) => {
        clientStats.set(c.id, { activeLotIds: new Set(), totalWip: 0 });
      });

      /** @type {Record<string, any>} */
      let journeysMap = {};
      try {
        journeysMap = await getBatchLotJourneys(lotIds) || {};
      } catch {
        // Fallback gracefully
      }

      lots.forEach((/** @type {any} */ lot) => {
        const journey = (lot.id ? (journeysMap[lot.id] || journeysMap[lot.id.toLowerCase()] || journeysMap[lot.id.toUpperCase()]) : null) || null;
        if (!journey) return;

        const activeSteps = journey.activeBalances
          ? journey.activeBalances.filter((/** @type {any} */ b) => b.quantity > 0)
          : [];

        if (activeSteps.length === 0) return;

        const activeNonTerminatingSteps = activeSteps.filter((/** @type {any} */ b) => {
          const stepObj = steps.find((s) => s.id === b.stepId || s.code === b.stepCode);
          return !stepObj?.isTerminating;
        });
        const lotActiveWip = activeNonTerminatingSteps.reduce((/** @type {number} */ acc, /** @type {any} */ b) => acc + b.quantity, 0);
        const lotClientId = lot.clientId || journey.clientId;

        if (clientTrackingMode === 'lot') {
          if (lotClientId && lot.status === 'Active' && lotActiveWip > 0) {
            const stats = clientStats.get(lotClientId);
            if (stats) {
              stats.activeLotIds.add(lot.id);
              stats.totalWip += lotActiveWip;
            }
          }
        } else {
          if (journey.movementHistory) {
            const activeStepNames = new Set(activeSteps.map((/** @type {any} */ s) => s.stepName));
            let foundMovementClient = false;

            journey.movementHistory.forEach((/** @type {any} */ m) => {
              if (!m.isReversed && m.clientId && m.toStepName && activeStepNames.has(m.toStepName)) {
                const stats = clientStats.get(m.clientId);
                if (stats) {
                  stats.activeLotIds.add(lot.id);
                  foundMovementClient = true;
                  const stepObj = activeSteps.find((/** @type {any} */ s) => s.stepName === m.toStepName);
                  if (stepObj) {
                    stats.totalWip += stepObj.quantity;
                  }
                }
              }
            });

            if (!foundMovementClient && lotClientId && lot.status === 'Active' && lotActiveWip > 0) {
              const stats = clientStats.get(lotClientId);
              if (stats) {
                stats.activeLotIds.add(lot.id);
                stats.totalWip += lotActiveWip;
              }
            }
          }
        }
      });

      clients = baseClients.map((/** @type {any} */ c) => {
        const stats = clientStats.get(c.id) || { activeLotIds: new Set(), totalWip: 0 };
        return {
          ...c,
          activeLotsCount: stats.activeLotIds.size,
          totalWipQuantity: stats.totalWip
        };
      });
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to fetch clients';
    } finally {
      loading = false;
    }
  }

  let filteredClients = $derived(
    clients.filter(c =>
      !clientSearchQuery ||
      c.name.toLowerCase().includes(clientSearchQuery.toLowerCase()) ||
      (c.phoneNumber && c.phoneNumber.includes(clientSearchQuery)) ||
      c.clientType.toLowerCase().includes(clientSearchQuery.toLowerCase())
    )
  );

  let kpiItems = $derived([
    { label: 'Clients', value: clients.length, icon: 'group', color: 'text-primary' },
    { label: 'Buyers', value: clients.filter(c => c.clientType === 'Buyer').length, icon: 'shopping_bag', color: 'text-blue-600 dark:text-blue-400' },
    { label: 'Wholesale', value: clients.filter(c => c.clientType === 'Wholesaler').length, icon: 'storefront', color: 'text-emerald-600 dark:text-emerald-400' },
    { label: 'Job Workers', value: clients.filter(c => c.clientType === 'Job Worker').length, icon: 'precision_manufacturing', color: 'text-amber-600 dark:text-amber-300' }
  ]);
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  {#if loading}
    <LoadingSpinner message="Loading clients..." />
  {:else}
    <!-- Unified 4-Metric KPI Strip -->
    <KpiStrip items={kpiItems} />

    <!-- Search Bar -->
    <section class="bg-surface p-3.5 sm:p-4 rounded-2xl border border-outline-variant/70 shadow-sm">
      <SearchBox bind:value={clientSearchQuery} placeholder="Search clients by name, phone, or client type..." />
    </section>

    <!-- Clients List Table & Cards -->
    <section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden p-3.5 sm:p-5 space-y-3.5">
      <div class="flex items-center justify-between border-b border-outline-variant/60 pb-2.5">
        <h2 class="text-sm sm:text-base font-bold flex items-center gap-1.5 text-on-surface">
          <span>Client Accounts</span>
          <span class="text-xs font-mono font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full ml-0.5">
            {filteredClients.length}
          </span>
        </h2>
        <span class="text-[10px] sm:text-xs text-on-surface-variant font-mono">
          Showing {filteredClients.length} of {clients.length}
        </span>
      </div>

      <!-- Mobile View: Card Stack Column (below md) -->
      <div class="block md:hidden space-y-2">
        {#each filteredClients as client (client.id)}
          <ClientCard {client} />
        {/each}
        {#if filteredClients.length === 0}
          <EmptyState icon="filter_alt_off" title="No clients found" message="Try adjusting your search criteria." />
        {/if}
      </div>

      <!-- Desktop View: Table (md and up) -->
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/70">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant/70 text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-6 py-3">Client Name</th>
              <th class="px-6 py-3">Phone Number</th>
              <th class="px-6 py-3">Client Type</th>
              <th class="px-6 py-3 text-right">Active Lots</th>
              <th class="px-6 py-3 text-right">Total Active Qty</th>
              <th class="px-6 py-3">Registered Date</th>
              <th class="px-6 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#each filteredClients as client (client.id)}
              <tr
                onclick={() => window.location.href = `/lots?client=${client.id}`}
                class="hover:bg-surface-container-low/70 transition-colors cursor-pointer group"
                title="View active lots for {client.name}"
              >
                <td class="px-6 py-4 font-bold text-primary flex items-center gap-2 group-hover:underline">
                  <span class="material-symbols-outlined text-[20px] text-primary/70">badge</span>
                  {client.name}
                </td>
                <td class="px-6 py-4 font-mono text-on-surface text-xs">
                  <span class="flex items-center gap-1.5">
                    <span class="material-symbols-outlined text-[16px] text-on-surface-variant">call</span>
                    {client.phoneNumber || 'N/A'}
                  </span>
                </td>
                <td class="px-6 py-4">
                  <span class="px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider
                    {client.clientType === 'Buyer' ? 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300' :
                     client.clientType === 'Wholesaler' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300' :
                     client.clientType === 'Job Worker' ? 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300' :
                     'bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300'}">
                    {client.clientType}
                  </span>
                </td>
                <td class="px-6 py-4 text-right font-mono font-bold text-on-surface">
                  {client.activeLotsCount}
                </td>
                <td class="px-6 py-4 text-right font-mono font-bold text-primary">
                  {(client.totalWipQuantity || 0).toLocaleString()} Pcs
                </td>
                <td class="px-6 py-4 text-xs text-on-surface-variant font-mono">
                  {new Date(client.createdAtUtc).toLocaleDateString()}
                </td>
                <td class="px-6 py-4 text-right">
                  <a
                    href="/lots?client={client.id}"
                    onclick={(e) => e.stopPropagation()}
                    class="px-3 py-1.5 text-xs bg-primary/10 text-primary hover:bg-primary/20 rounded-lg font-bold transition-colors inline-flex items-center gap-1"
                  >
                    <span class="material-symbols-outlined text-[14px]">reorder</span>
                    View Lots
                  </a>
                </td>
              </tr>
            {/each}
            {#if clients.length === 0}
              <tr>
                <td colspan="7" class="p-8 text-center text-on-surface-variant text-sm">
                  No clients registered yet. Click "Add Client" above to create your first client.
                </td>
              </tr>
            {/if}
          </tbody>
        </table>
      </div>
    </section>
  {/if}
</main>

<CreateClientModal
  bind:isOpen={isCreateClientModalOpen}
  onSuccess={(newClient) => {
    successMessage = `Client '${newClient.name}' created successfully!`;
    refreshClients();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to create client';
  }}
/>
