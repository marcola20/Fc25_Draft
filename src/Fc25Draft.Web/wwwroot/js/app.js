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
            if (groupLink) {
                let urlToOpen = groupLink;

                if (message) {
                    try {
                        const parsedUrl = new URL(groupLink);
                        parsedUrl.searchParams.set('text', message);
                        urlToOpen = parsedUrl.toString();
                    } catch {
                        // caso a URL não seja válida, mantém o link original
                    }
                }

                const groupWindow = window.open(urlToOpen, '_blank');
                if (groupWindow) {
                    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function' && message) {
                        navigator.clipboard.writeText(message).catch(() => { /* noop */ });
                    }

                    return 'group';
                }
            }

            if (shareUrl) {
                const shareWindow = window.open(shareUrl, '_blank');
                if (shareWindow) {
                    return 'share';
                }
            }
        } catch {
            // ignorado
        }

        return 'error';
    }
};
