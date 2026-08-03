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

window.fc25Team = {
    getToken: () => {
        try {
            return window.localStorage.getItem('fc25-team-token');
        } catch {
            return null;
        }
    },
    setToken: (token) => {
        try {
            window.localStorage.setItem('fc25-team-token', token);
        } catch {
            // ignorado
        }
    },
    clearToken: () => {
        try {
            window.localStorage.removeItem('fc25-team-token');
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

// Geração e compartilhamento de imagem (tabela / rodada) para WhatsApp e afins.
window.fc25ShareImage = (function () {
    function waitForImages(el) {
        const imgs = Array.from(el.querySelectorAll('img'));
        return Promise.all(imgs.map(img => {
            if (img.complete && img.naturalWidth > 0) return Promise.resolve();
            return new Promise(resolve => {
                img.addEventListener('load', resolve, { once: true });
                img.addEventListener('error', resolve, { once: true });
            });
        }));
    }

    async function toBlob(elementId) {
        if (typeof html2canvas !== 'function') {
            throw new Error('html2canvas não carregado.');
        }

        const el = document.getElementById(elementId);
        if (!el) {
            throw new Error('Elemento não encontrado: ' + elementId);
        }

        await waitForImages(el);

        const canvas = await html2canvas(el, {
            backgroundColor: '#ffffff',
            scale: Math.min(window.devicePixelRatio || 1, 2) * 1.5,
            useCORS: true,
            logging: false,
            onclone: function (clonedDoc) {
                const clonedEl = clonedDoc.getElementById(elementId);
                if (!clonedEl) return;
                // Revela elementos que só aparecem na imagem gerada.
                clonedEl.querySelectorAll('.share-only').forEach(function (e) {
                    e.style.display = '';
                });
                clonedEl.classList.add('share-rendering');
            }
        });

        return await new Promise(resolve => canvas.toBlob(resolve, 'image/png', 0.95));
    }

    // Retorna: 'shared' | 'downloaded' | 'error'
    async function capture(elementId, fileName, title, text) {
        try {
            const blob = await toBlob(elementId);
            if (!blob) return 'error';

            const safeName = (fileName && fileName.trim()) ? fileName : 'cbfv.png';
            const file = new File([blob], safeName, { type: 'image/png' });

            // Caminho preferido (celular): abre a folha de compartilhamento nativa (WhatsApp etc.)
            if (navigator.canShare && navigator.canShare({ files: [file] })) {
                try {
                    await navigator.share({
                        files: [file],
                        title: title || 'CBFV',
                        text: text || ''
                    });
                    return 'shared';
                } catch (err) {
                    // Usuário cancelou a folha de compartilhamento.
                    if (err && err.name === 'AbortError') return 'shared';
                    // Qualquer outro erro cai para o download.
                }
            }

            // Fallback (desktop): baixa o PNG.
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = safeName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            setTimeout(() => URL.revokeObjectURL(url), 1000);
            return 'downloaded';
        } catch (error) {
            console.error('Erro ao gerar imagem de compartilhamento:', error);
            return 'error';
        }
    }

    return { capture: capture };
})();
