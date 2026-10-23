/**
 * WebAuthn Passkey Helper JS for HR.Node Web UI
 */
window.passkeyHelper = {
    /**
     * Check if WebAuthn Passkeys are supported by current browser
     */
    isSupported: function () {
        return window.PublicKeyCredential !== undefined &&
            typeof window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable === 'function';
    },

    /**
     * Convert Base64 / Base64URL string to Uint8Array
     */
    base64ToUint8Array: function (base64) {
        if (!base64) return new Uint8Array();
        var padding = '='.repeat((4 - base64.length % 4) % 4);
        var base64Clean = (base64 + padding).replace(/\-/g, '+').replace(/_/g, '/');
        var rawData = window.atob(base64Clean);
        var outputArray = new Uint8Array(rawData.length);
        for (var i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    },

    /**
     * Convert ArrayBuffer / Uint8Array to Base64URL string
     */
    arrayBufferToBase64Url: function (buffer) {
        var bytes = new Uint8Array(buffer);
        var binary = '';
        for (var i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        var base64 = window.btoa(binary);
        return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
    },

    /**
     * Perform WebAuthn Registration (Browser Prompt for Windows Hello / Touch ID)
     */
    registerPasskey: async function (optionsJson) {
        try {
            if (window.location.hostname === '127.0.0.1') {
                var target = window.location.href.replace('://127.0.0.1', '://localhost');
                window.location.href = target;
                throw new Error('متصفحات الويب لا تسمح بتسجيل البصمة عبر IP مباشر (127.0.0.1). جارٍ تحويلك إلى localhost...');
            }

            var options = JSON.parse(optionsJson);

            // Decode binary fields
            options.challenge = this.base64ToUint8Array(options.challenge);
            options.user.id = this.base64ToUint8Array(options.user.id);

            if (options.excludeCredentials) {
                options.excludeCredentials = options.excludeCredentials.map(function (c) {
                    return {
                        id: passkeyHelper.base64ToUint8Array(c.id),
                        type: c.type
                    };
                });
            }

            if (!options.rp) {
                options.rp = { name: 'API.HR.Node Passkeys' };
            }

            if (window.location.hostname.endsWith('aml.lan')) {
                options.rp.id = window.location.hostname;
            } else if (window.location.hostname === 'localhost') {
                options.rp.id = 'localhost';
            }

            var credential = await navigator.credentials.create({ publicKey: options });

            var attestationResponse = {
                id: credential.id,
                rawId: this.arrayBufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    clientDataJSON: this.arrayBufferToBase64Url(credential.response.clientDataJSON),
                    attestationObject: this.arrayBufferToBase64Url(credential.response.attestationObject)
                }
            };

            return JSON.stringify(attestationResponse);
        } catch (err) {
            console.error('WebAuthn Passkey Registration Error:', err);
            var msg = err.message || err.name || String(err);
            if (err.name === 'NotAllowedError') {
                msg = 'تعذر تسجيل البصمة: تم إلغاء العملية، أو أن شهادة SSL للموقع غير موثوقة في المتصفح (تظهر Not secure باللون الأحمر). يُشترط قفل أمان موثوق 🔒 وتفعيل PIN أو بصمة في Windows Hello.';
            } else if (err.name === 'InvalidStateError') {
                msg = 'هذه البصمة مسجلة مسبقاً لهذا الحساب على هذا الجهاز.';
            } else if (err.name === 'SecurityError' || (typeof msg === 'string' && msg.toLowerCase().includes('domain'))) {
                msg = 'نطاق الخادم غير متطابق مع نطاق الموقع الحالي (' + window.location.hostname + ').';
            }
            throw new Error(msg);
        }
    }
};
