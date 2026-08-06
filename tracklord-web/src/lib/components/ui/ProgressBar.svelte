<script>
  /** @type {{ label: string, value: number, total: number, unit?: string, color?: 'primary' | 'secondary' | 'outline' | 'tertiary' }} */
  let {
    label = '',
    value = 0,
    total = 100,
    unit = 'Units',
    color = 'primary'
  } = $props();

  let percentage = $derived(total > 0 ? Math.min(100, Math.max(0, (value / total) * 100)) : 0);

  const barColors = {
    primary: 'bg-[#2563eb]',
    secondary: 'bg-[#006e2d]',
    outline: 'bg-[#737686]',
    tertiary: 'bg-[#973400]'
  };
</script>

<div class="space-y-1.5 w-full">
  <div class="flex justify-between items-center text-sm font-medium text-[#0b1c30]">
    <span>{label}</span>
    <span class="font-mono text-xs font-semibold text-[#434655]">{value} {unit}</span>
  </div>
  <div class="w-full bg-[#d3e4fe] rounded-full h-2 overflow-hidden">
    <div
      class="h-2 rounded-full transition-all duration-500 ease-out {barColors[color]}"
      style="width: {percentage}%"
    ></div>
  </div>
</div>
