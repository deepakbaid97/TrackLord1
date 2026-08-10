<script>
  import { onMount } from 'svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { getUsers } from '$lib/api.js';
  import { authStore } from '$lib/stores/authStore.js';

  let loading = $state(true);
  let errorMessage = $state('');
  let searchQuery = $state('');
  let roleFilter = $state('All');

  /** @type {Array<any>} */
  let users = $state([]);

  let currentUser = $derived($authStore.user);
  let isAuthenticated = $derived($authStore.isAuthenticated);

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

  let filteredUsers = $derived(
    users.filter(u => {
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

<!-- Header -->
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
      <span>Loading registered users...</span>
    </div>
  {:else}
    <!-- Top Hero Card & Current Session Banner -->
    <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-6 rounded-xl border border-outline-variant shadow-sm">
      <div>
        <h2 class="font-headline-md text-headline-md text-primary font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-[28px]">group_add</span>
          User Directory & Phone Auth Accounts
        </h2>
        <p class="text-sm text-on-surface-variant mt-1">
          Registered user accounts verified via Firebase Phone OTP authentication and JWT tokens.
        </p>
      </div>

      {#if isAuthenticated && currentUser}
        <!-- Active Session Card with Logout -->
        <div class="flex items-center gap-3 p-3 bg-slate-100 dark:bg-slate-800/80 rounded-xl border border-slate-200 dark:border-slate-700 w-full sm:w-auto">
          <div class="w-10 h-10 rounded-xl bg-primary flex items-center justify-center text-white font-bold text-sm shadow-sm">
            <span class="material-symbols-outlined text-[20px]">person</span>
          </div>
          <div class="flex-1 min-w-[120px]">
            <div class="text-[10px] font-mono font-bold text-slate-400 uppercase tracking-wider">Logged In Session</div>
            <div class="text-xs font-bold text-slate-900 dark:text-white font-['JetBrains_Mono']">{currentUser.phoneNumber}</div>
          </div>
          <button
            onclick={handleLogout}
            class="px-3 py-1.5 bg-red-600 hover:bg-red-700 text-white font-semibold text-xs rounded-lg shadow-sm transition-all flex items-center gap-1 active:scale-95 cursor-pointer"
          >
            <span class="material-symbols-outlined text-[16px]">logout</span>
            <span>Sign Out</span>
          </button>
        </div>
      {/if}
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
          placeholder="Search by phone number, name, or role..."
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

    <!-- User Table -->
    <div class="bg-surface rounded-xl border border-outline-variant shadow-sm overflow-hidden">
      <div class="w-full overflow-x-auto">
        <table class="w-full text-left border-collapse text-sm">
          <thead>
            <tr class="bg-surface-container-low border-b border-outline-variant text-xs font-semibold uppercase tracking-wider text-on-surface-variant font-mono">
              <th class="px-6 py-3">User / Identity</th>
              <th class="px-6 py-3">Phone Number</th>
              <th class="px-6 py-3">Role</th>
              <th class="px-6 py-3">Status</th>
              <th class="px-6 py-3">Firebase UID</th>
              <th class="px-6 py-3">Registered Date</th>
              <th class="px-6 py-3">Last Login</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant">
            {#each filteredUsers as user}
              {@const isMe = currentUser && currentUser.phoneNumber === user.phoneNumber}
              <tr class="hover:bg-surface-container-low/70 transition-colors">
                <!-- User Name / Badge -->
                <td class="px-6 py-4">
                  <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-full bg-gradient-to-tr from-primary to-blue-600 flex items-center justify-center text-white font-bold text-sm shadow-sm">
                      {user.name ? user.name.charAt(0).toUpperCase() : user.phoneNumber ? user.phoneNumber.slice(-2) : 'U'}
                    </div>
                    <div>
                      <div class="font-bold text-slate-900 dark:text-white flex items-center gap-2">
                        <span>{user.name || 'Phone Verified User'}</span>
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

                <!-- Firebase UID -->
                <td class="px-6 py-4 text-xs font-mono text-on-surface-variant truncate max-w-[150px]">
                  {user.firebaseUid || 'N/A'}
                </td>

                <!-- Registered Date -->
                <td class="px-6 py-4 text-xs text-on-surface-variant font-mono">
                  {formatDate(user.createdAtUtc)}
                </td>

                <!-- Last Login -->
                <td class="px-6 py-4 text-xs font-mono font-semibold text-primary">
                  {formatDate(user.lastLoginAtUtc)}
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
