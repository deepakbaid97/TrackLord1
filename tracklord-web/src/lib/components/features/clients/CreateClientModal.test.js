import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import CreateClientModal from './CreateClientModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  createClient: vi.fn()
}));

describe('CreateClientModal component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders inputs and client type options when isOpen=true', () => {
    render(CreateClientModal, {
      props: {
        isOpen: true
      }
    });

    expect(screen.getByText('Register New Client')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('e.g. Apex Textiles Ltd.')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('e.g. +91 98765 43210')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save client/i })).toBeInTheDocument();
  });

  it('submits valid form data, calls createClient and invokes onSuccess callback', async () => {
    vi.mocked(api.createClient).mockResolvedValue({ id: 'client-101' });
    const handleSuccess = vi.fn();

    render(CreateClientModal, {
      props: {
        isOpen: true,
        onSuccess: handleSuccess
      }
    });

    const nameInput = screen.getByPlaceholderText('e.g. Apex Textiles Ltd.');
    const phoneInput = screen.getByPlaceholderText('e.g. +91 98765 43210');

    await fireEvent.input(nameInput, { target: { value: '  Apex Textiles  ' } });
    await fireEvent.input(phoneInput, { target: { value: ' +91 9876543210 ' } });

    const submitBtn = screen.getByRole('button', { name: /save client/i });
    await fireEvent.click(submitBtn);

    expect(api.createClient).toHaveBeenCalledWith({
      name: 'Apex Textiles',
      phoneNumber: '+91 9876543210',
      clientType: 'Buyer'
    });

    expect(handleSuccess).toHaveBeenCalledWith(expect.objectContaining({
      id: 'client-101',
      name: 'Apex Textiles'
    }));
  });

  it('invokes onError callback when createClient fails', async () => {
    vi.mocked(api.createClient).mockRejectedValue(new Error('Duplicate client code'));
    const handleError = vi.fn();

    render(CreateClientModal, {
      props: {
        isOpen: true,
        onError: handleError
      }
    });

    const nameInput = screen.getByPlaceholderText('e.g. Apex Textiles Ltd.');
    await fireEvent.input(nameInput, { target: { value: 'Duplicate Client' } });

    const submitBtn = screen.getByRole('button', { name: /save client/i });
    await fireEvent.click(submitBtn);

    expect(handleError).toHaveBeenCalledTimes(1);
  });
});
