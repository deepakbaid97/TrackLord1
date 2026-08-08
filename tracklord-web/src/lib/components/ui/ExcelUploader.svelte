<script>
  /** 
   * @type {{ 
   *   accept?: string, 
   *   selectedFileName?: string, 
   *   onfileselect: (e: Event) => void 
   * }} 
   */
  let {
    accept = '.xlsx, .xls, .csv',
    selectedFileName = '',
    onfileselect
  } = $props();
</script>

<div class="border-2 border-dashed border-outline-variant hover:border-primary rounded-2xl p-6 text-center bg-surface-container-lowest transition-colors relative group">
  <input
    type="file"
    {accept}
    onchange={onfileselect}
    class="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
  />
  <div class="flex flex-col items-center gap-2">
    <div class="w-12 h-12 rounded-full bg-primary/10 text-primary flex items-center justify-center group-hover:scale-110 transition-transform">
      <span class="material-symbols-outlined text-[28px]">upload_file</span>
    </div>
    <div>
      <span class="font-bold text-sm text-primary underline">Click to select file</span>
      <span class="text-xs text-on-surface-variant"> or drag and drop</span>
    </div>
    <div class="flex gap-2 mt-1">
      <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-surface-container-high text-on-surface-variant">.XLSX</span>
      <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-surface-container-high text-on-surface-variant">.XLS</span>
      <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono bg-surface-container-high text-on-surface-variant">.CSV</span>
    </div>
    {#if selectedFileName}
      <div class="mt-2 text-xs font-mono font-bold text-emerald-600 flex items-center gap-1">
        <span class="material-symbols-outlined text-[16px]">description</span>
        Selected: {selectedFileName}
      </div>
    {/if}
  </div>
</div>
