<script>
  import { onMount } from 'svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

  import { getClients, createClient, getProductionLots, getLotJourney, getManufacturingSteps } from '$lib/api.js';

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
      const [clientData, rawLots, rawSteps] = await Promise.all([
        getClients(),
        getProductionLots(),
        getManufacturingSteps()
      ]);

      const baseClients = clientData || [];
      const lots = rawLots || [];
      const steps = rawSteps || [];
      const clientTrackingMode = $tenantConfigStore.clientMode; // 'lot' | 'step' | 'none'

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

            if (activeSteps.length === 0) return;

            // Non-terminating active WIP sum for this lot
            const activeNonTerminatingSteps = activeSteps.filter((/** @type {any} */ b) => {
              const stepObj = steps.find((s) => s.id === b.stepId || s.code === b.stepCode);
              return !stepObj?.isTerminating;
            });
            const lotActiveWip = activeNonTerminatingSteps.reduce((/** @type {number} */ acc, /** @type {any} */ b) => acc + b.quantity, 0);

            const lotClientId = lot.clientId || journey.clientId;

            if (clientTrackingMode === 'lot') {
              // ── Strategy A: Per Production Lot ──
              // In lot mode, the lot's active WIP belongs directly to the lot's client
              if (lotClientId && lot.status === 'Active' && lotActiveWip > 0) {
                const stats = clientStats.get(lotClientId);
                if (stats) {
                  stats.activeLotIds.add(lot.id);
                  stats.totalWip += lotActiveWip;
                }
              }
            } else {
              // ── Strategy B: Per Step Movement / Subcontracting ──
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

                // Fallback: If no movements had clients, but the lot had an initial client
                if (!foundMovementClient && lotClientId && lot.status === 'Active' && lotActiveWip > 0) {
                  const stats = clientStats.get(lotClientId);
                  if (stats) {
                    stats.activeLotIds.add(lot.id);
                    stats.totalWip += lotActiveWip;
                  }
                }
              }
            }
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
        phoneNumber: createClientForm.phoneNumber ? createClientForm.phoneNumber.trim() : '',
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
  import { onDestroy } from 'svelte';
  import { headerStore } from '$lib/stores/headerStore.js';

  $effect(() => {
    headerStore.setHeader({
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
    });
  });

  onDestroy(() => {
    headerStore.reset();
  });
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">

  {#if loading}
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span class="text-xs sm:text-sm font-medium">Loading clients...</span>
    </div>
  {:else}
    <!-- Unified 4-Metric KPI Strip (All 4 cards in a single line on mobile & desktop) -->
    <section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden">
      <div class="grid grid-cols-4 divide-x divide-outline-variant/40 p-1.5 sm:p-3 text-center">
        <!-- 1. Total Clients -->
        <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
          <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
            <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-primary hidden min-[380px]:inline">group</span>
            <span>Clients</span>
          </div>
          <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-on-surface font-mono mt-0.5 sm:mt-1">
            {clients.length}
          </div>
        </div>

        <!-- 2. Buyers -->
        <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
          <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
            <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-blue-600 hidden min-[380px]:inline">shopping_bag</span>
            <span>Buyers</span>
          </div>
          <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-blue-700 dark:text-blue-400 font-mono mt-0.5 sm:mt-1">
            {clients.filter(c => c.clientType === 'Buyer').length}
          </div>
        </div>

        <!-- 3. Wholesalers -->
        <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
          <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
            <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-emerald-600 hidden min-[380px]:inline">storefront</span>
            <span>Wholesale</span>
          </div>
          <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-emerald-700 dark:text-emerald-400 font-mono mt-0.5 sm:mt-1">
            {clients.filter(c => c.clientType === 'Wholesaler').length}
          </div>
        </div>

        <!-- 4. Job Workers -->
        <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
          <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
            <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-amber-600 hidden min-[380px]:inline">precision_manufacturing</span>
            <span>Job Workers</span>
          </div>
          <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-amber-800 dark:text-amber-300 font-mono mt-0.5 sm:mt-1">
            {clients.filter(c => c.clientType === 'Job Worker').length}
          </div>
        </div>
      </div>
    </section>

    <!-- Search Bar -->
    <section class="bg-surface p-3.5 sm:p-4 rounded-2xl border border-outline-variant/70 shadow-sm flex items-center gap-4">
      <div class="relative flex-1">
        <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[18px]">search</span>
        <input
          type="text"
          bind:value={clientSearchQuery}
          placeholder="Search clients by name, phone, or client type..."
          class="w-full pl-9 pr-4 py-2 bg-surface-container-low border border-outline-variant/70 rounded-xl text-xs sm:text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20 transition-all"
        />
        {#if clientSearchQuery}
          <button
            onclick={() => clientSearchQuery = ''}
            class="absolute right-3 top-2.5 text-on-surface-variant hover:text-on-surface"
          >
            <span class="material-symbols-outlined text-[18px]">close</span>
          </button>
        {/if}
      </div>
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
        {#each filteredClients as client}
          <div
            role="button"
            tabindex="0"
            onclick={() => window.location.href = `/lots?client=${client.id}`}
            onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (window.location.href = `/lots?client=${client.id}`)}
            class="bg-surface p-3 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-xs hover:shadow-sm transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
            title="View lots for {client.name}"
          >
            <!-- Card Line 1: Icon & Name | Client Type Badge | WIP Qty -->
            <div class="flex items-center justify-between gap-2 text-xs">
              <div class="flex items-center gap-1.5 min-w-0">
                <div class="font-bold text-sm text-primary flex items-center gap-1 shrink-0 font-mono">
                  <span class="material-symbols-outlined text-[16px]">badge</span>
                  <span>{client.name}</span>
                </div>
                <span class="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider shrink-0
                  {client.clientType === 'Buyer' ? 'bg-blue-100 text-blue-800 dark:bg-blue-950/60 dark:text-blue-300 border border-blue-200 dark:border-blue-800' :
                   client.clientType === 'Wholesaler' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800' :
                   client.clientType === 'Job Worker' ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-200 dark:border-amber-800' :
                   'bg-purple-100 text-purple-800 dark:bg-purple-950/60 dark:text-purple-300 border border-purple-200 dark:border-purple-800'}">
                  {client.clientType}
                </span>
              </div>

              <div class="flex items-center gap-1.5 shrink-0 font-mono">
                <span class="font-bold text-xs text-primary">
                  {client.totalWipQuantity.toLocaleString()} Pcs
                </span>
                <span class="material-symbols-outlined text-[16px] text-on-surface-variant/40 group-hover:text-primary transition-colors pointer-events-none">
                  chevron_right
                </span>
              </div>
            </div>

            <!-- Card Line 2: Active Lots Count, Phone Number, Registered Date -->
            <div class="flex flex-wrap gap-1 items-center pt-1 border-t border-dashed border-outline-variant/50 text-[10px] font-mono">
              <!-- Active Lots Badge -->
              <span class="px-2 py-0.5 rounded font-bold bg-primary-container text-on-primary-container flex items-center gap-1">
                <span class="material-symbols-outlined text-[11px]">inventory_2</span>
                {client.activeLotsCount} {client.activeLotsCount === 1 ? 'Active Lot' : 'Active Lots'}
              </span>

              <!-- Phone Number Pill -->
              {#if client.phoneNumber}
                <span class="px-2 py-0.5 rounded bg-surface-container-low text-on-surface-variant border border-outline-variant/60 flex items-center gap-0.5">
                  <span class="material-symbols-outlined text-[11px]">call</span>
                  {client.phoneNumber}
                </span>
              {/if}

              <!-- Registered Date Pill -->
              {#if client.createdAtUtc}
                <span class="px-1.5 py-0.5 rounded bg-surface-container-low text-on-surface-variant/70 border border-outline-variant/60 ml-auto">
                  {new Date(client.createdAtUtc).toLocaleDateString()}
                </span>
              {/if}
            </div>
          </div>
        {/each}
        {#if filteredClients.length === 0}
          <div class="p-6 text-center text-on-surface-variant bg-surface-container-low rounded-xl border border-dashed border-outline-variant text-xs">
            <span class="material-symbols-outlined text-[28px] text-on-surface-variant/40 mb-1 block">filter_alt_off</span>
            No clients found matching your search.
          </div>
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
      label="Phone Number (Optional)"
      type="tel"
      bind:value={createClientForm.phoneNumber}
      placeholder="e.g. +91 98765 43210"
      icon="call"
      required={false}
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
