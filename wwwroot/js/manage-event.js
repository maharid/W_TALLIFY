function initManageEvent(eventId, serverAccessCode, eventStatus) {
    let currentAction = '';
    let pendingRoundId = null;

    // -- Tabs --
    const tabs = document.querySelectorAll('.tab-link');
    const tabContents = document.querySelectorAll('.tab-content');
    
    // -- Photo Modal --
    const photoModal = document.getElementById('photoPreviewModal');
    const photoModalImg = document.getElementById('photoPreviewImage');
    const btnClosePhotoModal = document.getElementById('btnClosePhotoModal');

    // -- Select Contestants Modal Elements --
    const selectContestantsModal = document.getElementById('modal-select-contestants');
    const selectedRoundNameSpan = document.getElementById('selected-round-name');
    const selectContestantsTableBody = document.getElementById('select-contestants-table-body');
    const selectAllContestantsCheckbox = document.getElementById('select-all-contestants-checkbox');
    const topNInput = document.getElementById('top-n-input');
    const btnSelectTopN = document.getElementById('btn-select-top-n');
    const topNHint = document.getElementById('top-n-hint');
    const btnCloseSelectContestantsModal = document.getElementById('btn-close-select-contestants-modal');
    const btnCancelSelectContestants = document.getElementById('btn-cancel-select-contestants');
    
    // New Multi-Step Elements
    const btnReviewSelection = document.getElementById('btn-review-selection');
    const btnBackSelection = document.getElementById('btn-back-selection');
    const btnConfirmStartRound = document.getElementById('btn-confirm-start-round');
    
    const modalStep1 = document.getElementById('modal-step-1');
    const modalStep2 = document.getElementById('modal-step-2');
    const modalFooter1 = document.getElementById('modal-footer-step-1');
    const modalFooter2 = document.getElementById('modal-footer-step-2');
    const summaryContainer = document.getElementById('selected-contestants-summary');

    // -- Confirm Action Modal (Start/End/EndRound) --
    const confirmActionModal = document.getElementById('modal-confirm-action');
    const confirmModalTitle = document.getElementById('confirm-modal-title');
    const confirmModalMessage = document.getElementById('confirm-modal-message');
    const btnCancelConfirm = document.getElementById('btn-cancel-confirm');
    const btnProceedConfirm = document.getElementById('btn-proceed-confirm');

    // -- Access Code Modal --
    const accessCodeModal = document.getElementById('modal-access-code');
    const accessCodeModalTitle = document.getElementById('access-code-modal-title');
    const accessCodeModalHint = document.getElementById('access-code-modal-hint');
    const accessCodeInput = document.getElementById('accessCodeInput');
    const accessCodeError = document.getElementById('accessCodeError');
    const btnCancelAccessCode = document.getElementById('btn-cancel-access-code');
    const btnSubmitAccessCode = document.getElementById('btn-submit-access-code');
    
    if (accessCodeInput) {
        accessCodeInput.addEventListener('input', function() {
            if (accessCodeError) {
                accessCodeError.textContent = '';
                accessCodeError.style.display = 'none';
            }
        });
    }

    // -- Success Modal --
    const successModalOverlay = document.getElementById('success-modal-overlay');
    const successModalMessage = document.getElementById('success-modal-message');
    const btnCloseSuccess = document.getElementById('btn-close-success-modal');
    const btnOkSuccess = document.getElementById('btn-ok-success-modal');


    // --- 2. Tabs Logic ---
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            tabs.forEach(t => t.classList.remove('active'));
            tabContents.forEach(content => content.classList.remove('active'));
            tab.classList.add('active');
            const targetId = tab.getAttribute('data-tab');
            const targetContent = document.getElementById(`tab-${targetId}`);
            if (targetContent) targetContent.classList.add('active');
        });
    });
    
    // --- 2b. Photo Modal Logic ---
    const photoThumbs = document.querySelectorAll('.contestant-photo-thumb[data-full-src]');
    
    photoThumbs.forEach(thumb => {
        thumb.addEventListener('click', function() {
            const src = this.getAttribute('data-full-src');
            if (src && photoModal && photoModalImg) {
                photoModalImg.src = src;
                photoModal.classList.add('is-open');
                document.body.classList.add('has-modal-open');
            }
        });
    });
    
    function closePhotoModal() {
        if(photoModal) photoModal.classList.remove('is-open');
        if(photoModalImg) setTimeout(() => photoModalImg.src = '', 200); // Clear after transition
        document.body.classList.remove('has-modal-open');
    }
    
    if(btnClosePhotoModal) btnClosePhotoModal.addEventListener('click', closePhotoModal);
    if(photoModal) {
            photoModal.addEventListener('click', function(e) {
                if(e.target === photoModal) closePhotoModal();
            });
    }


    // --- 3. Start Round Logic (Select Contestants Modal) ---

    // Helper to load contestants
    async function loadContestantsForRound(roundOrder) {
        // RESET WIZARD STATE
        if (modalStep1) modalStep1.style.display = 'block';
        if (modalStep2) modalStep2.style.display = 'none';
        if (modalFooter1) modalFooter1.style.display = 'flex';
        if (modalFooter2) modalFooter2.style.display = 'none';
        if (btnConfirmStartRound) {
            btnConfirmStartRound.textContent = "Confirm & Start Round";
            btnConfirmStartRound.disabled = false;
        }
        if (btnBackSelection) btnBackSelection.disabled = false;

        const topNContainer = document.getElementById('top-n-container');
        const rankTh = document.getElementById('select-contestants-rank-th');
        const isFirstRound = roundOrder === 1;
        const canUseRank = !isFirstRound; // Can use rank means it's NOT the first round
        const colCount = isFirstRound ? 3 : 4; // Checkbox, Photo, Name/Org, (Rank)

        selectContestantsTableBody.innerHTML = `<tr><td colspan="${colCount}" style="text-align:center; padding: 20px;">Loading...</td></tr>`; 
        
        // Toggle Top N UI
        if (topNContainer) {
            topNContainer.style.display = isFirstRound ? 'none' : 'flex';
        }
        
        // Toggle Rank column header visibility
        if (rankTh) {
            rankTh.style.display = isFirstRound ? 'none' : 'table-cell';
        }

        // Hide hint as it's handled by container visibility
        if(topNHint) topNHint.style.display = 'none'; 
        if(btnSelectTopN) btnSelectTopN.disabled = false; 

        try {
            if (window.showLoader) window.showLoader();
            const response = await fetch(`/Events/GetContestantsRank?eventId=${eventId}`);
            const contestants = await response.json();

            selectContestantsTableBody.innerHTML = '';
            if (!contestants || contestants.length === 0) {
                selectContestantsTableBody.innerHTML = `<tr><td colspan="${colCount}" style="text-align:center; padding: 20px;">No contestants found.</td></tr>`;
                return;
            }

            if (canUseRank) {
                contestants.sort((a, b) => (a.rank || 9999) - (b.rank || 9999));
            } else {
                contestants.sort((a, b) => a.name.localeCompare(b.name));
            }

            contestants.forEach(c => {
                const rankDisplay = (c.rank && c.rank > 0 && canUseRank) ? `#${c.rank} (${c.score})` : '-';
                const photoHtml = c.photoUrl
                    ? `<div class="contestant-photo-wrapper"><div class="contestant-photo-thumb has-photo" style="background-image: url('${c.photoUrl}');"></div></div>`
                    : `<span class="text-muted">No photo</span>`;
                
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td style="text-align: center; vertical-align: middle;"><input type="checkbox" class="contestant-checkbox" value="${c.id}" data-rank="${c.rank || 9999}" style="width: 18px; height: 18px; cursor: pointer;"></td>
                    <td style="text-align: center; vertical-align: middle;">${photoHtml}</td>
                    <td style="vertical-align: middle;">
                        <div style="font-weight: 600; color: var(--color-text); font-size: 14px;">${c.name}</div>
                        <div style="font-size: 12px; color: var(--color-text-soft);">${c.organization}</div>
                    </td>
                    ${canUseRank ? `<td style="text-align: center; vertical-align: middle; font-weight: 500; color: var(--color-text-soft);">${rankDisplay}</td>` : ''}
                `;
                selectContestantsTableBody.appendChild(row);
            });

            // Automatically check "Select All" and all contestant checkboxes by default
            if (selectAllContestantsCheckbox) {
                selectAllContestantsCheckbox.checked = true;
                const checkboxes = selectContestantsTableBody.querySelectorAll('.contestant-checkbox');
                checkboxes.forEach(cb => cb.checked = true);
            }

        } catch (err) {
            console.error(err);
            selectContestantsTableBody.innerHTML = `<tr><td colspan="${colCount}" style="text-align:center; padding: 20px; color:red;">Error loading contestants.</td></tr>`; 
        } finally {
            if (window.hideLoader) window.hideLoader();
        }
    }

    // Global functions for inline onclicks
    window.openStartRoundModal = function(roundId, roundName, roundOrder) {
        pendingRoundId = roundId;
        selectedRoundNameSpan.textContent = roundName; // Update the span in the modal title
        if(selectContestantsModal) {
            selectContestantsModal.classList.add('open');
            document.body.classList.add('has-modal-open');
        }
        loadContestantsForRound(roundOrder);
    };

    window.openEndRoundModal = function(roundId, roundName) {
        currentAction = 'endRound';
        pendingRoundId = roundId;
        if(confirmModalTitle) confirmModalTitle.textContent = 'Confirm End Round';
        if(confirmModalMessage) confirmModalMessage.innerHTML = `Are you sure you want to <strong>end</strong> ${roundName}?<br>This will close scoring for this round.`;
        if(confirmActionModal) confirmActionModal.classList.add('open');
    };

    function hideSelectContestantsModal() {
        if(selectContestantsModal) {
            selectContestantsModal.classList.remove('open');
            document.body.classList.remove('has-modal-open');
        }
    }

    if (btnCloseSelectContestantsModal) btnCloseSelectContestantsModal.addEventListener('click', hideSelectContestantsModal);
    if (btnCancelSelectContestants) btnCancelSelectContestants.addEventListener('click', hideSelectContestantsModal);
    if (selectContestantsModal) {
        selectContestantsModal.addEventListener('click', function(e) {
            if (e.target === selectContestantsModal) hideSelectContestantsModal();
        });
    }

    if (selectAllContestantsCheckbox) {
        selectAllContestantsCheckbox.addEventListener('change', function() {
            const checkboxes = selectContestantsTableBody.querySelectorAll('.contestant-checkbox');
            checkboxes.forEach(cb => cb.checked = this.checked);
        });
    }

    if (btnSelectTopN) {
        btnSelectTopN.addEventListener('click', function() {
            const n = parseInt(topNInput.value);
            if (!n || n <= 0) {
                alert("Please enter a valid number for Top N.");
                return;
            }
            const checkboxes = Array.from(selectContestantsTableBody.querySelectorAll('.contestant-checkbox'));
            checkboxes.sort((a, b) => parseFloat(a.dataset.rank) - parseFloat(b.dataset.rank));
            checkboxes.forEach(cb => cb.checked = false); // Uncheck all first
            let count = 0;
            for (const cb of checkboxes) {
                if (count < n) {
                    cb.checked = true;
                    count++;
                }
            }
            // If "Select All" checkbox is checked, uncheck it after "Select Top N"
            if (selectAllContestantsCheckbox && selectAllContestantsCheckbox.checked) {
                selectAllContestantsCheckbox.checked = false;
            }
        });
    }

    // --- Multi-Step Logic ---

    // 1. Review Selection (Step 1 -> Step 2)
    if (btnReviewSelection) {
        btnReviewSelection.addEventListener('click', function() {
            const selectedCheckboxes = Array.from(selectContestantsTableBody.querySelectorAll('.contestant-checkbox:checked'));
            
            if (selectedCheckboxes.length === 0) {
                alert("Please select at least one contestant.");
                return;
            }

            // Build Summary
            let summaryHtml = '<ul style="list-style: none; padding: 0; margin: 0;">';
            selectedCheckboxes.forEach(cb => {
                const row = cb.closest('tr');
                // Cells: 0=Checkbox, 1=Photo, 2=Name/Org
                const photoHtml = row.cells[1].innerHTML; 
                const nameHtml = row.cells[2].innerHTML; 
                
                summaryHtml += `
                    <li style="display: flex; align-items: center; padding: 12px; border-bottom: 1px solid var(--color-border);">
                        <div style="width: 50px; text-align: center; margin-right: 12px;">${photoHtml}</div>
                        <div style="flex: 1;">${nameHtml}</div>
                    </li>
                `;
            });
            summaryHtml += '</ul>';
            
            if (summaryContainer) summaryContainer.innerHTML = summaryHtml;

            // Switch Steps
            if (modalStep1) modalStep1.style.display = 'none';
            if (modalStep2) modalStep2.style.display = 'block';
            if (modalFooter1) modalFooter1.style.display = 'none';
            if (modalFooter2) modalFooter2.style.display = 'flex';
        });
    }

    // 2. Back (Step 2 -> Step 1)
    if (btnBackSelection) {
        btnBackSelection.addEventListener('click', function() {
            if (modalStep1) modalStep1.style.display = 'block';
            if (modalStep2) modalStep2.style.display = 'none';
            if (modalFooter1) modalFooter1.style.display = 'flex';
            if (modalFooter2) modalFooter2.style.display = 'none';
        });
    }

    // 3. Confirm & Start (Step 2 -> Action)
    if (btnConfirmStartRound) {
        btnConfirmStartRound.addEventListener('click', async function() {
            const selectedIds = Array.from(selectContestantsTableBody.querySelectorAll('.contestant-checkbox:checked')).map(cb => cb.value);
            
            if (selectedIds.length === 0) return;

            const originalText = this.textContent;
            this.textContent = "Starting...";
            this.disabled = true;
            if(btnBackSelection) btnBackSelection.disabled = true;

            try {
                if (window.showLoader) window.showLoader();
                const response = await fetch('/Events/StartRound', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({ eventId: eventId, roundId: pendingRoundId, contestantIds: selectedIds })
                });
                const data = await response.json();
                if (data.success) {
                    hideSelectContestantsModal();
                    showSuccessModal(data.message);
                    setTimeout(() => window.location.reload(), 1500);
                } else {
                    alert(data.message || "Failed to start round.");
                    this.textContent = originalText;
                    this.disabled = false;
                    if(btnBackSelection) btnBackSelection.disabled = false;
                }
            } catch (err) {
                console.error(err);
                alert("An error occurred.");
                this.textContent = originalText;
                this.disabled = false;
                if(btnBackSelection) btnBackSelection.disabled = false;
            } finally {
                if (window.hideLoader) window.hideLoader();
            }
        });
    }


    // --- 4. Start/End Event & Confirm Logic ---

    // Exposed function for Start/End Event buttons
    window.openConfirmModal = function(action) {
        currentAction = action;
        if (action === 'start') {
            if(confirmModalTitle) confirmModalTitle.textContent = 'Ready to Start Event?';
            if(confirmModalMessage) confirmModalMessage.innerHTML = 'You are about to <strong>officially start</strong> this event. This action will activate all associated rounds and judging/scoring interfaces. Event details cannot be modified after starting. Ensure all configurations are final.';
        } else if (action === 'end') {
            if(confirmModalTitle) confirmModalTitle.textContent = 'Confirm Event End';
            if(confirmModalMessage) confirmModalMessage.innerHTML = 'Are you sure you want to <strong>end</strong> this event?<br>This action cannot be undone.';
        }
        if(confirmActionModal) confirmActionModal.classList.add('open');
    };

    function hideConfirmModal() {
        if(confirmActionModal) confirmActionModal.classList.remove('open');
    }

    function openAccessCodeModal() {
        if (currentAction === 'endRound') {
            if(accessCodeModalTitle) accessCodeModalTitle.textContent = 'End Round';
        } else {
            if(accessCodeModalTitle) accessCodeModalTitle.textContent = (currentAction === 'start' ? 'Start Event' : 'End Event');
        }
        if(accessCodeModalHint) accessCodeModalHint.textContent = 'Please enter the event\'s Access Code to confirm.';
        if(accessCodeInput) accessCodeInput.value = ''; 
        if(accessCodeError) accessCodeError.textContent = ''; 
        if(accessCodeModal) accessCodeModal.classList.add('open');
    }

    function hideAccessCodeModal() {
        if(accessCodeModal) accessCodeModal.classList.remove('open');
    }

    if (btnCancelConfirm) btnCancelConfirm.addEventListener('click', hideConfirmModal);
    if (btnProceedConfirm) btnProceedConfirm.addEventListener('click', function() {
        hideConfirmModal();
        openAccessCodeModal();
    });
    if (confirmActionModal) {
        confirmActionModal.addEventListener('click', function(e) {
            if (e.target === confirmActionModal) hideConfirmModal();
        });
    }

    if (btnCancelAccessCode) btnCancelAccessCode.addEventListener('click', hideAccessCodeModal);
    if (accessCodeModal) {
        accessCodeModal.addEventListener('click', function(e) {
            if (e.target === accessCodeModal) hideAccessCodeModal();
        });
    }

    // --- Validation Logic ---
    if (btnSubmitAccessCode) {
        // Handle Click
        btnSubmitAccessCode.addEventListener('click', validateAndSubmit);
        
        // Handle Enter Key in Input
        if(accessCodeInput) {
            accessCodeInput.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault(); // Prevent default form submit if any
                    validateAndSubmit();
                }
            });
        }
    }

    async function validateAndSubmit() {
        const enteredCode = accessCodeInput.value.trim();
        // Use SERVER-SIDE value as source of truth per requirement
        const expected = (serverAccessCode || "").trim();

        console.log("Validating. Expected:", expected, "Entered:", enteredCode);

        // Validate against server value
        if (enteredCode.toLowerCase() !== expected.toLowerCase()) {
            if(accessCodeError) {
                accessCodeError.textContent = 'Incorrect Access Code.';
                accessCodeError.style.display = 'block';
                accessCodeError.style.color = '#dc3545'; // Standard danger color
            }
            return; // Stop execution, do not close modal
        }

        // Success
        if(accessCodeError) {
            accessCodeError.textContent = '';
            accessCodeError.style.display = 'none';
        }
        hideAccessCodeModal(); 

        submitEventAction(enteredCode);
    }

    async function submitEventAction(accessCode) {
        // -- Logic for End Round --
        if (currentAction === 'endRound') {
            try {
                if (window.showLoader) window.showLoader();
                const response = await fetch('/Events/EndRound', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({ roundId: pendingRoundId, accessCode: accessCode })
                });
                const data = await response.json();
                if (data.success) {
                    showSuccessModal(data.message);
                    setTimeout(() => window.location.reload(), 1500);
                } else {
                    showSuccessModal(data.message || "Failed to end round.");
                }
            } catch (err) {
                console.error(err);
                showSuccessModal("An error occurred.");
            } finally {
                if (window.hideLoader) window.hideLoader();
            }
            return;
        }

        // -- Logic for Start/End Event --
        const startBtn = document.querySelector('.manage-event-actions button.btn-primary[onclick*="openConfirmModal(\'start\')"]');
        const endBtn = document.querySelector('.manage-event-actions button.btn-danger[onclick*="openConfirmModal(\'end\')"]');
        if (startBtn && currentAction === 'start') startBtn.disabled = true;
        if (endBtn && currentAction === 'end') endBtn.disabled = true;

        try {
            if (window.showLoader) window.showLoader();
            const response = await fetch(`/Events/${currentAction}?id=${eventId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value 
                },
                body: JSON.stringify({ accessCode: accessCode })
            });
            const data = await response.json();

            if (data.success) {
                showSuccessModal(data.message);
                setTimeout(() => {
                    window.location.reload(); 
                }, 1500);
            } else {
                showSuccessModal(data.message || `Failed to ${currentAction} event.`);
                if (startBtn && currentAction === 'start') startBtn.disabled = false;
                if (endBtn && currentAction === 'end') endBtn.disabled = false;
            }
        } catch (error) {
            console.error('Error:', error);
            showSuccessModal(`An error occurred while trying to ${currentAction} the event.`);
            if (startBtn && currentAction === 'start') startBtn.disabled = false;
            if (endBtn && currentAction === 'end') endBtn.disabled = false;
        } finally {
            if (window.hideLoader) window.hideLoader();
        }
    }

    // --- 5. Copy Access Code Logic ---
    const copyBtns = document.querySelectorAll('.btn-copy-access');
    copyBtns.forEach(btn => {
        btn.addEventListener('click', function() {
            const targetId = this.getAttribute('data-copy-target');
            const targetEl = document.getElementById(targetId);
            if (targetEl) {
                const textToCopy = targetEl.textContent.trim();
                if (textToCopy && textToCopy !== '—') {
                    navigator.clipboard.writeText(textToCopy).then(() => {
                        const originalText = this.textContent;
                        this.textContent = 'Copied!';
                        setTimeout(() => {
                            this.textContent = originalText;
                        }, 2000);
                    }).catch(err => {
                        console.error('Failed to copy: ', err);
                    });
                }
            }
        });
    });

    // --- 6. Send Access Logic ---
    const confirmSendOverlay = document.getElementById('confirm-send-overlay');
    const btnCancelSend = document.getElementById('btn-cancel-send');
    const btnConfirmSend = document.getElementById('btn-confirm-send');
    const sendAllBtn = document.querySelector('.send-all-access-btn');
    
    // Send All
    if (sendAllBtn) {
        sendAllBtn.addEventListener('click', function() {
            if (eventStatus !== 'open') {
                showSuccessModal("You cannot send access codes yet. Please start the event first.");
                return;
            }
            if (confirmSendOverlay) confirmSendOverlay.classList.add('open');
        });
    }
    
    if (btnCancelSend) {
        btnCancelSend.addEventListener('click', function() {
            if (confirmSendOverlay) confirmSendOverlay.classList.remove('open');
        });
    }
    
    if (btnConfirmSend) {
        btnConfirmSend.addEventListener('click', async function() {
            // Close confirm modal
            if (confirmSendOverlay) confirmSendOverlay.classList.remove('open');
            
            // Show loading state on button (optional)
            const originalText = this.textContent;
            this.textContent = "Sending...";
            
            try {
                if (window.showLoader) window.showLoader();
                const response = await fetch(`/Events/SendAccessCodeToAll?eventId=${eventId}`, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                });
                const data = await response.json();
                showSuccessModal(data.message || (data.success ? "Sent successfully." : "Failed to send."));
            } catch (err) {
                console.error(err);
                showSuccessModal("An error occurred while sending emails.");
            } finally {
                if (window.hideLoader) window.hideLoader();
                this.textContent = originalText;
            }
        });
    }
    
    // Send Single
    const confirmSendSingleOverlay = document.getElementById('confirm-send-single-overlay');
    const btnCancelSendSingle = document.getElementById('btn-cancel-send-single');
    const btnConfirmSendSingle = document.getElementById('btn-confirm-send-single');
    let pendingJudgeId = null;
    let pendingJudgeBtn = null;

    const sendSingleBtns = document.querySelectorAll('.send-access-btn');
    sendSingleBtns.forEach(btn => {
        btn.addEventListener('click', function() {
            if (eventStatus !== 'open') {
                showSuccessModal("You cannot send access codes yet. Please start the event first.");
                return;
            }

            pendingJudgeId = this.getAttribute('data-judge-id');
            pendingJudgeBtn = this;
            
            if (pendingJudgeId && confirmSendSingleOverlay) {
                confirmSendSingleOverlay.classList.add('open');
            }
        });
    });

    if (btnCancelSendSingle) {
        btnCancelSendSingle.addEventListener('click', function() {
            if (confirmSendSingleOverlay) confirmSendSingleOverlay.classList.remove('open');
            pendingJudgeId = null;
            pendingJudgeBtn = null;
        });
    }

    if (btnConfirmSendSingle) {
        btnConfirmSendSingle.addEventListener('click', async function() {
            if (!pendingJudgeId) return;
            if (confirmSendSingleOverlay) confirmSendSingleOverlay.classList.remove('open');

            const btn = pendingJudgeBtn;
            const judgeId = pendingJudgeId;
            
            // Reset pending vars
            pendingJudgeId = null;
            pendingJudgeBtn = null;

            if (btn) {
                var originalText = btn.textContent;
                btn.textContent = "Sending...";
                btn.disabled = true;
            }
            
            try {
                if (window.showLoader) window.showLoader();
                const response = await fetch(`/Events/SendAccessCode?judgeId=${judgeId}`, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                });
                const data = await response.json();
                showSuccessModal(data.message || (data.success ? "Sent successfully." : "Failed to send."));
            } catch (err) {
                console.error(err);
                showSuccessModal("An error occurred.");
            } finally {
                if (window.hideLoader) window.hideLoader();
                if (btn) {
                    btn.textContent = originalText;
                    btn.disabled = false;
                }
            }
        });
    }

                            // --- 7. Generate Report Logic ---
                            const btnGenerateReport = document.getElementById('btnGenerateReport');
                            const reportModal = document.getElementById('reportPreviewModal');
                            const btnCloseReportModal = document.getElementById('btnCloseReportModal');
                            const reportContent = document.getElementById('reportPreviewContent');
                            const btnDownloadReport = document.getElementById('btnDownloadReport'); // Added
                            
                            if (btnGenerateReport) {
                                btnGenerateReport.addEventListener('click', async function() {
                                    
                                    // 1. Validate Status
                                    if (eventStatus !== "closed") {
                                        showSuccessModal("Reports can only be generated when the event is ended (closed).");
                                        return;
                                    }
                
                                    // 2. Validate Selection
                                    const checkboxes = document.querySelectorAll('.report-checkbox:checked');
                                    if (checkboxes.length === 0) {
                                        showSuccessModal("Please select at least one report option.");
                                        return;
                                    }
                
                                    const selectedTypes = Array.from(checkboxes).map(cb => cb.value).join(',');
                
                                    // 3. Fetch Preview
                                    const originalText = this.textContent;
                                    this.textContent = "Generating...";
                                    this.disabled = true;
                
                                    try {
                                        if (window.showLoader) window.showLoader();
                                        const response = await fetch(`/Events/GenerateReportPdf?eventId=${eventId}&reportTypes=${selectedTypes}`);
                                        if (!response.ok) throw new Error("Failed to generate report.");
                                        
                                        const blob = await response.blob();
                                        const url = URL.createObjectURL(blob);
                                        
                                        if(reportContent) {
                                            reportContent.innerHTML = `<iframe src="${url}" style="width:100%; height:100%; border:none;"></iframe>`;
                                        }
                
                                        // Setup Download Link
                                        if (btnDownloadReport) {
                                            btnDownloadReport.href = url;
                                            // Set filename with date
                                            const dateStr = new Date().toISOString().slice(0, 10);
                                            btnDownloadReport.download = `Report_${eventId}_${dateStr}.pdf`;
                                        }
                
                                        if(reportModal) {
                                            reportModal.classList.add('is-open');
                                            document.body.classList.add('has-modal-open');
                                        }
                
                                    } catch (err) {
                                        console.error(err);
                                        showSuccessModal("An error occurred while generating the report.");
                                    } finally {
                                        if (window.hideLoader) window.hideLoader();
                                        this.textContent = originalText;
                                        this.disabled = false;
                                    }
                                });
                            }    function closeReportModal() {
        if(reportModal) reportModal.classList.remove('is-open');
        document.body.classList.remove('has-modal-open');
    }

    if(btnCloseReportModal) btnCloseReportModal.addEventListener('click', closeReportModal);
    
    // --- 8. Archive Event Logic ---
    const btnArchiveEvent = document.getElementById('btnArchiveEvent');
    const archiveConfirmModal = document.getElementById('modal-archive-confirm');
    const btnCancelArchive = document.getElementById('btn-cancel-archive');
    const btnConfirmArchive = document.getElementById('btn-confirm-archive');

    function openArchiveConfirmModal() {
        if (archiveConfirmModal) archiveConfirmModal.classList.add('open');
    }

    function hideArchiveConfirmModal() {
        if (archiveConfirmModal) archiveConfirmModal.classList.remove('open');
    }

    if (btnArchiveEvent) {
        btnArchiveEvent.addEventListener('click', function() {
            openArchiveConfirmModal();
        });
    }

    if (btnCancelArchive) {
        btnCancelArchive.addEventListener('click', hideArchiveConfirmModal);
    }

    if (btnConfirmArchive) {
        btnConfirmArchive.addEventListener('click', async function() {
            hideArchiveConfirmModal(); // Close the modal immediately

            const originalText = btnArchiveEvent.textContent;
            btnArchiveEvent.textContent = "Archiving...";
            btnArchiveEvent.disabled = true;

            try {
                if (window.showLoader) window.showLoader();
                const response = await fetch(`/Events/Archive?id=${eventId}`, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                });
                const data = await response.json();

                if (data.success) {
                    showSuccessModal(data.message || "Event archived successfully.");
                    setTimeout(() => {
                        window.location.href = "/Home/Dashboard"; // Redirect to dashboard after archiving
                    }, 1500);
                } else {
                    showSuccessModal(data.message || "Failed to archive event.");
                }
            } catch (error) {
                console.error('Error:', error);
                showSuccessModal("An error occurred while trying to archive the event.");
            } finally {
                if (window.hideLoader) window.hideLoader();
                btnArchiveEvent.textContent = originalText;
                btnArchiveEvent.disabled = false;
            }
        });
    }

    // --- 9. Helper Functions ---
    function showSuccessModal(msg) {
        if (successModalOverlay && successModalMessage) {
            successModalMessage.textContent = msg;
            successModalOverlay.classList.add('open');
        } else {
            alert(msg);
        }
    }
    if (btnCloseSuccess) btnCloseSuccess.addEventListener('click', () => successModalOverlay.classList.remove('open'));
    if (btnOkSuccess) btnOkSuccess.addEventListener('click', () => successModalOverlay.classList.remove('open'));

}