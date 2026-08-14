<script>
  import { authStore } from '../../stores/authStore.js';

  /** @type {{ title?: string }} */
  let { title = 'PRO-FAB ERP' } = $props();

  let user = $derived($authStore.user);
  let isAuthenticated = $derived($authStore.isAuthenticated);

  function handleLoginClick() {
    authStore.openModal();
  }

  function handleLogoutClick() {
    authStore.logout();
  }
</script>

<header class="fixed top-0 w-full z-40 bg-white/90 dark:bg-slate-900/90 backdrop-blur-md shadow-sm flex justify-between items-center px-4 md:px-8 h-14 border-b border-slate-200 dark:border-slate-800">
  <div class="flex items-center gap-2 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors p-2 rounded-xl active:opacity-80">
    <span class="material-symbols-outlined text-primary text-[24px]">factory</span>
    <div class="flex flex-col">
      <span class="font-bold text-base tracking-tight text-slate-900 dark:text-white font-['Inter'] leading-none">{title}</span>
      {#if user?.tenantName}
        <span class="text-[10px] font-semibold text-slate-500 font-['JetBrains_Mono'] tracking-wider uppercase">{user.tenantName}</span>
      {/if}
    </div>
  </div>

  <div class="flex items-center gap-2 sm:gap-3">
    {#if isAuthenticated && user}
      <!-- User Phone Badge -->
      <div class="hidden md:flex items-center gap-2 px-3 py-1 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-full">
        <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
        <span class="text-xs font-semibold text-slate-700 dark:text-slate-200 font-['JetBrains_Mono']">{user.phoneNumber}</span>
      </div>

      <!-- Logout Button -->
      <button
        onclick={handleLogoutClick}
        title="Sign Out"
        class="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-slate-100 hover:bg-red-50 text-slate-700 hover:text-red-600 dark:bg-slate-800 dark:hover:bg-red-950/40 dark:text-slate-300 dark:hover:text-red-400 text-xs font-semibold transition-all border border-slate-200 dark:border-slate-700 active:scale-95"
      >
        <span class="material-symbols-outlined text-base">logout</span>
        <span class="hidden sm:inline">Logout</span>
      </button>
    {:else}
      <!-- Login Button -->
      <button
        onclick={handleLoginClick}
        class="flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl bg-primary hover:bg-blue-700 text-white text-xs font-semibold shadow-sm shadow-primary/25 transition-all active:scale-95"
      >
        <span class="material-symbols-outlined text-base">login</span>
        <span>Sign In</span>
      </button>
    {/if}
  </div>
</header>
