// US0005: Session Management JavaScript
// Implements AC3 (session timeout warning) and AC8 (idle timeout)

(function () {
    'use strict';

    // Configuration
    const IDLE_TIMEOUT_MS = 30 * 60 * 1000; // 30 minutes idle timeout
    const IDLE_WARNING_MS = 5 * 60 * 1000;  // 5 minutes warning before idle logout
    const SESSION_WARNING_MS = 10 * 60 * 1000; // 10 minutes before session expiry warning

    let idleTimer = null;
    let warningTimer = null;
    let idleWarningShown = false;

    // Create warning modal HTML
    function createIdleWarningModal() {
        const modal = document.createElement('div');
        modal.id = 'idleWarningModal';
        modal.className = 'modal fade';
        modal.setAttribute('tabindex', '-1');
        modal.setAttribute('data-bs-backdrop', 'static');
        modal.innerHTML = `
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-warning">
                        <h5 class="modal-title">
                            <i class="bi bi-clock-history me-2"></i>Session Idle Warning
                        </h5>
                    </div>
                    <div class="modal-body text-center">
                        <i class="bi bi-hourglass-split display-4 text-warning mb-3"></i>
                        <p class="lead">You've been idle for a while.</p>
                        <p>Click "Continue" to stay logged in, or you'll be logged out in <strong id="idleCountdown">5</strong> minutes.</p>
                    </div>
                    <div class="modal-footer justify-content-center">
                        <button type="button" class="btn btn-primary btn-lg" id="continueSessionBtn">
                            <i class="bi bi-check-circle me-2"></i>Continue Session
                        </button>
                    </div>
                </div>
            </div>
        `;
        document.body.appendChild(modal);
        return modal;
    }

    // Create session expiry warning banner
    function createSessionWarningBanner() {
        const banner = document.createElement('div');
        banner.id = 'sessionWarningBanner';
        banner.className = 'alert alert-warning alert-dismissible position-fixed top-0 start-50 translate-middle-x mt-2 shadow';
        banner.style.zIndex = '1055';
        banner.style.display = 'none';
        banner.innerHTML = `
            <i class="bi bi-exclamation-triangle me-2"></i>
            Your session will expire soon.
            <a href="#" id="extendSessionLink" class="alert-link">Click here to stay logged in.</a>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        document.body.appendChild(banner);
        return banner;
    }

    // Reset idle timer on activity
    function resetIdleTimer() {
        if (idleTimer) clearTimeout(idleTimer);
        if (warningTimer) clearTimeout(warningTimer);

        // Hide warning if shown
        if (idleWarningShown) {
            hideIdleWarning();
        }

        // Set new idle timer
        idleTimer = setTimeout(showIdleWarning, IDLE_TIMEOUT_MS);
    }

    // Show idle warning modal
    function showIdleWarning() {
        idleWarningShown = true;
        const modal = document.getElementById('idleWarningModal');
        const bsModal = new bootstrap.Modal(modal);
        bsModal.show();

        // Start countdown
        let countdown = 5;
        const countdownEl = document.getElementById('idleCountdown');
        countdownEl.textContent = countdown;

        warningTimer = setInterval(() => {
            countdown--;
            countdownEl.textContent = countdown;
            if (countdown <= 0) {
                clearInterval(warningTimer);
                performIdleLogout();
            }
        }, 60000); // Update every minute
    }

    // Hide idle warning modal
    function hideIdleWarning() {
        idleWarningShown = false;
        if (warningTimer) {
            clearInterval(warningTimer);
            warningTimer = null;
        }
        const modal = document.getElementById('idleWarningModal');
        const bsModal = bootstrap.Modal.getInstance(modal);
        if (bsModal) {
            bsModal.hide();
        }
    }

    // Perform idle logout
    function performIdleLogout() {
        // Create and submit logout form
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = '/Account/Logout';

        // Add antiforgery token if available
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            const tokenInput = document.createElement('input');
            tokenInput.type = 'hidden';
            tokenInput.name = '__RequestVerificationToken';
            tokenInput.value = token.value;
            form.appendChild(tokenInput);
        }

        document.body.appendChild(form);
        form.submit();
    }

    // Extend session (make a request to keep session alive)
    function extendSession() {
        fetch('/health', { method: 'GET', credentials: 'same-origin' })
            .then(() => {
                resetIdleTimer();
                const banner = document.getElementById('sessionWarningBanner');
                if (banner) {
                    banner.style.display = 'none';
                }
            })
            .catch(err => console.error('Failed to extend session:', err));
    }

    // Initialize
    function init() {
        // Only run for authenticated users (check if logout form exists)
        const logoutForm = document.querySelector('form[action*="Logout"]');
        if (!logoutForm) return;

        // Create UI elements
        createIdleWarningModal();
        createSessionWarningBanner();

        // Set up activity listeners
        const events = ['mousedown', 'keydown', 'scroll', 'touchstart'];
        events.forEach(event => {
            document.addEventListener(event, resetIdleTimer, { passive: true });
        });

        // Set up continue button
        document.getElementById('continueSessionBtn').addEventListener('click', () => {
            hideIdleWarning();
            extendSession();
        });

        // Set up session extend link
        document.getElementById('extendSessionLink').addEventListener('click', (e) => {
            e.preventDefault();
            extendSession();
        });

        // Start idle timer
        resetIdleTimer();
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
