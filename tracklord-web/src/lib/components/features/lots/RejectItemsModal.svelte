<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { rejectItems } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    lotId = '',
    steps = [],
    clients = [],
    onSuccess,
    onError
  } = $props();

  let form = $state({
    stepId: '',
    targetStepId: '',
    quantityRejected: 1,
    reason: 'Damaged during operation - return for rework',
    clientId: '',
    createdBy: 'Inspector'
  });

  $effect(() => {
    if (steps.length > 0 && !form.stepId) {
      form.stepId = steps[0].id;
      form.targetStepId = steps[0].id;
    }
    if (clients.length > 0 && !form.clientId) {
      form.clientId = clients[0].id;
    }
  });

  let isSubmitting = $state(false);

  let stepOptions = $derived(
    steps.map(s => ({
      label: `${s.name} (${s.code})`,
      value: s.id
    }))
  );

  let clientOptions = $derived([
    { label: '-- No Client Attribution --', value: '' },
    ...clients.map(c => ({
      label: `${c.name} (${c.clientType})`,
      value: c.id
    }))
  ]);

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!lotId) return;
    isSubmitting = true;
    try {
      const payload = {
        stepId: form.stepId,
        targetStepId: form.targetStepId || form.stepId,
        quantityRejected: Number(form.quantityRejected),
        reason: form.reason,
        rejectionDate: new Date().toISOString(),
        clientId: form.clientId || null,
        createdBy: form.createdBy
      };

      await rejectItems(lotId, payload);
      isOpen = false;
      if (onSuccess) onSuccess();
    } catch (/** @type {any} */ err) {
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Log Rejection / Rework Transfer">
  <form onsubmit={handleSubmit} class="space-y-4">
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <Select
        label="Defect Found at Step"
        bind:value={form.stepId}
        options={stepOptions}
        icon="report_problem"
        required={true}
      />

      <Select
        label="Return for Rework to"
        bind:value={form.targetStepId}
        options={stepOptions}
        icon="replay"
        required={true}
      />
    </div>

    <Input
      label="Quantity Rejected (Pcs)"
      type="number"
      bind:value={form.quantityRejected}
      placeholder="e.g. 5"
      icon="numbers"
      required={true}
    />

    <Input
      label="Defect / Rejection Reason"
      bind:value={form.reason}
      placeholder="e.g. Stitch misalignment"
      icon="notes"
      required={true}
    />

    <Select
      label="Responsible Subcontractor / Partner (Optional)"
      bind:value={form.clientId}
      options={clientOptions}
      icon="badge"
      required={false}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="danger" type="submit" icon="report" disabled={isSubmitting}>
        {isSubmitting ? 'Logging...' : 'Log Rejection'}
      </Button>
    </div>
  </form>
</Modal>
