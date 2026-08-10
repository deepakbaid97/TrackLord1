import { initializeApp, getApps, getApp } from 'firebase/app';
import { getAuth, RecaptchaVerifier, signInWithPhoneNumber } from 'firebase/auth';

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY || 'AIzaSyDemoTrackLordApiKey2026FirebaseApp',
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN || 'tracklord-app.firebaseapp.com',
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID || 'tracklord-app',
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET || 'tracklord-app.appspot.com',
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID || '123456789012',
  appId: import.meta.env.VITE_FIREBASE_APP_ID || '1:123456789012:web:abcdef123456'
};

// Initialize Firebase App
const app = getApps().length > 0 ? getApp() : initializeApp(firebaseConfig);
export const auth = getAuth(app);

/**
 * Gets or creates the singleton RecaptchaVerifier on a DOM element.
 * Reuses existing verifier to prevent "reCAPTCHA has already been rendered" errors.
 * @param {string} [elementId] - ID of the container element
 * @returns {Promise<RecaptchaVerifier|null>}
 */
export async function getRecaptchaVerifier(elementId = 'recaptcha-container') {
  if (typeof window === 'undefined') return null;

  if (window.recaptchaVerifier) {
    return window.recaptchaVerifier;
  }

  const container = document.getElementById(elementId);
  if (!container) {
    console.warn(`reCAPTCHA container #${elementId} not found in DOM.`);
    return null;
  }

  // Clear container DOM contents to prevent duplicate widget errors
  container.innerHTML = '';

  const verifier = new RecaptchaVerifier(auth, container, {
    size: 'invisible',
    callback: () => {},
    'expired-callback': () => {
      console.warn('reCAPTCHA expired. Please request OTP again.');
    }
  });

  try {
    await verifier.render();
  } catch (e) {
    console.warn('Recaptcha render warning:', e);
  }

  window.recaptchaVerifier = verifier;
  return verifier;
}

/**
 * Clears and resets the recaptcha verifier widget.
 * @param {string} [elementId]
 */
export function resetRecaptcha(elementId = 'recaptcha-container') {
  if (typeof window === 'undefined') return;

  if (window.recaptchaVerifier) {
    try {
      window.recaptchaVerifier.clear();
    } catch {
      // Ignore cleanup error
    }
    window.recaptchaVerifier = null;
  }

  const container = document.getElementById(elementId);
  if (container) {
    container.innerHTML = '';
  }
}

/**
 * Sends phone OTP using Firebase Phone Auth.
 * @param {string} phoneNumber - Full phone number with country code (e.g. +91 9876543210)
 * @param {RecaptchaVerifier} verifier - Configured RecaptchaVerifier instance
 * @returns {Promise<any>} ConfirmationResult
 */
export async function sendPhoneOtp(phoneNumber, verifier) {
  const formattedPhone = phoneNumber.replace(/\s+/g, '');
  console.log(auth, formattedPhone, verifier);
  return await signInWithPhoneNumber(auth, formattedPhone, verifier);
}

/**
 * Confirms OTP code with Firebase ConfirmationResult.
 * @param {any} confirmationResult - Result returned by sendPhoneOtp
 * @param {string} code - 6-digit OTP code
 * @returns {Promise<{ user: any, idToken: string }>}
 */
export async function verifyOtpCode(confirmationResult, code) {
  const userCredential = await confirmationResult.confirm(code);
  const idToken = await userCredential.user.getIdToken();
  return {
    user: userCredential.user,
    idToken
  };
}
