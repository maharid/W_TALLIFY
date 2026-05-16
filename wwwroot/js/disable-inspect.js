document.addEventListener('DOMContentLoaded', function() {
    let rightClickCount = 0;
    let rightClickTimeout;
    let lastMouseX = 0;
    let lastMouseY = 0;

    // Track mouse position
    document.addEventListener('mousemove', function(e) {
        lastMouseX = e.clientX;
        lastMouseY = e.clientY;
    });

    function showProtectionToast(x, y) {
        // If x or y are undefined (e.g. triggered by keyboard), center it
        if (x === undefined) x = window.innerWidth / 2;
        if (y === undefined) y = window.innerHeight / 2;

        const toast = document.createElement('div');
        toast.className = 'protection-toast';
        
        // Use Remix Icon for the yellow triangle warning
        toast.innerHTML = `
            <i class="ri-error-warning-fill" style="color: #f59e0b; font-size: 20px;"></i>
            <span>ALERT: Content is protected !!</span>
        `;

        // Apply styles directly for precise control
        Object.assign(toast.style, {
            position: 'fixed',
            left: x + 'px',
            top: y + 'px',
            transform: 'translate(-50%, -120%)', // Position slightly above the cursor
            backgroundColor: '#ffffff',
            border: '1px solid rgba(255, 0, 122, 0.4)', // Soft red/pink border
            borderRadius: '12px',
            padding: '12px 18px',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            boxShadow: '0 4px 15px rgba(239, 68, 68, 0.2), 0 0 10px rgba(239, 68, 68, 0.1)', // Subtle light-red glow
            zIndex: '10000',
            pointerEvents: 'none',
            opacity: '0',
            transition: 'opacity 0.3s ease',
            whiteSpace: 'nowrap',
            color: '#4b5563', // Dark grey font
            fontWeight: '600',
            fontSize: '14px',
            fontFamily: 'inherit'
        });

        document.body.appendChild(toast);

        // Fade in
        requestAnimationFrame(() => {
            toast.style.opacity = '1';
        });

        // Remove after 2.5 seconds
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.addEventListener('transitionend', () => {
                toast.remove();
            });
        }, 2500);
    }

    // Disable right-click and track attempts
    document.addEventListener('contextmenu', function(e) {
        e.preventDefault();
        
        rightClickCount++;
        clearTimeout(rightClickTimeout);
        
        if (rightClickCount >= 2) {
            showProtectionToast(e.clientX, e.clientY);
            rightClickCount = 0;
        } else {
            rightClickTimeout = setTimeout(() => {
                rightClickCount = 0;
            }, 2000);
        }
    });

    // Disable keyboard shortcuts
    document.addEventListener('keydown', function(e) {
        const keys = [
            (e.keyCode === 123), // F12
            (e.ctrlKey && e.shiftKey && e.keyCode === 73), // Ctrl+Shift+I
            (e.ctrlKey && e.shiftKey && e.keyCode === 74), // Ctrl+Shift+J
            (e.ctrlKey && e.shiftKey && e.keyCode === 67), // Ctrl+Shift+C
            (e.ctrlKey && e.keyCode === 85), // Ctrl+U
            (e.ctrlKey && e.keyCode === 83)  // Ctrl+S
        ];

        if (keys.some(k => k)) {
            e.preventDefault();
            // Trigger toast at last known mouse position or center
            showProtectionToast(lastMouseX, lastMouseY);
            return false;
        }
    });
});
