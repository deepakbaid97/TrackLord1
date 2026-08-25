<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { reverseMovement } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    movement = null,
    onSuccess,
    onError
  } = $props();

  let form = $state({
    movementId: '',
    fromStep: '',
    toStep: '',
    quantity: '',
    referenceNumber: '',
    reason: 'Typo correction / Accidental movement',
    user: 'Supervisor'
  });

  $effect(() => {
    if (movement) {
      form.movementId = movement.id || '';
      form.fromStep = movement.from || 'Source Step';
      form.toStep = movement.to || 'Destination Step';
      form.quantity = movement.qty || '';
      form.referenceNumber = movement.referenceNumber || '-';
      form.reason = 'Typo correction / Accidental movement';
      form.user = 'Supervisor';
    }
  });

  let isSubmitting = $state(false);

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    if (!form.movementId) return;

    isSubmitting = true;
    try {
      await reverseMovement(form.movementId, {
        reason: form.reason.trim() || 'Reversal initiated from audit ledger UI',
        user: form.user.trim() || 'Supervisor'
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

<Modal bind:isOpen title="Reverse Movement Transaction">
  <form onsubmit={handleSubmit} class="space-y-4">
    <div class="p-3 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/60 rounded-xl text-xs text-amber-800 dark:text-amber-300 space-y-1">
      <div class="font-bold flex items-center gap-1.5">
        <span class="material-symbols-outlined text-[16px]">warning</span>
        <span>Audit Reversal Notice</span>
      </div>
      <p>This action will reverse the WIP quantities back to <strong class="font-mono">{form.fromStep}</strong> and mark this movement record as reversed.</p>
    </div>

    <div class="grid grid-cols-2 gap-3 text-xs font-mono bg-surface-container-low p-3 rounded-xl border border-outline-variant/60">
      <div><span class="text-on-surface-variant">From:</span> <strong class="text-on-surface">{form.fromStep}</strong></div>
      <div><span class="text-on-surface-variant">To:</span> <strong class="text-on-surface">{form.toStep}</strong></div>
      <div><span class="text-on-surface-variant">Quantity:</span> <strong class="text-primary">{form.quantity}</strong></div>
      <div><span class="text-on-surface-variant">Ref #:</span> <strong class="text-on-surface">{form.referenceNumber}</strong></div>
    </div>

    <Input
      label="Reversal Reason / Note"
      bind:value={form.reason}
      placeholder="e.g. Accidental transfer entry"
      icon="notes"
      required={true}
    />

    <Input
      label="Authorizing User"
      bind:value={form.user}
      placeholder="e.g. Supervisor"
      icon="person"
      required={true}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="danger" type="submit" icon="undo" disabled={isSubmitting}>
        {isSubmitting ? 'Reversing...' : 'Confirm Reversal'}
      </Button>
    </div>
  </form>
</Modal>
