<script>
  import { onMount } from 'svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';

  import { getClients, createClient, getProductionLots, getLotJourney } from '$lib/api.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');
  let clientSearchQuery = $state('');

  /** @type {Array<any>} */
  let clients = $state([]);

  let isCreateClientModalOpen = $state(false);
  let createClientForm = $state({
    name: '',
    phoneNumber: '',
    clientType: 'Buyer'
  });

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

      // Map client stats: active lots count and active WIP quantity
      // Key: clientId, Value: { activeLotIds: Set, totalWip: number }
      /** @type {Map<string, { activeLotIds: Set<string>, totalWip: number }>} */
      const clientStats = new Map();
      baseClients.forEach((/** @type {any} */ c) => {
        clientStats.set(c.id, { activeLotIds: new Set(), totalWip: 0 });
      });

      // Fetch journey for each lot to trace active WIP step balances and associated clients
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

            // Trace movements into active WIP steps to calculate client active lots & WIP quantity
            journey.movementHistory.forEach((/** @type {any} */ m) => {
              if (!m.isReversed && m.clientId && m.toStepName && activeStepNames.has(m.toStepName)) {
                const stats = clientStats.get(m.clientId);
                if (stats) {
                  stats.activeLotIds.add(lot.id);
                  // Find step quantity
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

  /** @param {SubmitEvent} e */
  async function handleCreateClientSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        name: createClientForm.name.trim(),
        phoneNumber: createClientForm.phoneNumber.trim(),
        clientType: createClientForm.clientType
      };

      await createClient(payload);
      successMessage = `Client '${createClientForm.name}' created successfully!`;
      isCreateClientModalOpen = false;
      createClientForm = { name: '', phoneNumber: '', clientType: 'Buyer' };
      await refreshClients();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to create client';
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
</script>

<!-- TopAppBar -->
<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">groups</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD ERP</div>
      <div class="text-xs text-on-surface-variant">Client Directory</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
    <button
      onclick={() => refreshClients()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh
    </button>
  </div>
</header>

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-20">

  <!-- Notification Banners -->
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
      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-primary/10 rounded-lg text-primary">
          <span class="material-symbols-outlined text-[24px]">group</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Total Clients</div>
          <div class="text-2xl font-bold text-on-surface">{clients.length}</div>
        </div>
      </div>

      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-blue-500/10 rounded-lg text-blue-600">
          <span class="material-symbols-outlined text-[24px]">shopping_bag</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Buyers</div>
          <div class="text-2xl font-bold text-on-surface">{clients.filter(c => c.clientType === 'Buyer').length}</div>
        </div>
      </div>

      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-emerald-500/10 rounded-lg text-emerald-600">
          <span class="material-symbols-outlined text-[24px]">storefront</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Wholesalers</div>
          <div class="text-2xl font-bold text-on-surface">{clients.filter(c => c.clientType === 'Wholesaler').length}</div>
        </div>
      </div>

      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-amber-500/10 rounded-lg text-amber-600">
          <span class="material-symbols-outlined text-[24px]">precision_manufacturing</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Job Workers</div>
          <div class="text-2xl font-bold text-on-surface">{clients.filter(c => c.clientType === 'Job Worker').length}</div>
        </div>
      </div>
    </div>

    <!-- Search Bar -->
    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="relative flex-1">
        <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[20px]">search</span>
        <input
          type="text"
          bind:value={clientSearchQuery}
          placeholder="Search clients by name, phone, or type..."
          class="w-full pl-10 pr-4 py-2 bg-surface-container-low border border-outline-variant rounded-lg text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
        />
      </div>
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
<Modal bind:isOpen={isCreateClientModalOpen} title="Register New Client">
  <form onsubmit={handleCreateClientSubmit} class="space-y-4">
    <Input
      label="Client / Company Name"
      bind:value={createClientForm.name}
      placeholder="e.g. Apex Textiles Ltd."
      icon="badge"
      required={true}
    />

    <Input
      label="Phone Number"
      type="tel"
      bind:value={createClientForm.phoneNumber}
      placeholder="e.g. +91 98765 43210"
      icon="call"
      required={true}
    />

    <Select
      label="Client Type"
      bind:value={createClientForm.clientType}
      options={[
        { label: 'Buyer', value: 'Buyer' },
        { label: 'Wholesaler', value: 'Wholesaler' },
        { label: 'Job Worker', value: 'Job Worker' },
        { label: 'Customer', value: 'Customer' },
        { label: 'Retailer', value: 'Retailer' },
        { label: 'Supplier', value: 'Supplier' },
        { label: 'Self', value: 'Self'}
      ]}
      icon="category"
      required={true}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isCreateClientModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save">
        Save Client
      </Button>
    </div>
  </form>
</Modal>
