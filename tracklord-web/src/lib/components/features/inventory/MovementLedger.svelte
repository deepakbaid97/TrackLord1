<script>
  import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
  import EmptyState from '$lib/components/ui/EmptyState.svelte';
  import PermissionGate from '$lib/components/ui/PermissionGate.svelte';

  let {
    movements = [],
    onReverseClick
  } = $props();

  let expandedMovementIds = $state(new Set());

  /** @param {string} id */
  function toggleExpand(id) {
    if (!id) return;
    const next = new Set(expandedMovementIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    expandedMovementIds = next;
  }
</script>

<section class="bg-surface rounded-2xl border border-outline-variant/70 shadow-sm overflow-hidden p-3.5 sm:p-5 space-y-3.5">
  <!-- Card Header -->
  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-outline-variant/60 pb-3">
    <div class="flex items-center gap-2.5">
      <div class="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary shadow-2xs">
        <span class="material-symbols-outlined text-[18px]">history_edu</span>
      </div>
      <div>
        <h2 class="font-bold text-sm sm:text-base text-on-surface flex items-center gap-2">
          Movements & Audit Ledger
        </h2>
        <p class="text-xs text-on-surface-variant">Immutable chronological WIP tracking and step transfer records</p>
      </div>
    </div>
    <div class="flex items-center gap-2">
      <span class="px-2.5 py-1 rounded-lg text-xs font-mono font-semibold bg-surface-container-low text-on-surface-variant border border-outline-variant/60">
        {movements.length} {movements.length === 1 ? 'record' : 'records'}
      </span>
    </div>
  </div>

  {#if movements.length > 0}
    <!-- Mobile View: Ledger Card Stack -->
    <div class="block md:hidden space-y-2.5">
      {#each movements as m}
        {@const mKey = m.id || `${m.date}-${m.from}-${m.to}`}
        {@const isExpanded = expandedMovementIds.has(mKey)}
        <div
          class="rounded-xl border transition-all overflow-hidden {m.isReversed ? 'bg-red-50/40 dark:bg-red-950/20 border-red-200 dark:border-red-900/50' : isExpanded ? 'bg-surface border-primary shadow-xs ring-1 ring-primary/20' : 'bg-surface-container-low/50 hover:bg-surface-container-low border-outline-variant/70'}"
        >
          <!-- Card Header & Trigger Button -->
          <button
            type="button"
            onclick={() => toggleExpand(mKey)}
            aria-expanded={isExpanded}
            class="w-full p-3 flex flex-col gap-2 text-left cursor-pointer focus:outline-none"
          >
            <!-- Line 1: Route & Quantity -->
            <div class="flex items-center justify-between gap-2">
              <div class="flex items-center gap-1.5 font-mono text-xs font-bold min-w-0">
                <span class="text-on-surface-variant truncate">{m.from}</span>
                <span class="material-symbols-outlined text-[13px] text-primary shrink-0">arrow_forward</span>
                <span class="text-primary truncate font-bold bg-primary/10 px-1.5 py-0.5 rounded">{m.to}</span>
              </div>
              <div class="flex items-center gap-2 shrink-0">
                <span class="font-mono font-bold text-xs bg-surface px-2 py-0.5 rounded border border-outline-variant/50 text-on-surface">
                  {m.qty}
                </span>
                <span class="material-symbols-outlined text-on-surface-variant text-[18px] transition-transform duration-200 {isExpanded ? 'rotate-180 text-primary' : ''}">
                  expand_more
                </span>
              </div>
            </div>

            <!-- Line 2: Date, Ref Badge, Status -->
            <div class="flex items-center justify-between text-[11px] text-on-surface-variant font-mono pt-0.5">
              <div class="flex items-center gap-2 truncate">
                <span>{m.date}</span>
                {#if m.referenceNumber && m.referenceNumber !== '-'}
                  <span class="px-1.5 py-0.2 rounded bg-surface-container font-semibold text-[10px] text-on-surface truncate border border-outline-variant/40">
                    #{m.referenceNumber}
                  </span>
                {/if}
              </div>
              <div>
                {#if m.isReversed}
                  <span class="px-1.5 py-0.5 rounded bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-300 font-bold text-[9px] uppercase">
                    Reversed
                  </span>
                {:else}
                  <span class="px-1.5 py-0.5 rounded bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 font-bold text-[9px] uppercase">
                    Active
                  </span>
                {/if}
              </div>
            </div>
          </button>

          <!-- Card Actions Bar (Reverse button) -->
          {#if !m.isReversed && onReverseClick}
            <div class="px-3 pb-2.5 pt-0 flex justify-end">
              <PermissionGate feature="movement_reversals" access="write">
                <button
                  type="button"
                  onclick={() => onReverseClick(m)}
                  class="px-2.5 py-0.5 text-xs font-bold text-error hover:bg-error-container/30 rounded-lg border border-error/30 transition-colors flex items-center gap-1 cursor-pointer"
                >
                  <span class="material-symbols-outlined text-[13px]">undo</span>
                  Reverse
                </button>
              </PermissionGate>
            </div>
          {/if}

          <!-- Expanded Inspection Section (Compact) -->
          {#if isExpanded}
            <div class="p-3 bg-surface-container-low/60 border-t border-dashed border-outline-variant/60 space-y-2.5">
              <div class="grid grid-cols-2 gap-2 text-[11px]">
                <div class="bg-surface p-2 rounded-lg border border-outline-variant/50 flex items-center gap-2">
                  <span class="material-symbols-outlined text-[14px] text-primary shrink-0">tag</span>
                  <div class="min-w-0">
                    <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">Ref #</span>
                    <strong class="font-mono text-on-surface font-bold text-xs truncate block">{m.referenceNumber && m.referenceNumber !== '-' ? m.referenceNumber : 'None'}</strong>
                  </div>
                </div>
                <div class="bg-surface p-2 rounded-lg border border-outline-variant/50 flex items-center gap-2">
                  <span class="material-symbols-outlined text-[14px] text-primary shrink-0">alt_route</span>
                  <div class="min-w-0">
                    <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">Type</span>
                    <span class="font-mono text-on-surface font-semibold text-xs truncate block">{m.type || 'StepTransfer'}</span>
                  </div>
                </div>
                {#if $tenantConfigStore.clientMode !== 'none'}
                  <div class="bg-surface p-2 rounded-lg border border-outline-variant/50 col-span-2 flex items-center gap-2">
                    <span class="material-symbols-outlined text-[14px] text-primary shrink-0">business</span>
                    <div class="min-w-0">
                      <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">Assigned Partner</span>
                      <span class="font-semibold text-on-surface text-xs truncate block">{m.clientName && m.clientName !== '-' ? m.clientName : 'Internal (In-House)'}</span>
                    </div>
                  </div>
                {/if}
                <div class="bg-surface p-2 rounded-lg border border-outline-variant/50 col-span-2 flex items-center gap-2">
                  <span class="material-symbols-outlined text-[14px] text-primary shrink-0">person</span>
                  <div class="min-w-0">
                    <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">Recorded By</span>
                    <span class="text-on-surface text-xs">{m.user || 'System'}</span>
                  </div>
                </div>
              </div>

              <!-- Remarks -->
              <div class="bg-surface p-2 rounded-lg border border-outline-variant/50 flex items-start gap-2">
                <span class="material-symbols-outlined text-[14px] text-primary shrink-0 mt-0.5">sticky_note_2</span>
                <div class="min-w-0 flex-1">
                  <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block mb-0.5">Remarks & Notes</span>
                  {#if m.remarks && m.remarks !== '-'}
                    <p class="text-xs text-on-surface italic font-medium">"{m.remarks}"</p>
                  {:else}
                    <p class="text-[11px] text-on-surface-variant/70 italic font-mono">No remarks recorded.</p>
                  {/if}
                </div>
              </div>
            </div>
          {/if}
        </div>
      {/each}
    </div>

    <!-- Desktop View: Ledger Table -->
    <div class="hidden md:block w-full overflow-x-auto rounded-xl border border-outline-variant/60 shadow-2xs">
      <table class="w-full text-left border-collapse text-xs">
        <thead>
          <tr class="bg-surface-container-low/80 border-b border-outline-variant/60 font-mono text-on-surface-variant uppercase tracking-wider text-[11px]">
            <th class="w-10 px-3 py-2.5 text-center"></th>
            <th class="px-3 py-2.5">Date & Time</th>
            <th class="px-3 py-2.5">From Step</th>
            <th class="px-3 py-2.5">To Step</th>
            <th class="px-3 py-2.5 text-right">Quantity</th>
            {#if $tenantConfigStore.clientMode !== 'none'}
              <th class="px-3 py-2.5">Assigned Partner</th>
            {/if}
            <th class="px-3 py-2.5">Reference #</th>
            <th class="px-3 py-2.5">Logged By</th>
            <th class="px-3 py-2.5 text-center">Status</th>
            <th class="px-3 py-2.5 text-right">Action</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-outline-variant/40">
          {#each movements as m}
            {@const mKey = m.id || `${m.date}-${m.from}-${m.to}`}
            {@const isExpanded = expandedMovementIds.has(mKey)}
            <tr
              class="hover:bg-surface-container-low/50 transition-colors select-none {m.isReversed ? 'bg-red-50/30 dark:bg-red-950/10 opacity-80' : isExpanded ? 'bg-primary/5 border-l-4 border-l-primary' : ''}"
            >
              <!-- Expand Caret Button -->
              <td class="px-3 py-2.5 text-center text-on-surface-variant">
                <button
                  type="button"
                  onclick={() => toggleExpand(mKey)}
                  aria-label="Toggle movement details"
                  aria-expanded={isExpanded}
                  class="p-1 rounded-lg hover:bg-surface-container transition-colors cursor-pointer flex items-center justify-center mx-auto"
                >
                  <span class="material-symbols-outlined text-[18px] transition-transform duration-200 {isExpanded ? 'rotate-180 text-primary' : 'text-on-surface-variant'}">
                    expand_more
                  </span>
                </button>
              </td>

              <!-- Date Column -->
              <td class="px-3 py-2.5 font-mono text-on-surface-variant whitespace-nowrap cursor-pointer" onclick={() => toggleExpand(mKey)}>
                <span class="font-medium text-on-surface">{m.date}</span>
              </td>

              <!-- From Step -->
              <td class="px-3 py-2.5 font-medium text-on-surface cursor-pointer" onclick={() => toggleExpand(mKey)}>
                <span class="px-2 py-0.5 rounded-md bg-surface-container text-on-surface-variant text-[11px] font-mono font-medium">
                  {m.from}
                </span>
              </td>

              <!-- To Step -->
              <td class="px-3 py-2.5 font-bold text-primary cursor-pointer" onclick={() => toggleExpand(mKey)}>
                <span class="px-2 py-0.5 rounded-md bg-primary/10 text-primary text-[11px] font-mono font-bold border border-primary/20">
                  {m.to}
                </span>
              </td>

              <!-- Quantity Column -->
              <td class="px-3 py-2.5 text-right font-mono font-bold text-on-surface cursor-pointer" onclick={() => toggleExpand(mKey)}>
                <span class="px-2 py-0.5 rounded bg-surface-container font-mono font-bold text-primary border border-outline-variant/40">
                  {m.qty}
                </span>
              </td>

              <!-- Assigned Partner Column -->
              {#if $tenantConfigStore.clientMode !== 'none'}
                <td class="px-3 py-2.5 cursor-pointer" onclick={() => toggleExpand(mKey)}>
                  {#if m.clientName && m.clientName !== '-'}
                    <span class="inline-flex items-center gap-1 font-medium text-on-surface bg-surface-container px-2 py-0.5 rounded text-xs">
                      <span class="material-symbols-outlined text-[13px] text-primary">handshake</span>
                      {m.clientName}
                    </span>
                  {:else}
                    <span class="text-outline font-mono">—</span>
                  {/if}
                </td>
              {/if}

              <!-- Reference # Column -->
              <td class="px-3 py-2.5 font-mono cursor-pointer" onclick={() => toggleExpand(mKey)}>
                {#if m.referenceNumber && m.referenceNumber !== '-'}
                  <span class="px-2 py-0.5 rounded bg-surface-container-high/70 text-on-surface font-semibold text-[11px] border border-outline-variant/40">
                    #{m.referenceNumber}
                  </span>
                {:else}
                  <span class="text-outline font-mono">—</span>
                {/if}
              </td>

              <!-- Logged By Column -->
              <td class="px-3 py-2.5 text-on-surface-variant cursor-pointer" onclick={() => toggleExpand(mKey)}>
                <div class="flex items-center gap-1 text-xs">
                  <span class="material-symbols-outlined text-[14px] text-outline">person</span>
                  <span>{m.user}</span>
                </div>
              </td>

              <!-- Status Column -->
              <td class="px-3 py-2.5 text-center cursor-pointer" onclick={() => toggleExpand(mKey)}>
                {#if m.isReversed}
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-900/60">
                    Reversed
                  </span>
                {:else}
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-900/60">
                    Active
                  </span>
                {/if}
              </td>

              <!-- Action Column -->
              <td class="px-3 py-2.5 text-right">
                {#if !m.isReversed && onReverseClick}
                  <PermissionGate feature="movement_reversals" access="write">
                    <button
                      type="button"
                      onclick={() => onReverseClick(m)}
                      class="px-2 py-1 text-[11px] font-bold text-error hover:bg-error-container/30 border border-error/30 rounded-lg transition-all cursor-pointer inline-flex items-center gap-1"
                    >
                      <span class="material-symbols-outlined text-[13px]">undo</span>
                      Reverse
                    </button>
                  </PermissionGate>
                {/if}
              </td>
            </tr>

            <!-- Expanded Inspection Dossier Row (Compact) -->
            {#if isExpanded}
              <tr class="bg-surface-container-lowest dark:bg-surface-container-low/20">
                <td colspan={$tenantConfigStore.clientMode !== 'none' ? 10 : 9} class="p-0">
                  <div class="p-3.5 sm:p-4 bg-gradient-to-b from-surface-container-low/60 to-surface/90 border-t border-b border-primary/20 space-y-3">
                    
                    <!-- Top Overview Flow Bar (Compact) -->
                    <div class="flex flex-wrap items-center justify-between gap-2.5 pb-2.5 border-b border-outline-variant/40">
                      <div class="flex items-center gap-2.5">
                        <div class="flex items-center gap-1.5 bg-surface py-1 px-2.5 rounded-lg border border-outline-variant/60 font-mono text-xs shadow-2xs">
                          <span class="text-on-surface-variant font-medium">{m.from}</span>
                          <span class="material-symbols-outlined text-primary text-[15px]">arrow_forward</span>
                          <span class="text-primary font-bold">{m.to}</span>
                          <span class="ml-1.5 pl-1.5 border-l border-outline-variant/60 font-bold text-on-surface">{m.qty}</span>
                        </div>
                        <span class="px-2 py-0.5 rounded-md text-[10px] font-bold uppercase tracking-wider bg-surface-container-high text-on-surface font-mono border border-outline-variant/50">
                          {m.type || 'StepTransfer'}
                        </span>
                      </div>

                      {#if m.isReversed}
                        <div class="flex items-center gap-1 px-2 py-0.5 rounded-md bg-red-100 dark:bg-red-950/80 text-red-700 dark:text-red-300 text-[11px] font-bold font-mono">
                          <span class="material-symbols-outlined text-[14px]">undo</span>
                          <span>Reversed</span>
                        </div>
                      {:else}
                        <div class="flex items-center gap-1 px-2 py-0.5 rounded-md bg-emerald-100 dark:bg-emerald-950/80 text-emerald-700 dark:text-emerald-300 text-[11px] font-bold font-mono">
                          <span class="material-symbols-outlined text-[14px]">verified</span>
                          <span>Active</span>
                        </div>
                      {/if}
                    </div>

                    <!-- Bento Cards Grid: 3 Compact Cards (Reference, Partner, Recorded By) -->
                    <div class="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
                      <!-- Card 1: Reference # -->
                      <div class="bg-surface p-2.5 px-3 rounded-xl border border-outline-variant/60 flex items-center gap-2.5 shadow-2xs">
                        <div class="w-7 h-7 rounded-lg bg-primary/10 flex items-center justify-center text-primary shrink-0">
                          <span class="material-symbols-outlined text-[15px]">tag</span>
                        </div>
                        <div class="min-w-0">
                          <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">
                            Reference / DC #
                          </span>
                          <div class="font-mono text-xs font-bold text-on-surface truncate">
                            {m.referenceNumber && m.referenceNumber !== '-' ? m.referenceNumber : 'None Assigned'}
                          </div>
                        </div>
                      </div>

                      <!-- Card 2: Assigned Partner -->
                      <div class="bg-surface p-2.5 px-3 rounded-xl border border-outline-variant/60 flex items-center gap-2.5 shadow-2xs">
                        <div class="w-7 h-7 rounded-lg bg-primary/10 flex items-center justify-center text-primary shrink-0">
                          <span class="material-symbols-outlined text-[15px]">business</span>
                        </div>
                        <div class="min-w-0">
                          <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">
                            Assigned Partner
                          </span>
                          <div class="text-xs font-bold text-on-surface truncate">
                            {m.clientName && m.clientName !== '-' ? m.clientName : 'Internal (In-House)'}
                          </div>
                        </div>
                      </div>

                      <!-- Card 3: Recorded By & Date -->
                      <div class="bg-surface p-2.5 px-3 rounded-xl border border-outline-variant/60 flex items-center gap-2.5 shadow-2xs">
                        <div class="w-7 h-7 rounded-lg bg-primary/10 flex items-center justify-center text-primary shrink-0">
                          <span class="material-symbols-outlined text-[15px]">person</span>
                        </div>
                        <div class="min-w-0">
                          <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block">
                            Recorded By
                          </span>
                          <div class="text-xs font-bold text-on-surface truncate">
                            {m.user || 'System Operator'} <span class="text-[10px] font-mono text-on-surface-variant font-normal">• {m.date}</span>
                          </div>
                        </div>
                      </div>
                    </div>

                    <!-- Remarks / Operational Notes Section (Compact) -->
                    <div class="bg-surface p-2.5 px-3 rounded-xl border border-outline-variant/60 shadow-2xs flex items-start gap-2.5">
                      <div class="w-7 h-7 rounded-lg bg-primary/10 flex items-center justify-center text-primary shrink-0 mt-0.5">
                        <span class="material-symbols-outlined text-[15px]">sticky_note_2</span>
                      </div>
                      <div class="min-w-0 flex-1">
                        <span class="text-[9px] font-mono uppercase tracking-wider text-on-surface-variant font-bold block mb-0.5">
                          Remarks & Operational Notes
                        </span>
                        {#if m.remarks && m.remarks !== '-'}
                          <p class="text-xs text-on-surface font-medium leading-relaxed italic">
                            "{m.remarks}"
                          </p>
                        {:else}
                          <p class="text-xs text-on-surface-variant/70 italic font-mono">
                            No remarks or notes recorded for this movement transfer.
                          </p>
                        {/if}
                      </div>
                    </div>

                  </div>
                </td>
              </tr>
            {/if}
          {/each}
        </tbody>
      </table>
    </div>
  {:else}
    <EmptyState icon="history_toggle_off" title="No movement history" message="Movements recorded for this lot will appear here." />
  {/if}
</section>
