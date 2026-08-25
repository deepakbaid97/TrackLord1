import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import ClientCard from './ClientCard.svelte';

describe('ClientCard component', () => {
  it('renders client name, type badge, wip quantity, and active lots count', () => {
    const client = {
      id: 'client-1',
      name: 'Zara Global',
      clientType: 'Buyer',
      totalWipQuantity: 2500,
      activeLotsCount: 3,
      phoneNumber: '+91 99999 88888',
      createdAtUtc: '2026-01-15T10:00:00Z'
    };

    render(ClientCard, {
      props: { client }
    });

    expect(screen.getByText('Zara Global')).toBeInTheDocument();
    expect(screen.getByText('Buyer')).toBeInTheDocument();
    expect(screen.getByText('2,500 Pcs')).toBeInTheDocument();
    expect(screen.getByText(/3 Active Lots/i)).toBeInTheDocument();
    expect(screen.getByText('+91 99999 88888')).toBeInTheDocument();
  });

  it('navigates to /lots?client=id when clicked', async () => {
    const client = {
      id: 'client-2',
      name: 'HM Retail',
      clientType: 'Wholesaler',
      totalWipQuantity: 0,
      activeLotsCount: 1
    };

    render(ClientCard, {
      props: { client }
    });

    const card = screen.getByRole('button', { name: /view lots for hm retail/i });
    await fireEvent.click(card);

    expect(window.location.href).toBe('/lots?client=client-2');
  });
});
