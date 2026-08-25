import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import KpiStrip from './KpiStrip.svelte';

describe('KpiStrip component', () => {
  it('renders all KPI items with labels and values', () => {
    const items = [
      { label: 'Active WIP', value: '1,250 Pcs', icon: 'pending_actions', color: 'text-primary' },
      { label: 'Completed', value: '450 Pcs', icon: 'check_circle', color: 'text-emerald-600' }
    ];

    render(KpiStrip, {
      props: { items, columns: 2 }
    });

    expect(screen.getByText('Active WIP')).toBeInTheDocument();
    expect(screen.getByText('1,250 Pcs')).toBeInTheDocument();
    expect(screen.getByText('pending_actions')).toBeInTheDocument();

    expect(screen.getByText('Completed')).toBeInTheDocument();
    expect(screen.getByText('450 Pcs')).toBeInTheDocument();
    expect(screen.getByText('check_circle')).toBeInTheDocument();
  });

  it('handles empty items array gracefully without error', () => {
    const { container } = render(KpiStrip, {
      props: { items: [] }
    });

    expect(container.querySelector('section')).toBeInTheDocument();
  });
});
