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

<nav class="fixed bottom-4 left-1/2 -translate-x-1/2 z-50 flex items-center gap-1 px-2 py-2 bg-white/90 backdrop-blur-xl border border-outline-variant/60 rounded-2xl shadow-[0_8px_32px_rgba(0,74,198,0.12),0_2px_8px_rgba(0,0,0,0.06)]">
  {#each navItems as item}
    {@const isActive = pathname === item.href || pathname.startsWith(item.href + '/')}
    <a
      href={item.href}
      data-sveltekit-preload-data="hover"
      class="relative flex flex-col items-center justify-center px-5 py-2 rounded-xl transition-all duration-300 ease-out group
        {isActive
          ? 'bg-primary text-on-primary shadow-md shadow-primary/25 scale-[1.02]'
          : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary active:scale-95'}"
    >
      <span class="material-symbols-outlined text-[22px] transition-transform duration-300 {isActive ? 'scale-110' : 'group-hover:scale-105'}">{item.icon}</span>
      <span class="text-[10px] font-semibold tracking-wider font-['JetBrains_Mono'] mt-0.5 uppercase leading-none">{item.label}</span>
    </a>
  {/each}
</nav>
