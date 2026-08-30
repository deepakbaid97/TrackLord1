<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import ClientModeGate from '$lib/components/ui/ClientModeGate.svelte';
  import FeatureGate from '$lib/components/ui/FeatureGate.svelte';
  import { moveLot } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    lotId = '',
    steps = [],
    clients = [],
    initialFromStepId = '',
    initialQuantity = 0,
    onSuccess,
    onError,
    onCreateClientClick
  } = $props();

  let form = $state({
    fromStepId: '',
    toStepId: '',
    quantity: 10,
    clientId: '',
    clientName: '',
    remarks: '',
    referenceNumber: ''
  });

  $effect(() => {
    if (isOpen) {
      form.fromStepId = initialFromStepId || (steps[0] ? steps[0].id : '');
      form.quantity = initialQuantity || 0;
      form.remarks = '';
      form.referenceNumber = '';
      form.clientId = '';
      form.clientName = '';
      if (steps.length > 1) {
        const fromIdx = steps.findIndex(s => s.id === form.fromStepId);
        const nextStep = steps[fromIdx + 1] || steps.find(s => s.id !== form.fromStepId);
        if (nextStep) form.toStepId = nextStep.id;
      }
    }
  });

  let isSubmitting = $state(false);

  let fromStepOptions = $derived(
    steps.map(s => ({
      label: `${s.name} (${s.code})`,
      value: s.id
    }))
  );

  let toStepOptions = $derived(
    steps.map(s => ({
      label: `${s.name} (${s.code})`,
      value: s.id
    }))
  );

  let clientOptions = $derived([
    { label: '-- Select Subcontractor / Client (Optional) --', value: '' },
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
        productionLotId: lotId,
        fromStepId: form.fromStepId,
        toStepId: form.toStepId,
        quantity: Number(form.quantity),
        movementDate: new Date().toISOString(),
        clientId: form.clientId || null,
        clientName: selectedClient ? selectedClient.name : (form.clientName || '').trim(),
        createdBy: 'Operator',
        remarks: form.remarks,
        referenceNumber: (form.referenceNumber || '').trim() || null
      };

      await moveLot(payload);
      isOpen = false;
      form.remarks = '';
      form.referenceNumber = '';
      form.clientId = '';
      form.clientName = '';
      if (onSuccess) onSuccess();
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Move Work-in-Progress (WIP)">
  <form onsubmit={handleSubmit} class="space-y-4">
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <Select
        label="From Process Step"
        bind:value={form.fromStepId}
        options={fromStepOptions}
        icon="output"
        required={true}
      />

      <Select
        label="To Destination Step"
        bind:value={form.toStepId}
        options={toStepOptions}
        icon="input"
        required={true}
      />
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <Input
        label="Quantity to Move (Pcs)"
        type="number"
        bind:value={form.quantity}
        placeholder="e.g. 50"
        icon="numbers"
        required={true}
      />

      <Input
        label="Reference Number"
        bind:value={form.referenceNumber}
        placeholder="e.g. REF-001 or DC-884"
        icon="tag"
        required={false}
      />
    </div>

    <ClientModeGate mode="step">
      <div class="space-y-1">
        <div class="flex items-center justify-between">
          <label for="moveClientId" class="text-xs font-bold text-on-surface uppercase font-mono">Assigned Partner / Subcontractor</label>
          {#if onCreateClientClick}
            <button
              type="button"
              onclick={onCreateClientClick}
              class="text-xs text-primary font-bold hover:underline cursor-pointer"
            >
              + Quick Add
            </button>
          {/if}
        </div>
        <Select
          id="moveClientId"
          bind:value={form.clientId}
          options={clientOptions}
          icon="badge"
          required={false}
        />
      </div>
    </ClientModeGate>

    <FeatureGate feature="fields.lots.remarks">
      <Input
        label="Remarks / Movement Notes"
        bind:value={form.remarks}
        placeholder="e.g. Sent to stitching unit 2"
        icon="notes"
        required={false}
      />
    </FeatureGate>

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="move_item" disabled={isSubmitting}>
        {isSubmitting ? 'Recording...' : 'Record Movement'}
      </Button>
    </div>
  </form>
</Modal>
