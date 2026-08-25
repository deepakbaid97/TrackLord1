import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import Button from './Button.svelte';

function textSnippet(text) {
  return createRawSnippet(() => ({
    render: () => `<span>${text}</span>`
  }));
}

describe('Button component', () => {
  it('renders with default primary variant and classes', () => {
    render(Button, {
      props: {
        children: textSnippet('Click Me')
      }
    });

    const btn = screen.getByRole('button', { name: /click me/i });
    expect(btn).toBeInTheDocument();
    expect(btn.className).toContain('bg-[#2563eb]');
    expect(btn.type).toBe('button');
  });

  it('renders icon when icon prop is provided', () => {
    render(Button, {
      props: {
        icon: 'add',
        children: textSnippet('Add Item')
      }
    });

    expect(screen.getByText('add')).toHaveClass('material-symbols-outlined');
    expect(screen.getByText('Add Item')).toBeInTheDocument();
  });

  it('applies disabled attribute and prevents clicks when disabled=true', async () => {
    const handleClick = vi.fn();
    render(Button, {
      props: {
        disabled: true,
        onclick: handleClick,
        children: textSnippet('Disabled Action')
      }
    });

    const btn = screen.getByRole('button', { name: /disabled action/i });
    expect(btn).toBeDisabled();

    await fireEvent.click(btn);
    expect(handleClick).not.toHaveBeenCalled();
  });

  it('triggers onclick callback on click when enabled', async () => {
    const handleClick = vi.fn();
    render(Button, {
      props: {
        onclick: handleClick,
        children: textSnippet('Submit')
      }
    });

    const btn = screen.getByRole('button', { name: /submit/i });
    await fireEvent.click(btn);
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('renders danger variant styling', () => {
    render(Button, {
      props: {
        variant: 'danger',
        children: textSnippet('Delete')
      }
    });

    const btn = screen.getByRole('button', { name: /delete/i });
    expect(btn.className).toContain('bg-[#ba1a1a]');
  });
});
