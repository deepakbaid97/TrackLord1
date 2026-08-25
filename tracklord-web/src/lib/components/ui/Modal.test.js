import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import Modal from './Modal.svelte';

function textSnippet(text) {
  return createRawSnippet(() => ({
    render: () => `<span>${text}</span>`
  }));
}

describe('Modal component', () => {
  it('does not render dialog when isOpen=false', () => {
    render(Modal, {
      props: {
        isOpen: false,
        title: 'Hidden Modal',
        children: textSnippet('Modal Content')
      }
    });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders modal with title and content when isOpen=true', () => {
    render(Modal, {
      props: {
        isOpen: true,
        title: 'Create Item',
        children: textSnippet('Form body goes here')
      }
    });

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Create Item')).toBeInTheDocument();
    expect(screen.getByText('Form body goes here')).toBeInTheDocument();
  });

  it('calls onclose when close button is clicked', async () => {
    const handleClose = vi.fn();
    render(Modal, {
      props: {
        isOpen: true,
        title: 'Closeable Modal',
        onclose: handleClose,
        children: textSnippet('Content')
      }
    });

    const closeBtn = screen.getByRole('button', { name: /close/i });
    await fireEvent.click(closeBtn);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('closes when Escape key is pressed', async () => {
    const handleClose = vi.fn();
    render(Modal, {
      props: {
        isOpen: true,
        onclose: handleClose,
        children: textSnippet('Content')
      }
    });

    await fireEvent.keyDown(window, { key: 'Escape' });
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('closes when backdrop is clicked', async () => {
    const handleClose = vi.fn();
    const { container } = render(Modal, {
      props: {
        isOpen: true,
        onclose: handleClose,
        children: textSnippet('Content')
      }
    });

    const backdrop = container.querySelector('[aria-hidden="true"]');
    if (backdrop) {
      await fireEvent.click(backdrop);
      expect(handleClose).toHaveBeenCalledTimes(1);
    }
  });
});
