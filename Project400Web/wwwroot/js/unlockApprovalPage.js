(function () {
    function init() {
        const root = document.getElementById('unlock-approval-root');
        if (!root) return;

        const requestId = root.dataset.unlockRequestId || '';
        const username = root.dataset.unlockUsername || '';
        const expiresAtMs = parseInt(root.dataset.unlockExpiresMs || '0', 10);
        const door = root.dataset.unlockDoor || '';
        const apiBaseUrl = (root.dataset.unlockApiBaseUrl || '').replace(/\/+$/, '');

        const countdownEl = document.getElementById('unlock-countdown-text');
        const countdownChip = document.getElementById('unlock-countdown-chip');
        const approveBtn = document.getElementById('unlock-approve-button');
        const denyBtn = document.getElementById('unlock-deny-button');
        const approveLabel = document.getElementById('unlock-approve-label');
        const approveSpinner = document.getElementById('unlock-approve-spinner');

        let isExpired = false;
        let isApproving = false;
        let intervalId = null;

        function setApproving(value) {
            isApproving = value;
            if (approveBtn) approveBtn.disabled = value || isExpired;
            if (denyBtn) denyBtn.disabled = value;
            if (approveLabel) approveLabel.style.display = value ? 'none' : '';
            if (approveSpinner) approveSpinner.style.display = value ? 'inline-block' : 'none';
        }

        function setExpired() {
            if (isExpired) return;
            isExpired = true;
            if (approveBtn) approveBtn.disabled = true;
            if (countdownChip) {
                countdownChip.classList.remove('mud-chip-color-warning');
                countdownChip.classList.add('mud-chip-color-error');
            }
            if (intervalId) {
                clearInterval(intervalId);
                intervalId = null;
            }
        }

        function tickCountdown() {
            if (!isFinite(expiresAtMs) || expiresAtMs <= 0) {
                if (countdownEl) countdownEl.textContent = 'Request Expired';
                setExpired();
                return;
            }
            const remaining = Math.max(0, Math.floor((expiresAtMs - Date.now()) / 1000));
            if (countdownEl) {
                countdownEl.textContent = remaining > 0
                    ? 'Expires in ' + remaining + ' seconds'
                    : 'Request Expired';
            }
            if (remaining <= 0) {
                setExpired();
            }
        }

        function navigateToComplete(status, message) {
            let url = '/unlock-complete?status=' + encodeURIComponent(status);
            if (door) url += '&door=' + encodeURIComponent(door);
            if (message) url += '&message=' + encodeURIComponent(message);
            window.location.href = url;
        }

        async function handleApprove(e) {
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }
            if (isApproving || isExpired) return;
            if (approveBtn && approveBtn.disabled) return;

            setApproving(true);

            try {
                if (typeof window.authenticateWithPasskey !== 'function') {
                    throw new Error('Passkey support not loaded');
                }

                await window.authenticateWithPasskey(username, apiBaseUrl);

                const response = await fetch(
                    apiBaseUrl + '/api/unlock/approve?requestId=' + encodeURIComponent(requestId),
                    {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: '{}'
                    }
                );

                if (!response.ok) {
                    navigateToComplete('failed', 'Failed to unlock door (HTTP ' + response.status + ')');
                    return;
                }

                navigateToComplete('approved');
            } catch (err) {
                console.error('Unlock approval failed', err);
                const message = (err && err.message) ? err.message : String(err);
                navigateToComplete('failed', 'Passkey error: ' + message);
            }
        }

        function handleDeny(e) {
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }
            if (isApproving) return;
            navigateToComplete('denied');
        }

        if (approveBtn) {
            approveBtn.addEventListener('click', handleApprove);
        }
        if (denyBtn) {
            denyBtn.addEventListener('click', handleDeny);
        }

        tickCountdown();
        if (!isExpired) {
            intervalId = setInterval(tickCountdown, 1000);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
