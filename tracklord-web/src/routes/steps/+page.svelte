<script>
  import { onMount } from 'svelte';
  import {
    getManufacturingSteps,
    createManufacturingStep,
    updateManufacturingStep,
    getProductionLots,
    getLotJourney
  } from '$lib/api.js';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  /** @type {any[]} */
  let steps = $state([]);

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
</script>

<!-- Header -->
<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">alt_route</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD</div>
      <div class="text-xs text-on-surface-variant">Manufacturing Steps</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
    <button
      onclick={() => fetchSteps()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh
    </button>
    <button
      onclick={openCreateModal}
      class="px-4 py-1.5 rounded-lg bg-primary text-on-primary font-bold text-xs hover:bg-primary/90 shadow-sm flex items-center gap-1.5 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[18px]">add</span>
      Add Step
    </button>
  </div>
</header>

<!-- Main Content -->
<main class="max-w-[1200px] mx-auto p-4 md:p-8 space-y-6 pt-20 pb-24">

  <!-- Success Toast -->
  {#if successMessage}
    <div class="p-4 bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 rounded-xl text-emerald-800 dark:text-emerald-300 text-sm flex items-center justify-between shadow-sm animate-fade-in">
      <div class="flex items-center gap-2">
        <span class="material-symbols-outlined text-emerald-600 text-[20px]">check_circle</span>
        <span>{successMessage}</span>
      </div>
      <button onclick={() => successMessage = ''} class="text-emerald-600 hover:text-emerald-800">
        <span class="material-symbols-outlined text-[18px]">close</span>
      </button>
    </div>
  {/if}

  <!-- Error Alert -->
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

  <!-- Summary Cards -->
  <section class="grid grid-cols-3 gap-1.5 sm:gap-4">
    <!-- Total Steps -->
    <div class="bg-surface p-2.5 sm:p-5 rounded-xl border border-outline-variant shadow-sm flex flex-col sm:flex-row items-start sm:items-center gap-1.5 sm:gap-4 min-w-0">
      <div class="p-1.5 sm:p-3 bg-primary/10 rounded-lg sm:rounded-xl text-primary shrink-0">
        <span class="material-symbols-outlined text-[20px] sm:text-[28px]">alt_route</span>
      </div>
      <div class="min-w-0">
        <div class="text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider truncate">Total Steps</div>
        <div class="text-base sm:text-3xl font-bold text-on-surface leading-tight truncate">{steps.length}</div>
      </div>
    </div>

    <!-- Active Quantity across steps -->
    <div class="bg-surface p-2.5 sm:p-5 rounded-xl border border-outline-variant shadow-sm flex flex-col sm:flex-row items-start sm:items-center gap-1.5 sm:gap-4 min-w-0">
      <div class="p-1.5 sm:p-3 bg-emerald-500/10 rounded-lg sm:rounded-xl text-emerald-600 shrink-0">
        <span class="material-symbols-outlined text-[20px] sm:text-[28px]">stacks</span>
      </div>
      <div class="min-w-0">
        <div class="text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider truncate">Active Qty</div>
        <div class="text-base sm:text-3xl font-bold text-on-surface leading-tight truncate font-mono">
          {steps.filter(s => !s.isTerminating).reduce((sum, s) => sum + (s.totalQuantity || 0), 0).toLocaleString()} Pcs
        </div>
      </div>
    </div>

    <!-- Terminating Steps -->
    <div class="bg-surface p-2.5 sm:p-5 rounded-xl border border-outline-variant shadow-sm flex flex-col sm:flex-row items-start sm:items-center gap-1.5 sm:gap-4 min-w-0">
      <div class="p-1.5 sm:p-3 bg-amber-500/10 rounded-lg sm:rounded-xl text-amber-600 shrink-0">
        <span class="material-symbols-outlined text-[20px] sm:text-[28px]">flag_circle</span>
      </div>
      <div class="min-w-0">
        <div class="text-[9px] sm:text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider truncate">Terminating</div>
        <div class="text-base sm:text-3xl font-bold text-on-surface leading-tight truncate">
          {steps.filter(s => s.isTerminating).length}
        </div>
        <div class="hidden sm:block text-[10px] text-on-surface-variant font-mono truncate">Excluded from Active WIP</div>
      </div>
    </div>
  </section>

  <!-- Steps Table / Directory -->
  <section class="bg-surface p-4 sm:p-6 rounded-xl border border-outline-variant shadow-sm space-y-4">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between border-b border-outline-variant pb-3 gap-1 sm:gap-2">
      <div>
        <h2 class="text-base sm:text-xl font-bold flex items-center gap-2 text-on-surface">
          <span class="material-symbols-outlined text-primary text-[20px] sm:text-[22px]">view_timeline</span>
          Manufacturing Sequence & WIP
        </h2>
      </div>
      <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2.5 py-1 rounded-full border border-outline-variant w-fit">
        {steps.length} steps configured
      </span>
    </div>

    {#if loading}
      <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
        <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
        <span>Loading manufacturing steps & lots...</span>
      </div>
    {:else if steps.length > 0}
      <!-- Mobile View: Card Stack Column (below md) -->
      <div class="block md:hidden space-y-2">
        {#each steps as step}
          <div
            role="button"
            tabindex="0"
            onclick={() => window.location.href = `/lots?step=${step.id}`}
            onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (window.location.href = `/lots?step=${step.id}`)}
            class="bg-surface px-3 py-2.5 rounded-lg border border-outline-variant hover:border-primary/50 shadow-xs hover:shadow-sm transition-all cursor-pointer space-y-2 active:scale-[0.99] select-none"
            title="Click to view lots at '{step.name}' in Lots Directory"
          >
            <!-- Card Line 1: Seq # | Icon | Code & Name | Terminating Badge | Edit -->
            <div class="flex items-center justify-between gap-2">
              <div class="flex items-center gap-2 min-w-0">
                <span class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-primary/10 text-primary font-bold font-mono text-xs shrink-0">
                  {step.sequenceOrder}
                </span>
                <span class="material-symbols-outlined text-[18px] text-primary/80 shrink-0">
                  {stepIconMap[step.code] || 'work'}
                </span>
                <div class="min-w-0">
                  <span class="font-bold text-sm text-on-surface truncate">{step.name}</span>
                  <span class="font-mono text-xs text-primary font-bold ml-1">({step.code})</span>
                </div>
              </div>

              <div class="flex items-center gap-1.5 shrink-0">
                {#if step.isTerminating}
                  <span class="inline-flex items-center gap-0.5 px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300">
                    <span class="material-symbols-outlined text-[12px] text-amber-600">flag</span>
                    Terminating
                  </span>
                {/if}
                <button
                  onclick={(e) => { e.stopPropagation(); openEditModal(step); }}
                  title="Edit Step"
                  class="p-1 rounded-md border border-outline-variant hover:border-primary text-on-surface-variant hover:text-primary transition-colors cursor-pointer"
                >
                  <span class="material-symbols-outlined text-[15px]">edit</span>
                </button>
              </div>
            </div>

            <!-- Card Line 2: WIP Quantity & Lot Count -->
            <div class="flex items-center justify-between text-xs pt-1.5 border-t border-dashed border-outline-variant/50">
              <span class="text-on-surface-variant font-mono">
                {step.lotsAtStep?.length || 0} {step.lotsAtStep?.length === 1 ? 'active lot' : 'active lots'}
              </span>
              <span class="font-mono font-bold text-sm text-primary">
                {(step.totalQuantity || 0).toLocaleString()} Pcs
              </span>
            </div>
          </div>
        {/each}
      </div>

      <!-- Desktop View: Table (md and up) -->
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-4 py-3 text-center w-14">Seq</th>
              <th class="px-4 py-3">Code</th>
              <th class="px-4 py-3">Step Name</th>
              <th class="px-4 py-3 text-right">Total Qty</th>
              <th class="px-4 py-3 text-center">Terminating Step?</th>
              <th class="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#each steps as step}
              <tr
                onclick={() => window.location.href = `/lots?step=${step.id}`}
                class="hover:bg-surface-container-low/60 transition-colors cursor-pointer group"
                title="Click to view lots at '{step.name}' in Lots Directory"
              >
                <!-- Sequence Order Badge -->
                <td class="px-4 py-3.5 text-center">
                  <span class="inline-flex items-center justify-center w-7 h-7 rounded-full bg-primary/10 text-primary font-bold font-mono text-xs group-hover:scale-110 transition-transform">
                    {step.sequenceOrder}
                  </span>
                </td>

                <!-- Code -->
                <td class="px-4 py-3.5">
                  <div class="flex items-center gap-2">
                    <span class="material-symbols-outlined text-[20px] text-primary/80 group-hover:text-primary transition-colors">
                      {stepIconMap[step.code] || 'work'}
                    </span>
                    <span class="font-mono font-bold text-sm text-primary tracking-wide group-hover:underline">
                      {step.code}
                    </span>
                  </div>
                </td>

                <!-- Name -->
                <td class="px-4 py-3.5 font-bold text-on-surface group-hover:text-primary transition-colors">
                  {step.name}
                </td>

                <!-- Total Quantity -->
                <td class="px-4 py-3.5 text-right whitespace-nowrap">
                  <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-primary/10 text-primary font-bold text-base font-mono tracking-wide">
                    {(step.totalQuantity || 0).toLocaleString()} Pcs
                  </span>
                  <div class="text-[11px] text-on-surface-variant font-mono mt-1">
                    {step.lotsAtStep?.length || 0} {step.lotsAtStep?.length === 1 ? 'lot' : 'lots'}
                  </div>
                </td>

                <!-- Terminating Indicator -->
                <td class="px-4 py-3.5 text-center">
                  {#if step.isTerminating}
                    <span class="inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300 dark:border-amber-700">
                      <span class="material-symbols-outlined text-[15px] text-amber-600">flag</span>
                      Terminating
                    </span>
                  {:else}
                    <span class="text-xs text-on-surface-variant/60 font-mono">—</span>
                  {/if}
                </td>

                <!-- Actions -->
                <td class="px-4 py-3.5 text-right">
                  <button
                    onclick={(e) => { e.stopPropagation(); openEditModal(step); }}
                    class="px-3 py-1.5 rounded-lg border border-outline-variant hover:border-primary text-on-surface-variant hover:text-primary font-bold text-xs flex items-center gap-1 ml-auto transition-colors cursor-pointer"
                  >
                    <span class="material-symbols-outlined text-[16px]">edit</span>
                    Edit
                  </button>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {:else}
      <div class="p-12 text-center text-on-surface-variant text-sm bg-surface-container-low rounded-xl border border-dashed border-outline-variant">
        <span class="material-symbols-outlined text-[36px] text-on-surface-variant/50 mb-2 block">alt_route</span>
        No manufacturing steps found. Click "Add Step" to configure your first step.
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
