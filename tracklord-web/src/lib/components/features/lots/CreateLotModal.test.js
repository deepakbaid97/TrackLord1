import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import CreateLotModal from './CreateLotModal.svelte';
import * as api from '$lib/api.js';
import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';

vi.mock('$lib/api.js', () => ({
  createProductionLot: vi.fn()
}));

describe('CreateLotModal component', () => {
  const mockSteps = [
    { id: 'step-1', code: 'CUT', name: 'Cutting' },
    { id: 'step-2', code: 'STITCH', name: 'Stitching' }
  ];

  const mockClients = [
    { id: 'client-1', name: 'Zara', clientType: 'Buyer' }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    tenantConfigStore.resetToDefaults();
  });

  it('renders lot form fields and submits payload', async () => {
    vi.mocked(api.createProductionLot).mockResolvedValue({ id: 'lot-new-1' });
    const handleSuccess = vi.fn();

    render(CreateLotModal, {
      props: {
        isOpen: true,
        steps: mockSteps,
        clients: mockClients,
        onSuccess: handleSuccess
      }
    });

    expect(screen.getByText('Create Production Lot')).toBeInTheDocument();
    const lotInput = screen.getByPlaceholderText('e.g. LOT-2026-001');
    const qtyInput = screen.getByPlaceholderText('e.g. 500');

    await fireEvent.input(lotInput, { target: { value: ' LOT-2026-999 ' } });
    await fireEvent.input(qtyInput, { target: { value: '250' } });

    const submitBtn = screen.getByRole('button', { name: /create lot/i });
    await fireEvent.click(submitBtn);

    expect(api.createProductionLot).toHaveBeenCalledWith(expect.objectContaining({
      lotNumber: 'LOT-2026-999',
      initialQuantity: 250,
      initialStepId: 'step-1'
    }));

    expect(handleSuccess).toHaveBeenCalledWith(expect.objectContaining({
      id: 'lot-new-1',
      lotNumber: 'LOT-2026-999'
    }));
  });

  it('displays client selector when clientMode is "lot"', () => {
    tenantConfigStore.setClientMode('lot');

    render(CreateLotModal, {
      props: {
        isOpen: true,
        steps: mockSteps,
        clients: mockClients
      }
    });

    expect(screen.getByText(/Assign Client \/ Buyer/i)).toBeInTheDocument();
  });
});
