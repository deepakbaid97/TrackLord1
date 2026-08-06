<script>
  /** @type {{ variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger', size?: 'sm' | 'md' | 'lg', icon?: string, disabled?: boolean, type?: 'button' | 'submit' | 'reset', class?: string, onclick?: (e: MouseEvent) => void, children?: any }} */
  let {
    variant = 'primary',
    size = 'md',
    icon = '',
    disabled = false,
    type = 'button',
    class: className = '',
    onclick,
    children
  } = $props();

  const variantClasses = {
    primary: 'bg-[#2563eb] text-white hover:bg-[#004ac6] active:bg-[#003ea8] shadow-sm',
    secondary: 'bg-[#006e2d] text-white hover:bg-[#005320] active:bg-[#003d17] shadow-sm',
    outline: 'bg-white border border-[#c3c6d7] text-[#0b1c30] hover:bg-[#eff4ff] active:bg-[#d3e4fe]',
    ghost: 'bg-transparent text-[#434655] hover:bg-[#eff4ff] active:bg-[#d3e4fe]',
    danger: 'bg-[#ba1a1a] text-white hover:bg-[#93000a] active:bg-[#690005] shadow-sm'
  };

  const sizeClasses = {
    sm: 'h-9 px-3 text-xs rounded-md',
    md: 'h-11 px-5 text-sm rounded-lg',
    lg: 'h-12 px-8 text-base rounded-xl'
  };
</script>

<button
  {type}
  {disabled}
  {onclick}
  class="inline-flex items-center justify-center gap-2 font-medium transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-[#2563eb]/40 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer {variantClasses[variant]} {sizeClasses[size]} {className}"
>
  {#if icon}
    <span class="material-symbols-outlined text-[18px]">{icon}</span>
  {/if}
  {#if children}
    {@render children()}
  {/if}
</button>
