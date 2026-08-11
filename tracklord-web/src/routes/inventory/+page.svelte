<script>
  import { onMount } from 'svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import Timeline from '$lib/components/ui/Timeline.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';

  import {
    getManufacturingSteps,
    getProductionLots,
    getMaterialLots,
    getLotJourney,
    getMaterialTraceability,
    getClients,
    createClient,
    createProductionLot,
    moveLot,
    createMaterialLot,
    consumeMaterial,
    editMaterialConsumption,
    rejectItems,
    reverseMovement,
    deleteProductionLot
  } from '$lib/api.js';

  // App State
  let activeTab = $state('lots');
  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  // Data Collections
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

  // Modals state
  let isCreateLotModalOpen = $state(false);
  let isMoveModalOpen = $state(false);
  let isCreateMaterialModalOpen = $state(false);
  let isConsumeMaterialModalOpen = $state(false);
  let isRejectModalOpen = $state(false);
  let isCreateClientModalOpen = $state(false);

  // Form States
  let createLotForm = $state({
    lotNumber: '',
    initialQuantity: 100,
    unit: 1, // Pieces
    initialStepId: '',
    createdBy: 'Operator',
    remarks: ''
  });

  let moveForm = $state({
    fromStepId: '',
    toStepId: '',
    quantity: 10,
    clientId: '',
    clientName: '',
    remarks: '',
    referenceNumber: ''
  });

  let createClientForm = $state({
    name: '',
    phoneNumber: '',
    clientType: 'Buyer'
  });

  let createMaterialForm = $state({
    lotNumber: '',
    materialName: '',
    initialQuantity: 500,
    unit: 2, // Meters
    unitRate: 0
  });

  let consumeMaterialForm = $state({
    materialLotId: '',
    stepId: '',
    quantityConsumed: 25,
    unit: 2, // Meters
    recordedBy: 'Operator'
  });

  let isEditConsumptionModalOpen = $state(false);
  let editConsumptionForm = $state({
    id: '',
    materialLotNumber: '',
    materialName: '',
    quantityConsumed: 0,
    remarks: ''
  });

  let rejectForm = $state({
    stepId: '',
    targetStepId: '',
    quantityRejected: 1,
    reason: 'Damaged during operation - return for rework',
    clientId: '',
    createdBy: 'Inspector'
  });

  let isConsumePiecesUnit = $derived.by(() => {
    const selectedMatLot = materialLots.find(m => m.id === consumeMaterialForm.materialLotId);
    return selectedMatLot && (selectedMatLot.unit === 'Pieces' || selectedMatLot.unit === 1 || selectedMatLot.unitType === 1);
  });

  let isEditConsumePiecesUnit = $derived.by(() => {
    const targetMatLot = materialLots.find(m => m.lotNumber === editConsumptionForm.materialLotNumber);
    return targetMatLot && (targetMatLot.unit === 'Pieces' || targetMatLot.unit === 1 || targetMatLot.unitType === 1);
  });

  // Step Icon mapping helper
  /** @type {Record<string, string>} */
  const stepIconMap = {
    'CUT': 'content_cut',
    'STITCH': 'webhook',
    'WASH': 'local_laundry_service',
    'FIN': 'iron',
    'PACK': 'inventory_2'
  };

  import { page } from '$app/stores';

  onMount(async () => {
    // Priority: 1. URL query parameter ('?lot=...'), 2. localStorage ('tracklord_last_lot_id')
    const urlParams = $page.url.searchParams;
    const lotParam = urlParams.get('lot');
    if (lotParam) {
      selectedLotId = lotParam;
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
      // 1. Fetch Steps
      const steps = await getManufacturingSteps();
      manufacturingSteps = steps || [];

      // Set default step ID for forms if available
      if (manufacturingSteps.length > 0) {
        if (!createLotForm.initialStepId) createLotForm.initialStepId = manufacturingSteps[0].id;
        if (!moveForm.fromStepId) moveForm.fromStepId = manufacturingSteps[0].id;
        if (!moveForm.toStepId && manufacturingSteps.length > 1) moveForm.toStepId = manufacturingSteps[1].id;
        if (!consumeMaterialForm.stepId) consumeMaterialForm.stepId = manufacturingSteps[0].id;
        if (!rejectForm.stepId) rejectForm.stepId = manufacturingSteps[0].id;
        if (!rejectForm.targetStepId) rejectForm.targetStepId = manufacturingSteps[0].id;
      }

      // 2. Fetch Lots
      const lots = await getProductionLots();
      productionLots = lots || [];

      if (productionLots.length > 0) {
        if (!selectedLotId || !productionLots.some((/** @type {any} */ l) => l.id === selectedLotId)) {
          selectedLotId = productionLots[0].id;
        }
        localStorage.setItem('tracklord_last_lot_id', selectedLotId);
        await loadSelectedLotDetails(selectedLotId);
      } else {
        selectedLotId = '';
        currentLotJourney = null;
        currentMaterialTraceability = null;
      }

      // 3. Fetch Raw Material Lots
      const matLots = await getMaterialLots();
      materialLots = matLots || [];
      if (materialLots.length > 0 && !consumeMaterialForm.materialLotId) {
        consumeMaterialForm.materialLotId = materialLots[0].id;
      }

      // 4. Fetch Clients
      const clientData = await getClients();
      clients = clientData || [];
      if (clients.length > 0) {
        if (!moveForm.clientId) {
          moveForm.clientId = clients[0].id;
          moveForm.clientName = clients[0].name;
        }
        if (!rejectForm.clientId) {
          rejectForm.clientId = clients[0].id;
        }
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
      const journey = await getLotJourney(lotId);
      currentLotJourney = journey;

      const trace = await getMaterialTraceability(lotId);
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

  /** @param {string} stepId */
  function updateMoveQuantityFromStep(stepId) {
    moveForm.fromStepId = stepId;
    const balanceObj = activeWipBalances.find((/** @type {any} */ b) => b.stepId === stepId);
    moveForm.quantity = balanceObj && typeof balanceObj.quantity === 'number' ? balanceObj.quantity : 0;
  }

  function openMoveModal() {
    // Find the first step that actually has an active WIP balance > 0
    const activeStepWithBalance = activeWipBalances.find((/** @type {any} */ b) => b.quantity > 0);
    if (activeStepWithBalance) {
      moveForm.fromStepId = activeStepWithBalance.stepId;
    } else if (manufacturingSteps.length > 0) {
      moveForm.fromStepId = manufacturingSteps[0].id;
    }

    if (manufacturingSteps.length > 1) {
      const fromIdx = manufacturingSteps.findIndex(s => s.id === moveForm.fromStepId);
      const nextStep = manufacturingSteps[fromIdx + 1] || manufacturingSteps.find(s => s.id !== moveForm.fromStepId);
      if (nextStep) moveForm.toStepId = nextStep.id;
    }

    updateMoveQuantityFromStep(moveForm.fromStepId);
    isMoveModalOpen = true;
  }



  // Calculated properties from real API data
  let activeWipBalances = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.activeBalances) return [];
    return currentLotJourney.activeBalances;
  });

  let timelineSteps = $derived.by(() => {
    if (!manufacturingSteps || manufacturingSteps.length === 0) return [];
    const stepBalancesMap = new Map();
    if (activeWipBalances) {
      activeWipBalances.forEach((/** @type {any} */ b) => stepBalancesMap.set(b.stepId, b.quantity));
    }

    // Find the latest step that has quantity
    let activeStepIndex = -1;
    manufacturingSteps.forEach((s, idx) => {
      if (stepBalancesMap.has(s.id) && stepBalancesMap.get(s.id) > 0) {
        activeStepIndex = idx;
      }
    });

    return manufacturingSteps.map((step, idx) => {
      const qty = stepBalancesMap.get(step.id) || 0;
      /** @type {'completed' | 'in-progress' | 'pending'} */
      let status = 'pending';
      let subtitle = 'Pending';

      if (qty > 0) {
        status = 'in-progress';
        subtitle = `${qty} Units In Progress`;
      } else if (activeStepIndex >= 0 && idx < activeStepIndex) {
        status = 'completed';
        subtitle = 'Completed';
      }

      // Find latest non-reversed movement associated with this step to extract Client Name
      let stepClientName = '';
      if (currentLotJourney && currentLotJourney.movementHistory) {
        const stepMovements = currentLotJourney.movementHistory.filter((/** @type {any} */ m) => m.toStepName === step.name && !m.isReversed);
        if (stepMovements.length > 0) {
          stepClientName = stepMovements[stepMovements.length - 1].clientName || '';
        }
      }

      return {
        id: step.id,
        name: step.name,
        status: status,
        subtitle: subtitle,
        clientName: stepClientName,
        icon: stepIconMap[step.code] || 'work'
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

  // Form Handlers
  /** @param {SubmitEvent} e */
  async function handleCreateLotSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        lotNumber: createLotForm.lotNumber.trim(),
        itemId: '00000000-0000-0000-0000-000000000001',
        initialQuantity: Number(createLotForm.initialQuantity),
        unit: Number(createLotForm.unit),
        initialStepId: createLotForm.initialStepId,
        createdBy: createLotForm.createdBy,
        remarks: createLotForm.remarks
      };

      const res = await createProductionLot(payload);
      successMessage = `Production Lot '${createLotForm.lotNumber}' created successfully!`;
      isCreateLotModalOpen = false;
      createLotForm.lotNumber = '';

      await refreshAllData();
      if (res && res.id) {
        selectedLotId = res.id;
        await loadSelectedLotDetails(res.id);
      }
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

  /** @param {SubmitEvent} e */
  async function handleMoveSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const selectedClient = clients.find((/** @type {any} */ c) => c.id === moveForm.clientId);
      const payload = {
        productionLotId: selectedLotId,
        fromStepId: moveForm.fromStepId,
        toStepId: moveForm.toStepId,
        quantity: Number(moveForm.quantity),
        movementDate: new Date().toISOString(),
        clientId: moveForm.clientId || null,
        clientName: selectedClient ? selectedClient.name : (moveForm.clientName || '').trim(),
        createdBy: 'Operator',
        remarks: moveForm.remarks,
        referenceNumber: moveForm.referenceNumber || `REF-${Math.floor(Math.random() * 900 + 100)}`
      };

      await moveLot(payload);
      successMessage = 'Movement recorded successfully!';
      isMoveModalOpen = false;
      moveForm.remarks = '';
      moveForm.referenceNumber = '';

      await loadSelectedLotDetails(selectedLotId);
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
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

      const res = await createClient(payload);
      successMessage = `Client '${createClientForm.name}' created successfully!`;
      isCreateClientModalOpen = false;
      
      const newClientId = res.id;
      createClientForm = { name: '', phoneNumber: '', clientType: 'Buyer' };
      
      const clientData = await getClients();
      clients = clientData || [];
      if (newClientId) {
        moveForm.clientId = newClientId;
      }
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to create client';
    }
  }

  /** @param {SubmitEvent} e */
  async function handleCreateMaterialSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        lotNumber: createMaterialForm.lotNumber.trim(),
        materialItemId: '00000000-0000-0000-0000-000000000002',
        materialName: createMaterialForm.materialName.trim(),
        initialQuantity: Number(createMaterialForm.initialQuantity),
        unit: Number(createMaterialForm.unit),
        unitRate: Number(createMaterialForm.unitRate) || 0
      };

      await createMaterialLot(payload);
      successMessage = `Raw Material Lot '${createMaterialForm.lotNumber}' registered successfully!`;
      isCreateMaterialModalOpen = false;
      createMaterialForm.lotNumber = '';
      createMaterialForm.materialName = '';
      createMaterialForm.unitRate = 0;

      const matLots = await getMaterialLots();
      materialLots = matLots || [];
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

  /** @param {SubmitEvent} e */
  async function handleConsumeMaterialSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        productionLotId: selectedLotId,
        materialLotId: consumeMaterialForm.materialLotId,
        stepId: consumeMaterialForm.stepId,
        quantityConsumed: Number(consumeMaterialForm.quantityConsumed),
        unit: Number(consumeMaterialForm.unit),
        consumptionDate: new Date().toISOString(),
        createdBy: consumeMaterialForm.recordedBy
      };

      await consumeMaterial(payload);
      successMessage = 'Raw material consumption logged successfully!';
      isConsumeMaterialModalOpen = false;

      const matLots = await getMaterialLots();
      materialLots = matLots || [];
      await loadSelectedLotDetails(selectedLotId);
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

  /** @param {any} c */
  function openEditConsumptionModal(c) {
    const rawId = c.id || c.Id || c.consumptionId || c.ConsumptionId || '';
    editConsumptionForm.id = rawId;
    editConsumptionForm.materialLotNumber = c.materialLotNumber || c.MaterialLotNumber || '';
    editConsumptionForm.materialName = c.materialName || c.MaterialName || '';
    editConsumptionForm.quantityConsumed = c.quantityConsumed ?? c.QuantityConsumed ?? 0;
    editConsumptionForm.remarks = c.remarks || c.Remarks || '';
    isEditConsumptionModalOpen = true;
  }

  /** @param {SubmitEvent} e */
  async function handleEditConsumptionSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    const targetId = editConsumptionForm.id;
    if (!targetId) {
      errorMessage = 'Unable to edit: Material consumption ID is missing.';
      return;
    }
    try {
      const payload = {
        quantityConsumed: Number(editConsumptionForm.quantityConsumed),
        remarks: editConsumptionForm.remarks.trim()
      };

      await editMaterialConsumption(targetId, payload);
      successMessage = 'Material consumption updated successfully!';
      isEditConsumptionModalOpen = false;

      await loadSelectedLotDetails(selectedLotId);
      const matLots = await getMaterialLots();
      materialLots = matLots || [];
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

  /** @param {SubmitEvent} e */
  async function handleRejectSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      const payload = {
        stepId: rejectForm.stepId,
        targetStepId: rejectForm.targetStepId || rejectForm.stepId,
        quantityRejected: Number(rejectForm.quantityRejected),
        reason: rejectForm.reason,
        rejectionDate: new Date().toISOString(),
        clientId: rejectForm.clientId || null,
        createdBy: rejectForm.createdBy
      };

      await rejectItems(selectedLotId, payload);
      successMessage = 'Item rejection & return transfer recorded successfully!';
      isRejectModalOpen = false;

      await loadSelectedLotDetails(selectedLotId);
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

  /** @param {string} movementId */
  async function handleReverseMovement(movementId) {
    if (!confirm('Are you sure you want to reverse this movement transaction?')) return;
    errorMessage = '';
    try {
      await reverseMovement(movementId, {
        reason: 'Reversal initiated from audit ledger UI',
        user: 'Supervisor'
      });
      successMessage = 'Movement transaction reversed successfully!';
      await loadSelectedLotDetails(selectedLotId);
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

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
</script>

<!-- TopAppBar -->
<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">factory</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD ERP</div>
      <div class="text-xs text-on-surface-variant">Production Lot Inventory</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
    <button
      onclick={() => refreshAllData()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh Data
    </button>
  </div>
</header>

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-10">

  <!-- Notification Banner -->
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

  <!-- Action Bar & Lot Selector -->
  <section class="bg-surface-container-low p-4 rounded-xl border border-outline-variant shadow-sm flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
    <div class="flex flex-col sm:flex-row items-start sm:items-center gap-3 w-full md:w-auto min-w-[280px]">
      <Select
        label="Select Lot:"
        value={selectedLotId}
        onchange={(val) => handleLotChange(val)}
        options={productionLots.map(lot => ({
          label: `${lot.lotNumber} (${lot.initialQuantity} Pcs - ${lot.status})`,
          value: lot.id
        }))}
        placeholder={productionLots.length === 0 ? "No lots available" : "Select a lot..."}
        searchPlaceholder="Search lot number or status..."
      />
    </div>

    <!-- Quick Action Buttons -->
    <div class="flex flex-wrap gap-2 w-full md:w-auto">
      <Button variant="primary" icon="add" onclick={() => isCreateLotModalOpen = true}>
        New Production Lot
      </Button>
      <Button variant="outline" icon="move_item" disabled={!selectedLotId} onclick={openMoveModal}>
        Move Quantity
      </Button>
      <Button variant="outline" icon="inventory" onclick={() => isCreateMaterialModalOpen = true}>
        Register Material
      </Button>
      <Button variant="outline" icon="local_mall" disabled={!selectedLotId} onclick={() => isConsumeMaterialModalOpen = true}>
        Log Consumption
      </Button>
      <Button variant="outline" icon="report_problem" disabled={!selectedLotId} onclick={() => isRejectModalOpen = true}>
        Report Rejection
      </Button>
      {#if selectedLotId}
        <button
          onclick={handleDeleteLot}
          class="px-3 py-1.5 rounded-lg border border-red-200 dark:border-red-900 bg-red-50/50 dark:bg-red-950/40 text-red-600 dark:text-red-400 font-stat-label text-xs hover:bg-red-100 dark:hover:bg-red-900/60 flex items-center gap-1 cursor-pointer transition-colors"
        >
          <span class="material-symbols-outlined text-[16px]">delete</span>
          Delete Lot
        </button>
      {/if}
    </div>
  </section>

  <!-- View Mode Tabs -->
  <div class="flex items-center justify-between border-b border-outline-variant pb-2">
    <div class="flex gap-2">
      <button
        onclick={() => activeTab = 'lots'}
        class="px-4 py-2 text-sm font-bold rounded-lg transition-colors flex items-center gap-2 {activeTab === 'lots' ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:bg-surface-container-low'}"
      >
        <span class="material-symbols-outlined text-[18px]">reorder</span>
        Lot Tracking & Journey
      </button>
      <button
        onclick={() => activeTab = 'materials'}
        class="px-4 py-2 text-sm font-bold rounded-lg transition-colors flex items-center gap-2 {activeTab === 'materials' ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:bg-surface-container-low'}"
      >
        <span class="material-symbols-outlined text-[18px]">inventory_2</span>
        Raw Materials Ledger
      </button>
    </div>
  </div>

  {#if loading}
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span>Fetching live data from backend API...</span>
    </div>
  {:else if activeTab === 'lots'}
    <!-- LOT TRACKING VIEW -->
    {#if currentLotJourney}
      <!-- Header Info -->
      <section class="flex flex-col md:flex-row justify-between items-start md:items-end gap-4 bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="font-headline-lg text-headline-lg text-primary font-bold">{currentLotJourney.lotNumber}</h1>
            <span class="px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider bg-primary-container text-on-primary-container">
              {currentLotJourney.status}
            </span>
            <button
              onclick={handleDeleteLot}
              title="Delete Production Lot"
              class="px-2.5 py-1 text-xs bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-300 hover:bg-red-200 dark:hover:bg-red-900 rounded-lg font-bold flex items-center gap-1 transition-colors cursor-pointer"
            >
              <span class="material-symbols-outlined text-[15px]">delete</span>
              Delete Lot
            </button>
          </div>
          <p class="text-sm text-on-surface-variant mt-1">
            Lot ID: <span class="font-mono text-xs">{currentLotJourney.lotId}</span>
          </p>
        </div>

        <div class="flex flex-wrap gap-3">
          <div class="bg-surface-container-low border border-outline-variant px-4 py-2 rounded-lg text-center">
            <div class="text-xs text-on-surface-variant font-mono uppercase">Total WIP Qty</div>
            <div class="text-xl font-bold text-on-surface">{currentLotJourney.activeBalances ? currentLotJourney.activeBalances.reduce((/** @type {number} */ a, /** @type {any} */ b) => a + b.quantity, 0) : 0} Pcs</div>
          </div>
          <div class="bg-surface-container-low border border-outline-variant px-4 py-2 rounded-lg text-center">
            <div class="text-xs text-on-surface-variant font-mono uppercase">Created At</div>
            <div class="text-sm font-semibold text-on-surface">{new Date(currentLotJourney.createdAtUtc).toLocaleDateString()}</div>
          </div>
        </div>
      </section>

      <!-- Grid Content -->
      <div class="grid grid-cols-1 md:grid-cols-12 gap-6">
        <!-- Journey Timeline -->
        <section class="md:col-span-4 bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
          <h2 class="font-headline-md text-headline-md border-b border-outline-variant pb-2 mb-4">Journey Timeline</h2>
          <Timeline steps={timelineSteps} />
        </section>

        <div class="md:col-span-8 space-y-6">
          <!-- Current Inventory Distribution -->
          <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
            <h2 class="font-headline-md text-headline-md border-b border-outline-variant pb-2 mb-4">WIP Distribution Across Steps</h2>
            <div class="space-y-4">
              {#each manufacturingSteps as step}
                {@const balanceObj = activeWipBalances.find((/** @type {any} */ b) => b.stepId === step.id)}
                {@const qty = balanceObj ? balanceObj.quantity : 0}
                {@const totalActive = activeWipBalances.reduce((/** @type {number} */ a, /** @type {any} */ b) => a + b.quantity, 0)}
                <div>
                  <div class="flex justify-between font-stat-label text-stat-label mb-1">
                    <span class="font-bold text-on-surface">{step.name} ({step.code})</span>
                    <span class="font-mono text-primary">{qty} Pcs</span>
                  </div>
                  <div class="w-full bg-surface-variant rounded-full h-2.5">
                    <div
                      class="bg-primary h-2.5 rounded-full transition-all duration-500"
                      style="width: {totalActive > 0 ? (qty / totalActive) * 100 : 0}%"
                    ></div>
                  </div>
                </div>
              {/each}
            </div>
          </section>

          <!-- Raw Material Traceability -->
          <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
            <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center border-b border-outline-variant pb-2 mb-4 gap-2">
              <div class="flex items-center gap-3">
                <h2 class="font-headline-md text-headline-md">Raw Material Traceability</h2>
                {#if currentMaterialTraceability}
                  {@const totalCost = currentMaterialTraceability.totalMaterialCost ?? (currentMaterialTraceability.consumptions ? currentMaterialTraceability.consumptions.reduce((/** @type {number} */ a, /** @type {any} */ c) => a + (c.totalPrice ?? (c.quantityConsumed * (c.unitRate || 0))), 0) : 0)}
                  <span class="px-3 py-1 bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 text-xs font-bold rounded-full border border-emerald-300 dark:border-emerald-700 flex items-center gap-1 font-mono">
                    <span class="material-symbols-outlined text-[15px]">payments</span>
                    Total Price: ₹{Number(totalCost).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                {/if}
              </div>
              <button
                onclick={() => isConsumeMaterialModalOpen = true}
                class="text-xs text-primary hover:underline font-bold flex items-center gap-1 cursor-pointer"
              >
                <span class="material-symbols-outlined text-[16px]">add</span>
                Log Material
              </button>
            </div>

            {#if currentMaterialTraceability && currentMaterialTraceability.consumptions && currentMaterialTraceability.consumptions.length > 0}
              <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
                {#each currentMaterialTraceability.consumptions as mat}
                  <div class="bg-surface-container-low p-4 rounded-lg border border-outline-variant flex flex-col justify-between relative group">
                    <div class="flex justify-between items-start mb-2">
                      <div class="flex items-center gap-2">
                        <span class="material-symbols-outlined text-tertiary">texture</span>
                        <div>
                          <div class="font-bold text-sm text-on-surface">{mat.materialLotNumber}</div>
                          {#if mat.materialName}
                            <div class="text-[11px] font-medium text-on-surface-variant">{mat.materialName}</div>
                          {/if}
                        </div>
                      </div>
                      <button
                        onclick={() => openEditConsumptionModal(mat)}
                        title="Edit Material Consumption"
                        class="p-1 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                      >
                        <span class="material-symbols-outlined text-[16px]">edit</span>
                      </button>
                    </div>
                    <div class="text-2xl font-bold text-primary">{mat.quantityConsumed} {mat.unit}</div>
                    
                    {#if mat.unitRate !== undefined && mat.unitRate !== null}
                      {@const itemTotal = mat.totalPrice ?? (mat.quantityConsumed * mat.unitRate)}
                      <div class="text-xs font-mono font-semibold text-emerald-700 dark:text-emerald-300 mt-1 flex items-center justify-between bg-emerald-50 dark:bg-emerald-950/40 px-2 py-1 rounded border border-emerald-200/50 dark:border-emerald-800/50">
                        <span>@ ₹{Number(mat.unitRate).toLocaleString('en-IN', { minimumFractionDigits: 2 })}/{mat.unit}</span>
                        <span class="font-bold text-emerald-800 dark:text-emerald-200">Total: ₹{Number(itemTotal).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</span>
                      </div>
                    {/if}

                    <div class="text-xs text-on-surface-variant mt-2 font-mono flex justify-between items-end">
                      <div>
                        Step: {mat.stepName}<br/>
                        Date: {new Date(mat.consumptionDate).toLocaleDateString()}
                      </div>
                      {#if mat.remarks}
                        <div class="text-[11px] italic text-on-surface-variant max-w-[120px] truncate" title={mat.remarks}>
                          "{mat.remarks}"
                        </div>
                      {/if}
                    </div>
                  </div>
                {/each}
              </div>
            {:else}
              <div class="p-6 text-center text-on-surface-variant bg-surface-container-low rounded-lg border border-dashed border-outline-variant text-sm">
                No raw material consumptions logged for this production lot yet.
              </div>
            {/if}
          </section>
        </div>
      </div>
    {:else}
      <div class="p-12 text-center text-on-surface-variant bg-surface rounded-xl border border-outline-variant">
        No lot selected or no production lots found. Click "New Production Lot" above to create one.
      </div>
    {/if}
  {:else if activeTab === 'ledger'}
    <!-- Ledger Table -->
    <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm space-y-4">
      <div class="flex justify-between items-center border-b border-outline-variant pb-2">
        <h2 class="font-headline-md text-headline-md">Inventory Movement Ledger</h2>
        <span class="text-xs text-on-surface-variant font-mono">Total Transactions: {movementsLedgerRows.length}</span>
      </div>

      <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface shadow-sm">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-4 py-3">Movement Date</th>
              <th class="px-4 py-3">Type</th>
              <th class="px-4 py-3">Ref No</th>
              <th class="px-4 py-3">Client Name</th>
              <th class="px-4 py-3">From Step</th>
              <th class="px-4 py-3">To Step</th>
              <th class="px-4 py-3">Quantity</th>
              <th class="px-4 py-3">User</th>
              <th class="px-4 py-3">Remarks</th>
              <th class="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#if movementsLedgerRows.length === 0}
              <tr>
                <td colspan="10" class="px-4 py-8 text-center text-on-surface-variant">
                  No movements recorded for this production lot.
                </td>
              </tr>
            {:else}
              {#each movementsLedgerRows as m}
                <tr class="hover:bg-surface-container-low/60 transition-colors {m.isReversed ? 'line-through opacity-50 bg-red-50/20' : ''}">
                  <td class="px-4 py-3 whitespace-nowrap text-xs font-mono">{m.date}</td>
                  <td class="px-4 py-3 whitespace-nowrap">
                    <span class="px-2 py-0.5 rounded text-[11px] font-bold font-mono bg-primary-container text-on-primary-container">
                      {m.type}
                    </span>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap font-mono text-xs font-bold text-on-surface-variant">{m.referenceNumber}</td>
                  <td class="px-4 py-3 whitespace-nowrap font-bold text-primary">{m.clientName}</td>
                  <td class="px-4 py-3 whitespace-nowrap">{m.from}</td>
                  <td class="px-4 py-3 whitespace-nowrap">{m.to}</td>
                  <td class="px-4 py-3 whitespace-nowrap font-bold text-primary">{m.qty}</td>
                  <td class="px-4 py-3 whitespace-nowrap text-xs">{m.user}</td>
                  <td class="px-4 py-3">{m.remarks}</td>
                  <td class="px-4 py-3 text-right whitespace-nowrap">
                    {#if !m.isReversed && m.type !== 'InitialProduction'}
                      <button
                        onclick={() => handleReverseMovement(m.id)}
                        class="px-2 py-1 text-xs bg-red-100 text-red-700 hover:bg-red-200 dark:bg-red-950 dark:text-red-300 rounded font-semibold transition-colors cursor-pointer"
                      >
                        Reverse
                      </button>
                    {:else if m.isReversed}
                      <span class="text-xs text-red-500 font-bold">REVERSED</span>
                    {/if}
                  </td>
                </tr>
              {/each}
            {/if}
          </tbody>
        </table>
      </div>
    </section>
  {/if}
</main>

<!-- Modal 1: Create Production Lot -->
<Modal bind:isOpen={isCreateLotModalOpen} title="Create New Production Lot">
  <form onsubmit={handleCreateLotSubmit} class="space-y-4">
    <Input
      label="Production Lot Number"
      bind:value={createLotForm.lotNumber}
      placeholder="e.g. LOT-2026-002"
      icon="tag"
      required={true}
    />

    <Input
      label="Initial Quantity (Pcs)"
      type="number"
      bind:value={createLotForm.initialQuantity}
      placeholder="e.g. 100"
      icon="numbers"
      required={true}
    />

    <Select
      label="Initial Manufacturing Step"
      bind:value={createLotForm.initialStepId}
      options={manufacturingSteps.map(s => ({ label: `${s.name} (${s.code})`, value: s.id }))}
      icon="factory"
      required={true}
    />

    <Input
      label="Created By"
      bind:value={createLotForm.createdBy}
      placeholder="e.g. Production Manager"
      icon="person"
    />

    <Input
      label="Remarks / Notes"
      bind:value={createLotForm.remarks}
      placeholder="Optional notes..."
      icon="notes"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isCreateLotModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="send">
        Create Lot
      </Button>
    </div>
  </form>
</Modal>

<!-- Modal 2: Move Production Lot Quantity -->
<Modal bind:isOpen={isMoveModalOpen} title="Move Production Lot Quantity">
  <form onsubmit={handleMoveSubmit} class="space-y-4">
    <Select
      label="Source Step (From)"
      bind:value={moveForm.fromStepId}
      onchange={(stepId) => updateMoveQuantityFromStep(stepId)}
      options={manufacturingSteps.map(s => {
        const bal = activeWipBalances.find((/** @type {any} */ b) => b.stepId === s.id);
        const availableQty = bal ? bal.quantity : 0;
        return { label: `${s.name} (${s.code}) - Avail: ${availableQty} Pcs`, value: s.id };
      })}
      icon="logout"
      required={true}
    />

    <Select
      label="Destination Step (To)"
      bind:value={moveForm.toStepId}
      options={manufacturingSteps.map(s => ({ label: `${s.name} (${s.code})`, value: s.id }))}
      icon="login"
      required={true}
    />

    <Input
      label="Movement Quantity (Pcs)"
      type="number"
      bind:value={moveForm.quantity}
      placeholder="Quantity to move..."
      icon="numbers"
      required={true}
    />

    <div class="space-y-1">
      <div class="flex items-center justify-between">
        <label for="move-client-select" class="text-xs font-semibold text-on-surface-variant">Client / Customer</label>
        <button
          type="button"
          onclick={() => isCreateClientModalOpen = true}
          class="text-xs text-primary hover:underline font-bold flex items-center gap-1 cursor-pointer"
        >
          <span class="material-symbols-outlined text-[14px]">add_circle</span>
          Add New Client
        </button>
      </div>
      <Select
        id="move-client-select"
        bind:value={moveForm.clientId}
        options={clients.map(c => ({ label: `${c.name} (${c.clientType})`, value: c.id }))}
        icon="badge"
        required={true}
      />
    </div>

    <Input
      label="Reference Number"
      bind:value={moveForm.referenceNumber}
      placeholder="e.g. CHIT-901"
      icon="tag"
    />

    <Input
      label="Remarks / Notes"
      bind:value={moveForm.remarks}
      placeholder="Optional movement notes..."
      icon="notes"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isMoveModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="send">
        Confirm Movement
      </Button>
    </div>
  </form>
</Modal>

<!-- Modal 3: Register Raw Material Lot -->
<Modal bind:isOpen={isCreateMaterialModalOpen} title="Register Raw Material Lot">
  <form onsubmit={handleCreateMaterialSubmit} class="space-y-4">
    <Input
      label="Material Lot Number"
      bind:value={createMaterialForm.lotNumber}
      placeholder="e.g. FAB-2026-001"
      icon="tag"
      required={true}
    />

    <Input
      label="Product / Material Name"
      bind:value={createMaterialForm.materialName}
      placeholder="e.g. Denim Grey Cloth 300GSM"
      icon="category"
      required={true}
    />

    <Input
      label="Initial Quantity"
      type="number"
      step={Number(createMaterialForm.unit) === 1 ? '1' : 'any'}
      bind:value={createMaterialForm.initialQuantity}
      placeholder={Number(createMaterialForm.unit) === 1 ? 'e.g. 500' : 'e.g. 500.5'}
      icon="numbers"
      required={true}
    />

    <Select
      label="Unit of Measurement"
      bind:value={createMaterialForm.unit}
      onchange={() => {
        if (Number(createMaterialForm.unit) === 1) {
          createMaterialForm.initialQuantity = Math.round(Number(createMaterialForm.initialQuantity) || 0);
        }
      }}
      options={[
        { label: 'Meters', value: 2 },
        { label: 'Pieces', value: 1 },
        { label: 'Kilograms', value: 3 },
        { label: 'Yards', value: 5 }
      ]}
      icon="square_foot"
      required={true}
    />

    <Input
      label="Rate per Unit (₹)"
      type="number"
      step="any"
      bind:value={createMaterialForm.unitRate}
      placeholder="e.g. 150.00"
      icon="payments"
      required={false}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isCreateMaterialModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save">
        Register Material
      </Button>
    </div>
  </form>
</Modal>

<!-- Modal 4: Consume Raw Material -->
<Modal bind:isOpen={isConsumeMaterialModalOpen} title="Log Raw Material Consumption">
  <form onsubmit={handleConsumeMaterialSubmit} class="space-y-4">
    <Select
      label="Select Raw Material Lot"
      bind:value={consumeMaterialForm.materialLotId}
      options={materialLots.map(m => ({ label: `${m.lotNumber}${m.materialName ? ' (' + m.materialName + ')' : ''} - Avail: ${m.remainingQuantity} ${m.unit}${m.unitRate ? ' @ ₹' + m.unitRate + '/' + m.unit : ''}`, value: m.id }))}
      icon="texture"
      required={true}
    />

    <Select
      label="Consumption Step"
      bind:value={consumeMaterialForm.stepId}
      options={manufacturingSteps.map(s => ({ label: `${s.name} (${s.code})`, value: s.id }))}
      icon="factory"
      required={true}
    />

    <Input
      label="Quantity Consumed"
      type="number"
      step={isConsumePiecesUnit ? '1' : 'any'}
      bind:value={consumeMaterialForm.quantityConsumed}
      placeholder={isConsumePiecesUnit ? 'e.g. 25' : 'e.g. 25.5'}
      icon="numbers"
      required={true}
    />

    <Input
      label="Recorded By"
      bind:value={consumeMaterialForm.recordedBy}
      placeholder="Operator name"
      icon="person"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isConsumeMaterialModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="local_mall">
        Log Consumption
      </Button>
    </div>
  </form>
</Modal>

<!-- Modal 5: Report Rejection -->
<Modal bind:isOpen={isRejectModalOpen} title="Report Item Rejection / Return">
  <form onsubmit={handleRejectSubmit} class="space-y-4">
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <Select
        label="Current Step (From)"
        bind:value={rejectForm.stepId}
        options={manufacturingSteps.map(s => ({ label: `${s.name} (${s.code})`, value: s.id }))}
        icon="factory"
        required={true}
      />

      <Select
        label="Return To Step (Target)"
        bind:value={rejectForm.targetStepId}
        options={manufacturingSteps.map(s => ({ label: `${s.name} (${s.code})`, value: s.id }))}
        icon="undo"
        required={true}
      />
    </div>

    <div>
      <div class="flex justify-between items-center mb-1">
        <span class="text-xs font-medium text-on-surface-variant">Associated Client</span>
        <button
          type="button"
          onclick={() => isCreateClientModalOpen = true}
          class="text-xs text-primary hover:underline font-semibold flex items-center gap-1 cursor-pointer"
        >
          <span class="material-symbols-outlined text-[14px]">add</span>
          Add New Client
        </button>
      </div>
      <Select
        bind:value={rejectForm.clientId}
        options={clients.length > 0
          ? clients.map(c => ({ label: `${c.name} (${c.clientType})`, value: c.id }))
          : [{ label: 'No clients found - Click + Add New Client', value: '' }]}
        icon="person"
      />
    </div>

    <Input
      label="Quantity Rejected"
      type="number"
      bind:value={rejectForm.quantityRejected}
      placeholder="e.g. 5"
      icon="numbers"
      required={true}
    />

    <Input
      label="Reason / Defect Description"
      bind:value={rejectForm.reason}
      placeholder="e.g. Defective stitching / return to washing for rework"
      icon="report_problem"
      required={true}
    />

    <Input
      label="Inspector / Reporter"
      bind:value={rejectForm.createdBy}
      placeholder="Inspector name"
      icon="person"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isRejectModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="warning">
        Report Rejection & Return
      </Button>
    </div>
  </form>
</Modal>

<!-- Modal 6: Edit Raw Material Consumption -->
<Modal bind:isOpen={isEditConsumptionModalOpen} title="Edit Raw Material Consumption">
  <form onsubmit={handleEditConsumptionSubmit} class="space-y-4">
    <div class="p-3 bg-surface-container-low rounded border border-outline-variant text-xs space-y-1">
      <div><span class="text-on-surface-variant font-medium">Material Lot:</span> <strong class="text-on-surface">{editConsumptionForm.materialLotNumber}</strong></div>
      {#if editConsumptionForm.materialName}
        <div><span class="text-on-surface-variant font-medium">Item Name:</span> <strong class="text-primary">{editConsumptionForm.materialName}</strong></div>
      {/if}
    </div>

    <Input
      label="Quantity Consumed"
      type="number"
      step={isEditConsumePiecesUnit ? '1' : 'any'}
      bind:value={editConsumptionForm.quantityConsumed}
      placeholder={isEditConsumePiecesUnit ? 'e.g. 30' : 'e.g. 30.5'}
      icon="numbers"
      required={true}
    />

    <Input
      label="Remarks / Notes"
      bind:value={editConsumptionForm.remarks}
      placeholder="Optional consumption notes..."
      icon="notes"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isEditConsumptionModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save">
        Update Consumption
      </Button>
    </div>
  </form>
</Modal>

<!-- Modal 7: Add New Client (inline for movements) -->
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
