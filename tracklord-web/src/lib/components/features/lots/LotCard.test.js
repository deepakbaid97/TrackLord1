import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import LotCard from './LotCard.svelte';

describe('LotCard component', () => {
  const mockSteps = [
    { id: 's1', code: 'CUT', name: 'Cutting', isTerminating: false },
    { id: 's2', code: 'PACK', name: 'Packing', isTerminating: true }
  ];

  it('renders lot number, client badge, and total quantity when selectedStepId is ALL', () => {
    const lot = {
      id: 'lot-1',
      lotNumber: 'LOT-2026-001',
      clientNamesDisplay: 'Acme Garments',
      totalWipQty: 500,
      activeSteps: [
        { stepId: 's1', stepCode: 'CUT', stepName: 'Cutting', quantity: 300 },
        { stepId: 's2', stepCode: 'PACK', stepName: 'Packing', quantity: 200 }
      ]
    };

    render(LotCard, {
      props: { lot, steps: mockSteps, selectedStepId: 'ALL' }
    });

    expect(screen.getByText('LOT-2026-001')).toBeInTheDocument();
    expect(screen.getByText('• Acme Garments')).toBeInTheDocument();
    expect(screen.getByText('500 Pcs')).toBeInTheDocument();
    expect(screen.getByText(/Cutting: 300 Pcs/i)).toBeInTheDocument();
    expect(screen.getByText(/Packing: 200 Pcs/i)).toBeInTheDocument();
    expect(screen.getByText('(Terminated)')).toBeInTheDocument();
  });

  it('filters displayed quantity when specific selectedStepId is selected', () => {
    const lot = {
      id: 'lot-2',
      lotNumber: 'LOT-2026-002',
      totalWipQty: 500,
      activeSteps: [
        { stepId: 's1', stepCode: 'CUT', stepName: 'Cutting', quantity: 300 },
        { stepId: 's2', stepCode: 'PACK', stepName: 'Packing', quantity: 200 }
      ]
    };

    render(LotCard, {
      props: { lot, steps: mockSteps, selectedStepId: 's1' }
    });

    expect(screen.getByText('300 Pcs')).toBeInTheDocument();
  });

  it('navigates to /inventory?lot={id} on click', async () => {
    const lot = {
      id: 'lot-nav',
      lotNumber: 'LOT-NAV',
      totalWipQty: 100
    };

    render(LotCard, {
      props: { lot, steps: mockSteps }
    });

    const card = screen.getByRole('button', { name: /view lot lot-nav details/i });
    await fireEvent.click(card);

    expect(window.location.href).toBe('/inventory?lot=lot-nav');
  });
});
