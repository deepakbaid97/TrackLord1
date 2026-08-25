import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import EmptyState from './EmptyState.svelte';

describe('EmptyState component', () => {
  it('renders default icon, title and message', () => {
    render(EmptyState);

    expect(screen.getByText('search_off')).toBeInTheDocument();
    expect(screen.getByText('No items found')).toBeInTheDocument();
    expect(screen.getByText('Try adjusting your search query or filters.')).toBeInTheDocument();
  });

  it('renders custom props when provided', () => {
    render(EmptyState, {
      props: {
        icon: 'inventory_2',
        title: 'No production lots',
        message: 'Create your first production lot to get started.'
      }
    });

    expect(screen.getByText('inventory_2')).toBeInTheDocument();
    expect(screen.getByText('No production lots')).toBeInTheDocument();
    expect(screen.getByText('Create your first production lot to get started.')).toBeInTheDocument();
  });

  it('hides message element if message is empty string', () => {
    render(EmptyState, {
      props: {
        title: 'Empty List',
        message: ''
      }
    });

    expect(screen.getByText('Empty List')).toBeInTheDocument();
    expect(screen.queryByText('Try adjusting your search query or filters.')).not.toBeInTheDocument();
  });
});
