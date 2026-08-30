import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/svelte';
import Select from './Select.svelte';

describe('Select component', () => {
  const options = [
    { label: 'Cutting (CUT)', value: 'step-1' },
    { label: 'Stitching (STIT)', value: 'step-2' },
    { label: 'Washing (WASH)', value: 'step-3' },
    { label: 'Finishing (FIN)', value: 'step-4' }
  ];

  it('renders with placeholder and selected label', () => {
    const { rerender } = render(Select, {
      props: {
        label: 'Stage',
        placeholder: 'Select stage...',
        options,
        value: ''
      }
    });

    expect(screen.getByText('Stage')).toBeInTheDocument();
    expect(screen.getByText('Select stage...')).toBeInTheDocument();

    rerender({
      label: 'Stage',
      placeholder: 'Select stage...',
      options,
      value: 'step-3'
    });

    expect(screen.getByText('Washing (WASH)')).toBeInTheDocument();
  });

  it('allows cycling options with ArrowDown and ArrowUp when closed', async () => {
    let currentValue = 'step-1';
    const handleChange = vi.fn((val) => {
      currentValue = val;
    });

    render(Select, {
      props: {
        options,
        value: currentValue,
        onchange: handleChange
      }
    });

    const button = screen.getByRole('combobox');
    button.focus();

    // Arrow down -> should select Stitching (step-2)
    await fireEvent.keyDown(button, { key: 'ArrowDown' });
    expect(handleChange).toHaveBeenCalledWith('step-2');

    // Arrow down again -> should select Washing (step-3)
    await fireEvent.keyDown(button, { key: 'ArrowDown' });
    expect(handleChange).toHaveBeenCalledWith('step-3');

    // Arrow up -> should select Stitching (step-2)
    await fireEvent.keyDown(button, { key: 'ArrowUp' });
    expect(handleChange).toHaveBeenCalledWith('step-2');
  });

  it('supports typeahead search without opening dropdown', async () => {
    let currentValue = 'step-1';
    const handleChange = vi.fn((val) => {
      currentValue = val;
    });

    render(Select, {
      props: {
        options,
        value: currentValue,
        onchange: handleChange
      }
    });

    const button = screen.getByRole('combobox');
    button.focus();

    // Type 'w' -> should match 'Washing (WASH)' (step-3)
    await fireEvent.keyDown(button, { key: 'w' });
    expect(handleChange).toHaveBeenCalledWith('step-3');
    expect(screen.getByText('Washing (WASH)')).toBeInTheDocument();

    // Type 'f' -> should match 'Finishing (FIN)' (step-4)
    await fireEvent.keyDown(button, { key: 'f' });
    expect(handleChange).toHaveBeenCalledWith('step-4');
    expect(screen.getByText('Finishing (FIN)')).toBeInTheDocument();
  });

  it('opens dropdown and allows searching and selecting with Enter', async () => {
    let currentValue = '';
    const handleChange = vi.fn((val) => {
      currentValue = val;
    });

    render(Select, {
      props: {
        options,
        value: currentValue,
        searchable: true,
        onchange: handleChange
      }
    });

    const button = screen.getByRole('combobox');
    
    // Press Enter to open
    await fireEvent.keyDown(button, { key: 'Enter' });
    expect(button).toHaveAttribute('aria-expanded', 'true');

    const searchInput = screen.getByPlaceholderText('Search...');
    expect(searchInput).toBeInTheDocument();

    // Filter for 'wash'
    await fireEvent.input(searchInput, { target: { value: 'wash' } });

    // Press Enter in search input -> should select Washing (step-3)
    await fireEvent.keyDown(searchInput, { key: 'Enter' });
    expect(handleChange).toHaveBeenCalledWith('step-3');
  });

  it('navigates search results with ArrowDown/ArrowUp and selects with Enter', async () => {
    let currentValue = '';
    const handleChange = vi.fn((val) => {
      currentValue = val;
    });

    render(Select, {
      props: {
        options,
        value: currentValue,
        searchable: true,
        onchange: handleChange
      }
    });

    const button = screen.getByRole('combobox');
    await fireEvent.click(button);

    const searchInput = screen.getByPlaceholderText('Search...');

    // Arrow down to highlight second item (Stitching)
    await fireEvent.keyDown(searchInput, { key: 'ArrowDown' });
    // Press Enter
    await fireEvent.keyDown(searchInput, { key: 'Enter' });

    expect(handleChange).toHaveBeenCalledWith('step-2');
  });

  it('closes dropdown when Escape is pressed', async () => {
    render(Select, {
      props: {
        options,
        value: ''
      }
    });

    const button = screen.getByRole('combobox');
    await fireEvent.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');

    await fireEvent.keyDown(window, { key: 'Escape' });
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });
});
