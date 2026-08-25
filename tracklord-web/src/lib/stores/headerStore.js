import { writable } from 'svelte/store';

/**
 * @typedef {Object} HeaderAction
 * @property {string} label
 * @property {string} [icon]
 * @property {() => void} [onclick]
 * @property {'primary' | 'outline' | 'ghost' | 'danger'} [variant]
 * @property {boolean} [disabled]
 * @property {Array<{ label: string, icon?: string, onclick: () => void, danger?: boolean }>} [dropdownItems]
 */

/**
 * @typedef {Object} HeaderConfig
 * @property {string} [title]
 * @property {string} [subtitle]
 * @property {string} [icon]
 * @property {Array<HeaderAction>} [actions]
 */

function createHeaderStore() {
  const { subscribe, set, update } = writable(/** @type {HeaderConfig} */ ({
    title: '',
    subtitle: '',
    icon: '',
    actions: []
  }));

  return {
    subscribe,
    /** @param {HeaderConfig} config */
    setHeader: (config) => set(config),
    /** @param {Array<HeaderAction>} actions */
    setActions: (actions) => update(s => ({ ...s, actions })),
    reset: () => set({ title: '', subtitle: '', icon: '', actions: [] })
  };
}

export const headerStore = createHeaderStore();
