import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	server: {
		// Firebase Phone Auth + reCAPTCHA Enterprise reject "localhost" for real numbers.
		// Use http://127.0.0.1:5173 and add 127.0.0.1 to Firebase Authorized Domains.
		host: '127.0.0.1',
		proxy: {
			'/api': 'http://localhost:5217',
			'/voice-api': {
				target: 'http://localhost:8000',
				changeOrigin: true,
				rewrite: (path) => path.replace(/^\/voice-api/, '')
			}
		}
	},
	plugins: [
		sveltekit({
			compilerOptions: {
				// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},

			adapter: adapter({
				pages: '../TrackLord.Api/wwwroot',
				assets: '../TrackLord.Api/wwwroot',
				fallback: 'index.html',
				precompress: false,
				strict: false
			})
		})
	]
});
