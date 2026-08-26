import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import PwaInstallPrompt from './PwaInstallPrompt.svelte';
import { pwaStore, openInstallModal, dismissInstallModal } from '../../stores/pwaStore.js';

describe('PwaInstallPrompt Component', () => {
  beforeEach(() => {
    dismissInstallModal();
  });

  it('does not display install modal when showInstallModal is false', () => {
    render(PwaInstallPrompt);
    expect(screen.queryByText(/Install TrackLord App/i)).not.toBeInTheDocument();
  });

  it('displays rich install modal when showInstallModal is true', async () => {
    render(PwaInstallPrompt);
    openInstallModal();

    // Svelte store reactivity updates UI
    const heading = await screen.findByText(/Install TrackLord App/i);
    expect(heading).toBeInTheDocument();
    expect(screen.getByText(/Instant Launch & Speed/i)).toBeInTheDocument();
    expect(screen.getByText(/Offline Resilience/i)).toBeInTheDocument();
  });
});
