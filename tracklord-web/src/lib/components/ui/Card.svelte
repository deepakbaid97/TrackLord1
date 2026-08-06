<script>
  /** @type {{ title?: string, subtitle?: string, class?: string, children?: any, headerAction?: any }} */
  let {
    title = '',
    subtitle = '',
    class: className = '',
    children,
    headerAction
  } = $props();
</script>

<section class="bg-white p-5 rounded-xl border border-[#c3c6d7] shadow-sm {className}">
  {#if title}
    <div class="flex justify-between items-center border-b border-[#c3c6d7] pb-3 mb-4">
      <div>
        <h2 class="text-lg font-semibold text-[#0b1c30] tracking-tight">{title}</h2>
        {#if subtitle}
          <p class="text-xs text-[#434655] font-mono mt-0.5">{subtitle}</p>
        {/if}
      </div>
      {#if headerAction}
        {@render headerAction()}
      {/if}
    </div>
  {/if}

  {#if children}
    {@render children()}
  {/if}
</section>
