<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import ClientModeGate from '$lib/components/ui/ClientModeGate.svelte';
  import FeatureGate from '$lib/components/ui/FeatureGate.svelte';
  import { createProductionLot } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    steps = [],
    clients = [],
    onSuccess,
    onError
  } = $props();

  let form = $state({
    lotNumber: '',
    initialQuantity: 100,
    unit: 1, // Pieces
    initialStepId: '',
    createdBy: 'Operator',
    remarks: '',
    clientId: ''
  });

  let isSubmitting = $state(false);

  $effect(() => {
    if (steps.length > 0 && !form.initialStepId) {
      form.initialStepId = steps[0].id;
    }
  });

  let stepOptions = $derived(
    steps.map(s => ({
      label: `${s.name} (${s.code})`,
      value: s.id
    }))
  );

  let clientOptions = $derived([
    { label: '-- Select Associated Client --', value: '' },
    ...clients.map(c => ({
      label: `${c.name} (${c.clientType})`,
      value: c.id
    }))
  ]);

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    isSubmitting = true;
    try {
      const selectedClient = clients.find(c => c.id === form.clientId);
      const payload = {
        lotNumber: form.lotNumber.trim(),
        itemId: '00000000-0000-0000-0000-000000000001',
        initialQuantity: Number(form.initialQuantity),
        unit: Number(form.unit),
        initialStepId: form.initialStepId || (steps[0] ? steps[0].id : ''),
        createdBy: form.createdBy,
        remarks: form.remarks,
        clientId: form.clientId || null,
        clientName: selectedClient ? selectedClient.name : null
      };

      const res = await createProductionLot(payload);
      isOpen = false;
      const lotNum = payload.lotNumber;
      form.lotNumber = '';
      form.clientId = '';
      form.remarks = '';
      if (onSuccess) onSuccess({ ...payload, id: res?.id, lotNumber: lotNum });
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Create Production Lot">
  <form onsubmit={handleSubmit} class="space-y-4">
    <Input
      label="Lot / Batch Number"
      bind:value={form.lotNumber}
      placeholder="e.g. LOT-2026-001"
      icon="tag"
      required={true}
    />

    <Input
      label="Initial Quantity (Pcs)"
      type="number"
      bind:value={form.initialQuantity}
      placeholder="e.g. 500"
      icon="numbers"
      required={true}
    />

    <Select
      label="Initial Manufacturing Stage"
      bind:value={form.initialStepId}
      options={stepOptions}
      icon="alt_route"
      required={true}
    />

    <ClientModeGate mode="lot">
      <Select
        label="Assign Client / Buyer"
        bind:value={form.clientId}
        options={clientOptions}
        icon="badge"
        required={false}
      />
    </ClientModeGate>

    <FeatureGate feature="fields.lots.remarks">
      <Input
        label="Remarks / Notes"
        bind:value={form.remarks}
        placeholder="e.g. Summer order batch 1"
        icon="notes"
        required={false}
      />
    </FeatureGate>

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save" disabled={isSubmitting}>
        {isSubmitting ? 'Creating...' : 'Create Lot'}
      </Button>
    </div>
  </form>
</Modal>
