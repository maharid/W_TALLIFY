// Notification Logic using SignalR - Facebook Style

let currentNotifLimit = 10;
let currentNotifFilter = 'all'; // 'all' or 'unread'

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
    const tabs = pop ? pop.querySelectorAll('.notif-tab') : [];
    
    // Initial Unread Count Load
    updateUnreadCount();

    // --- UI INTERACTION ---
    if (btn && pop) {
        // Toggle on bell click
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            const wasOpen = pop.classList.contains('is-open');
            
            if (wasOpen) {
                closePopover();
            } else {
                pop.classList.add('is-open');
                pop.setAttribute('aria-hidden', 'false');
                currentNotifLimit = 10;
                if (content) loadNotifications(content);
            }
        });

        // Tabs Logic
        tabs.forEach(tab => {
            tab.addEventListener('click', function(e) {
                e.stopPropagation();
                const filter = this.dataset.filter;
                if (filter === currentNotifFilter) return;

                currentNotifFilter = filter;
                tabs.forEach(t => t.classList.remove('is-active'));
                this.classList.add('is-active');

                loadNotifications(content);
            });
        });

        if (closeBtn) {
            closeBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                closePopover();
            });
        }
        
        if (markAllBtn) {
            markAllBtn.addEventListener('click', function(e) {
                e.stopPropagation();
                fetch('/Notifications/MarkAllAsRead', { method: 'POST' })
                    .then(res => {
                        if(res.ok) {
                            loadNotifications(content);
                            updateUnreadCount(); 
                        }
                    });
            });
        }

        function closePopover() {
            pop.classList.remove('is-open');
            pop.setAttribute('aria-hidden', 'true');
        }

        document.addEventListener('click', function (e) {
            if (!pop.classList.contains('is-open')) return;
            if (pop.contains(e.target) || e.target === btn || btn.contains(e.target)) return;
            closePopover();
        });
    }

    // --- SIGNALR CONNECTION ---
    window.signalrConnection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .build();

    window.signalrConnection.on("ReceiveNotification", function (title, message, type, actionUrl) {
        showNotificationToast(title, message, type, actionUrl);
        updateUnreadCount();
        if (pop && pop.classList.contains('is-open') && content) {
            loadNotifications(content);
        }
    });

    window.signalrConnection.start().catch(function (err) {
        console.error("SignalR Error:", err.toString());
    });
});

function loadNotifications(container) {
    if (!container) return;
    
    // Construct URL with filter
    const url = `/Notifications/GetMyNotifications?limit=${currentNotifLimit}&unreadOnly=${currentNotifFilter === 'unread'}`;

    container.innerHTML = '<div style="padding:24px; text-align:center; color:var(--color-text-faint); font-size:12px;">Loading...</div>';

    fetch(url)
        .then(res => res.json())
        .then(data => {
            if (!data || data.length === 0) {
                container.innerHTML = `
                    <div class="notifications-empty">
                         <p style="margin:0;">No ${currentNotifFilter === 'unread' ? 'unread ' : ''}notifications yet.</p>
                    </div>`;
                return;
            }

            let html = '';
            data.forEach(n => {
                let iconClass = "ri-information-fill";
                let iconColor = "#3b82f6"; // blue
                let iconBg = "rgba(59, 130, 246, 0.1)";

                if (n.type === "success") { 
                    iconClass = "ri-checkbox-circle-fill"; iconColor = "#10b981"; iconBg = "rgba(16, 185, 129, 0.1)"; 
                }
                if (n.type === "warning") { 
                    iconClass = "ri-alert-fill"; iconColor = "#f59e0b"; iconBg = "rgba(245, 158, 11, 0.1)"; 
                }
                if (n.type === "error") { 
                    iconClass = "ri-error-warning-fill"; iconColor = "#ef4444"; iconBg = "rgba(239, 68, 68, 0.1)"; 
                }

                const dateObj = new Date(n.createdAt);
                const timeStr = dateObj.toLocaleString('en-US', { 
                    month: 'short', day: '2-digit', 
                    hour: 'numeric', minute: '2-digit', hour12: true 
                });

                html += `
                <div class="notification-item ${n.isRead ? '' : 'notification-unread'}" 
                     data-id="${n.id}" data-url="${n.actionUrl || ''}">
                    <div class="notif-icon-circle" style="background: ${iconBg}; color: ${iconColor};">
                        <i class="${iconClass}"></i>
                    </div>
                    <div class="notif-details">
                        <div class="notif-message">
                            <strong>${n.title}:</strong> ${n.message}
                        </div>
                        <div class="notif-time">
                            ${timeStr}
                        </div>
                    </div>
                </div>
                `;
            });

            if (currentNotifLimit > 0 && data.length >= currentNotifLimit) {
                html += `
                <div style="padding:16px; text-align:center;">
                    <button type="button" id="btnSeeAllNotifications" 
                        style="border:none; background:transparent; color:var(--color-primary); font-size:13px; font-weight:700; cursor:pointer;">
                        See all notifications
                    </button>
                </div>
                `;
            }

            container.innerHTML = html;

            const seeAllBtn = document.getElementById('btnSeeAllNotifications');
            if (seeAllBtn) {
                seeAllBtn.addEventListener('click', function(e) {
                    e.stopPropagation();
                    currentNotifLimit = 0; 
                    loadNotifications(container);
                });
            }

            container.querySelectorAll('.notification-item').forEach(item => {
                item.addEventListener('click', function() {
                    const id = this.dataset.id;
                    const url = this.dataset.url;
                    
                    if (this.classList.contains('notification-unread')) {
                        fetch('/Notifications/MarkAsRead', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ id: parseInt(id) })
                        }).then(() => updateUnreadCount());
                    }
                    
                    if (url) window.location.href = url;
                });
            });
        })
        .catch(err => {
            console.error(err);
            container.innerHTML = '<div style="padding:20px;text-align:center;color:red;font-size:12px;">Failed to load.</div>';
        });
}

function showNotificationToast(title, message, type, actionUrl) {
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
    setTimeout(() => toast.style.transform = 'translateX(0)', 10);
    setTimeout(() => {
        toast.style.transform = 'translateX(120%)';
        setTimeout(() => toast.remove(), 300); 
    }, 5000);
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