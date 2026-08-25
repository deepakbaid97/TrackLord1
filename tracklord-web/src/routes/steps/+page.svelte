<script>
  import { onMount } from 'svelte';
  import {
    getManufacturingSteps,
    createManufacturingStep,
    updateManufacturingStep,
    reorderManufacturingSteps,
    getProductionLots,
    getLotJourney
  } from '$lib/api.js';
  import Callout from '$lib/components/ui/Callout.svelte';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  /** @type {any[]} */
  let steps = $state([]);

  // Drag & drop reorder state
  let draggedStepIndex = $state(/** @type {number|null} */ (null));
  let dragOverStepIndex = $state(/** @type {number|null} */ (null));

  // Reorder confirmation modal state
  let showReorderConfirmModal = $state(false);
  /** @type {{ movedStep: any, fromIndex: number, toIndex: number, newSteps: any[] } | null} */
  let pendingReorder = $state(null);
  let submittingReorder = $state(false);

  // Modals state
  let showCreateModal = $state(false);
  let showEditModal = $state(false);
  let submitting = $state(false);
  let modalError = $state('');

  // Create form state
  let newCode = $state('');
  let newName = $state('');
  let newSequenceOrder = $state(1);
  let newIsTerminating = $state(false);

  // Edit form state
  /** @type {any} */
  let editingStep = $state(null);
  let editName = $state('');
  let editSequenceOrder = $state(1);
  let editIsTerminating = $state(false);
  let editIsActive = $state(true);

  onMount(async () => {
    await fetchSteps();
  });

  async function fetchSteps() {
    loading = true;
    errorMessage = '';
    try {
      const [stepsData, lotsData] = await Promise.all([
        getManufacturingSteps(),
        getProductionLots()
      ]);

      const stepsList = stepsData || [];
      const lotsList = lotsData || [];

      // Map to accumulate lots and quantities at each step
      /** @type {Record<string, Array<{lotId: string, lotNumber: string, quantity: number}>>} */
      const stepLotsMap = {};
      stepsList.forEach(s => { stepLotsMap[s.id] = []; });

      await Promise.all(
        lotsList.map(async (lot) => {
          try {
            const journey = await getLotJourney(lot.id);
            if (journey && journey.activeBalances) {
              journey.activeBalances.forEach((/** @type {any} */ b) => {
                if (b.quantity > 0) {
                  if (!stepLotsMap[b.stepId]) stepLotsMap[b.stepId] = [];
                  stepLotsMap[b.stepId].push({
                    lotId: lot.id,
                    lotNumber: lot.lotNumber,
                    quantity: b.quantity
                  });
                }
              });
            }
          } catch {
            // Ignore single lot failure
          }
        })
      );

      steps = stepsList.map(s => {
        const lotsAtThisStep = stepLotsMap[s.id] || [];
        const totalQty = lotsAtThisStep.reduce((sum, l) => sum + l.quantity, 0);
        return {
          ...s,
          lotsAtStep: lotsAtThisStep,
          totalQuantity: totalQty
        };
      });
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load manufacturing steps.';
    } finally {
      loading = false;
    }
  }

  function openCreateModal() {
    newCode = '';
    newName = '';
    // Auto suggest next sequence number
    const maxSeq = steps.length > 0 ? Math.max(...steps.map(s => s.sequenceOrder || 0)) : 0;
    newSequenceOrder = maxSeq + 1;
    newIsTerminating = false;
    modalError = '';
    showCreateModal = true;
  }

  /** @param {any} step */
  function openEditModal(step) {
    editingStep = step;
    editName = step.name;
    editSequenceOrder = step.sequenceOrder;
    editIsTerminating = step.isTerminating ?? false;
    editIsActive = step.isActive ?? true;
    modalError = '';
    showEditModal = true;
  }

  async function handleCreateStep(e) {
    e.preventDefault();
    if (!newCode.trim()) {
      modalError = 'Step code is required.';
      return;
    }
    if (!newName.trim()) {
      modalError = 'Step name is required.';
      return;
    }

    submitting = true;
    modalError = '';
    try {
      await createManufacturingStep({
        code: newCode.trim().toUpperCase(),
        name: newName.trim(),
        sequenceOrder: Number(newSequenceOrder),
        isTerminating: newIsTerminating
      });
      showCreateModal = false;
      showSuccessToast(`Manufacturing step '${newCode.trim().toUpperCase()}' created successfully!`);
      await fetchSteps();
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to create manufacturing step.';
    } finally {
      submitting = false;
    }
  }

  async function handleUpdateStep(e) {
    e.preventDefault();
    if (!editingStep) return;
    if (!editName.trim()) {
      modalError = 'Step name is required.';
      return;
    }

    submitting = true;
    modalError = '';
    try {
      await updateManufacturingStep(editingStep.id, {
        name: editName.trim(),
        sequenceOrder: Number(editSequenceOrder),
        isTerminating: editIsTerminating,
        isActive: editIsActive
      });
      showEditModal = false;
      showSuccessToast(`Step '${editingStep.code}' updated successfully!`);
      await fetchSteps();
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to update manufacturing step.';
    } finally {
      submitting = false;
    }
  }

  /** @param {string} msg */
  function showSuccessToast(msg) {
    successMessage = msg;
    setTimeout(() => {
      successMessage = '';
    }, 4000);
  }

  let isDragging = false;

  /**
   * @param {number} index
   * @param {DragEvent} e
   */
  function handleDragStart(index, e) {
    draggedStepIndex = index;
    isDragging = true;
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
      e.dataTransfer.setData('text/plain', index.toString());
    }
  }

  /**
   * @param {DragEvent} e
   */
  function handleDragOver(e) {
    e.preventDefault();
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = 'move';
    }
  }

  /**
   * @param {number} index
   * @param {DragEvent} e
   */
  function handleDragEnter(index, e) {
    e.preventDefault();
    if (draggedStepIndex !== null && dragOverStepIndex !== index) {
      dragOverStepIndex = index;
    }
  }

  function handleDragEnd() {
    draggedStepIndex = null;
    dragOverStepIndex = null;
    setTimeout(() => {
      isDragging = false;
    }, 150);
  }

  /**
   * @param {number} targetIndex
   * @param {DragEvent} e
   */
  function handleDrop(targetIndex, e) {
    e.preventDefault();
    e.stopPropagation();

    let from = draggedStepIndex;
    if (from === null && e.dataTransfer) {
      const data = e.dataTransfer.getData('text/plain');
      if (data !== '') from = parseInt(data, 10);
    }

    draggedStepIndex = null;
    dragOverStepIndex = null;
    setTimeout(() => {
      isDragging = false;
    }, 150);

    if (from === null || isNaN(from) || from === targetIndex) {
      return;
    }

    const to = targetIndex;
    if (from < 0 || from >= steps.length || to < 0 || to >= steps.length) return;

    const moved = steps[from];
    const reordered = [...steps];
    const [item] = reordered.splice(from, 1);
    reordered.splice(to, 0, item);

    const proposedSteps = reordered.map((s, idx) => ({
      ...s,
      sequenceOrder: idx + 1
    }));

    pendingReorder = {
      movedStep: moved,
      fromIndex: from,
      toIndex: to,
      newSteps: proposedSteps
    };
    showReorderConfirmModal = true;
  }

  // Touch handlers for mobile devices
  let touchFromIndex = /** @type {number|null} */ (null);
  let touchTargetIndex = /** @type {number|null} */ (null);

  /**
   * @param {number} index
   * @param {TouchEvent} e
   */
  function handleTouchStart(index, e) {
    if (e.touches.length !== 1) return;
    touchFromIndex = index;
    touchTargetIndex = index;
    draggedStepIndex = index;
    isDragging = true;
  }

  /**
   * @param {TouchEvent} e
   */
  function handleTouchMove(e) {
    if (touchFromIndex === null || e.touches.length !== 1) return;
    if (e.cancelable) {
      e.preventDefault();
    }
    const touch = e.touches[0];
    const element = document.elementFromPoint(touch.clientX, touch.clientY);
    if (!element) return;

    const stepEl = element.closest('[data-step-index]');
    if (stepEl instanceof HTMLElement && stepEl.dataset.stepIndex !== undefined) {
      const idx = parseInt(stepEl.dataset.stepIndex, 10);
      if (!isNaN(idx) && idx >= 0 && idx < steps.length) {
        touchTargetIndex = idx;
        if (dragOverStepIndex !== idx) {
          dragOverStepIndex = idx;
        }
      }
    }
  }

  /**
   * @param {TouchEvent} e
   */
  function handleTouchEnd(e) {
    if (touchFromIndex === null) return;
    const from = touchFromIndex;
    const to = touchTargetIndex;

    touchFromIndex = null;
    touchTargetIndex = null;
    draggedStepIndex = null;
    dragOverStepIndex = null;
    setTimeout(() => {
      isDragging = false;
    }, 150);

    if (to === null || from === to || from < 0 || from >= steps.length || to < 0 || to >= steps.length) {
      return;
    }

    const moved = steps[from];
    const reordered = [...steps];
    const [item] = reordered.splice(from, 1);
    reordered.splice(to, 0, item);

    const proposedSteps = reordered.map((s, idx) => ({
      ...s,
      sequenceOrder: idx + 1
    }));

    pendingReorder = {
      movedStep: moved,
      fromIndex: from,
      toIndex: to,
      newSteps: proposedSteps
    };
    showReorderConfirmModal = true;
  }

  function handleTouchCancel() {
    touchFromIndex = null;
    touchTargetIndex = null;
    draggedStepIndex = null;
    dragOverStepIndex = null;
    isDragging = false;
  }

  function cancelReorder() {
    showReorderConfirmModal = false;
    pendingReorder = null;
  }

  async function confirmReorder() {
    if (!pendingReorder) return;
    const { movedStep, toIndex, newSteps } = pendingReorder;
    submittingReorder = true;
    try {
      await reorderManufacturingSteps(newSteps.map(s => ({
        id: s.id,
        sequenceOrder: s.sequenceOrder
      })));
      steps = newSteps;
      showSuccessToast(`Reordered '${movedStep.name}' to sequence #${toIndex + 1}!`);
      showReorderConfirmModal = false;
      pendingReorder = null;
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to save new step sequence.';
      showReorderConfirmModal = false;
      pendingReorder = null;
    } finally {
      submittingReorder = false;
    }
  }

  // Icon mapper for steps
  /** @type {Record<string, string>} */
  const stepIconMap = {
    'CUT': 'content_cut',
    'STITCH': 'webhook',
    'WASH': 'local_laundry_service',
    'FIN': 'iron',
    'PACK': 'inventory_2',
    'QUAL': 'verified',
    'INSP': 'search_check',
    'DISP': 'local_shipping'
  };

  import { onDestroy } from 'svelte';
  import { headerStore } from '$lib/stores/headerStore.js';

  $effect(() => {
    headerStore.setHeader({
      title: 'Manufacturing Steps',
      subtitle: 'Sequence & Routing',
      icon: 'alt_route',
      actions: [
        {
          label: 'Add Step',
          icon: 'add',
          variant: 'primary',
          onclick: () => openCreateModal()
        },
        {
          label: 'Refresh',
          icon: 'refresh',
          variant: 'outline',
          onclick: () => fetchSteps()
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
<main class="max-w-[1200px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4 pb-24">

  <!-- Unified 3-Metric KPI Summary Strip -->
  <section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden">
    <div class="grid grid-cols-3 divide-x divide-outline-variant/40 p-1.5 sm:p-3 text-center">
      <!-- 1. Total Steps -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-primary hidden min-[380px]:inline">alt_route</span>
          <span>Total Steps</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-on-surface font-mono mt-0.5 sm:mt-1">
          {steps.length}
        </div>
      </div>

      <!-- 2. Active WIP -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-emerald-600 hidden min-[380px]:inline">stacks</span>
          <span>Active WIP</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-emerald-700 dark:text-emerald-400 font-mono mt-0.5 sm:mt-1 truncate max-w-full">
          {steps.filter(s => !s.isTerminating && s.isActive !== false).reduce((sum, s) => sum + (s.totalQuantity || 0), 0).toLocaleString()} <span class="text-[9px] sm:text-xs font-normal">Pcs</span>
        </div>
      </div>

      <!-- 3. Terminating Steps -->
      <div class="p-1 sm:p-2 flex flex-col items-center justify-center">
        <div class="flex items-center gap-1 text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">
          <span class="material-symbols-outlined text-[12px] sm:text-[14px] text-amber-600 hidden min-[380px]:inline">flag_circle</span>
          <span>Terminating</span>
        </div>
        <div class="text-xs min-[360px]:text-sm sm:text-xl md:text-2xl font-bold text-amber-800 dark:text-amber-300 font-mono mt-0.5 sm:mt-1">
          {steps.filter(s => s.isTerminating).length}
        </div>
      </div>
    </div>
  </section>

  <!-- Manufacturing Pipeline Section -->
  <section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden p-3 sm:p-5 space-y-3">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between border-b border-outline-variant/60 pb-2.5 gap-1 sm:gap-2">
      <div>
        <h2 class="text-sm sm:text-base font-bold flex items-center gap-1.5 text-on-surface">
          <span class="material-symbols-outlined text-primary text-[18px] sm:text-[20px]">view_timeline</span>
          <span>Manufacturing Sequence</span>
          <span class="text-xs font-mono font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full ml-1">
            {steps.length}
          </span>
        </h2>
        <p class="text-[11px] text-on-surface-variant mt-0.5 flex items-center gap-1">
          <span class="material-symbols-outlined text-[13px] text-primary/80">drag_indicator</span>
          Drag handle to reorder pipeline sequence. Click step to view lots.
        </p>
      </div>
      <div class="hidden sm:flex items-center gap-2">
        <span class="text-[11px] text-on-surface-variant font-mono bg-surface-container-low px-2.5 py-1 rounded-full border border-outline-variant/70">
          {steps.filter(s => (s.totalQuantity || 0) > 0).length} active stages
        </span>
      </div>
    </div>

    {#if loading}
      <div class="p-10 text-center text-on-surface-variant flex flex-col items-center gap-2.5">
        <span class="material-symbols-outlined animate-spin text-[32px] text-primary">sync</span>
        <span class="text-xs sm:text-sm font-medium">Loading manufacturing sequence...</span>
      </div>
    {:else if steps.length > 0}
      <!-- Mobile View: Compact Pipeline Cards (below md) -->
      <div class="block md:hidden space-y-1.5">
        {#each steps as step, index (step.id)}
          <div
            data-step-index={index}
            draggable="true"
            ondragstart={(e) => handleDragStart(index, e)}
            ondragover={handleDragOver}
            ondragenter={(e) => handleDragEnter(index, e)}
            ondrop={(e) => handleDrop(index, e)}
            ondragend={handleDragEnd}
            role="button"
            tabindex="0"
            onclick={() => { if (!isDragging) window.location.href = `/lots?step=${step.id}`; }}
            onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && !isDragging && (window.location.href = `/lots?step=${step.id}`)}
            class="bg-surface px-3 py-2.5 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-2xs hover:shadow-xs transition-all cursor-pointer flex items-center justify-between gap-2 active:scale-[0.99] select-none {draggedStepIndex === index ? 'opacity-30 bg-primary/5 border-dashed border-primary' : ''} {dragOverStepIndex === index && draggedStepIndex !== index ? 'ring-2 ring-primary ring-inset bg-primary/10 shadow-md' : ''}"
            title="Click to view lots at '{step.name}', or drag grip to reorder"
          >
            <!-- Left: Drag Grip | Seq # | Icon | Name + Code | Status Flags -->
            <div class="flex items-center gap-1.5 min-w-0">
              <!-- Drag Grip -->
              <div
                role="button"
                tabindex="0"
                class="text-on-surface-variant/40 hover:text-primary active:text-primary cursor-grab active:cursor-grabbing p-1 -ml-1 shrink-0 rounded flex items-center justify-center touch-none select-none active:bg-primary/20"
                ontouchstart={(e) => handleTouchStart(index, e)}
                ontouchmove={handleTouchMove}
                ontouchend={handleTouchEnd}
                ontouchcancel={handleTouchCancel}
                onclick={(e) => e.stopPropagation()}
                onkeydown={(e) => e.stopPropagation()}
                aria-label="Drag to reorder sequence"
                title="Touch & drag to reorder"
              >
                <span class="material-symbols-outlined text-[18px] pointer-events-none select-none">drag_indicator</span>
              </div>

              <!-- Sequence Number Badge -->
              <span class="inline-flex items-center justify-center w-5 h-5 rounded-md bg-primary/10 text-primary font-bold font-mono text-[11px] shrink-0 select-none">
                #{step.sequenceOrder}
              </span>

              <!-- Step Icon -->
              <div class="w-6 h-6 rounded-lg bg-surface-container-low flex items-center justify-center text-primary shrink-0">
                <span class="material-symbols-outlined text-[15px] pointer-events-none select-none">
                  {stepIconMap[step.code] || 'work'}
                </span>
              </div>

              <!-- Name & Code -->
              <div class="min-w-0 truncate">
                <div class="flex items-center gap-1 truncate">
                  <span class="font-bold text-xs sm:text-sm text-on-surface truncate select-none">{step.name}</span>
                  <span class="font-mono text-[10px] text-primary/80 font-bold uppercase shrink-0">({step.code})</span>
                </div>
              </div>

              {#if step.isTerminating}
                <span class="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[9px] font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300 select-none shrink-0" title="Terminating Step">
                  <span class="material-symbols-outlined text-[11px] text-amber-600 pointer-events-none">flag</span>
                  Final
                </span>
              {:else if step.isActive === false}
                <span class="px-1.5 py-0.5 rounded text-[9px] font-bold bg-zinc-100 dark:bg-zinc-800 text-zinc-500 border border-zinc-300 select-none shrink-0">
                  Inactive
                </span>
              {/if}
            </div>

            <!-- Right: Quantity Pill, Lot Count & Edit Button -->
            <div class="flex items-center gap-2 shrink-0">
              <!-- Quantity & Lot Count Badge -->
              {#if (step.totalQuantity || 0) > 0}
                <div class="flex flex-col items-end leading-none font-mono">
                  <span class="text-xs font-bold text-primary">
                    {(step.totalQuantity || 0).toLocaleString()} Pcs
                  </span>
                  <span class="text-[9px] text-on-surface-variant font-medium mt-0.5">
                    {step.lotsAtStep?.length || 0} {step.lotsAtStep?.length === 1 ? 'lot' : 'lots'}
                  </span>
                </div>
              {:else}
                <span class="text-[11px] text-on-surface-variant/50 font-mono">
                  0 Pcs
                </span>
              {/if}

              <button
                type="button"
                onclick={(e) => { e.stopPropagation(); openEditModal(step); }}
                title="Edit Step"
                class="p-1 rounded-lg border border-outline-variant/70 hover:border-primary text-on-surface-variant hover:text-primary transition-colors cursor-pointer active:scale-95 shrink-0"
              >
                <span class="material-symbols-outlined text-[14px] pointer-events-none">edit</span>
              </button>

              <span class="material-symbols-outlined text-[16px] text-on-surface-variant/40 group-hover:text-primary transition-colors pointer-events-none">
                chevron_right
              </span>
            </div>
          </div>
        {/each}
      </div>

      <!-- Desktop View: Sleek Pipeline List (md and up) -->
      <div class="hidden md:block w-full overflow-hidden rounded-xl border border-outline-variant/70 bg-surface">
        <!-- Table Header -->
        <div class="grid grid-cols-[40px_60px_100px_1fr_100px_140px_110px_70px] items-center px-4 py-2.5 bg-surface-container-low border-b border-outline-variant/70 text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
          <div class="text-center"></div>
          <div class="text-center">Seq</div>
          <div>Code</div>
          <div>Step Name</div>
          <div class="text-center">Active Lots</div>
          <div class="text-right">Total WIP Qty</div>
          <div class="text-center">Type</div>
          <div class="text-right">Action</div>
        </div>

        <!-- Table Rows -->
        <div class="divide-y divide-outline-variant/40 text-on-surface">
          {#each steps as step, index (step.id)}
            <div
              data-step-index={index}
              draggable="true"
              ondragstart={(e) => handleDragStart(index, e)}
              ondragover={handleDragOver}
              ondragenter={(e) => handleDragEnter(index, e)}
              ondrop={(e) => handleDrop(index, e)}
              ondragend={handleDragEnd}
              role="button"
              tabindex="0"
              onclick={() => { if (!isDragging) window.location.href = `/lots?step=${step.id}`; }}
              onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && !isDragging && (window.location.href = `/lots?step=${step.id}`)}
              class="grid grid-cols-[40px_60px_100px_1fr_100px_140px_110px_70px] items-center px-4 py-3 transition-all cursor-pointer group select-none {draggedStepIndex === index ? 'opacity-30 bg-primary/5 border-dashed border-primary scale-[0.99]' : 'hover:bg-surface-container-low/60'} {dragOverStepIndex === index && draggedStepIndex !== index ? 'ring-2 ring-primary ring-inset bg-primary/10' : ''}"
              title="Click to view lots at '{step.name}', or drag grip to reorder"
            >
              <!-- Drag Handle -->
              <div
                role="button"
                tabindex="0"
                class="flex items-center justify-center text-on-surface-variant/40 hover:text-primary transition-colors cursor-grab active:cursor-grabbing p-1 rounded touch-none select-none"
                ontouchstart={(e) => handleTouchStart(index, e)}
                ontouchmove={handleTouchMove}
                ontouchend={handleTouchEnd}
                ontouchcancel={handleTouchCancel}
                onclick={(e) => e.stopPropagation()}
                onkeydown={(e) => e.stopPropagation()}
                aria-label="Drag handle"
                title="Drag to reorder step sequence"
              >
                <span class="material-symbols-outlined text-[18px] pointer-events-none select-none">drag_indicator</span>
              </div>

              <!-- Sequence Order Badge -->
              <div class="text-center">
                <span class="inline-flex items-center justify-center w-6 h-6 rounded-md bg-primary/10 text-primary font-bold font-mono text-xs group-hover:scale-105 transition-transform select-none">
                  #{step.sequenceOrder}
                </span>
              </div>

              <!-- Code -->
              <div class="flex items-center gap-1.5 min-w-0">
                <span class="material-symbols-outlined text-[18px] text-primary/80 group-hover:text-primary transition-colors pointer-events-none select-none shrink-0">
                  {stepIconMap[step.code] || 'work'}
                </span>
                <span class="font-mono font-bold text-xs text-primary tracking-wide group-hover:underline truncate select-none">
                  {step.code}
                </span>
              </div>

              <!-- Name -->
              <div class="font-bold text-sm text-on-surface group-hover:text-primary transition-colors truncate select-none pr-3">
                {step.name}
              </div>

              <!-- Active Lots Count -->
              <div class="text-center font-mono text-xs select-none">
                {#if (step.lotsAtStep?.length || 0) > 0}
                  <span class="px-2 py-0.5 rounded-full bg-surface-container-low text-on-surface font-semibold border border-outline-variant/60">
                    {step.lotsAtStep.length} {step.lotsAtStep.length === 1 ? 'lot' : 'lots'}
                  </span>
                {:else}
                  <span class="text-on-surface-variant/40">—</span>
                {/if}
              </div>

              <!-- Total Quantity -->
              <div class="text-right whitespace-nowrap select-none">
                {#if (step.totalQuantity || 0) > 0}
                  <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-primary/10 text-primary font-bold text-sm font-mono tracking-wide">
                    {(step.totalQuantity || 0).toLocaleString()} Pcs
                  </span>
                {:else}
                  <span class="text-xs text-on-surface-variant/50 font-mono">0 Pcs</span>
                {/if}
              </div>

              <!-- Terminating Indicator -->
              <div class="text-center select-none">
                {#if step.isTerminating}
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300 dark:border-amber-700">
                    <span class="material-symbols-outlined text-[13px] text-amber-600 pointer-events-none">flag</span>
                    Final Step
                  </span>
                {:else if step.isActive === false}
                  <span class="px-2 py-0.5 rounded-full text-[11px] font-bold bg-zinc-100 dark:bg-zinc-800 text-zinc-500 border border-zinc-300 dark:border-zinc-700">
                    Inactive
                  </span>
                {:else}
                  <span class="text-xs text-on-surface-variant/50 font-mono">Standard</span>
                {/if}
              </div>

              <!-- Actions -->
              <div class="text-right whitespace-nowrap">
                <button
                  type="button"
                  onclick={(e) => { e.stopPropagation(); openEditModal(step); }}
                  class="px-2.5 py-1 rounded-lg border border-outline-variant/70 hover:border-primary text-on-surface-variant hover:text-primary font-bold text-xs flex items-center gap-1 ml-auto transition-colors cursor-pointer active:scale-95"
                >
                  <span class="material-symbols-outlined text-[14px] pointer-events-none">edit</span>
                  Edit
                </button>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {:else}
      <div class="p-10 text-center text-on-surface-variant text-xs sm:text-sm bg-surface-container-low rounded-xl border border-dashed border-outline-variant">
        <span class="material-symbols-outlined text-[32px] text-on-surface-variant/50 mb-1.5 block">alt_route</span>
        No manufacturing steps found. Click "Add Step" in the header to configure your first step.
      </div>
    {/if}
  </section>
</main>

<!-- Create Step Modal -->
{#if showCreateModal}
  <div class="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="bg-surface dark:bg-surface-dim w-full max-w-md rounded-2xl border border-outline-variant shadow-xl p-6 space-y-5 animate-scale-up">
      <div class="flex items-center justify-between border-b border-outline-variant pb-3">
        <h3 class="font-bold text-lg text-on-surface flex items-center gap-2">
          <span class="material-symbols-outlined text-primary">add_circle</span>
          Add Manufacturing Step
        </h3>
        <button onclick={() => showCreateModal = false} class="text-on-surface-variant hover:text-on-surface">
          <span class="material-symbols-outlined">close</span>
        </button>
      </div>

      {#if modalError}
        <div class="p-3 bg-red-50 dark:bg-red-950/40 border border-red-200 text-red-700 text-xs rounded-lg">
          {modalError}
        </div>
      {/if}

      <form onsubmit={handleCreateStep} class="space-y-4">
        <div>
          <label for="step-code" class="block text-xs font-bold font-mono uppercase text-on-surface-variant mb-1">Step Code *</label>
          <input
            id="step-code"
            type="text"
            bind:value={newCode}
            placeholder="e.g. QUAL, PACK, STITCH"
            maxlength="20"
            required
            class="w-full px-3 py-2 rounded-lg border border-outline-variant bg-surface text-on-surface font-mono font-bold uppercase text-sm focus:outline-none focus:ring-2 focus:ring-primary/50"
          />
          <p class="text-[11px] text-on-surface-variant mt-1">Unique short identifier for the step (uppercase).</p>
        </div>

        <div>
          <label for="step-name" class="block text-xs font-bold font-mono uppercase text-on-surface-variant mb-1">Step Name *</label>
          <input
            id="step-name"
            type="text"
            bind:value={newName}
            placeholder="e.g. Quality Inspection, Packing & Boxing"
            required
            class="w-full px-3 py-2 rounded-lg border border-outline-variant bg-surface text-on-surface text-sm focus:outline-none focus:ring-2 focus:ring-primary/50"
          />
        </div>

        <div>
          <label for="seq-order" class="block text-xs font-bold font-mono uppercase text-on-surface-variant mb-1">Sequence Order *</label>
          <input
            id="seq-order"
            type="number"
            min="1"
            bind:value={newSequenceOrder}
            required
            class="w-full px-3 py-2 rounded-lg border border-outline-variant bg-surface text-on-surface text-sm focus:outline-none focus:ring-2 focus:ring-primary/50 font-mono"
          />
          <p class="text-[11px] text-on-surface-variant mt-1">Numerical position in the manufacturing pipeline.</p>
        </div>

        <!-- Terminating Step Checkbox -->
        <div class="p-3 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-xl space-y-1">
          <label class="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              bind:checked={newIsTerminating}
              class="w-4 h-4 text-amber-600 rounded border-amber-300 focus:ring-amber-500"
            />
            <span class="font-bold text-xs text-amber-900 dark:text-amber-200 flex items-center gap-1">
              <span class="material-symbols-outlined text-[16px] text-amber-600">flag</span>
              Mark as Terminating Step
            </span>
          </label>
          <p class="text-[11px] text-amber-700 dark:text-amber-400 pl-6 leading-tight">
            Quantities residing at terminating steps are treated as finished product and excluded from active WIP and active lot counts.
          </p>
        </div>

        <div class="flex justify-end gap-2 pt-2 border-t border-outline-variant">
          <button
            type="button"
            onclick={() => showCreateModal = false}
            class="px-4 py-2 rounded-lg border border-outline-variant text-on-surface-variant font-bold text-xs hover:bg-surface-container-low cursor-pointer"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={submitting}
            class="px-4 py-2 rounded-lg bg-primary text-on-primary font-bold text-xs hover:bg-primary/90 shadow-sm flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
          >
            {#if submitting}
              <span class="material-symbols-outlined text-[16px] animate-spin">sync</span>
            {/if}
            Save Step
          </button>
        </div>
      </form>
    </div>
  </div>
{/if}

<!-- Edit Step Modal -->
{#if showEditModal && editingStep}
  <div class="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="bg-surface dark:bg-surface-dim w-full max-w-md rounded-2xl border border-outline-variant shadow-xl p-6 space-y-5 animate-scale-up">
      <div class="flex items-center justify-between border-b border-outline-variant pb-3">
        <h3 class="font-bold text-lg text-on-surface flex items-center gap-2">
          <span class="material-symbols-outlined text-primary">edit_note</span>
          Edit Step ({editingStep.code})
        </h3>
        <button onclick={() => showEditModal = false} class="text-on-surface-variant hover:text-on-surface">
          <span class="material-symbols-outlined">close</span>
        </button>
      </div>

      {#if modalError}
        <div class="p-3 bg-red-50 dark:bg-red-950/40 border border-red-200 text-red-700 text-xs rounded-lg">
          {modalError}
        </div>
      {/if}

      <form onsubmit={handleUpdateStep} class="space-y-4">
        <div>
          <label for="edit-code" class="block text-xs font-bold font-mono uppercase text-on-surface-variant mb-1">Step Code</label>
          <input
            id="edit-code"
            type="text"
            value={editingStep.code}
            disabled
            class="w-full px-3 py-2 rounded-lg border border-outline-variant bg-surface-container-low text-on-surface-variant font-mono font-bold text-sm cursor-not-allowed opacity-75"
          />
          <p class="text-[11px] text-on-surface-variant mt-1">Step code is unique and immutable.</p>
        </div>

        <div>
          <label for="edit-name" class="block text-xs font-bold font-mono uppercase text-on-surface-variant mb-1">Step Name *</label>
          <input
            id="edit-name"
            type="text"
            bind:value={editName}
            required
            class="w-full px-3 py-2 rounded-lg border border-outline-variant bg-surface text-on-surface text-sm focus:outline-none focus:ring-2 focus:ring-primary/50"
          />
        </div>

        <div>
          <label for="edit-seq" class="block text-xs font-bold font-mono uppercase text-on-surface-variant mb-1">Sequence Order *</label>
          <input
            id="edit-seq"
            type="number"
            min="1"
            bind:value={editSequenceOrder}
            required
            class="w-full px-3 py-2 rounded-lg border border-outline-variant bg-surface text-on-surface text-sm focus:outline-none focus:ring-2 focus:ring-primary/50 font-mono"
          />
        </div>

        <!-- Terminating Step Checkbox -->
        <div class="p-3 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-xl space-y-1">
          <label class="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              bind:checked={editIsTerminating}
              class="w-4 h-4 text-amber-600 rounded border-amber-300 focus:ring-amber-500"
            />
            <span class="font-bold text-xs text-amber-900 dark:text-amber-200 flex items-center gap-1">
              <span class="material-symbols-outlined text-[16px] text-amber-600">flag</span>
              Mark as Terminating Step
            </span>
          </label>
          <p class="text-[11px] text-amber-700 dark:text-amber-400 pl-6 leading-tight">
            Quantities residing at terminating steps are treated as finished product and excluded from active WIP and active lot counts.
          </p>
        </div>

        <!-- Is Active Checkbox -->
        <div class="flex items-center gap-2 pt-1">
          <input
            id="edit-active"
            type="checkbox"
            bind:checked={editIsActive}
            class="w-4 h-4 text-primary rounded border-outline-variant focus:ring-primary"
          />
          <label for="edit-active" class="font-bold text-xs text-on-surface cursor-pointer">
            Step is Active
          </label>
        </div>

        <div class="flex justify-end gap-2 pt-2 border-t border-outline-variant">
          <button
            type="button"
            onclick={() => showEditModal = false}
            class="px-4 py-2 rounded-lg border border-outline-variant text-on-surface-variant font-bold text-xs hover:bg-surface-container-low cursor-pointer"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={submitting}
            class="px-4 py-2 rounded-lg bg-primary text-on-primary font-bold text-xs hover:bg-primary/90 shadow-sm flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
          >
            {#if submitting}
              <span class="material-symbols-outlined text-[16px] animate-spin">sync</span>
            {/if}
            Update Step
          </button>
        </div>
      </form>
    </div>
  </div>
{/if}

<!-- Reorder Confirmation Modal -->
{#if showReorderConfirmModal && pendingReorder}
  <div class="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="bg-surface dark:bg-surface-dim w-full max-w-md rounded-2xl border border-outline-variant shadow-xl p-6 space-y-5 animate-scale-up">
      <div class="flex items-center justify-between border-b border-outline-variant pb-3">
        <h3 class="font-bold text-lg text-on-surface flex items-center gap-2">
          <span class="material-symbols-outlined text-primary">swap_vert</span>
          Confirm Step Sequence Change
        </h3>
        <button onclick={cancelReorder} class="text-on-surface-variant hover:text-on-surface">
          <span class="material-symbols-outlined">close</span>
        </button>
      </div>

      <div class="space-y-3 text-sm text-on-surface">
        <p class="leading-relaxed">
          Do you really want to move step <strong class="text-primary font-bold">{pendingReorder.movedStep.name} ({pendingReorder.movedStep.code})</strong>
          from sequence position <span class="font-mono font-bold px-1.5 py-0.5 rounded bg-surface-container-low border border-outline-variant">#{pendingReorder.fromIndex + 1}</span>
          to <span class="font-mono font-bold px-1.5 py-0.5 rounded bg-primary/10 text-primary border border-primary/20">#{pendingReorder.toIndex + 1}</span>?
        </p>

        <!-- Preview of new sequence -->
        <div class="p-3 bg-surface-container-low rounded-xl border border-outline-variant space-y-1.5">
          <div class="text-[11px] font-bold uppercase tracking-wider text-on-surface-variant font-mono mb-1">
            New Sequence Order Preview
          </div>
          <div class="space-y-1 max-h-48 overflow-y-auto pr-1">
            {#each pendingReorder.newSteps as s}
              <div class="flex items-center justify-between text-xs px-2.5 py-1.5 rounded-lg {s.id === pendingReorder.movedStep.id ? 'bg-primary text-on-primary font-bold shadow-xs' : 'bg-surface border border-outline-variant/60 text-on-surface'}">
                <div class="flex items-center gap-2 min-w-0">
                  <span class="font-mono font-bold text-[11px] w-5 text-center {s.id === pendingReorder.movedStep.id ? 'text-on-primary' : 'text-primary'}">
                    #{s.sequenceOrder}
                  </span>
                  <span class="truncate">{s.name} ({s.code})</span>
                </div>
                {#if s.id === pendingReorder.movedStep.id}
                  <span class="text-[10px] uppercase font-bold tracking-wider opacity-90">Moved</span>
                {/if}
              </div>
            {/each}
          </div>
        </div>
      </div>

      <div class="flex justify-end gap-2 pt-2 border-t border-outline-variant">
        <button
          type="button"
          onclick={cancelReorder}
          class="px-4 py-2 rounded-lg border border-outline-variant text-on-surface-variant font-bold text-xs hover:bg-surface-container-low cursor-pointer"
        >
          Cancel
        </button>
        <button
          type="button"
          onclick={confirmReorder}
          disabled={submittingReorder}
          class="px-4 py-2 rounded-lg bg-primary text-on-primary font-bold text-xs hover:bg-primary/90 shadow-sm flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
        >
          {#if submittingReorder}
            <span class="material-symbols-outlined text-[16px] animate-spin">sync</span>
          {/if}
          OK
        </button>
      </div>
    </div>
  </div>
{/if}

