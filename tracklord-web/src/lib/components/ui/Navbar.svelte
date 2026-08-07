<script>
  import { page } from '$app/stores';

  const navItems = [
    { href: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { href: '/inventory', label: 'Inventory', icon: 'inventory_2' },
    { href: '/lots', label: 'Lots', icon: 'reorder' },
    { href: '/clients', label: 'Clients', icon: 'groups' },
    { href: '/steps', label: 'Steps', icon: 'alt_route' }
  ];

  let pathname = $derived($page.url.pathname);
</script>

<nav class="fixed bottom-3 sm:bottom-4 left-1/2 -translate-x-1/2 z-50 flex items-center gap-0.5 sm:gap-1 p-1 sm:p-2 max-w-[calc(100vw-1rem)] sm:max-w-none bg-white/90 dark:bg-slate-900/90 backdrop-blur-xl border border-outline-variant/60 rounded-2xl shadow-[0_8px_32px_rgba(0,74,198,0.12),0_2px_8px_rgba(0,0,0,0.06)]">
  {#each navItems as item}
    {@const isActive = pathname === item.href || pathname.startsWith(item.href + '/')}
    <a
      href={item.href}
      data-sveltekit-preload-data="hover"
      class="relative flex flex-1 sm:flex-initial flex-col items-center justify-center min-w-0 px-2 sm:px-4.5 py-1.5 sm:py-2 rounded-xl transition-all duration-300 ease-out group
        {isActive
          ? 'bg-primary text-on-primary shadow-md shadow-primary/25 scale-[1.02]'
          : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary active:scale-95'}"
    >
      <span class="material-symbols-outlined text-[19px] sm:text-[22px] transition-transform duration-300 {isActive ? 'scale-110' : 'group-hover:scale-105'}">{item.icon}</span>
      <span class="text-[9px] sm:text-[10px] font-semibold tracking-tight sm:tracking-wider font-['JetBrains_Mono'] mt-0.5 uppercase leading-none truncate max-w-full">{item.label}</span>
    </a>
  {/each}
</nav>
