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

// Foto do jogador (admin) ou do treinador (perfil): até 600×600, a mesma medida das figurinhas do álbum.
// Arquivo já quadrado, de até 600 px e até 300 KB, sobe do jeito que veio (a foto editada não é recomprimida).
// Fora isso, recorta o quadrado do meio, reduz e comprime: WebP; onde o navegador não gera WebP, PNG para
// não perder o fundo transparente e, se ainda ficar grande, JPEG sobre fundo branco.
window.cbfvFoto = {
    ler: async function (inputId, lado) {
        lado = lado || 600;
        const LIMITE = 300 * 1024; // o mesmo do servidor
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

            const tiposAceitos = ['image/webp', 'image/png', 'image/jpeg'];
            if (img.naturalWidth === img.naturalHeight && img.naturalWidth <= lado
                && arquivo.size <= LIMITE && tiposAceitos.includes(arquivo.type)) {
                const dados = await new Promise((ok, erro) => {
                    const leitor = new FileReader();
                    leitor.onload = () => ok(leitor.result);
                    leitor.onerror = erro;
                    leitor.readAsDataURL(arquivo);
                });
                return { base64: dados.substring(dados.indexOf(',') + 1), tipo: arquivo.type };
            }

            const menor = Math.min(img.naturalWidth, img.naturalHeight);
            const tamanho = Math.min(lado, menor);
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = tamanho;
            const ctx = canvas.getContext('2d');
            const desenhar = () => ctx.drawImage(img,
                (img.naturalWidth - menor) / 2, (img.naturalHeight - menor) / 2, menor, menor, 0, 0, tamanho, tamanho);
            desenhar();

            const cabe = dados => dados.length - dados.indexOf(',') - 1 <= LIMITE * 4 / 3;
            const resultado = dados => ({ base64: dados.substring(dados.indexOf(',') + 1), tipo: dados.substring(5, dados.indexOf(';')) });

            for (const qualidade of [0.9, 0.8, 0.7, 0.55, 0.4]) {
                const webp = canvas.toDataURL('image/webp', qualidade);
                if (!webp.startsWith('data:image/webp')) break;
                if (cabe(webp)) return resultado(webp);
            }

            const png = canvas.toDataURL('image/png');
            if (png.startsWith('data:image/png') && cabe(png)) return resultado(png);

            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, tamanho, tamanho);
            desenhar();
            for (const qualidade of [0.85, 0.7, 0.55, 0.4]) {
                const jpeg = canvas.toDataURL('image/jpeg', qualidade);
                if (cabe(jpeg)) return resultado(jpeg);
            }
            throw new Error('Imagem grande demais mesmo comprimida.');
        } finally {
            URL.revokeObjectURL(url);
            input.value = '';
        }
    }
};

// Álbum de figurinhas: o holográfico das brilhantes e lendárias segue o mouse (a carta inclina e o
// reflexo anda) e, no celular, o giroscópio — só onde o navegador entrega sem pedir permissão (o iPhone
// pede; lá fica o brilho que anda sozinho). Escuta no documento inteiro: vale para qualquer carta que
// aparecer, sem ligar nada por carta.
window.cbfvHolo = (function () {
    const SELETOR = '.carta.r-brilhante, .carta.r-lendaria, .carta.r-tecnico';
    const reduzido = window.matchMedia('(prefers-reduced-motion: reduce)');
    const INCLINACAO = 14; // graus no canto da carta
    let atual = null;
    let ultimo = null;
    let quadro = 0;

    function soltar(el) {
        el.classList.remove('holo-ativo');
        ['--px', '--py', '--rx', '--ry'].forEach(function (p) { el.style.removeProperty(p); });
    }

    function aplicar() {
        quadro = 0;
        const e = ultimo;
        if (!e) return;
        const el = e.target && e.target.closest ? e.target.closest(SELETOR) : null;
        if (atual && atual !== el) { soltar(atual); atual = null; }
        if (!el) return;

        const r = el.getBoundingClientRect();
        const x = (e.clientX - r.left) / r.width;
        const y = (e.clientY - r.top) / r.height;
        if (x < 0 || x > 1 || y < 0 || y > 1) { soltar(el); return; }

        atual = el;
        el.classList.add('holo-ativo');
        el.style.setProperty('--px', (x * 100).toFixed(1));
        el.style.setProperty('--py', (y * 100).toFixed(1));
        if (!reduzido.matches) {
            el.style.setProperty('--rx', ((0.5 - y) * INCLINACAO).toFixed(2) + 'deg');
            el.style.setProperty('--ry', ((x - 0.5) * INCLINACAO).toFixed(2) + 'deg');
        }
    }

    document.addEventListener('pointermove', function (e) {
        if (e.pointerType === 'touch') return;
        ultimo = e;
        if (!quadro) quadro = requestAnimationFrame(aplicar);
    }, { passive: true });

    // Saiu da janela: a carta volta ao lugar.
    document.addEventListener('pointerout', function (e) {
        if (!e.relatedTarget && atual) { soltar(atual); atual = null; }
    });

    const temGiroscopio = 'DeviceOrientationEvent' in window
        && typeof DeviceOrientationEvent.requestPermission !== 'function'
        && window.matchMedia('(pointer: coarse)').matches;

    if (temGiroscopio) {
        const raiz = document.documentElement;
        let base = null;
        let quadroGiro = 0;
        let leitura = null;
        const limite = function (v) { return Math.max(-20, Math.min(20, v)); };

        function aplicarGiro() {
            quadroGiro = 0;
            const e = leitura;
            if (!base) base = { b: e.beta, g: e.gamma };
            // A posição "parada" vai acompanhando devagar o jeito que a pessoa segura o celular.
            base.b += (e.beta - base.b) * 0.02;
            base.g += (e.gamma - base.g) * 0.02;
            const db = limite(e.beta - base.b);
            const dg = limite(e.gamma - base.g);
            raiz.classList.add('holo-giroscopio');
            raiz.style.setProperty('--gpx', (50 + dg * 2.5).toFixed(1));
            raiz.style.setProperty('--gpy', (50 + db * 2.5).toFixed(1));
            if (!reduzido.matches) {
                raiz.style.setProperty('--grx', (-db * 0.4).toFixed(2) + 'deg');
                raiz.style.setProperty('--gry', (dg * 0.4).toFixed(2) + 'deg');
            }
        }

        window.addEventListener('deviceorientation', function (e) {
            if (e.beta == null || e.gamma == null) return;
            leitura = e;
            if (!quadroGiro) quadroGiro = requestAnimationFrame(aplicarGiro);
        }, { passive: true });
    }

    return {
        /** Se o sistema pediu menos movimento (o pacote abre sem animação). */
        movimentoReduzido: function () { return reduzido.matches; }
    };
})();
