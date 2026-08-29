<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { createUser, getRoles } from '$lib/api.js';

  /**
   * @type {{
   *   isOpen: boolean,
   *   availableRoles?: Array<any>,
   *   onSuccess?: (phone: string) => void,
   *   onError?: (err: any) => void
   * }}
   */
  let {
    isOpen = $bindable(false),
    availableRoles = [],
    onSuccess,
    onError
  } = $props();

  let formName = $state('');
  let formPhone = $state('');
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
    if (isOpen) {
      if (effectiveRoles.length === 0) {
        getRoles().then(data => {
          if (data && data.length > 0) {
            loadedRoles = data;
            const op = data.find((r) => r.name?.toLowerCase() === 'operator') || data[0];
            if (op && !formRoleId) formRoleId = op.id;
          }
        }).catch(() => {});
      } else if (!formRoleId) {
        const op = effectiveRoles.find((r) => r.name?.toLowerCase() === 'operator') || effectiveRoles[0];
        if (op) formRoleId = op.id;
      }
    }
  });

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    if (e) e.preventDefault();
    const cleanPhone = formPhone.replace(/\D/g, '');
    if (cleanPhone.length < 10) {
      modalError = 'Please enter a valid 10-digit mobile number.';
      return;
    }

    isSubmitting = true;
    modalError = '';
    try {
      const fullPhone = `+91${cleanPhone.slice(-10)}`;
      const selectedRoleObj = effectiveRoles.find(r => r.id === formRoleId);

      await createUser({
        phoneNumber: fullPhone,
        name: formName.trim() || null,
        role: selectedRoleObj?.name || 'Operator',
        roleId: selectedRoleObj?.id || null
      });

      isOpen = false;
      const phone = fullPhone;
      formName = '';
      formPhone = '';
      if (onSuccess) onSuccess(phone);
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to add user.';
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Add Team Member">
  <form onsubmit={handleSubmit} class="space-y-4">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    <Input
      label="Full Name (Optional)"
      bind:value={formName}
      placeholder="e.g. Ramesh Kumar"
      icon="person"
      required={false}
    />

    <Input
      label="Mobile Number"
      type="tel"
      bind:value={formPhone}
      placeholder="10-digit number (e.g. 9876543210)"
      icon="call"
      required={true}
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
      <Button variant="primary" type="submit" icon="person_add" disabled={isSubmitting}>
        {isSubmitting ? 'Adding...' : 'Add Member'}
      </Button>
    </div>
  </form>
</Modal>
