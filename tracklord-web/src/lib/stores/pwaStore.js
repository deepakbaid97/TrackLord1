import { writable, derived } from 'svelte/store';

/**
 * @typedef {Object} PwaState
 * @property {boolean} canInstall - Whether the app can be installed via beforeinstallprompt
 * @property {boolean} isInstalled - Whether running in standalone / PWA window mode
 * @property {boolean} isIOS - Whether on iOS (which uses manual Share -> Add to Home Screen)
 * @property {boolean} isOnline - Real-time network connectivity
 * @property {boolean} updateAvailable - Whether an updated service worker is installed & waiting
 * @property {boolean} showInstallModal - Whether the install dialog is open
 * @property {any} deferredPrompt - The captured BeforeInstallPromptEvent
 */

const initialState = {
  canInstall: false,
  isInstalled: false,
  isIOS: false,
  isOnline: typeof navigator !== 'undefined' ? navigator.onLine : true,
  updateAvailable: false,
  showInstallModal: false,
  deferredPrompt: null
};

const store = writable(initialState);

/**
 * Check if the app is currently running in standalone display mode
 */
function checkIsInstalled() {
  if (typeof window === 'undefined') return false;
  const matchMedia = typeof window.matchMedia === 'function' ? window.matchMedia.bind(window) : null;
  const isStandalone = (matchMedia && matchMedia('(display-mode: standalone)').matches) ||
    (matchMedia && matchMedia('(display-mode: window-controls-overlay)').matches) ||
    /** @type {any} */ (window.navigator)?.standalone === true;
  return Boolean(isStandalone);
}

/**
 * Check if device is iOS (Safari / WebKit)
 */
function checkIsIOS() {
  if (typeof window === 'undefined' || typeof navigator === 'undefined') return false;
  const ua = navigator.userAgent || '';
  return /iPad|iPhone|iPod/.test(ua) && !(/** @type {any} */ (window).MSStream);
}

/**
 * Register Service Worker and initialize PWA event listeners
 */
export function initPwa() {
  if (typeof window === 'undefined') return;

  const isInstalled = checkIsInstalled();
  const isIOS = checkIsIOS();
  const isOnline = typeof navigator !== 'undefined' ? navigator.onLine : true;

  store.update(state => ({
    ...state,
    isInstalled,
    isIOS,
    isOnline
  }));

  // Listen for standalone display changes
  if (typeof window.matchMedia === 'function') {
    const standaloneQuery = window.matchMedia('(display-mode: standalone)');
    if (standaloneQuery && typeof standaloneQuery.addEventListener === 'function') {
      standaloneQuery.addEventListener('change', (e) => {
        store.update(state => ({
          ...state,
          isInstalled: e.matches,
          canInstall: !e.matches && Boolean(state.deferredPrompt)
        }));
      });
    }
  }

  // Listen for online / offline events
  window.addEventListener('online', () => {
    store.update(state => ({ ...state, isOnline: true }));
  });

  window.addEventListener('offline', () => {
    store.update(state => ({ ...state, isOnline: false }));
  });

  // Listen for PWA installation prompt event
  window.addEventListener('beforeinstallprompt', (e) => {
    // Prevent the default mini-infobar or auto-prompt
    e.preventDefault();
    store.update(state => ({
      ...state,
      deferredPrompt: e,
      canInstall: true
    }));
  });

  // Listen for when the app has been successfully installed
  window.addEventListener('appinstalled', () => {
    store.update(state => ({
      ...state,
      canInstall: false,
      deferredPrompt: null,
      isInstalled: true,
      showInstallModal: false
    }));
  });

  // Register the service worker
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/service-worker.js', { scope: '/' })
      .then((registration) => {
        // Check for updates on page load
        registration.addEventListener('updatefound', () => {
          const newWorker = registration.installing;
          if (newWorker) {
            newWorker.addEventListener('statechange', () => {
              if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                store.update(state => ({ ...state, updateAvailable: true }));
              }
            });
          }
        });
      })
      .catch((err) => {
        console.debug('PWA Service Worker registration skipped or failed:', err);
      });

    // Handle controller change (when new worker takes over)
    navigator.serviceWorker.addEventListener('controllerchange', () => {
      // Reload on controller change if needed
    });
  }
}

/**
 * Trigger the native PWA install prompt
 */
export async function installApp() {
  let promptEvent = null;
  store.subscribe(s => promptEvent = s.deferredPrompt)();

  if (promptEvent) {
    try {
      await promptEvent.prompt();
      const choiceResult = await promptEvent.userChoice;
      
      store.update(state => ({
        ...state,
        deferredPrompt: null,
        canInstall: false,
        showInstallModal: false
      }));

      return choiceResult.outcome === 'accepted';
    } catch (err) {
      console.error('Failed to trigger PWA install prompt:', err);
    }
  }
  return false;
}

/**
 * Open the Rich Install / Download Modal
 */
export function openInstallModal() {
  store.update(state => ({ ...state, showInstallModal: true }));
}

/**
 * Dismiss the Rich Install Modal
 */
export function dismissInstallModal() {
  store.update(state => ({ ...state, showInstallModal: false }));
}

/**
 * Apply service worker update and refresh page
 */
export function refreshApp() {
  if (typeof navigator !== 'undefined' && 'serviceWorker' in navigator) {
    navigator.serviceWorker.getRegistration().then((reg) => {
      if (reg && reg.waiting) {
        reg.waiting.postMessage({ type: 'SKIP_WAITING' });
      }
    });
  }
  window.location.reload();
}

export const pwaStore = {
  subscribe: store.subscribe,
  initPwa,
  installApp,
  openInstallModal,
  dismissInstallModal,
  refreshApp
};
