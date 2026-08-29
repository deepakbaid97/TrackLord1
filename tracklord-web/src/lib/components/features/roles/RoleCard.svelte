<script>
  /**
   * Role Card Component
   * Displays role metadata, permission summary chips, assigned members count, and actions.
   */

  /**
   * @type {{
   *   role: any,
   *   assignedCount?: number,
   *   canManage?: boolean,
   *   onEdit?: (role: any) => void,
   *   onDuplicate?: (role: any) => void,
   *   onDelete?: (role: any) => void
   * }}
   */
  let {
    role,
    assignedCount = 0,
    canManage = true,
    onEdit,
    onDuplicate,
    onDelete
  } = $props();

  const isSystemAdmin = $derived(role?.isSystemRole && role?.name?.toLowerCase() === 'admin');
  const isSystem = $derived(role?.isSystemRole);

  /**
   * Builds summary chips for the role's key permissions
   */
  let permissionChips = $derived.by(() => {
    if (isSystemAdmin) {
      return [
        { label: 'All Modules: Full Control', icon: 'verified_user', variant: 'admin' }
      ];
    }

    const perms = role.permissions || {};
    const chips = [];

    if (perms.production_lots?.includes('write')) {
      chips.push({ label: 'Lots: Full Control', icon: 'reorder', variant: 'primary' });
    } else if (perms.production_lots?.includes('read')) {
      chips.push({ label: 'Lots: View Only', icon: 'visibility', variant: 'neutral' });
    }

    if (perms.movements?.includes('write')) {
      chips.push({ label: 'Movements: Transfer Lots', icon: 'sync_alt', variant: 'primary' });
    }

    if (perms.movement_reversals?.includes('write')) {
      chips.push({ label: 'Reversals: Allowed', icon: 'undo', variant: 'warning' });
    }

    if (perms.material_lots?.includes('write')) {
      chips.push({ label: 'Materials: Manage & Intake', icon: 'texture', variant: 'primary' });
    } else if (perms.material_lots?.includes('read')) {
      chips.push({ label: 'Materials: View Stock', icon: 'visibility', variant: 'neutral' });
    }

    if (perms.material_consumptions?.includes('write')) {
      chips.push({ label: 'Consumptions: Record Usage', icon: 'check_circle', variant: 'primary' });
    }

    if (perms.rejection_records?.includes('write')) {
      chips.push({ label: 'Scrap & Defect Logging', icon: 'report_problem', variant: 'warning' });
    }

    if (perms.manufacturing_steps?.includes('write')) {
      chips.push({ label: 'Steps: Configure & Reorder', icon: 'settings', variant: 'primary' });
    }

    if (perms.users?.includes('write') || perms.roles?.includes('write')) {
      chips.push({ label: 'Team & Roles Administration', icon: 'manage_accounts', variant: 'admin' });
    }

    if (chips.length === 0) {
      chips.push({ label: 'No Active Permissions', icon: 'lock', variant: 'neutral' });
    }

    return chips;
  });
</script>

<div class="bg-surface dark:bg-slate-900 p-4 sm:p-5 rounded-2xl border border-outline-variant/80 shadow-xs hover:shadow-md transition-all flex flex-col justify-between gap-3.5">
  <!-- Top Header: Title, Icon & System Badge -->
  <div>
    <div class="flex items-start justify-between gap-2">
      <div class="flex items-center gap-2.5 min-w-0">
        <div class="w-10 h-10 rounded-xl flex items-center justify-center font-bold shrink-0
          {isSystem
            ? 'bg-amber-500/15 text-amber-600 dark:text-amber-400'
            : 'bg-primary/10 text-primary'}">
          <span class="material-symbols-outlined text-[22px]">
            {isSystemAdmin ? 'shield' : isSystem ? 'lock' : 'badge'}
          </span>
        </div>
        <div class="min-w-0">
          <div class="flex items-center gap-1.5 flex-wrap">
            <h3 class="font-bold text-base text-on-surface truncate">{role.name}</h3>
            {#if isSystem}
              <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono uppercase bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/30">
                System Role
              </span>
            {:else}
              <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono uppercase bg-primary/10 text-primary border border-primary/20">
                Custom Role
              </span>
            {/if}
          </div>
          <p class="text-xs text-on-surface-variant line-clamp-1 mt-0.5 font-mono">
            {assignedCount} {assignedCount === 1 ? 'member' : 'members'} assigned
          </p>
        </div>
      </div>
    </div>

    <!-- Description -->
    <p class="text-xs text-on-surface-variant mt-2.5 leading-relaxed line-clamp-2 min-h-[32px]">
      {role.description || 'No description provided.'}
    </p>

    <!-- Permission Chips Preview -->
    <div class="mt-3 flex flex-wrap gap-1.5">
      {#each permissionChips as chip}
        <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-[11px] font-semibold font-mono
          {chip.variant === 'admin'
            ? 'bg-purple-500/15 text-purple-700 dark:text-purple-300 border border-purple-500/30'
            : chip.variant === 'warning'
            ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/30'
            : chip.variant === 'primary'
            ? 'bg-primary/10 text-primary border border-primary/20'
            : 'bg-surface-container-low text-on-surface-variant border border-outline-variant/60'}">
          <span class="material-symbols-outlined text-[13px]">{chip.icon}</span>
          <span>{chip.label}</span>
        </span>
      {/each}
    </div>
  </div>

  <!-- Actions Toolbar -->
  {#if canManage}
    <div class="pt-3 border-t border-outline-variant/60 flex items-center justify-between gap-2">
      <button
        type="button"
        onclick={() => onEdit && onEdit(role)}
        class="px-3 py-1.5 rounded-xl font-bold text-xs bg-primary/10 hover:bg-primary text-primary hover:text-white transition-all flex items-center gap-1.5 cursor-pointer active:scale-95"
      >
        <span class="material-symbols-outlined text-[15px]">edit</span>
        <span>{isSystemAdmin ? 'View Permissions' : 'Edit Role'}</span>
      </button>

      <div class="flex items-center gap-1.5">
        <button
          type="button"
          onclick={() => onDuplicate && onDuplicate(role)}
          title="Create a copy of this role as a new template"
          class="p-1.5 rounded-lg border border-outline-variant/80 hover:bg-surface-container-low text-on-surface-variant hover:text-primary transition-all cursor-pointer"
        >
          <span class="material-symbols-outlined text-[17px]">content_copy</span>
        </button>

        {#if !isSystem}
          <button
            type="button"
            onclick={() => onDelete && onDelete(role)}
            title="Delete this custom role"
            class="p-1.5 rounded-lg border border-red-200 dark:border-red-900/60 hover:bg-red-50 dark:hover:bg-red-950/40 text-red-600 dark:text-red-400 transition-all cursor-pointer"
          >
            <span class="material-symbols-outlined text-[17px]">delete</span>
          </button>
        {/if}
      </div>
    </div>
  {/if}
</div>
