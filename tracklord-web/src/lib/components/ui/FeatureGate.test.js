import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import FeatureGate from './FeatureGate.svelte';
import { tenantConfigStore } from '../../stores/tenantConfigStore.js';

function textSnippet(text) {
  return createRawSnippet(() => ({
    render: () => `<span>${text}</span>`
  }));
}

describe('FeatureGate component', () => {
  beforeEach(() => {
    tenantConfigStore.resetToDefaults();
  });

  it('renders children when feature is enabled by default', () => {
    render(FeatureGate, {
      props: {
        feature: 'pages.dashboard',
        children: textSnippet('Dashboard Content')
      }
    });

    expect(screen.getByText('Dashboard Content')).toBeInTheDocument();
  });

  it('hides children when feature is overridden to false', () => {
    tenantConfigStore.setFeature('pages.dashboard', false);

    render(FeatureGate, {
      props: {
        feature: 'pages.dashboard',
        children: textSnippet('Dashboard Content')
      }
    });

    expect(screen.queryByText('Dashboard Content')).not.toBeInTheDocument();
  });

  it('renders fallback snippet when feature is disabled', () => {
    tenantConfigStore.setFeature('forms.lots.import', false);

    render(FeatureGate, {
      props: {
        feature: 'forms.lots.import',
        children: textSnippet('Import Form'),
        fallback: textSnippet('Feature Disabled Notice')
      }
    });

    expect(screen.queryByText('Import Form')).not.toBeInTheDocument();
    expect(screen.getByText('Feature Disabled Notice')).toBeInTheDocument();
  });

  it('automatically hides pages.clients when clientMode is "none"', () => {
    tenantConfigStore.setClientMode('none');

    render(FeatureGate, {
      props: {
        feature: 'pages.clients',
        children: textSnippet('Clients Directory')
      }
    });

    expect(screen.queryByText('Clients Directory')).not.toBeInTheDocument();
  });
});
