import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import { headerStore } from './headerStore.js';

describe('headerStore', () => {
  beforeEach(() => {
    headerStore.reset();
  });

  it('initializes with default empty config', () => {
    const state = get(headerStore);
    expect(state).toEqual({
      title: '',
      subtitle: '',
      icon: '',
      actions: []
    });
  });

  it('sets full header configuration via setHeader', () => {
    headerStore.setHeader({
      title: 'Production Lots',
      subtitle: 'Active factory batches',
      icon: 'inventory_2',
      actions: [{ label: 'Create Lot' }]
    });

    const state = get(headerStore);
    expect(state.title).toBe('Production Lots');
    expect(state.subtitle).toBe('Active factory batches');
    expect(state.icon).toBe('inventory_2');
    expect(state.actions).toHaveLength(1);
    expect(state.actions[0].label).toBe('Create Lot');
  });

  it('updates actions dynamically via setActions without affecting title/subtitle', () => {
    headerStore.setHeader({
      title: 'Inventory',
      subtitle: 'Materials',
      icon: 'texture',
      actions: []
    });

    headerStore.setActions([{ label: 'Import Stock', variant: 'primary' }]);

    const state = get(headerStore);
    expect(state.title).toBe('Inventory');
    expect(state.subtitle).toBe('Materials');
    expect(state.actions).toHaveLength(1);
    expect(state.actions[0].label).toBe('Import Stock');
  });

  it('resets header to default empty state on reset()', () => {
    headerStore.setHeader({
      title: 'Steps',
      subtitle: 'Pipeline',
      icon: 'alt_route',
      actions: [{ label: 'New Step' }]
    });

    headerStore.reset();

    const state = get(headerStore);
    expect(state.title).toBe('');
    expect(state.subtitle).toBe('');
    expect(state.icon).toBe('');
    expect(state.actions).toEqual([]);
  });
});
