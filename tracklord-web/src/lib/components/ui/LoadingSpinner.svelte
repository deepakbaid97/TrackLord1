<script>
  let { message = 'Loading...' } = $props();
</script>

<div class="p-12 text-center text-on-surface-variant flex flex-col items-center gap-3">
  <span class="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
  <span class="text-xs sm:text-sm font-medium">{message}</span>
</div>
