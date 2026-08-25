<script>
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import Button from '$lib/components/ui/Button.svelte';
  // import Timeline from '$lib/components/ui/Timeline.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import FeatureGate from '$lib/components/ui/FeatureGate.svelte';
  import ClientModeGate from '$lib/components/ui/ClientModeGate.svelte';
  import Callout from '$lib/components/ui/Callout.svelte';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

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
    remarks: '',
    clientId: ''
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

  let isReverseModalOpen = $state(false);
  let reverseForm = $state({
    movementId: '',
    fromStep: '',
    toStep: '',
    quantity: '',
    referenceNumber: '',
    reason: 'Typo correction / Accidental movement',
    user: 'Supervisor'
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
    // Priority: 1. URL query parameter ('?lot=...'), 2. localStorage ('tracklord_last_lot_id')
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

      // 2. Fetch Lots & Filter out lots whose items are all in terminating steps
      const lots = await getProductionLots();
      const rawLots = lots || [];
      const terminatingStepIds = new Set(
        manufacturingSteps
          .filter((s) => s.isTerminating)
          .map((s) => (s.id || '').toLowerCase())
      );
      const terminatingStepCodes = new Set(
        manufacturingSteps
          .filter((s) => s.isTerminating)
          .map((s) => (s.code || '').toUpperCase())
      );

      const urlLotParam = $page.url.searchParams.get('lot');

      const enrichedLots = await Promise.all(
        rawLots.map(async (lot) => {
          try {
            const journey = await getLotJourney(lot.id);
            if (journey && journey.activeBalances) {
              const activeSteps = journey.activeBalances.filter((/** @type {any} */ b) => Number(b.quantity) > 0);
              const isTerminated = activeSteps.length === 0 || activeSteps.every((/** @type {any} */ b) => {
                const stepIdLower = (b.stepId || '').toLowerCase();
                const stepCodeUpper = (b.stepCode || '').toUpperCase();
                return terminatingStepIds.has(stepIdLower) || terminatingStepCodes.has(stepCodeUpper);
              });
              return { ...lot, isTerminated };
            }
          } catch {
            // Ignore single lot journey failure
          }
          return { ...lot, isTerminated: false };
        })
      );

      // Exclude lots where all quantities are in terminating steps (preserve if specifically requested via URL query param)
      productionLots = enrichedLots.filter((l) => !l.isTerminated || (urlLotParam && l.id === urlLotParam));

      if (urlLotParam && enrichedLots.some((l) => l.id === urlLotParam)) {
        selectedLotId = urlLotParam;
        localStorage.setItem('tracklord_last_lot_id', selectedLotId);
        await loadSelectedLotDetails(selectedLotId);
      } else if (productionLots.length > 0) {
        if (!selectedLotId || !productionLots.some((/** @type {any} */ l) => l.id === selectedLotId)) {
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
    moveForm.clientId = '';
    moveForm.clientName = '';
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

      // Find client name associated with this step (LotLevel or StepLevel)
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
      const selectedClient = clients.find((/** @type {any} */ c) => c.id === createLotForm.clientId);
      const payload = {
        lotNumber: createLotForm.lotNumber.trim(),
        itemId: '00000000-0000-0000-0000-000000000001',
        initialQuantity: Number(createLotForm.initialQuantity),
        unit: Number(createLotForm.unit),
        initialStepId: createLotForm.initialStepId,
        createdBy: createLotForm.createdBy,
        remarks: createLotForm.remarks,
        clientId: createLotForm.clientId || null,
        clientName: selectedClient ? selectedClient.name : null
      };

      const res = await createProductionLot(payload);
      successMessage = `Production Lot '${createLotForm.lotNumber}' created successfully!`;
      isCreateLotModalOpen = false;
      createLotForm.lotNumber = '';
      createLotForm.clientId = '';

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
      moveForm.clientId = '';
      moveForm.clientName = '';

      await refreshAllData();
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
        phoneNumber: createClientForm.phoneNumber ? createClientForm.phoneNumber.trim() : '',
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
        createLotForm.clientId = newClientId;
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

      await refreshAllData();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message;
    }
  }

  /** @param {any} m */
  function openReverseModal(m) {
    reverseForm.movementId = m.id;
    reverseForm.fromStep = m.from || 'Source Step';
    reverseForm.toStep = m.to || 'Destination Step';
    reverseForm.quantity = m.qty || '';
    reverseForm.referenceNumber = m.referenceNumber || '-';
    reverseForm.reason = 'Typo correction / Accidental movement';
    reverseForm.user = 'Supervisor';
    isReverseModalOpen = true;
  }

  /** @param {SubmitEvent} e */
  async function handleReverseSubmit(e) {
    e.preventDefault();
    if (!reverseForm.movementId) return;
    errorMessage = '';
    try {
      await reverseMovement(reverseForm.movementId, {
        reason: reverseForm.reason.trim() || 'Reversal initiated from audit ledger UI',
        user: reverseForm.user.trim() || 'Supervisor'
      });
      successMessage = 'Movement transaction reversed successfully!';
      isReverseModalOpen = false;
      await refreshAllData();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to reverse movement transaction.';
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
  let isMobileActionSheetOpen = $state(false);
  let isLotPickerModalOpen = $state(false);
  let lotSearchFilter = $state('');

  let filteredLots = $derived(
    productionLots.filter(l => 
      (l.lotNumber || '').toLowerCase().includes(lotSearchFilter.toLowerCase()) ||
      (l.status || '').toLowerCase().includes(lotSearchFilter.toLowerCase()) ||
      (l.clientName || '').toLowerCase().includes(lotSearchFilter.toLowerCase())
    )
  );

  let selectedLotObj = $derived(productionLots.find(l => l.id === selectedLotId));

  let totalLotWip = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.activeBalances) return 0;
    return currentLotJourney.activeBalances.reduce((/** @type {number} */ a, /** @type {any} */ b) => a + (Number(b.quantity) || 0), 0);
  });

  let currentActiveStepName = $derived.by(() => {
    if (!currentLotJourney || !currentLotJourney.activeBalances) return 'None';
    const active = currentLotJourney.activeBalances.find((/** @type {any} */ b) => Number(b.quantity) > 0);
    return active ? active.stepName : 'Completed';
  });

  let isMoreActionsOpen = $state(false);

  /** @param {MouseEvent} e */
  function handleDocumentClick(e) {
    const target = /** @type {HTMLElement} */ (e.target);
    if (isMoreActionsOpen && target && !target.closest('#more-actions-container')) {
      isMoreActionsOpen = false;
    }
  }

  import { onDestroy } from 'svelte';
  import { headerStore } from '$lib/stores/headerStore.js';

  $effect(() => {
    headerStore.setHeader({
      title: 'Lot Inventory',
      subtitle: selectedLotObj ? `${selectedLotObj.lotNumber} • ${selectedLotObj.clientName || 'Live WIP'}` : 'Live Production WIP',
      icon: 'inventory_2',
      actions: [
        {
          label: 'Refresh',
          icon: 'refresh',
          variant: 'outline',
          onclick: () => { refreshAllData(); }
        }
      ]
    });
  });

  onDestroy(() => {
    headerStore.reset();
  });
</script>

<svelte:window onclick={handleDocumentClick} />

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
        onclick={() => { lotSearchFilter = ''; isLotPickerModalOpen = true; }}
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
        onclick={() => refreshAllData()}
        title="Refresh Live Data"
        class="p-2 rounded-xl border border-outline-variant/80 text-on-surface-variant hover:text-primary hover:bg-surface-container-low transition-colors flex items-center justify-center cursor-pointer shrink-0 active:scale-95"
      >
        <span class="material-symbols-outlined text-[18px]">refresh</span>
      </button>
    </div>

    <!-- Right: Desktop Toolbar (hidden on mobile) -->
    <div class="hidden md:flex items-center gap-2">
      <Button variant="primary" icon="add" onclick={() => isCreateLotModalOpen = true}>
        New Lot
      </Button>
      <Button variant="outline" icon="move_item" disabled={!selectedLotId} onclick={openMoveModal}>
        Move Qty
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

  <!-- View Mode Tabs (Compact iOS-Style Segmented Control) -->
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
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span class="text-sm font-medium">Fetching live manufacturing data...</span>
    </div>
  {:else if activeTab === 'lots'}
    <!-- LOT TRACKING VIEW -->
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

      <!-- Lot Content Sections -->
      <div class="space-y-4 sm:space-y-6">
        <!-- Journey Timeline (Commented out)
        <section class="bg-surface p-4 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3">
          <div class="flex items-center justify-between border-b border-outline-variant/60 pb-2">
            <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-1.5">
              <span class="material-symbols-outlined text-primary text-[18px]">route</span>
              Journey Timeline
            </h2>
          </div>
          <Timeline 
            steps={timelineSteps} 
            onMoveStep={(stepId) => {
              updateMoveQuantityFromStep(stepId);
              isMoveModalOpen = true;
            }}
          />
        </section>
        -->

        <!-- WIP Distribution Across Steps -->
          <section class="bg-surface p-4 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3">
            <div class="flex justify-between items-center border-b border-outline-variant/60 pb-2">
              <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-1.5">
                <span class="material-symbols-outlined text-primary text-[18px]">bar_chart</span>
                WIP Distribution
              </h2>
              <span class="text-xs font-mono font-bold text-primary">{totalLotWip} Pcs Total</span>
            </div>

            {#if totalLotWip > 0}
              <div class="h-2.5 w-full rounded-full bg-surface-container overflow-hidden flex shadow-inner">
                {#each timelineSteps as step}
                  {@const pct = (step.quantity / totalLotWip) * 100}
                  {#if pct > 0}
                    <div
                      class="h-full bg-primary transition-all duration-500 first:rounded-l-full last:rounded-r-full"
                      style="width: {pct}%"
                      title="{step.name}: {step.quantity} Pcs ({Math.round(pct)}%)"
                    ></div>
                  {/if}
                {/each}
              </div>
            {/if}

            <div class="space-y-1.5 pt-1">
              {#each timelineSteps as step}
                {@const hasWip = step.quantity > 0}
                <div class="flex items-center justify-between p-2 rounded-xl transition-colors {hasWip ? 'bg-primary/5 border border-primary/20' : 'hover:bg-surface-container-low opacity-60'}">
                  <div class="flex items-center gap-2 min-w-0 flex-1">
                    <span class="w-2 h-2 rounded-full shrink-0 {hasWip ? 'bg-primary animate-pulse' : 'bg-slate-300 dark:bg-slate-600'}"></span>
                    <span class="text-xs sm:text-sm font-semibold {hasWip ? 'text-primary' : 'text-on-surface'} truncate">{step.name}</span>
                    <span class="text-[11px] font-mono text-on-surface-variant shrink-0">({step.code})</span>
                    {#if $tenantConfigStore.clientMode !== 'none' && step.clientName}
                      <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-amber-500/10 text-amber-900 dark:text-amber-300 border border-amber-500/25 shrink-0 max-w-[130px] sm:max-w-[200px]">
                        <span class="material-symbols-outlined text-[12px] text-amber-600 dark:text-amber-400">badge</span>
                        <span class="truncate">{step.clientName}</span>
                      </span>
                    {/if}
                  </div>
                  <div class="flex items-center gap-2 shrink-0 pl-2">
                    <span class="font-mono text-xs sm:text-sm font-bold {hasWip ? 'text-primary' : 'text-on-surface-variant'}">{step.quantity} Pcs</span>
                    {#if hasWip}
                      <button
                        type="button"
                        onclick={() => { updateMoveQuantityFromStep(step.id); isMoveModalOpen = true; }}
                        class="px-2 py-0.5 bg-primary text-white rounded text-[11px] font-bold hover:bg-blue-700 transition-all cursor-pointer"
                      >
                        Move
                      </button>
                    {/if}
                  </div>
                </div>
              {/each}
            </div>
          </section>

          <!-- Raw Material Traceability -->
          <section class="bg-surface p-4 sm:p-5 rounded-2xl border border-outline-variant/70 shadow-sm space-y-3">
            <div class="flex justify-between items-center border-b border-outline-variant/60 pb-2">
              <div class="flex items-center gap-2">
                <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-tertiary text-[18px]">texture</span>
                  Raw Materials Logged
                </h2>
                {#if currentMaterialTraceability}
                  {@const totalCost = currentMaterialTraceability.totalMaterialCost ?? (currentMaterialTraceability.consumptions ? currentMaterialTraceability.consumptions.reduce((/** @type {number} */ a, /** @type {any} */ c) => a + (c.totalPrice ?? (c.quantityConsumed * (c.unitRate || 0))), 0) : 0)}
                  <span class="px-2 py-0.5 bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 text-[11px] font-bold rounded-full font-mono">
                    ₹{Number(totalCost).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                {/if}
              </div>
              <button
                onclick={() => isConsumeMaterialModalOpen = true}
                class="text-xs text-primary hover:underline font-bold flex items-center gap-1 cursor-pointer"
              >
                <span class="material-symbols-outlined text-[15px]">add</span>
                Log Material
              </button>
            </div>

            {#if currentMaterialTraceability && currentMaterialTraceability.consumptions && currentMaterialTraceability.consumptions.length > 0}
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                {#each currentMaterialTraceability.consumptions as mat}
                  <div class="bg-surface-container-low p-3 rounded-xl border border-outline-variant/60 flex flex-col justify-between relative group">
                    <div class="flex justify-between items-start mb-1.5">
                      <div class="flex items-center gap-1.5">
                        <span class="material-symbols-outlined text-tertiary text-[18px]">texture</span>
                        <div>
                          <div class="font-bold text-xs text-on-surface font-mono">{mat.materialLotNumber}</div>
                          {#if mat.materialName}
                            <div class="text-[10px] text-on-surface-variant truncate max-w-[120px]">{mat.materialName}</div>
                          {/if}
                        </div>
                      </div>
                      <button
                        onclick={() => openEditConsumptionModal(mat)}
                        title="Edit Material Consumption"
                        class="p-1 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                      >
                        <span class="material-symbols-outlined text-[15px]">edit</span>
                      </button>
                    </div>

                    <div class="text-xl font-bold text-primary font-mono">{mat.quantityConsumed} {mat.unit}</div>

                    {#if mat.unitRate !== undefined && mat.unitRate !== null}
                      {@const itemTotal = mat.totalPrice ?? (mat.quantityConsumed * mat.unitRate)}
                      <div class="text-[11px] font-mono font-semibold text-emerald-700 dark:text-emerald-300 mt-1 flex items-center justify-between bg-emerald-50 dark:bg-emerald-950/40 px-2 py-0.5 rounded border border-emerald-200/50 dark:border-emerald-800/50">
                        <span>@ ₹{Number(mat.unitRate).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</span>
                        <span class="font-bold">₹{Number(itemTotal).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</span>
                      </div>
                    {/if}

                    <div class="text-[10px] text-on-surface-variant mt-2 font-mono flex justify-between items-end border-t border-outline-variant/30 pt-1">
                      <span>Step: {mat.stepName}</span>
                      <span>{new Date(mat.consumptionDate).toLocaleDateString()}</span>
                    </div>
                  </div>
                {/each}
              </div>
            {:else}
              <div class="p-4 text-center text-on-surface-variant bg-surface-container-low rounded-xl border border-dashed border-outline-variant text-xs">
                No raw materials logged for this production lot yet.
              </div>
            {/if}
          </section>
        </div>
    {:else}
      <div class="p-12 text-center text-on-surface-variant bg-surface rounded-2xl border border-outline-variant">
        No lot selected. Click "New Lot" or select one from the picker above.
      </div>
    {/if}
  {:else if activeTab === 'ledger'}
    <!-- Movements Ledger View -->
    <section class="bg-surface p-4 sm:p-6 rounded-2xl border border-outline-variant/70 shadow-sm space-y-4">
      <div class="flex justify-between items-center border-b border-outline-variant/60 pb-2">
        <h2 class="font-bold text-base text-on-surface flex items-center gap-1.5">
          <span class="material-symbols-outlined text-primary text-[20px]">history</span>
          Movement History
        </h2>
        <span class="text-xs text-on-surface-variant font-mono">{movementsLedgerRows.length} Total</span>
      </div>

      <!-- Mobile Card List View (< md) -->
      <div class="block md:hidden space-y-2.5">
        {#if movementsLedgerRows.length === 0}
          <div class="p-6 text-center text-xs text-on-surface-variant bg-surface-container-low rounded-xl">
            No movements recorded for this production lot.
          </div>
        {:else}
          {#each movementsLedgerRows as m}
            <div class="p-3 bg-surface-container-low rounded-xl border border-outline-variant/60 shadow-xs space-y-2 {m.isReversed ? 'line-through opacity-50 bg-red-50/20' : ''}">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-1.5">
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-primary/10 text-primary">
                    {m.type}
                  </span>
                  {#if m.referenceNumber !== '-'}
                    <span class="text-[11px] font-mono text-on-surface-variant font-semibold">#{m.referenceNumber}</span>
                  {/if}
                </div>
                <span class="text-[10px] font-mono text-on-surface-variant">{m.date}</span>
              </div>

              <div class="flex items-center justify-between pt-1">
                <div class="flex items-center gap-1.5 text-xs font-semibold text-on-surface">
                  <span>{m.from}</span>
                  <span class="material-symbols-outlined text-[14px] text-primary">arrow_forward</span>
                  <span>{m.to}</span>
                </div>
                <span class="text-sm font-bold text-primary font-mono">{m.qty}</span>
              </div>

              {#if m.clientName !== '-'}
                <div class="text-[11px] text-on-surface-variant flex items-center gap-1">
                  <span class="material-symbols-outlined text-[12px] text-primary">badge</span>
                  <span>Client: <strong class="text-on-surface">{m.clientName}</strong></span>
                </div>
              {/if}

              <div class="flex items-center justify-between pt-1 border-t border-outline-variant/30 text-[10px] text-on-surface-variant">
                <span>By: {m.user}</span>
                {#if !m.isReversed && m.type !== 'InitialProduction'}
                  <button
                    onclick={() => openReverseModal(m)}
                    class="px-2 py-0.5 text-[11px] bg-red-100 text-red-700 hover:bg-red-200 dark:bg-red-950 dark:text-red-300 rounded font-semibold transition-colors cursor-pointer"
                  >
                    Reverse
                  </button>
                {:else if m.isReversed}
                  <span class="text-[10px] text-red-500 font-bold">REVERSED</span>
                {/if}
              </div>
            </div>
          {/each}
        {/if}
      </div>

      <!-- Desktop Table View (>= md) -->
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/60 bg-surface shadow-xs">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant/60 text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
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
                        onclick={() => openReverseModal(m)}
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

<!-- Mobile Floating Action Button (FAB) -->
<div class="fixed bottom-20 right-4 z-40 md:hidden">
  <button
    type="button"
    onclick={() => isMobileActionSheetOpen = true}
    class="flex items-center gap-1.5 px-4 py-3 bg-gradient-to-r from-primary to-blue-600 text-white rounded-full shadow-[0_4px_24px_rgba(0,74,198,0.45)] hover:shadow-primary/60 transition-all duration-200 active:scale-95 cursor-pointer font-bold text-sm tracking-wide border border-white/25"
  >
    <span class="material-symbols-outlined text-[20px]">bolt</span>
    <span>Actions</span>
  </button>
</div>

<!-- Mobile Bottom Action Sheet Drawer -->
<Modal bind:isOpen={isMobileActionSheetOpen} title="Lot & Inventory Actions">
  <div class="space-y-2 py-1">
    <button
      disabled={!selectedLotId}
      onclick={() => { isMobileActionSheetOpen = false; openMoveModal(); }}
      class="w-full px-4 py-3 rounded-xl bg-primary text-white font-bold text-sm flex items-center justify-between shadow-md shadow-primary/25 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer active:scale-[0.98] transition-transform"
    >
      <div class="flex items-center gap-3">
        <span class="material-symbols-outlined text-[22px]">send</span>
        <span>Move Lot Quantity</span>
      </div>
      <span class="material-symbols-outlined text-[18px]">chevron_right</span>
    </button>

    <button
      disabled={!selectedLotId}
      onclick={() => { isMobileActionSheetOpen = false; isConsumeMaterialModalOpen = true; }}
      class="w-full px-4 py-3 rounded-xl bg-surface-container-low hover:bg-surface-container font-semibold text-sm text-on-surface flex items-center justify-between border border-outline-variant/60 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer active:scale-[0.98] transition-transform"
    >
      <div class="flex items-center gap-3">
        <span class="material-symbols-outlined text-[22px] text-emerald-600">local_mall</span>
        <span>Log Material Consumption</span>
      </div>
      <span class="material-symbols-outlined text-[18px] text-on-surface-variant">chevron_right</span>
    </button>

    <button
      disabled={!selectedLotId}
      onclick={() => { isMobileActionSheetOpen = false; isRejectModalOpen = true; }}
      class="w-full px-4 py-3 rounded-xl bg-surface-container-low hover:bg-surface-container font-semibold text-sm text-on-surface flex items-center justify-between border border-outline-variant/60 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer active:scale-[0.98] transition-transform"
    >
      <div class="flex items-center gap-3">
        <span class="material-symbols-outlined text-[22px] text-amber-600">report_problem</span>
        <span>Report Item Rejection</span>
      </div>
      <span class="material-symbols-outlined text-[18px] text-on-surface-variant">chevron_right</span>
    </button>

    <button
      onclick={() => { isMobileActionSheetOpen = false; isCreateLotModalOpen = true; }}
      class="w-full px-4 py-3 rounded-xl bg-surface-container-low hover:bg-surface-container font-semibold text-sm text-on-surface flex items-center justify-between border border-outline-variant/60 cursor-pointer active:scale-[0.98] transition-transform"
    >
      <div class="flex items-center gap-3">
        <span class="material-symbols-outlined text-[22px] text-primary">add_circle</span>
        <span>Create New Production Lot</span>
      </div>
      <span class="material-symbols-outlined text-[18px] text-on-surface-variant">chevron_right</span>
    </button>

    <button
      onclick={() => { isMobileActionSheetOpen = false; isCreateMaterialModalOpen = true; }}
      class="w-full px-4 py-3 rounded-xl bg-surface-container-low hover:bg-surface-container font-semibold text-sm text-on-surface flex items-center justify-between border border-outline-variant/60 cursor-pointer active:scale-[0.98] transition-transform"
    >
      <div class="flex items-center gap-3">
        <span class="material-symbols-outlined text-[22px] text-tertiary">inventory</span>
        <span>Register Raw Material</span>
      </div>
      <span class="material-symbols-outlined text-[18px] text-on-surface-variant">chevron_right</span>
    </button>

    {#if selectedLotId}
      <div class="pt-2 border-t border-outline-variant/60">
        <button
          onclick={() => { isMobileActionSheetOpen = false; handleDeleteLot(); }}
          class="w-full px-4 py-3 rounded-xl bg-red-50 hover:bg-red-100 dark:bg-red-950/40 text-red-700 dark:text-red-300 font-semibold text-sm flex items-center justify-between border border-red-200 dark:border-red-800/60 cursor-pointer active:scale-[0.98] transition-transform"
        >
          <div class="flex items-center gap-3">
            <span class="material-symbols-outlined text-[22px]">delete</span>
            <span>Delete Current Lot</span>
          </div>
          <span class="material-symbols-outlined text-[18px]">chevron_right</span>
        </button>
      </div>
    {/if}
  </div>
</Modal>

<!-- Lot Quick Picker Modal -->
<Modal bind:isOpen={isLotPickerModalOpen} title="Select Production Lot">
  <div class="space-y-3">
    <!-- Search Filter Input -->
    <div class="relative">
      <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[18px]">search</span>
      <input
        type="text"
        bind:value={lotSearchFilter}
        placeholder="Search by lot number, status, or client..."
        class="w-full pl-9 pr-4 py-2 bg-surface-container-low border border-outline-variant/80 rounded-xl text-sm text-on-surface focus:outline-none focus:border-primary"
      />
      {#if lotSearchFilter}
        <button
          type="button"
          onclick={() => lotSearchFilter = ''}
          class="material-symbols-outlined absolute right-3 top-2.5 text-on-surface-variant text-[16px] hover:text-on-surface"
        >
          close
        </button>
      {/if}
    </div>

    <!-- Lot List -->
    <div class="max-h-[55vh] overflow-y-auto space-y-2 divide-y divide-outline-variant/30">
      {#if filteredLots.length === 0}
        <div class="p-8 text-center text-on-surface-variant text-sm">
          No matching lots found.
        </div>
      {:else}
        {#each filteredLots as lot}
          {@const isSelected = lot.id === selectedLotId}
          <button
            type="button"
            onclick={() => { handleLotChange(lot.id); isLotPickerModalOpen = false; }}
            class="w-full p-3 rounded-xl flex items-center justify-between transition-colors text-left cursor-pointer {isSelected ? 'bg-primary/10 border-2 border-primary' : 'hover:bg-surface-container-low border border-transparent'}"
          >
            <div class="space-y-0.5 truncate">
              <div class="flex items-center gap-2">
                <span class="font-bold text-sm text-on-surface font-mono">{lot.lotNumber}</span>
                <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-primary/10 text-primary">
                  {lot.status}
                </span>
              </div>
              <div class="text-xs text-on-surface-variant font-mono flex items-center gap-2">
                <span>{lot.initialQuantity} Pcs</span>
                {#if lot.clientName}
                  <span>•</span>
                  <span class="truncate">{lot.clientName}</span>
                {/if}
              </div>
            </div>

            {#if isSelected}
              <span class="material-symbols-outlined text-primary text-[20px] shrink-0 ml-2">check_circle</span>
            {/if}
          </button>
        {/each}
      {/if}
    </div>

    <div class="pt-2 border-t border-outline-variant/60 flex justify-between items-center">
      <Button
        variant="primary"
        icon="add"
        onclick={() => { isLotPickerModalOpen = false; isCreateLotModalOpen = true; }}
      >
        Create New Lot
      </Button>
      <Button
        variant="outline"
        onclick={() => isLotPickerModalOpen = false}
      >
        Close
      </Button>
    </div>
  </div>
</Modal>

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

    <ClientModeGate mode="lot">
      <div class="space-y-1">
        <div class="flex items-center justify-between">
          <label for="create-client-select" class="text-xs font-semibold text-on-surface-variant">Client / Customer (Optional)</label>
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
          id="create-client-select"
          bind:value={createLotForm.clientId}
          options={[
            { label: 'None (Direct / Internal Factory)', value: '' },
            ...clients.map(c => ({ label: `${c.name} (${c.clientType})`, value: c.id }))
          ]}
          placeholder="Select Client (Optional)..."
          icon="badge"
        />
      </div>
    </ClientModeGate>

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

    <ClientModeGate mode="step">
      <div class="space-y-1">
        <div class="flex items-center justify-between">
          <label for="move-client-select" class="text-xs font-semibold text-on-surface-variant">Subcontractor / Client (Optional)</label>
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
          options={[
            { label: 'None (Direct / Internal Factory)', value: '' },
            ...clients.map(c => ({ label: `${c.name} (${c.clientType})`, value: c.id }))
          ]}
          placeholder="Select Client (Optional)..."
          icon="badge"
        />
      </div>
    </ClientModeGate>

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

<!-- Modal 8: Reverse Movement Transaction -->
<Modal bind:isOpen={isReverseModalOpen} title="Reverse Movement Transaction">
  <form onsubmit={handleReverseSubmit} class="space-y-4">
    <div class="p-3 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 rounded-lg text-xs space-y-1">
      <div class="font-bold text-red-700 dark:text-red-300">Transaction to Reverse:</div>
      <div><span class="text-on-surface-variant font-medium">Ref No:</span> <strong class="text-on-surface">{reverseForm.referenceNumber}</strong></div>
      <div><span class="text-on-surface-variant font-medium">Transfer:</span> <strong class="text-primary">{reverseForm.fromStep} &rarr; {reverseForm.toStep} ({reverseForm.quantity})</strong></div>
    </div>

    <Input
      label="Reason for Reversal"
      bind:value={reverseForm.reason}
      placeholder="e.g. Typo correction / Accidental movement"
      icon="report_problem"
      required={true}
    />

    <Input
      label="Authorized By"
      bind:value={reverseForm.user}
      placeholder="e.g. Supervisor"
      icon="person"
      required={true}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isReverseModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="undo">
        Confirm Reversal
      </Button>
    </div>
  </form>
</Modal>
