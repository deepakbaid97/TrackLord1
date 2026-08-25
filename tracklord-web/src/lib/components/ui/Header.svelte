<script>
  import { page } from '$app/stores';
  import { authStore } from '../../stores/authStore.js';
  import { headerStore } from '../../stores/headerStore.js';

  let user = $derived($authStore.user);
  let isAuthenticated = $derived($authStore.isAuthenticated);
  let pathname = $derived($page.url.pathname);

  // Default page metadata based on current route
  const routeDefaults = {
    '/dashboard': { title: 'Dashboard', icon: 'dashboard', subtitle: 'Overview & Metrics' },
    '/inventory': { title: 'Lot Inventory', icon: 'inventory_2', subtitle: 'Live Production WIP' },
    '/lots': { title: 'Production Lots', icon: 'reorder', subtitle: 'Batches & Directory' },
    '/steps': { title: 'Manufacturing Steps', icon: 'alt_route', subtitle: 'Sequence & Routing' },
    '/materials': { title: 'Raw Materials', icon: 'texture', subtitle: 'Stock & Consumption' },
    '/clients': { title: 'Clients Directory', icon: 'groups', subtitle: 'Buyers & Partners' },
    '/users': { title: 'User Management', icon: 'manage_accounts', subtitle: 'Access & Operators' }
  };

  /** @type {{ title?: string, icon?: string, subtitle?: string }} */
  let currentRouteMeta = $derived.by(() => {
    for (const [route, meta] of Object.entries(routeDefaults)) {
      if (pathname === route || pathname.startsWith(route + '/')) {
        return meta;
      }
    }
    return { title: 'TRACKLORD', icon: 'factory', subtitle: 'Production Management' };
  });

  let displayTitle = $derived($headerStore.title || currentRouteMeta.title);
  let displayIcon = $derived($headerStore.icon || currentRouteMeta.icon);
  let displaySubtitle = $derived($headerStore.subtitle || user?.tenantName || currentRouteMeta.subtitle);
  let actions = $derived($headerStore.actions || []);

  let activeDropdownIndex = $state(/** @type {number | null} */ (null));

  function handleLoginClick() {
    authStore.openModal();
  }

  function handleLogoutClick() {
    authStore.logout();
  }

  /** @param {MouseEvent} e */
  function handleDocumentClick(e) {
    const target = /** @type {HTMLElement} */ (e.target);
    if (activeDropdownIndex !== null && target && !target.closest('.header-dropdown-container')) {
      activeDropdownIndex = null;
    }
  }
</script>

<svelte:window onclick={handleDocumentClick} />

<header class="fixed top-0 w-full z-40 bg-white/95 dark:bg-slate-900/95 backdrop-blur-md shadow-xs flex justify-between items-center px-3 sm:px-6 md:px-8 h-14 border-b border-outline-variant/60">
  <!-- Left: Page Icon, Title & Tenant Subtitle -->
  <div class="flex items-center gap-2.5 min-w-0">
    <div class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 text-primary shrink-0">
      <span class="material-symbols-outlined text-[20px]">{displayIcon}</span>
    </div>
    <div class="flex flex-col min-w-0">
      <div class="flex items-center gap-1.5 truncate">
        <span class="font-bold text-sm sm:text-base tracking-tight text-slate-900 dark:text-white leading-tight truncate">
          {displayTitle}
        </span>
      </div>
      {#if displaySubtitle}
        <span class="text-[10px] font-semibold text-slate-500 dark:text-slate-400 font-mono tracking-wider uppercase truncate">
          {displaySubtitle}
        </span>
      {/if}
    </div>
  </div>

  <!-- Right: Page-Specific Actions & User Profile -->
  <div class="flex items-center gap-1.5 sm:gap-2.5 shrink-0">
    <!-- Dynamic Page Actions from Store -->
    {#if actions && actions.length > 0}
      <div class="flex items-center gap-1.5 sm:gap-2">
        {#each actions as action, idx}
          {#if action.dropdownItems && action.dropdownItems.length > 0}
            <!-- Dropdown Button -->
            <div class="relative header-dropdown-container">
              <button
                type="button"
                disabled={action.disabled}
                onclick={() => activeDropdownIndex = activeDropdownIndex === idx ? null : idx}
                class="px-2.5 sm:px-3 py-1.5 rounded-xl font-bold text-xs flex items-center gap-1 shadow-sm transition-all active:scale-95 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed
                  {action.variant === 'outline'
                    ? 'border border-outline-variant/80 bg-surface-container-low text-on-surface hover:bg-surface-container'
                    : 'bg-primary text-white hover:bg-blue-700'}"
              >
                {#if action.icon}
                  <span class="material-symbols-outlined text-[16px]">{action.icon}</span>
                {/if}
                <span>{action.label}</span>
                <span class="material-symbols-outlined text-[15px] transition-transform duration-200 {activeDropdownIndex === idx ? 'rotate-180' : ''}">
                  expand_more
                </span>
              </button>

              {#if activeDropdownIndex === idx}
                <div class="absolute right-0 top-full mt-2 w-52 bg-surface dark:bg-slate-900 rounded-xl border border-outline-variant/80 shadow-2xl z-50 py-1 divide-y divide-outline-variant/30 animate-in fade-in zoom-in-95 duration-100">
                  <div class="py-0.5">
                    {#each action.dropdownItems as item}
                      <button
                        type="button"
                        onclick={() => { activeDropdownIndex = null; item.onclick(); }}
                        class="w-full px-3.5 py-2 text-left text-xs font-semibold flex items-center gap-2 transition-colors cursor-pointer
                          {item.danger
                            ? 'text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950/50'
                            : 'text-on-surface hover:bg-surface-container-low dark:hover:bg-slate-800'}"
                      >
                        {#if item.icon}
                          <span class="material-symbols-outlined text-[16px] {item.danger ? 'text-red-500' : 'text-primary'}">{item.icon}</span>
                        {/if}
                        <span class="truncate">{item.label}</span>
                      </button>
                    {/each}
                  </div>
                </div>
              {/if}
            </div>
          {:else}
            <!-- Standard Button Action -->
            <button
              type="button"
              disabled={action.disabled}
              onclick={action.onclick}
              class="px-2.5 sm:px-3 py-1.5 rounded-xl font-bold text-xs flex items-center gap-1 shadow-sm transition-all active:scale-95 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed
                {action.variant === 'outline'
                  ? 'border border-outline-variant/80 bg-surface-container-low text-on-surface hover:bg-surface-container'
                  : 'bg-primary text-white hover:bg-blue-700'}"
            >
              {#if action.icon}
                <span class="material-symbols-outlined text-[16px]">{action.icon}</span>
              {/if}
              <span>{action.label}</span>
            </button>
          {/if}
        {/each}
      </div>
    {/if}

    <!-- User Auth / Sign Out -->
    {#if isAuthenticated && user}
      <button
        onclick={handleLogoutClick}
        title="Sign Out ({user.phoneNumber || user.tenantName})"
        class="p-1.5 rounded-xl bg-slate-100 hover:bg-red-50 text-slate-700 hover:text-red-600 dark:bg-slate-800 dark:hover:bg-red-950/40 dark:text-slate-300 dark:hover:text-red-400 transition-all border border-slate-200 dark:border-slate-700 active:scale-95 flex items-center cursor-pointer"
      >
        <span class="material-symbols-outlined text-[18px]">logout</span>
      </button>
    {:else if !isAuthenticated}
      <button
        onclick={handleLoginClick}
        class="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-primary hover:bg-blue-700 text-white text-xs font-bold shadow-sm transition-all active:scale-95 cursor-pointer"
      >
        <span class="material-symbols-outlined text-[16px]">login</span>
        <span>Sign In</span>
      </button>
    {/if}
  </div>
</header>

