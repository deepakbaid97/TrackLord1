<script>
  import { onMount } from 'svelte';
  import {
    getManufacturingSteps,
    reorderManufacturingSteps,
    getProductionLots,
    getLotJourney
  } from '$lib/api.js';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import KpiStrip from '$lib/components/ui/KpiStrip.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import StepCard from '$lib/components/features/steps/StepCard.svelte';
  import CreateStepModal from '$lib/components/features/steps/CreateStepModal.svelte';
  import EditStepModal from '$lib/components/features/steps/EditStepModal.svelte';
  import { STEP_ICON_MAP } from '$lib/services/lotService.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';

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
  /** @type {any} */
  let editingStep = $state(null);

  usePageHeader(() => ({
    title: 'Manufacturing Steps',
    subtitle: 'Sequence & Routing',
    icon: 'alt_route',
    actions: [
      {
        label: 'Add Step',
        icon: 'add',
        variant: 'primary',
        onclick: () => { showCreateModal = true; }
      },
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: 'outline',
        onclick: () => fetchSteps()
      }
    ]
  }));

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

  function showSuccessToast(msg) {
    successMessage = msg;
    setTimeout(() => {
      successMessage = '';
    }, 4000);
  }

  let isDragging = $state(false);

  function handleDragStart(index, e) {
    draggedStepIndex = index;
    isDragging = true;
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
      e.dataTransfer.setData('text/plain', index.toString());
    }
  }

  function handleDragOver(e) {
    e.preventDefault();
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = 'move';
    }
  }

  function handleDragEnter(index, e) {
    e.preventDefault();
    if (draggedStepIndex !== null && dragOverStepIndex !== index) {
      dragOverStepIndex = index;
    }
  }

  function handleDragEnd() {
    draggedStepIndex = null;
    dragOverStepIndex = null;
    setTimeout(() => { isDragging = false; }, 150);
  }

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
    setTimeout(() => { isDragging = false; }, 150);

    if (from === null || isNaN(from) || from === targetIndex) return;

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

  let touchFromIndex = /** @type {number|null} */ (null);
  let touchTargetIndex = /** @type {number|null} */ (null);

  function handleTouchStart(index, e) {
    if (e.touches.length !== 1) return;
    touchFromIndex = index;
    touchTargetIndex = index;
    draggedStepIndex = index;
    isDragging = true;
  }

  function handleTouchMove(e) {
    if (touchFromIndex === null || e.touches.length !== 1) return;
    const touch = e.touches[0];
    const element = document.elementFromPoint(touch.clientX, touch.clientY);
    if (!element) return;
    const cardEl = element.closest('[data-step-index]');
    if (cardEl) {
      const idxAttr = cardEl.getAttribute('data-step-index');
      if (idxAttr !== null) {
        const overIdx = parseInt(idxAttr, 10);
        if (!isNaN(overIdx) && overIdx !== dragOverStepIndex) {
          dragOverStepIndex = overIdx;
          touchTargetIndex = overIdx;
        }
      }
    }
  }

  function handleTouchEnd() {
    if (touchFromIndex !== null && touchTargetIndex !== null && touchFromIndex !== touchTargetIndex) {
      handleDrop(touchTargetIndex, {
        preventDefault: () => {},
        stopPropagation: () => {},
        dataTransfer: null
      });
    }
    touchFromIndex = null;
    touchTargetIndex = null;
    draggedStepIndex = null;
    dragOverStepIndex = null;
    setTimeout(() => { isDragging = false; }, 150);
  }

  function handleTouchCancel() {
    touchFromIndex = null;
    touchTargetIndex = null;
    draggedStepIndex = null;
    dragOverStepIndex = null;
    isDragging = false;
  }

  async function confirmReorder() {
    if (!pendingReorder) return;
    submittingReorder = true;
    try {
      const payload = pendingReorder.newSteps.map((s, idx) => ({
        id: s.id,
        sequenceOrder: idx + 1
      }));
      await reorderManufacturingSteps(payload);
      showReorderConfirmModal = false;
      showSuccessToast('Manufacturing steps sequence reordered successfully!');
      await fetchSteps();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to reorder manufacturing steps.';
      showReorderConfirmModal = false;
    } finally {
      submittingReorder = false;
      pendingReorder = null;
    }
  }

  function cancelReorder() {
    showReorderConfirmModal = false;
    pendingReorder = null;
  }

  function openEditModal(step) {
    editingStep = step;
    showEditModal = true;
  }

  let totalActiveWip = $derived(
    steps.filter(s => !s.isTerminating && s.isActive !== false).reduce((sum, s) => sum + (s.totalQuantity || 0), 0)
  );

  let kpiItems = $derived([
    { label: 'Total Steps', value: steps.length, icon: 'alt_route', color: 'text-primary' },
    { label: 'Active WIP', value: `${totalActiveWip.toLocaleString()} Pcs`, icon: 'stacks', color: 'text-emerald-700 dark:text-emerald-400' },
    { label: 'Terminating', value: steps.filter(s => s.isTerminating).length, icon: 'flag_circle', color: 'text-amber-800 dark:text-amber-300' }
  ]);
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1200px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4 pb-24">
  <!-- Unified 3-Metric KPI Summary Strip -->
  <KpiStrip items={kpiItems} columns={3} />

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
      <LoadingSpinner message="Loading manufacturing sequence..." />
    {:else if steps.length > 0}
      <!-- Mobile View: Compact Pipeline Cards -->
      <div class="block md:hidden space-y-1.5">
        {#each steps as step, index (step.id)}
          <StepCard
            {step}
            {index}
            {isDragging}
            {draggedStepIndex}
            {dragOverStepIndex}
            onEdit={() => openEditModal(step)}
            onDragStart={(e) => handleDragStart(index, e)}
            onDragOver={handleDragOver}
            onDragEnter={(e) => handleDragEnter(index, e)}
            onDrop={(e) => handleDrop(index, e)}
            onDragEnd={handleDragEnd}
            onTouchStart={(e) => handleTouchStart(index, e)}
            onTouchMove={handleTouchMove}
            onTouchEnd={handleTouchEnd}
            onTouchCancel={handleTouchCancel}
          />
        {/each}
      </div>

      <!-- Desktop View: Pipeline Table with Drag Grips -->
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/70">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant/70 text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="w-10 px-3 py-3 text-center">#</th>
              <th class="px-4 py-3">Step Code</th>
              <th class="px-4 py-3">Step Name</th>
              <th class="px-4 py-3 text-center">Step Type</th>
              <th class="px-4 py-3 text-center">Status</th>
              <th class="px-4 py-3 text-right">Active Lots</th>
              <th class="px-4 py-3 text-right">Total WIP Qty</th>
              <th class="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#each steps as step, index (step.id)}
              <tr
                data-step-index={index}
                draggable="true"
                ondragstart={(e) => handleDragStart(index, e)}
                ondragover={handleDragOver}
                ondragenter={(e) => handleDragEnter(index, e)}
                ondrop={(e) => handleDrop(index, e)}
                ondragend={handleDragEnd}
                onclick={() => { if (!isDragging) window.location.href = `/lots?step=${step.id}`; }}
                class="hover:bg-surface-container-low/70 transition-all cursor-pointer group select-none {draggedStepIndex === index ? 'opacity-30 bg-primary/5' : ''} {dragOverStepIndex === index && draggedStepIndex !== index ? 'bg-primary/10 border-t-2 border-primary' : ''}"
                title="Click to view lots at this step, or drag to reorder"
              >
                <!-- Drag Grip & Seq # -->
                <td class="px-3 py-3.5 text-center text-on-surface-variant/40 group-hover:text-primary transition-colors cursor-grab active:cursor-grabbing">
                  <div class="flex items-center justify-center gap-1">
                    <span class="material-symbols-outlined text-[18px]">drag_indicator</span>
                    <span class="font-mono text-xs font-bold text-on-surface">#{step.sequenceOrder}</span>
                  </div>
                </td>

                <td class="px-4 py-3.5 font-bold font-mono text-primary flex items-center gap-2">
                  <div class="w-7 h-7 rounded-lg bg-surface-container-low flex items-center justify-center text-primary shrink-0 group-hover:bg-primary group-hover:text-white transition-colors">
                    <span class="material-symbols-outlined text-[16px]">{STEP_ICON_MAP[step.code] || 'work'}</span>
                  </div>
                  <span class="group-hover:underline">{step.code}</span>
                </td>

                <td class="px-4 py-3.5 font-semibold text-on-surface">{step.name}</td>

                <td class="px-4 py-3.5 text-center">
                  {#if step.isTerminating}
                    <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-100 dark:bg-amber-950 text-amber-800 dark:text-amber-300 border border-amber-300">
                      <span class="material-symbols-outlined text-[13px]">flag</span>
                      Terminating
                    </span>
                  {:else}
                    <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-surface-container-low text-on-surface-variant border border-outline-variant/60">
                      <span class="material-symbols-outlined text-[13px]">trending_flat</span>
                      Intermediate
                    </span>
                  {/if}
                </td>

                <td class="px-4 py-3.5 text-center">
                  {#if step.isActive !== false}
                    <span class="px-2 py-0.5 rounded-full text-xs font-bold bg-emerald-100 dark:bg-emerald-950 text-emerald-800 dark:text-emerald-300">
                      Active
                    </span>
                  {:else}
                    <span class="px-2 py-0.5 rounded-full text-xs font-bold bg-zinc-100 dark:bg-zinc-800 text-zinc-500">
                      Inactive
                    </span>
                  {/if}
                </td>

                <td class="px-4 py-3.5 text-right font-mono font-bold text-on-surface">
                  {step.lotsAtStep?.length || 0}
                </td>

                <td class="px-4 py-3.5 text-right font-mono font-bold text-primary">
                  {(step.totalQuantity || 0).toLocaleString()} Pcs
                </td>

                <td class="px-4 py-3.5 text-right">
                  <button
                    onclick={(e) => { e.stopPropagation(); openEditModal(step); }}
                    class="px-2.5 py-1 text-xs font-semibold text-primary hover:bg-primary/10 rounded-lg transition-colors cursor-pointer"
                  >
                    Edit
                  </button>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {:else}
      <EmptyState icon="alt_route" title="No manufacturing steps configured" message="Click 'Add Step' above to configure your factory steps." />
    {/if}
  </section>
</main>

<!-- Modals -->
<CreateStepModal
  bind:isOpen={showCreateModal}
  defaultSequence={steps.length + 1}
  onSuccess={(code) => {
    showSuccessToast(`Manufacturing step '${code}' created successfully!`);
    fetchSteps();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to create step.';
  }}
/>

<EditStepModal
  bind:isOpen={showEditModal}
  step={editingStep}
  onSuccess={(code) => {
    showSuccessToast(`Step '${code}' updated successfully!`);
    fetchSteps();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to update step.';
  }}
/>

<!-- Reorder Confirmation Modal -->
<Modal bind:isOpen={showReorderConfirmModal} title="Confirm Sequence Reorder">
  {#if pendingReorder}
    <div class="space-y-4">
      <p class="text-xs sm:text-sm text-on-surface">
        Are you sure you want to move step <strong class="text-primary font-mono">{pendingReorder.movedStep.name} ({pendingReorder.movedStep.code})</strong> to position <strong class="font-mono">#{pendingReorder.toIndex + 1}</strong>?
      </p>

      <div class="p-3 bg-surface-container-low rounded-xl border border-outline-variant/60 text-xs space-y-1">
        <span class="font-bold text-on-surface block font-mono">New Manufacturing Order:</span>
        <div class="flex flex-wrap gap-1 items-center pt-1 font-mono">
          {#each pendingReorder.newSteps as s, i}
            <span class="px-2 py-0.5 rounded text-[11px] {s.id === pendingReorder.movedStep.id ? 'bg-primary text-white font-bold' : 'bg-surface border border-outline-variant text-on-surface'}">
              {i + 1}. {s.code}
            </span>
            {#if i < pendingReorder.newSteps.length - 1}
              <span class="text-on-surface-variant/40">→</span>
            {/if}
          {/each}
        </div>
      </div>

      <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
        <Button variant="outline" onclick={cancelReorder}>
          Cancel
        </Button>
        <Button variant="primary" icon="check" disabled={submittingReorder} onclick={confirmReorder}>
          {submittingReorder ? 'Reordering...' : 'Confirm Reorder'}
        </Button>
      </div>
    </div>
  {/if}
</Modal>
