<script>
  /** 
   * @type {{ 
   *   steps: Array<{ id: string, name: string, status: 'completed' | 'in-progress' | 'pending', subtitle?: string, clientName?: string, icon: string, quantity?: number }>,
   *   onMoveStep?: (stepId: string) => void
   * }} 
   */
  let { steps = [], onMoveStep } = $props();

  let isExpanded = $state(false);

  let inProgressCount = $derived(steps.filter(s => s.status === 'in-progress').length);
  let completedCount = $derived(steps.filter(s => s.status === 'completed').length);
  let pendingCount = $derived(steps.filter(s => s.status === 'pending').length);
</script>

<div class="space-y-3">
  <!-- View mode toggle -->
  <div class="flex items-center justify-between text-xs text-on-surface-variant pb-1">
    <div class="flex items-center gap-1.5 font-mono text-[11px]">
      {#if inProgressCount > 0}
        <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-primary/10 text-primary font-bold">
          <span class="w-1.5 h-1.5 rounded-full bg-primary animate-pulse"></span>
          {inProgressCount} Active
        </span>
      {/if}
      <span class="text-emerald-700 dark:text-emerald-400 font-semibold">{completedCount} Done</span>
      <span>•</span>
      <span class="text-slate-400">{pendingCount} Next</span>
    </div>

    <button
      type="button"
      onclick={() => isExpanded = !isExpanded}
      class="text-[11px] font-semibold text-primary hover:underline flex items-center gap-0.5 cursor-pointer"
    >
      <span class="material-symbols-outlined text-[14px]">{isExpanded ? 'unfold_less' : 'unfold_more'}</span>
      {isExpanded ? 'Compact View' : 'All Details'}
    </button>
  </div>

  <!-- Timeline Nodes Container -->
  <div class="relative pl-6 before:absolute before:left-3 before:top-2 before:bottom-2 before:w-0.5 before:bg-gradient-to-b before:from-emerald-400 before:via-primary before:to-slate-200 dark:before:to-slate-700">
    <div class="space-y-2.5">
      {#each steps as step, index}
        {#if step.status === 'in-progress'}
          <!-- ACTIVE / IN-PROGRESS STEP: Full Prominence -->
          <div class="relative group">
            <!-- Node Pin -->
            <div class="absolute -left-6 top-3 flex items-center justify-center w-6 h-6 rounded-full bg-primary text-white ring-4 ring-primary/20 shadow-md z-10 animate-pulse">
              <span class="material-symbols-outlined text-[13px]">{step.icon || 'play_arrow'}</span>
            </div>

            <!-- Active Card -->
            <div class="p-3.5 sm:p-4 rounded-xl bg-gradient-to-br from-primary to-blue-700 text-white shadow-lg shadow-primary/25 border border-blue-400/40 transition-all duration-200 hover:shadow-xl">
              <div class="flex items-start justify-between gap-2">
                <div>
                  <div class="flex items-center gap-2">
                    <span class="px-2 py-0.5 rounded-full bg-white/20 text-[10px] font-mono uppercase tracking-wider font-bold">
                      Current Stage
                    </span>
                  </div>
                  <h3 class="text-base sm:text-lg font-bold tracking-tight mt-1 text-white">{step.name}</h3>
                </div>

                {#if onMoveStep}
                  <button
                    type="button"
                    onclick={() => onMoveStep(step.id)}
                    class="px-3 py-1.5 bg-white text-primary hover:bg-white/95 rounded-lg text-xs font-bold shadow-sm transition-all active:scale-95 flex items-center gap-1 cursor-pointer shrink-0"
                  >
                    <span class="material-symbols-outlined text-[15px]">send</span>
                    Move
                  </button>
                {/if}
              </div>

              <div class="flex flex-wrap items-center gap-2 mt-2 pt-2 border-t border-white/15 text-xs font-mono">
                <span class="font-bold text-white bg-white/15 px-2 py-0.5 rounded">
                  {step.subtitle || 'In Progress'}
                </span>

                {#if step.clientName}
                  <span class="flex items-center gap-1 bg-white/10 px-2 py-0.5 rounded text-[11px]">
                    <span class="material-symbols-outlined text-[12px]">badge</span>
                    Client: <strong>{step.clientName}</strong>
                  </span>
                {/if}
              </div>
            </div>
          </div>

        {:else if step.status === 'completed'}
          <!-- COMPLETED STEP: Slim, sleek 1-line item in compact mode -->
          <div class="relative group">
            <!-- Node Pin -->
            <div class="absolute -left-6 top-1.5 flex items-center justify-center w-6 h-6 rounded-full bg-emerald-600 text-white ring-2 ring-emerald-200 dark:ring-emerald-900/60 shadow-sm z-10">
              <span class="material-symbols-outlined text-[13px]">check</span>
            </div>

            {#if isExpanded}
              <!-- Expanded Detailed View -->
              <div class="p-3 rounded-lg bg-emerald-50/80 dark:bg-emerald-950/30 border border-emerald-200/70 dark:border-emerald-800/40 text-xs">
                <div class="flex items-center justify-between">
                  <h4 class="font-bold text-emerald-900 dark:text-emerald-300 text-sm">{step.name}</h4>
                  <span class="font-mono text-emerald-700 dark:text-emerald-400 font-semibold">{step.subtitle || 'Completed'}</span>
                </div>
                {#if step.clientName}
                  <div class="text-[11px] text-emerald-800 dark:text-emerald-400 mt-1 flex items-center gap-1">
                    <span class="material-symbols-outlined text-[12px]">badge</span>
                    <span>Client: <strong>{step.clientName}</strong></span>
                  </div>
                {/if}
              </div>
            {:else}
              <!-- Compact 1-Line Row -->
              <div class="flex items-center justify-between py-1 px-2.5 rounded-md hover:bg-surface-container-low transition-colors text-xs">
                <div class="flex items-center gap-2">
                  <span class="font-semibold text-emerald-900 dark:text-emerald-300">{step.name}</span>
                  {#if step.clientName}
                    <span class="text-[10px] text-on-surface-variant font-mono truncate max-w-[120px]">({step.clientName})</span>
                  {/if}
                </div>
                <span class="text-[11px] font-mono text-emerald-600 dark:text-emerald-400 font-medium">Completed</span>
              </div>
            {/if}
          </div>

        {:else}
          <!-- PENDING STEP: Compact, low-visual weight node -->
          <div class="relative group opacity-65 hover:opacity-100 transition-opacity">
            <!-- Node Pin -->
            <div class="absolute -left-6 top-1.5 flex items-center justify-center w-6 h-6 rounded-full bg-slate-100 dark:bg-slate-800 border-2 border-slate-300 dark:border-slate-600 text-slate-400 z-10">
              <span class="w-1.5 h-1.5 rounded-full bg-slate-300 dark:bg-slate-600"></span>
            </div>

            {#if isExpanded}
              <!-- Expanded View -->
              <div class="p-3 rounded-lg bg-surface border border-outline-variant/60 text-xs">
                <div class="flex items-center justify-between">
                  <h4 class="font-semibold text-on-surface">{step.name}</h4>
                  <span class="font-mono text-slate-400">{step.subtitle || 'Pending'}</span>
                </div>
                {#if step.clientName}
                  <div class="text-[11px] text-on-surface-variant mt-1 flex items-center gap-1">
                    <span class="material-symbols-outlined text-[12px]">badge</span>
                    <span>Client: <strong>{step.clientName}</strong></span>
                  </div>
                {/if}
              </div>
            {:else}
              <!-- Compact 1-Line Row -->
              <div class="flex items-center justify-between py-1 px-2.5 rounded-md hover:bg-surface-container-low transition-colors text-xs text-on-surface-variant">
                <span class="font-medium text-slate-600 dark:text-slate-400">{step.name}</span>
                <span class="text-[11px] font-mono text-slate-400">Pending</span>
              </div>
            {/if}
          </div>
        {/if}
      {/each}
    </div>
  </div>
</div>

