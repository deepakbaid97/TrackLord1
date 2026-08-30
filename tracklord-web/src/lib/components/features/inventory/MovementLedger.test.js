import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import MovementLedger from './MovementLedger.svelte';
import { tenantConfigStore } from '$lib/stores/tenantConfigStore.js';
import { authStore } from '$lib/stores/authStore.js';

describe('MovementLedger component', () => {
  beforeEach(() => {
    tenantConfigStore.resetToDefaults();
    authStore.setUser({
      id: 'admin-1',
      name: 'Admin User',
      role: 'Admin',
      permissions: { movement_reversals: ['write'] }
    }, 'test-token');
  });

  it('renders empty state when movements list is empty', () => {
    render(MovementLedger, {
      props: {
        movements: []
      }
    });

    expect(screen.getByText('No movement history')).toBeInTheDocument();
  });

  it('renders movement list with records and status badges', () => {
    const movements = [
      { id: 'm1', from: 'Cutting', to: 'Stitching', qty: '100 Pcs', date: '2026-02-20', isReversed: false, referenceNumber: 'REF-01', user: 'Op 1' },
      { id: 'm2', from: 'Stitching', to: 'Washing', qty: '50 Pcs', date: '2026-02-21', isReversed: true, referenceNumber: 'REF-02', user: 'Op 2' }
    ];

    render(MovementLedger, {
      props: {
        movements
      }
    });

    expect(screen.getByText('2 records')).toBeInTheDocument();
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Reversed').length).toBeGreaterThan(0);
  });

  it('triggers onReverseClick callback when Reverse button is clicked', async () => {
    const handleReverse = vi.fn();
    const movements = [
      { id: 'm1', from: 'Cutting', to: 'Stitching', qty: '100 Pcs', date: '2026-02-20', isReversed: false, referenceNumber: 'REF-01', user: 'Op 1' }
    ];

    render(MovementLedger, {
      props: {
        movements,
        onReverseClick: handleReverse
      }
    });

    const reverseButtons = screen.getAllByRole('button', { name: /reverse/i });
    await fireEvent.click(reverseButtons[0]);

    expect(handleReverse).toHaveBeenCalledWith(movements[0]);
  });

  it('reveals additional movement details when a movement is clicked', async () => {
    const movements = [
      {
        id: 'm1',
        from: 'Cutting',
        to: 'Stitching',
        qty: '100 Pcs',
        date: '2026-02-20',
        isReversed: false,
        referenceNumber: 'REF-EXP-001',
        user: 'Operator Joe',
        type: 'StepTransfer',
        remarks: 'Transferred for urgent batch',
        clientName: 'Acme Corp'
      }
    ];

    render(MovementLedger, {
      props: {
        movements
      }
    });

    // Before click: Remarks not visible
    expect(screen.queryByText(/Transferred for urgent batch/i)).not.toBeInTheDocument();

    // Click movement to expand
    const cuttingElement = screen.getAllByText('Cutting')[0];
    await fireEvent.click(cuttingElement);

    // After click: Expanded details revealed
    expect(screen.getAllByText(/Transferred for urgent batch/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText('REF-EXP-001').length).toBeGreaterThan(0);
    expect(screen.getAllByText('StepTransfer').length).toBeGreaterThan(0);

    // Click again to collapse
    await fireEvent.click(cuttingElement);
    expect(screen.queryByText(/Transferred for urgent batch/i)).not.toBeInTheDocument();
  });
});
