<script>
  import { onMount } from 'svelte';
  import { getUsers, createUser, updateUser } from '$lib/api.js';
  import { authStore } from '$lib/stores/authStore.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let successMessage = $state('');
  let searchQuery = $state('');
  let roleFilter = $state('All');

  /** @type {Array<any>} */
  let users = $state([]);

  let currentUser = $derived($authStore.user);
  let isAuthenticated = $derived($authStore.isAuthenticated);
  let isAdmin = $derived(currentUser?.role === 'Admin');

  // Add User Modal State
  let showAddModal = $state(false);
  let addFormName = $state('');
  let addFormPhone = $state('');
  let addFormRole = $state('Operator');
  let addIsSubmitting = $state(false);
  let addModalError = $state('');

  // Edit User Modal State
  let showEditModal = $state(false);
  /** @type {any} */
  let editingUser = $state(null);
  let editFormName = $state('');
  let editFormRole = $state('Operator');
  let editIsSubmitting = $state(false);
  let editModalError = $state('');

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

  function handleLogout() {
    authStore.logout();
  }

  function openAddModal() {
    addFormName = '';
    addFormPhone = '';
    addFormRole = 'Operator';
    addModalError = '';
    showAddModal = true;
  }

  function closeAddModal() {
    showAddModal = false;
  }

  async function handleAddUserSubmit(e) {
    if (e) e.preventDefault();
    const cleanPhone = addFormPhone.replace(/\D/g, '');
    if (cleanPhone.length < 10) {
      addModalError = 'Please enter a valid 10-digit mobile number.';
      return;
    }

    addIsSubmitting = true;
    addModalError = '';
    try {
      const fullPhone = `+91${cleanPhone.slice(-10)}`;
      await createUser({
        phoneNumber: fullPhone,
        name: addFormName.trim() || null,
        role: addFormRole
      });
      successMessage = `Successfully added new team member ${fullPhone}`;
      closeAddModal();
      await refreshUsers();
      setTimeout(() => successMessage = '', 4000);
    } catch (/** @type {any} */ err) {
      addModalError = err.message || 'Failed to add user.';
    } finally {
      addIsSubmitting = false;
    }
  }

  /** @param {any} user */
  function openEditModal(user) {
    editingUser = user;
    editFormName = user.name || '';
    editFormRole = user.role || 'Operator';
    editModalError = '';
    showEditModal = true;
  }

  function closeEditModal() {
    showEditModal = false;
    editingUser = null;
  }

  async function handleEditUserSubmit(e) {
    if (e) e.preventDefault();
    if (!editingUser) return;

    editIsSubmitting = true;
    editModalError = '';
    try {
      await updateUser(editingUser.id, {
        name: editFormName.trim() || null,
        role: editFormRole
      });
      successMessage = `Updated profile for ${editingUser.phoneNumber}`;
      closeEditModal();
      await refreshUsers();
      setTimeout(() => successMessage = '', 4000);
    } catch (/** @type {any} */ err) {
      editModalError = err.message || 'Failed to update user profile.';
    } finally {
      editIsSubmitting = false;
    }
  }

  let isManager = $derived(currentUser?.role === 'Admin' || currentUser?.role === 'Supervisor');

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
</script>

<!-- Top Header Navigation -->
<header class="fixed top-0 w-full z-40 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-4 md:px-8 h-16 border-b border-outline-variant">
  <div class="flex items-center gap-3">
    <div class="flex items-center gap-2 p-2 bg-primary/10 rounded-full">
      <span class="material-symbols-outlined text-primary text-[24px]">manage_accounts</span>
    </div>
    <div>
      <div class="font-bold text-lg text-primary leading-none">TRACKLORD ERP</div>
      <div class="text-xs text-on-surface-variant">User & Access Management</div>
    </div>
  </div>

  <div class="flex items-center gap-2 sm:gap-3">
    {#if isAdmin}
      <button
        onclick={openAddModal}
        class="px-3.5 py-1.5 rounded-lg bg-primary hover:bg-blue-700 text-white font-bold text-xs shadow-md shadow-primary/20 flex items-center gap-1.5 cursor-pointer transition-all active:scale-95"
      >
        <span class="material-symbols-outlined text-[18px]">person_add</span>
        <span class="hidden sm:inline">Add Team Member</span>
        <span class="sm:hidden">Add</span>
      </button>
    {/if}

    <button
      onclick={() => refreshUsers()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Refresh
    </button>

    {#if isAuthenticated && currentUser}
      <button
        onclick={handleLogout}
        class="px-3.5 py-1.5 rounded-lg bg-red-50 hover:bg-red-100 text-red-600 dark:bg-red-950/50 dark:hover:bg-red-900/60 dark:text-red-300 font-semibold text-xs border border-red-200 dark:border-red-800 flex items-center gap-1.5 cursor-pointer transition-all active:scale-95 shadow-sm"
      >
        <span class="material-symbols-outlined text-[18px]">logout</span>
        <span>Logout</span>
      </button>
    {/if}
  </div>
</header>

<!-- Main Container -->
<main class="max-w-[1440px] mx-auto p-4 md:p-8 space-y-6 pt-10">

  <!-- Success Notification -->
  {#if successMessage}
    <div class="p-4 bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 rounded-xl text-emerald-800 dark:text-emerald-300 text-sm flex items-center justify-between shadow-sm animate-fadeIn">
      <div class="flex items-center gap-2">
        <span class="material-symbols-outlined text-emerald-600">check_circle</span>
        <span class="font-medium">{successMessage}</span>
      </div>
      <button onclick={() => successMessage = ''} class="text-emerald-600 hover:text-emerald-800">
        <span class="material-symbols-outlined text-[18px]">close</span>
      </button>
    </div>
  {/if}

  <!-- Error Banner -->
  {#if errorMessage}
    <div class="p-4 bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 rounded-xl text-red-700 dark:text-red-300 text-sm flex items-center justify-between shadow-sm">
      <div class="flex items-center gap-2">
        <span class="material-symbols-outlined text-red-500">error</span>
        <span>{errorMessage}</span>
      </div>
      <button onclick={() => errorMessage = ''} class="text-red-500 hover:text-red-700">
        <span class="material-symbols-outlined text-[18px]">close</span>
      </button>
    </div>
  {/if}

  {#if loading}
    <div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
      <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
      <span>Loading tenant directory...</span>
    </div>
  {:else}
    <!-- Top Hero Card & Action Banner -->
    <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
      <div>
        <h2 class="font-headline-md text-headline-md text-primary font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-[28px]">group_add</span>
          User Directory & Tenant Access Controls
        </h2>
        <p class="text-sm text-on-surface-variant mt-1">
          Manage team members, roles, and verified phone accounts for your organization.
        </p>
      </div>

      <div class="flex items-center gap-3 w-full sm:w-auto">
        {#if isAdmin}
          <button
            onclick={openAddModal}
            class="px-4 py-2.5 bg-primary hover:bg-blue-700 text-white font-bold text-xs rounded-xl shadow-md shadow-primary/25 transition-all flex items-center justify-center gap-2 cursor-pointer active:scale-95 w-full sm:w-auto"
          >
            <span class="material-symbols-outlined text-base">person_add</span>
            <span>+ Add New User</span>
          </button>
        {/if}

        {#if isAuthenticated && currentUser}
          <!-- Active Session Card -->
          <div class="flex items-center gap-3 p-2.5 bg-slate-100 dark:bg-slate-800/80 rounded-xl border border-slate-200 dark:border-slate-700">
            <div class="w-9 h-9 rounded-xl bg-primary flex items-center justify-center text-white font-bold text-sm shadow-sm">
              <span class="material-symbols-outlined text-[18px]">person</span>
            </div>
            <div class="hidden md:block">
              <div class="text-[9px] font-mono font-bold text-slate-400 uppercase tracking-wider">Logged In Session</div>
              <div class="text-xs font-bold text-slate-900 dark:text-white font-['JetBrains_Mono']">{currentUser.phoneNumber}</div>
            </div>
          </div>
        {/if}
      </div>
    </div>

    <!-- User KPI Stats Cards -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-primary/10 rounded-lg text-primary">
          <span class="material-symbols-outlined text-[24px]">group</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Total Users</div>
          <div class="text-2xl font-bold text-on-surface">{users.length}</div>
        </div>
      </div>

      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-emerald-500/10 rounded-lg text-emerald-600">
          <span class="material-symbols-outlined text-[24px]">verified_user</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Active Accounts</div>
          <div class="text-2xl font-bold text-on-surface">{users.filter(u => u.isActive).length}</div>
        </div>
      </div>

      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-purple-500/10 rounded-lg text-purple-600">
          <span class="material-symbols-outlined text-[24px]">admin_panel_settings</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Admins</div>
          <div class="text-2xl font-bold text-on-surface">{users.filter(u => u.role === 'Admin').length}</div>
        </div>
      </div>

      <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex items-center gap-3">
        <div class="p-3 bg-blue-500/10 rounded-lg text-blue-600">
          <span class="material-symbols-outlined text-[24px]">engineering</span>
        </div>
        <div>
          <div class="text-xs text-on-surface-variant uppercase font-mono font-bold">Operators</div>
          <div class="text-2xl font-bold text-on-surface">{users.filter(u => u.role === 'Operator').length}</div>
        </div>
      </div>
    </div>

    <!-- Search & Filter Controls -->
    <div class="bg-surface p-4 rounded-xl border border-outline-variant shadow-sm flex flex-col sm:flex-row items-center gap-4">
      <!-- Search input -->
      <div class="relative flex-1 w-full">
        <span class="material-symbols-outlined absolute left-3 top-2.5 text-on-surface-variant text-[20px]">search</span>
        <input
          type="text"
          bind:value={searchQuery}
          placeholder="Search by name, phone number, or role..."
          class="w-full pl-10 pr-4 py-2 bg-surface-container-low border border-outline-variant rounded-lg text-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/20"
        />
      </div>

      <!-- Role Filter -->
      <div class="flex items-center gap-2 w-full sm:w-auto">
        <span class="text-xs font-mono font-bold uppercase text-on-surface-variant">Role:</span>
        <select
          bind:value={roleFilter}
          class="px-3 py-2 bg-surface-container-low border border-outline-variant rounded-lg text-xs font-bold text-on-surface focus:outline-none focus:ring-2 focus:ring-primary/20"
        >
          <option value="All">All Roles</option>
          <option value="Admin">Admin</option>
          <option value="Supervisor">Supervisor</option>
          <option value="Operator">Operator</option>
        </select>
      </div>
    </div>

    <!-- User Directory Table -->
    <div class="bg-surface rounded-xl border border-outline-variant shadow-sm overflow-hidden">
      <div class="w-full overflow-x-auto">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-6 py-3">Full Name / User Identity</th>
              <th class="px-6 py-3">Phone Number</th>
              <th class="px-6 py-3">Role</th>
              <th class="px-6 py-3">Status</th>
              <th class="px-6 py-3">Registered Date</th>
              <th class="px-6 py-3">Last Login</th>
              <th class="px-6 py-3 text-right">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant">
            {#each filteredUsers as user}
              {@const isMe = currentUser && currentUser.phoneNumber === user.phoneNumber}
              {@const canEdit = isMe || isAdmin}
              <tr class="hover:bg-surface-container-low/70 transition-colors">
                <!-- User Name / Avatar -->
                <td class="px-6 py-4">
                  <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-full bg-gradient-to-tr from-primary to-blue-600 flex items-center justify-center text-white font-bold text-sm shadow-sm shrink-0">
                      {user.name ? user.name.charAt(0).toUpperCase() : user.phoneNumber ? user.phoneNumber.slice(-2) : 'U'}
                    </div>
                    <div>
                      <div class="font-bold text-slate-900 dark:text-white flex items-center gap-2">
                        <span>{user.name || 'Unnamed Team Member'}</span>
                        {#if isMe}
                          <span class="px-2 py-0.5 rounded-full text-[10px] font-extrabold uppercase bg-primary text-white shadow-sm">You</span>
                        {/if}
                      </div>
                      <div class="text-xs text-on-surface-variant font-mono">ID: {user.id.slice(0, 8)}...</div>
                    </div>
                  </div>
                </td>

                <!-- Phone Number -->
                <td class="px-6 py-4 font-mono font-bold text-slate-800 dark:text-slate-200">
                  <div class="flex items-center gap-1.5">
                    <span class="material-symbols-outlined text-base text-primary">smartphone</span>
                    <span>{user.phoneNumber}</span>
                  </div>
                </td>

                <!-- Role -->
                <td class="px-6 py-4">
                  <span class="px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider
                    {user.role === 'Admin' ? 'bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300 border border-purple-200 dark:border-purple-800' :
                     user.role === 'Supervisor' ? 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800' :
                     'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300 border border-blue-200 dark:border-blue-800'}">
                    {user.role}
                  </span>
                </td>

                <!-- Status -->
                <td class="px-6 py-4">
                  {#if user.isActive}
                    <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800">
                      <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                      <span>Active</span>
                    </span>
                  {:else}
                    <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400">
                      <span class="w-2 h-2 rounded-full bg-slate-400"></span>
                      <span>Inactive</span>
                    </span>
                  {/if}
                </td>

                <!-- Registered Date -->
                <td class="px-6 py-4 text-xs text-on-surface-variant font-mono">
                  {formatDate(user.createdAtUtc)}
                </td>

                <!-- Last Login -->
                <td class="px-6 py-4 text-xs font-mono font-semibold text-primary">
                  {formatDate(user.lastLoginAtUtc)}
                </td>

                <!-- Edit Action -->
                <td class="px-6 py-4 text-right">
                  {#if canEdit}
                    <button
                      onclick={() => openEditModal(user)}
                      class="px-2.5 py-1.5 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 text-xs font-semibold flex items-center gap-1 ml-auto cursor-pointer transition-all active:scale-95"
                    >
                      <span class="material-symbols-outlined text-[16px] text-primary">edit</span>
                      <span>Edit</span>
                    </button>
                  {:else}
                    <span class="text-xs text-slate-400 font-mono">-</span>
                  {/if}
                </td>
              </tr>
            {/each}

            {#if filteredUsers.length === 0}
              <tr>
                <td colspan="7" class="p-8 text-center text-on-surface-variant text-sm">
                  No users found matching your search criteria.
                </td>
              </tr>
            {/if}
          </tbody>
        </table>
      </div>
    </div>
  {/if}
</main>

<!-- ADD USER MODAL (ADMIN ONLY) -->
{#if showAddModal}
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-md transition-all animate-fadeIn">
    <div class="relative w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl shadow-2xl overflow-hidden p-6 sm:p-8">
      
      <!-- Top Icon & Header -->
      <div class="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100 dark:border-slate-800">
        <div class="w-12 h-12 rounded-2xl bg-primary/10 text-primary flex items-center justify-center font-bold shadow-sm">
          <span class="material-symbols-outlined text-[28px]">person_add</span>
        </div>
        <div>
          <h3 class="text-lg font-bold text-slate-900 dark:text-white">Add New Team Member</h3>
          <p class="text-xs text-slate-500 dark:text-slate-400">Register a new user in your tenant organization</p>
        </div>
      </div>

      <!-- Error Alert -->
      {#if addModalError}
        <div class="mb-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 text-red-600 dark:text-red-300 text-xs flex items-center gap-2">
          <span class="material-symbols-outlined text-base shrink-0">error</span>
          <span>{addModalError}</span>
        </div>
      {/if}

      <!-- Add User Form -->
      <form onsubmit={handleAddUserSubmit} class="space-y-4">
        <!-- Full Name -->
        <div>
          <label for="add-user-name" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1 font-['JetBrains_Mono']">
            Full Name
          </label>
          <input
            id="add-user-name"
            type="text"
            bind:value={addFormName}
            placeholder="e.g. Rajesh Kumar"
            class="w-full h-11 px-4 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm font-medium text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        <!-- Phone Number -->
        <div>
          <label for="add-user-phone" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1 font-['JetBrains_Mono']">
            Mobile Number *
          </label>
          <div class="flex items-center gap-2">
            <div class="h-11 px-3 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-xs font-bold text-slate-800 dark:text-slate-200 flex items-center justify-center gap-1.5 select-none shrink-0 font-['JetBrains_Mono']">
              <span>🇮🇳</span>
              <span>+91</span>
            </div>
            <input
              id="add-user-phone"
              type="tel"
              bind:value={addFormPhone}
              placeholder="98765 43210"
              maxlength="10"
              required
              class="flex-1 h-11 px-4 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm font-medium text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-primary font-['JetBrains_Mono'] tracking-wide"
            />
          </div>
        </div>

        <!-- Role Selector -->
        <div>
          <label for="add-user-role" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1 font-['JetBrains_Mono']">
            Assigned Role *
          </label>
          <select
            id="add-user-role"
            bind:value={addFormRole}
            class="w-full h-11 px-3 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-xs font-bold text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="Operator">Operator (Floor Operations)</option>
            <option value="Supervisor">Supervisor (Line Manager)</option>
            <option value="Admin">Admin (Full Control)</option>
          </select>
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-800">
          <button
            type="button"
            onclick={closeAddModal}
            class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all cursor-pointer"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={addIsSubmitting || !addFormPhone}
            class="px-5 py-2.5 bg-primary hover:bg-blue-700 disabled:opacity-50 text-white font-bold text-xs rounded-xl shadow-md shadow-primary/25 transition-all flex items-center gap-2 cursor-pointer active:scale-95"
          >
            {#if addIsSubmitting}
              <span class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
              <span>Adding User...</span>
            {:else}
              <span class="material-symbols-outlined text-base">check</span>
              <span>Save & Add User</span>
            {/if}
          </button>
        </div>
      </form>
    </div>
  </div>
{/if}

<!-- EDIT USER MODAL -->
{#if showEditModal && editingUser}
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-md transition-all animate-fadeIn">
    <div class="relative w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl shadow-2xl overflow-hidden p-6 sm:p-8">
      
      <!-- Top Icon & Header -->
      <div class="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100 dark:border-slate-800">
        <div class="w-12 h-12 rounded-2xl bg-primary/10 text-primary flex items-center justify-center font-bold shadow-sm">
          <span class="material-symbols-outlined text-[28px]">edit_square</span>
        </div>
        <div>
          <h3 class="text-lg font-bold text-slate-900 dark:text-white">Edit User Profile</h3>
          <p class="text-xs text-slate-500 dark:text-slate-400">{editingUser.phoneNumber}</p>
        </div>
      </div>

      <!-- Error Alert -->
      {#if editModalError}
        <div class="mb-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 text-red-600 dark:text-red-300 text-xs flex items-center gap-2">
          <span class="material-symbols-outlined text-base shrink-0">error</span>
          <span>{editModalError}</span>
        </div>
      {/if}

      <!-- Edit Form -->
      <form onsubmit={handleEditUserSubmit} class="space-y-4">
        <!-- Full Name -->
        <div>
          <label for="edit-user-name" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1 font-['JetBrains_Mono']">
            Full Name
          </label>
          <input
            id="edit-user-name"
            type="text"
            bind:value={editFormName}
            placeholder="Enter full name"
            class="w-full h-11 px-4 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm font-medium text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        <!-- Role (Editable if Admin, Read-only if User self-editing) -->
        <div>
          <label for="edit-user-role" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1 font-['JetBrains_Mono']">
            Role
          </label>
          {#if isAdmin}
            <select
              id="edit-user-role"
              bind:value={editFormRole}
              class="w-full h-11 px-3 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-xs font-bold text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary"
            >
              <option value="Operator">Operator</option>
              <option value="Supervisor">Supervisor</option>
              <option value="Admin">Admin</option>
            </select>
          {:else}
            <div class="h-11 px-4 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-xs font-bold text-slate-700 dark:text-slate-300 flex items-center font-mono">
              {editingUser.role} (Role changes require Admin privileges)
            </div>
          {/if}
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-800">
          <button
            type="button"
            onclick={closeEditModal}
            class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all cursor-pointer"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={editIsSubmitting}
            class="px-5 py-2.5 bg-primary hover:bg-blue-700 disabled:opacity-50 text-white font-bold text-xs rounded-xl shadow-md shadow-primary/25 transition-all flex items-center gap-2 cursor-pointer active:scale-95"
          >
            {#if editIsSubmitting}
              <span class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
              <span>Updating Profile...</span>
            {:else}
              <span class="material-symbols-outlined text-base">save</span>
              <span>Save Changes</span>
            {/if}
          </button>
        </div>
      </form>
    </div>
  </div>
{/if}
