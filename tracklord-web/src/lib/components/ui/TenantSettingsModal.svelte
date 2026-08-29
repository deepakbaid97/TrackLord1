<script>
  import { tenantConfigStore } from '../../stores/tenantConfigStore.js';
  import { permissionStore } from '../../stores/permissionStore.js';
  import { CLIENT_MODES, CONFIG_SCHEMA } from '../../configRegistry.js';
  import Button from './Button.svelte';

  let isOpen = $derived($tenantConfigStore.isModalOpen);
  let isSaving = $derived($tenantConfigStore.isSaving);
  let saveSuccess = $derived($tenantConfigStore.saveSuccess);
  let errorMessage = $derived($tenantConfigStore.errorMessage);
  let canWriteSettings = $derived($permissionStore.canWrite('tenant_settings') || $permissionStore.isAdmin);

  let activeTab = $state('all'); // 'all' | 'clientMode' | 'pages' | 'forms' | 'fields'
  let searchQuery = $state('');

  let clientMode = $derived($tenantConfigStore.clientMode);

  // Compute filtered categories and items
  let filteredCategories = $derived(
    CONFIG_SCHEMA.map(cat => {
      if (activeTab !== 'all' && activeTab !== cat.categoryId) {
        return { ...cat, items: [] };
      }
      const query = searchQuery.trim().toLowerCase();
      if (!query) return cat;

      const filteredItems = cat.items.filter(
        item =>
          item.label.toLowerCase().includes(query) ||
          item.description.toLowerCase().includes(query) ||
          item.key.toLowerCase().includes(query)
      );
      return { ...cat, items: filteredItems };
    }).filter(cat => cat.items.length > 0)
  );

  let totalAvailableFeatures = $derived(
    CONFIG_SCHEMA.reduce((acc, cat) => acc + cat.items.length, 0)
  );

  let enabledCount = $derived(
    CONFIG_SCHEMA.reduce(
      (acc, cat) =>
        acc + cat.items.filter(item => {
          // Re-evaluate on featureOverrides change
          const overrides = $tenantConfigStore.featureOverrides;
          if (overrides && typeof overrides[item.key] === 'boolean') {
            return overrides[item.key];
          }
          return item.default;
        }).length,
      0
    )
  );

  function handleModeSelect(modeId) {
    if (!canWriteSettings) return;
    tenantConfigStore.setClientMode(modeId);
  }

  function handleToggle(key, currentValue) {
    if (!canWriteSettings) return;
    tenantConfigStore.setFeature(key, !currentValue);
  }

  async function handleSave() {
    if (!canWriteSettings) return;
    try {
      await tenantConfigStore.saveSettings();
    } catch {
      // handled in store
    }
  }

  function handleClose() {
    tenantConfigStore.closeModal();
  }
</script>

{#if isOpen}
  <!-- Backdrop -->
  <div
    class="fixed inset-0 z-50 bg-slate-950/70 backdrop-blur-md flex items-center justify-center p-3 sm:p-6 overflow-y-auto animate-in fade-in duration-200"
    role="dialog"
    aria-modal="true"
  >
    <div
      class="relative w-full max-w-4xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl shadow-2xl overflow-hidden flex flex-col h-[90vh] max-h-[850px] animate-in zoom-in-95 duration-200"
    >
      <!-- Modal Header -->
      <div class="px-6 py-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-900/80 backdrop-blur flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-primary/10 dark:bg-primary/20 text-primary flex items-center justify-center shrink-0">
            <span class="material-symbols-outlined text-[24px]">tune</span>
          </div>
          <div>
            <h2 class="text-base sm:text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2 font-['Inter']">
              Tenant Settings & Feature Visibility
            </h2>
            <p class="text-xs text-slate-500 dark:text-slate-400">
              Customize workflows, operating modes, and visible pages/forms for your organization.
            </p>
          </div>
        </div>

        <button
          onclick={handleClose}
          class="p-2 rounded-xl text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-200/60 dark:hover:bg-slate-800 transition-colors shrink-0"
          title="Close Settings"
        >
          <span class="material-symbols-outlined text-xl">close</span>
        </button>
      </div>

      <!-- Quick Action / Status Bar & Search -->
      <div class="px-6 py-3 bg-slate-100/60 dark:bg-slate-950/40 border-b border-slate-200 dark:border-slate-800 flex flex-wrap items-center justify-between gap-3 shrink-0">
        <!-- Search Input -->
        <div class="relative flex-1 min-w-[220px]">
          <span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-[18px]">search</span>
          <input
            type="text"
            bind:value={searchQuery}
            placeholder="Search settings (e.g. client, import, cost, timeline)..."
            class="w-full pl-9 pr-4 py-2 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-xs text-slate-900 dark:text-slate-100 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/40 transition-all"
          />
          {#if searchQuery}
            <button
              onclick={() => searchQuery = ''}
              class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs"
            >
              <span class="material-symbols-outlined text-sm">cancel</span>
            </button>
          {/if}
        </div>

        <!-- Active Feature Counter Badge & Quick Toggles -->
        <div class="flex items-center gap-2 text-xs shrink-0">
          <span class="px-2.5 py-1 rounded-lg bg-primary/10 text-primary font-semibold font-['JetBrains_Mono']">
            {enabledCount} / {totalAvailableFeatures} Active
          </span>
          <button
            onclick={() => tenantConfigStore.enableAll()}
            class="px-2.5 py-1 rounded-lg border border-slate-300 dark:border-slate-700 hover:bg-slate-200/50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 font-medium transition-colors"
          >
            Enable All
          </button>
          <button
            onclick={() => tenantConfigStore.resetToDefaults()}
            class="px-2.5 py-1 rounded-lg border border-slate-300 dark:border-slate-700 hover:bg-slate-200/50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 font-medium transition-colors"
          >
            Reset Defaults
          </button>
        </div>
      </div>

      <!-- Navigation Category Tabs -->
      <div class="px-6 py-2 border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 flex gap-2 overflow-x-auto shrink-0 scrollbar-none items-center">
        <button
          onclick={() => activeTab = 'all'}
          class="px-3.5 py-1.5 text-xs font-bold rounded-lg transition-all whitespace-nowrap flex items-center gap-1.5 cursor-pointer shrink-0
            {activeTab === 'all'
              ? 'bg-primary text-white shadow-xs'
              : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'}"
        >
          <span>All Settings</span>
        </button>
        <button
          onclick={() => activeTab = 'clientMode'}
          class="px-3.5 py-1.5 text-xs font-bold rounded-lg transition-all whitespace-nowrap flex items-center gap-1.5 cursor-pointer shrink-0
            {activeTab === 'clientMode'
              ? 'bg-primary text-white shadow-xs'
              : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'}"
        >
          <span class="material-symbols-outlined text-[18px]">hub</span>
          <span>Client Operating Mode</span>
        </button>
        {#each CONFIG_SCHEMA as cat}
          <button
            onclick={() => activeTab = cat.categoryId}
            class="px-3.5 py-1.5 text-xs font-bold rounded-lg transition-all whitespace-nowrap flex items-center gap-1.5 cursor-pointer shrink-0
              {activeTab === cat.categoryId
                ? 'bg-primary text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'}"
          >
            <span class="material-symbols-outlined text-[18px]">{cat.icon}</span>
            <span>{cat.categoryName}</span>
          </button>
        {/each}
      </div>

      <!-- Scrollable Settings Body -->
      <div class="p-6 overflow-y-auto space-y-6 flex-1 min-h-0 bg-slate-50/50 dark:bg-slate-950/20">

        <!-- 1. Client Operating Mode Section (Shown if 'all' or 'clientMode') -->
        {#if activeTab === 'all' || activeTab === 'clientMode'}
          <div class="bg-white dark:bg-slate-900 rounded-2xl p-5 border border-slate-200 dark:border-slate-800 shadow-sm">
            <div class="flex items-center justify-between mb-3">
              <div class="flex items-center gap-2">
                <span class="material-symbols-outlined text-primary text-[22px]">hub</span>
                <h3 class="text-sm font-bold text-slate-900 dark:text-white font-['Inter']">
                  Client & Subcontractor Tracking Strategy
                </h3>
              </div>
              <span class="text-[11px] font-semibold text-slate-500 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-full font-['JetBrains_Mono']">
                Active: {CLIENT_MODES.find(m => m.id === clientMode)?.name || 'Default'}
              </span>
            </div>
            <p class="text-xs text-slate-500 dark:text-slate-400 mb-4">
              Select how client and customer relationships are structured in this tenant. This automatically adapts dashboard WIP aggregations and creation/movement forms.
            </p>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
              {#each CLIENT_MODES as mode}
                {@const isSelected = clientMode === mode.id}
                <button
                  type="button"
                  onclick={() => handleModeSelect(mode.id)}
                  class="relative p-4 rounded-xl border text-left transition-all duration-200 flex flex-col justify-between
                    {isSelected
                      ? 'border-primary bg-primary/5 dark:bg-primary/10 shadow-sm ring-2 ring-primary/20'
                      : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 hover:border-slate-300 dark:hover:border-slate-700'}"
                >
                  <div>
                    <div class="flex items-center justify-between mb-2">
                      <div class="w-8 h-8 rounded-lg flex items-center justify-center {isSelected ? 'bg-primary text-white' : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'}">
                        <span class="material-symbols-outlined text-[18px]">{mode.icon}</span>
                      </div>
                      <span class="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-full {isSelected ? 'bg-primary text-white' : 'bg-slate-100 dark:bg-slate-800 text-slate-500'}">
                        {mode.tag}
                      </span>
                    </div>
                    <h4 class="text-xs font-bold text-slate-900 dark:text-white font-['Inter'] mb-1">
                      {mode.name}
                    </h4>
                    <p class="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed">
                      {mode.description}
                    </p>
                  </div>

                  <div class="mt-3 pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center gap-1.5 text-[11px] font-semibold {isSelected ? 'text-primary' : 'text-slate-400'}">
                    <span class="material-symbols-outlined text-[16px]">{isSelected ? 'radio_button_checked' : 'radio_button_unchecked'}</span>
                    <span>{isSelected ? 'Selected' : 'Click to select'}</span>
                  </div>
                </button>
              {/each}
            </div>
          </div>
        {/if}

        <!-- 2. Configurable Schema Categories (Pages, Forms, Fields) -->
        {#each filteredCategories as category}
          <div class="bg-white dark:bg-slate-900 rounded-2xl p-5 border border-slate-200 dark:border-slate-800 shadow-sm">
            <div class="flex items-center gap-2 mb-1">
              <span class="material-symbols-outlined text-primary text-[20px]">{category.icon}</span>
              <h3 class="text-sm font-bold text-slate-900 dark:text-white font-['Inter']">
                {category.categoryName}
              </h3>
            </div>
            <p class="text-xs text-slate-500 dark:text-slate-400 mb-4">
              {category.description}
            </p>

            <div class="divide-y divide-slate-100 dark:divide-slate-800/80">
              {#each category.items as item}
                {@const isEnabled = typeof $tenantConfigStore.featureOverrides?.[item.key] === 'boolean'
                  ? $tenantConfigStore.featureOverrides[item.key]
                  : item.default}
                <div class="py-3 flex items-center justify-between gap-4 group">
                  <div class="flex-1 pr-2">
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-semibold text-slate-900 dark:text-slate-100 font-['Inter']">
                        {item.label}
                      </span>
                      <span class="text-[10px] text-slate-400 font-['JetBrains_Mono'] opacity-75">
                        {item.key}
                      </span>
                    </div>
                    <p class="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
                      {item.description}
                    </p>
                  </div>

                  <!-- Modern Switch Toggle Button -->
                  <button
                    type="button"
                    role="switch"
                    aria-checked={isEnabled}
                    aria-label={`Toggle ${item.label}`}
                    onclick={() => handleToggle(item.key, isEnabled)}
                    class="relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary/40
                      {isEnabled ? 'bg-primary' : 'bg-slate-300 dark:bg-slate-700'}"
                  >
                    <span
                      class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-md ring-0 transition duration-200 ease-in-out
                        {isEnabled ? 'translate-x-5' : 'translate-x-0'}"
                    ></span>
                  </button>
                </div>
              {/each}
            </div>
          </div>
        {/each}

        {#if filteredCategories.length === 0 && activeTab !== 'clientMode'}
          <div class="py-12 text-center text-slate-400">
            <span class="material-symbols-outlined text-4xl mb-2 text-slate-300">search_off</span>
            <p class="text-sm font-medium">No settings match "{searchQuery}"</p>
            <button
              onclick={() => searchQuery = ''}
              class="mt-2 text-xs text-primary font-semibold hover:underline"
            >
              Clear search filter
            </button>
          </div>
        {/if}

      </div>

      <!-- Modal Footer -->
      <div class="px-6 py-4 border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 flex items-center justify-between shrink-0">
        <div>
          {#if !canWriteSettings}
            <div class="flex items-center gap-1.5 text-xs text-amber-600 dark:text-amber-400 font-semibold animate-in fade-in">
              <span class="material-symbols-outlined text-[18px]">lock</span>
              <span>View-Only Mode (Write access restricted for this role)</span>
            </div>
          {:else if saveSuccess}
            <div class="flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400 font-semibold animate-in fade-in">
              <span class="material-symbols-outlined text-[18px]">check_circle</span>
              <span>Settings saved successfully!</span>
            </div>
          {:else if errorMessage}
            <div class="flex items-center gap-1.5 text-xs text-red-600 dark:text-red-400 font-semibold animate-in fade-in">
              <span class="material-symbols-outlined text-[18px]">error</span>
              <span>{errorMessage}</span>
            </div>
          {:else}
            <span class="text-xs text-slate-400">
              Changes apply instantly to all tenant operators upon save.
            </span>
          {/if}
        </div>

        <div class="flex items-center gap-3">
          <button
            onclick={handleClose}
            class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            {canWriteSettings ? 'Cancel' : 'Close'}
          </button>
          {#if canWriteSettings}
            <Button
              onclick={handleSave}
              disabled={isSaving}
              icon={isSaving ? 'sync' : 'save'}
              class="shadow-md shadow-primary/20"
            >
              {isSaving ? 'Saving...' : 'Save Configuration'}
            </Button>
          {/if}
        </div>
      </div>

    </div>
  </div>
{/if}
