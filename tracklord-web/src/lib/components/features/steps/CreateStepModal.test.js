import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import CreateStepModal from './CreateStepModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  createManufacturingStep: vi.fn()
}));

describe('CreateStepModal component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('validates required fields and uppercases step code on submit', async () => {
    vi.mocked(api.createManufacturingStep).mockResolvedValue({ id: 'step-1' });
    const handleSuccess = vi.fn();

    render(CreateStepModal, {
      props: {
        isOpen: true,
        defaultSequence: 3,
        onSuccess: handleSuccess
      }
    });

    const codeInput = screen.getByPlaceholderText('e.g. CUT, STITCH, PACK');
    const nameInput = screen.getByPlaceholderText('e.g. Fabric Cutting & Bundling');

    await fireEvent.input(codeInput, { target: { value: 'iron' } });
    await fireEvent.input(nameInput, { target: { value: 'Ironing & Finishing' } });

    const submitBtn = screen.getByRole('button', { name: /create step/i });
    await fireEvent.click(submitBtn);

    expect(api.createManufacturingStep).toHaveBeenCalledWith({
      code: 'IRON',
      name: 'Ironing & Finishing',
      sequenceOrder: 3,
      isTerminating: false
    });

    expect(handleSuccess).toHaveBeenCalledWith('IRON');
  });

  it('shows validation error if code or name is missing', async () => {
    const { container } = render(CreateStepModal, {
      props: {
        isOpen: true
      }
    });

    const form = container.querySelector('form');
    if (form) await fireEvent.submit(form);

    expect(screen.getByText('Both Step Code and Step Name are required.')).toBeInTheDocument();
    expect(api.createManufacturingStep).not.toHaveBeenCalled();
  });
});
