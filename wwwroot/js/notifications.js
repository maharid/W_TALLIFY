// Notification Logic (Refactored - Modern Row-Based Layout)

let currentNotifLimit = 10;
let currentNotifFilter = 'all';

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

function getRelativeTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffInSeconds = Math.floor((now - date) / 1000);

    if (diffInSeconds < 60) return 'just now';
    if (diffInSeconds < 3600) return Math.floor(diffInSeconds / 60) + 'm';
    if (diffInSeconds < 86400) return Math.floor(diffInSeconds / 3600) + 'h';
    if (diffInSeconds < 604800) return Math.floor(diffInSeconds / 86400) + 'd';
    return Math.floor(diffInSeconds / 604800) + 'w';
}

function formatNotifMessage(title, message) {
    // Cleanup: Eliminate hardcoded prefixes and merge into prose
    // e.g. "Round Complete: Round 1 has been finalized" -> "**Round 1** has been finalized"
    
    let combined = message;
    
    // If title is descriptive but message repeats it, or title is a category
    if (title && !message.toLowerCase().includes(title.toLowerCase())) {
        // Fallback: merge them if they are distinct
        // But the goal is prose. Let's try to detect patterns.
    }

    // Bold Actors/Subjects (Names, Rounds, Contestants, Judges)
    // Simple regex to bold common targets
    combined = combined.replace(/(Round \d+|Event [^.]+?|All \d+ judges|Contestant [^.]+?|Judge [^.]+?)/gi, '<strong>$1</strong>');
    
    // Bold specific keywords that look like actors (Start of sentence or capitalized words)
    // This is a heuristic.
    return combined;
}

document.addEventListener("DOMContentLoaded", function () {
    const btn = document.getElementById('btnNotifications');
    const pop = document.getElementById('notificationsPopover');
    const closeBtn = document.getElementById('btnNotificationsClose');
    const markAllBtn = document.getElementById('btnMarkAllRead');
    const content = pop ? pop.querySelector('.notifications-content') : null;
    const tabs = pop ? pop.querySelectorAll('.notif-tab') : [];
    
    updateUnreadCount();

    if (btn && pop) {
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

        if (closeBtn) closeBtn.addEventListener('click', e => { e.stopPropagation(); closePopover(); });
        
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

    window.signalrConnection.start().catch(err => console.error("SignalR Error:", err.toString()));
});

function loadNotifications(container) {
    if (!container) return;
    
    const url = `/Notifications/GetMyNotifications?limit=${currentNotifLimit}&unreadOnly=${currentNotifFilter === 'unread'}`;
    container.innerHTML = '<div style="padding:24px; text-align:center; color:#65676b; font-size:14px;">Loading...</div>';

    fetch(url)
        .then(res => res.json())
        .then(data => {
            if (!data || data.length === 0) {
                container.innerHTML = `<div class="notifications-empty">No ${currentNotifFilter === 'unread' ? 'unread ' : ''}notifications yet.</div>`;
                return;
            }

            // Group by Time
            const today = [];
            const earlier = [];
            const now = new Date();
            now.setHours(0, 0, 0, 0);

            data.forEach(n => {
                const date = new Date(n.createdAt);
                if (date >= now) today.push(n);
                else earlier.push(n);
            });

            let html = '';

            if (today.length > 0) {
                html += `<div class="notif-section-header"><span class="notif-section-title">Today</span></div>`;
                today.forEach(n => html += renderNotifItem(n));
            }

            if (earlier.length > 0) {
                html += `
                <div class="notif-section-header" style="margin-top:8px;">
                    <span class="notif-section-title">Earlier</span>
                    <span class="notif-see-all" id="btnSeeAllEarlier">See all</span>
                </div>`;
                earlier.forEach(n => html += renderNotifItem(n));
            }

            container.innerHTML = html;

            const seeAllEarlier = document.getElementById('btnSeeAllEarlier');
            if (seeAllEarlier) {
                seeAllEarlier.onclick = (e) => {
                    e.stopPropagation();
                    currentNotifLimit = 0;
                    loadNotifications(container);
                };
            }

            container.querySelectorAll('.notification-item').forEach(item => {
                item.onclick = function() {
                    const id = this.dataset.id;
                    const url = this.dataset.url;
                    if (this.classList.contains('notif-unread')) {
                        fetch('/Notifications/MarkAsRead', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ id: parseInt(id) })
                        }).then(() => updateUnreadCount());
                    }
                    if (url) window.location.href = url;
                };
            });
        })
        .catch(err => {
            console.error(err);
            container.innerHTML = '<div style="padding:20px;text-align:center;color:red;font-size:13px;">Failed to load.</div>';
        });
}

function renderNotifItem(n) {
    let iconClass = "ri-information-line";
    let iconBg = "#1877f2"; // Default blue
    
    if (n.type === "success") { iconClass = "ri-check-line"; iconBg = "#10b981"; }
    if (n.type === "warning") { iconClass = "ri-error-warning-line"; iconBg = "#f59e0b"; }
    if (n.type === "error") { iconClass = "ri-close-line"; iconBg = "#ef4444"; }

    const timeRel = getRelativeTime(n.createdAt);
    const proseMessage = formatNotifMessage(n.title, n.message);

    return `
    <div class="notification-item ${n.isRead ? '' : 'notif-unread'}" data-id="${n.id}" data-url="${n.actionUrl || ''}">
        <div class="notif-avatar-stack">
            <img src="/images/default pfp.png" class="notif-main-avatar" alt="Avatar">
            <div class="notif-badge-icon" style="background:${iconBg}; color:#fff;">
                <i class="${iconClass}"></i>
            </div>
        </div>
        <div class="notif-details">
            <div class="notif-message">${proseMessage}</div>
            <div class="notif-time">${timeRel}</div>
            
            ${n.message.toLowerCase().includes('invite') ? `
            <div class="notif-actions">
                <button class="notif-btn-primary">Confirm</button>
                <button class="notif-btn-secondary">Delete</button>
            </div>
            ` : ''}
        </div>
        <div class="notif-status-col">
            ${n.isRead ? '' : '<div class="unread-dot"></div>'}
        </div>
    </div>
    `;
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
    toast.style.cssText = 'pointer-events:auto; cursor:pointer; background:#fff; border-radius:12px; box-shadow:0 10px 25px rgba(0,0,0,0.15); border-left:5px solid #1877f2; padding:16px; transform:translateX(120%); transition:transform 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55);';
    
    let borderColor = "#1877f2"; 
    if (type === "success") borderColor = "#10b981";
    if (type === "warning") borderColor = "#f59e0b";
    if (type === "error") borderColor = "#ef4444";

    toast.style.borderLeftColor = borderColor;
    toast.innerHTML = `
        <div style="font-size:14px; font-weight:700; color:#050505; margin-bottom:4px;">${title}</div>
        <div style="font-size:13px; color:#65676b; line-height:1.4;">${message}</div>
    `;

    if (actionUrl) {
        toast.onclick = () => window.location.href = actionUrl;
    }
    container.appendChild(toast);
    setTimeout(() => toast.style.transform = 'translateX(0)', 10);
    setTimeout(() => {
        toast.style.transform = 'translateX(120%)';
        setTimeout(() => {
            toast.remove();
            if (container.children.length === 0) container.remove();
        }, 300); 
    }, 5000);
}