<script>
  /** 
   * @type {{ 
   *   clientType: string 
   * }} 
   */
  let { clientType = 'Buyer' } = $props();
</script>

<span class="px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider inline-block
  {clientType === 'Buyer' ? 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300' :
   clientType === 'Wholesaler' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300' :
   clientType === 'Job Worker' ? 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300' :
   'bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300'}">
  {clientType}
</span>
