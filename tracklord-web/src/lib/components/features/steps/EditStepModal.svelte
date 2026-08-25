<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { updateManufacturingStep } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    step = null,
    onSuccess,
    onError
  } = $props();

  let name = $state('');
  let sequenceOrder = $state(1);
  let isTerminating = $state(false);
  let isActive = $state(true);
  let isSubmitting = $state(false);
  let modalError = $state('');

  $effect(() => {
    if (step) {
      name = step.name || '';
      sequenceOrder = step.sequenceOrder ?? 1;
      isTerminating = Boolean(step.isTerminating);
      isActive = step.isActive !== false;
      modalError = '';
    }
  });

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!step) return;
    if (!name.trim()) {
      modalError = 'Step name is required.';
      return;
    }

    isSubmitting = true;
    modalError = '';
    try {
      await updateManufacturingStep(step.id, {
        name: name.trim(),
        sequenceOrder: Number(sequenceOrder),
        isTerminating,
        isActive
      });

      isOpen = false;
      if (onSuccess) onSuccess(step.code);
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to update manufacturing step.';
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Edit Step: {step?.code}">
  <form onsubmit={handleSubmit} class="space-y-4">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    <div class="p-3 rounded-xl bg-surface-container-low border border-outline-variant/60 text-xs flex items-center gap-2 font-mono">
      <span class="material-symbols-outlined text-[16px] text-primary">tag</span>
      <span class="font-bold text-on-surface">Step Code: {step?.code}</span>
    </div>

    <Input
      label="Step Name"
      bind:value={name}
      placeholder="e.g. Fabric Cutting"
      icon="badge"
      required={true}
    />

    <Input
      label="Sequence Order"
      type="number"
      bind:value={sequenceOrder}
      placeholder="e.g. 1"
      icon="format_list_numbered"
      required={true}
    />

    <div class="space-y-2">
      <div class="flex items-center gap-3 p-3 bg-surface-container-low rounded-xl border border-outline-variant/60">
        <input
          type="checkbox"
          id="editIsTerminating"
          bind:checked={isTerminating}
          class="w-4 h-4 text-primary rounded border-outline-variant focus:ring-primary cursor-pointer"
        />
        <label for="editIsTerminating" class="text-xs text-on-surface cursor-pointer select-none">
          <span class="font-bold">Terminating / Completion Step</span>
          <span class="block text-on-surface-variant text-[11px]">Items reaching this step are considered finished output</span>
        </label>
      </div>

      <div class="flex items-center gap-3 p-3 bg-surface-container-low rounded-xl border border-outline-variant/60">
        <input
          type="checkbox"
          id="editIsActive"
          bind:checked={isActive}
          class="w-4 h-4 text-primary rounded border-outline-variant focus:ring-primary cursor-pointer"
        />
        <label for="editIsActive" class="text-xs text-on-surface cursor-pointer select-none">
          <span class="font-bold">Active in Routing Workflow</span>
          <span class="block text-on-surface-variant text-[11px]">Inactive steps are hidden from lot movement dropdowns</span>
        </label>
      </div>
    </div>

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
