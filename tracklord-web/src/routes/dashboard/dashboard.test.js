import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/svelte';
import DashboardPage from './+page.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  getDashboardOverview: vi.fn(),
  getMaterialLots: vi.fn()
}));

describe('Dashboard Page (+page.svelte)', () => {
  const mockOverview = {
    totalActiveLots: 3,
    totalActiveQuantity: 1500,
    totalTerminatedQuantity: 200,
    totalClients: 2,
    totalMaterialLots: 1,
    stepSummaries: [
      {
        stepId: 'step-1',
        stepCode: 'CUT',
        stepName: 'Cutting',
        totalWipQuantity: 1500,
        activeLotsCount: 3,
        isActive: true
      }
    ],
    clientSummaries: [],
    activeLotSummaries: [
      {
        lotId: '00000000-0000-0000-0000-000000000001',
        lotNumber: 'LOT-2026-001',
        initialQuantity: 500,
        currentWipQuantity: 500,
        currentStepName: 'Cutting',
        createdAtUtc: '2026-09-01T10:00:00Z',
        clientName: 'Client A'
      },
      {
        lotId: '00000000-0000-0000-0000-000000000002',
        lotNumber: 'LOT-2026-002',
        initialQuantity: 1000,
        currentWipQuantity: 1000,
        currentStepName: 'Cutting',
        createdAtUtc: '2026-09-02T10:00:00Z',
        clientName: 'Client B'
      }
    ]
  };

  beforeEach(() => {
    vi.clearAllMocks();
    api.getDashboardOverview.mockResolvedValue(mockOverview);
    api.getMaterialLots.mockResolvedValue([]);
    delete window.location;
    window.location = { href: 'http://localhost/dashboard' };
  });

  it('renders active production lots and links to specific lot inventory with ?lot= query parameter', async () => {
    render(DashboardPage);

    await waitFor(() => {
      expect(screen.getByText('LOT-2026-001')).toBeInTheDocument();
      expect(screen.getByText('LOT-2026-002')).toBeInTheDocument();
    });

    const lot1Link = screen.getByRole('link', { name: /LOT-2026-001/i });
    expect(lot1Link).toBeInTheDocument();
    expect(lot1Link.getAttribute('href')).toBe('/inventory?lot=00000000-0000-0000-0000-000000000001');

    const lot2Link = screen.getByRole('link', { name: /LOT-2026-002/i });
    expect(lot2Link).toBeInTheDocument();
    expect(lot2Link.getAttribute('href')).toBe('/inventory?lot=00000000-0000-0000-0000-000000000002');
  });

  it('navigates to specific lot inventory when clicking the table row', async () => {
    render(DashboardPage);

    await waitFor(() => {
      expect(screen.getByText('LOT-2026-001')).toBeInTheDocument();
    });

    const row = screen.getByText('LOT-2026-001').closest('tr');
    expect(row).toBeInTheDocument();

    await fireEvent.click(row);
    expect(window.location.href).toBe('/inventory?lot=00000000-0000-0000-0000-000000000001');
  });

  it('renders WIP by step and links to /steps/[id]/lots', async () => {
    render(DashboardPage);

    await waitFor(() => {
      expect(screen.getByTitle('View lots in Cutting')).toBeInTheDocument();
    });

    const stepLink = screen.getByTitle('View lots in Cutting');
    expect(stepLink.getAttribute('href')).toBe('/steps/step-1/lots');
  });
});
