<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { createMaterialLot } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    onSuccess,
    onError
  } = $props();

  let form = $state({
    lotNumber: '',
    materialName: '',
    initialQuantity: 500,
    unit: 2, // Meters
    unitRate: 0
  });

  let isSubmitting = $state(false);

  const unitOptions = [
    { label: 'Meters', value: 2 },
    { label: 'Pieces', value: 1 },
    { label: 'Kilograms', value: 3 },
    { label: 'Yards', value: 5 }
  ];

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    isSubmitting = true;
    try {
      const payload = {
        lotNumber: form.lotNumber.trim(),
        materialItemId: '00000000-0000-0000-0000-000000000002',
        materialName: form.materialName.trim(),
        initialQuantity: Number(form.initialQuantity),
        unit: Number(form.unit),
        unitRate: Number(form.unitRate) || 0
      };

      const res = await createMaterialLot(payload);
      isOpen = false;
      const lotNum = form.lotNumber;
      form = {
        lotNumber: '',
        materialName: '',
        initialQuantity: 500,
        unit: 2,
        unitRate: 0
      };

      if (onSuccess) onSuccess({ ...payload, id: res?.id, lotNumber: lotNum });
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Register Raw Material Lot">
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

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save" disabled={isSubmitting}>
        {isSubmitting ? 'Registering...' : 'Register Material'}
      </Button>
    </div>
  </form>
</Modal>
