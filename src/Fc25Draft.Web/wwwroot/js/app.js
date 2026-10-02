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

    // Largura fixa (px) do cartão gerado — mantém a imagem no formato "print"
    // legível no WhatsApp, independente da largura da página (evita banner esticado).
    const RENDER_WIDTH = 700;

    async function toBlob(elementId) {
        if (typeof html2canvas !== 'function') {
            throw new Error('html2canvas não carregado.');
        }

        const el = document.getElementById(elementId);
        if (!el) {
            throw new Error('Elemento não encontrado: ' + elementId);
        }

        await waitForImages(el);

        // Fixa a largura do elemento durante a captura para o html2canvas medir
        // o tamanho certo do canvas, depois restaura o layout original.
        const prevWidth = el.style.width;
        const prevMaxWidth = el.style.maxWidth;
        el.style.width = RENDER_WIDTH + 'px';
        el.style.maxWidth = RENDER_WIDTH + 'px';

        try {
            const canvas = await html2canvas(el, {
                backgroundColor: '#ffffff',
                // Alta resolução (nitidez) mesmo em telas sem retina.
                scale: Math.max(2, window.devicePixelRatio || 1),
                useCORS: true,
                logging: false,
                windowWidth: 1200,
                onclone: function (clonedDoc) {
                    const clonedEl = clonedDoc.getElementById(elementId);
                    if (!clonedEl) return;
                    // A imagem compartilhada sai sempre no tema claro, mesmo com o site no escuro.
                    clonedDoc.documentElement.setAttribute('data-bs-theme', 'light');
                    clonedEl.style.width = RENDER_WIDTH + 'px';
                    clonedEl.style.maxWidth = RENDER_WIDTH + 'px';
                    clonedEl.style.margin = '0';
                    // Revela elementos que só aparecem na imagem gerada.
                    clonedEl.querySelectorAll('.share-only').forEach(function (e) {
                        e.style.display = '';
                    });
                    clonedEl.classList.add('share-rendering');
                }
            });

            return await new Promise(resolve => canvas.toBlob(resolve, 'image/png', 0.95));
        } finally {
            el.style.width = prevWidth;
            el.style.maxWidth = prevMaxWidth;
        }
    }

    // Retorna: 'shared' | 'downloaded' | 'error'
    async function capture(elementId, fileName, text) {
        try {
            const blob = await toBlob(elementId);
            if (!blob) return 'error';

            const safeName = (fileName && fileName.trim()) ? fileName : 'cbfv.png';
            const file = new File([blob], safeName, { type: 'image/png' });

            // Caminho preferido (celular): abre a folha de compartilhamento nativa (WhatsApp etc.)
            // Envia a imagem + o texto JUNTOS. No celular o WhatsApp usa o texto como
            // legenda da foto (fica tudo numa mensagem só). Em alguns apps de desktop o
            // texto pode aparecer separado — limitação do app receptor, não dá para forçar.
            if (navigator.canShare && navigator.canShare({ files: [file] })) {
                const shareData = { files: [file] };
                if (text && text.trim()) {
                    shareData.text = text;
                }
                try {
                    await navigator.share(shareData);
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

// Telão do draft: tela cheia (precisa de um clique do usuário, regra do navegador).
window.fc25Telao = {
    telaCheia: function () {
        const el = document.documentElement;
        if (document.fullscreenElement) { document.exitFullscreen(); return; }
        if (el.requestFullscreen) { el.requestFullscreen(); }
    },
    // Chat do telão do jogo: mostra sempre a mensagem mais nova.
    rolarParaFim: function (el) {
        if (el) el.scrollTop = el.scrollHeight;
    }
};

// Notificações no celular (Web Push), usado pelo cartão da Minha Área.
window.cbfvPush = (function () {
    function suportado() {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    }

    function chaveEmBytes(base64url) {
        const base64 = (base64url + '='.repeat((4 - base64url.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
        const bruto = atob(base64);
        return Uint8Array.from(bruto, c => c.charCodeAt(0));
    }

    async function registro() {
        await navigator.serviceWorker.register('sw.js');
        return navigator.serviceWorker.ready;
    }

    // iPhone só recebe notificação com o site instalado na tela inicial.
    function iosSemInstalar() {
        const ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
        const instalado = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
        return ios && !instalado;
    }

    function aparelho() {
        const ua = navigator.userAgent;
        const so = /android/i.test(ua) ? 'Android' : /iphone|ipad|ipod/i.test(ua) ? 'iPhone' : /windows/i.test(ua) ? 'Windows' : /mac os/i.test(ua) ? 'Mac' : 'Outro';
        const nav = /samsungbrowser/i.test(ua) ? 'Samsung Internet' : /edg\//i.test(ua) ? 'Edge' : /chrome|crios/i.test(ua) ? 'Chrome' : /firefox|fxios/i.test(ua) ? 'Firefox' : /safari/i.test(ua) ? 'Safari' : 'navegador';
        const app = window.matchMedia('(display-mode: standalone)').matches ? ' (app)' : '';
        return so + ' · ' + nav + app;
    }

    return {
        // 'sem-suporte' | 'ios-instalar' | 'negado' | 'desligado' | 'ligado'; endpoint quando ligado.
        estado: async function () {
            if (iosSemInstalar()) return { estado: 'ios-instalar', endpoint: null };
            if (!suportado()) return { estado: 'sem-suporte', endpoint: null };
            if (Notification.permission === 'denied') return { estado: 'negado', endpoint: null };
            const reg = await registro();
            const sub = await reg.pushManager.getSubscription();
            return sub ? { estado: 'ligado', endpoint: sub.endpoint } : { estado: 'desligado', endpoint: null };
        },

        // Pede permissão e assina; devolve a inscrição para o servidor guardar (ou null se a pessoa negou).
        ativar: async function (chavePublica) {
            if (!suportado()) return null;
            const permissao = await Notification.requestPermission();
            if (permissao !== 'granted') return null;
            const reg = await registro();
            let sub = await reg.pushManager.getSubscription();
            if (!sub) {
                sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: chaveEmBytes(chavePublica) });
            }
            const json = sub.toJSON();
            return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth, aparelho: aparelho() };
        },

        // Cancela a assinatura deste aparelho; devolve o endpoint que saiu.
        desativar: async function () {
            if (!suportado()) return null;
            const reg = await registro();
            const sub = await reg.pushManager.getSubscription();
            if (!sub) return null;
            const endpoint = sub.endpoint;
            await sub.unsubscribe();
            return endpoint;
        }
    };
})();

// Foto do jogador escolhida pelo admin: recorta no centro, reduz para 160×160 e comprime (WebP, ou JPEG
// onde o navegador não gera WebP) para caber na conexão do Blazor (~32 KB por mensagem).
window.cbfvFoto = {
    ler: async function (inputId) {
        const input = document.getElementById(inputId);
        const arquivo = input && input.files && input.files[0];
        if (!arquivo) return null;

        const url = URL.createObjectURL(arquivo);
        try {
            const img = await new Promise((ok, erro) => {
                const i = new Image();
                i.onload = () => ok(i);
                i.onerror = erro;
                i.src = url;
            });
            const lado = Math.min(img.naturalWidth, img.naturalHeight);
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = 160;
            canvas.getContext('2d').drawImage(img,
                (img.naturalWidth - lado) / 2, (img.naturalHeight - lado) / 2, lado, lado, 0, 0, 160, 160);

            for (const qualidade of [0.85, 0.7, 0.55]) {
                let dados = canvas.toDataURL('image/webp', qualidade);
                if (!dados.startsWith('data:image/webp')) dados = canvas.toDataURL('image/jpeg', qualidade);
                const base64 = dados.substring(dados.indexOf(',') + 1);
                if (base64.length < 28000) {
                    return { base64: base64, tipo: dados.substring(5, dados.indexOf(';')) };
                }
            }
            throw new Error('Imagem grande demais mesmo comprimida.');
        } finally {
            URL.revokeObjectURL(url);
            input.value = '';
        }
    }
};
