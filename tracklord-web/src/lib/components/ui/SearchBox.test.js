import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import SearchBox from './SearchBox.svelte';

describe('SearchBox component', () => {
  it('renders search input with placeholder', () => {
    render(SearchBox, {
      props: {
        placeholder: 'Search lots...'
      }
    });

    expect(screen.getByPlaceholderText('Search lots...')).toBeInTheDocument();
    expect(screen.getByText('search')).toBeInTheDocument();
  });

  it('shows clear button only when value is non-empty and clicking clears it', async () => {
    let value = 'LOT-001';
    render(SearchBox, {
      props: {
        value,
        placeholder: 'Search...'
      }
    });

    const clearBtn = screen.getByTitle('Clear search');
    expect(clearBtn).toBeInTheDocument();

    await fireEvent.click(clearBtn);
    expect(screen.queryByTitle('Clear search')).not.toBeInTheDocument();
  });
});
