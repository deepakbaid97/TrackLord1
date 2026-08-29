<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import PermissionMatrix from './PermissionMatrix.svelte';
  import { createRole, updateRole } from '$lib/api.js';

  /**
   * @type {{
   *   isOpen: boolean,
   *   role?: any,
   *   onSuccess?: (role: any) => void,
   *   onError?: (err: any) => void
   * }}
   */
  let {
    isOpen = $bindable(false),
    role = null,
    onSuccess,
    onError
  } = $props();

  let formName = $state('');
  let formDescription = $state('');
  let formPermissions = $state(/** @type {Record<string, string[]>} */ ({}));
  let isSubmitting = $state(false);
  let modalError = $state('');
  let selectedPreset = $state(/** @type {string | null} */ (null));

  /** @type {PermissionMatrix | null} */
  let matrixRef = $state(null);

  const isEditing = $derived(!!role && !!role.id);
  const isSystemAdmin = $derived(role?.isSystemRole && role?.name?.toLowerCase() === 'admin');

  // Quick-Start Manufacturing Role Presets
  const PRESETS = [
    {
      id: 'floor_incharge',
      name: 'Floor Incharge',
      icon: 'engineering',
      desc: 'Raw materials, consumptions, and movement reversal oversight',
      permissions: {
        material_lots: ['read', 'write'],
        material_consumptions: ['read', 'write'],
        movement_reversals: ['write'],
        production_lots: ['read'],
        movements: ['read'],
        manufacturing_steps: ['read'],
        clients: ['read'],
        dashboard: ['read']
      }
    },
    {
      id: 'job_worker',
      name: 'Job Worker',
      icon: 'directions_run',
      desc: 'Step-to-step quantity transfer operator',
      permissions: {
        movements: ['read', 'write'],
        production_lots: ['read'],
        manufacturing_steps: ['read'],
        clients: ['read'],
        dashboard: ['read']
      }
    },
    {
      id: 'qc_inspector',
      name: 'QC Inspector',
      icon: 'verified',
      desc: 'Quality control defect and scrap reporting',
      permissions: {
        rejection_records: ['read', 'write'],
        production_lots: ['read'],
        movements: ['read'],
        material_consumptions: ['read'],
        manufacturing_steps: ['read'],
        clients: ['read'],
        dashboard: ['read']
      }
    },
    {
      id: 'store_keeper',
      name: 'Store Keeper',
      icon: 'warehouse',
      desc: 'Raw material inventory, stock intake, and batch imports',
      permissions: {
        material_lots: ['read', 'write', 'delete'],
        material_consumptions: ['read', 'write'],
        imports: ['write'],
        manufacturing_steps: ['read'],
        clients: ['read'],
        dashboard: ['read']
      }
    },
    {
      id: 'auditor',
      name: 'Auditor / Viewer',
      icon: 'visibility',
      desc: 'Read-only visibility across all manufacturing logs and dashboards',
      permissions: {
        production_lots: ['read'],
        movements: ['read'],
        material_lots: ['read'],
        material_consumptions: ['read'],
        rejection_records: ['read'],
        manufacturing_steps: ['read'],
        clients: ['read'],
        dashboard: ['read'],
        users: ['read'],
        roles: ['read'],
        tenant_settings: ['read']
      }
    }
  ];

  $effect(() => {
    if (isOpen) {
      if (role) {
        formName = role.name || '';
        formDescription = role.description || '';
        formPermissions = role.permissions ? JSON.parse(JSON.stringify(role.permissions)) : {};
        selectedPreset = null;
      } else {
        formName = '';
        formDescription = '';
        formPermissions = {};
        selectedPreset = null;
      }
      modalError = '';
    }
  });

  /** @param {typeof PRESETS[0]} preset */
  function applyPreset(preset) {
    selectedPreset = preset.id;
    if (!formName) formName = preset.name;
    if (!formDescription) formDescription = preset.desc;
    formPermissions = JSON.parse(JSON.stringify(preset.permissions));
  }

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    if (e) e.preventDefault();
    if (!formName.trim()) {
      modalError = 'Role name is required.';
      return;
    }

    isSubmitting = true;
    modalError = '';

    try {
      const payload = {
        name: formName.trim(),
        description: formDescription.trim() || null,
        permissionsJson: JSON.stringify(formPermissions)
      };

      let result;
      if (isEditing) {
        result = await updateRole(role.id, {
          ...payload,
          isActive: role.isActive ?? true
        });
      } else {
        result = await createRole(payload);
      }

      isOpen = false;
      if (onSuccess) onSuccess(result);
    } catch (/** @type {any} */ err) {
      modalError = err.message || 'Failed to save role.';
      if (onError) onError(err);
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal
  bind:isOpen
  title={isEditing ? `Edit Role: ${role?.name}` : 'Create Custom Role'}
>
  <form onsubmit={handleSubmit} class="space-y-4 max-h-[80vh] overflow-y-auto pr-1">
    {#if modalError}
      <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">error</span>
        <span>{modalError}</span>
      </div>
    {/if}

    {#if isSystemAdmin}
      <div class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/60 text-amber-800 dark:text-amber-300 text-xs flex items-center gap-2">
        <span class="material-symbols-outlined text-[18px]">lock</span>
        <span>The System Admin role always has unconditional full access to all features and cannot be restricted.</span>
      </div>
    {/if}

    <!-- Role Name & Description -->
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
      <Input
        label="Role Name"
        bind:value={formName}
        placeholder="e.g. Floor Incharge"
        icon="badge"
        required={true}
        disabled={isSystemAdmin}
      />

      <Input
        label="Description (Optional)"
        bind:value={formDescription}
        placeholder="e.g. Supervise cutting and roll stock"
        icon="notes"
        required={false}
      />
    </div>

    <!-- Quick-Start Templates (Only for new roles) -->
    {#if !isEditing && !isSystemAdmin}
      <div class="space-y-1.5 pt-1">
        <span class="text-xs font-bold text-on-surface-variant font-mono uppercase tracking-wider flex items-center gap-1">
          <span class="material-symbols-outlined text-[15px] text-primary">bolt</span>
          <span>Quick-Start Manufacturing Templates</span>
        </span>

        <div class="grid grid-cols-2 sm:grid-cols-3 gap-2">
          {#each PRESETS as preset}
            <button
              type="button"
              onclick={() => applyPreset(preset)}
              class="p-2.5 rounded-xl border text-left transition-all cursor-pointer flex flex-col justify-between gap-1
                {selectedPreset === preset.id
                  ? 'border-primary bg-primary/10 text-primary shadow-xs ring-1 ring-primary/30'
                  : 'border-outline-variant/80 bg-surface-container-low/50 hover:bg-surface-container hover:border-primary/40 text-on-surface'}"
            >
              <div class="flex items-center gap-1.5">
                <span class="material-symbols-outlined text-[16px] text-primary">{preset.icon}</span>
                <span class="font-bold text-xs truncate">{preset.name}</span>
              </div>
              <p class="text-[10px] text-on-surface-variant line-clamp-1 leading-tight">{preset.desc}</p>
            </button>
          {/each}
        </div>
      </div>
    {/if}

    <!-- Permission Matrix -->
    <div class="pt-2 border-t border-outline-variant/60">
      <PermissionMatrix
        bind:this={matrixRef}
        bind:permissions={formPermissions}
        disabled={isSystemAdmin}
      />
    </div>

    <!-- Modal Footer Actions -->
    <div class="flex items-center justify-end gap-3 pt-3 border-t border-outline-variant sticky bottom-0 bg-surface dark:bg-slate-900 z-10">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save" disabled={isSubmitting || isSystemAdmin}>
        {isSubmitting ? 'Saving...' : isEditing ? 'Save Changes' : 'Create Role'}
      </Button>
    </div>
  </form>
</Modal>
