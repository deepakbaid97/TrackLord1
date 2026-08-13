<script>
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import Button from '$lib/components/ui/Button.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import { getMaterialLotHistory, editMaterialConsumption } from '$lib/api.js';

  let materialLotId = $derived($page.params.id);

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');

  /** @type {any} */
  let historyData = $state(null);

  // Edit modal state
  let isEditModalOpen = $state(false);
  let editForm = $state({
    id: '',
    materialLotNumber: '',
    materialName: '',
    quantityConsumed: 0,
    remarks: ''
  });

  onMount(async () => {
    if (materialLotId) {
      await fetchHistory();
    }
  });

  async function fetchHistory() {
    loading = true;
    errorMessage = '';
    try {
      historyData = await getMaterialLotHistory(materialLotId);
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load material consumption history.';
    } finally {
      loading = false;
    }
  }

  /** @param {any} c */
  function openEditModal(c) {
    editForm.id = c.id || c.Id;
    editForm.materialLotNumber = historyData?.lot?.lotNumber || '';
    editForm.materialName = historyData?.lot?.materialName || '';
    editForm.quantityConsumed = c.quantityConsumed;
    editForm.remarks = c.remarks || '';
    isEditModalOpen = true;
  }

  /** @param {SubmitEvent} e */
  async function handleEditSubmit(e) {
    e.preventDefault();
    errorMessage = '';
    try {
      await editMaterialConsumption(editForm.id, {
        quantityConsumed: Number(editForm.quantityConsumed),
        remarks: editForm.remarks.trim()
      });
      successMessage = 'Material consumption record updated successfully!';
      isEditModalOpen = false;
      await fetchHistory();
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to update material consumption.';
    }
  }

  let lot = $derived(historyData?.lot);
  let consumptions = $derived(historyData?.consumptions || []);
  let remainingQty = $derived(lot ? (lot.remainingQuantity ?? lot.initialQuantity ?? 0) : 0);
  let initialQty = $derived(lot ? (lot.initialQuantity ?? 0) : 0);
  let usagePercent = $derived(initialQty > 0 ? (remainingQty / initialQty) * 100 : 0);
</script>

<!-- Top Navigation Header -->
<header class="fixed top-0 w-full z-40 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <button
      onclick={() => goto('/materials')}
      class="p-2 rounded-full hover:bg-surface-container-low text-on-surface-variant transition-colors flex items-center justify-center cursor-pointer"
      title="Back to Materials List"
    >
      <span class="material-symbols-outlined text-[22px]">arrow_back</span>
    </button>
    <div>
      <div class="font-bold text-lg text-primary leading-none flex items-center gap-2">
        <span>TRACKLORD</span>
        <span class="text-xs text-on-surface-variant font-normal">/ Raw Material History</span>
      </div>
      <div class="text-xs text-on-surface-variant font-mono">{lot ? lot.lotNumber : 'Loading...'}</div>
    </div>
  </div>

  <div class="flex items-center gap-2">
    <button
      onclick={() => fetchHistory()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh History
    </button>
  </div>
</header>

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

  {#if loading}
    <div class="p-16 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[40px] text-primary">sync</span>
      <span>Loading material consumption history...</span>
    </div>
  {:else if historyData && lot}
    <!-- Material Lot Header Card -->
    <section class="bg-surface p-6 rounded-2xl border border-outline-variant shadow-sm flex flex-col md:flex-row justify-between items-start md:items-center gap-6">
      <div class="space-y-1">
        <div class="flex items-center gap-3">
          <h1 class="text-3xl font-bold text-primary font-mono">{lot.lotNumber}</h1>
          <span class="px-3 py-1 rounded-full text-xs font-bold font-mono uppercase tracking-wider {remainingQty <= 0 ? 'bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-300' : 'bg-emerald-100 dark:bg-emerald-950 text-emerald-800 dark:text-emerald-300'}">
            {remainingQty <= 0 ? 'Depleted' : 'In Stock'}
          </span>
        </div>
        {#if lot.materialName}
          <p class="text-base font-semibold text-on-surface flex items-center gap-1.5">
            <span class="material-symbols-outlined text-tertiary text-[20px]">category</span>
            {lot.materialName}
          </p>
        {/if}
      </div>

      <div class="flex flex-wrap gap-4 w-full md:w-auto">
        <div class="bg-surface-container-low border border-outline-variant p-4 rounded-xl text-center flex-1 min-w-[120px]">
          <div class="text-xs text-on-surface-variant font-mono uppercase">Initial Qty</div>
          <div class="text-xl font-bold text-on-surface font-mono">{initialQty.toLocaleString()} {lot.unit}</div>
        </div>

        <div class="bg-surface-container-low border border-outline-variant p-4 rounded-xl text-center flex-1 min-w-[120px]">
          <div class="text-xs text-on-surface-variant font-mono uppercase">Remaining Stock</div>
          <div class="text-xl font-bold font-mono {remainingQty <= 0 ? 'text-red-500' : 'text-emerald-600'}">
            {remainingQty.toLocaleString()} {lot.unit}
          </div>
        </div>

        <div class="bg-surface-container-low border border-outline-variant p-4 rounded-xl text-center flex-1 min-w-[120px]">
          <div class="text-xs text-on-surface-variant font-mono uppercase">Rate per Unit</div>
          <div class="text-xl font-bold text-on-surface font-mono">
            {lot.unitRate ? `₹${Number(lot.unitRate).toLocaleString('en-IN', { minimumFractionDigits: 2 })}` : '—'}
          </div>
        </div>

        <div class="bg-surface-container-low border border-outline-variant p-4 rounded-xl text-center flex-1 min-w-[140px]">
          <div class="text-xs text-on-surface-variant font-mono uppercase">Total Consumed Value</div>
          <div class="text-xl font-bold text-emerald-700 dark:text-emerald-400 font-mono">
            ₹{Number(historyData.totalConsumptionCost || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
        </div>
      </div>
    </section>

    <!-- Consumption History Ledger -->
    <section class="bg-surface p-6 rounded-2xl border border-outline-variant shadow-sm space-y-4">
      <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center border-b border-outline-variant pb-3 gap-2">
        <div>
          <h2 class="text-xl font-bold text-on-surface flex items-center gap-2">
            <span class="material-symbols-outlined text-primary text-[24px]">history</span>
            Material Consumption History
          </h2>
          <p class="text-xs text-on-surface-variant mt-0.5">
            Audit trail of all manufacturing steps that consumed this raw material lot.
          </p>
        </div>
        <span class="text-xs text-on-surface-variant font-mono bg-surface-container-low px-3 py-1 rounded-full border border-outline-variant">
          Total Entries: {historyData.totalConsumptionCount}
        </span>
      </div>

      {#if consumptions.length > 0}
        <div class="w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface shadow-sm">
          <table class="w-full text-left border-collapse text-sm">
            <thead>
              <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                <th class="px-4 py-3.5">Date & Time</th>
                <th class="px-4 py-3.5">Production Lot</th>
                <th class="px-4 py-3.5">Manufacturing Step</th>
                <th class="px-4 py-3.5 text-right">Quantity Consumed</th>
                <th class="px-4 py-3.5 text-right">Rate / Unit</th>
                <th class="px-4 py-3.5 text-right">Total Price</th>
                <th class="px-4 py-3.5">User / Operator</th>
                <th class="px-4 py-3.5">Remarks</th>
                <th class="px-4 py-3.5 text-right">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-outline-variant/50 text-on-surface">
              {#each consumptions as c}
                <tr class="hover:bg-surface-container-low/60 transition-colors">
                  <td class="px-4 py-3.5 whitespace-nowrap text-xs font-mono">
                    {new Date(c.consumptionDate).toLocaleString()}
                  </td>
                  <td class="px-4 py-3.5 whitespace-nowrap">
                    <a
                      href="/inventory?lot={c.productionLotId}"
                      class="font-bold text-primary hover:underline font-mono flex items-center gap-1"
                    >
                      <span class="material-symbols-outlined text-[16px]">tag</span>
                      {c.productionLotNumber}
                    </a>
                  </td>
                  <td class="px-4 py-3.5 whitespace-nowrap font-medium text-on-surface">
                    <span class="px-2.5 py-1 bg-surface-container-high rounded-lg text-xs font-semibold">
                      {c.stepName}
                    </span>
                  </td>
                  <td class="px-4 py-3.5 text-right font-bold text-primary font-mono whitespace-nowrap">
                    {c.quantityConsumed} {c.unit}
                  </td>
                  <td class="px-4 py-3.5 text-right font-mono text-on-surface-variant whitespace-nowrap">
                    ₹{Number(c.unitRate || 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                  </td>
                  <td class="px-4 py-3.5 text-right font-mono font-bold text-emerald-700 dark:text-emerald-400 whitespace-nowrap">
                    ₹{Number(c.totalPrice || (c.quantityConsumed * (c.unitRate || 0))).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </td>
                  <td class="px-4 py-3.5 whitespace-nowrap text-xs text-on-surface-variant">
                    {c.createdBy || 'Operator'}
                  </td>
                  <td class="px-4 py-3.5 text-xs text-on-surface-variant max-w-[200px] truncate" title={c.remarks}>
                    {c.remarks || '—'}
                  </td>
                  <td class="px-4 py-3.5 text-right whitespace-nowrap">
                    <button
                      onclick={() => openEditModal(c)}
                      class="px-2.5 py-1 text-xs font-semibold bg-surface-container text-on-surface hover:bg-primary hover:text-white rounded transition-colors cursor-pointer"
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
        <div class="p-12 text-center text-on-surface-variant bg-surface-container-low rounded-xl border border-dashed border-outline-variant">
          <span class="material-symbols-outlined text-[40px] text-on-surface-variant/40 mb-2 block">history_toggle_off</span>
          <h3 class="text-base font-bold text-on-surface">No consumption entries recorded yet</h3>
          <p class="text-xs text-on-surface-variant mt-1">This material lot has not been consumed in any production lot so far.</p>
        </div>
      {/if}
    </section>
  {/if}
</main>

<!-- Edit Consumption Modal -->
<Modal bind:isOpen={isEditModalOpen} title="Edit Material Consumption Record">
  <form onsubmit={handleEditSubmit} class="space-y-4">
    <Input
      label="Quantity Consumed"
      type="number"
      step="any"
      bind:value={editForm.quantityConsumed}
      icon="numbers"
      required={true}
    />

    <Input
      label="Remarks / Notes"
      bind:value={editForm.remarks}
      placeholder="Optional consumption notes..."
      icon="notes"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isEditModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save">
        Save Changes
      </Button>
    </div>
  </form>
</Modal>
