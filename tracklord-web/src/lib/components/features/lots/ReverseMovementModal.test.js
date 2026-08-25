import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import ReverseMovementModal from './ReverseMovementModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  reverseMovement: vi.fn()
}));

describe('ReverseMovementModal component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders movement summary and confirms reversal', async () => {
    vi.mocked(api.reverseMovement).mockResolvedValue({ success: true });
    const handleSuccess = vi.fn();

    const mockMovement = {
      id: 'mov-99',
      from: 'Cutting',
      to: 'Stitching',
      qty: '50 Pcs',
      referenceNumber: 'REF-123'
    };

    render(ReverseMovementModal, {
      props: {
        isOpen: true,
        movement: mockMovement,
        onSuccess: handleSuccess
      }
    });

    expect(screen.getByText('Reverse Movement Transaction')).toBeInTheDocument();
    expect(screen.getAllByText('Cutting').length).toBeGreaterThan(0);
    expect(screen.getByText('Stitching')).toBeInTheDocument();
    expect(screen.getByText('50 Pcs')).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /confirm reversal/i });
    await fireEvent.click(submitBtn);

    expect(api.reverseMovement).toHaveBeenCalledWith('mov-99', expect.objectContaining({
      reason: expect.any(String),
      user: 'Supervisor'
    }));

    expect(handleSuccess).toHaveBeenCalledTimes(1);
  });
});
