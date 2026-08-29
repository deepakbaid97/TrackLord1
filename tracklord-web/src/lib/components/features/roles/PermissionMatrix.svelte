<script>
  /**
   * Permission Matrix Component
   * Provides categorized, visual toggle pills for feature access levels.
   */

  /**
   * @type {{
   *   permissions?: Record<string, string[]>,
   *   disabled?: boolean,
   *   onChange?: (updated: Record<string, string[]>) => void
   * }}
   */
  let {
    permissions = $bindable({}),
    disabled = false,
    onChange
  } = $props();

  // Categorized Permission Modules Definition
  const MODULES = [
    {
      id: 'production',
      name: 'Production & Manufacturing',
      icon: 'precision_manufacturing',
      color: 'blue',
      features: [
        {
          key: 'production_lots',
          label: 'Production Lots',
          description: 'Batches, active WIP balances, and journey history',
          levels: [
            { id: 'read', label: 'View Lots', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Create & Edit', icon: 'edit', type: 'write' },
            { id: 'delete', label: 'Delete Lot', icon: 'delete', type: 'delete' }
          ]
        },
        {
          key: 'movements',
          label: 'Step Movements',
          description: 'Transfer lot quantities between manufacturing process steps',
          levels: [
            { id: 'read', label: 'View WIP', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Transfer Lots', icon: 'sync_alt', type: 'write' }
          ]
        },
        {
          key: 'movement_reversals',
          label: 'Movement Reversals',
          description: 'Reverse historical step transfers in inventory & production',
          levels: [
            { id: 'write', label: 'Allow Reversals', icon: 'undo', type: 'delete' }
          ]
        },
        {
          key: 'rejection_records',
          label: 'Defects & Scrap',
          description: 'Log and track defective pieces, wastage, and rework targets',
          levels: [
            { id: 'read', label: 'View Scrap', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Log Rejections', icon: 'report_problem', type: 'write' }
          ]
        }
      ]
    },
    {
      id: 'inventory',
      name: 'Inventory & Materials',
      icon: 'inventory_2',
      color: 'emerald',
      features: [
        {
          key: 'material_lots',
          label: 'Raw Material Stock',
          description: 'Fabric rolls, yarn, trims, and material lot intake',
          levels: [
            { id: 'read', label: 'View Stock', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Intake & Edit', icon: 'edit', type: 'write' },
            { id: 'delete', label: 'Delete Batch', icon: 'delete', type: 'delete' }
          ]
        },
        {
          key: 'material_consumptions',
          label: 'Material Consumption',
          description: 'Record raw material consumption against production batches',
          levels: [
            { id: 'read', label: 'View Consumptions', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Record Usage', icon: 'check_circle', type: 'write' }
          ]
        },
        {
          key: 'imports',
          label: 'Bulk Data Imports',
          description: 'Bulk upload production batches and material stock via spreadsheet',
          levels: [
            { id: 'write', label: 'Allow Imports', icon: 'upload_file', type: 'write' }
          ]
        }
      ]
    },
    {
      id: 'operations',
      name: 'Workflow & Partners',
      icon: 'hub',
      color: 'purple',
      features: [
        {
          key: 'manufacturing_steps',
          label: 'Process Steps',
          description: 'Factory workflow order, terminal steps, and sequence configuration',
          levels: [
            { id: 'read', label: 'View Steps', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Configure Steps', icon: 'settings', type: 'write' }
          ]
        },
        {
          key: 'clients',
          label: 'Clients & Job Workers',
          description: 'Customer directory, outside contractors, and subcontractor parties',
          levels: [
            { id: 'read', label: 'View Directory', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Manage Clients', icon: 'person_add', type: 'write' }
          ]
        }
      ]
    },
    {
      id: 'admin',
      name: 'System & Administration',
      icon: 'admin_panel_settings',
      color: 'indigo',
      features: [
        {
          key: 'dashboard',
          label: 'Analytics Dashboard',
          description: 'Factory overview, KPI summary cards, and WIP distribution charts',
          levels: [
            { id: 'read', label: 'View Dashboard', icon: 'analytics', type: 'read' }
          ]
        },
        {
          key: 'users',
          label: 'Team Members',
          description: 'View team profiles, invite operators, and manage user accounts',
          levels: [
            { id: 'read', label: 'View Team', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Manage Users', icon: 'manage_accounts', type: 'write' }
          ]
        },
        {
          key: 'roles',
          label: 'Roles & Access Control',
          description: 'Create and configure custom roles and feature permission sets',
          levels: [
            { id: 'read', label: 'View Roles', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Configure Roles', icon: 'shield_person', type: 'write' }
          ]
        },
        {
          key: 'tenant_settings',
          label: 'Tenant Settings',
          description: 'Organization profiles, client tracking mode, and tenant feature overrides',
          levels: [
            { id: 'read', label: 'View Settings', icon: 'visibility', type: 'read' },
            { id: 'write', label: 'Update Settings', icon: 'tune', type: 'write' }
          ]
        }
      ]
    }
  ];

  /**
   * Checks if permission is active.
   * @param {string} feature
   * @param {string} level
   */
  function isChecked(feature, level) {
    return permissions[feature]?.includes(level) || false;
  }

  /**
   * Toggles permission with smart dependency handling.
   * @param {string} feature
   * @param {string} level
   */
  function togglePermission(feature, level) {
    if (disabled) return;

    const currentList = permissions[feature] ? [...permissions[feature]] : [];
    const index = currentList.indexOf(level);

    if (index > -1) {
      // Deactivating level
      currentList.splice(index, 1);
      // If deactivating 'read', also remove dependent 'write' and 'delete'
      if (level === 'read') {
        const writeIdx = currentList.indexOf('write');
        if (writeIdx > -1) currentList.splice(writeIdx, 1);
        const deleteIdx = currentList.indexOf('delete');
        if (deleteIdx > -1) currentList.splice(deleteIdx, 1);
      }
    } else {
      // Activating level
      currentList.push(level);
      // If activating 'write' or 'delete', automatically ensure 'read' is enabled
      if ((level === 'write' || level === 'delete') && !currentList.includes('read')) {
        // Only if feature has a 'read' level
        const featureDef = MODULES.flatMap(m => m.features).find(f => f.key === feature);
        if (featureDef?.levels.some(l => l.id === 'read')) {
          currentList.push('read');
        }
      }
    }

    const updated = { ...permissions };
    if (currentList.length > 0) {
      updated[feature] = currentList;
      // Auto-grant step and client visibility when activating production lots or movements
      if ((feature === 'production_lots' || feature === 'movements')) {
        if (!updated['manufacturing_steps']) updated['manufacturing_steps'] = ['read'];
        if (!updated['clients']) updated['clients'] = ['read'];
      }
    } else {
      delete updated[feature];
    }

    permissions = updated;
    if (onChange) onChange(updated);
  }

  /**
   * Sets all permissions for a specific module.
   * @param {typeof MODULES[0]} mod
   * @param {'all' | 'read' | 'clear'} mode
   */
  function setModulePermissions(mod, mode) {
    if (disabled) return;
    const updated = { ...permissions };

    for (const feat of mod.features) {
      if (mode === 'clear') {
        delete updated[feat.key];
      } else if (mode === 'read') {
        const readLevel = feat.levels.find(l => l.id === 'read');
        if (readLevel) {
          updated[feat.key] = ['read'];
        } else {
          delete updated[feat.key];
        }
      } else if (mode === 'all') {
        updated[feat.key] = feat.levels.map(l => l.id);
      }
    }

    if (mod.id === 'production' && (mode === 'all' || mode === 'read')) {
      if (!updated['manufacturing_steps']) updated['manufacturing_steps'] = ['read'];
      if (!updated['clients']) updated['clients'] = ['read'];
    }

    permissions = updated;
    if (onChange) onChange(updated);
  }

  /**
   * Global Select All
   */
  export function selectAll() {
    if (disabled) return;
    const updated = {};
    for (const mod of MODULES) {
      for (const feat of mod.features) {
        updated[feat.key] = feat.levels.map(l => l.id);
      }
    }
    permissions = updated;
    if (onChange) onChange(updated);
  }

  /**
   * Global Reset / Clear All
   */
  export function clearAll() {
    if (disabled) return;
    permissions = {};
    if (onChange) onChange({});
  }

  /**
   * Applies a preset template dictionary
   * @param {Record<string, string[]>} preset
   */
  export function applyPreset(preset) {
    if (disabled) return;
    permissions = { ...preset };
    if (onChange) onChange(permissions);
  }
</script>

<div class="space-y-4 text-xs">
  <!-- Top Quick Actions Bar -->
  <div class="flex items-center justify-between bg-surface-container-low/70 dark:bg-slate-800/60 p-2.5 rounded-xl border border-outline-variant/60">
    <span class="text-xs font-bold text-on-surface flex items-center gap-1.5 font-mono">
      <span class="material-symbols-outlined text-[18px] text-primary">tune</span>
      <span>Configure Feature Permissions</span>
    </span>

    {#if !disabled}
      <div class="flex items-center gap-1.5">
        <button
          type="button"
          onclick={selectAll}
          class="px-2.5 py-1 text-[11px] font-bold text-primary bg-primary/10 hover:bg-primary/20 rounded-lg transition-colors cursor-pointer"
        >
          Select All
        </button>
        <button
          type="button"
          onclick={clearAll}
          class="px-2.5 py-1 text-[11px] font-semibold text-on-surface-variant hover:bg-surface-container-high rounded-lg transition-colors cursor-pointer"
        >
          Clear All
        </button>
      </div>
    {/if}
  </div>

  <!-- Modules Stack -->
  {#each MODULES as mod}
    <div class="bg-surface dark:bg-slate-900 rounded-2xl border border-outline-variant/70 shadow-xs overflow-hidden">
      <!-- Section Header -->
      <div class="flex items-center justify-between px-3.5 py-2.5 bg-surface-container-low/50 dark:bg-slate-800/40 border-b border-outline-variant/50">
        <div class="flex items-center gap-2">
          <div class="w-6 h-6 rounded-lg bg-primary/10 text-primary flex items-center justify-center">
            <span class="material-symbols-outlined text-[16px]">{mod.icon}</span>
          </div>
          <span class="font-bold text-sm text-on-surface tracking-tight">{mod.name}</span>
        </div>

        {#if !disabled}
          <div class="flex items-center gap-1">
            <button
              type="button"
              onclick={() => setModulePermissions(mod, 'all')}
              class="px-2 py-0.5 text-[10px] font-bold text-primary hover:bg-primary/10 rounded transition-colors cursor-pointer"
            >
              Full Access
            </button>
            <span class="text-outline-variant">•</span>
            <button
              type="button"
              onclick={() => setModulePermissions(mod, 'read')}
              class="px-2 py-0.5 text-[10px] font-semibold text-on-surface-variant hover:bg-surface-container rounded transition-colors cursor-pointer"
            >
              View Only
            </button>
            <span class="text-outline-variant">•</span>
            <button
              type="button"
              onclick={() => setModulePermissions(mod, 'clear')}
              class="px-2 py-0.5 text-[10px] font-semibold text-red-500 hover:bg-red-50 dark:hover:bg-red-950/40 rounded transition-colors cursor-pointer"
            >
              Clear
            </button>
          </div>
        {/if}
      </div>

      <!-- Feature Rows -->
      <div class="divide-y divide-outline-variant/30">
        {#each mod.features as feature}
          <div class="p-3 sm:px-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5 hover:bg-surface-container-low/40 transition-colors">
            <!-- Feature Info -->
            <div class="min-w-0 flex-1">
              <div class="font-bold text-xs text-on-surface flex items-center gap-1.5">
                <span>{feature.label}</span>
              </div>
              <p class="text-[11px] text-on-surface-variant leading-relaxed line-clamp-1">{feature.description}</p>
            </div>

            <!-- Permission Toggle Pills -->
            <div class="flex flex-wrap items-center gap-1.5 shrink-0">
              {#each feature.levels as level}
                {@const active = isChecked(feature.key, level.id)}
                <button
                  type="button"
                  disabled={disabled}
                  onclick={() => togglePermission(feature.key, level.id)}
                  class="px-2.5 py-1 rounded-lg text-xs font-bold transition-all flex items-center gap-1 cursor-pointer active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed
                    {active
                      ? level.type === 'delete'
                        ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/40 shadow-xs'
                        : 'bg-primary text-white shadow-xs shadow-primary/20'
                      : 'bg-surface-container-low border border-outline-variant/80 text-on-surface-variant hover:bg-surface-container'}"
                >
                  <span class="material-symbols-outlined text-[14px]">
                    {active ? 'check_circle' : level.icon}
                  </span>
                  <span>{level.label}</span>
                </button>
              {/each}
            </div>
          </div>
        {/each}
      </div>
    </div>
  {/each}
</div>
