<script>
  /** @type {{ variant?: 'primary' | 'secondary' | 'variant' | 'outline' | 'error', icon?: string, children?: any }} */
  let {
    variant = 'primary',
    icon = '',
    children
  } = $props();

  const variantClasses = {
    primary: 'bg-[#2563eb] text-white',
    secondary: 'bg-[#7cf994] text-[#007230]',
    variant: 'bg-[#d3e4fe] text-[#434655]',
    outline: 'bg-white text-[#434655] border border-[#c3c6d7]',
    error: 'bg-[#ffdad6] text-[#93000a]'
  };
</script>

<span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold font-['Inter'] shadow-sm {variantClasses[variant]}">
  {#if icon}
    <span class="material-symbols-outlined text-[15px]">{icon}</span>
  {/if}
  {#if children}
    {@render children()}
  {/if}
</span>
