window.webAuthn = {
    isSupported() {
        return !!window.PublicKeyCredential;
    },

    base64urlEncode(buffer) {
        const bytes = new Uint8Array(buffer);
        let str = '';
        for (let i = 0; i < bytes.length; i++) {
            str += String.fromCharCode(bytes[i]);
        }
        return btoa(str)
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=/g, '');
    },

    base64urlDecode(str) {
        str = str.replace(/-/g, '+').replace(/_/g, '/');
        while (str.length % 4) {
            str += '=';
        }
        const binaryString = atob(str);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    },

    encodeArrayBuffersAsBase64url(obj) {
        if (obj instanceof ArrayBuffer) {
            return this.base64urlEncode(obj);
        }
        if (obj instanceof Uint8Array) {
            return this.base64urlEncode(obj.buffer);
        }
        if (typeof obj !== 'object' || obj === null) {
            return obj;
        }
        if (Array.isArray(obj)) {
            return obj.map(item => this.encodeArrayBuffersAsBase64url(item));
        }
        const result = {};
        for (const [key, value] of Object.entries(obj)) {
            result[key] = this.encodeArrayBuffersAsBase64url(value);
        }
        return result;
    },

    async createCredential(optionsJson) {
        try {
            if (!this.isSupported()) {
                throw new Error('WebAuthn is not supported in this browser');
            }

            const options = JSON.parse(optionsJson);
            const effectiveDomain = window.location.hostname;

            console.log('WebAuthn createCredential - server rp.id:', options.rp?.id,
                'effectiveDomain:', effectiveDomain, 'origin:', window.location.origin);

            const publicKeyOptions = {
                challenge: this.base64urlDecode(options.challenge),
                rp: {
                    name: options.rp?.name || 'Project400 Door Access',
                    id: effectiveDomain
                },
                user: {
                    id: this.base64urlDecode(options.user.id),
                    name: options.user.name,
                    displayName: options.user.displayName
                },
                pubKeyCredParams: options.pubKeyCredParams,
                timeout: options.timeout,
                attestation: options.attestation || 'none',
                authenticatorSelection: options.authenticatorSelection
            };

            if (options.excludeCredentials) {
                publicKeyOptions.excludeCredentials = options.excludeCredentials.map(cred => ({
                    ...cred,
                    id: this.base64urlDecode(cred.id)
                }));
            }

            console.log('WebAuthn createCredential - using rp.id:', publicKeyOptions.rp.id);

            const credential = await navigator.credentials.create({
                publicKey: publicKeyOptions
            });

            if (!credential) {
                throw new Error('Failed to create credential');
            }

            const attestationResponse = {
                id: credential.id,
                rawId: this.base64urlEncode(credential.rawId),
                type: credential.type,
                response: {
                    clientDataJSON: this.base64urlEncode(credential.response.clientDataJSON),
                    attestationObject: this.base64urlEncode(credential.response.attestationObject)
                }
            };

            return JSON.stringify(attestationResponse);
        } catch (error) {
            console.error('WebAuthn create credential error:', error);
            throw error;
        }
    },

    async getCredential(optionsJson) {
        try {
            if (!this.isSupported()) {
                throw new Error('WebAuthn is not supported in this browser');
            }

            const options = JSON.parse(optionsJson);
            const effectiveDomain = window.location.hostname;

            console.log('WebAuthn getCredential - server rpId:', options.rpId,
                'effectiveDomain:', effectiveDomain, 'origin:', window.location.origin);

            const publicKeyOptions = {
                challenge: this.base64urlDecode(options.challenge),
                rpId: effectiveDomain,
                timeout: options.timeout,
                userVerification: options.userVerification
            };

            if (options.allowCredentials) {
                publicKeyOptions.allowCredentials = options.allowCredentials.map(cred => ({
                    ...cred,
                    id: this.base64urlDecode(cred.id)
                }));
            }

            console.log('WebAuthn getCredential - using rpId:', publicKeyOptions.rpId);

            const assertion = await navigator.credentials.get({
                publicKey: publicKeyOptions
            });

            if (!assertion) {
                throw new Error('Failed to get credential');
            }

            const assertionResponse = {
                id: assertion.id,
                rawId: this.base64urlEncode(assertion.rawId),
                type: assertion.type,
                response: {
                    clientDataJSON: this.base64urlEncode(assertion.response.clientDataJSON),
                    authenticatorData: this.base64urlEncode(assertion.response.authenticatorData),
                    signature: this.base64urlEncode(assertion.response.signature),
                    userHandle: assertion.response.userHandle ?
                        this.base64urlEncode(assertion.response.userHandle) : null
                }
            };

            return JSON.stringify(assertionResponse);
        } catch (error) {
            console.error('WebAuthn get credential error:', error);
            throw error;
        }
    }
};

window.unlockCountdown = {
    intervalId: null,
    stop() {
        if (this.intervalId) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
    },
    start(elementId, expiresAtMs, dotNetRef) {
        this.stop();
        const expiresAt = Number(expiresAtMs);
        if (!isFinite(expiresAt)) {
            console.warn('unlockCountdown.start: invalid expiresAtMs', expiresAtMs);
            return;
        }
        const tick = () => {
            const remaining = Math.max(0, Math.floor((expiresAt - Date.now()) / 1000));
            const el = document.getElementById(elementId);
            if (el) {
                el.textContent = remaining > 0
                    ? `Expires in ${remaining} seconds`
                    : 'Request Expired';
            }
            if (remaining <= 0) {
                this.stop();
                if (dotNetRef) {
                    try { dotNetRef.invokeMethodAsync('OnCountdownExpired'); } catch (_) { }
                }
            }
        };
        tick();
        this.intervalId = setInterval(tick, 1000);
    }
};

window.unlockApproval = {
    handler: null,
    boundButtonId: null,

    init(buttonId, username, apiBaseUrl, dotNetRef) {
        const btn = document.getElementById(buttonId);
        if (!btn) {
            return false;
        }

        if (this.handler && this.boundButtonId) {
            const oldBtn = document.getElementById(this.boundButtonId);
            if (oldBtn) {
                oldBtn.removeEventListener('click', this.handler);
            }
        }

        const handler = async (e) => {
            if (btn.disabled || btn.dataset.unlockBusy === '1') {
                return;
            }
            btn.dataset.unlockBusy = '1';

            try { dotNetRef.invokeMethodAsync('OnApproveStarted'); } catch (_) { }

            try {
                await window.authenticateWithPasskey(username, apiBaseUrl);
                await dotNetRef.invokeMethodAsync('OnApproveCompleted');
            } catch (err) {
                console.error('Unlock approval failed', err);
                const message = (err && err.message) ? err.message : String(err);
                try { await dotNetRef.invokeMethodAsync('OnApproveFailed', message); } catch (_) { }
            } finally {
                btn.dataset.unlockBusy = '0';
            }
        };

        btn.addEventListener('click', handler);
        this.handler = handler;
        this.boundButtonId = buttonId;
        return true;
    },

    teardown() {
        if (this.handler && this.boundButtonId) {
            const btn = document.getElementById(this.boundButtonId);
            if (btn) {
                btn.removeEventListener('click', this.handler);
            }
        }
        this.handler = null;
        this.boundButtonId = null;
    }
};

window.authenticateWithPasskey = async function(username, apiBaseUrl) {
    console.log('Starting passkey authentication for:', username, 'apiBaseUrl:', apiBaseUrl);

    if (!window.webAuthn.isSupported()) {
        throw new Error('WebAuthn is not supported in this browser');
    }

    const optionsResponse = await fetch(`${apiBaseUrl}/api/auth/login-options`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: username })
    });

    if (!optionsResponse.ok) {
        throw new Error('Failed to get authentication challenge');
    }

    const optionsResult = await optionsResponse.json();
    if (!optionsResult.optionsJson) {
        throw new Error('Invalid authentication options from server');
    }

    const assertionJson = await window.webAuthn.getCredential(optionsResult.optionsJson);

    const loginResponse = await fetch(`${apiBaseUrl}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            username: username,
            assertionResponse: assertionJson
        })
    });

    if (!loginResponse.ok) {
        throw new Error('Authentication verification failed');
    }

    const loginResult = await loginResponse.json();
    if (!loginResult.success) {
        throw new Error(loginResult.message || 'Passkey authentication failed');
    }

    console.log('Passkey authentication successful');
    return true;
};
