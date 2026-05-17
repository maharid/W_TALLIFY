// Notification Logic using SignalR

let currentNotifLimit = 10;

// Helper to update badge - Defined in Global Scope
function updateUnreadCount() {
    const badge = document.getElementById('notifBadge');
    if (!badge) return;
    fetch('/Notifications/GetUnreadCount')
        .then(res => res.json())
        .then(count => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.style.display = 'block';
            } else {
                badge.style.display = 'none';
            }
        })
        .catch(err => console.error("Error fetching unread count:", err));
}

document.addEventListener("DOMContentLoaded", function () {
    const btn = document.getElementById('btnNotifications');
    const pop = document.getElementById('notificationsPopover');
    const closeBtn = document.getElementById('btnNotificationsClose');
    const markAllBtn = document.getElementById('btnMarkAllRead');
    const content = pop ? pop.querySelector('.notifications-content') : null;
    
    // Initial Unread Count Load
    updateUnreadCount();

    // --- UI INTERACTION ---
    if (btn && pop) {
        // Toggle on bell click
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            const wasOpen = pop.classList.contains('is-open');
            
            if (wasOpen) {
                // Close
                closePopover();
            } else {
                // Open
                pop.classList.add('is-open');
                pop.setAttribute('aria-hidden', 'false');
                
                // Reset to default 10 on open
                currentNotifLimit = 10;
                
                // Fetch notifications immediately
                if (content) loadNotifications(content);
            }
        });

        // Close button (X)
        if (closeBtn) {
            closeBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                closePopover();
            });
        }
        
        // Mark All Read
        if (markAllBtn) {
            markAllBtn.addEventListener('click', function(e) {
                e.stopPropagation();
                const originalText = markAllBtn.textContent;
                markAllBtn.textContent = "...";
                
                fetch('/Notifications/MarkAllAsRead', { method: 'POST' })
                    .then(res => {
                        if(res.ok) {
                            // Refresh list to remove highlights
                            if (content) loadNotifications(content);
                            // Refresh badge to 0
                            updateUnreadCount(); 
                        }
                    })
                    .finally(() => {
                        markAllBtn.textContent = originalText;
                    });
            });
        }

        function closePopover() {
            pop.classList.remove('is-open');
            pop.setAttribute('aria-hidden', 'true');
        }

        // Close on outside click
        document.addEventListener('click', function (e) {
            if (!pop.classList.contains('is-open')) return;
            // If click is inside popover or on the bell, ignore
            if (pop.contains(e.target) || e.target === btn || btn.contains(e.target)) return;
            closePopover();
        });
    }

    // --- SIGNALR CONNECTION ---
    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .build();

    connection.on("ReceiveNotification", function (title, message, type, actionUrl) {
        // 1. Show floating toast
        showNotificationToast(title, message, type, actionUrl);
        
        // 2. Update Badge
        updateUnreadCount();

        // 3. If popover is open, refresh the list to show the new item
        if (pop && pop.classList.contains('is-open') && content) {
            loadNotifications(content);
        }
    });

    connection.start().catch(function (err) {
        console.error("SignalR Error:", err.toString());
    });
});

// --- API FETCH ---
function loadNotifications(container) {
    if (!container) return;
    
    // Use currentNotifLimit (if 0, it fetches all)
    // IMPORTANT: Ensure limit is passed correctly.
    const url = `/Notifications/GetMyNotifications?limit=${currentNotifLimit}`;

    // Spinner (only if we are loading first time or see all, maybe avoid full wipe if just refreshing?)
    // For "See All", we want to show loading.
    container.innerHTML = '<div style="padding:24px; text-align:center; color:var(--color-text-faint); font-size:12px;">Loading...</div>';

    fetch(url)
        .then(res => {
            if (!res.ok) throw new Error("Unauthorized or Error");
            return res.json();
        })
        .then(data => {
            if (!data || data.length === 0) {
                container.innerHTML = `
                    <div class="notifications-empty">
                         <p style="margin:0;">No notifications yet.</p>
                    </div>`;
                return;
            }

            let html = '<ul style="list-style:none; padding:0; margin:0;">';
            data.forEach(n => {
                // Icon & Color
                let colorStyle = "color:#3b82f6"; // blue (info)
                let iconClass = "ri-information-fill";
                
                if (n.type === "success") { colorStyle = "color:#10b981"; iconClass = "ri-checkbox-circle-fill"; }
                if (n.type === "warning") { colorStyle = "color:#f59e0b"; iconClass = "ri-alert-fill"; }
                if (n.type === "error")   { colorStyle = "color:#ef4444"; iconClass = "ri-error-warning-fill"; }

                // Unread highlight
                const bgStyle = n.isRead ? 'background:#fff;' : 'background:var(--color-primary-soft-bg);'; 
                
                const dateObj = new Date(n.createdAt);
                const timeStr = dateObj.toLocaleString('en-US', { 
                    month: 'short', day: '2-digit', 
                    hour: 'numeric', minute: '2-digit', hour12: true 
                });

                html += `
                <li class="notification-item ${n.isRead ? '' : 'notification-unread'}" 
                    data-id="${n.id}"
                    data-url="${n.actionUrl || ''}"
                    style="${bgStyle} border-bottom:1px solid var(--color-border); padding:14px 18px; display:flex; gap:12px; align-items:start; cursor:pointer;">
                    <div style="${colorStyle}; font-size:18px; line-height:1; margin-top:2px;">
                        <i class="${iconClass}"></i>
                    </div>
                    <div style="flex:1;">
                        <div style="font-size:13px; font-weight:600; color:var(--color-text); margin-bottom:2px;">
                            ${n.title}
                        </div>
                        <div style="font-size:12px; color:var(--color-text-soft); line-height:1.4;">
                            ${n.message}
                        </div>
                        <div style="font-size:11px; color:var(--color-text-faint); margin-top:6px;">
                            ${timeStr}
                        </div>
                    </div>
                </li>
                `;
            });
            html += '</ul>';

            // Show "See All" button IF:
            // 1. currentLimit is NOT 0 (meaning we are not already showing all)
            // 2. The number of items returned matches the limit (implying there might be more)
            //    Note: This is a heuristic. Ideally backend returns "totalCount". 
            //    But checking data.length >= currentNotifLimit is usually "good enough" for simple pagination.
            if (currentNotifLimit > 0 && data.length >= currentNotifLimit) {
                html += `
                <div style="padding:10px; text-align:center; border-top:1px solid var(--color-border); background:#f9fafb;">
                    <button type="button" id="btnSeeAllNotifications" 
                        style="border:none; background:transparent; color:var(--color-primary); font-size:12px; font-weight:600; cursor:pointer;">
                        See all
                    </button>
                </div>
                `;
            }

            container.innerHTML = html;

            // Bind See All click
            const seeAllBtn = document.getElementById('btnSeeAllNotifications');
            if (seeAllBtn) {
                seeAllBtn.addEventListener('click', function(e) {
                    e.stopPropagation(); // Prevent closing popover
                    currentNotifLimit = 0; // 0 = All
                    loadNotifications(container); // Re-load with new limit
                });
            }

            // Bind click listeners for notification items
            container.querySelectorAll('.notification-item').forEach(item => {
                item.addEventListener('click', function() {
                    const notificationId = this.dataset.id;
                    const url = this.dataset.url;
                    if (!notificationId) return;

                    // Only mark as read if it's currently unread
                    if (this.classList.contains('notification-unread')) {
                        // 1. Optimistic UI Update: Remove highlight immediately
                        this.classList.remove('notification-unread');
                        this.style.background = '#fff'; 
                        
                        // 2. Silent Background Request
                        fetch('/Notifications/MarkAsRead', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ id: parseInt(notificationId) })
                        })
                        .then(res => {
                            if (res.ok) {
                                // 3. Update badge count silently
                                updateUnreadCount(); 
                                if (url) window.location.href = url;
                            }
                        })
                        .catch(err => console.error('Error marking notification as read:', err));
                    } else {
                        // Already read, just navigate if url exists
                        if (url) window.location.href = url;
                    }
                });
            });
        })
        .catch(err => {
            console.error(err);
            container.innerHTML = '<div style="padding:20px;text-align:center;color:red;font-size:12px;">Failed to load.</div>';
        });
}

// --- TOAST UI ---
function showNotificationToast(title, message, type, actionUrl) {
    // Ensure container exists
    let container = document.getElementById('notifToastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'notifToastContainer';
        container.style.cssText = 'position:fixed; top:20px; right:20px; z-index:10001; display:flex; flex-direction:column; gap:10px; width:320px; pointer-events:none;';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.style.cssText = 'pointer-events:auto; cursor:pointer; background:#fff; border-radius:12px; box-shadow:0 10px 25px rgba(0,0,0,0.15); border-left:5px solid #3b82f6; padding:16px; transform:translateX(120%); transition:transform 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55);';
    
    let borderColor = "#3b82f6"; 
    if (type === "success") borderColor = "#10b981";
    if (type === "warning") borderColor = "#f59e0b";
    if (type === "error") borderColor = "#ef4444";

    toast.style.borderLeftColor = borderColor;
    toast.innerHTML = `
        <div style="font-size:14px; font-weight:700; color:#111827; margin-bottom:4px;">${title}</div>
        <div style="font-size:12px; color:#6b7280; line-height:1.4;">${message}</div>
    `;

    if (actionUrl) {
        toast.onclick = () => window.location.href = actionUrl;
    }

    container.appendChild(toast);

    // Animate in
    setTimeout(() => toast.style.transform = 'translateX(0)', 10);

    // Auto-remove
    setTimeout(() => {
        toast.style.transform = 'translateX(120%)';
        setTimeout(() => {
            toast.remove();
            if (container.children.length === 0) container.remove();
        }, 300); 
    }, 5000);
}