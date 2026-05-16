// wwwroot/js/settings.js

/****************************************************
 * GLOBAL NOTIFICATION TOAST (shared across app)
 ****************************************************/
let notifToastEl = null;
let notifToastMsg = null;
let notifToastTimeout = null;

function ensureNotifToast() {
    if (notifToastEl) return;

    notifToastEl = document.getElementById("notif-toast");
    notifToastMsg = notifToastEl?.querySelector(".notif-toast-message");
}

function showNotif(message, type = "error") {
    ensureNotifToast();
    if (!notifToastEl || !notifToastMsg) return;

    notifToastMsg.textContent = message;

    // color themes
    notifToastEl.style.background =
        type === "success"
            ? "#10b981"      // green
            : type === "warn"
            ? "#f59e0b"      // yellow
            : "#ef4444";     // default red

    notifToastEl.classList.add("is-visible");

    if (notifToastTimeout) clearTimeout(notifToastTimeout);

    notifToastTimeout = setTimeout(() => {
        notifToastEl.classList.remove("is-visible");
    }, 2400);
}

/****************************************************
 * SETTINGS PAGE LOGIC
 ****************************************************/
(function () {
  var COLOR_KEY = "tallify-primary-color";
  var PROFILE_NAME_KEY = "tallify-profile-name";
  var PROFILE_EMAIL_KEY = "tallify-profile-email";
  var PROFILE_PHOTO_KEY = "tallify-profile-photo";
  var ORG_NAME_KEY = "tallify-org-name";
  var ORG_SUBTITLE_KEY = "tallify-org-subtitle";
  var ORG_PHOTO_KEY = "tallify-org-photo";

  var root = document.documentElement;

  function hexToRgba(hex, alpha) {
    if (!hex) return "";
    var h = hex.trim().replace("#", "");
    if (h.length === 3) {
      h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
    }
    if (h.length !== 6) return "";
    var r = parseInt(h.slice(0, 2), 16);
    var g = parseInt(h.slice(2, 4), 16);
    var b = parseInt(h.slice(4, 6), 16);
    return "rgba(" + r + ", " + g + ", " + b + ", " + alpha + ")";
  }

  function setPrimaryColor(hex) {
    if (!hex) return;
    root.style.setProperty("--color-primary", hex);
    root.style.setProperty("--color-link", hex);

    var soft = hexToRgba(hex, 0.35);
    var softBg = hexToRgba(hex, 0.12);

    if (soft) {
      root.style.setProperty("--color-primary-soft", soft);
    }
    if (softBg) {
      root.style.setProperty("--color-primary-soft-bg", softBg);
    }
  }

  function applySavedPrimaryColor() {
    // Exclude Auth pages and Event Manage pages from global theme application
    if (document.body.classList.contains("auth-body") || 
        window.location.pathname.toLowerCase().indexOf("/events/manage") !== -1) {
        return;
    }

    var saved = localStorage.getItem(COLOR_KEY) || "#ff007a";
    setPrimaryColor(saved);

    var buttons = document.querySelectorAll(".settings-color-option");
    var matched = false;
    for (var i = 0; i < buttons.length; i++) {
      var btn = buttons[i];
      var val = (btn.getAttribute("data-color") || "").toLowerCase();
      btn.classList.remove("is-selected");
      if (val && val !== "custom" && val === saved.toLowerCase()) {
        btn.classList.add("is-selected");
        matched = true;
      }
    }
    if (!matched) {
      var customBtn = document.querySelector(".settings-color-option-custom");
      if (customBtn) customBtn.classList.add("is-selected");
    }
  }

  function applyOrgText() {
    var name = localStorage.getItem(ORG_NAME_KEY) || "Your Organization";
    var subtitle = localStorage.getItem(ORG_SUBTITLE_KEY) || "Subtitle";

    var nameEls = document.querySelectorAll("[data-org-name]");
    for (var i = 0; i < nameEls.length; i++) {
      nameEls[i].textContent = name;
    }

    var subEls = document.querySelectorAll("[data-org-subtitle]");
    for (var j = 0; j < subEls.length; j++) {
      subEls[j].textContent = subtitle;
    }

    var orgNameInput = document.getElementById("orgNameInput");
    var orgSubtitleInput = document.getElementById("orgSubtitleInput");
    if (orgNameInput) orgNameInput.value = name;
    if (orgSubtitleInput) orgSubtitleInput.value = subtitle;
  }

  function applyProfileText() {
    var nameInputEl = document.getElementById("profileNameInput");
    var emailInputEl = document.getElementById("profileEmailInput");

    var nameDefault =
      nameInputEl && nameInputEl.value ? nameInputEl.value : "Tallify User";
    var emailDefault =
      emailInputEl && emailInputEl.value ? emailInputEl.value : "";

    var name = localStorage.getItem(PROFILE_NAME_KEY) || nameDefault;
    var email = localStorage.getItem(PROFILE_EMAIL_KEY) || emailDefault;

    if (nameInputEl) nameInputEl.value = name;
    if (emailInputEl && email) emailInputEl.value = email;

    var avatarCircle = document.getElementById("accountAvatarCircle");
    if (avatarCircle && !avatarCircle.style.backgroundImage) {
      var parts = name.split(" ");
      var initials = "";
      for (var i = 0; i < parts.length; i++) {
        if (parts[i]) initials += parts[i][0];
      }
      initials = initials.toUpperCase().slice(0, 2);
      avatarCircle.textContent = initials;
    }
  }

  function applyPhotos() {
    // Get the profilePhoto path from localStorage. This value is updated after upload/remove.
    var profilePhoto = localStorage.getItem(PROFILE_PHOTO_KEY); 

    // Update Account tab avatarCircle
    var avatarCircle = document.getElementById("accountAvatarCircle");
    if (avatarCircle) {
      if (profilePhoto) {
        avatarCircle.style.backgroundImage = "url(" + profilePhoto + ")";
        avatarCircle.classList.add("has-photo");
        avatarCircle.textContent = ""; // Clear initials if photo is present
      } else {
        avatarCircle.style.backgroundImage = "";
        avatarCircle.classList.remove("has-photo");
        // Re-calculate and set initials if no photo
        var nameInputEl = document.getElementById("profileNameInput");
        var name = nameInputEl && nameInputEl.value ? nameInputEl.value : "Tallify User";
        var parts = name.split(" ");
        var initials = "";
        for (var i = 0; i < parts.length; i++) {
          if (parts[i]) initials += parts[i][0];
        }
        initials = initials.toUpperCase().slice(0, 2);
        avatarCircle.textContent = initials;
      }
    }

    // Update Header Profile Image
    var headerProfileImage = document.getElementById("headerProfileImage"); 
    var headerInitials = document.getElementById("headerInitials"); 

    if (headerProfileImage && headerInitials) {
        if (profilePhoto) {
            headerProfileImage.src = profilePhoto;
            headerProfileImage.classList.remove("hidden");
            headerInitials.classList.add("hidden");
        } else {
            headerProfileImage.classList.add("hidden");
            headerInitials.classList.remove("hidden");
            
            // Calculate initials client-side for the header too
            var nameInputEl = document.getElementById("profileNameInput");
            var name = nameInputEl && nameInputEl.value ? nameInputEl.value : "Tallify User";
            var parts = name.split(" ");
            var initials = "";
            for (var i = 0; i < parts.length; i++) {
                if (parts[i]) initials += parts[i][0];
            }
            initials = initials.toUpperCase().slice(0, 2);
            headerInitials.textContent = initials;
        }
    }

    // Toggle button visibility based on photo presence
    var photoPresentButtons = document.getElementById("photoPresentButtons");
    var noPhotoButtons = document.getElementById("noPhotoButtons");

    if (photoPresentButtons && noPhotoButtons) {
        if (profilePhoto) {
            photoPresentButtons.style.display = "flex";
            noPhotoButtons.style.display = "none";
        } else {
            photoPresentButtons.style.display = "none";
            noPhotoButtons.style.display = "flex";
        }
    }

    var orgSquare = document.getElementById("orgAvatarSquare");
    if (orgSquare) {
      if (localStorage.getItem(ORG_PHOTO_KEY)) { // Org photo also uses localStorage
        orgSquare.style.backgroundImage = "url(" + localStorage.getItem(ORG_PHOTO_KEY) + ")";
      } else {
        orgSquare.style.backgroundImage = "";
      }
    }

    var orgAvatars = document.querySelectorAll(".org-avatar");
    for (var i = 0; i < orgAvatars.length; i++) {
      if (localStorage.getItem(ORG_PHOTO_KEY)) {
        orgAvatars[i].style.backgroundImage = "url(" + localStorage.getItem(ORG_PHOTO_KEY) + ")";
      } else {
        orgAvatars[i].style.backgroundImage = "";
      }
    }
  }

  function readImageAsDataUrl(file, callback) {
    var reader = new FileReader();
    reader.onload = function (e) {
      callback(e.target.result);
    };
    reader.readAsDataURL(file);
  }

  function isValidEmail(email) {
    return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email);
  }

  document.addEventListener("DOMContentLoaded", function () {
    // apply saved theme on ALL non-auth pages
    applySavedPrimaryColor(); 
    // Initial profile, org text and photos are now rendered directly by Razor in the HTML.
    // Client-side JS should no longer overwrite initial values from localStorage.

    /* ---------- Tabs (Account / Organization / About) ---------- */
    var tabs = document.querySelectorAll(".settings-tab");
    var panes = document.querySelectorAll(".settings-pane");
    if (tabs.length && panes.length) {
      for (var i = 0; i < tabs.length; i++) {
        (function (tab) {
          tab.addEventListener("click", function () {
            var name = tab.getAttribute("data-settings-tab");
            // localStorage.setItem("tallify-settings-active-tab", name); // Removed: Do not persist tab across reloads
            for (var j = 0; j < tabs.length; j++) {
              tabs[j].classList.remove("is-active");
            }
            tab.classList.add("is-active");
            for (var k = 0; k < panes.length; k++) {
              var pane = panes[k];
              var paneName = pane.getAttribute("data-settings-pane");
              if (paneName === name) {
                pane.classList.add("is-active");
              } else {
                pane.classList.remove("is-active");
              }
            }
            // Reset scroll position to top for settings-main
            var settingsMain = document.querySelector(".settings-main");
            if (settingsMain) {
              settingsMain.scrollTop = 0;
            }

            // Load archived events if that tab is clicked
            if (name === "archived-events" && !archivedEventsLoaded) {
                loadArchivedEvents();
            }
          });
        })(tabs[i]);
      }
    }

    // Restore active tab on load (priority: ViewBag > Account default)
    var initialSettingsTab = "@ViewBag.SettingsTab"; 
    // var storedTab = localStorage.getItem("tallify-settings-active-tab"); // Removed: Do not restore from localStorage

    if (initialSettingsTab === "ArchivedEvents") {
        document.querySelector('.settings-tab[data-settings-tab="archived-events"]')?.click();
    } 
    // If no ViewBag, the default "is-active" on the "Account" tab in HTML will take effect.


    // Flag to ensure archived events are loaded only once initially
    var archivedEventsLoaded = false;

    // Function to load archived events via AJAX
    async function loadArchivedEvents() {
        var archivedEventsPane = document.querySelector('[data-settings-pane="archived-events"]');
        var contentContainer = archivedEventsPane?.querySelector('#archivedEventsContent');

        if (!archivedEventsPane || !contentContainer) return;

        // Show loading state
        contentContainer.innerHTML = '<p style="text-align:center; padding: 20px; color: var(--color-text-faint);">Loading archived events...</p>';
        
        try {
            var response = await fetch("/Settings/GetArchivedEventsPartial");
            if (!response.ok) {
                throw new Error("Failed to load archived events.");
            }
            var html = await response.text();
            contentContainer.innerHTML = html;
            archivedEventsLoaded = true;
            wireArchivedEventsButtons(contentContainer); // Wire buttons after content is loaded
        } catch (error) {
            console.error("Error loading archived events:", error);
            contentContainer.innerHTML = '<p style="text-align:center; padding: 20px; color: var(--color-danger-primary);">Failed to load archived events.</p>';
            showNotif("Failed to load archived events.", "error");
        }
    }

    // Function to wire up event listeners for dynamically loaded buttons
    function wireArchivedEventsButtons(container) {
        // --- Restore Event Logic ---
        const restoreModalOverlay = document.getElementById('restore-event-modal-overlay');
        const btnCloseRestore = document.getElementById('btn-close-restore-event');
        const btnCancelRestore = document.getElementById('btnCancelRestoreEvent');
        const btnConfirmRestore = document.getElementById('btnConfirmRestoreEvent');
        let eventToRestoreId = null;

        function closeRestoreModal() {
            if (restoreModalOverlay) restoreModalOverlay.classList.remove('open');
            eventToRestoreId = null;
        }

        // Open Modal
        container.querySelectorAll('.btn-restore-event').forEach(button => {
            button.addEventListener('click', function() {
                eventToRestoreId = this.dataset.eventId;
                if (restoreModalOverlay) restoreModalOverlay.classList.add('open');
            });
        });

        // Close Modal Handlers
        if (btnCloseRestore) btnCloseRestore.onclick = closeRestoreModal;
        if (btnCancelRestore) btnCancelRestore.onclick = closeRestoreModal;
        if (restoreModalOverlay) {
            restoreModalOverlay.onclick = (e) => {
                if (e.target === restoreModalOverlay) closeRestoreModal();
            };
        }

        // Confirm Action
        if (btnConfirmRestore) {
            // Remove previous event listener to avoid duplicates if re-wired
            const newBtn = btnConfirmRestore.cloneNode(true);
            btnConfirmRestore.parentNode.replaceChild(newBtn, btnConfirmRestore);
            
            newBtn.addEventListener('click', async function() {
                if (!eventToRestoreId) return;
                
                // Show loading state on button
                const originalText = this.textContent;
                this.textContent = 'Restoring...';
                this.disabled = true;

                try {
                    // Get anti-forgery token from the partial view or main layout
                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                    
                    const response = await fetch(`/Settings/RestoreEvent?id=${eventToRestoreId}`, {
                        method: 'POST',
                        headers: {
                            'RequestVerificationToken': token || ''
                        }
                    });
                    const data = await response.json();
                    
                    if (data.success) {
                        showNotif(data.message || 'Event restored successfully.', 'success');
                        closeRestoreModal();
                        setTimeout(() => loadArchivedEvents(), 500); // Reload the list
                    } else {
                        showNotif(data.message || 'Failed to restore event.', 'error');
                        closeRestoreModal();
                    }
                } catch (error) {
                    console.error('Error:', error);
                    showNotif('An error occurred during restoration.', 'error');
                    closeRestoreModal();
                } finally {
                    this.textContent = originalText;
                    this.disabled = false;
                }
            });
        }


        // --- Delete Permanently Logic ---
        const deleteModalOverlay = document.getElementById('delete-event-modal-overlay');
        const btnCloseDelete = document.getElementById('btn-close-delete-event');
        const btnCancelDelete = document.getElementById('btnCancelDeleteEvent');
        const btnConfirmDelete = document.getElementById('btnConfirmDeleteEvent');
        let eventToDeleteId = null;

        function closeDeleteModal() {
            if (deleteModalOverlay) deleteModalOverlay.classList.remove('open');
            eventToDeleteId = null;
        }

        // Open Modal
        container.querySelectorAll('.btn-delete-event').forEach(button => {
            button.addEventListener('click', function() {
                eventToDeleteId = this.dataset.eventId;
                if (deleteModalOverlay) deleteModalOverlay.classList.add('open');
            });
        });

        // Close Modal Handlers
        if (btnCloseDelete) btnCloseDelete.onclick = closeDeleteModal;
        if (btnCancelDelete) btnCancelDelete.onclick = closeDeleteModal;
        if (deleteModalOverlay) {
            deleteModalOverlay.onclick = (e) => {
                if (e.target === deleteModalOverlay) closeDeleteModal();
            };
        }

        // Confirm Action
        if (btnConfirmDelete) {
             // Remove previous event listener
            const newBtn = btnConfirmDelete.cloneNode(true);
            btnConfirmDelete.parentNode.replaceChild(newBtn, btnConfirmDelete);

            newBtn.addEventListener('click', async function() {
                if (!eventToDeleteId) return;

                const originalText = this.textContent;
                this.textContent = 'Deleting...';
                this.disabled = true;

                try {
                     const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

                    const response = await fetch(`/Settings/PermanentlyDeleteEvent?id=${eventToDeleteId}`, {
                        method: 'POST',
                        headers: {
                            'RequestVerificationToken': token || ''
                        }
                    });
                    const data = await response.json();
                    
                    if (data.success) {
                        showNotif(data.message || 'Event permanently deleted.', 'success');
                        closeDeleteModal();
                        setTimeout(() => loadArchivedEvents(), 500); 
                    } else {
                        showNotif(data.message || 'Failed to delete event permanently.', 'error');
                        closeDeleteModal();
                    }
                } catch (error) {
                    console.error('Error:', error);
                    showNotif('An error occurred during permanent deletion.', 'error');
                    closeDeleteModal();
                } finally {
                    this.textContent = originalText;
                    this.disabled = false;
                }
            });
        }
    }

    // Initial check for active tab on load (if page was refreshed on archived-events)
    var initialSettingsTabInput = document.getElementById("initial-settings-tab");
    var initialSettingsTab = initialSettingsTabInput ? initialSettingsTabInput.value : "";
    
    if (initialSettingsTab === "ArchivedEvents") {
        document.querySelector('.settings-tab[data-settings-tab="archived-events"]')?.click();
    }

    /* ---------- Color scheme + Organization (Save button) ---------- */
    var colorButtons = document.querySelectorAll(".settings-color-option");
    var customBtn = document.querySelector(".settings-color-option-custom");
    var customPicker = document.getElementById("customColorPicker");
    var saveBtn = document.querySelector(".settings-color-save");

    var pendingColor = localStorage.getItem(COLOR_KEY) || "#ff007a";

    function markSelected(btn) {
      for (var i = 0; i < colorButtons.length; i++) {
        colorButtons[i].classList.remove("is-selected");
      }
      if (btn) btn.classList.add("is-selected");
    }

    for (var i = 0; i < colorButtons.length; i++) {
      (function (btn) {
        btn.addEventListener("click", function () {
          var value = btn.getAttribute("data-color");
          if (!value) return;

          if (value === "custom") {
            if (customPicker) customPicker.click();
            return;
          }

          pendingColor = value;
          markSelected(btn);
        });
      })(colorButtons[i]);
    }

    if (customPicker) {
      customPicker.addEventListener("input", function (e) {
        pendingColor = e.target.value;
        if (customBtn) markSelected(customBtn);
      });
    }

            if (saveBtn) {
              saveBtn.addEventListener("click", function () {
                // validate + save org text
                var orgNameInput = document.getElementById("orgNameInput");
                var orgSubtitleInput = document.getElementById("orgSubtitleInput");
                var notificationsToggle = document.getElementById("enableNotificationsToggle");
                var nameError = document.getElementById("orgNameError");
                var subError = document.getElementById("orgSubtitleError");
        
                if (nameError) nameError.textContent = "";
                if (subError) subError.textContent = "";
        
                var valid = true;
                if (orgNameInput && !orgNameInput.value.trim()) {
                  valid = false;
                  if (nameError)
                    nameError.textContent = "Organization name is required.";
                }
                if (orgSubtitleInput && !orgSubtitleInput.value.trim()) {
                  valid = false;
                  if (subError) subError.textContent = "Subtitle is required.";
                }
                if (!valid) return;
        
                // 1. Apply color locally immediately
                setPrimaryColor(pendingColor);
                localStorage.setItem(COLOR_KEY, pendingColor);
        
                // 2. Save to Server
                fetch("/Settings/UpdateTheme", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({
                        themeColor: pendingColor,
                        organizationName: orgNameInput ? orgNameInput.value.trim() : null,
                        organizationSubtitle: orgSubtitleInput ? orgSubtitleInput.value.trim() : null,
                        enableNotifications: notificationsToggle ? notificationsToggle.checked : true
                    })
                })
                .then(res => {
                    if (res.ok) {
                         // show confirmation
                         showNotif("Settings saved successfully.", "success");
                    } else {
                         showNotif("Failed to save theme to server.", "warn");
                    }
                })
                .catch(err => {
                    console.error(err);
                    showNotif("Error saving settings.", "error");
                });
        
                // Update localStorage for org name and subtitle
                if (orgNameInput) {
                  localStorage.setItem(ORG_NAME_KEY, orgNameInput.value.trim());
                }
                if (orgSubtitleInput) {
                  localStorage.setItem(ORG_SUBTITLE_KEY, orgSubtitleInput.value.trim());
                }
                applyOrgText();
              });
            }    /* ---------- Profile name / email Edit buttons ---------- */
    var editButtons = document.querySelectorAll(
      ".settings-input-action[data-edit-target]"
    );
    for (var i = 0; i < editButtons.length; i++) {
      (function (btn) {
        btn.addEventListener("click", function () {
          var targetId = btn.getAttribute("data-edit-target");
          var errorId = btn.getAttribute("data-error-target");
          var input = document.getElementById(targetId);
          var errorEl = errorId ? document.getElementById(errorId) : null;
          if (!input) return;

          var isEditing = btn.getAttribute("data-editing") === "true";

          if (!isEditing) {
            // start editing
            btn.setAttribute("data-editing", "true");
            btn.querySelector("span").textContent = "Save";
            input.disabled = false;
            input.focus();
            input.select();
            if (errorEl) errorEl.textContent = "";
          } else {
            // save with validation
            var value = input.value.trim();
            var valid = true;

            if (!value) {
              valid = false;
              if (errorEl) errorEl.textContent = "This field is required.";
            } else if (
              targetId === "profileEmailInput" &&
              !isValidEmail(value)
            ) {
              valid = false;
              if (errorEl) errorEl.textContent = "Please enter a valid email.";
            }

            if (!valid) return;

            if (targetId === "profileNameInput") {
              localStorage.setItem(PROFILE_NAME_KEY, value);
            } else if (targetId === "profileEmailInput") {
              localStorage.setItem(PROFILE_EMAIL_KEY, value);
            }

            input.disabled = true;
            btn.setAttribute("data-editing", "false");
            btn.querySelector("span").textContent = "Edit";
            applyProfileText();

            // confirmation for account fields too
            showNotif("Account details saved.", "success");
          }
        });
      })(editButtons[i]);
    }

    /* ---------- Profile photo change ---------- */
    var profileBtn = document.getElementById("btnChangeProfilePhoto"); // This is the "Change" button
    var addProfileBtn = document.getElementById("btnAddProfilePhoto");   // This is the new "Add" button
    var removeProfileBtn = document.getElementById("btnRemoveProfilePhoto");
    var profileInput = document.getElementById("profilePhotoInput");
    var removePhotoOverlay = document.getElementById("remove-photo-confirm-modal-overlay");
    var btnCloseRemovePhoto = document.getElementById("btn-close-remove-photo");
    var btnCancelRemovePhoto = document.getElementById("btnCancelRemovePhoto");
    var btnConfirmRemovePhoto = document.getElementById("btnConfirmRemovePhoto");

    // Click handler for Change or Add buttons
    if (profileBtn && profileInput) {
      profileBtn.addEventListener("click", function () {
        profileInput.click();
      });
    }
    if (addProfileBtn && profileInput) {
        addProfileBtn.addEventListener("click", function () {
            profileInput.click();
        });
    }

    // Handle file input change event (common for both add/change)
    if (profileInput) {
      profileInput.addEventListener("change", function (e) {
        var file = e.target.files && e.target.files[0];
        if (!file) return;
        
        // 1. Upload to Server
        var formData = new FormData();
        formData.append("file", file);

        fetch("/Settings/UploadPhoto", {
            method: "POST",
            body: formData
        })
        .then(res => {
            if (!res.ok) throw new Error("Upload failed");
            return res.json();
        })
        .then(data => {
            var filePath = data.filePath;

            // 2. Persist path to User record via UpdateTheme
            return fetch("/Settings/UpdateTheme", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    profilePhotoPath: filePath
                })
            }).then(res => {
                if (res.ok) return filePath;
                throw new Error("Failed to save profile photo path");
            });
        })
        .then(filePath => {
            // 3. Update Local Storage & UI
            localStorage.setItem(PROFILE_PHOTO_KEY, filePath);
            applyPhotos();
            showNotif("Profile photo updated.", "success");
        })
        .catch(err => {
            console.error(err);
            showNotif("Error updating profile photo.", "error");
        });
      });
    }

    /* ---------- Remove Profile Photo Logic ---------- */
    function openRemovePhotoModal() {
        if (removePhotoOverlay) {
            removePhotoOverlay.classList.add("open");
            removePhotoOverlay.setAttribute("aria-hidden", "false");
        }
    }

    function closeRemovePhotoModal() {
        if (removePhotoOverlay) {
            removePhotoOverlay.classList.remove("open");
            removePhotoOverlay.setAttribute("aria-hidden", "true");
        }
    }

    if (removeProfileBtn) {
        removeProfileBtn.addEventListener("click", openRemovePhotoModal);
    }
    if (btnCloseRemovePhoto) {
        btnCloseRemovePhoto.addEventListener("click", closeRemovePhotoModal);
    }
    if (btnCancelRemovePhoto) {
        btnCancelRemovePhoto.addEventListener("click", closeRemovePhotoModal);
    }
    if (removePhotoOverlay) {
        removePhotoOverlay.addEventListener("click", function (e) {
            if (e.target === removePhotoOverlay) {
                closeRemovePhotoModal();
            }
        });
    }

    if (btnConfirmRemovePhoto) {
        btnConfirmRemovePhoto.addEventListener("click", function () {
            // 1. Close modal
            closeRemovePhotoModal();

            // 2. Send update to server with empty/null path
            fetch("/Settings/UpdateTheme", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    profilePhotoPath: "" // Empty string signals removal
                })
            })
            .then(res => {
                if (res.ok) {
                    // 3. Clear Local Storage & Update UI
                    localStorage.removeItem(PROFILE_PHOTO_KEY);
                    
                    // Manually reset UI immediately without reload
                    var headerImg = document.getElementById("appProfileImage");
                    var avatarCircle = document.getElementById("accountAvatarCircle");
                    
                    if (headerImg) {
                       // We need to reload or manually set to initials/default image logic.
                       // For simplicity, setting to default or empty and letting applyProfileText handle initials
                       // is tricky because applyPhotos runs on load.
                       // Let's re-run applyProfileText to regenerate initials if needed,
                       // but applyPhotos prioritizes photo. Removing photo key lets applyPhotos show empty.
                       // The issue is applyPhotos doesn't regenerate initials if they are missing from DOM.
                       // Actually, initials are always in textContent of avatarCircle (hidden by bg image).
                       // So removing bg image is enough.
                    }
                    
                    // Re-run applyPhotos will see no local storage key and clear the bg image.
                    applyPhotos(); 
                    
                    showNotif("Profile photo removed.", "success");
                    
                    // Reload to fully sync server-side rendered initials logic in header if implemented
                    setTimeout(() => window.location.reload(), 1000); 
                } else {
                    showNotif("Failed to remove photo.", "error");
                }
            })
            .catch(err => {
                console.error(err);
                showNotif("Error removing photo.", "error");
            });
        });
    }

    /* ---------- Organization photo change ---------- */
    var orgBtn = document.getElementById("btnChangeOrgPhoto");
    var orgInput = document.getElementById("orgPhotoInput");
    if (orgBtn && orgInput) {
      orgBtn.addEventListener("click", function () {
        orgInput.click();
      });
      orgInput.addEventListener("change", function (e) {
        var file = e.target.files && e.target.files[0];
        if (!file) return;

        // 1. Upload to Server immediately
        var formData = new FormData();
        formData.append("file", file);

        fetch("/Settings/UploadPhoto", {
            method: "POST",
            body: formData
        })
        .then(res => {
            if (!res.ok) throw new Error("Upload failed");
            return res.json();
        })
        .then(data => {
            var filePath = data.filePath; // e.g. "/uploads/..."

            // 2. Persist path to User record via UpdateTheme
            return fetch("/Settings/UpdateTheme", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    organizationPhotoPath: filePath
                })
            }).then(res => {
                if (res.ok) return filePath;
                throw new Error("Failed to save photo path");
            });
        })
        .then(filePath => {
            // 3. Update Local Storage & UI
            localStorage.setItem(ORG_PHOTO_KEY, filePath);
            applyPhotos();
            showNotif("Organization photo updated.", "success");
        })
        .catch(err => {
            console.error(err);
            showNotif("Error updating photo.", "error");
        });
      });
    }

    /* ---------- Logout ---------- */
    var logoutBtn = document.getElementById("btnLogout");
    var logoutOverlay = document.getElementById("logout-overlay");
    var btnCloseLogout = document.getElementById("btn-close-logout");
    var btnCancelLogout = document.getElementById("btnCancelLogout");

    function openLogoutModal() {
        if (logoutOverlay) {
            logoutOverlay.classList.add("open");
            logoutOverlay.setAttribute("aria-hidden", "false");
        }
    }

    function closeLogoutModal() {
        if (logoutOverlay) {
            logoutOverlay.classList.remove("open");
            logoutOverlay.setAttribute("aria-hidden", "true");
        }
    }

    if (logoutBtn) {
        logoutBtn.addEventListener("click", openLogoutModal);
    }
    if (btnCloseLogout) {
        btnCloseLogout.addEventListener("click", closeLogoutModal);
    }
    if (btnCancelLogout) {
        btnCancelLogout.addEventListener("click", closeLogoutModal);
    }
    if (logoutOverlay) {
        logoutOverlay.addEventListener("click", function (e) {
            if (e.target === logoutOverlay) {
                closeLogoutModal();
            }
        });
    }

    /* ---------- Deactivate Account Modal ---------- */
    var deactivateBtn = document.getElementById("btnDeactivateAccount");
    var deactivateOverlay = document.getElementById("deactivate-confirm-modal-overlay");
    var deactivateCloseBtn = document.getElementById("btn-close-deactivate");
    var cancelDeactivateBtn = document.getElementById("btnCancelDeactivate");
    var deactivateForm = document.getElementById("deactivateForm");
    
    // Incorrect Password Modal Elements
    var incorrectPassOverlay = document.getElementById("incorrect-password-modal-overlay");
    var btnCloseIncorrectPass = document.getElementById("btn-close-incorrect-password");
    var btnOkIncorrectPass = document.getElementById("btnOkIncorrectPassword");

    function openDeactivateModal() {
        if (deactivateOverlay) {
            deactivateOverlay.classList.add("open");
            deactivateOverlay.setAttribute("aria-hidden", "false");
            // Clear password field
            var passField = document.getElementById("deactivatePassword");
            if (passField) passField.value = "";
        }
    }

    function closeDeactivateModal() {
        if (deactivateOverlay) {
            deactivateOverlay.classList.remove("open");
            deactivateOverlay.setAttribute("aria-hidden", "true");
        }
    }

    function openIncorrectPassModal() {
        if (incorrectPassOverlay) {
            incorrectPassOverlay.classList.add("open");
            incorrectPassOverlay.setAttribute("aria-hidden", "false");
        }
    }

    function closeIncorrectPassModal() {
        if (incorrectPassOverlay) {
            incorrectPassOverlay.classList.remove("open");
            incorrectPassOverlay.setAttribute("aria-hidden", "true");
        }
    }

    if (deactivateBtn) {
        deactivateBtn.addEventListener("click", openDeactivateModal);
    }
    if (deactivateCloseBtn) {
        deactivateCloseBtn.addEventListener("click", closeDeactivateModal);
    }
    if (cancelDeactivateBtn) {
        cancelDeactivateBtn.addEventListener("click", closeDeactivateModal);
    }
    if (deactivateOverlay) {
        deactivateOverlay.addEventListener("click", function (e) {
            if (e.target === deactivateOverlay) {
                closeDeactivateModal();
            }
        });
    }

    // Wire up Incorrect Password Modal
    if (btnCloseIncorrectPass) btnCloseIncorrectPass.addEventListener("click", closeIncorrectPassModal);
    if (btnOkIncorrectPass) btnOkIncorrectPass.addEventListener("click", closeIncorrectPassModal);
    if (incorrectPassOverlay) {
        incorrectPassOverlay.addEventListener("click", function (e) {
            if (e.target === incorrectPassOverlay) closeIncorrectPassModal();
        });
    }

    // Deactivation Success Modal Elements
    var deactivateSuccessOverlay = document.getElementById("deactivate-success-modal-overlay");
    var btnOkDeactivateSuccess = document.getElementById("btnOkDeactivateSuccess");

    function openDeactivateSuccessModal() {
        if (deactivateSuccessOverlay) {
            deactivateSuccessOverlay.classList.add("open");
            deactivateSuccessOverlay.setAttribute("aria-hidden", "false");
        }
    }

    if (btnOkDeactivateSuccess) {
        btnOkDeactivateSuccess.addEventListener("click", function() {
            window.location.href = "/Auth/Login";
        });
    }

    // Handle Form Submission
    if (deactivateForm) {
        deactivateForm.addEventListener("submit", function(e) {
            e.preventDefault();
            
            // Use FormData from the form directly to ensure correct token and password
            var formData = new FormData(deactivateForm);
            
            // Check password existence in formData just in case
            if (!formData.get("password")) return;

            // Show loading state
            var submitBtn = document.querySelector('button[form="deactivateForm"]'); // Button is outside form
            var originalText = submitBtn ? submitBtn.textContent : "Yes, Deactivate";
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.textContent = "Processing...";
            }

            fetch("/Settings/DeactivateAccount", {
                method: "POST",
                body: formData
            })
            .then(res => {
                if (!res.ok) throw new Error("Network response was not ok");
                return res.json();
            })
            .then(data => {
                if (data.success) {
                    // Success -> Close Deactivate Modal -> Open Success Modal
                    closeDeactivateModal();
                    openDeactivateSuccessModal();
                } else {
                    // Failure
                    if (data.reason === "password") {
                        // Close deactivate modal (optional, but requested behavior implies showing error)
                        // closeDeactivateModal(); // Keep it open? Or close? User asked to "show a modal... saying incorrect password".
                        // Usually better to keep the context open, but let's follow the standard pattern of stacking or replacing.
                        // We will just open the error modal on top.
                        openIncorrectPassModal();
                    } else {
                        showNotif(data.message || "An error occurred.", "error");
                    }
                }
            })
            .catch(err => {
                console.error(err);
                showNotif("An error occurred. Please try again.", "error");
            })
            .finally(() => {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.textContent = originalText;
                }
                // Clear password field for security
                var passField = document.getElementById("deactivatePassword");
                if (passField) passField.value = "";
            });
        });
    }
  });
})();