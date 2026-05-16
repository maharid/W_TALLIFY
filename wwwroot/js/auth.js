// auth.js

document.addEventListener("DOMContentLoaded", function () {
  /* ---------------- Screen switching ---------------- */

  const screens = {
    login: document.getElementById("screen-login"),
    join: document.getElementById("screen-join"),
    signup: document.getElementById("screen-signup"),
  };

  function showScreen(name) {
    Object.keys(screens).forEach((key) => {
      if (screens[key]) {
        screens[key].classList.toggle("is-active", key === name);
      }
    });

    // remember current screen
    try {
      localStorage.setItem("authCurrentScreen", name);
    } catch (e) {
      // ignore storage errors
    }
  }

  // Initial screen: try restoring from localStorage
  const authPage = document.querySelector(".auth-page");
  let initialScreen = "login";

  // 1) Prefer what the server says (TempData / ViewBag)
  if (authPage && authPage.dataset.authMode) {
    initialScreen = authPage.dataset.authMode;
  } else {
    // 2) Otherwise, restore from localStorage if available
    try {
      const saved = localStorage.getItem("authCurrentScreen");
      if (saved) initialScreen = saved;
    } catch (e) {
      // ignore storage errors
    }
  }

  showScreen(initialScreen);

  // Navigation buttons / links
  const goToJoinFromLogin = document.getElementById("goToJoinFromLogin");
  const goToSignupFromLogin = document.getElementById("goToSignupFromLogin");
  const goToLoginFromJoin = document.getElementById("goToLoginFromJoin");
  const goToLoginFromSignup = document.getElementById("goToLoginFromSignup");

  if (goToJoinFromLogin) {
    goToJoinFromLogin.addEventListener("click", () => showScreen("join"));
  }

  if (goToSignupFromLogin) {
    goToSignupFromLogin.addEventListener("click", () => showScreen("signup"));
  }

  if (goToLoginFromJoin) {
    goToLoginFromJoin.addEventListener("click", () => showScreen("login"));
  }

  if (goToLoginFromSignup) {
    goToLoginFromSignup.addEventListener("click", () => showScreen("login"));
  }

  /* ---------------- "Where is my code and pin?" modal ---------------- */

  const codeHelpLink = document.getElementById("codeHelpLink");
  const codeHelpModal = document.getElementById("codeHelpModal");
  const codeHelpClose = codeHelpModal
    ? codeHelpModal.querySelector(".auth-modal-close")
    : null;

  function openCodeHelp() {
    if (!codeHelpModal) return;
    codeHelpModal.classList.add("is-open");
    codeHelpModal.setAttribute("aria-hidden", "false");
  }

  function closeCodeHelp() {
    if (!codeHelpModal) return;
    codeHelpModal.classList.remove("is-open");
    codeHelpModal.setAttribute("aria-hidden", "true");
  }

  if (codeHelpLink && codeHelpModal) {
    codeHelpLink.addEventListener("click", (e) => {
      e.preventDefault(); // don't navigate
      openCodeHelp();
    });
  }

  if (codeHelpClose) {
    codeHelpClose.addEventListener("click", closeCodeHelp);
  }

  // click outside inner box closes modal
  if (codeHelpModal) {
    codeHelpModal.addEventListener("click", (e) => {
      if (e.target === codeHelpModal) {
        closeCodeHelp();
      }
    });
  }

  /* ---------------- Validation helpers ---------------- */

  function clearFormErrors(form) {
    if (!form) return;
    form.querySelectorAll(".auth-error").forEach((el) => el.remove());
    form.querySelectorAll(".auth-input-error").forEach((el) =>
      el.classList.remove("auth-input-error")
    );
  }

  // NEW: clear one field's error (used on input)
  function clearFieldError(input) {
    if (!input) return;
    const field = input.closest(".auth-field") || input.parentElement;
    if (!field) return;

    const err = field.querySelector(".auth-error");
    if (err) err.remove();
    input.classList.remove("auth-input-error");
  }

  function setFieldError(input, message) {
    if (!input) return;
    const field = input.closest(".auth-field") || input.parentElement;
    if (!field) return;

    let errorEl = field.querySelector(".auth-error");
    if (!errorEl) {
      errorEl = document.createElement("div");
      errorEl.className = "auth-error";
      field.appendChild(errorEl);
    }

    errorEl.textContent = message;
    input.classList.add("auth-input-error");
  }

  function isValidEmail(value) {
    const trimmed = value.trim();
    if (!trimmed) return false;
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(trimmed);
  }

  // Strong password rules
  function isStrongPassword(value) {
    const trimmed = value.trim();

    if (trimmed.length < 8) return false;            // minimum 8
    if (!/[A-Z]/.test(trimmed)) return false;        // at least 1 uppercase
    if (!/[a-z]/.test(trimmed)) return false;        // at least 1 lowercase
    if (!/[0-9]/.test(trimmed)) return false;        // at least 1 number
    if (!/[^A-Za-z0-9]/.test(trimmed)) return false; // at least 1 special char

    return true;
  }

  function validateLoginForm(form) {
    let isValid = true;

    const emailInput = form.querySelector("#login-email");
    const passwordInput = form.querySelector("#login-password");

    if (!emailInput.value.trim()) {
      setFieldError(emailInput, "Email is required.");
      isValid = false;
    } else if (!isValidEmail(emailInput.value)) {
      setFieldError(emailInput, "Please enter a valid email.");
      isValid = false;
    }

    if (!passwordInput.value.trim()) {
      setFieldError(passwordInput, "Password is required.");
      isValid = false;
    }

    return isValid;
  }

  function validateJoinForm(form) {
    let isValid = true;

    const codeInput = form.querySelector("#join-code");
    const pinInput = form.querySelector("#join-pin");

    if (!codeInput.value.trim()) {
      setFieldError(codeInput, "Event code is required.");
      isValid = false;
    }

    if (!pinInput.value.trim()) {
      setFieldError(pinInput, "Pin is required.");
      isValid = false;
    }

    return isValid;
  }

  function validateSignupForm(form) {
    let isValid = true;

    const emailInput = form.querySelector("#signup-email");
    const passwordInput = form.querySelector("#signup-password");
    const confirmInput = form.querySelector("#signup-password-confirm");

    if (!emailInput.value.trim()) {
      setFieldError(emailInput, "Email is required.");
      isValid = false;
    } else if (!isValidEmail(emailInput.value)) {
      setFieldError(emailInput, "Please enter a valid email.");
      isValid = false;
    }

    if (!passwordInput.value.trim()) {
      setFieldError(passwordInput, "Password is required.");
      isValid = false;
    } else if (!isStrongPassword(passwordInput.value)) {
      setFieldError(
        passwordInput,
        "Password must be at least 8 characters and include 1 uppercase, 1 lowercase, 1 number, and 1 special character."
      );
      isValid = false;
    }

    if (!confirmInput.value.trim()) {
      setFieldError(confirmInput, "Please confirm your password.");
      isValid = false;
    } else if (passwordInput.value !== confirmInput.value) {
      setFieldError(confirmInput, "Passwords do not match.");
      isValid = false;
    }

    return isValid;
  }

    function validateResetForm(form) {
    let isValid = true;

    const passwordInput = form.querySelector("#rp-password");
    const confirmInput = form.querySelector("#rp-confirm");

    // New password
    if (!passwordInput.value.trim()) {
      setFieldError(passwordInput, "Password is required.");
      isValid = false;
    } else if (!isStrongPassword(passwordInput.value)) {
      setFieldError(
        passwordInput,
        "Password must be at least 8 characters and include 1 uppercase, 1 lowercase, 1 number, and 1 special character."
      );
      isValid = false;
    }

    // Confirm
    if (!confirmInput.value.trim()) {
      setFieldError(confirmInput, "Please confirm your password.");
      isValid = false;
    } else if (passwordInput.value !== confirmInput.value) {
      setFieldError(confirmInput, "Passwords do not match.");
      isValid = false;
    }

    return isValid;
  }



  /* ---------------- Form submit handlers ---------------- */

  const loginForm = document.getElementById("loginForm");
  const joinForm = document.getElementById("joinForm");
  const signupForm = document.getElementById("signupForm");
  const resetForm = document.getElementById("resetPasswordForm");


  if (loginForm) {
    loginForm.addEventListener("submit", (e) => {
      e.preventDefault();
      clearFormErrors(loginForm);

      if (!validateLoginForm(loginForm)) {
        return;
      }

      // ✅ Make sure we send the REAL password, not the masked preview
      const passwordInput = document.getElementById("login-password");
      if (passwordInput && typeof realValues !== "undefined") {
        const real = realValues.get(passwordInput);

        // If we have a stored real value, use it
        if (typeof real === "string" && real.length > 0) {
          passwordInput.type = "password";   // ensure it’s a password field
          passwordInput.value = real;        // put the real password into the input
        }
      }

      loginForm.submit();
    });
  }

  if (joinForm) {
    joinForm.addEventListener("submit", (e) => {
      e.preventDefault();
      clearFormErrors(joinForm);

      if (!validateJoinForm(joinForm)) {
        return;
      }

      const codeInput = joinForm.querySelector("#join-code");
      const pinInput  = joinForm.querySelector("#join-pin");
      const btn       = joinForm.querySelector("button[type='submit']");
      const originalText = btn ? btn.textContent : "Join";

      if (btn) {
        btn.disabled = true;
        btn.textContent = "Joining...";
      }

      const payload = {
        accessCode: codeInput.value.trim(),
        pin:        pinInput.value.trim()
      };

      fetch("/Judge/Login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      })
      .then(async (res) => {
        let data = {};
        try { data = await res.json(); } catch {}

        if (res.ok && data.success) {
          // Success -> Redirect
          window.location.href = data.redirectUrl || "/";
        } else {
          // Error
          const msg = data.message || "Login failed. Please check your code and PIN.";
          setFieldError(codeInput, msg);
          // also shake or highlight?
        }
      })
      .catch(err => {
        console.error(err);
        setFieldError(codeInput, "Network error. Please try again.");
      })
      .finally(() => {
        if (btn) {
          btn.disabled = false;
          btn.textContent = originalText;
        }
      });
    });
  }

    if (signupForm) {
      signupForm.addEventListener("submit", (e) => {
        // we’ll control whether it goes or not
        e.preventDefault();
        clearFormErrors(signupForm);

        // run JS validation
        if (!validateSignupForm(signupForm)) {
          // there are errors – stay on page and show messages
          return;
        }

        // ✅ valid → now actually submit to the backend
        signupForm.submit();
      });

      // LIVE clearing of errors while typing (so red text goes away)
      const signupEmail = document.getElementById("signup-email");
      const signupPassword = document.getElementById("signup-password");
      const signupConfirm = document.getElementById("signup-password-confirm");

      [signupEmail, signupPassword, signupConfirm].forEach((input) => {
        if (!input) return;
        input.addEventListener("input", () => clearFieldError(input));
      });
    }

      if (resetForm) {
    resetForm.addEventListener("submit", (e) => {
      e.preventDefault();
      clearFormErrors(resetForm);

      if (validateResetForm(resetForm)) {
        resetForm.submit();  // only submit if valid
      }
    });

    const rpPassword = document.getElementById("rp-password");
    const rpConfirm = document.getElementById("rp-confirm");

    [rpPassword, rpConfirm].forEach((input) => {
      if (!input) return;
      input.addEventListener("input", () => clearFieldError(input));
    });
  }


  /* ---------------- Password / pin visibility + preview ---------------- */

  const passwordInputs = document.querySelectorAll(".auth-password");
  const toggleButtons = document.querySelectorAll(".password-toggle");

  // Map<HTMLElement, string> : real underlying value
  const realValues = new Map();
  // Map<HTMLElement, number> : timeout IDs for 1s previews
  const previewTimeouts = new Map();

  passwordInputs.forEach((input) => {
    realValues.set(input, "");
  });

  function getInputFromToggle(btn) {
    const targetId = btn.getAttribute("data-target");
    if (!targetId) return null;
    return document.getElementById(targetId);
  }

  function updateToggleIcon(btn) {
    const visible = btn.getAttribute("data-visible") === "true";
    // 🙈 when visible, 🙉 when hidden
    btn.textContent = visible ? "🙈" : "🙉";
  }

  toggleButtons.forEach((btn) => {
    updateToggleIcon(btn);
  });

  // Toggle show/hide on click
  toggleButtons.forEach((btn) => {
    btn.addEventListener("click", () => {
      const input = getInputFromToggle(btn);
      if (!input) return;

      const currentlyVisible = btn.getAttribute("data-visible") === "true";
      const nextVisible = !currentlyVisible;
      btn.setAttribute("data-visible", String(nextVisible));

      const real = realValues.get(input) ?? "";

      // clear any preview timer
      const existing = previewTimeouts.get(input);
      if (existing) {
        clearTimeout(existing);
        previewTimeouts.delete(input);
      }

      if (nextVisible) {
        // show full value
        input.type = "text";
        input.value = real;
      } else {
        // hide value
        input.type = "password";
        input.value = real;
      }

      updateToggleIcon(btn);
    });
  });

  // Input behavior: last-character preview with • for 1 second
  passwordInputs.forEach((input) => {
    input.addEventListener("input", (e) => {
      const wrapper = input.closest(".auth-input-wrapper");
      if (!wrapper) return;

      const btn = wrapper.querySelector(".password-toggle");
      if (!btn) return;

      const isVisible = btn.getAttribute("data-visible") === "true";

      // If visible: normal behavior
      if (isVisible) {
        const value = input.value;
        realValues.set(input, value);

        if (value.length > 0) {
          btn.classList.add("is-visible");
        } else {
          btn.classList.remove("is-visible");
        }
        return;
      }

      // Hidden mode: manage our own real value
      let real = realValues.get(input) ?? "";

      // Handle deletes
      if (e.inputType && e.inputType.startsWith("delete")) {
        if (input.value.length === 0) {
          real = "";
        } else {
          if (real.length > 0) {
            real = real.slice(0, -1);
          }
        }
      } else if (e.inputType === "insertText") {
        real += e.data ?? "";
      } else if (e.inputType === "insertFromPaste") {
        real = input.value;
      } else {
        real = input.value;
      }

      realValues.set(input, real);

      // Clear existing preview timeout
      const existing = previewTimeouts.get(input);
      if (existing) {
        clearTimeout(existing);
        previewTimeouts.delete(input);
      }

      if (real.length === 0) {
        btn.classList.remove("is-visible");
        input.type = "password";
        input.value = "";
        return;
      }

      btn.classList.add("is-visible");

      // Build preview: •••a (only last character visible)
      const lastChar = real.slice(-1);
      const masked = "•".repeat(Math.max(0, real.length - 1)) + lastChar;

      input.type = "text";
      input.value = masked;

      // After 1 second, mask everything again
      const timeoutId = setTimeout(() => {
        const stillHidden = btn.getAttribute("data-visible") === "false";
        const latestReal = realValues.get(input) ?? "";

        if (stillHidden && latestReal.length > 0) {
          input.type = "password";
          input.value = latestReal; // browser will show standard bullets
        }

        previewTimeouts.delete(input);
      }, 1000);

      previewTimeouts.set(input, timeoutId);
    });
  });

    /* -------- Center message popup close -------- */

    const authMessageOverlay = document.getElementById("authMessageOverlay");
    const authMessageClose = document.getElementById("authMessageClose");

    function hideAuthMessage() {
      if (!authMessageOverlay) return;
      authMessageOverlay.style.display = "none";
      authMessageOverlay.setAttribute("aria-hidden", "true");
    }

    if (authMessageClose && authMessageOverlay) {
      authMessageClose.addEventListener("click", hideAuthMessage);

      // click outside card closes as well
      authMessageOverlay.addEventListener("click", (e) => {
        if (e.target === authMessageOverlay) {
          hideAuthMessage();
        }
      });
    }
});
