import { describe, it, expect } from 'vitest';
import { CLIENT_MODES, CONFIG_SCHEMA } from './configRegistry.js';

describe('configRegistry.js', () => {
  it('contains exactly 3 client tracking modes with required metadata', () => {
    expect(CLIENT_MODES).toHaveLength(3);
    const modeIds = CLIENT_MODES.map(m => m.id);
    expect(modeIds).toEqual(['lot', 'step', 'none']);

    CLIENT_MODES.forEach(mode => {
      expect(mode.name).toBeTruthy();
      expect(mode.icon).toBeTruthy();
      expect(mode.tag).toBeTruthy();
      expect(mode.description).toBeTruthy();
    });
  });

  it('defines 3 configuration categories in CONFIG_SCHEMA', () => {
    expect(CONFIG_SCHEMA).toHaveLength(3);
    const categoryIds = CONFIG_SCHEMA.map(c => c.categoryId);
    expect(categoryIds).toEqual(['pages', 'forms', 'fields']);
  });

  it('guarantees unique keys across all schema configuration items', () => {
    const allKeys = CONFIG_SCHEMA.flatMap(cat => cat.items.map(item => item.key));
    const uniqueKeys = new Set(allKeys);
    expect(uniqueKeys.size).toBe(allKeys.length);
  });

  it('every config item has key, label, description, and default boolean', () => {
    CONFIG_SCHEMA.forEach(category => {
      expect(category.categoryName).toBeTruthy();
      expect(category.icon).toBeTruthy();
      expect(Array.isArray(category.items)).toBe(true);

      category.items.forEach(item => {
        expect(typeof item.key).toBe('string');
        expect(item.key.length).toBeGreaterThan(0);
        expect(typeof item.label).toBe('string');
        expect(typeof item.description).toBe('string');
        expect(typeof item.default).toBe('boolean');
      });
    });
  });

  it('contains expected navigation pages in pages category', () => {
    const pagesCategory = CONFIG_SCHEMA.find(c => c.categoryId === 'pages');
    expect(pagesCategory).toBeDefined();
    const pageKeys = pagesCategory.items.map(i => i.key);
    expect(pageKeys).toContain('pages.dashboard');
    expect(pageKeys).toContain('pages.inventory');
    expect(pageKeys).toContain('pages.lots');
    expect(pageKeys).toContain('pages.steps');
    expect(pageKeys).toContain('pages.materials');
    expect(pageKeys).toContain('pages.clients');
    expect(pageKeys).toContain('pages.users');
  });
});
