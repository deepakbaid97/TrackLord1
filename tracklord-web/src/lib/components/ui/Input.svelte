<script>
  /** @type {{ label?: string, type?: string, value?: any, placeholder?: string, error?: string, disabled?: boolean, icon?: string, id?: string, required?: boolean, step?: string | number, oninput?: (e: Event) => void, onchange?: (e: Event) => void }} */
  let {
    label = '',
    type = 'text',
    value = $bindable(''),
    placeholder = '',
    error = '',
    disabled = false,
    icon = '',
    id = `input-${Math.random().toString(36).substring(2, 9)}`,
    required = false,
    step,
    oninput,
    onchange
  } = $props();
</script>

<div class="flex flex-col gap-1.5 w-full">
  {#if label}
    <label for={id} class="text-xs font-semibold uppercase tracking-wider text-[#434655] font-['JetBrains_Mono'] flex items-center justify-between">
      <span>{label} {#if required}<span class="text-[#ba1a1a]">*</span>{/if}</span>
    </label>
  {/if}

  <div class="relative flex items-center w-full">
    {#if icon}
      <span class="material-symbols-outlined absolute left-3 text-[#737686] text-[20px] pointer-events-none">{icon}</span>
    {/if}

    <input
      {id}
      {type}
      bind:value
      {placeholder}
      {disabled}
      {required}
      {step}
      {oninput}
      {onchange}
      class="w-full bg-[#eff4ff] border border-[#c3c6d7] rounded-lg px-3 py-2.5 text-sm text-[#0b1c30] placeholder-[#737686] transition-all duration-200 outline-none focus:border-[#2563eb] focus:ring-2 focus:ring-[#2563eb]/20 disabled:opacity-60 disabled:cursor-not-allowed {icon ? 'pl-10' : ''} {error ? 'border-[#ba1a1a] focus:ring-[#ba1a1a]/20' : ''}"
    />
  </div>

  {#if error}
    <span class="text-xs text-[#ba1a1a] font-medium flex items-center gap-1">
      <span class="material-symbols-outlined text-[14px]">error</span>
      {error}
    </span>
  {/if}
</div>
