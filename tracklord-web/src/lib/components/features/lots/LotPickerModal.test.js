import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import LotPickerModal from './LotPickerModal.svelte';

describe('LotPickerModal component', () => {
  const mockLots = [
    { id: 'lot-1', lotNumber: 'LOT-ALPHA-100', clientName: 'Alpha Corp', initialQuantity: 500, status: 'Active' },
    { id: 'lot-2', lotNumber: 'LOT-BETA-200', clientName: 'Beta Inc', initialQuantity: 300, status: 'Active' }
  ];

  it('renders lot list and allows search filtering', async () => {
    render(LotPickerModal, {
      props: {
        isOpen: true,
        lots: mockLots,
        selectedLotId: 'lot-1'
      }
    });

    expect(screen.getByText('LOT-ALPHA-100')).toBeInTheDocument();
    expect(screen.getByText('LOT-BETA-200')).toBeInTheDocument();

    const searchInput = screen.getByPlaceholderText('Search by lot number, status, client...');
    await fireEvent.input(searchInput, { target: { value: 'BETA' } });

    expect(screen.queryByText('LOT-ALPHA-100')).not.toBeInTheDocument();
    expect(screen.getByText('LOT-BETA-200')).toBeInTheDocument();
  });

  it('invokes onSelect when lot item is clicked', async () => {
    const handleSelect = vi.fn();

    render(LotPickerModal, {
      props: {
        isOpen: true,
        lots: mockLots,
        onSelect: handleSelect
      }
    });

    const lotBtn = screen.getByText('LOT-ALPHA-100');
    await fireEvent.click(lotBtn);

    expect(handleSelect).toHaveBeenCalledWith('lot-1');
  });
});
