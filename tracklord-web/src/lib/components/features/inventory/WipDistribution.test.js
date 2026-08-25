import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import WipDistribution from './WipDistribution.svelte';

describe('WipDistribution component', () => {
  const timelineSteps = [
    { id: 's1', name: 'Cutting', code: 'CUT', quantity: 200 },
    { id: 's2', name: 'Stitching', code: 'STITCH', quantity: 100 }
  ];

  it('renders step rows with quantities and total WIP count', () => {
    render(WipDistribution, {
      props: {
        timelineSteps,
        totalLotWip: 300
      }
    });

    expect(screen.getByText('300 Pcs Total')).toBeInTheDocument();
    expect(screen.getByText('Cutting')).toBeInTheDocument();
    expect(screen.getByText('200 Pcs')).toBeInTheDocument();
    expect(screen.getByText('Stitching')).toBeInTheDocument();
    expect(screen.getByText('100 Pcs')).toBeInTheDocument();
  });

  it('renders Move buttons and calls onMoveStep when clicked', async () => {
    const handleMove = vi.fn();

    render(WipDistribution, {
      props: {
        timelineSteps,
        totalLotWip: 300,
        onMoveStep: handleMove
      }
    });

    const moveButtons = screen.getAllByRole('button', { name: /move/i });
    expect(moveButtons).toHaveLength(2);

    await fireEvent.click(moveButtons[0]);
    expect(handleMove).toHaveBeenCalledWith('s1');
  });
});
