<script>
  import { onMount, tick } from 'svelte';
  import { authStore } from '../../stores/authStore.js';
  import { getRecaptchaVerifier, resetRecaptcha, sendPhoneOtp, verifyOtpCode } from '../../firebase.js';

  const countryCode = '+91';
  let rawPhoneNumber = $state('');
  let step = $state('phone'); // 'phone' | 'otp'
  let otpDigits = $state(['', '', '', '', '', '']);
  let isLoading = $state(false);
  let errorMessage = $state('');
  let resendCountdown = $state(30);
  let countdownTimer = null;
  let confirmationResult = null;

  let fullPhoneNumber = $derived(`${countryCode}${rawPhoneNumber.replace(/\D/g, '')}`);
  let isPhoneValid = $derived(rawPhoneNumber.replace(/\D/g, '').length >= 10);
  let isOtpComplete = $derived(otpDigits.join('').length === 6);

  onMount(() => {
    if (step === 'phone') {
      initRecaptcha();
    }

    return () => {
      if (countdownTimer) clearInterval(countdownTimer);
    };
  });

  async function initRecaptcha() {
    await tick();
    try {
      await getRecaptchaVerifier('recaptcha-container');
    } catch (err) {
      console.warn('Recaptcha init notice:', err);
    }
  }

  function startResendTimer() {
    resendCountdown = 30;
    if (countdownTimer) clearInterval(countdownTimer);
    countdownTimer = setInterval(() => {
      if (resendCountdown > 0) {
        resendCountdown -= 1;
      } else {
        clearInterval(countdownTimer);
      }
    }, 1000);
  }

  async function handleSendOtp(e) {
    if (e) e.preventDefault();
    if (!isPhoneValid) {
      errorMessage = 'Please enter a valid 10-digit mobile number.';
      return;
    }

    isLoading = true;
    errorMessage = '';

    try {
      await tick();
      const verifier = await getRecaptchaVerifier('recaptcha-container');
      if (!verifier) {
        throw new Error('Could not initialize reCAPTCHA container.');
      }

      confirmationResult = await sendPhoneOtp(fullPhoneNumber, verifier);
      step = 'otp';
      startResendTimer();
      await tick();
      const firstInput = document.getElementById('otp-input-0');
      if (firstInput) firstInput.focus();
    } catch (err) {
      console.error('Firebase Phone Auth Error:', err);
      resetRecaptcha('recaptcha-container');
      const code = err?.code || '';
      if (code === 'auth/invalid-app-credential') {
        const onLocalhost = typeof window !== 'undefined' && window.location.hostname === 'localhost';
        errorMessage = onLocalhost
          ? 'Real SMS requires reCAPTCHA verification. Open the app at http://127.0.0.1:5173 (not localhost), add 127.0.0.1 to Firebase Authorized Domains, and allow that domain on your reCAPTCHA Enterprise key. Test numbers bypass this check.'
          : 'Real SMS failed reCAPTCHA verification. In Firebase Console → Authentication → Settings → reCAPTCHA, ensure your production domain is listed on the Enterprise key (or switch Web to reCAPTCHA v2). Test numbers bypass this check.';
      } else if (code === 'auth/quota-exceeded') {
        errorMessage = 'Firebase Error (auth/quota-exceeded): Free daily SMS quota exceeded in Firebase. Please add phone number to "Phone numbers for testing" in Firebase Console.';
      } else {
        errorMessage = err?.message || 'Failed to send SMS OTP.';
      }
    } finally {
      isLoading = false;
    }
  }

  async function handleVerifyOtp(e) {
    if (e) e.preventDefault();
    const code = otpDigits.join('');
    if (code.length !== 6) {
      errorMessage = 'Please enter the complete 6-digit OTP code.';
      return;
    }

    isLoading = true;
    errorMessage = '';

    try {
      if (!confirmationResult) {
        throw new Error('OTP session expired. Please request a new verification code.');
      }
      const { idToken } = await verifyOtpCode(confirmationResult, code);
      await authStore.loginWithFirebaseToken(idToken, fullPhoneNumber);
    } catch (err) {
      console.error('OTP Verification Error:', err);
      errorMessage = err?.message || 'Invalid OTP code. Please check and try again.';
    } finally {
      isLoading = false;
    }
  }

  function handleOtpInput(index, event) {
    const val = event.target.value.replace(/\D/g, '');
    otpDigits[index] = val ? val.slice(-1) : '';

    if (val && index < 5) {
      const nextInput = document.getElementById(`otp-input-${index + 1}`);
      if (nextInput) nextInput.focus();
    }
  }

  function handleOtpKeyDown(index, event) {
    if (event.key === 'Backspace' && !otpDigits[index] && index > 0) {
      const prevInput = document.getElementById(`otp-input-${index - 1}`);
      if (prevInput) prevInput.focus();
    }
  }

  function handleOtpPaste(event) {
    event.preventDefault();
    const pastedText = (event.clipboardData || window.clipboardData).getData('text').replace(/\D/g, '');
    if (pastedText.length >= 6) {
      for (let i = 0; i < 6; i++) {
        otpDigits[i] = pastedText[i];
      }
      const lastInput = document.getElementById('otp-input-5');
      if (lastInput) lastInput.focus();
    }
  }

  function resetToPhoneStep() {
    step = 'phone';
    otpDigits = ['', '', '', '', '', ''];
    errorMessage = '';
    resetRecaptcha('recaptcha-container');
    initRecaptcha();
  }
</script>

<!-- Backdrop Modal -->
<div class="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-md transition-all animate-fadeIn">
  <div class="relative w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl shadow-2xl overflow-hidden p-6 sm:p-8">
    
    <!-- Top Branding & Icon -->
    <div class="flex flex-col items-center text-center mb-6">
      <div class="w-14 h-14 rounded-2xl bg-gradient-to-tr from-primary to-blue-600 flex items-center justify-center text-white shadow-lg shadow-primary/30 mb-3">
        <span class="material-symbols-outlined text-[32px]">smartphone</span>
      </div>
      <h2 class="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
        {step === 'phone' ? 'Phone Number Sign In' : 'Enter Verification Code'}
      </h2>
      <p class="text-xs text-slate-500 dark:text-slate-400 mt-1">
        {step === 'phone' 
          ? 'Enter your mobile number to receive a 6-digit verification code' 
          : `We sent an SMS with a code to ${fullPhoneNumber}`}
      </p>
    </div>

    <!-- Error Alert -->
    {#if errorMessage}
      <div class="mb-5 p-3.5 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800/60 text-red-600 dark:text-red-300 text-xs flex items-start gap-2">
        <span class="material-symbols-outlined text-base shrink-0 mt-0.5">error</span>
        <span class="flex-1 leading-relaxed">{errorMessage}</span>
      </div>
    {/if}

    <!-- STEP 1: Phone Number Entry -->
    {#if step === 'phone'}
      <form onsubmit={handleSendOtp} class="space-y-4">
        <div>
          <label for="phone-input" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1.5 font-['JetBrains_Mono']">
            Mobile Number
          </label>
          <div class="flex items-center gap-2">
            <!-- Fixed Country Code Badge -->
            <div class="h-12 px-3.5 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-xs font-bold text-slate-800 dark:text-slate-200 flex items-center justify-center gap-1.5 select-none shrink-0 font-['JetBrains_Mono']">
              <span>🇮🇳</span>
              <span>+91</span>
            </div>

            <!-- Phone Number Input -->
            <input
              id="phone-input"
              type="tel"
              bind:value={rawPhoneNumber}
              placeholder="98765 43210"
              maxlength="12"
              class="flex-1 h-12 px-4 bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm font-medium text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-primary font-['JetBrains_Mono'] tracking-wide"
            />
          </div>
        </div>

        <!-- Invisible reCAPTCHA container -->
        <div id="recaptcha-container"></div>

        <!-- Submit Button -->
        <button
          type="submit"
          disabled={!isPhoneValid || isLoading}
          class="w-full h-12 mt-2 bg-primary hover:bg-blue-700 disabled:opacity-50 text-white font-semibold text-sm rounded-xl shadow-md shadow-primary/25 transition-all flex items-center justify-center gap-2 active:scale-[0.99]"
        >
          {#if isLoading}
            <span class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
            <span>Sending OTP...</span>
          {:else}
            <span class="material-symbols-outlined text-lg">sms</span>
            <span>Send OTP Code</span>
          {/if}
        </button>
      </form>
    {/if}

    <!-- STEP 2: 6-Digit OTP Entry -->
    {#if step === 'otp'}
      <form onsubmit={handleVerifyOtp} class="space-y-5">
        <div>
          <div class="flex items-center justify-between mb-2">
            <label for="otp-input-0" class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 font-['JetBrains_Mono']">
              6-Digit Code
            </label>
            <button
              type="button"
              onclick={resetToPhoneStep}
              class="text-xs text-primary hover:underline font-medium"
            >
              Edit Phone
            </button>
          </div>

          <!-- OTP Input Boxes -->
          <div class="flex items-center justify-between gap-2" onpaste={handleOtpPaste}>
            {#each otpDigits as digit, idx}
              <input
                id="otp-input-{idx}"
                type="text"
                inputmode="numeric"
                maxlength="1"
                value={digit}
                oninput={(e) => handleOtpInput(idx, e)}
                onkeydown={(e) => handleOtpKeyDown(idx, e)}
                class="w-11 sm:w-12 h-13 text-center text-xl font-bold bg-slate-100 dark:bg-slate-800 border-2 border-slate-200 dark:border-slate-700 focus:border-primary dark:focus:border-primary rounded-xl text-slate-900 dark:text-white focus:outline-none transition-all font-['JetBrains_Mono']"
              />
            {/each}
          </div>
        </div>

        <!-- Resend OTP Link -->
        <div class="flex items-center justify-between text-xs text-slate-500">
          <span>Didn't receive code?</span>
          {#if resendCountdown > 0}
            <span class="font-['JetBrains_Mono'] font-medium text-slate-400">Resend in {resendCountdown}s</span>
          {:else}
            <button
              type="button"
              onclick={handleSendOtp}
              class="text-primary hover:underline font-semibold"
            >
              Resend OTP
            </button>
          {/if}
        </div>

        <!-- Action Buttons -->
        <div class="space-y-2">
          <button
            type="submit"
            disabled={!isOtpComplete || isLoading}
            class="w-full h-12 bg-primary hover:bg-blue-700 disabled:opacity-50 text-white font-semibold text-sm rounded-xl shadow-md shadow-primary/25 transition-all flex items-center justify-center gap-2 active:scale-[0.99]"
          >
            {#if isLoading}
              <span class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
              <span>Verifying Token...</span>
            {:else}
              <span class="material-symbols-outlined text-lg">verified_user</span>
              <span>Verify & Login</span>
            {/if}
          </button>

          <button
            type="button"
            onclick={resetToPhoneStep}
            class="w-full h-10 text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 font-medium text-xs rounded-xl transition-all"
          >
            Back to Phone Input
          </button>
        </div>
      </form>
    {/if}

  </div>
</div>
