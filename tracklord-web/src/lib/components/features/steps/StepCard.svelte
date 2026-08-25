<script>
  import { STEP_ICON_MAP } from '$lib/services/lotService.js';

  let {
    step,
    index,
    isDragging = false,
    draggedStepIndex = null,
    dragOverStepIndex = null,
    onEdit,
    onDragStart,
    onDragOver,
    onDragEnter,
    onDrop,
    onDragEnd,
    onTouchStart,
    onTouchMove,
    onTouchEnd,
    onTouchCancel
  } = $props();
</script>

<div
  data-step-index={index}
  draggable="true"
  ondragstart={onDragStart}
  ondragover={onDragOver}
  ondragenter={onDragEnter}
  ondrop={onDrop}
  ondragend={onDragEnd}
  role="button"
  tabindex="0"
  onclick={() => { if (!isDragging) window.location.href = `/lots?step=${step.id}`; }}
  onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && !isDragging && (window.location.href = `/lots?step=${step.id}`)}
  class="bg-surface px-3 py-2.5 rounded-xl border border-outline-variant/70 hover:border-primary/50 shadow-2xs hover:shadow-xs transition-all cursor-pointer flex items-center justify-between gap-2 active:scale-[0.99] select-none {draggedStepIndex === index ? 'opacity-30 bg-primary/5 border-dashed border-primary' : ''} {dragOverStepIndex === index && draggedStepIndex !== index ? 'ring-2 ring-primary ring-inset bg-primary/10 shadow-md' : ''}"
  title="Click to view lots at '{step.name}', or drag grip to reorder"
>
  <!-- Left: Drag Grip | Seq # | Icon | Name + Code | Status Flags -->
  <div class="flex items-center gap-1.5 min-w-0">
    <!-- Drag Grip -->
    <div
      role="button"
      tabindex="0"
      class="text-on-surface-variant/40 hover:text-primary active:text-primary cursor-grab active:cursor-grabbing p-1 -ml-1 shrink-0 rounded flex items-center justify-center touch-none select-none active:bg-primary/20"
      ontouchstart={onTouchStart}
      ontouchmove={onTouchMove}
      ontouchend={onTouchEnd}
      ontouchcancel={onTouchCancel}
      onclick={(e) => e.stopPropagation()}
      onkeydown={(e) => e.stopPropagation()}
      aria-label="Drag to reorder sequence"
      title="Touch & drag to reorder"
    >
      <span class="material-symbols-outlined text-[18px] pointer-events-none select-none">drag_indicator</span>
    </div>

    <!-- Sequence Number Badge -->
    <span class="inline-flex items-center justify-center w-5 h-5 rounded-md bg-primary/10 text-primary font-bold font-mono text-[11px] shrink-0 select-none">
      #{step.sequenceOrder}
    </span>

    <!-- Step Icon -->
    <div class="w-6 h-6 rounded-lg bg-surface-container-low flex items-center justify-center text-primary shrink-0">
      <span class="material-symbols-outlined text-[15px] pointer-events-none select-none">
        {STEP_ICON_MAP[step.code] || 'work'}
      </span>
    </div>

    <!-- Name & Code -->
    <div class="min-w-0 truncate">
      <div class="flex items-center gap-1 truncate">
        <span class="font-bold text-xs sm:text-sm text-on-surface truncate select-none">{step.name}</span>
        <span class="font-mono text-[10px] text-primary/80 font-bold uppercase shrink-0">({step.code})</span>
      </div>
    </div>

    {#if step.isTerminating}
      <span class="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[9px] font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-900 dark:text-amber-300 border border-amber-300 select-none shrink-0" title="Terminating Step">
        <span class="material-symbols-outlined text-[11px] text-amber-600 pointer-events-none">flag</span>
        Final
      </span>
    {:else if step.isActive === false}
      <span class="px-1.5 py-0.5 rounded text-[9px] font-bold bg-zinc-100 dark:bg-zinc-800 text-zinc-500 border border-zinc-300 select-none shrink-0">
        Inactive
      </span>
    {/if}
  </div>

  <!-- Right: Quantity Pill, Lot Count & Edit Button -->
  <div class="flex items-center gap-2 shrink-0">
    {#if (step.totalQuantity || 0) > 0}
      <div class="flex flex-col items-end leading-none font-mono">
        <span class="text-xs font-bold text-primary">
          {(step.totalQuantity || 0).toLocaleString()} Pcs
        </span>
        <span class="text-[9px] text-on-surface-variant font-medium mt-0.5">
          {step.lotsAtStep?.length || 0} {step.lotsAtStep?.length === 1 ? 'lot' : 'lots'}
        </span>
      </div>
    {:else}
      <span class="text-[11px] text-on-surface-variant/50 font-mono">
        0 Pcs
      </span>
    {/if}

    <button
      type="button"
      onclick={(e) => { e.stopPropagation(); if (onEdit) onEdit(); }}
      class="p-1 text-on-surface-variant/70 hover:text-primary hover:bg-surface-container rounded-md transition-colors"
      title="Edit step configuration"
    >
      <span class="material-symbols-outlined text-[15px]">edit</span>
    </button>
  </div>
</div>
