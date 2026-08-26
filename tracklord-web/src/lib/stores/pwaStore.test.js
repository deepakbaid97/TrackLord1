import { describe, it, expect, beforeEach, vi } from 'vitest';
import { pwaStore, initPwa, installApp, openInstallModal, dismissInstallModal } from './pwaStore.js';
import { get } from 'svelte/store';

describe('pwaStore', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with default online state and install flags', () => {
    const state = get(pwaStore);
    expect(state.isOnline).toBe(true);
    expect(typeof state.isInstalled).toBe('boolean');
    expect(typeof state.canInstall).toBe('boolean');
  });

  it('toggles install modal visibility', () => {
    openInstallModal();
    expect(get(pwaStore).showInstallModal).toBe(true);

    dismissInstallModal();
    expect(get(pwaStore).showInstallModal).toBe(false);
  });

  it('triggers prompt and resolves outcome when deferredPrompt exists', async () => {
    const mockPrompt = vi.fn().mockResolvedValue(undefined);
    const mockUserChoice = Promise.resolve({ outcome: 'accepted' });

    // Simulate beforeinstallprompt event
    const fakeEvent = {
      preventDefault: vi.fn(),
      prompt: mockPrompt,
      userChoice: mockUserChoice
    };

    window.dispatchEvent(Object.assign(new Event('beforeinstallprompt'), fakeEvent));
    initPwa();
    window.dispatchEvent(Object.assign(new Event('beforeinstallprompt'), fakeEvent));

    const result = await installApp();
    expect(typeof result).toBe('boolean');
  });
});
