<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { updateMaterialLot, deleteMaterialLot } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    material = null,
    onSuccess,
    onDelete,
    onError
  } = $props();

  const unitOptions = [
    { label: 'Meters', value: 2 },
    { label: 'Pieces', value: 1 },
    { label: 'Kilograms', value: 3 },
    { label: 'Yards', value: 5 }
  ];

  function getUnitValue(unitInput) {
    if (typeof unitInput === 'number') return unitInput;
    if (typeof unitInput === 'string') {
      const match = unitOptions.find(o => o.label.toLowerCase() === unitInput.toLowerCase());
      if (match) return match.value;
      const parsed = parseInt(unitInput, 10);
      if (!isNaN(parsed)) return parsed;
    }
    return 2;
  }

  let form = $state({
    id: '',
    lotNumber: '',
    materialName: '',
    initialQuantity: 0,
    unit: 2,
    unitRate: 0
  });

  $effect(() => {
    if (material) {
      form = {
        id: material.id,
        lotNumber: material.lotNumber || '',
        materialName: material.materialName || '',
        initialQuantity: material.initialQuantity || 0,
        unit: getUnitValue(material.unit),
        unitRate: material.unitRate || 0
      };
    }
  });

  let isSubmitting = $state(false);
  let isDeleting = $state(false);

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!form.id) return;
    isSubmitting = true;
    try {
      const payload = {
        lotNumber: form.lotNumber.trim(),
        materialName: form.materialName.trim(),
        initialQuantity: Number(form.initialQuantity),
        unit: Number(form.unit),
        unitRate: Number(form.unitRate) || 0
      };

      await updateMaterialLot(form.id, payload);
      isOpen = false;
      if (onSuccess) onSuccess(form.lotNumber);
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }

  async function handleDelete() {
    if (!form.id) return;
    if (!confirm(`Are you sure you want to delete material lot '${form.lotNumber}'?`)) return;

    isDeleting = true;
    try {
      await deleteMaterialLot(form.id);
      isOpen = false;
      if (onDelete) onDelete(form.lotNumber);
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isDeleting = false;
    }
  }
</script>

<Modal bind:isOpen title="Edit Raw Material Lot">
  <form onsubmit={handleSubmit} class="space-y-4">
    <Input
      label="Material Lot Number"
      bind:value={form.lotNumber}
      placeholder="e.g. FAB-2026-001"
      icon="tag"
      required={true}
    />

    <Input
      label="Product / Material Name"
      bind:value={form.materialName}
      placeholder="e.g. Denim Grey Cloth 300GSM"
      icon="category"
      required={true}
    />

    <Input
      label="Initial Quantity"
      type="number"
      step={Number(form.unit) === 1 ? '1' : 'any'}
      bind:value={form.initialQuantity}
      placeholder={Number(form.unit) === 1 ? 'e.g. 500' : 'e.g. 500.5'}
      icon="numbers"
      required={true}
    />

    <Select
      label="Unit of Measurement"
      bind:value={form.unit}
      options={unitOptions}
      icon="square_foot"
      required={true}
    />

    <Input
      label="Rate per Unit (₹)"
      type="number"
      step="any"
      bind:value={form.unitRate}
      placeholder="e.g. 150.00"
      icon="payments"
      required={false}
    />

    <div class="flex justify-between items-center pt-3 border-t border-outline-variant">
      <Button
        variant="danger"
        icon="delete"
        disabled={isDeleting}
        onclick={handleDelete}
      >
        {isDeleting ? 'Deleting...' : 'Delete Material'}
      </Button>

      <div class="flex gap-3">
        <Button variant="outline" onclick={() => isOpen = false}>
          Cancel
        </Button>
        <Button variant="primary" type="submit" icon="save" disabled={isSubmitting}>
          {isSubmitting ? 'Saving...' : 'Save Changes'}
        </Button>
      </div>
    </div>
  </form>
</Modal>
