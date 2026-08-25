import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import Badge from './Badge.svelte';

function textSnippet(text) {
  return createRawSnippet(() => ({
    render: () => `<span>${text}</span>`
  }));
}

describe('Badge component', () => {
  it('renders default primary variant and text content', () => {
    render(Badge, {
      props: {
        children: textSnippet('Active Batch')
      }
    });

    const badge = screen.getByText('Active Batch');
    expect(badge).toBeInTheDocument();
    expect(badge.closest('.rounded-full').className).toContain('bg-[#2563eb]');
  });

  it('renders icon and error variant classes', () => {
    render(Badge, {
      props: {
        variant: 'error',
        icon: 'error',
        children: textSnippet('Rejected')
      }
    });

    expect(screen.getByText('error')).toBeInTheDocument();
    const badge = screen.getByText('Rejected');
    expect(badge.closest('.rounded-full').className).toContain('bg-[#ffdad6]');
  });
});
