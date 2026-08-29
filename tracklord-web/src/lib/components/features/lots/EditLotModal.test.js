import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import EditLotModal from './EditLotModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  updateProductionLot: vi.fn()
}));

describe('EditLotModal component', () => {
  const mockLot = {
    id: 'lot-123',
    lotNumber: 'LOT-2026-001',
    initialQuantity: 100,
    status: 'Active',
    clientId: 'client-1',
    clientName: 'Zara',
    createdAtUtc: '2026-08-25T10:00:00Z',
    remarks: 'Initial notes'
  };

  const mockClients = [
    { id: 'client-1', name: 'Zara', clientType: 'Buyer' },
    { id: 'client-2', name: 'H&M', clientType: 'Subcontractor' }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders fixed lot details and allows updating client and date', async () => {
    vi.mocked(api.updateProductionLot).mockResolvedValue({ success: true });
    const handleSuccess = vi.fn();

    render(EditLotModal, {
      props: {
        isOpen: true,
        lot: mockLot,
        clients: mockClients,
        onSuccess: handleSuccess
      }
    });

    expect(screen.getByText('Edit Production Lot: LOT-2026-001')).toBeInTheDocument();
    expect(screen.getByText('#LOT-2026-001')).toBeInTheDocument();
    expect(screen.getByText('100 Pcs')).toBeInTheDocument();

    // Open client dropdown and pick H&M
    const selectTrigger = screen.getByText('Zara (Buyer)');
    await fireEvent.click(selectTrigger);

    const optionBtn = screen.getByText('H&M (Subcontractor)');
    await fireEvent.click(optionBtn);

    // Submit form
    const submitBtn = screen.getByRole('button', { name: /save changes/i });
    await fireEvent.click(submitBtn);

    expect(api.updateProductionLot).toHaveBeenCalledWith(
      'lot-123',
      expect.objectContaining({
        clientId: 'client-2',
        clientName: 'H&M'
      })
    );

    expect(handleSuccess).toHaveBeenCalled();
  });
});
