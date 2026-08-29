import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/svelte';
import { get } from 'svelte/store';
import InventoryPage from './+page.svelte';
import { headerStore } from '$lib/stores/headerStore.js';
import { authStore } from '$lib/stores/authStore.js';
import * as api from '$lib/api.js';

vi.mock('$app/stores', () => {
  const { readable } = require('svelte/store');
  return {
    page: readable({
      url: new URL('http://localhost/inventory'),
      params: {},
      route: { id: '/inventory' }
    })
  };
});

vi.mock('$lib/api.js', () => ({
  getManufacturingSteps: vi.fn(),
  getProductionLots: vi.fn(),
  getMaterialLots: vi.fn(),
  getLotJourney: vi.fn(),
  getMaterialTraceability: vi.fn(),
  getClients: vi.fn(),
  deleteProductionLot: vi.fn(),
  moveLot: vi.fn()
}));

describe('Inventory Page Header Actions', () => {
  const mockSteps = [
    { id: 'step-1', code: 'CUT', name: 'Cutting', sequence: 1 },
    { id: 'step-2', code: 'STITCH', name: 'Stitching', sequence: 2 }
  ];

  const mockLots = [
    {
      id: 'lot-1',
      lotNumber: 'LOT-2026-001',
      clientName: 'Acme Corp',
      initialQuantity: 100,
      status: 'In Progress'
    }
  ];

  const mockJourney = {
    lotId: 'lot-1',
    lotNumber: 'LOT-2026-001',
    status: 'In Progress',
    clientName: 'Acme Corp',
    createdAtUtc: new Date().toISOString(),
    activeBalances: [{ stepId: 'step-1', stepName: 'Cutting', quantity: 100 }],
    movementHistory: []
  };

  const mockTraceability = {
    lotId: 'lot-1',
    lotNumber: 'LOT-2026-001',
    consumptions: []
  };

  beforeEach(() => {
    headerStore.reset();
    authStore.setUser({
      id: 'admin-1',
      name: 'Admin User',
      role: 'Admin'
    }, 'test-token');
    vi.clearAllMocks();

    vi.mocked(api.getManufacturingSteps).mockResolvedValue(mockSteps);
    vi.mocked(api.getProductionLots).mockResolvedValue(mockLots);
    vi.mocked(api.getMaterialLots).mockResolvedValue([]);
    vi.mocked(api.getClients).mockResolvedValue([]);
    vi.mocked(api.getLotJourney).mockResolvedValue(mockJourney);
    vi.mocked(api.getMaterialTraceability).mockResolvedValue(mockTraceability);
  });

  it('registers the Actions dropdown in headerStore with all 5 required actions', async () => {
    render(InventoryPage);

    await waitFor(() => {
      const state = get(headerStore);
      expect(state.actions).toBeDefined();
      expect(state.actions.length).toBeGreaterThanOrEqual(1);
    });

    const state = get(headerStore);
    const actionsDropdown = state.actions.find(a => a.label === 'Actions');
    expect(actionsDropdown).toBeDefined();
    expect(actionsDropdown?.variant).toBe('primary');
    expect(actionsDropdown?.icon).toBe('bolt');
    expect(actionsDropdown?.dropdownItems).toHaveLength(5);

    const items = actionsDropdown?.dropdownItems || [];
    const itemLabels = items.map(i => i.label);
    expect(itemLabels).toEqual(['Move Qty', 'New Lot', 'Material', 'Consume', 'Rejection']);

    const moveItem = items.find(i => i.label === 'Move Qty');
    expect(moveItem?.icon).toBe('move_item');

    const newLotItem = items.find(i => i.label === 'New Lot');
    expect(newLotItem?.icon).toBe('add');

    const materialItem = items.find(i => i.label === 'Material');
    expect(materialItem?.icon).toBe('inventory');

    const consumeItem = items.find(i => i.label === 'Consume');
    expect(consumeItem?.icon).toBe('local_mall');

    const rejectionItem = items.find(i => i.label === 'Rejection');
    expect(rejectionItem?.icon).toBe('report_problem');
    expect(rejectionItem?.danger).toBe(true);
  });

  it('executes "Move Qty" action and opens MoveLotModal', async () => {
    render(InventoryPage);

    await waitFor(() => {
      expect(get(headerStore).actions?.length).toBeGreaterThanOrEqual(1);
    });

    const actionsDropdown = get(headerStore).actions?.find(a => a.label === 'Actions');
    const moveItem = actionsDropdown?.dropdownItems?.find(i => i.label === 'Move Qty');

    moveItem?.onclick();

    await waitFor(() => {
      expect(screen.getByText('Move Work-in-Progress (WIP)')).toBeInTheDocument();
    });
  });

  it('executes "New Lot" action and opens CreateLotModal', async () => {
    render(InventoryPage);

    await waitFor(() => {
      expect(get(headerStore).actions?.length).toBeGreaterThanOrEqual(1);
    });

    const actionsDropdown = get(headerStore).actions?.find(a => a.label === 'Actions');
    const newLotItem = actionsDropdown?.dropdownItems?.find(i => i.label === 'New Lot');

    newLotItem?.onclick();

    await waitFor(() => {
      expect(screen.getByText('Create Production Lot')).toBeInTheDocument();
    });
  });

  it('executes "Material" action and opens CreateMaterialModal', async () => {
    render(InventoryPage);

    await waitFor(() => {
      expect(get(headerStore).actions?.length).toBeGreaterThanOrEqual(1);
    });

    const actionsDropdown = get(headerStore).actions?.find(a => a.label === 'Actions');
    const materialItem = actionsDropdown?.dropdownItems?.find(i => i.label === 'Material');

    materialItem?.onclick();

    await waitFor(() => {
      expect(screen.getByText('Register Raw Material Lot')).toBeInTheDocument();
    });
  });

  it('executes "Consume" action and opens ConsumeMaterialModal', async () => {
    render(InventoryPage);

    await waitFor(() => {
      expect(get(headerStore).actions?.length).toBeGreaterThanOrEqual(1);
    });

    const actionsDropdown = get(headerStore).actions?.find(a => a.label === 'Actions');
    const consumeItem = actionsDropdown?.dropdownItems?.find(i => i.label === 'Consume');

    consumeItem?.onclick();

    await waitFor(() => {
      expect(screen.getByText('Log Raw Material Consumption')).toBeInTheDocument();
    });
  });

  it('executes "Rejection" action and opens RejectItemsModal', async () => {
    render(InventoryPage);

    await waitFor(() => {
      expect(get(headerStore).actions?.length).toBeGreaterThanOrEqual(1);
    });

    const actionsDropdown = get(headerStore).actions?.find(a => a.label === 'Actions');
    const rejectionItem = actionsDropdown?.dropdownItems?.find(i => i.label === 'Rejection');

    rejectionItem?.onclick();

    await waitFor(() => {
      expect(screen.getByText('Log Rejection / Rework Transfer')).toBeInTheDocument();
    });
  });
});
