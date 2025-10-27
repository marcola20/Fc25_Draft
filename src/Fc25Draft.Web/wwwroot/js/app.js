window.fc25Auth = {
    getToken: () => {
        try {
            return window.localStorage.getItem('fc25-admin-token');
        } catch {
            return null;
        }
    },
    setToken: (token) => {
        try {
            window.localStorage.setItem('fc25-admin-token', token);
        } catch {
            // ignorado
        }
    },
    clearToken: () => {
        try {
            window.localStorage.removeItem('fc25-admin-token');
        } catch {
            // ignorado
        }
    }
};

window.fc25Share = {
    openWhatsapp: function (shareUrl, groupLink, message) {
        try {
            if (shareUrl) {
                const shareWindow = window.open(shareUrl, '_blank');
                if (shareWindow) {
                    return 'share';
                }
            }

            if (groupLink) {
                window.open(groupLink, '_blank');
                if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function' && message) {
                    navigator.clipboard.writeText(message).catch(() => { /* noop */ });
                }
                return 'fallback';
            }
        } catch {
            // ignorado
        }

        return 'error';
    }
};
