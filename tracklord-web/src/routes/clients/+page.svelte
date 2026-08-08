<script>
  import { onMount } from 'svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import TopAppBar from '$lib/components/ui/TopAppBar.svelte';
  import AlertBanner from '$lib/components/ui/AlertBanner.svelte';
  import StatCard from '$lib/components/ui/StatCard.svelte';
  import SearchBar from '$lib/components/ui/SearchBar.svelte';
  import ClientTypeBadge from '$lib/components/ui/ClientTypeBadge.svelte';
  import RegisterClientModal from '$lib/components/domain/RegisterClientModal.svelte';

  import { getClients, getProductionLots, getLotJourney } from '$lib/api.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');
  let clientSearchQuery = $state('');

  /** @type {Array<any>} */
  let clients = $state([]);
  let isCreateClientModalOpen = $state(false);

  onMount(async () => {
    await refreshClients();
  });

  async function refreshClients() {
    loading = true;
    errorMessage = '';
    try {
      const [clientData, rawLots] = await Promise.all([
        getClients(),
        getProductionLots()
      ]);

      const baseClients = clientData || [];
      const lots = rawLots || [];

      /** @type {Map<string, { activeLotIds: Set<string>, totalWip: number }>} */
      const clientStats = new Map();
      baseClients.forEach((/** @type {any} */ c) => {
        clientStats.set(c.id, { activeLotIds: new Set(), totalWip: 0 });
      });

      await Promise.all(
        lots.map(async (/** @type {any} */ lot) => {
          try {
            const journey = await getLotJourney(lot.id);
            if (!journey) return;

            const activeSteps = journey.activeBalances
              ? journey.activeBalances.filter((/** @type {any} */ b) => b.quantity > 0)
              : [];

            if (activeSteps.length === 0 || !journey.movementHistory) return;

            const activeStepNames = new Set(activeSteps.map((/** @type {any} */ s) => s.stepName));

            journey.movementHistory.forEach((/** @type {any} */ m) => {
              if (!m.isReversed && m.clientId && m.toStepName && activeStepNames.has(m.toStepName)) {
                const stats = clientStats.get(m.clientId);
                if (stats) {
                  stats.activeLotIds.add(lot.id);
                  const stepObj = activeSteps.find((/** @type {any} */ s) => s.stepName === m.toStepName);
                  if (stepObj) {
                    stats.totalWip += stepObj.quantity;
                  }
                }
              }
            });
          } catch {
            // Ignore single lot fetch error
          }
        })
      );

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

  /** @param {any} newClient */
  function handleClientCreated(newClient) {
    successMessage = `Client '${newClient.name}' created successfully!`;
    refreshClients();
  }

  let filteredClients = $derived(
    clients.filter(c =>
      !clientSearchQuery ||
      c.name.toLowerCase().includes(clientSearchQuery.toLowerCase()) ||
      (c.phoneNumber && c.phoneNumber.includes(clientSearchQuery)) ||
      c.clientType.toLowerCase().includes(clientSearchQuery.toLowerCase())
    )
  );
</script>

<!-- TopAppBar -->
<TopAppBar title="TRACKLORD ERP" subtitle="Client Directory" icon="groups">
  <button
    onclick={() => refreshClients()}
    class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
  >
    <span class="material-symbols-outlined text-[16px]">refresh</span>
    Refresh
  </button>
</TopAppBar>

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-10">

  <!-- Notification Banners -->
  <AlertBanner type="error" message={errorMessage} ondismiss={() => errorMessage = ''} />
  <AlertBanner type="success" message={successMessage} ondismiss={() => successMessage = ''} />

  {#if loading}
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span>Loading clients...</span>
    </div>
  {:else}
    <!-- Header & Add Button -->
    <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
      <div>
        <h2 class="font-headline-md text-headline-md text-primary font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-[28px]">groups</span>
          Client Directory & Customer Registry
        </h2>
        <p class="text-sm text-on-surface-variant mt-1">
          Manage buyers, wholesalers, suppliers, and job workers for manufacturing movements.
        </p>
      </div>
      <Button variant="primary" icon="person_add" onclick={() => isCreateClientModalOpen = true}>
        Add New Client
      </Button>
    </div>

    <!-- Client Statistics Summary -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
      <StatCard label="Total Clients" value={clients.length} icon="group" variant="primary" />
      <StatCard label="Buyers" value={clients.filter(c => c.clientType === 'Buyer').length} icon="shopping_bag" variant="info" />
      <StatCard label="Wholesalers" value={clients.filter(c => c.clientType === 'Wholesaler').length} icon="storefront" variant="success" />
      <StatCard label="Job Workers" value={clients.filter(c => c.clientType === 'Job Worker').length} icon="precision_manufacturing" variant="warning" />
    </div>

    <!-- Search Bar -->
    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <SearchBar bind:value={clientSearchQuery} placeholder="Search clients by name, phone, or type..." />
    </div>

    <!-- Clients List Table -->
    <div class="bg-surface rounded-xl border border-outline-variant shadow-sm overflow-hidden">
      <div class="w-full overflow-x-auto">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-6 py-3">Client Name</th>
              <th class="px-6 py-3">Phone Number</th>
              <th class="px-6 py-3">Client Type</th>
              <th class="px-6 py-3 text-right">Active Lots</th>
              <th class="px-6 py-3 text-right">Total Active Qty</th>
              <th class="px-6 py-3">Registered Date</th>
              <th class="px-6 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant">
            {#each filteredClients as client}
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
                  <ClientTypeBadge clientType={client.clientType} />
                </td>
                <td class="px-6 py-4 text-right font-mono font-bold text-on-surface">
                  {client.activeLotsCount}
                </td>
                <td class="px-6 py-4 text-right font-mono font-bold text-primary">
                  {client.totalWipQuantity.toLocaleString()} Pcs
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
                  No clients registered yet. Click "Add New Client" above to create your first client.
                </td>
              </tr>
            {/if}
          </tbody>
        </table>
      </div>
    </div>
  {/if}
</main>

<!-- Create Client Modal -->
<RegisterClientModal
  bind:isOpen={isCreateClientModalOpen}
  onsuccess={handleClientCreated}
  onerror={(err) => errorMessage = err}
/>
