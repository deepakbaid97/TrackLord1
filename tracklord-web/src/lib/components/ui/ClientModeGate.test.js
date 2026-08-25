import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import ClientModeGate from './ClientModeGate.svelte';
import { tenantConfigStore } from '../../stores/tenantConfigStore.js';

function textSnippet(text) {
  return createRawSnippet(() => ({
    render: () => `<span>${text}</span>`
  }));
}

describe('ClientModeGate component', () => {
  beforeEach(() => {
    tenantConfigStore.resetToDefaults();
  });

  it('renders children when mode="lot" matches clientMode="lot"', () => {
    tenantConfigStore.setClientMode('lot');

    render(ClientModeGate, {
      props: {
        mode: 'lot',
        children: textSnippet('Lot Mode Client Field')
      }
    });

    expect(screen.getByText('Lot Mode Client Field')).toBeInTheDocument();
  });

  it('hides children and renders fallback when mode does not match', () => {
    tenantConfigStore.setClientMode('step');

    render(ClientModeGate, {
      props: {
        mode: 'lot',
        children: textSnippet('Lot Mode Only'),
        fallback: textSnippet('Not In Lot Mode')
      }
    });

    expect(screen.queryByText('Lot Mode Only')).not.toBeInTheDocument();
    expect(screen.getByText('Not In Lot Mode')).toBeInTheDocument();
  });

  it('renders children for mode="any" when clientMode is "lot" or "step"', () => {
    tenantConfigStore.setClientMode('lot');

    render(ClientModeGate, {
      props: {
        mode: 'any',
        children: textSnippet('Client Tracking Active')
      }
    });

    expect(screen.getByText('Client Tracking Active')).toBeInTheDocument();
  });

  it('hides children for mode="any" when clientMode is "none"', () => {
    tenantConfigStore.setClientMode('none');

    render(ClientModeGate, {
      props: {
        mode: 'any',
        children: textSnippet('Client Tracking Active')
      }
    });

    expect(screen.queryByText('Client Tracking Active')).not.toBeInTheDocument();
  });
});
