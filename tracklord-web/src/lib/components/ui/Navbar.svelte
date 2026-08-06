<script>
  /** @type {{ activeTab?: string, ontabchange?: (tab: string) => void }} */
  let { activeTab = 'lots', ontabchange } = $props();

  const navItems = [
    { id: 'home', label: 'Home', icon: 'home' },
    { id: 'lots', label: 'Lots', icon: 'reorder' },
    { id: 'steps', label: 'Steps', icon: 'settings_input_component' },
    { id: 'inventory', label: 'Inventory', icon: 'inventory_2' }
  ];

  function selectTab(id) {
    activeTab = id;
    if (ontabchange) ontabchange(id);
  }
</script>

<nav class="fixed bottom-0 left-0 w-full z-50 flex justify-around items-center px-4 py-2 bg-white border-t border-[#c3c6d7] md:hidden shadow-lg">
  {#each navItems as item}
    <button
      type="button"
      onclick={() => selectTab(item.id)}
      class="flex flex-col items-center justify-center p-2 rounded-xl active:scale-95 transition-all duration-200 {activeTab === item.id ? 'bg-[#2563eb] text-white shadow-sm' : 'text-[#434655] hover:bg-[#eff4ff]'}"
    >
      <span class="material-symbols-outlined text-[20px]">{item.icon}</span>
      <span class="text-[10px] font-semibold tracking-wider font-['JetBrains_Mono'] mt-0.5 uppercase">{item.label}</span>
    </button>
  {/each}
</nav>
