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

window.fc25Files = {
    saveFileFromBase64: function (fileName, contentType, base64Data) {
        try {
            if (!base64Data) {
                throw new Error('Conteúdo do arquivo ausente.');
            }

            const safeName = (typeof fileName === 'string' && fileName.trim()) ? fileName : 'download.csv';
            const type = (typeof contentType === 'string' && contentType.trim()) ? contentType : 'application/octet-stream';

            const binary = atob(base64Data);
            const length = binary.length;
            const bytes = new Uint8Array(length);

            for (let i = 0; i < length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }

            const blob = new Blob([bytes], { type: type });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = safeName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Erro ao salvar o arquivo exportado:', error);
            throw error;
        }
    }
};

window.fc25Unsaved = (function () {
    let handler = null;

    function enable(message) {
        if (handler) {
            return;
        }

        handler = function (event) {
            event.preventDefault();
            if (message) {
                event.returnValue = message;
                return message;
            }

            event.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', handler);
    }

    function disable() {
        if (!handler) {
            return;
        }

        window.removeEventListener('beforeunload', handler);
        handler = null;
    }

    return {
        enable: enable,
        disable: disable
    };
})();
