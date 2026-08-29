<script>
  import { onMount } from 'svelte';
  import { getUsers, getRoles } from '$lib/api.js';
  import { authStore } from '$lib/stores/authStore.js';
  import { permissionStore } from '$lib/stores/permissionStore.js';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import SearchBox from '$lib/components/ui/SearchBox.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import AddUserModal from '$lib/components/features/users/AddUserModal.svelte';
  import EditUserModal from '$lib/components/features/users/EditUserModal.svelte';
  import RoleCard from '$lib/components/features/roles/RoleCard.svelte';
  import RoleModal from '$lib/components/features/roles/RoleModal.svelte';
  import DeleteRoleModal from '$lib/components/features/roles/DeleteRoleModal.svelte';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');
  let activeTab = $state('users'); // 'users' | 'roles'

  // Users State
  let searchQuery = $state('');
  let roleFilter = $state('All');
  /** @type {Array<any>} */
  let users = $state([]);

  // Roles State
  let roleSearchQuery = $state('');
  /** @type {Array<any>} */
  let roles = $state([]);

  let currentUser = $derived($authStore.user);
  let isAuthenticated = $derived($authStore.isAuthenticated);
  let isAdmin = $derived($permissionStore.isAdmin);
  let canManageUsers = $derived($permissionStore.canWrite('users'));
  let canViewRoles = $derived($permissionStore.canRead('roles'));
  let canManageRoles = $derived($permissionStore.canWrite('roles'));

  // User Modals State
  let showAddUserModal = $state(false);
  let showEditUserModal = $state(false);
  /** @type {any} */
  let editingUser = $state(null);

  // Role Modals State
  let showRoleModal = $state(false);
  let showDeleteRoleModal = $state(false);
  /** @type {any} */
  let editingRole = $state(null);
  /** @type {any} */
  let deletingRole = $state(null);

  usePageHeader(() => {
    /** @type {import('$lib/stores/headerStore.js').HeaderAction[]} */
    const actions = [
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: 'outline',
        onclick: () => refreshAll()
      }
    ];

    if (isAuthenticated) {
      actions.push({
        label: 'Log Out',
        icon: 'logout',
        variant: 'outline',
        onclick: () => authStore.logout()
      });
    }

    return {
      title: 'Team & Access',
      subtitle: 'Users, Roles & Permissions',
      icon: 'manage_accounts',
      actions
    };
  });

  onMount(async () => {
    await refreshAll();
  });

  async function refreshAll() {
    loading = true;
    errorMessage = '';
    try {
      const [usersData, rolesData] = await Promise.all([
        getUsers().catch(() => []),
        getRoles().catch(() => [])
      ]);
      users = usersData || [];
      roles = rolesData || [];
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load team data.';
    } finally {
      loading = false;
    }
  }

  // Users filtering
  let filteredUsers = $derived(
    users.filter(u => {
      // Non-admin / non-supervisor can only see their own profile if they cannot manage users
      if (!canManageUsers && currentUser && currentUser.phoneNumber !== u.phoneNumber) {
        return false;
      }

      const matchesSearch = !searchQuery ||
        (u.name && u.name.toLowerCase().includes(searchQuery.toLowerCase())) ||
        (u.phoneNumber && u.phoneNumber.includes(searchQuery)) ||
        (u.role && u.role.toLowerCase().includes(searchQuery.toLowerCase()));

      const matchesRole = roleFilter === 'All' || u.role === roleFilter;

      return matchesSearch && matchesRole;
    })
  );

  // Roles filtering
  let filteredRoles = $derived(
    roles.filter(r => {
      if (!roleSearchQuery) return true;
      const q = roleSearchQuery.toLowerCase();
      return (r.name && r.name.toLowerCase().includes(q)) ||
             (r.description && r.description.toLowerCase().includes(q));
    })
  );

  // Computed member count map per role
  let roleUserCountMap = $derived.by(() => {
    /** @type {Record<string, number>} */
    const map = {};
    for (const u of users) {
      const rId = u.roleId;
      const rName = u.role;
      if (rId) map[rId] = (map[rId] || 0) + 1;
      if (rName) map[rName.toLowerCase()] = (map[rName.toLowerCase()] || 0) + 1;
    }
    return map;
  });

  /** @param {any} r */
  function getRoleMemberCount(r) {
    return roleUserCountMap[r.id] || roleUserCountMap[r.name?.toLowerCase()] || 0;
  }

  /** @param {string} dateStr */
  function formatDate(dateStr) {
    if (!dateStr) return 'Never';
    const date = new Date(dateStr);
    return date.toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  function openEditUser(user) {
    editingUser = user;
    showEditUserModal = true;
  }

  function openCreateRole() {
    editingRole = null;
    showRoleModal = true;
  }

  function openEditRole(role) {
    editingRole = role;
    showRoleModal = true;
  }

  function openDuplicateRole(role) {
    editingRole = {
      name: `${role.name} Copy`,
      description: role.description,
      permissions: JSON.parse(JSON.stringify(role.permissions || {}))
    };
    showRoleModal = true;
  }

  function openDeleteRole(role) {
    deletingRole = role;
    showDeleteRoleModal = true;
  }
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  {#if loading}
    <LoadingSpinner message="Loading team directory & roles..." />
  {:else}
    <!-- Top Hero Card & Action Banner -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-surface p-3.5 sm:p-5 rounded-2xl border border-outline-variant shadow-sm">
      <div class="flex items-center gap-2.5">
        <div class="p-2 bg-primary/10 rounded-xl text-primary flex items-center justify-center">
          <span class="material-symbols-outlined text-[22px]">manage_accounts</span>
        </div>
        <div>
          <h2 class="text-lg sm:text-2xl text-primary font-bold tracking-tight">Team & Access Control</h2>
          <p class="text-xs text-on-surface-variant font-mono">Manage operators, supervisors, custom roles & security permissions</p>
        </div>
      </div>

      <div class="flex items-center gap-2 w-full sm:w-auto justify-end flex-wrap">
        <button
          onclick={() => tenantConfigStore.openModal()}
          class="flex-1 sm:flex-none px-3 py-1.5 sm:px-4 sm:py-2 bg-surface-container-low hover:bg-primary/10 hover:text-primary text-on-surface font-semibold text-xs sm:text-sm rounded-xl border border-outline-variant shadow-xs transition-all flex items-center justify-center gap-1.5 cursor-pointer active:scale-95"
          title="Configure Tenant Settings, Visibility & Modes"
        >
          <span class="material-symbols-outlined text-[16px] sm:text-[18px] text-primary">settings</span>
          <span>Tenant Settings</span>
        </button>

        {#if activeTab === 'users' && canManageUsers}
          <button
            onclick={() => showAddUserModal = true}
            class="flex-1 sm:flex-none px-3 py-1.5 sm:px-4 sm:py-2 bg-primary hover:bg-blue-700 text-white font-bold text-xs sm:text-sm rounded-xl shadow-sm transition-all flex items-center justify-center gap-1.5 cursor-pointer active:scale-95"
          >
            <span class="material-symbols-outlined text-[16px] sm:text-[18px]">person_add</span>
            <span>Add Member</span>
          </button>
        {:else if activeTab === 'roles' && canManageRoles}
          <button
            onclick={openCreateRole}
            class="flex-1 sm:flex-none px-3 py-1.5 sm:px-4 sm:py-2 bg-primary hover:bg-blue-700 text-white font-bold text-xs sm:text-sm rounded-xl shadow-sm transition-all flex items-center justify-center gap-1.5 cursor-pointer active:scale-95"
          >
            <span class="material-symbols-outlined text-[16px] sm:text-[18px]">add_moderator</span>
            <span>Create Role</span>
          </button>
        {/if}
      </div>
    </div>

    <!-- Active Session & Tenant Info Card (if logged in) -->
    {#if isAuthenticated && currentUser}
      <div class="flex items-center justify-between p-3 bg-surface-container-low dark:bg-surface-container-high rounded-xl border border-outline-variant shadow-xs text-xs">
        <div class="flex items-center gap-2.5 min-w-0">
          <div class="w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center font-bold shrink-0">
            <span class="material-symbols-outlined text-[18px]">account_circle</span>
          </div>
          <div class="min-w-0">
            <div class="flex items-center gap-1.5">
              <span class="font-bold text-on-surface text-xs truncate">{currentUser.name || 'User'}</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-primary/15 text-primary shrink-0 uppercase">
                {currentUser.role || 'Operator'}
              </span>
            </div>
            <p class="text-[10px] text-on-surface-variant font-mono truncate">{currentUser.phoneNumber} • {currentUser.tenantName || 'Tenant'}</p>
          </div>
        </div>

        <button
          type="button"
          onclick={() => authStore.logout()}
          class="px-3 py-1.5 bg-red-50 hover:bg-red-100 text-red-600 dark:bg-red-950/40 dark:hover:bg-red-900/60 dark:text-red-400 font-semibold text-xs rounded-lg border border-red-200 dark:border-red-800 shadow-xs transition-all flex items-center gap-1.5 cursor-pointer active:scale-95 shrink-0"
          title="Sign out of your account"
        >
          <span class="material-symbols-outlined text-[16px]">logout</span>
          <span>Log Out</span>
        </button>
      </div>
    {/if}

    <!-- Tabs Header -->
    <div class="flex items-center gap-2 border-b border-outline-variant/80 pb-0.5">
      <button
        type="button"
        onclick={() => activeTab = 'users'}
        class="px-4 py-2 rounded-t-xl text-xs sm:text-sm font-bold transition-all flex items-center gap-2 cursor-pointer border-b-2
          {activeTab === 'users'
            ? 'border-primary text-primary bg-surface shadow-xs'
            : 'border-transparent text-on-surface-variant hover:text-on-surface hover:bg-surface-container-low'}"
      >
        <span class="material-symbols-outlined text-[18px]">groups</span>
        <span>Team Members</span>
        <span class="px-1.5 py-0.2 rounded-full text-[10px] font-mono bg-surface-container-high text-on-surface font-bold">
          {users.length}
        </span>
      </button>

      {#if canViewRoles}
        <button
          type="button"
          onclick={() => activeTab = 'roles'}
          class="px-4 py-2 rounded-t-xl text-xs sm:text-sm font-bold transition-all flex items-center gap-2 cursor-pointer border-b-2
            {activeTab === 'roles'
              ? 'border-primary text-primary bg-surface shadow-xs'
              : 'border-transparent text-on-surface-variant hover:text-on-surface hover:bg-surface-container-low'}"
        >
          <span class="material-symbols-outlined text-[18px]">shield_person</span>
          <span>Roles & Permissions</span>
          <span class="px-1.5 py-0.2 rounded-full text-[10px] font-mono bg-surface-container-high text-on-surface font-bold">
            {roles.length}
          </span>
        </button>
      {/if}
    </div>

    <!-- TAB 1: USERS DIRECTORY -->
    {#if activeTab === 'users'}
      <!-- Search & Role Filter Bar -->
      <section class="bg-surface p-3.5 sm:p-4 rounded-2xl border border-outline-variant shadow-xs flex flex-col md:flex-row justify-between items-stretch md:items-center gap-3">
        <div class="flex-1 max-w-md">
          <SearchBox bind:value={searchQuery} placeholder="Search team by name, phone or role..." />
        </div>

        <div class="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0 shrink-0">
          <span class="text-xs font-semibold text-on-surface-variant mr-1 font-mono">Role:</span>
          <button
            type="button"
            onclick={() => roleFilter = 'All'}
            class="px-2.5 py-1.5 rounded-lg text-xs font-bold whitespace-nowrap transition-colors cursor-pointer {roleFilter === 'All' ? 'bg-primary text-white shadow-xs' : 'bg-surface-container-low border border-outline-variant text-on-surface-variant hover:bg-surface-container'}"
          >
            All
          </button>
          {#each roles as r}
            <button
              type="button"
              onclick={() => roleFilter = r.name}
              class="px-2.5 py-1.5 rounded-lg text-xs font-bold whitespace-nowrap transition-colors cursor-pointer {roleFilter === r.name ? 'bg-primary text-white shadow-xs' : 'bg-surface-container-low border border-outline-variant text-on-surface-variant hover:bg-surface-container'}"
            >
              {r.name}
            </button>
          {/each}
        </div>
      </section>

      <!-- Users List / Table -->
      {#if filteredUsers.length > 0}
        <!-- Mobile List Cards -->
        <div class="block md:hidden space-y-2.5">
          {#each filteredUsers as user (user.id)}
            <div class="bg-surface p-3.5 rounded-xl border border-outline-variant shadow-xs space-y-2">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="material-symbols-outlined text-primary text-[20px]">person</span>
                  <span class="font-bold text-sm text-on-surface">{user.name || 'Anonymous User'}</span>
                </div>
                <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase font-mono bg-primary/10 text-primary">
                  {user.role}
                </span>
              </div>

              <div class="text-xs font-mono text-on-surface-variant flex items-center justify-between pt-1 border-t border-dashed border-outline-variant">
                <span>{user.phoneNumber}</span>
                {#if canManageUsers}
                  <button
                    onclick={() => openEditUser(user)}
                    class="px-2 py-1 text-[11px] font-bold text-primary hover:bg-primary/10 rounded transition-colors"
                  >
                    Edit
                  </button>
                {/if}
              </div>
            </div>
          {/each}
        </div>

        <!-- Desktop Table -->
        <div class="hidden md:block w-full overflow-x-auto rounded-2xl border border-outline-variant bg-surface shadow-xs">
          <table class="w-full text-left border-collapse text-sm">
            <thead>
              <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
                <th class="px-5 py-3.5">Name</th>
                <th class="px-5 py-3.5">Phone Number</th>
                <th class="px-5 py-3.5">Assigned Role</th>
                <th class="px-5 py-3.5">Last Login</th>
                <th class="px-5 py-3.5">Created</th>
                {#if canManageUsers}
                  <th class="px-5 py-3.5 text-right">Actions</th>
                {/if}
              </tr>
            </thead>
            <tbody class="divide-y divide-outline-variant/50 text-on-surface">
              {#each filteredUsers as user (user.id)}
                <tr class="hover:bg-surface-container-low/70 transition-colors">
                  <td class="px-5 py-3.5 font-bold text-on-surface flex items-center gap-2">
                    <div class="w-7 h-7 rounded-lg bg-primary/10 text-primary flex items-center justify-center text-xs font-mono font-bold">
                      {(user.name || user.phoneNumber || 'U').charAt(0).toUpperCase()}
                    </div>
                    <span>{user.name || '—'}</span>
                  </td>
                  <td class="px-5 py-3.5 font-mono text-xs text-on-surface-variant">{user.phoneNumber}</td>
                  <td class="px-5 py-3.5">
                    <span class="px-2.5 py-1 rounded-full text-xs font-bold uppercase font-mono tracking-wider bg-primary/10 text-primary">
                      {user.role}
                    </span>
                  </td>
                  <td class="px-5 py-3.5 text-xs text-on-surface-variant font-mono">{formatDate(user.lastLoginAtUtc)}</td>
                  <td class="px-5 py-3.5 text-xs text-on-surface-variant font-mono">{formatDate(user.createdAtUtc)}</td>
                  {#if canManageUsers}
                    <td class="px-5 py-3.5 text-right">
                      <button
                        onclick={() => openEditUser(user)}
                        class="px-2.5 py-1 text-xs font-semibold text-primary hover:bg-primary/10 rounded-lg transition-colors cursor-pointer"
                      >
                        Edit
                      </button>
                    </td>
                  {/if}
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <EmptyState icon="person_off" title="No users found" message="Try changing your search query or role filter." />
      {/if}

    <!-- TAB 2: ROLES & PERMISSIONS -->
    {:else if activeTab === 'roles'}
      <!-- Roles Search & Overview Bar -->
      <section class="bg-surface p-3.5 sm:p-4 rounded-2xl border border-outline-variant shadow-xs flex flex-col sm:flex-row justify-between items-stretch sm:items-center gap-3">
        <div class="flex-1 max-w-md">
          <SearchBox bind:value={roleSearchQuery} placeholder="Search roles by title or capability..." />
        </div>

        {#if canManageRoles}
          <button
            onclick={openCreateRole}
            class="px-4 py-2 rounded-xl text-xs font-bold bg-primary hover:bg-blue-700 text-white transition-all flex items-center justify-center gap-1.5 shadow-xs cursor-pointer active:scale-95 shrink-0"
          >
            <span class="material-symbols-outlined text-[16px]">add</span>
            <span>New Custom Role</span>
          </button>
        {/if}
      </section>

      <!-- Role Cards Grid -->
      {#if filteredRoles.length > 0}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {#each filteredRoles as role (role.id)}
            <RoleCard
              {role}
              assignedCount={getRoleMemberCount(role)}
              canManage={canManageRoles}
              onEdit={openEditRole}
              onDuplicate={openDuplicateRole}
              onDelete={openDeleteRole}
            />
          {/each}
        </div>
      {:else}
        <EmptyState icon="shield" title="No roles found" message="Try changing your search term or create a new custom role." />
      {/if}
    {/if}
  {/if}
</main>

<!-- Modals -->
<AddUserModal
  bind:isOpen={showAddUserModal}
  availableRoles={roles}
  onSuccess={(phone) => {
    successMessage = `Successfully added team member ${phone}`;
    refreshAll();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to add user.';
  }}
/>

<EditUserModal
  bind:isOpen={showEditUserModal}
  user={editingUser}
  availableRoles={roles}
  onSuccess={(phone) => {
    successMessage = `Updated profile for ${phone}`;
    refreshAll();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to update user profile.';
  }}
/>

<RoleModal
  bind:isOpen={showRoleModal}
  role={editingRole}
  onSuccess={(saved) => {
    successMessage = `Successfully saved role '${saved?.name || editingRole?.name || 'custom role'}'`;
    refreshAll();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to save role.';
  }}
/>

<DeleteRoleModal
  bind:isOpen={showDeleteRoleModal}
  role={deletingRole}
  assignedCount={deletingRole ? getRoleMemberCount(deletingRole) : 0}
  onSuccess={(deleted) => {
    successMessage = `Deleted custom role '${deleted.name}'`;
    refreshAll();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to delete role.';
  }}
/>
