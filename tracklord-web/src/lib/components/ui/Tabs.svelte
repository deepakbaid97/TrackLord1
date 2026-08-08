<script>
  /** 
   * @type {{ 
   *   tabs: Array<{ id: string, label: string, icon?: string }>, 
   *   activeTab: string, 
   *   ontabchange: (id: string) => void 
   * }} 
   */
  let {
    tabs = [],
    activeTab = '',
    ontabchange
  } = $props();
</script>

<div class="flex items-center justify-between border-b border-outline-variant pb-2">
  <div class="flex gap-2">
    {#each tabs as tab}
      <button
        onclick={() => ontabchange(tab.id)}
        class="px-4 py-2 text-sm font-bold rounded-lg transition-colors flex items-center gap-2 
          {activeTab === tab.id ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:bg-surface-container-low'} cursor-pointer"
      >
        {#if tab.icon}
          <span class="material-symbols-outlined text-[18px]">{tab.icon}</span>
        {/if}
        {tab.label}
      </button>
    {/each}
  </div>
</div>
