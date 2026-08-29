<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { updateProductionLot } from '$lib/api.js';

  /**
   * @type {{
   *   isOpen: boolean,
   *   lot?: any,
   *   clients?: Array<any>,
   *   onSuccess?: (res: any) => void,
   *   onError?: (err: any) => void,
   *   onCreateClientClick?: () => void
   * }}
   */
  let {
    isOpen = $bindable(false),
    lot = null,
    clients = [],
    onSuccess,
    onError,
    onCreateClientClick
  } = $props();

  let selectedClientId = $state('');
  let formDate = $state('');
  let formRemarks = $state('');
  let isSubmitting = $state(false);
  let modalError = $state('');

  const clientOptions = $derived([
    { label: 'None (Unassigned / Internal)', value: '' },
    ...clients.map(c => ({
      label: `${c.name} (${c.clientType})`,
      value: c.id
    }))
  ]);

  $effect(() => {
    if (isOpen && lot) {
      selectedClientId = lot.clientId || '';
      // Format date to YYYY-MM-DD for date input
      const dateSource = lot.createdAtUtc || lot.createdDate || lot.creationDate;
      if (dateSource) {
        try {
          const d = new Date(dateSource);
          formDate = d.toISOString().split('T')[0];
        } catch {
          formDate = '';
        }
      } else {
        formDate = '';
      }
      formRemarks = lot.remarks || '';
      modalError = '';
    }
  });

  /** @param {any} [e] */
  async function handleSubmit(e) {
    if (e && e.preventDefault) e.preventDefault();
    const lotId = lot?.id || lot?.lotId;
    if (!lot || !lotId) {
      modalError = 'Production lot identifier could not be resolved.';
      return;
    }

    isSubmitting = true;
    modalError = '';

    try {
      const selectedClientObj = clients.find(c => c.id === selectedClientId);

      let utcDate = null;
      if (formDate) {
        // Construct ISO date at 00:00:00 UTC or preserve existing time
        const [year, month, day] = formDate.split('-').map(Number);
        const d = new Date(Date.UTC(year, month - 1, day, 12, 0, 0));
        utcDate = d.toISOString();
      }

      const payload = {
        clientId: selectedClientId || null,
        clientName: selectedClientObj?.name || null,
        createdAtUtc: utcDate,
        remarks: formRemarks ? formRemarks.trim() : null
      };

      await updateProductionLot(lotId, payload);

      isOpen = false;
      if (onSuccess) onSuccess({ ...lot, ...payload, id: lotId, lotId });
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to update production lot.';
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title={`Edit Production Lot: ${lot?.lotNumber || ''}`}>
  <form onsubmit={handleSubmit} class="space-y-4">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    <!-- Read-Only Overview Card -->
    <div class="p-3.5 rounded-xl bg-surface-container-low border border-outline-variant/70 space-y-2">
      <div class="text-[11px] font-bold text-on-surface-variant font-mono uppercase tracking-wider">
        Production Lot Overview (Fixed)
      </div>
      <div class="grid grid-cols-2 sm:grid-cols-3 gap-2.5 text-xs font-mono">
        <div>
          <span class="text-on-surface-variant text-[10px] uppercase block">Lot Number</span>
          <span class="font-bold text-primary text-sm">#{lot?.lotNumber || '—'}</span>
        </div>
        <div>
          <span class="text-on-surface-variant text-[10px] uppercase block">Initial Quantity</span>
          <span class="font-bold text-on-surface text-sm">{lot?.initialQuantity || lot?.totalQuantity || '0'} Pcs</span>
        </div>
        <div>
          <span class="text-on-surface-variant text-[10px] uppercase block">Status</span>
          <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-primary/10 text-primary border border-primary/20 inline-block mt-0.5">
            {lot?.status || 'Active'}
          </span>
        </div>
      </div>
    </div>

    <!-- Editable Fields -->
    <div class="space-y-3 pt-1">
      <!-- 1. Client / Customer Selector -->
      <div class="space-y-1">
        <div class="flex items-center justify-between">
          <label for="edit-lot-client" class="text-xs font-bold text-on-surface uppercase font-mono">
            Assigned Client / Customer
          </label>
          {#if onCreateClientClick}
            <button
              type="button"
              onclick={() => {
                isOpen = false;
                onCreateClientClick();
              }}
              class="text-[11px] font-semibold text-primary hover:underline flex items-center gap-0.5 cursor-pointer"
            >
              <span class="material-symbols-outlined text-[14px]">add</span>
              <span>New Partner</span>
            </button>
          {/if}
        </div>

        <Select
          id="edit-lot-client"
          bind:value={selectedClientId}
          options={clientOptions}
          icon="badge"
        />
        <p class="text-[11px] text-on-surface-variant">
          Assign or reassign the owner client/subcontractor for this production batch.
        </p>
      </div>

      <!-- 2. Creation Date Picker -->
      <div class="space-y-1">
        <label for="edit-lot-date" class="text-xs font-bold text-on-surface uppercase font-mono">
          Lot Creation Date
        </label>
        <div class="relative flex items-center">
          <span class="material-symbols-outlined absolute left-3 text-on-surface-variant text-[18px] pointer-events-none">
            calendar_today
          </span>
          <input
            id="edit-lot-date"
            type="date"
            bind:value={formDate}
            class="w-full bg-surface-container-low border border-outline-variant/70 rounded-xl pl-9.5 pr-3 py-2 text-xs sm:text-sm font-semibold text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
          />
        </div>
        <p class="text-[11px] text-on-surface-variant">
          Update the creation timestamp recorded for this production batch.
        </p>
      </div>

      <!-- 3. Remarks (Optional) -->
      <Input
        label="Batch Remarks (Optional)"
        bind:value={formRemarks}
        placeholder="e.g. Expedited order from Jayshree"
        icon="notes"
        required={false}
      />
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" onclick={handleSubmit} icon="save" disabled={isSubmitting}>
        {isSubmitting ? 'Saving...' : 'Save Changes'}
      </Button>
    </div>
  </form>
</Modal>
