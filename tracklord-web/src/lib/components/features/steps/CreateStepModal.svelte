<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { createManufacturingStep } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    defaultSequence = 1,
    onSuccess,
    onError
  } = $props();

  let code = $state('');
  let name = $state('');
  let sequenceOrder = $state(1);
  let isTerminating = $state(false);
  let isSubmitting = $state(false);
  let modalError = $state('');

  $effect(() => {
    if (isOpen) {
      sequenceOrder = defaultSequence;
      modalError = '';
    }
  });

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!code.trim() || !name.trim()) {
      modalError = 'Both Step Code and Step Name are required.';
      return;
    }

    isSubmitting = true;
    modalError = '';
    try {
      await createManufacturingStep({
        code: code.trim().toUpperCase(),
        name: name.trim(),
        sequenceOrder: Number(sequenceOrder),
        isTerminating
      });

      isOpen = false;
      const createdCode = code.trim().toUpperCase();
      code = '';
      name = '';
      isTerminating = false;
      if (onSuccess) onSuccess(createdCode);
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to create manufacturing step.';
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Create Manufacturing Step">
  <form onsubmit={handleSubmit} class="space-y-4">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    <Input
      label="Step Code"
      bind:value={code}
      placeholder="e.g. CUT, STITCH, PACK"
      icon="tag"
      required={true}
    />

    <Input
      label="Step Name"
      bind:value={name}
      placeholder="e.g. Fabric Cutting & Bundling"
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

    <div class="flex items-center gap-3 p-3 bg-surface-container-low rounded-xl border border-outline-variant/60">
      <input
        type="checkbox"
        id="createIsTerminating"
        bind:checked={isTerminating}
        class="w-4 h-4 text-primary rounded border-outline-variant focus:ring-primary cursor-pointer"
      />
      <label for="createIsTerminating" class="text-xs text-on-surface cursor-pointer select-none">
        <span class="font-bold">Terminating / Completion Step</span>
        <span class="block text-on-surface-variant text-[11px]">Items reaching this step are considered finished output</span>
      </label>
    </div>

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="add" disabled={isSubmitting}>
        {isSubmitting ? 'Creating...' : 'Create Step'}
      </Button>
    </div>
  </form>
</Modal>
