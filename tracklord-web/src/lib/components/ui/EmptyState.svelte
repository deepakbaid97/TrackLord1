<script>
  let {
    icon = 'search_off',
    title = 'No items found',
    message = 'Try adjusting your search query or filters.'
  } = $props();
</script>

<div class="p-10 sm:p-12 text-center text-on-surface-variant bg-surface rounded-2xl border border-dashed border-outline-variant">
  <span class="material-symbols-outlined text-[42px] sm:text-[48px] text-on-surface-variant/40 mb-2 block">{icon}</span>
  <h3 class="text-sm sm:text-base font-bold text-on-surface">{title}</h3>
  {#if message}
    <p class="text-xs text-on-surface-variant mt-1">{message}</p>
  {/if}
</div>
