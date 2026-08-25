import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import RejectItemsModal from './RejectItemsModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  rejectItems: vi.fn()
}));

describe('RejectItemsModal component', () => {
  const mockSteps = [
    { id: 's1', code: 'CUT', name: 'Cutting' }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('submits rejection reason and quantity', async () => {
    vi.mocked(api.rejectItems).mockResolvedValue({ success: true });
    const handleSuccess = vi.fn();

    render(RejectItemsModal, {
      props: {
        isOpen: true,
        lotId: 'lot-rej-1',
        steps: mockSteps,
        onSuccess: handleSuccess
      }
    });

    expect(screen.getByText('Log Rejection / Rework Transfer')).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /log rejection/i });
    await fireEvent.click(submitBtn);

    expect(api.rejectItems).toHaveBeenCalledWith('lot-rej-1', expect.objectContaining({
      stepId: 's1',
      targetStepId: 's1',
      quantityRejected: 1
    }));

    expect(handleSuccess).toHaveBeenCalledTimes(1);
  });
});
