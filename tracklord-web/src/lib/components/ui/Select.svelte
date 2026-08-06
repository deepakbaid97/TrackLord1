<script>
  /** @type {{ label?: string, value?: any, options?: Array<{ label: string, value: any }>, placeholder?: string, disabled?: boolean, icon?: string, id?: string, required?: boolean, onchange?: (value: any) => void }} */
  let {
    label = '',
    value = $bindable(''),
    options = [],
    placeholder = 'Select an option...',
    disabled = false,
    icon = '',
    id = `select-${Math.random().toString(36).substring(2, 9)}`,
    required = false,
    onchange
  } = $props();

  function handleChange(event) {
    value = event.target.value;
    if (onchange) onchange(value);
  }
</script>

<div class="flex flex-col gap-1.5 w-full">
  {#if label}
    <label for={id} class="text-xs font-semibold uppercase tracking-wider text-[#434655] font-['JetBrains_Mono']">
      {label} {#if required}<span class="text-[#ba1a1a]">*</span>{/if}
    </label>
  {/if}

  <div class="relative flex items-center w-full">
    {#if icon}
      <span class="material-symbols-outlined absolute left-3 text-[#737686] text-[20px] pointer-events-none">{icon}</span>
    {/if}

    <select
      {id}
      {value}
      {disabled}
      {required}
      onchange={handleChange}
      class="w-full bg-[#eff4ff] border border-[#c3c6d7] rounded-lg px-3 py-2.5 text-sm text-[#0b1c30] transition-all duration-200 outline-none appearance-none focus:border-[#2563eb] focus:ring-2 focus:ring-[#2563eb]/20 disabled:opacity-60 disabled:cursor-not-allowed cursor-pointer {icon ? 'pl-10' : ''} pr-10"
    >
      {#if placeholder}
        <option value="" disabled selected={!value}>{placeholder}</option>
      {/if}
      {#each options as opt}
        <option value={opt.value} selected={value === opt.value}>{opt.label}</option>
      {/each}
    </select>

    <span class="material-symbols-outlined absolute right-3 text-[#737686] text-[20px] pointer-events-none">unfold_more</span>
  </div>
</div>
