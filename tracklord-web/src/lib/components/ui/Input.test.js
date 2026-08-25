import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import Input from './Input.svelte';

describe('Input component', () => {
  it('renders label, input element and placeholder', () => {
    render(Input, {
      props: {
        label: 'Client Name',
        placeholder: 'e.g. Acme Garments',
        id: 'client-name-input'
      }
    });

    expect(screen.getByLabelText(/client name/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText('e.g. Acme Garments')).toBeInTheDocument();
  });

  it('renders required asterisk when required=true', () => {
    render(Input, {
      props: {
        label: 'Lot Number',
        required: true
      }
    });

    expect(screen.getByText('*')).toBeInTheDocument();
  });

  it('renders error message and icon when error is present', () => {
    render(Input, {
      props: {
        label: 'Quantity',
        error: 'Quantity must be greater than zero'
      }
    });

    expect(screen.getByText('Quantity must be greater than zero')).toBeInTheDocument();
    expect(screen.getByText('error')).toBeInTheDocument();
  });

  it('applies disabled attribute when disabled=true', () => {
    render(Input, {
      props: {
        label: 'Locked Field',
        disabled: true
      }
    });

    const input = screen.getByRole('textbox');
    expect(input).toBeDisabled();
  });
});
