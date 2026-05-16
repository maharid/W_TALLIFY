// wwwroot/js/create-event.js

document.addEventListener("DOMContentLoaded", function () {
    const LOCAL_STORAGE_KEY = "tallify_create_event_draft";
    const root = document.querySelector(".app-page--create-event");
    if (!root) return;

    /* =========================================================
     * STATE
     * =======================================================*/
    let currentStep = 1;
    const totalSteps = 5;
    let eventThemeColor = "#ff007a";
    window.selectedHeaderImageFileName = "";
    let usedJudgePins = new Set();
    let roundCounter = 0;

    const stepIndexEl  = document.getElementById("eventStepIndex");
    const stepTotalEl  = document.getElementById("eventStepTotal");
    const stepPillText = document.getElementById("eventStepPillText");
    const btnNext = document.getElementById("btnStepNext");
    const btnBack = document.getElementById("btnStepBack");
    const criteriaRoundsList = document.getElementById("criteriaRoundsList");
    const contestantsBody = document.getElementById("contestantsBody");
    const judgesBody = document.getElementById("judgesBody");
    const publishLoadingOverlay = document.getElementById("publishLoadingOverlay");

    if (stepTotalEl) stepTotalEl.textContent = totalSteps;

    /* =========================================================
     * UPPERCASE ENFORCEMENT
     * =======================================================*/
    root.addEventListener("input", (e) => {
        const target = e.target;
        const uppercaseSelectors = [
            "#eventName",
            "#eventVenue",
            "#eventAccessCode",
            ".event-round-name",
            ".criteria-name",
            ".contestant-name",
            ".contestant-org",
            ".judge-name"
        ];

        if (uppercaseSelectors.some(selector => target.matches(selector))) {
            const start = target.selectionStart;
            const end = target.selectionEnd;
            target.value = target.value.toUpperCase();
            target.setSelectionRange(start, end);
        }
    });

    /* =========================================================
     * UI HELPERS (Toast & Errors)
     * =======================================================*/
    function showToast(msg, type = "error") {
        const container = document.getElementById("toast-container");
        if (!container) return;

        // PREVENT DUPLICATE TOASTS
        const existing = Array.from(container.querySelectorAll(".toast-item")).find(t => t.textContent === msg);
        if (existing) return;

        const t = document.createElement("div");
        t.className = `toast-item toast-${type}`;
        t.textContent = msg;
        container.appendChild(t);
        setTimeout(() => t.classList.add("is-visible"), 10);
        setTimeout(() => { 
            t.classList.remove("is-visible"); 
            setTimeout(() => t.remove(), 300); 
        }, 4000);
    }

    function setError(id, msg) {
        const input = document.getElementById(id);
        const field = input?.closest(".event-field") || document.querySelector(`[data-error-for="${id}"]`)?.closest(".event-field");
        if (!field) return;
        
        let err = field.querySelector(".event-error-message");
        if (!err) {
            err = document.createElement("div");
            err.className = "event-error-message";
            const header = field.querySelector(".event-field-header");
            if (header) header.appendChild(err);
            else field.prepend(err);
        }
        err.textContent = msg;
        err.style.display = "block";
        input?.classList.add("invalid");
    }

    function clearAllErrors() {
        document.querySelectorAll(".event-error-message").forEach(el => el.style.display = "none");
        document.querySelectorAll(".event-input.invalid").forEach(el => el.classList.remove("invalid"));
    }

    /* =========================================================
     * NAVIGATION & UI LABELS
     * =======================================================*/
    function stepLabel(step) {
        switch (step) {
            case 1: return "Event Identity";
            case 2: return "Scoring Logic";
            case 3: return "Contestant Roster";
            case 4: return "Judging Panel";
            case 5: return "Branding & Review";
            default: return "";
        }
    }

    function updateStepUI(step) {
        if (stepPillText) stepPillText.textContent = stepLabel(step);
        if (btnNext) {
            if (step === 5) {
                btnNext.textContent = "Finalize & Send Judge Invites";
                btnNext.style.backgroundColor = "var(--color-primary)";
            } else {
                btnNext.textContent = "Next";
                btnNext.style.backgroundColor = "";
            }
        }
        if (btnBack) {
            btnBack.style.display = "inline-flex";
            btnBack.textContent = step === 1 ? "Cancel" : "Back";
        }
    }

    function showStep(step) {
        currentStep = step;
        document.querySelectorAll(".event-step").forEach(s => {
            s.classList.toggle("is-active", Number(s.dataset.step) === step);
        });
        if (stepIndexEl) stepIndexEl.textContent = step;
        updateStepUI(step);
        if (step === 5) populateReview();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    /* =========================================================
     * STEP 1 — IDENTITY VALIDATION
     * =======================================================*/
    function validateStep1() {
        clearAllErrors();
        let valid = true;
        const name = document.getElementById("eventName").value.trim();
        const venue = document.getElementById("eventVenue").value.trim();
        const startDate = document.getElementById("eventStartDate").value;
        const startTime = document.getElementById("eventStartTime").value;

        if (!name) { setError("eventName", "Required"); valid = false; }
        if (!venue) { setError("eventVenue", "Required"); valid = false; }
        if (!startDate || !startTime) { setError("eventStart", "Required"); valid = false; }
        else {
            const dt = new Date(`${startDate}T${startTime}`);
            if (dt <= new Date()) { setError("eventStart", "Must be in the future"); valid = false; }
        }
        if (!valid) showToast("Please check the highlighted fields.");
        return valid;
    }

    /* =========================================================
     * STEP 2 — SCORING BRAIN (Rounds & Criteria)
     * =======================================================*/
    function createCriteriaRound() {
        roundCounter++;
        const round = document.createElement("div");
        round.className = "event-round-card";
        round.innerHTML = `
            <div class="event-round-header">
                <div class="event-round-header-left">
                    <span class="event-round-index">${roundCounter}</span>
                    <input class="event-input event-round-name" placeholder="Round Name (e.g. Elimination)" value="Round ${roundCounter}" />
                </div>
                <button type="button" class="btn-danger-soft btn-small event-round-remove"><i class="ri-delete-bin-line"></i></button>
            </div>
            <div class="criteria-blocks"></div>
            
            <div class="event-section-actions-row" style="margin-top:16px; display:flex; gap:12px; align-items:center; justify-content:flex-start;">
                <button type="button" class="btn-primary-soft btn-small btn-add-criteria" style="display:inline-flex; align-items:center; justify-content:center; height:38px; margin:0; padding:0 16px; box-sizing:border-box; line-height:1; vertical-align:middle;"><i class="ri-add-line"></i> Add Criteria</button>
                <button type="button" class="btn-secondary-soft btn-small btn-copy-criteria" style="display:none; align-items:center; justify-content:center; height:38px; margin:0; padding:0 16px; box-sizing:border-box; line-height:1; vertical-align:middle;"><i class="ri-file-copy-line"></i>&nbsp;&nbsp;Copy Previous</button>
            </div>

            <div class="weight-tracker-container" style="margin-top:16px; display:flex; align-items:center; gap:12px;">
                <p class="event-subtext weight-status-label" style="font-size:12px; font-weight:700; color:#374151; margin-bottom:0; white-space:nowrap;">Total Weight:</p>
                <div class="weight-tracker" style="height:24px; flex-grow:1; background:#f3f4f6; border-radius:12px; position:relative; overflow:hidden; border:1px solid #e5e7eb;">
                    <div class="weight-tracker-bar" style="height:100%; width:0%; transition:width 0.3s ease; position:absolute; top:0; left:0; z-index:1;"></div>
                    <div class="weight-tracker-text" style="position:absolute; top:0; left:0; width:100%; height:100%; display:flex; align-items:center; justify-content:center; font-size:11px; font-weight:700; color:#000; z-index:2;">0%</div>
                </div>
            </div>
        `;
        
        round.querySelector(".event-round-remove").addEventListener("click", () => {
            if (criteriaRoundsList.children.length > 1) {
                round.remove();
                renumberRounds();
            } else {
                showToast("Your competition needs at least one round.");
            }
        });

        criteriaRoundsList.appendChild(round);
        const container = round.querySelector(".criteria-blocks");
        addCriteriaBlock(container);
        
        round.querySelector(".btn-add-criteria").addEventListener("click", () => addCriteriaBlock(container));
        
        round.querySelector(".btn-copy-criteria").addEventListener("click", () => {
            const blocks = container.querySelectorAll(".criteria-block");
            if (blocks.length > 0) {
                const last = blocks[blocks.length - 1];
                const data = {
                    name: last.querySelector(".criteria-name").value,
                    weight: last.querySelector(".criteria-weight").value,
                    min: last.querySelector(".min-point").value,
                    max: last.querySelector(".max-point").value
                };
                addCriteriaBlock(container, data);
            }
        });

        updateWeightTracker(round);
    }

    function addCriteriaBlock(container, data = null) {
        const roundCard = container.closest(".event-round-card");
        const roundIndex = Array.from(criteriaRoundsList.children).indexOf(roundCard);

        const block = document.createElement("div");
        block.className = "criteria-block";
        block.innerHTML = `
            <div class="criteria-row-main" style="display:flex; gap:10px; margin-bottom:10px; align-items:flex-end;">
                ${roundIndex > 0 ? `
                <div style="flex:1.5;">
                    <label class="event-label" style="font-size:11px;">Derived From</label>
                    <select class="event-input criteria-derived-from" style="padding: 0 12px; height: 42px;">
                        <option value="">None</option>
                    </select>
                </div>
                ` : ''}
                <div style="flex:2;">
                    <label class="event-label" style="font-size:11px;">Criteria Name</label>
                    <input class="event-input criteria-name" placeholder="e.g. Talent" value="${data ? data.name : ''}" ${data && data.derivedFrom ? 'readonly' : ''} />
                </div>
                <div class="criteria-weight-field" style="flex:1;">
                    <label class="event-label" style="font-size:11px;">Weight %</label>
                    <input type="number" class="event-input criteria-weight" placeholder="0" min="1" max="100" value="${data ? data.weight : ''}" />
                </div>
                <button type="button" class="btn-danger-soft criteria-remove" style="height:42px; width:42px; display:flex; align-items:center; justify-content:center;"><i class="ri-close-line"></i></button>
            </div>
            <div class="criteria-points-row" style="display:${data && data.derivedFrom ? 'none' : 'flex'}; gap:10px; margin-top:8px;">
                <div class="event-field" style="flex:1;"><label class="event-label" style="font-size:11px;">Min Score</label><input type="number" class="event-input min-point" value="${data ? data.min : '0'}" /></div>
                <div class="event-field" style="flex:1;"><label class="event-label" style="font-size:11px;">Max Score</label><input type="number" class="event-input max-point" value="${data ? data.max : '100'}" /></div>
            </div>
        `;

        const derivedSelect = block.querySelector(".criteria-derived-from");
        const nameInput = block.querySelector(".criteria-name");
        const pointsRow = block.querySelector(".criteria-points-row");

        if (derivedSelect) {
            populateDerivedOptions(derivedSelect, roundIndex, data ? data.derivedFrom : null);

            derivedSelect.addEventListener("change", () => {
                const val = derivedSelect.value;
                if (val) {
                    const sourceRoundName = derivedSelect.options[derivedSelect.selectedIndex].text;
                    nameInput.value = sourceRoundName.toUpperCase();
                    nameInput.readOnly = true;
                    pointsRow.style.display = "none";
                } else {
                    nameInput.readOnly = false;
                    pointsRow.style.display = "flex";
                }
            });
        }

        block.querySelector(".criteria-remove").addEventListener("click", () => {
            const roundCard = container.closest(".event-round-card");
            if (container.children.length > 1) {
                block.remove();
                updateWeightTracker(roundCard);
            } else {
                showToast("A round must have at least one criteria.");
            }
        });

        const weightInput = block.querySelector(".criteria-weight");
        weightInput?.addEventListener("input", () => {
            if (weightInput.value > 100) weightInput.value = 100;
            updateWeightTracker(container.closest(".event-round-card"));
        });

        container.appendChild(block);
        updateCriteriaVisibility();
        updateWeightTracker(container.closest(".event-round-card"));
    }

    function populateDerivedOptions(select, currentRoundIndex, selectedValue = null) {
        const rounds = Array.from(criteriaRoundsList.querySelectorAll(".event-round-card"));
        const currentValue = select.value || selectedValue;
        
        select.innerHTML = '<option value="">None</option>';
        for (let i = 0; i < currentRoundIndex; i++) {
            const rName = rounds[i].querySelector(".event-round-name").value || `Round ${i + 1}`;
            const opt = document.createElement("option");
            opt.value = i + 1; // 1-based index
            opt.textContent = rName;
            if (currentValue == opt.value) opt.selected = true;
            select.appendChild(opt);
        }
    }

    function updateWeightTracker(roundCard) {
        if (!roundCard) return;
        const weights = Array.from(roundCard.querySelectorAll(".criteria-weight")).map(i => parseFloat(i.value) || 0);
        const total = weights.reduce((a, b) => a + b, 0);
        const bar = roundCard.querySelector(".weight-tracker-bar");
        const textOverlay = roundCard.querySelector(".weight-tracker-text");
        const isPointing = document.getElementById("criteriaSystemPointing").checked;

        // Toggle Copy Previous Button
        const blocks = roundCard.querySelectorAll(".criteria-block");
        const btnCopy = roundCard.querySelector(".btn-copy-criteria");
        if (btnCopy) btnCopy.style.display = blocks.length > 0 ? "inline-flex" : "none";

        // Sync Derived Dropdowns
        const roundIndex = Array.from(criteriaRoundsList.children).indexOf(roundCard);
        roundCard.querySelectorAll(".criteria-derived-from").forEach(sel => {
            populateDerivedOptions(sel, roundIndex);
        });

        if (isPointing) {
            roundCard.querySelector(".weight-tracker-container").style.display = "none";
            return;
        }

        roundCard.querySelector(".weight-tracker-container").style.display = "flex";
        const displayTotal = Math.min(total, 100);
        bar.style.width = displayTotal + "%";
        textOverlay.textContent = Math.round(total) + "%";

        bar.classList.remove("is-perfect", "is-over", "is-under");
        if (total === 100) {
            bar.classList.add("is-perfect");
            bar.style.backgroundColor = "#10b981";
        } else if (total > 100) {
            bar.classList.add("is-over");
            bar.style.backgroundColor = "#ef4444";
        } else {
            bar.classList.add("is-under");
            bar.style.backgroundColor = "#f59e0b";
        }

        // Keep text black as requested
        textOverlay.style.color = "#000";
        textOverlay.style.textShadow = "none";

        if (total === 0) {
            textOverlay.style.color = "#374151";
        }
    }

    function renumberRounds() {
        const rounds = criteriaRoundsList.querySelectorAll(".event-round-card");
        rounds.forEach((r, i) => r.querySelector(".event-round-index").textContent = i + 1);
        roundCounter = rounds.length;
    }

    function validateStep2() {
        const isAveraging = document.getElementById("criteriaSystemAveraging").checked;
        const rounds = criteriaRoundsList.querySelectorAll(".event-round-card");
        if (rounds.length === 0) { showToast("Please add at least one round."); return false; }
        
        let valid = true;
        rounds.forEach(r => {
            const name = r.querySelector(".event-round-name").value.trim();
            if (!name) { showToast("Missing round name."); valid = false; }
            if (isAveraging) {
                const weights = Array.from(r.querySelectorAll(".criteria-weight")).map(i => parseFloat(i.value) || 0);
                const total = Math.round(weights.reduce((a, b) => a + b, 0));
                if (total !== 100) { 
                    showToast(`Round "${name || 'Unnamed'}" total weight must be exactly 100%. Currently ${total}%.`); 
                    valid = false; 
                }
            }
            // Check criteria names
            r.querySelectorAll(".criteria-name").forEach(cn => {
                if (!cn.value.trim()) { showToast("All criteria must have a name."); valid = false; }
            });
        });
        return valid;
    }

    /* =========================================================
     * STEP 3 — CONTESTANTS
     * =======================================================*/
    function addContestantRow() {
        const tr = document.createElement("tr");
        tr.dataset.contestantRow = "true";
        tr.innerHTML = `
            <td class="col-id" style="font-weight:600; color:#6b7280;"></td>
            <td><input class="event-input contestant-name" placeholder="e.g. Jane Doe" /></td>
            <td><input class="event-input contestant-org" placeholder="e.g. Science Club" /></td>
            <td>
                <div style="display:flex; align-items:center; gap:12px;">
                    <div class="contestant-photo-thumb" style="width:40px; height:40px; border-radius:8px; background:#FF007F; background-size:cover; background-position:center; border:1px solid #e5e7eb;" data-photo-url=""></div>
                    <button type="button" class="btn-outline btn-small btn-upload-photo" style="font-size:11px; padding:4px 8px; border-radius:8px;">Upload photo</button>
                    <input type="file" accept="image/*" class="contestant-photo-input" hidden />
                </div>
            </td>
            <td><button type="button" class="btn-danger-soft btn-small btn-remove-contestant" style="color:#991b1b; background-color:#fee2e2; border-radius:8px; font-weight:600; padding:6px 12px; border:none;">Remove</button></td>
        `;
        
        const nameInput = tr.querySelector(".contestant-name");
        const orgInput = tr.querySelector(".contestant-org");
        const photoBtn = tr.querySelector(".btn-upload-photo");
        const photoInput = tr.querySelector(".contestant-photo-input");
        const thumb = tr.querySelector(".contestant-photo-thumb");

        // REMOVE RED HIGHLIGHT ON TYPING
        nameInput.addEventListener("input", () => nameInput.classList.remove("is-invalid-red"));
        orgInput.addEventListener("input", () => orgInput.classList.remove("is-invalid-red"));

        photoBtn.addEventListener("click", () => photoInput.click());

        photoInput.addEventListener("change", async () => {
            const file = photoInput.files[0];
            if (!file) return;
            if (file.size > 5 * 1024 * 1024) { showToast("Photo exceeds 5MB limit."); return; }

            photoBtn.textContent = "...";
            const formData = new FormData();
            formData.append("file", file);
            try {
                const res = await fetch("/Events/UploadImage", { method: "POST", body: formData });
                const data = await res.json();
                if (data.success) {
                    const path = "/uploads/" + data.fileName;
                    thumb.style.backgroundImage = `url('${path}')`;
                    thumb.dataset.photoUrl = path;
                    saveDraft();
                } else { showToast(data.message); }
            } catch (e) { showToast("Upload failed."); }
            photoBtn.textContent = "Upload photo";
        });

        tr.querySelector(".btn-remove-contestant").addEventListener("click", () => {
            tr.remove();
            renumberContestants();
        });
        contestantsBody.appendChild(tr);
        renumberContestants();
    }

    function renumberContestants() {
        contestantsBody.querySelectorAll("tr").forEach((tr, i) => {
            const idCell = tr.querySelector(".col-id");
            if (idCell) idCell.textContent = "C" + String(i + 1).padStart(3, "0");
        });
    }

    function validateStep3() {
        const rows = Array.from(contestantsBody.querySelectorAll("tr[data-contestant-row]"));
        
        const completeCount = rows.filter(r => 
            r.querySelector(".contestant-name").value.trim() !== "" && 
            r.querySelector(".contestant-org").value.trim() !== ""
        ).length;

        if (completeCount < 2) {
            showToast("At least two are required");
            return false;
        }

        // Check for validity of all rows
        for (let r of rows) {
            const nameEl = r.querySelector(".contestant-name");
            const orgEl = r.querySelector(".contestant-org");
            const name = nameEl.value.trim();
            const org = orgEl.value.trim();
            
            if (!name) {
                showToast("Name is required");
                nameEl.classList.add("is-invalid-red");
                return false;
            }
            if (!org) {
                showToast("Organization is required");
                orgEl.classList.add("is-invalid-red");
                return false;
            }
        }

        if (rows.length < 2) { 
            showToast("At least two is required"); 
            return false; 
        }
        return true;
    }

    /* =========================================================
     * STEP 4 — JUDGING PANEL
     * =======================================================*/
    function addJudgeRow() {
        const tr = document.createElement("tr");
        tr.dataset.judgeRow = "true";
        tr.innerHTML = `
            <td class="col-id" style="font-weight:600; color:#6b7280;"></td>
            <td><input class="event-input judge-name" placeholder="e.g. John Doe" /></td>
            <td><input class="event-input judge-email" placeholder="email@example.com" /></td>
            <td><span class="judge-pin" style="font-family:monospace; font-weight:700; color:var(--color-primary); background:var(--color-primary-soft-bg); padding:4px 8px; border-radius:4px; letter-spacing:2px;">-----</span></td>
            <td>
                <div style="display:flex; gap:8px;">
                    <button type="button" class="btn-outline btn-small btn-gen-pin" title="Generate PIN">Pin</button>
                    <button type="button" class="btn-danger-soft btn-small btn-remove-judge"><i class="ri-delete-bin-line"></i></button>
                </div>
            </td>
        `;
        
        tr.querySelector(".btn-gen-pin").addEventListener("click", () => {
            const name = tr.querySelector(".judge-name").value.trim();
            const email = tr.querySelector(".judge-email").value.trim();
            if (!name || !email) { showToast("Name and Email required before generating PIN."); return; }
            
            const pin = generatePin();
            tr.querySelector(".judge-pin").textContent = pin;
        });

        tr.querySelector(".btn-remove-judge").addEventListener("click", () => {
            tr.remove();
            renumberJudges();
        });
        judgesBody.appendChild(tr);
        renumberJudges();
    }

    function renumberJudges() {
        judgesBody.querySelectorAll("tr").forEach((tr, i) => {
            const idCell = tr.querySelector(".col-id");
            if (idCell) idCell.textContent = "J" + String(i + 1).padStart(3, "0");
        });
    }

    function generatePin() {
        const chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        let pin = "";
        for (let i = 0; i < 5; i++) pin += chars[Math.floor(Math.random() * chars.length)];
        return pin;
    }

    function validateStep4() {
        const code = document.getElementById("eventAccessCode").value.trim();
        if (!code) { setError("eventAccessCode", "Required"); showToast("Event Access Code is required."); return false; }
        
        const rows = judgesBody.querySelectorAll("tr[data-judge-row]");
        if (rows.length === 0) { showToast("Please add at least one judge."); return false; }
        
        let valid = true;
        rows.forEach(r => {
            const name = r.querySelector(".judge-name").value.trim();
            const email = r.querySelector(".judge-email").value.trim();
            const pin = r.querySelector(".judge-pin").textContent;
            if (!name || !email || pin === "-----") valid = false;
        });
        if (!valid) showToast("Ensure all judges have a name, email, and generated PIN.");
        return valid;
    }

    /* =========================================================
     * STEP 5 — BRANDING & REVIEW
     * =======================================================*/
    function populateReview() {
        const data = getDraftData();
        
        // Identity
        document.getElementById("reviewIdentity").innerHTML = `
            <div style="font-size:14px; color:#374151;">
                <p style="margin-bottom:8px;"><strong>Event Name:</strong> ${data.eventName || "—"}</p>
                <p style="margin-bottom:8px;"><strong>Venue:</strong> ${data.eventVenue || "—"}</p>
                <p><strong>Start Date:</strong> ${data.eventStartDate} at ${data.eventStartTime}</p>
            </div>
        `;

        // Scoring
        document.getElementById("reviewScoring").innerHTML = data.rounds.map(r => `
            <div style="margin-bottom:16px; border-bottom:1px solid #f3f4f6; padding-bottom:12px;">
                <p style="font-weight:600; margin-bottom:6px;">${r.roundName}</p>
                <div style="display:flex; flex-wrap:wrap; gap:8px;">
                    ${r.criteria.map(c => `<span style="background:#eff6ff; color:#1d4ed8; padding:2px 10px; border-radius:20px; font-size:12px;">${c.name}: ${c.weight}%</span>`).join("")}
                </div>
            </div>
        `).join("") || "No rounds configured.";

        document.getElementById("reviewContestants").innerHTML = `<p style="font-size:14px;">Total of <strong>${data.contestants.length}</strong> participants added.</p>`;
        document.getElementById("reviewJudges").innerHTML = `<p style="font-size:14px;">Total of <strong>${data.judges.length}</strong> judges assigned.</p>`;
        
        // Update Preview Window
        const previewTitle = document.getElementById("previewTitle");
        if (previewTitle) previewTitle.textContent = data.eventName || "Your Competition Title";
        const previewHeader = document.getElementById("previewHeader");
        if (previewHeader) {
            previewHeader.style.backgroundColor = data.themeColor;
            if (data.headerImage) {
                previewHeader.style.backgroundImage = `url('${data.headerImage}')`;
                previewHeader.style.backgroundSize = "cover";
            } else {
                previewHeader.style.backgroundImage = "none";
            }
        }
    }

    /* =========================================================
     * DRAFTING SYSTEM
     * =======================================================*/
    function getDraftData() {
        return {
            eventName: document.getElementById("eventName").value.trim(),
            eventVenue: document.getElementById("eventVenue").value.trim(),
            eventDescription: document.getElementById("eventDescription").value.trim(),
            eventStartDate: document.getElementById("eventStartDate").value,
            eventStartTime: document.getElementById("eventStartTime").value,
            eventType: document.querySelector('input[name="criteriaSystem"]:checked')?.value || "averaging",
            accessCode: document.getElementById("eventAccessCode").value.trim(),
            themeColor: eventThemeColor,
            headerImage: window.selectedHeaderImageFileName,
            rounds: Array.from(criteriaRoundsList.querySelectorAll(".event-round-card")).map(r => ({
                roundName: r.querySelector(".event-round-name").value.trim(),
                criteria: Array.from(r.querySelectorAll(".criteria-block")).map(b => ({
                    name: b.querySelector(".criteria-name").value.trim(),
                    weight: parseFloat(b.querySelector(".criteria-weight").value) || 0,
                    min: parseFloat(b.querySelector(".min-point").value) || 0,
                    max: parseFloat(b.querySelector(".max-point").value) || 100
                }))
            })),
            contestants: Array.from(contestantsBody.querySelectorAll("tr[data-contestant-row]")).map(tr => ({
                name: tr.querySelector(".contestant-name").value.trim(),
                org: tr.querySelector(".contestant-org").value.trim(),
                photo: tr.querySelector(".contestant-photo-thumb").dataset.photoUrl
            })),
            judges: Array.from(judgesBody.querySelectorAll("tr[data-judge-row]")).map(tr => ({
                name: tr.querySelector(".judge-name").value.trim(),
                email: tr.querySelector(".judge-email").value.trim(),
                pin: tr.querySelector(".judge-pin").textContent
            }))
        };
    }

    function saveDraft() {
        const data = getDraftData();
        // Only save if there's significant progress
        if (data.eventName || data.rounds.length > 1 || data.contestants.length > 2) {
            localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(data));
        }
    }

    function loadDraft() {
        const raw = localStorage.getItem(LOCAL_STORAGE_KEY);
        if (!raw) return;
        
        const data = JSON.parse(raw);
        if (!data.eventName && data.rounds.length <= 1) return;

        const modalEl = document.getElementById('draftResumeModal');
        const modal = new bootstrap.Modal(modalEl);
        modal.show();

        document.getElementById("btnDraftResume").onclick = () => {
            try {
                // Restore logic
                document.getElementById("eventName").value = data.eventName || "";
                document.getElementById("eventVenue").value = data.eventVenue || "";
                document.getElementById("eventDescription").value = data.eventDescription || "";
                document.getElementById("eventStartDate").value = data.eventStartDate || "";
                document.getElementById("eventStartTime").value = data.eventStartTime || "";

                if (data.eventType === "pointing") document.getElementById("criteriaSystemPointing").checked = true;
                else document.getElementById("criteriaSystemAveraging").checked = true;
                updateCriteriaVisibility();

                if (data.rounds && data.rounds.length) {
                    criteriaRoundsList.innerHTML = "";
                    roundCounter = 0;
                    data.rounds.forEach(r => {
                        createCriteriaRound();
                        const card = criteriaRoundsList.lastElementChild;
                        card.querySelector(".event-round-name").value = r.roundName;
                        const container = card.querySelector(".criteria-blocks");
                        container.innerHTML = "";
                        r.criteria.forEach(c => {
                            addCriteriaBlock(container);
                            const b = container.lastElementChild;
                            b.querySelector(".criteria-name").value = c.name;
                            b.querySelector(".criteria-weight").value = c.weight;
                            b.querySelector(".min-point").value = c.min;
                            b.querySelector(".max-point").value = c.max;
                        });
                        updateWeightTracker(card);
                    });
                }

                if (data.contestants && data.contestants.length) {
                    contestantsBody.innerHTML = "";
                    data.contestants.forEach(c => {
                        addContestantRow();
                        const tr = contestantsBody.lastElementChild;
                        tr.querySelector(".contestant-name").value = c.name;
                        tr.querySelector(".contestant-org").value = c.org;
                        const thumb = tr.querySelector(".contestant-photo-thumb");
                        thumb.dataset.photoUrl = c.photo || "";
                        if (c.photo) thumb.style.backgroundImage = `url('${c.photo}')`;
                    });
                }

                document.getElementById("eventAccessCode").value = data.accessCode || "";
                if (data.judges && data.judges.length) {
                    judgesBody.innerHTML = "";
                    data.judges.forEach(j => {
                        addJudgeRow();
                        const tr = judgesBody.lastElementChild;
                        tr.querySelector(".judge-name").value = j.name;
                        tr.querySelector(".judge-email").value = j.email;
                        tr.querySelector(".judge-pin").textContent = j.pin;
                    });
                }

                eventThemeColor = data.themeColor || "#ff007a";
                window.selectedHeaderImageFileName = data.headerImage || "";
                
                modal.hide();
                showToast("Your progress has been restored.", "success");
            } catch (e) { showToast("Error restoring draft."); modal.hide(); }
        };

        document.getElementById("btnDraftFresh").onclick = () => {
            localStorage.removeItem(LOCAL_STORAGE_KEY);
            modal.hide();
        };
    }

    /* =========================================================
     * THEME & BRANDING
     * =======================================================*/
    document.querySelectorAll(".theme-preset-option").forEach(opt => {
        opt.addEventListener("click", () => {
            document.querySelectorAll(".theme-preset-option").forEach(o => o.classList.remove("is-selected"));
            opt.classList.add("is-selected");
            if (opt.dataset.color === "custom") {
                document.getElementById("eventThemeCustom").click();
            } else {
                eventThemeColor = opt.dataset.color;
                saveDraft();
            }
        });
    });

    document.getElementById("eventThemeCustom")?.addEventListener("input", (e) => {
        eventThemeColor = e.target.value;
        saveDraft();
    });

    document.getElementById("eventHeaderImage")?.addEventListener("change", async (e) => {
        const file = e.target.files[0];
        if (!file) return;
        const formData = new FormData();
        formData.append("file", file);
        try {
            const res = await fetch("/Events/UploadImage", { method: "POST", body: formData });
            const data = await res.json();
            if (data.success) {
                window.selectedHeaderImageFileName = data.fileName;
                document.getElementById("headerFileName").textContent = data.fileName;
                saveDraft();
            }
        } catch (e) { showToast("Logo upload failed."); }
    });

    /* =========================================================
     * NAVIGATION HANDLERS
     * =======================================================*/
    btnNext.addEventListener("click", () => {
        let valid = false;
        if (currentStep === 1) valid = validateStep1();
        else if (currentStep === 2) valid = validateStep2();
        else if (currentStep === 3) valid = validateStep3();
        else if (currentStep === 4) valid = validateStep4();
        else if (currentStep === 5) { finalizeEvent(); return; }
        
        if (valid) showStep(currentStep + 1);
    });

    btnBack.addEventListener("click", () => {
        if (currentStep === 1) {
            window.location.href = "/";
        } else if (currentStep > 1) {
            showStep(currentStep - 1);
        }
    });

    function finalizeEvent() {
        publishLoadingOverlay.style.display = "flex";
        const data = getDraftData();
        const payload = {
            eventName: data.eventName, eventVenue: data.eventVenue, eventDescription: data.eventDescription,
            eventStartDate: data.eventStartDate, eventStartTime: data.eventStartTime, eventType: data.eventType,
            accessCode: data.accessCode, themeColor: data.themeColor, headerImage: data.headerImage,
            contestants: data.contestants.map(c => ({ name: c.name, organization: c.org, photoPath: c.photo })),
            accessUsers: data.judges.map(j => ({ name: j.name, email: j.email, pin: j.pin })),
            roundsJson: JSON.stringify(data.rounds)
        };

        fetch("/Events/CreateFromWizard", {
            method: "POST", headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        }).then(r => r.json()).then(res => {
            if (res.success) {
                localStorage.removeItem(LOCAL_STORAGE_KEY);
                window.location.href = res.redirectUrl;
            } else {
                publishLoadingOverlay.style.display = "none";
                showToast(res.message);
            }
        }).catch(() => {
            publishLoadingOverlay.style.display = "none";
            showToast("A server error occurred during finalization.");
        });
    }

    /* =========================================================
     * INITIALIZATION
     * =======================================================*/
    document.querySelectorAll(".review-toggle").forEach(btn => {
        btn.onclick = () => btn.closest(".review-item").classList.toggle("is-open");
    });

    document.querySelectorAll('input[name="criteriaSystem"]').forEach(r => {
        r.onchange = () => {
            updateCriteriaVisibility();
            criteriaRoundsList.querySelectorAll(".event-round-card").forEach(updateWeightTracker);
        };
    });

    function updateCriteriaVisibility() {
        const isPointing = document.getElementById("criteriaSystemPointing")?.checked;
        document.querySelectorAll(".criteria-weight-field").forEach(f => f.style.display = isPointing ? "none" : "block");
    }

    // Picker icon wiring
    document.querySelectorAll("[data-date-icon-for]").forEach(btn => btn.onclick = () => document.getElementById(btn.dataset.dateIconFor)?.showPicker());
    document.querySelectorAll("[data-time-icon-for]").forEach(btn => btn.onclick = () => document.getElementById(btn.dataset.timeIconFor)?.showPicker());

    // Boot components
    createCriteriaRound();
    addJudgeRow();
    addContestantRow();
    addContestantRow();
    updateCriteriaVisibility();

    document.getElementById("btnAddCriteriaRound")?.addEventListener("click", () => {
        const isAveraging = document.getElementById("criteriaSystemAveraging").checked;
        if (isAveraging) {
            const rounds = criteriaRoundsList.querySelectorAll(".event-round-card");
            if (rounds.length > 0) {
                const lastRound = rounds[rounds.length - 1];
                const weights = Array.from(lastRound.querySelectorAll(".criteria-weight")).map(i => parseFloat(i.value) || 0);
                const total = Math.round(weights.reduce((a, b) => a + b, 0));
                
                if (total !== 100) {
                    showToast(`Total weight of criteria must be exactly 100%.`);
                    return;
                }
            }
        }
        createCriteriaRound();
    });

    document.getElementById("btnAddContestant")?.addEventListener("click", () => {
        const rows = Array.from(contestantsBody.querySelectorAll("tr[data-contestant-row]"));
        
        // 0. Check for at least two complete contestants
        const completeCount = rows.filter(r => 
            r.querySelector(".contestant-name").value.trim() !== "" && 
            r.querySelector(".contestant-org").value.trim() !== ""
        ).length;
        
        if (completeCount < 2) {
            showToast("At least two is required");
        }

        // 1. Check completion of all existing rows
        for (let r of rows) {
            const nameEl = r.querySelector(".contestant-name");
            const orgEl = r.querySelector(".contestant-org");
            const name = nameEl.value.trim();
            const org = orgEl.value.trim();
            
            if (!name) {
                showToast("Name is required");
                nameEl.classList.add("is-invalid-red");
                return;
            }
            if (!org) {
                showToast("Organization is required");
                orgEl.classList.add("is-invalid-red");
                return;
            }
        }

        // 2. Count check
        if (rows.length < 2) {
            showToast("At least two is required");
        }

        addContestantRow();
    });
    
    // Resume Draft Check
    loadDraft();
    setInterval(saveDraft, 10000);
});
