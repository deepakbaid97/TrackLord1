<script>
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import Button from '$lib/components/ui/Button.svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';

  // Feature Components
  import LotPickerModal from '$lib/components/features/lots/LotPickerModal.svelte';
  import CreateLotModal from '$lib/components/features/lots/CreateLotModal.svelte';
  import MoveLotModal from '$lib/components/features/lots/MoveLotModal.svelte';
  import RejectItemsModal from '$lib/components/features/lots/RejectItemsModal.svelte';
  import ReverseMovementModal from '$lib/components/features/lots/ReverseMovementModal.svelte';
  import CreateMaterialModal from '$lib/components/features/materials/CreateMaterialModal.svelte';
  import ConsumeMaterialModal from '$lib/components/features/materials/ConsumeMaterialModal.svelte';
  import EditConsumptionModal from '$lib/components/features/materials/EditConsumptionModal.svelte';
  import CreateClientModal from '$lib/components/features/clients/CreateClientModal.svelte';
  import WipDistribution from '$lib/components/features/inventory/WipDistribution.svelte';
  import MovementLedger from '$lib/components/features/inventory/MovementLedger.svelte';
  import MaterialTraceability from '$lib/components/features/inventory/MaterialTraceability.svelte';

  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import { STEP_ICON_MAP } from '$lib/services/lotService.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';

  import {
    getManufacturingSteps,
    getProductionLots,
    getMaterialLots,
    getLotJourney,
    getMaterialTraceability,
    getClients,
    deleteProductionLot
  } from '$lib/api.js';

  // App State
  let activeTab = $state('lots');
  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  // Collections
  /** @type {Array<any>} */
  let manufacturingSteps = $state([]);
  /** @type {Array<any>} */
  let productionLots = $state([]);
  /** @type {Array<any>} */
  let materialLots = $state([]);
  /** @type {Array<any>} */
  let clients = $state([]);
  let selectedLotId = $state('');

  // Selected Lot Details
  /** @type {any} */
  let currentLotJourney = $state(null);
  /** @type {any} */
  let currentMaterialTraceability = $state(null);

  // Modals Visibility State
  let isLotPickerModalOpen = $state(false);
  let isCreateLotModalOpen = $state(false);
  let isMoveModalOpen = $state(false);
  let isCreateMaterialModalOpen = $state(false);
  let isConsumeMaterialModalOpen = $state(false);
  let isEditConsumptionModalOpen = $state(false);
  let isRejectModalOpen = $state(false);
  let isReverseModalOpen = $state(false);
  let isCreateClientModalOpen = $state(false);

  // Modal Context State
  let moveInitialStepId = $state('');
  let moveInitialQuantity = $state(0);
  /** @type {any} */
  let selectedConsumptionForEdit = $state(null);
  /** @type {any} */
  let selectedMovementForReverse = $state(null);

  let selectedLotObj = $derived(productionLots.find(l => l.id === selectedLotId));

  usePageHeader(() => ({
    title: 'Lot Inventory',
    subtitle: selectedLotObj ? `${selectedLotObj.lotNumber} • ${selectedLotObj.clientName || 'Live WIP'}` : 'Live Production WIP',
    icon: 'inventory_2',
    actions: [
      {
        label: 'Actions',
        icon: 'bolt',
        variant: /** @type {const} */ ('primary'),
        dropdownItems: [
          {
            label: 'Move Qty',
            icon: 'move_item',
            onclick: () => {
              if (!selectedLotId) {
                errorMessage = 'Please select a production lot first.';
                isLotPickerModalOpen = true;
                return;
              }
              openMoveModal('');
            }
          },
          {
            label: 'New Lot',
            icon: 'add',
            onclick: () => { isCreateLotModalOpen = true; }
          },
          {
            label: 'Material',
            icon: 'inventory',
            onclick: () => { isCreateMaterialModalOpen = true; }
          },
          {
            label: 'Consume',
            icon: 'local_mall',
            onclick: () => {
              if (!selectedLotId) {
                errorMessage = 'Please select a production lot first.';
                isLotPickerModalOpen = true;
                return;
              }
              isConsumeMaterialModalOpen = true;
            }
          },
          {
            label: 'Rejection',
            icon: 'report_problem',
            danger: true,
            onclick: () => {
              if (!selectedLotId) {
                errorMessage = 'Please select a production lot first.';
                isLotPickerModalOpen = true;
                return;
              }
              isRejectModalOpen = true;
            }
          }
        ]
      },
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: /** @type {const} */ ('outline'),
        onclick: () => { refreshAllData(); }
      }
    ]
  }));

  let lastUrlLotParam = '';
  $effect(() => {
    const currentParam = $page.url.searchParams.get('lot');
    if (currentParam && currentParam !== lastUrlLotParam && !loading) {
      lastUrlLotParam = currentParam;
      selectedLotId = currentParam;
      refreshAllData();
    }
  });

  onMount(async () => {
    const urlParams = $page.url.searchParams;
    const lotParam = urlParams.get('lot');
    if (lotParam) {
      selectedLotId = lotParam;
      lastUrlLotParam = lotParam;
    } else {
      const savedLotId = localStorage.getItem('tracklord_last_lot_id');
      if (savedLotId) selectedLotId = savedLotId;
    }

    await refreshAllData();
  });

  async function refreshAllData() {
    loading = true;
    errorMessage = '';
    try {
      const [steps, rawLots, matLots, clientData] = await Promise.all([
        getManufacturingSteps(),
        getProductionLots(),
        getMaterialLots(),
        getClients()
      ]);

      manufacturingSteps = steps || [];
      materialLots = matLots || [];
      clients = clientData || [];
      productionLots = rawLots || [];

      const urlLotParam = $page.url.searchParams.get('lot');

      if (urlLotParam && productionLots.some((l) => l.id === urlLotParam)) {
        selectedLotId = urlLotParam;
        localStorage.setItem('tracklord_last_lot_id', selectedLotId);
        await loadSelectedLotDetails(selectedLotId);
      } else if (productionLots.length > 0) {
        if (!selectedLotId || !productionLots.some((l) => l.id === selectedLotId)) {
          selectedLotId = productionLots[0].id;
        }
        localStorage.setItem('tracklord_last_lot_id', selectedLotId);
        await loadSelectedLotDetails(selectedLotId);
      } else {
        selectedLotId = '';
        currentLotJourney = null;
        currentMaterialTraceability = null;
        localStorage.removeItem('tracklord_last_lot_id');
      }
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to connect to backend server';
    } finally {
      loading = false;
    }
  }

  /** @param {string} lotId */
  async function loadSelectedLotDetails(lotId) {
    if (!lotId) return;
    try {
      const [journey, trace] = await Promise.all([
        getLotJourney(lotId),
        getMaterialTraceability(lotId)
      ]);
      currentLotJourney = journey;
      currentMaterialTraceability = trace;
    } catch (/** @type {any} */ err) {
      errorMessage = `Failed to load details for lot: ${err.message}`;
    }
  }

  /** @param {string} newLotId */
  async function handleLotChange(newLotId) {
    selectedLotId = newLotId;
    if (newLotId) {
      localStorage.setItem('tracklord_last_lot_id', newLotId);
    }
    await loadSelectedLotDetails(newLotId);
  }

  function openMoveModal(fromStepId) {
    if (fromStepId) {
      moveInitialStepId = fromStepId;
      const balanceObj = activeWipBalances.find(b => b.stepId === fromStepId);
      moveInitialQuantity = balanceObj && typeof balanceObj.quantity === 'number' ? balanceObj.quantity : 0;
    } else {
      const activeStepWithBalance = activeWipBalances.find(b => b.quantity > 0);
      if (activeStepWithBalance) {
        moveInitialStepId = activeStepWithBalance.stepId;
        moveInitialQuantity = activeStepWithBalance.quantity;
      } else if (manufacturingSteps.length > 0) {
        moveInitialStepId = manufacturingSteps[0].id;
        moveInitialQuantity = 0;
      }
    }
    isMoveModalOpen = true;
  }

  // Calculated derived properties
  let activeWipBalances = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.activeBalances) return [];
    return currentLotJourney.activeBalances;
  });

  let totalLotWip = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.activeBalances) return 0;
    return currentLotJourney.activeBalances.reduce((/** @type {number} */ a, /** @type {any} */ b) => a + (Number(b.quantity) || 0), 0);
  });

  let currentActiveStepName = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.activeBalances) return 'None';
    const active = currentLotJourney.activeBalances.find((/** @type {any} */ b) => Number(b.quantity) > 0);
    return active ? active.stepName : 'Completed';
  });

  let timelineSteps = $derived.by(() => {
    if (!manufacturingSteps || manufacturingSteps.length === 0) return [];
    const stepBalancesMap = new Map();
    if (activeWipBalances) {
      activeWipBalances.forEach((/** @type {any} */ b) => stepBalancesMap.set(b.stepId, b.quantity));
    }

    let activeStepIndex = -1;
    manufacturingSteps.forEach((s, idx) => {
      if (stepBalancesMap.has(s.id) && stepBalancesMap.get(s.id) > 0) {
        activeStepIndex = idx;
      }
    });

    return manufacturingSteps.map((step, idx) => {
      const qty = stepBalancesMap.get(step.id) || 0;
      let status = 'pending';
      let subtitle = 'Pending';

      if (qty > 0) {
        status = 'in-progress';
        subtitle = `${qty} Units In Progress`;
      } else if (activeStepIndex >= 0 && idx < activeStepIndex) {
        status = 'completed';
        subtitle = 'Completed';
      }

      let stepClientName = '';
      if ($tenantConfigStore.clientMode === 'lot') {
        stepClientName = currentLotJourney?.clientName || '';
      } else if (currentLotJourney && currentLotJourney.movementHistory) {
        const stepMovements = currentLotJourney.movementHistory.filter((/** @type {any} */ m) => m.toStepName === step.name && !m.isReversed);
        if (stepMovements.length > 0) {
          stepClientName = stepMovements[stepMovements.length - 1].clientName || '';
        } else if (currentLotJourney.clientName) {
          stepClientName = currentLotJourney.clientName;
        }
      }

      return {
        id: step.id,
        code: step.code,
        name: step.name,
        quantity: qty,
        status,
        subtitle,
        clientName: stepClientName,
        icon: STEP_ICON_MAP[step.code] || 'work'
      };
    });
  });

  let movementsLedgerRows = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.movementHistory) return [];
    return currentLotJourney.movementHistory.map((/** @type {any} */ m) => ({
      id: m.movementId,
      date: new Date(m.movementDate).toLocaleString(),
      from: m.fromStepName || 'Initial Creation',
      to: m.toStepName,
      qty: `${m.quantity} Pcs`,
      type: m.movementType,
      clientName: m.clientName || '-',
      referenceNumber: m.referenceNumber || '-',
      user: m.createdBy || 'System',
      remarks: m.remarks || '-',
      isReversed: m.isReversed
    }));
  });

  async function handleDeleteLot() {
    if (!selectedLotId || !currentLotJourney) return;
    const lotNumber = currentLotJourney.lotNumber;
    const confirmMessage = `Are you sure you want to delete Production Lot '${lotNumber}'?\n\nWARNING:\n• All raw material consumptions will be reversed and stock quantities restored.\n• All manufacturing movements & audit records for this lot will be permanently deleted.\n• The production lot entity will be removed.`;

    if (!confirm(confirmMessage)) return;

    errorMessage = '';
    try {
      await deleteProductionLot(selectedLotId);
      successMessage = `Production Lot '${lotNumber}' deleted successfully and raw material stock restored!`;
      selectedLotId = '';
      currentLotJourney = null;
      currentMaterialTraceability = null;
      localStorage.removeItem('tracklord_last_lot_id');
      await refreshAllData();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to delete production lot.';
    }
  }

  function handleOpenEditConsumption(c) {
    selectedConsumptionForEdit = c;
    isEditConsumptionModalOpen = true;
  }

  function handleOpenReverseMovement(m) {
    selectedMovementForReverse = m;
    isReverseModalOpen = true;
  }
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  <!-- Compact Top Bar: Lot Switcher Pill & Desktop Actions -->
  <section class="bg-surface p-2.5 sm:p-4 rounded-2xl border border-outline-variant/70 shadow-sm flex flex-col md:flex-row justify-between items-stretch md:items-center gap-3">
    <!-- Left: Sleek Lot Switcher Pill -->
    <div class="flex items-center gap-2 flex-1 max-w-full">
      <button
        type="button"
        onclick={() => { isLotPickerModalOpen = true; }}
        class="flex-1 sm:flex-none flex items-center justify-between sm:justify-start gap-2.5 px-3.5 py-2 rounded-xl bg-surface-container-low hover:bg-surface-container border border-outline-variant/80 transition-all text-left group cursor-pointer shadow-xs active:scale-[0.99]"
      >
        <div class="flex items-center gap-2 truncate">
          <span class="material-symbols-outlined text-primary text-[20px] shrink-0">inventory_2</span>
          {#if selectedLotObj}
            <div class="truncate">
              <span class="font-bold text-sm text-on-surface tracking-tight font-mono">{selectedLotObj.lotNumber}</span>
              <span class="text-xs text-on-surface-variant ml-1.5 font-mono">({selectedLotObj.initialQuantity} Pcs)</span>
            </div>
            <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-primary/10 text-primary shrink-0">
              {selectedLotObj.status}
            </span>
          {:else}
            <span class="text-sm font-medium text-on-surface-variant">Select Production Lot...</span>
          {/if}
        </div>
        <span class="material-symbols-outlined text-on-surface-variant text-[18px] group-hover:text-primary transition-colors shrink-0">unfold_more</span>
      </button>

      <button
        type="button"
        onclick={() => refreshAllData()}
        title="Refresh Live Data"
        class="p-2 rounded-xl border border-outline-variant/80 text-on-surface-variant hover:text-primary hover:bg-surface-container-low transition-colors flex items-center justify-center cursor-pointer shrink-0 active:scale-95"
      >
        <span class="material-symbols-outlined text-[18px]">refresh</span>
      </button>
    </div>

    <!-- Right: Desktop Toolbar -->
    <div class="hidden md:flex items-center gap-2">
      <Button variant="primary" icon="move_item" disabled={!selectedLotId} onclick={() => openMoveModal('')}>
        Move Qty
      </Button>
      <Button variant="outline" icon="add" onclick={() => isCreateLotModalOpen = true}>
        New Lot
      </Button>
      <Button variant="outline" icon="inventory" onclick={() => isCreateMaterialModalOpen = true}>
        Material
      </Button>
      <Button variant="outline" icon="local_mall" disabled={!selectedLotId} onclick={() => isConsumeMaterialModalOpen = true}>
        Consume
      </Button>
      <Button variant="outline" icon="report_problem" disabled={!selectedLotId} onclick={() => isRejectModalOpen = true}>
        Rejection
      </Button>
    </div>
  </section>

  <!-- View Mode Tabs -->
  <div class="flex items-center justify-between">
    <div class="inline-flex p-1 bg-surface-container-low dark:bg-slate-800 rounded-xl border border-outline-variant/60 shadow-xs">
      <button
        type="button"
        onclick={() => activeTab = 'lots'}
        class="px-3.5 py-1.5 text-xs sm:text-sm font-bold rounded-lg transition-all duration-200 flex items-center gap-1.5 cursor-pointer {activeTab === 'lots' ? 'bg-primary text-white shadow-md shadow-primary/20 scale-[1.01]' : 'text-on-surface-variant hover:text-on-surface'}"
      >
        <span class="material-symbols-outlined text-[16px]">alt_route</span>
        Journey & WIP
      </button>
      <button
        type="button"
        onclick={() => activeTab = 'ledger'}
        class="px-3.5 py-1.5 text-xs sm:text-sm font-bold rounded-lg transition-all duration-200 flex items-center gap-1.5 cursor-pointer {activeTab === 'ledger' ? 'bg-primary text-white shadow-md shadow-primary/20 scale-[1.01]' : 'text-on-surface-variant hover:text-on-surface'}"
      >
        <span class="material-symbols-outlined text-[16px]">history</span>
        Ledger ({movementsLedgerRows.length})
      </button>
    </div>
  </div>

  {#if loading}
    <LoadingSpinner message="Fetching live manufacturing data..." />
  {:else if activeTab === 'lots'}
    {#if currentLotJourney}
      <!-- Compact Lot Summary Card -->
      <section class="bg-surface p-3.5 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3">
        <div class="flex items-center justify-between gap-2 flex-wrap">
          <div class="flex items-center gap-2 flex-wrap">
            <h1 class="text-xl sm:text-2xl text-primary font-bold tracking-tight font-mono">{currentLotJourney.lotNumber}</h1>
            <span class="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wider bg-primary/10 text-primary border border-primary/20">
              {currentLotJourney.status}
            </span>
            {#if currentLotJourney.clientName}
              <span class="px-2.5 py-0.5 rounded-full text-[11px] font-medium bg-surface-container text-on-surface flex items-center gap-1 border border-outline-variant/50">
                <span class="material-symbols-outlined text-[13px] text-primary">badge</span>
                {currentLotJourney.clientName}
              </span>
            {/if}
          </div>

          <button
            type="button"
            onclick={handleDeleteLot}
            title="Delete Production Lot"
            class="px-2.5 py-1 text-xs text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950/60 rounded-lg font-bold flex items-center gap-1 transition-colors cursor-pointer border border-red-200 dark:border-red-900/60"
          >
            <span class="material-symbols-outlined text-[14px]">delete</span>
            <span class="hidden sm:inline">Delete Lot</span>
          </button>
        </div>

        <!-- 3-Metric Clean Strip -->
        <div class="grid grid-cols-3 gap-2 pt-2 border-t border-outline-variant/50 font-mono text-center">
          <div class="p-2 rounded-xl bg-surface-container-low border border-outline-variant/40">
            <div class="text-[10px] text-on-surface-variant uppercase font-sans font-semibold tracking-wider">Total WIP</div>
            <div class="text-base sm:text-lg font-bold text-primary mt-0.5">{totalLotWip} <span class="text-xs font-normal">Pcs</span></div>
          </div>
          <div class="p-2 rounded-xl bg-surface-container-low border border-outline-variant/40">
            <div class="text-[10px] text-on-surface-variant uppercase font-sans font-semibold tracking-wider">Current Stage</div>
            <div class="text-xs sm:text-sm font-bold text-on-surface truncate mt-1">{currentActiveStepName}</div>
          </div>
          <div class="p-2 rounded-xl bg-surface-container-low border border-outline-variant/40">
            <div class="text-[10px] text-on-surface-variant uppercase font-sans font-semibold tracking-wider">Created</div>
            <div class="text-xs sm:text-sm font-semibold text-on-surface mt-1">{new Date(currentLotJourney.createdAtUtc).toLocaleDateString()}</div>
          </div>
        </div>
      </section>

      <!-- Lot Content Sections: Side by Side on Desktop -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-6 items-start">
        <!-- WIP Distribution Section -->
        <WipDistribution
          {timelineSteps}
          {totalLotWip}
          onMoveStep={(stepId) => openMoveModal(stepId)}
        />

        <!-- Raw Material Traceability Section -->
        <MaterialTraceability
          traceability={currentMaterialTraceability}
          onLogMaterialClick={() => isConsumeMaterialModalOpen = true}
          onEditConsumptionClick={handleOpenEditConsumption}
        />
      </div>
    {:else}
      <EmptyState icon="inventory_2" title="No production lots found" message="Click 'New Lot' to create your first production lot." />
    {/if}
  {:else if activeTab === 'ledger'}
    <!-- Movement History Ledger -->
    <MovementLedger
      movements={movementsLedgerRows}
      onReverseClick={handleOpenReverseMovement}
    />
  {/if}
</main>

<!-- Modals -->
<LotPickerModal
  bind:isOpen={isLotPickerModalOpen}
  lots={productionLots}
  {selectedLotId}
  onSelect={handleLotChange}
/>

<CreateLotModal
  bind:isOpen={isCreateLotModalOpen}
  steps={manufacturingSteps}
  {clients}
  onSuccess={(res) => {
    successMessage = `Production Lot '${res.lotNumber}' created successfully!`;
    refreshAllData();
    if (res.id) handleLotChange(res.id);
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to create lot.';
  }}
/>

<MoveLotModal
  bind:isOpen={isMoveModalOpen}
  lotId={selectedLotId}
  steps={manufacturingSteps}
  {clients}
  initialFromStepId={moveInitialStepId}
  initialQuantity={moveInitialQuantity}
  onSuccess={() => {
    successMessage = 'Movement recorded successfully!';
    refreshAllData();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to record movement.';
  }}
  onCreateClientClick={() => isCreateClientModalOpen = true}
/>

<RejectItemsModal
  bind:isOpen={isRejectModalOpen}
  lotId={selectedLotId}
  steps={manufacturingSteps}
  {clients}
  onSuccess={() => {
    successMessage = 'Item rejection & return transfer recorded successfully!';
    refreshAllData();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to record rejection.';
  }}
/>

<ReverseMovementModal
  bind:isOpen={isReverseModalOpen}
  movement={selectedMovementForReverse}
  onSuccess={() => {
    successMessage = 'Movement transaction reversed successfully!';
    refreshAllData();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to reverse movement.';
  }}
/>

<CreateMaterialModal
  bind:isOpen={isCreateMaterialModalOpen}
  onSuccess={(mat) => {
    successMessage = `Raw Material Lot '${mat.lotNumber}' registered successfully!`;
    getMaterialLots().then(m => materialLots = m || []);
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to register material lot.';
  }}
/>

<ConsumeMaterialModal
  bind:isOpen={isConsumeMaterialModalOpen}
  lotId={selectedLotId}
  {materialLots}
  steps={manufacturingSteps}
  onSuccess={() => {
    successMessage = 'Raw material consumption logged successfully!';
    loadSelectedLotDetails(selectedLotId);
    getMaterialLots().then(m => materialLots = m || []);
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to log consumption.';
  }}
/>

<EditConsumptionModal
  bind:isOpen={isEditConsumptionModalOpen}
  consumption={selectedConsumptionForEdit}
  onSuccess={() => {
    successMessage = 'Material consumption updated successfully!';
    loadSelectedLotDetails(selectedLotId);
    getMaterialLots().then(m => materialLots = m || []);
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to edit consumption.';
  }}
/>

<CreateClientModal
  bind:isOpen={isCreateClientModalOpen}
  onSuccess={(client) => {
    successMessage = `Client '${client.name}' created successfully!`;
    getClients().then(c => clients = c || []);
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to create client.';
  }}
/>
