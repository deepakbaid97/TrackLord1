import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import MoveLotModal from './MoveLotModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  moveLot: vi.fn()
}));

describe('MoveLotModal component', () => {
  const mockSteps = [
    { id: 's1', code: 'CUT', name: 'Cutting' },
    { id: 's2', code: 'STITCH', name: 'Stitching' }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders movement modal and submits WIP transfer', async () => {
    vi.mocked(api.moveLot).mockResolvedValue({ id: 'mov-1' });
    const handleSuccess = vi.fn();

    render(MoveLotModal, {
      props: {
        isOpen: true,
        lotId: 'lot-123',
        steps: mockSteps,
        initialFromStepId: 's1',
        initialQuantity: 100,
        onSuccess: handleSuccess
      }
    });

    expect(screen.getByText('Move Work-in-Progress (WIP)')).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /record movement/i });
    await fireEvent.click(submitBtn);

    expect(api.moveLot).toHaveBeenCalledWith(expect.objectContaining({
      productionLotId: 'lot-123',
      fromStepId: 's1',
      toStepId: 's2',
      quantity: 100,
      referenceNumber: null
    }));

    expect(handleSuccess).toHaveBeenCalledTimes(1);
  });

  it('submits WIP transfer with user provided reference number', async () => {
    vi.mocked(api.moveLot).mockResolvedValue({ id: 'mov-2' });
    const handleSuccess = vi.fn();

    render(MoveLotModal, {
      props: {
        isOpen: true,
        lotId: 'lot-123',
        steps: mockSteps,
        initialFromStepId: 's1',
        initialQuantity: 100,
        onSuccess: handleSuccess
      }
    });

    const refInput = screen.getByLabelText(/reference number/i);
    await fireEvent.input(refInput, { target: { value: 'REF-TEST-999' } });

    const submitBtn = screen.getByRole('button', { name: /record movement/i });
    await fireEvent.click(submitBtn);

    expect(api.moveLot).toHaveBeenCalledWith(expect.objectContaining({
      productionLotId: 'lot-123',
      fromStepId: 's1',
      toStepId: 's2',
      quantity: 100,
      referenceNumber: 'REF-TEST-999'
    }));

    expect(handleSuccess).toHaveBeenCalledTimes(1);
  });
});
