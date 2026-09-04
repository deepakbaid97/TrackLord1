import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/svelte';
import StepLotsPage from './+page.svelte';
import * as api from '$lib/api.js';

vi.mock('$app/stores', () => {
  const { readable } = require('svelte/store');
  return {
    page: readable({
      url: new URL('http://localhost/steps/step-123/lots'),
      params: { id: 'step-123' },
      route: { id: '/steps/[id]/lots' }
    })
  };
});

vi.mock('$app/navigation', () => ({
  goto: vi.fn()
}));

vi.mock('$lib/api.js', () => ({
  getManufacturingSteps: vi.fn(),
  getLotsAtStep: vi.fn()
}));

describe('Step Lots Inspection Page (+page.svelte)', () => {
  const mockSteps = [
    { id: 'step-123', code: 'INWARD', name: 'Inward', sequenceOrder: 1, isTerminating: false },
    { id: 'step-456', code: 'CUT', name: 'Cutting', sequenceOrder: 2, isTerminating: false }
  ];

  const mockLotsResponse = {
    stepId: 'step-123',
    stepCode: 'INWARD',
    stepName: 'Inward',
    totalQuantity: 219,
    totalLots: 5,
    lots: [
      {
        lotId: 'lot-1',
        lotNumber: 'NEW-001',
        quantityAtStep: 10,
        status: 'Active',
        stepCreatedAt: '2026-09-02T11:14:00.000Z',
        clientName: 'Amit Bhai'
      },
      {
        lotId: 'lot-2',
        lotNumber: 'TEst6',
        quantityAtStep: 10,
        status: 'Active',
        stepCreatedAt: '2026-08-30T00:49:00.000Z',
        clientName: 'Denim Jacket'
      },
      {
        lotId: 'lot-3',
        lotNumber: 'somethung',
        quantityAtStep: 99,
        status: 'Active',
        stepCreatedAt: '2026-08-29T17:44:00.000Z',
        clientName: 'Silk Blouse'
      },
      {
        lotId: 'lot-4',
        lotNumber: 'Test3',
        quantityAtStep: 10,
        status: 'Active',
        stepCreatedAt: '2026-08-25T23:34:00.000Z',
        clientName: null
      },
      {
        lotId: 'lot-5',
        lotNumber: 'LOT-002',
        quantityAtStep: 90,
        status: 'Active',
        stepCreatedAt: '2026-08-25T20:33:00.000Z',
        clientName: 'Dharmesh Jeans'
      }
    ]
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.getManufacturingSteps).mockResolvedValue(mockSteps);
    vi.mocked(api.getLotsAtStep).mockResolvedValue(mockLotsResponse);
  });

  it('renders step details, initial lots count and WIP in Current mode', async () => {
    render(StepLotsPage);

    await waitFor(() => {
      expect(screen.getByText(/Lots in Stage/i)).toBeInTheDocument();
      expect(screen.getAllByText('NEW-001').length).toBeGreaterThan(0);
      expect(screen.getAllByText('TEst6').length).toBeGreaterThan(0);
      expect(screen.getAllByText('LOT-002').length).toBeGreaterThan(0);
    });

    expect(api.getLotsAtStep).toHaveBeenCalledWith('step-123', {});
  });

  it('switches to Period mode and calls getLotsAtStep with fromDate and toDate ISO strings', async () => {
    render(StepLotsPage);

    await waitFor(() => {
      expect(screen.getAllByText('NEW-001').length).toBeGreaterThan(0);
    });

    const periodButton = screen.getByRole('button', { name: /^Period$/i });
    await fireEvent.click(periodButton);

    await waitFor(() => {
      expect(api.getLotsAtStep).toHaveBeenCalledTimes(2);
      const lastCallArgs = vi.mocked(api.getLotsAtStep).mock.calls[1];
      expect(lastCallArgs[0]).toBe('step-123');
      expect(lastCallArgs[1].fromDate).toBeDefined();
      expect(lastCallArgs[1].toDate).toBeDefined();
    });
  });

  it('filters out lots whose stepCreatedAt is prior to the fromDate in Period mode', async () => {
    // Return the 5 lots from mock response (2 are from Aug 25, 3 are from Aug 29 - Sep 2)
    render(StepLotsPage);

    await waitFor(() => {
      expect(screen.getAllByText('NEW-001').length).toBeGreaterThan(0);
    });

    // Switch to Period mode
    const periodButton = screen.getByRole('button', { name: /^Period$/i });
    await fireEvent.click(periodButton);

    // Set filter from 2026-08-28 to 2026-09-04
    const dateInputs = document.querySelectorAll('input[type="date"]');
    expect(dateInputs.length).toBe(2);

    await fireEvent.change(dateInputs[0], { target: { value: '2026-08-28' } });
    await fireEvent.change(dateInputs[1], { target: { value: '2026-09-04' } });

    await waitFor(() => {
      // Lots inside period (Aug 29 - Sep 2) must be present
      expect(screen.getAllByText('NEW-001').length).toBeGreaterThan(0);
      expect(screen.getAllByText('TEst6').length).toBeGreaterThan(0);
      expect(screen.getAllByText('somethung').length).toBeGreaterThan(0);

      // Lots from Aug 25 (BEFORE fromDate 2026-08-28) MUST be excluded from the view
      expect(screen.queryAllByText('Test3')).toHaveLength(0);
      expect(screen.queryAllByText('LOT-002')).toHaveLength(0);
    });

    // Verify displayed quantity is 10 + 10 + 99 = 119 Pcs (excluding 10 + 90 = 100 Pcs from Aug 25)
    expect(screen.getByText('Total Quantity: 119 Pcs')).toBeInTheDocument();
  });

  it('switches to By Date mode and sends asOfDate to the API', async () => {
    render(StepLotsPage);

    await waitFor(() => {
      expect(screen.getAllByText('NEW-001').length).toBeGreaterThan(0);
    });

    const byDateButton = screen.getByRole('button', { name: /By Date/i });
    await fireEvent.click(byDateButton);

    await waitFor(() => {
      const lastCallArgs = vi.mocked(api.getLotsAtStep).mock.calls.at(-1);
      expect(lastCallArgs[0]).toBe('step-123');
      expect(lastCallArgs[1].asOfDate).toBeDefined();
    });
  });
});
