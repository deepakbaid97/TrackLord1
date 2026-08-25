<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { editMaterialConsumption } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    consumption = null,
    onSuccess,
    onError
  } = $props();

  let form = $state({
    id: '',
    materialLotNumber: '',
    materialName: '',
    quantityConsumed: 0,
    remarks: ''
  });

  $effect(() => {
    if (consumption) {
      const rawId = consumption.id || consumption.Id || consumption.consumptionId || consumption.ConsumptionId || '';
      form = {
        id: rawId,
        materialLotNumber: consumption.materialLotNumber || consumption.MaterialLotNumber || '',
        materialName: consumption.materialName || consumption.MaterialName || '',
        quantityConsumed: consumption.quantityConsumed ?? consumption.QuantityConsumed ?? 0,
        remarks: consumption.remarks || consumption.Remarks || ''
      };
    }
  });

  let isSubmitting = $state(false);

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!form.id) return;

    isSubmitting = true;
    try {
      await editMaterialConsumption(form.id, {
        quantityConsumed: Number(form.quantityConsumed),
        remarks: form.remarks.trim()
      });

      isOpen = false;
      if (onSuccess) onSuccess();
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Edit Material Consumption">
  <form onsubmit={handleSubmit} class="space-y-4">
    <div class="p-3 bg-surface-container-low rounded-xl border border-outline-variant/60 text-xs font-mono">
      <div class="font-bold text-on-surface flex items-center gap-1.5">
        <span class="material-symbols-outlined text-[16px] text-primary">texture</span>
        <span>{form.materialLotNumber} {form.materialName ? `• ${form.materialName}` : ''}</span>
      </div>
    </div>

    <Input
      label="Quantity Consumed"
      type="number"
      step="any"
      bind:value={form.quantityConsumed}
      placeholder="e.g. 20"
      icon="numbers"
      required={true}
    />

    <Input
      label="Adjustment Remarks"
      bind:value={form.remarks}
      placeholder="e.g. Corrected consumption typo"
      icon="notes"
      required={false}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save" disabled={isSubmitting}>
        {isSubmitting ? 'Saving...' : 'Save Changes'}
      </Button>
    </div>
  </form>
</Modal>
