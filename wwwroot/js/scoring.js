/* ========================================================================
   SCORING.JS - Shared Logic
   ======================================================================== */

document.addEventListener("DOMContentLoaded", () => {
    initLogoutModal();
    initGenericTools();
});

/* ========================================================================
   LOGOUT MODAL
   ======================================================================== */
function initLogoutModal() {
    const logoutBtn = document.getElementById("btnLogout");
    const overlay = document.getElementById("logout-overlay");
    const cancelBtn = document.getElementById("btnCancelLogout");
    
    // The modal might have a close (X) button, though not in current layout
    const closeBtn = document.getElementById("btn-close-logout"); 

    if (!logoutBtn || !overlay) return;

    function openLogout(e) {
        e.preventDefault(); // Stop any form submission if wrapped
        overlay.classList.add("open");
        overlay.setAttribute("aria-hidden", "false");
    }

    function closeLogout() {
        overlay.classList.remove("open");
        overlay.setAttribute("aria-hidden", "true");
    }

    logoutBtn.addEventListener("click", openLogout);
    
    if (cancelBtn) cancelBtn.addEventListener("click", closeLogout);
    if (closeBtn) closeBtn.addEventListener("click", closeLogout);

    // Close on clicking background
    overlay.addEventListener("click", (e) => {
        if (e.target === overlay) closeLogout();
    });
}

/* ========================================================================
   GENERIC TOOLS (Search, Simple Modals)
   ======================================================================== */
function initGenericTools() {
    // 1. Contestant Search Filter (if present)
    const searchInput = document.getElementById("contestant-search");
    const listRoot = document.getElementById("contestant-list"); // For Sidebar layout

    if (searchInput && listRoot) {
        const items = Array.from(listRoot.querySelectorAll(".contestant-item"));
        
        searchInput.addEventListener("input", (e) => {
            const term = e.target.value.trim().toLowerCase();
            items.forEach(item => {
                // Search by name, ID, or any text content
                const text = item.textContent.toLowerCase();
                item.style.display = text.includes(term) ? "" : "none";
            });
        });
    }
}
