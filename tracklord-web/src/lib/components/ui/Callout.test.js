import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import Callout from './Callout.svelte';

describe('Callout component', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders nothing when message is empty', () => {
    render(Callout, {
      props: {
        message: ''
      }
    });

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders error alert with high z-index (z-[120]) above modals when message is present', () => {
    render(Callout, {
      props: {
        message: 'HTTP Error 500: Internal Server Error',
        type: 'error'
      }
    });

    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(alert.className).toContain('z-[120]');
    expect(alert.className).toContain('bg-red-50/95');
    expect(screen.getByText('HTTP Error 500: Internal Server Error')).toBeInTheDocument();
    expect(screen.getByText('error')).toBeInTheDocument();
  });

  it('renders success, warning, and info alert styles correctly', () => {
    const { unmount } = render(Callout, {
      props: {
        message: 'Operation successful!',
        type: 'success'
      }
    });

    expect(screen.getByRole('alert').className).toContain('bg-emerald-50/95');
    expect(screen.getByText('check_circle')).toBeInTheDocument();
    unmount();

    render(Callout, {
      props: {
        message: 'Caution needed',
        type: 'warning'
      }
    });

    expect(screen.getByRole('alert').className).toContain('bg-amber-50/95');
    expect(screen.getByText('warning')).toBeInTheDocument();
  });

  it('dismisses and calls onclose when close button is clicked', async () => {
    const handleClose = vi.fn();
    render(Callout, {
      props: {
        message: 'Failed to process request',
        type: 'error',
        onclose: handleClose
      }
    });

    const closeButton = screen.getByTitle('Dismiss notification');
    expect(closeButton).toBeInTheDocument();

    await fireEvent.click(closeButton);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('calls onclose after duration timeout', () => {
    const handleClose = vi.fn();
    render(Callout, {
      props: {
        message: 'Auto dismiss message',
        duration: 3000,
        onclose: handleClose
      }
    });

    expect(screen.getByRole('alert')).toBeInTheDocument();

    vi.advanceTimersByTime(3000);

    expect(handleClose).toHaveBeenCalledTimes(1);
  });
});
