<script>
  import { page } from '$app/stores';

  const primaryItems = [
    { href: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { href: '/inventory', label: 'Inventory', icon: 'inventory_2' },
    { href: '/lots', label: 'Lots', icon: 'reorder' },
    { href: '/steps', label: 'Steps', icon: 'alt_route' }
  ];

  const moreItems = [
    { href: '/materials', label: 'Materials', icon: 'texture' },
    { href: '/clients', label: 'Clients', icon: 'groups' },
    { href: '/users', label: 'Users', icon: 'manage_accounts' }
  ];

  const allNavItems = [...primaryItems, ...moreItems];

  let pathname = $derived($page.url.pathname);
  let isMoreOpen = $state(false);

  let isMoreSubItemActive = $derived(
    moreItems.some(item => pathname === item.href || pathname.startsWith(item.href + '/'))
  );

  /** @param {MouseEvent} e */
  function handleDocumentClick(e) {
    const target = /** @type {HTMLElement} */ (e.target);
    if (isMoreOpen && target && !target.closest('#nav-more-container')) {
      isMoreOpen = false;
    }
  }
</script>

<svelte:window onclick={handleDocumentClick} />

<nav class="fixed bottom-3 sm:bottom-4 left-1/2 -translate-x-1/2 z-50 p-1 sm:p-2 max-w-[calc(100vw-1rem)] sm:max-w-none bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-outline-variant/60 rounded-2xl shadow-[0_8px_32px_rgba(0,74,198,0.12),0_2px_8px_rgba(0,0,0,0.06)]">
  <!-- Desktop Nav (sm and above): Shows all 7 items inline -->
  <div class="hidden sm:flex items-center gap-1">
    {#each allNavItems as item}
      {@const isActive = pathname === item.href || pathname.startsWith(item.href + '/')}
      <a
        href={item.href}
        data-sveltekit-preload-data="hover"
        class="relative flex flex-col items-center justify-center px-4 py-2 rounded-xl transition-all duration-300 ease-out group
          {isActive
            ? 'bg-primary text-on-primary shadow-md shadow-primary/25 scale-[1.02]'
            : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary active:scale-95'}"
      >
        <span class="material-symbols-outlined text-[22px] transition-transform duration-300 {isActive ? 'scale-110' : 'group-hover:scale-105'}">{item.icon}</span>
        <span class="text-[10px] font-semibold tracking-wider font-['JetBrains_Mono'] mt-0.5 uppercase leading-none">{item.label}</span>
      </a>
    {/each}
  </div>

  <!-- Mobile Nav (below sm): Shows 4 primary items + 1 "More" button with popup menu -->
  <div class="flex sm:hidden items-center gap-0.5">
    {#each primaryItems as item}
      {@const isActive = pathname === item.href || pathname.startsWith(item.href + '/')}
      <a
        href={item.href}
        data-sveltekit-preload-data="hover"
        class="relative flex flex-col items-center justify-center flex-1 min-w-[58px] px-1.5 py-1.5 rounded-xl transition-all duration-200 ease-out
          {isActive
            ? 'bg-primary text-on-primary shadow-md shadow-primary/25'
            : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary active:scale-95'}"
      >
        <span class="material-symbols-outlined text-[20px]">{item.icon}</span>
        <span class="text-[9px] font-semibold tracking-tight font-['JetBrains_Mono'] mt-0.5 uppercase leading-none truncate max-w-full">{item.label}</span>
      </a>
    {/each}

    <!-- Mobile 'More' Button & Dropdown Container -->
    <div id="nav-more-container" class="relative flex-1 min-w-[58px]">
      {#if isMoreOpen}
        <div class="absolute bottom-full right-0 mb-3 w-48 bg-surface dark:bg-slate-900 rounded-2xl border border-outline-variant/80 shadow-2xl p-1.5 z-50 divide-y divide-outline-variant/40 animate-in fade-in slide-in-from-bottom-2 duration-150">
          <div class="py-0.5">
            {#each moreItems as item}
              {@const isSubActive = pathname === item.href || pathname.startsWith(item.href + '/')}
              <a
                href={item.href}
                onclick={() => isMoreOpen = false}
                class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-xs font-medium transition-colors cursor-pointer
                  {isSubActive
                    ? 'bg-primary/10 text-primary font-bold'
                    : 'text-on-surface hover:bg-surface-container-low'}"
              >
                <span class="material-symbols-outlined text-[20px] {isSubActive ? 'text-primary' : 'text-on-surface-variant'}">{item.icon}</span>
                <span class="font-['JetBrains_Mono'] uppercase tracking-wider">{item.label}</span>
              </a>
            {/each}
          </div>
        </div>
      {/if}

      <button
        type="button"
        onclick={() => isMoreOpen = !isMoreOpen}
        class="w-full flex flex-col items-center justify-center px-1.5 py-1.5 rounded-xl transition-all duration-200 ease-out cursor-pointer
          {isMoreSubItemActive || isMoreOpen
            ? 'bg-primary text-on-primary shadow-md shadow-primary/25'
            : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary active:scale-95'}"
      >
        <span class="material-symbols-outlined text-[20px]">more_horiz</span>
        <span class="text-[9px] font-semibold tracking-tight font-['JetBrains_Mono'] mt-0.5 uppercase leading-none truncate max-w-full">More</span>
      </button>
    </div>
  </div>
</nav>
