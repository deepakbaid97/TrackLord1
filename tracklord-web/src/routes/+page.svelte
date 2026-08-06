<script>
  import Header from '$lib/components/ui/Header.svelte';
  import Navbar from '$lib/components/ui/Navbar.svelte';
  import Badge from '$lib/components/ui/Badge.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import Card from '$lib/components/ui/Card.svelte';
  import Timeline from '$lib/components/ui/Timeline.svelte';
  import ProgressBar from '$lib/components/ui/ProgressBar.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Modal from '$lib/components/ui/Modal.svelte';
  import Table from '$lib/components/ui/Table.svelte';

  // Active view tab (lots, ledger)
  let activeTab = $state('lots');
  let isMoveModalOpen = $state(false);

  // Dynamic state for Lot-101
  let lotInfo = $state({
    lotNumber: 'Lot-101',
    customer: 'ABC Garments',
    currentStep: 'Washing',
    age: '3 Days',
    totalQuantity: 95
  });

  // Step balances state (Lot-101 split inventory)
  let inventoryDistribution = $state([
    { label: 'Washing (Current)', value: 15, total: 95, color: 'primary' },
    { label: 'Finishing (Queued)', value: 80, total: 95, color: 'outline' },
    { label: 'Completed', value: 5, total: 95, color: 'secondary' }
  ]);

  // Timeline steps state
  let timelineSteps = $state([
    { id: '1', name: 'Cutting', status: 'completed', subtitle: 'Completed (1 Day)', icon: 'content_cut' },
    { id: '2', name: 'Stitching', status: 'completed', subtitle: 'Completed (2 Days)', icon: 'webhook' },
    { id: '3', name: 'Washing', status: 'in-progress', subtitle: 'In Progress', icon: 'local_laundry_service' },
    { id: '4', name: 'Finishing', status: 'pending', subtitle: 'Pending', icon: 'iron' },
    { id: '5', name: 'Packing', status: 'pending', subtitle: 'Pending', icon: 'inventory_2' }
  ]);

  // Raw Material Consumptions state
  let materials = $state([
    { name: 'Grey Cloth', value: '30m', icon: 'texture' },
    { name: 'Buttons', value: '100', icon: 'radio_button_checked' },
    { name: 'Labels', value: '95', icon: 'label' }
  ]);

  // Movements Audit Ledger state
  let movementsLedger = $state([
    { id: 'MOV-101', date: '2026-08-04 09:30', from: 'Initial Creation', to: 'Cutting', qty: '95 Pcs', user: 'Operator1', ref: 'INIT-Lot-101' },
    { id: 'MOV-102', date: '2026-08-05 14:00', from: 'Cutting', to: 'Stitching', qty: '95 Pcs', user: 'Operator2', ref: 'STITCH-001' },
    { id: 'MOV-103', date: '2026-08-06 10:15', from: 'Stitching', to: 'Washing', qty: '95 Pcs', user: 'Operator3', ref: 'WASH-001' },
    { id: 'MOV-104', date: '2026-08-06 16:45', from: 'Washing', to: 'Finishing', qty: '80 Pcs', user: 'Supervisor', ref: 'FIN-001' }
  ]);

  const movementTableColumns = [
    { key: 'id', label: 'Movement ID' },
    { key: 'date', label: 'Movement Date' },
    { key: 'from', label: 'From Step' },
    { key: 'to', label: 'To Step' },
    { key: 'qty', label: 'Quantity' },
    { key: 'user', label: 'User' },
    { key: 'ref', label: 'Reference' }
  ];

  // Move Lot Modal Form state
  let moveForm = $state({
    fromStep: 'Washing',
    toStep: 'Finishing',
    quantity: 15,
    remarks: 'Transfer remaining WIP to Finishing'
  });

  function handleMoveSubmit(e) {
    e.preventDefault();
    const qty = Number(moveForm.quantity);
    if (qty <= 0) return alert('Quantity must be greater than 0');
    if (qty > inventoryDistribution[0].value) return alert('Cannot move more than available balance in Washing!');

    // Deduct from Washing, add to Finishing
    inventoryDistribution[0].value -= qty;
    inventoryDistribution[1].value += qty;

    // Add new movement to audit ledger
    const now = new Date();
    const formattedDate = `${now.toISOString().split('T')[0]} ${now.toTimeString().split(' ')[0].substring(0, 5)}`;
    movementsLedger = [
      {
        id: `MOV-${105 + movementsLedger.length}`,
        date: formattedDate,
        from: moveForm.fromStep,
        to: moveForm.toStep,
        qty: `${qty} Pcs`,
        user: 'Current User',
        ref: `REF-${Math.floor(Math.random() * 900 + 100)}`
      },
      ...movementsLedger
    ];

    // If washing reaches 0 balance, update timeline status
    if (inventoryDistribution[0].value === 0) {
      timelineSteps[2].status = 'completed';
      timelineSteps[2].subtitle = 'Completed';
      timelineSteps[3].status = 'in-progress';
      timelineSteps[3].subtitle = 'In Progress';
      lotInfo.currentStep = 'Finishing';
    }

    isMoveModalOpen = false;
  }
</script>

<!-- TopAppBar -->
<header class="fixed top-0 w-full z-50 bg-surface dark:bg-surface-dim shadow-sm flex justify-between items-center px-margin-mobile h-touch-target-min border-b border-outline-variant">
  <div class="flex items-center gap-2 cursor-pointer hover:bg-surface-container-high transition-colors p-2 rounded-full active:opacity-80">
    <span class="material-symbols-outlined text-primary text-[24px]">factory</span>
  </div>
  <div class="font-headline-md text-headline-md font-bold text-primary">PRO-FAB ERP</div>
  <div class="flex items-center gap-2 cursor-pointer hover:bg-surface-container-high transition-colors p-2 rounded-full active:opacity-80">
    <span class="material-symbols-outlined text-on-surface-variant">notifications</span>
  </div>
</header>

<!-- Main Content -->
<main class="max-w-[1440px] mx-auto p-margin-mobile md:p-margin-desktop space-y-8 pt-16 pb-24 md:pb-8">
  <!-- View Mode Sub-tabs -->
  <div class="flex items-center justify-between border-b border-outline-variant pb-2">
    <div class="flex gap-2">
      <button
        onclick={() => activeTab = 'lots'}
        class="px-4 py-2 rounded-lg font-stat-label text-stat-label transition-colors {activeTab === 'lots' ? 'bg-primary text-on-primary font-bold shadow-sm' : 'text-on-surface-variant hover:bg-surface-container-low'}"
      >
        Lot Details
      </button>
      <button
        onclick={() => activeTab = 'ledger'}
        class="px-4 py-2 rounded-lg font-stat-label text-stat-label transition-colors {activeTab === 'ledger' ? 'bg-primary text-on-primary font-bold shadow-sm' : 'text-on-surface-variant hover:bg-surface-container-low'}"
      >
        Movements Ledger ({movementsLedger.length})
      </button>
    </div>

    <button
      onclick={() => location.reload()}
      class="px-3 py-1.5 rounded-lg border border-outline-variant text-on-surface-variant font-stat-label text-xs hover:bg-surface-container-low flex items-center gap-1"
    >
      <span class="material-symbols-outlined text-[16px]">refresh</span>
      Reset
    </button>
  </div>

  {#if activeTab === 'lots'}
    <!-- Header Info -->
    <section class="flex flex-col md:flex-row justify-between items-start md:items-end gap-4 bg-surface-container-low p-card-padding rounded-xl border border-outline-variant shadow-sm">
      <div>
        <h1 class="font-headline-lg-mobile text-headline-lg-mobile md:font-headline-lg md:text-headline-lg text-primary">{lotInfo.lotNumber}</h1>
        <p class="font-body-md text-body-md text-on-surface-variant mt-1">Customer: <span class="font-semibold text-on-surface">{lotInfo.customer}</span></p>
      </div>

      <div class="flex flex-wrap gap-2">
        <div class="bg-primary-container text-on-primary-container px-3 py-1 rounded-full font-stat-label text-stat-label flex items-center gap-1">
          <span class="material-symbols-outlined text-[16px]">local_laundry_service</span>
          Current Step: {lotInfo.currentStep}
        </div>
        <div class="bg-surface-variant text-on-surface-variant px-3 py-1 rounded-full font-stat-label text-stat-label flex items-center gap-1">
          <span class="material-symbols-outlined text-[16px]">schedule</span>
          Age: {lotInfo.age}
        </div>
      </div>
    </section>

    <!-- Grid Content -->
    <div class="grid grid-cols-1 md:grid-cols-12 gap-6">
      <!-- Journey Timeline -->
      <section class="md:col-span-4 bg-surface p-card-padding rounded-xl border border-outline-variant shadow-sm">
        <h2 class="font-headline-md text-headline-md border-b border-outline-variant pb-2 mb-4">Journey Timeline</h2>
        <Timeline steps={timelineSteps} />
      </section>

      <div class="md:col-span-8 space-y-6">
        <!-- Current Inventory Distribution -->
        <section class="bg-surface p-card-padding rounded-xl border border-outline-variant shadow-sm">
          <h2 class="font-headline-md text-headline-md border-b border-outline-variant pb-2 mb-4">Inventory Distribution</h2>
          <div class="space-y-4">
            {#each inventoryDistribution as dist}
              <div>
                <div class="flex justify-between font-stat-label text-stat-label mb-1">
                  <span>{dist.label}</span>
                  <span>{dist.value} Units</span>
                </div>
                <div class="w-full bg-surface-variant rounded-full h-2">
                  <div
                    class="{dist.color === 'primary' ? 'bg-primary' : dist.color === 'secondary' ? 'bg-secondary' : 'bg-outline'} h-2 rounded-full transition-all duration-500"
                    style="width: {dist.total > 0 ? (dist.value / dist.total) * 100 : 0}%"
                  ></div>
                </div>
              </div>
            {/each}
          </div>
        </section>

        <!-- Material Consumption -->
        <section class="bg-surface p-card-padding rounded-xl border border-outline-variant shadow-sm">
          <h2 class="font-headline-md text-headline-md border-b border-outline-variant pb-2 mb-4">Material Consumption</h2>
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {#each materials as mat}
              <div class="bg-surface-container-low p-4 rounded-lg border border-outline-variant flex flex-col items-center justify-center text-center">
                <span class="material-symbols-outlined text-[32px] text-tertiary mb-2">{mat.icon}</span>
                <div class="font-display-metrics text-display-metrics text-on-surface">{mat.value}</div>
                <div class="font-label-caps text-label-caps text-on-surface-variant uppercase">{mat.name}</div>
              </div>
            {/each}
          </div>
        </section>
      </div>
    </div>

    <!-- Sticky Bottom Action -->
    <div class="fixed bottom-0 md:bottom-auto md:relative left-0 w-full md:w-auto p-4 md:p-0 bg-surface md:bg-transparent border-t md:border-t-0 border-outline-variant flex justify-end z-40 mb-16 md:mb-0">
      <button
        onclick={() => isMoveModalOpen = true}
        class="w-full md:w-auto h-touch-target-min px-8 bg-[#2563EB] text-white font-stat-label text-stat-label rounded-lg flex items-center justify-center gap-2 hover:bg-primary-fixed hover:text-on-primary-fixed transition-colors shadow-sm cursor-pointer"
      >
        <span class="material-symbols-outlined">iron</span>
        Move to Finishing
      </button>
    </div>
  {:else if activeTab === 'ledger'}
    <!-- Ledger Table -->
    <section class="bg-surface p-card-padding rounded-xl border border-outline-variant shadow-sm space-y-4">
      <h2 class="font-headline-md text-headline-md border-b border-outline-variant pb-2">Inventory Movement Ledger</h2>
      <Table columns={movementTableColumns} rows={movementsLedger} />
    </section>
  {/if}
</main>

<!-- Interactive Move Lot Modal -->
<Modal bind:isOpen={isMoveModalOpen} title="Move Production Lot Quantity">
  <form onsubmit={handleMoveSubmit} class="space-y-4">
    <div class="p-3 bg-surface-container-low rounded-lg border border-outline-variant text-xs font-label-caps space-y-1">
      <div>Lot Identity: <span class="font-bold text-primary">Lot-101 (IMMUTABLE)</span></div>
      <div>Current Washing Balance: <span class="font-bold text-secondary">{inventoryDistribution[0].value} Pcs</span></div>
    </div>

    <Input
      label="Source Step"
      value={moveForm.fromStep}
      disabled={true}
      icon="local_laundry_service"
    />

    <Select
      label="Destination Step"
      bind:value={moveForm.toStep}
      options={[
        { label: 'Finishing', value: 'Finishing' },
        { label: 'Packing', value: 'Packing' }
      ]}
      icon="iron"
      required={true}
    />

    <Input
      label="Movement Quantity (Pcs)"
      type="number"
      bind:value={moveForm.quantity}
      placeholder="Enter quantity..."
      icon="tag"
      required={true}
    />

    <Input
      label="Remarks / Ref Number"
      bind:value={moveForm.remarks}
      placeholder="Optional notes..."
      icon="notes"
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isMoveModalOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="send">
        Confirm Movement
      </Button>
    </div>
  </form>
</Modal>

<!-- BottomNavBar for Mobile -->
<Navbar activeTab={activeTab} ontabchange={(tab) => activeTab = tab} />
