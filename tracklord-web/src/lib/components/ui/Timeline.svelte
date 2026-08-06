<script>
  /** 
   * @type {{ steps: Array<{ id: string, name: string, status: 'completed' | 'in-progress' | 'pending', subtitle?: string, icon: string }> }} 
   */
  let { steps = [] } = $props();
</script>

<div class="space-y-6 relative before:absolute before:inset-0 before:ml-[15px] before:-translate-x-px md:before:mx-auto md:before:translate-x-0 before:h-full before:w-0.5 before:bg-[#c3c6d7]">
  {#each steps as step, index}
    <div class="relative flex items-center justify-between md:justify-normal md:odd:flex-row-reverse group {step.status !== 'pending' ? 'is-active' : ''}">
      <!-- Step Icon Badge -->
      {#if step.status === 'completed'}
        <div class="flex items-center justify-center w-8 h-8 rounded-full border-2 border-[#7cf994] bg-[#006e2d] text-white shadow shrink-0 md:order-1 md:group-odd:-translate-x-1/2 md:group-even:translate-x-1/2 z-10">
          <span class="material-symbols-outlined text-[16px]">{step.icon}</span>
        </div>
      {:else if step.status === 'in-progress'}
        <div class="flex items-center justify-center w-8 h-8 rounded-full border-2 border-[#004ac6] bg-[#2563eb] text-white shadow shrink-0 md:order-1 md:group-odd:-translate-x-1/2 md:group-even:translate-x-1/2 z-10 animate-pulse">
          <span class="material-symbols-outlined text-[16px]">{step.icon}</span>
        </div>
      {:else}
        <div class="flex items-center justify-center w-8 h-8 rounded-full border-2 border-[#c3c6d7] bg-[#f8f9ff] text-[#434655] shadow shrink-0 md:order-1 md:group-odd:-translate-x-1/2 md:group-even:translate-x-1/2 z-10">
          <span class="material-symbols-outlined text-[16px]">{step.icon}</span>
        </div>
      {/if}

      <!-- Step Content Card -->
      {#if step.status === 'completed'}
        <div class="w-[calc(100%-4rem)] md:w-[calc(50%-2.5rem)] p-4 rounded-lg bg-[#eff4ff] border border-[#c3c6d7]/50 shadow-sm ml-4 md:ml-0">
          <h3 class="text-sm font-semibold text-[#006e2d]">{step.name}</h3>
          <p class="text-xs font-mono text-[#434655] mt-0.5">{step.subtitle || 'Completed'}</p>
        </div>
      {:else if step.status === 'in-progress'}
        <div class="w-[calc(100%-4rem)] md:w-[calc(50%-2.5rem)] p-4 rounded-lg bg-[#2563eb] text-[#eeefff] shadow-md ml-4 md:ml-0 border border-[#b4c5ff]">
          <h3 class="text-sm font-bold text-white">{step.name}</h3>
          <p class="text-xs font-mono opacity-90 mt-0.5">{step.subtitle || 'In Progress'}</p>
        </div>
      {:else}
        <div class="w-[calc(100%-4rem)] md:w-[calc(50%-2.5rem)] p-4 rounded-lg bg-white border border-[#c3c6d7] shadow-sm ml-4 md:ml-0 opacity-60">
          <h3 class="text-sm font-semibold text-[#434655]">{step.name}</h3>
          <p class="text-xs font-mono text-[#737686] mt-0.5">{step.subtitle || 'Pending'}</p>
        </div>
      {/if}
    </div>
  {/each}
</div>
