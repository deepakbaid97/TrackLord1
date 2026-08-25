import { defineConfig } from 'vitest/config';
import { sveltekit } from '@sveltejs/kit/vite';
import { svelteTesting } from '@testing-library/svelte/vite';

export default defineConfig({
  plugins: [
    sveltekit({
      compilerOptions: {
        runes: ({ filename }) =>
          filename.split(/[/\\]/).includes('node_modules') ? undefined : true
      }
    }),
    svelteTesting()
  ],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/lib/test-utils/setup.js'],
    include: ['src/**/*.test.js'],
    server: {
      deps: {
        inline: [/svelte/]
      }
    }
  },
  resolve: {
    conditions: ['browser']
  }
});
