<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { deleteRole } from '$lib/api.js';

  /**
   * @type {{
   *   isOpen: boolean,
   *   role?: any,
   *   assignedCount?: number,
   *   onSuccess?: (role: any) => void,
   *   onError?: (err: any) => void
   * }}
   */
  let {
    isOpen = $bindable(false),
    role = null,
    assignedCount = 0,
    onSuccess,
    onError
  } = $props();

  let isDeleting = $state(false);
  let modalError = $state('');

  const hasAssignedUsers = $derived(assignedCount > 0);
  const isSystemRole = $derived(role?.isSystemRole);

  async function handleDelete() {
    if (!role) return;
    if (isSystemRole) {
      modalError = 'System roles cannot be deleted.';
      return;
    }
    if (hasAssignedUsers) {
      modalError = `Cannot delete role '${role.name}' because ${assignedCount} team member(s) are currently assigned to it. Please reassign them first.`;
      return;
    }

    isDeleting = true;
    modalError = '';

    try {
      await deleteRole(role.id);
      isOpen = false;
      if (onSuccess) onSuccess(role);
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to delete role.';
      if (onError) onError(err);
    } finally {
      isDeleting = false;
    }
  }
</script>

<Modal bind:isOpen title="Delete Role">
  <div class="space-y-4">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    {#if isSystemRole}
      <div class="p-3.5 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/60 text-amber-800 dark:text-amber-300 text-xs flex items-start gap-2.5">
        <span class="material-symbols-outlined text-[20px] text-amber-600 shrink-0">lock</span>
        <div>
          <p class="font-bold text-sm">System Role Protected</p>
          <p class="mt-0.5 leading-relaxed">The '{role?.name}' role is a core system role and cannot be deleted.</p>
        </div>
      </div>
    {:else if hasAssignedUsers}
      <div class="p-3.5 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/60 text-amber-800 dark:text-amber-300 text-xs flex items-start gap-2.5">
        <span class="material-symbols-outlined text-[20px] text-amber-600 shrink-0">group_off</span>
        <div>
          <p class="font-bold text-sm">Team Members Assigned</p>
          <p class="mt-0.5 leading-relaxed">
            There are currently <strong>{assignedCount}</strong> team member(s) assigned to '{role?.name}'. Please reassign them to another role before deleting.
          </p>
        </div>
      </div>
    {:else}
      <p class="text-sm text-on-surface leading-relaxed">
        Are you sure you want to permanently delete the custom role <strong>{role?.name}</strong>? This action cannot be undone.
      </p>
    {/if}

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button
        variant="danger"
        icon="delete"
        onclick={handleDelete}
        disabled={isDeleting || isSystemRole || hasAssignedUsers}
      >
        {isDeleting ? 'Deleting...' : 'Delete Role'}
      </Button>
    </div>
  </div>
</Modal>
