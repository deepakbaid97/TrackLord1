<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { consumeMaterial } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    lotId = '',
    materialLots = [],
    steps = [],
    onSuccess,
    onError
  } = $props();

  let form = $state({
    materialLotId: '',
    stepId: '',
    quantityConsumed: 25,
    unit: 2, // Meters
    recordedBy: 'Operator'
  });

  $effect(() => {
    if (materialLots.length > 0 && !form.materialLotId) {
      form.materialLotId = materialLots[0].id;
    }
    if (steps.length > 0 && !form.stepId) {
      form.stepId = steps[0].id;
    }
  });

  let selectedMatLot = $derived(materialLots.find(m => m.id === form.materialLotId));
  let isPiecesUnit = $derived(selectedMatLot && (selectedMatLot.unit === 'Pieces' || selectedMatLot.unit === 1 || selectedMatLot.unitType === 1));

  let matOptions = $derived(
    materialLots.map(m => ({
      label: `${m.lotNumber} - ${m.materialName || 'Material'} (${m.remainingQuantity ?? m.initialQuantity} ${m.unit} avail)`,
      value: m.id
    }))
  );

  let stepOptions = $derived(
    steps.map(s => ({
      label: `${s.name} (${s.code})`,
      value: s.id
    }))
  );

  let isSubmitting = $state(false);

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!lotId) return;

    isSubmitting = true;
    try {
      const payload = {
        productionLotId: lotId,
        materialLotId: form.materialLotId,
        stepId: form.stepId,
        quantityConsumed: Number(form.quantityConsumed),
        unit: Number(selectedMatLot?.unit === 'Pieces' || selectedMatLot?.unit === 1 ? 1 : 2),
        consumptionDate: new Date().toISOString(),
        createdBy: form.recordedBy
      };

      await consumeMaterial(payload);
      isOpen = false;
      if (onSuccess) onSuccess();
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Log Raw Material Consumption">
  <form onsubmit={handleSubmit} class="space-y-4">
    <Select
      label="Select Raw Material Batch"
      bind:value={form.materialLotId}
      options={matOptions}
      icon="texture"
      required={true}
    />

    <Select
      label="Consumption Stage / Step"
      bind:value={form.stepId}
      options={stepOptions}
      icon="alt_route"
      required={true}
    />

    <Input
      label="Quantity Consumed"
      type="number"
      step={isPiecesUnit ? '1' : 'any'}
      bind:value={form.quantityConsumed}
      placeholder={isPiecesUnit ? 'e.g. 50' : 'e.g. 25.5'}
      icon="numbers"
      required={true}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="local_mall" disabled={isSubmitting}>
        {isSubmitting ? 'Logging...' : 'Log Consumption'}
      </Button>
    </div>
  </form>
</Modal>
