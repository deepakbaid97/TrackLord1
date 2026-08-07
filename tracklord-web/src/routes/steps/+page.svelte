<script>
  import { onMount } from 'svelte';
  import { getManufacturingSteps, createManufacturingStep, updateManufacturingStep } from '$lib/api.js';

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
      steps = await getManufacturingSteps();
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
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD ERP</div>
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
  <section class="grid grid-cols-1 sm:grid-cols-3 gap-4">
    <!-- Total Steps -->
    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-primary/10 rounded-xl text-primary">
        <span class="material-symbols-outlined text-[28px]">alt_route</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">Total Steps</div>
        <div class="text-3xl font-bold text-on-surface leading-tight">{steps.length}</div>
      </div>
    </div>

    <!-- Active Steps -->
    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-emerald-500/10 rounded-xl text-emerald-600">
        <span class="material-symbols-outlined text-[28px]">play_circle</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">Active Steps</div>
        <div class="text-3xl font-bold text-on-surface leading-tight">
          {steps.filter(s => s.isActive !== false).length}
        </div>
      </div>
    </div>

    <!-- Terminating Steps -->
    <div class="bg-surface p-5 rounded-xl border border-outline-variant shadow-sm flex items-center gap-4">
      <div class="p-3 bg-amber-500/10 rounded-xl text-amber-600">
        <span class="material-symbols-outlined text-[28px]">flag_circle</span>
      </div>
      <div>
        <div class="text-xs text-on-surface-variant uppercase font-mono font-bold tracking-wider">Terminating Steps</div>
        <div class="text-3xl font-bold text-on-surface leading-tight">
          {steps.filter(s => s.isTerminating).length}
        </div>
        <div class="text-[10px] text-on-surface-variant font-mono">Excluded from Active WIP</div>
      </div>
    </div>
  </section>

  <!-- Steps Table / Directory -->
  <section class="bg-surface p-6 rounded-xl border border-outline-variant shadow-sm space-y-4">
    <div class="flex items-center justify-between border-b border-outline-variant pb-4">
      <div>
        <h2 class="font-headline-md text-headline-md font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-primary text-[22px]">view_timeline</span>
          Manufacturing Sequence
        </h2>
        <p class="text-xs text-on-surface-variant mt-0.5">
          Steps define the workflow pipeline. Mark a step as <strong>Terminating</strong> if quantities at that step represent completed production.
        </p>
      </div>
      <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-2.5 py-1 rounded-full border border-outline-variant">
        {steps.length} steps configured
      </span>
    </div>

    {#if loading}
      <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
        <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
        <span>Loading manufacturing steps...</span>
      </div>
    {:else if steps.length > 0}
      <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-4 py-3 text-center w-16">Seq</th>
              <th class="px-4 py-3">Code</th>
              <th class="px-4 py-3">Step Name</th>
              <th class="px-4 py-3 text-center">Status</th>
              <th class="px-4 py-3 text-center">Terminating Step?</th>
              <th class="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/50 text-on-surface">
            {#each steps as step}
              <tr class="hover:bg-surface-container-low/60 transition-colors">
                <!-- Sequence Order Badge -->
                <td class="px-4 py-3.5 text-center">
                  <span class="inline-flex items-center justify-center w-7 h-7 rounded-full bg-primary/10 text-primary font-bold font-mono text-xs">
                    {step.sequenceOrder}
                  </span>
                </td>

                <!-- Code -->
                <td class="px-4 py-3.5">
                  <div class="flex items-center gap-2">
                    <span class="material-symbols-outlined text-[20px] text-primary/80">
                      {stepIconMap[step.code] || 'work'}
                    </span>
                    <span class="font-mono font-bold text-sm text-primary tracking-wide">
                      {step.code}
                    </span>
                  </div>
                </td>

                <!-- Name -->
                <td class="px-4 py-3.5 font-bold text-on-surface">
                  {step.name}
                </td>

                <!-- Status Badge -->
                <td class="px-4 py-3.5 text-center">
                  {#if step.isActive !== false}
                    <span class="px-2.5 py-1 rounded-full text-[11px] font-bold uppercase tracking-wider bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300">
                      Active
                    </span>
                  {:else}
                    <span class="px-2.5 py-1 rounded-full text-[11px] font-bold uppercase tracking-wider bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400">
                      Inactive
                    </span>
                  {/if}
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
                    onclick={() => openEditModal(step)}
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
