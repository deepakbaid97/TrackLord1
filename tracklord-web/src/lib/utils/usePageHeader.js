import { onDestroy } from 'svelte';
import { headerStore } from '../stores/headerStore.js';

/**
 * Sets page header configuration with automatic cleanup on component destroy.
 * @param {() => import('../stores/headerStore.js').HeaderConfig} configGetter
 */
export function usePageHeader(configGetter) {
  $effect(() => {
    headerStore.setHeader(configGetter());
  });

  onDestroy(() => {
    headerStore.reset();
  });
}
