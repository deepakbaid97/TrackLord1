import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import MaterialTraceability from './MaterialTraceability.svelte';

describe('MaterialTraceability component', () => {
  it('renders empty message when no consumptions exist', () => {
    render(MaterialTraceability, {
      props: {
        traceability: { consumptions: [] }
      }
    });

    expect(screen.getByText('No raw material consumptions recorded for this lot yet.')).toBeInTheDocument();
  });

  it('renders material cards with total cost and allows editing', async () => {
    const handleEdit = vi.fn();
    const handleLog = vi.fn();

    const traceability = {
      totalMaterialCost: 5000,
      consumptions: [
        {
          id: 'c1',
          materialLotNumber: 'FAB-001',
          materialName: 'Denim Fabric',
          quantityConsumed: 50,
          unit: 'Meters',
          unitRate: 100,
          totalPrice: 5000
        }
      ]
    };

    render(MaterialTraceability, {
      props: {
        traceability,
        onLogMaterialClick: handleLog,
        onEditConsumptionClick: handleEdit
      }
    });

    expect(screen.getByText('FAB-001')).toBeInTheDocument();
    expect(screen.getByText('Denim Fabric')).toBeInTheDocument();
    expect(screen.getByText('50 Meters')).toBeInTheDocument();
    expect(screen.getAllByText('₹5,000.00').length).toBe(2);

    const editBtn = screen.getByTitle('Edit Material Consumption');
    await fireEvent.click(editBtn);
    expect(handleEdit).toHaveBeenCalledWith(traceability.consumptions[0]);

    const logBtn = screen.getByRole('button', { name: /log material/i });
    await fireEvent.click(logBtn);
    expect(handleLog).toHaveBeenCalledTimes(1);
  });
});
