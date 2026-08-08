<script>
  /** 
   * @type {{ 
   *   type?: 'success' | 'error', 
   *   message?: string, 
   *   ondismiss?: () => void 
   * }} 
   */
  let {
    type = 'error',
    message = '',
    ondismiss
  } = $props();
</script>

{#if message}
  <div class="p-4 rounded-xl text-sm flex items-center justify-between shadow-sm border
    {type === 'error'
      ? 'bg-red-50 dark:bg-red-950/40 border-red-200 dark:border-red-800 text-red-700 dark:text-red-300'
      : 'bg-emerald-50 dark:bg-emerald-950/40 border-emerald-200 dark:border-emerald-800 text-emerald-700 dark:text-emerald-300'}"
  >
    <div class="flex items-center gap-2">
      <span class="material-symbols-outlined {type === 'error' ? 'text-red-500' : 'text-emerald-500'}">
        {type === 'error' ? 'error' : 'check_circle'}
      </span>
      <span>{message}</span>
    </div>
    {#if ondismiss}
      <button onclick={ondismiss} class="{type === 'error' ? 'text-red-500 hover:text-red-700' : 'text-emerald-500 hover:text-emerald-700'} cursor-pointer">
        <span class="material-symbols-outlined text-[18px]">close</span>
      </button>
    {/if}
  </div>
{/if}
