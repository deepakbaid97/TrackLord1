<script>
  /** @type {{ title?: string }} */
  let { title = 'PRO-FAB ERP' } = $props();
</script>

<header class="fixed top-0 w-full z-50 bg-[#f8f9ff] shadow-sm flex justify-between items-center px-4 md:px-8 h-14 border-b border-[#c3c6d7]/40">
  <div class="flex items-center gap-2 cursor-pointer hover:bg-[#eff4ff] transition-colors p-2 rounded-full active:opacity-80">
    <span class="material-symbols-outlined text-[#004ac6] text-[24px]">factory</span>
  </div>

  <div class="font-semibold text-lg tracking-tight text-[#004ac6] font-['Inter']">
    {title}
  </div>

  <div class="flex items-center gap-3">
    <button class="relative flex items-center justify-center p-2 rounded-full hover:bg-[#eff4ff] transition-colors active:scale-95 text-[#434655]">
      <span class="material-symbols-outlined text-[22px]">notifications</span>
      <span class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-[#2563eb]"></span>
    </button>
  </div>
</header>
