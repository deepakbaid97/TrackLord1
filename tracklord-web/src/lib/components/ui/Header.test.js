import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { readable } from 'svelte/store';
import Header from './Header.svelte';
import { headerStore } from '../../stores/headerStore.js';

vi.mock('$app/stores', () => {
  const { readable } = require('svelte/store');
  return {
    page: readable({
      url: new URL('http://localhost/inventory'),
      params: {},
      route: { id: '/inventory' }
    })
  };
});

describe('Header component', () => {
  beforeEach(() => {
    headerStore.reset();
  });

  it('renders default route title and icon when headerStore is empty', () => {
    render(Header);
    expect(screen.getByText('Lot Inventory')).toBeInTheDocument();
    expect(screen.getByText('inventory_2')).toBeInTheDocument();
  });

  it('renders header actions and handles dropdown toggle and item clicks', async () => {
    const handleMove = vi.fn();
    const handleNewLot = vi.fn();
    const handleMaterial = vi.fn();
    const handleConsume = vi.fn();
    const handleRejection = vi.fn();

    headerStore.setHeader({
      title: 'Lot Inventory',
      subtitle: 'Live WIP',
      icon: 'inventory_2',
      actions: [
        {
          label: 'Actions',
          icon: 'bolt',
          variant: 'primary',
          dropdownItems: [
            { label: 'Move Qty', icon: 'move_item', onclick: handleMove },
            { label: 'New Lot', icon: 'add', onclick: handleNewLot },
            { label: 'Material', icon: 'inventory', onclick: handleMaterial },
            { label: 'Consume', icon: 'local_mall', onclick: handleConsume },
            { label: 'Rejection', icon: 'report_problem', danger: true, onclick: handleRejection }
          ]
        }
      ]
    });

    render(Header);

    // Header actions dropdown button is present
    const actionsBtn = screen.getByRole('button', { name: /actions/i });
    expect(actionsBtn).toBeInTheDocument();
    expect(screen.getByText('bolt')).toBeInTheDocument();

    // Dropdown items should not be visible before clicking
    expect(screen.queryByText('Move Qty')).not.toBeInTheDocument();

    // Open dropdown
    await fireEvent.click(actionsBtn);

    // Verify all 5 action buttons are present with their labels and icons
    expect(screen.getByText('Move Qty')).toBeInTheDocument();
    expect(screen.getByText('move_item')).toBeInTheDocument();

    expect(screen.getByText('New Lot')).toBeInTheDocument();
    expect(screen.getByText('add')).toBeInTheDocument();

    expect(screen.getByText('Material')).toBeInTheDocument();
    expect(screen.getByText('inventory')).toBeInTheDocument();

    expect(screen.getByText('Consume')).toBeInTheDocument();
    expect(screen.getByText('local_mall')).toBeInTheDocument();

    expect(screen.getByText('Rejection')).toBeInTheDocument();
    expect(screen.getByText('report_problem')).toBeInTheDocument();

    // Test clicking an item (Move Qty)
    const moveBtn = screen.getByText('Move Qty').closest('button');
    expect(moveBtn).toBeInTheDocument();
    await fireEvent.click(moveBtn);

    expect(handleMove).toHaveBeenCalledTimes(1);

    // Dropdown closes after clicking
    expect(screen.queryByText('Move Qty')).not.toBeInTheDocument();

    // Open dropdown again and click Rejection (danger item)
    await fireEvent.click(actionsBtn);
    const rejectionBtn = screen.getByText('Rejection').closest('button');
    expect(rejectionBtn).toBeInTheDocument();
    expect(rejectionBtn.className).toContain('text-red-600');

    await fireEvent.click(rejectionBtn);
    expect(handleRejection).toHaveBeenCalledTimes(1);
  });
});
