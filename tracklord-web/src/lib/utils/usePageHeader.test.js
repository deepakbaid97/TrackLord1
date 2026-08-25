import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import { headerStore } from '../stores/headerStore.js';
import { usePageHeader } from './usePageHeader.svelte.js';
import { render } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';

describe('usePageHeader utility', () => {
  beforeEach(() => {
    headerStore.reset();
  });

  it('exports usePageHeader function', () => {
    expect(typeof usePageHeader).toBe('function');
  });
});
