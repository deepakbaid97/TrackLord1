<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { updateUser, getRoles } from '$lib/api.js';

  /**
   * @type {{
   *   isOpen: boolean,
   *   user?: any,
   *   availableRoles?: Array<any>,
   *   onSuccess?: (phone: string) => void,
   *   onError?: (err: any) => void
   * }}
   */
  let {
    isOpen = $bindable(false),
    user = null,
    availableRoles = [],
    onSuccess,
    onError
  } = $props();

  let formName = $state('');
  let formRoleId = $state('');
  let isSubmitting = $state(false);
  let modalError = $state('');
  let loadedRoles = $state(/** @type {Array<any>} */ ([]));

  const effectiveRoles = $derived(availableRoles.length > 0 ? availableRoles : loadedRoles);

  const roleOptions = $derived(
    effectiveRoles.length > 0
      ? effectiveRoles.map(r => ({
          label: `${r.name}${r.isSystemRole ? ' (System)' : ''}`,
          value: r.id
        }))
      : [
          { label: 'Operator (View / Move)', value: 'Operator' },
          { label: 'Supervisor (Reverse / Adjust)', value: 'Supervisor' },
          { label: 'Manager (Manage / Reports)', value: 'Manager' },
          { label: 'Admin (Full Tenant Access)', value: 'Admin' }
        ]
  );

  $effect(() => {
    if (isOpen && effectiveRoles.length === 0) {
      getRoles().then(data => {
        if (data && data.length > 0) loadedRoles = data;
      }).catch(() => {});
    }
  });

  $effect(() => {
    if (user) {
      formName = user.name || '';
      // Find matching role by id or name
      const matched = effectiveRoles.find(r => r.id === user.roleId || r.name?.toLowerCase() === user.role?.toLowerCase());
      formRoleId = matched ? matched.id : user.roleId || user.role || '';
      modalError = '';
    }
  });

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    if (e) e.preventDefault();
    if (!user) return;

    isSubmitting = true;
    modalError = '';
    try {
      const selectedRoleObj = effectiveRoles.find(r => r.id === formRoleId);

      await updateUser(user.id, {
        name: formName.trim() || null,
        role: selectedRoleObj?.name || user.role,
        roleId: selectedRoleObj?.id || null
      });

      isOpen = false;
      if (onSuccess) onSuccess(user.phoneNumber);
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to update user profile.';
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Edit Team Member Profile">
  <form onsubmit={handleSubmit} class="space-y-4">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    <div class="p-3 rounded-xl bg-surface-container-low border border-outline-variant/60 text-xs flex items-center gap-2 font-mono">
      <span class="material-symbols-outlined text-[16px] text-primary">phone</span>
      <span class="font-bold text-on-surface">{user?.phoneNumber}</span>
    </div>

    <Input
      label="Full Name"
      bind:value={formName}
      placeholder="e.g. Ramesh Kumar"
      icon="person"
      required={false}
    />

    <Select
      label="Assigned Role"
      bind:value={formRoleId}
      options={roleOptions}
      icon="admin_panel_settings"
      required={true}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save" disabled={isSubmitting}>
        {isSubmitting ? 'Saving...' : 'Save Profile'}
      </Button>
    </div>
  </form>
</Modal>
