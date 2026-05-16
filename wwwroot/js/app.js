// wwwroot/js/app.js

// GLOBAL wizard state – these are used in Create Event wizard + Manage
let wizardContestants = [];   // [{ id, name, organization, photoUrl }]
let wizardAccessUsers = [];   // [{ id, name, assigned, pin }]
let wizardCriteria    = {};   // criteria-based events config
let wizardRounds      = [];   // list of rounds
let wizardPointing    = {};   // ORW pointing system

let currentEventId    = null; // null = new, number = editing

document.addEventListener("DOMContentLoaded", function () {
  console.log("app.js loaded");

  /* =========================================================
   * GLOBAL LOADER LOGIC
   * =======================================================*/
  const loader = document.getElementById("global-loader");
  
  window.showLoader = function() {
    if (loader) loader.classList.add("is-visible");
  };

  window.hideLoader = function() {
    if (loader) loader.classList.remove("is-visible");
  };

  // 1. Auto-show on AJAX (jQuery)
  if (typeof $ !== 'undefined') {
    $(document).ajaxStart(function () {
        window.showLoader();
    }).ajaxStop(function () {
        window.hideLoader();
    });
  }

  // 2. Auto-show on Form Submit (unless strictly prevented or invalid)
  // Note: We use delegation or direct attachment.
  document.querySelectorAll("form").forEach(form => {
    form.addEventListener("submit", function(e) {
      if (!e.defaultPrevented && form.checkValidity()) {
         // Check if it's a download or target blank
         if (form.target !== "_blank") {
             window.showLoader();
         }
      }
    });
  });

  // 3. Expose to global window for manual use if needed
  // (Done above via window.showLoader)

  /* =========================================================
   * FILTER SELECT DROPDOWNS (Events + Audit Logs)
   * =======================================================*/

  var selects = document.querySelectorAll(".filter-select");

  function closeAllSelects(except) {
    for (var i = 0; i < selects.length; i++) {
      if (selects[i] !== except) {
        selects[i].classList.remove("filter-select--open");
      }
    }
  }

  for (var i = 0; i < selects.length; i++) {
    (function (select) {
      var trigger   = select.querySelector(".filter-select-trigger");
      var menu      = select.querySelector(".filter-select-menu");
      var labelSpan = select.querySelector("[data-filter-label]");

      if (!trigger || !menu) return;

      // open / close this dropdown
      trigger.addEventListener("click", function (e) {
        e.stopPropagation();
        var isOpen = select.classList.contains("filter-select--open");
        closeAllSelects(select);
        if (!isOpen) {
          select.classList.add("filter-select--open");
        } else {
          select.classList.remove("filter-select--open");
        }
      });

      // handle options
      var options = menu.querySelectorAll(".filter-select-option");
      for (var j = 0; j < options.length; j++) {
        (function (option) {
          option.addEventListener("click", function (e) {
            e.stopPropagation();

            // mark selected
            for (var k = 0; k < options.length; k++) {
              options[k].classList.remove("is-selected");
            }
            option.classList.add("is-selected");

            // update label
            if (labelSpan) {
              labelSpan.textContent = option.textContent.trim();
            }

            // close
            select.classList.remove("filter-select--open");

            // NOTE: real filtering is handled server-side (querystring)
          });
        })(options[j]);
      }
    })(selects[i]);
  }

  /* =========================================================
   * CREATE EVENT buttons redirect (Dashboard + Events)
   * =======================================================*/

  var createButtons = document.querySelectorAll(
    ".dashboard-create-btn, " +
      ".dashboard-create-event-btn, " +
      ".create-event-button, " +
      "#btnCreateEventDashboard, " +
      "#btnCreateEventEvents"
  );

  createButtons.forEach(function (btn) {
    btn.addEventListener("click", function () {
      window.location.href = "/Home/CreateEvent";
    });
  });

  /* =========================================================
   * Close all filter dropdowns on outside click
   * =======================================================*/
  document.addEventListener("click", function () {
    closeAllSelects();
  });

  /* =========================================================
   * LOAD EXISTING EVENT DATA (for Edit)
   * =======================================================*/

  const hiddenId        = document.getElementById("existingEventId");
  const hiddenContest   = document.getElementById("existingContestants");
  const hiddenAccess    = document.getElementById("existingAccessUsers");
  const hiddenCriteria  = document.getElementById("existingCriteria");
  const hiddenRounds    = document.getElementById("existingRounds");
  const hiddenPointing  = document.getElementById("existingPointing");

  // NEW HIDDEN INPUTS (basic event details)
  const existingEventNameEl        = document.getElementById("existingEventName");
  const existingEventVenueEl       = document.getElementById("existingEventVenue");
  const existingEventDescriptionEl = document.getElementById("existingEventDescription");
  const existingEventStartDateEl   = document.getElementById("existingEventStartDate");
  const existingEventStartTimeEl   = document.getElementById("existingEventStartTime");
  const existingThemeColorEl       = document.getElementById("existingThemeColor");
  const existingHeaderImageEl      = document.getElementById("existingHeaderImage");

  if (hiddenId) {
    const parsedId = parseInt(hiddenId.value || "0", 10);
    if (!isNaN(parsedId) && parsedId > 0) {
      currentEventId = parsedId;
      window.currentEventId = parsedId; // Make it globally accessible
    }
  }

  // Assign basic event details to global window variables
  if (existingEventNameEl)        window.existingEventName        = existingEventNameEl.value;
  if (existingEventVenueEl)       window.existingEventVenue       = existingEventVenueEl.value;
  if (existingEventDescriptionEl) window.existingEventDescription = existingEventDescriptionEl.value;
  if (existingEventStartDateEl)   window.existingEventStartDate   = existingEventStartDateEl.value;
  if (existingEventStartTimeEl)   window.existingEventStartTime   = existingEventStartTimeEl.value;
  if (existingThemeColorEl)       window.existingThemeColor       = existingThemeColorEl.value;
  if (existingHeaderImageEl)      window.existingHeaderImage      = existingHeaderImageEl.value;
  
  // Existing specific event properties
  if (document.getElementById("existingEventType")) window.existingEventType = document.getElementById("existingEventType").value;
  if (document.getElementById("existingAccessCode")) window.existingAccessCode = document.getElementById("existingAccessCode").value;


  try {
    if (hiddenContest && hiddenContest.value) {
      wizardContestants = JSON.parse(hiddenContest.value);
      window.wizardContestants = wizardContestants; // Make global
    }
    if (hiddenAccess && hiddenAccess.value) {
      wizardAccessUsers = JSON.parse(hiddenAccess.value);
      window.wizardAccessUsers = wizardAccessUsers; // Make global
    }
    if (hiddenCriteria && hiddenCriteria.value) {
      wizardCriteria = JSON.parse(hiddenCriteria.value);
      window.wizardCriteria = wizardCriteria; // Make global
    }
    if (hiddenRounds && hiddenRounds.value) {
      wizardRounds = JSON.parse(hiddenRounds.value);
      window.wizardRounds = wizardRounds; // Make global
    }
    if (hiddenPointing && hiddenPointing.value) {
      wizardPointing = JSON.parse(hiddenPointing.value);
      window.wizardPointing = wizardPointing; // Make global
    }
  } catch (e) {
    console.error("Failed to parse existing event JSON", e);
  }

  // TODO: here you can take wizardContestants / wizardAccessUsers / etc.
  // and actually paint them into your wizard UI (repeaters, fields, etc.)

  /* =========================================================
   * CREATE EVENT – payload builder (optional helper)
   * =======================================================*/

  // If you already have a function like this elsewhere, adapt this logic there.
  window.createEventAndSave = async function () {
    const nameInput  = document.getElementById("eventName");
    const venueInput = document.getElementById("eventVenue");
    const descInput  = document.getElementById("eventDescription");
    const startDate  = document.getElementById("eventStartDate");
    const startTime  = document.getElementById("eventStartTime");
    const endDate    = document.getElementById("eventEndDate");
    const endTime    = document.getElementById("eventEndTime");
    const codeInput  = document.getElementById("eventAccessCode");

    const payload = {
      eventId:          currentEventId || null,
      eventName:        nameInput?.value || "",
      eventVenue:       venueInput?.value || "",
      eventDescription: descInput?.value || "",
      eventStartDate:   startDate?.value || "",
      eventStartTime:   startTime?.value || "",
      eventEndDate:     endDate?.value || "",
      eventEndTime:     endTime?.value || "",
      eventType:        typeof getSelectedEventType === "function"
                          ? getSelectedEventType()
                          : "criteria",
      accessCode:       codeInput?.value.trim() || "",

      // JSON blobs from wizard state
      contestantsJson:    JSON.stringify(wizardContestants || []),
      accessJson:         JSON.stringify(wizardAccessUsers || []),
      criteriaJson:       JSON.stringify(wizardCriteria || {}),
      roundsJson:         JSON.stringify(wizardRounds || []),
      pointingSystemJson: JSON.stringify(wizardPointing || {})
    };

    try {
      const response = await fetch("/Events/CreateFromWizard", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        const err = await response.json().catch(() => null);
        alert(err?.message || "Failed to save event.");
        return;
      }

      const result = await response.json();
      if (result.success && result.redirectUrl) {
        window.location.href = result.redirectUrl;
      } else {
        alert(result.message || "Failed to save event.");
      }
    } catch (e) {
      console.error(e);
      alert("Something went wrong while saving the event.");
    }
  };
});
