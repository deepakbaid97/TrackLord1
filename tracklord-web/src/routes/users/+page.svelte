<script>
  import { onMount } from 'svelte';
  import { getUsers } from '$lib/api.js';
  import { authStore } from '$lib/stores/authStore.js';
  import Callout from '$lib/components/ui/Callout.svelte';
  import LoadingSpinner from '$lib/components/ui/LoadingSpinner.svelte';
  import SearchBox from '$lib/components/ui/SearchBox.svelte';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import AddUserModal from '$lib/components/features/users/AddUserModal.svelte';
  import EditUserModal from '$lib/components/features/users/EditUserModal.svelte';
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import { usePageHeader } from '$lib/utils/usePageHeader.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');
  let searchQuery = $state('');
  let roleFilter = $state('All');

  /** @type {Array<any>} */
  let users = $state([]);

  let currentUser = $derived($authStore.user);
  let isAuthenticated = $derived($authStore.isAuthenticated);
  let isAdmin = $derived(currentUser?.role?.toLowerCase() === 'admin');
  let isManager = $derived(currentUser?.role?.toLowerCase() === 'admin' || currentUser?.role?.toLowerCase() === 'supervisor');

  // Modals State
  let showAddModal = $state(false);
  let showEditModal = $state(false);
  /** @type {any} */
  let editingUser = $state(null);

  usePageHeader(() => {
    /** @type {import('$lib/stores/headerStore.js').HeaderAction[]} */
    const actions = [
      {
        label: 'Refresh',
        icon: 'refresh',
        variant: 'outline',
        onclick: () => refreshUsers()
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
      title: 'User Management',
      subtitle: 'Access & Operators',
      icon: 'manage_accounts',
      actions
    };
  });

  onMount(async () => {
    await refreshUsers();
  });

  async function refreshUsers() {
    loading = true;
    errorMessage = '';
    try {
      const data = await getUsers();
      users = data || [];
    } catch (/** @type {any} */ err) {
      errorMessage = err.message || 'Failed to load user directory.';
    } finally {
      loading = false;
    }
  }

  let filteredUsers = $derived(
    users.filter(u => {
      // Non-admin / non-supervisor can only see their own profile
      if (!isManager && currentUser && currentUser.phoneNumber !== u.phoneNumber) {
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

  function openEditModal(user) {
    editingUser = user;
    showEditModal = true;
  }
</script>

<Callout bind:message={errorMessage} type="error" />
<Callout bind:message={successMessage} type="success" />

<main class="max-w-[1440px] mx-auto px-3 py-2 sm:px-6 sm:py-3 md:px-8 flex flex-col gap-2.5 sm:gap-4">
  {#if loading}
    <LoadingSpinner message="Loading tenant directory..." />
  {:else}
    <!-- Top Hero Card & Action Banner -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-surface p-3.5 sm:p-5 rounded-xl border border-outline-variant shadow-sm">
      <div class="flex items-center gap-2">
        <div class="p-1.5 bg-primary/10 rounded-lg text-primary sm:hidden">
          <span class="material-symbols-outlined text-[20px]">groups</span>
        </div>
        <h2 class="text-lg sm:text-2xl text-primary font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-[26px] hidden sm:inline">groups</span>
          <span>User Directory</span>
        </h2>
      </div>

      <div class="flex items-center gap-2 w-full sm:w-auto justify-end">
        <button
          onclick={() => tenantConfigStore.openModal()}
          class="flex-1 sm:flex-none px-3 py-1.5 sm:px-4 sm:py-2 bg-surface-container-low hover:bg-primary/10 hover:text-primary dark:bg-slate-800 dark:hover:bg-primary/20 text-on-surface font-semibold text-xs sm:text-sm rounded-lg border border-outline-variant shadow-xs transition-all flex items-center justify-center gap-1.5 cursor-pointer active:scale-95"
          title="Configure Tenant Settings, Visibility & Modes"
        >
          <span class="material-symbols-outlined text-[16px] sm:text-[18px] text-primary">settings</span>
          <span>Tenant Settings</span>
        </button>

        {#if isAdmin}
          <button
            onclick={() => showAddModal = true}
            class="flex-1 sm:flex-none px-3 py-1.5 sm:px-4 sm:py-2 bg-primary hover:bg-blue-700 text-white font-bold text-xs sm:text-sm rounded-lg shadow-sm transition-all flex items-center justify-center gap-1.5 cursor-pointer active:scale-95"
          >
            <span class="material-symbols-outlined text-[16px] sm:text-[18px]">person_add</span>
            <span>Add User</span>
          </button>
        {/if}
      </div>
    </div>

    <!-- Active Session & Tenant Info Card (if logged in) -->
    {#if isAuthenticated && currentUser}
      <div class="flex items-center justify-between p-3 bg-surface-container-low dark:bg-surface-container-high rounded-xl border border-outline-variant shadow-sm text-xs">
        <div class="flex items-center gap-2 min-w-0">
          <div class="w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center font-bold shrink-0">
            <span class="material-symbols-outlined text-[18px]">account_circle</span>
          </div>
          <div class="min-w-0">
            <div class="flex items-center gap-1.5">
              <span class="font-bold text-on-surface text-xs truncate">{currentUser.name || 'User'}</span>
              <span class="px-1.5 py-0.5 rounded text-[9px] font-bold font-mono bg-primary/15 text-primary shrink-0">
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

    <!-- Search & Role Filter Bar -->
    <section class="bg-surface p-3.5 sm:p-5 rounded-2xl border border-outline-variant shadow-sm flex flex-col md:flex-row justify-between items-stretch md:items-center gap-3">
      <div class="flex-1 max-w-md">
        <SearchBox bind:value={searchQuery} placeholder="Search team by name, phone or role..." />
      </div>

      <div class="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0 shrink-0">
        <span class="text-xs font-semibold text-on-surface-variant mr-1 font-mono">Role:</span>
        {#each ['All', 'Operator', 'Supervisor', 'Manager', 'Admin'] as role}
          <button
            type="button"
            onclick={() => roleFilter = role}
            class="px-2.5 py-1.5 rounded-lg text-xs font-bold whitespace-nowrap transition-colors cursor-pointer {roleFilter === role ? 'bg-primary text-white shadow-xs' : 'bg-surface-container-low border border-outline-variant text-on-surface-variant hover:bg-surface-container'}"
          >
            {role}
          </button>
        {/each}
      </div>
    </section>

    <!-- Users List Table / Cards -->
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
              {#if isAdmin}
                <button
                  onclick={() => openEditModal(user)}
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
      <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant bg-surface shadow-sm">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-5 py-3.5">Name</th>
              <th class="px-5 py-3.5">Phone Number</th>
              <th class="px-5 py-3.5">Role</th>
              <th class="px-5 py-3.5">Last Login</th>
              <th class="px-5 py-3.5">Created</th>
              {#if isAdmin}
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
                {#if isAdmin}
                  <td class="px-5 py-3.5 text-right">
                    <button
                      onclick={() => openEditModal(user)}
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
  {/if}
</main>

<AddUserModal
  bind:isOpen={showAddModal}
  onSuccess={(phone) => {
    successMessage = `Successfully added team member ${phone}`;
    refreshUsers();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to add user.';
  }}
/>

<EditUserModal
  bind:isOpen={showEditModal}
  user={editingUser}
  onSuccess={(phone) => {
    successMessage = `Updated profile for ${phone}`;
    refreshUsers();
  }}
  onError={(err) => {
    errorMessage = err.message || 'Failed to update user profile.';
  }}
/>
