using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Abas de páginas da faixa embaixo do topo, uma lista por conexão. Cada página aberta vira uma aba e
/// continua viva escondida (rolagem, filtros e modal ficam como estavam); a Central CBFV é a primeira e
/// não fecha. O <see cref="Components.Layouts.AdminRouteView"/> avisa a rota de cada navegação
/// (<see cref="DefinirRota"/>) e o AppLayout desenha as páginas das abas vivas, só a ativa à mostra.
/// </summary>
public sealed class AbasService
{
    /// <summary>Páginas vivas ao mesmo tempo; passou disso, a usada há mais tempo dorme (fica na faixa e recarrega ao voltar).</summary>
    public const int MaximoVivas = 10;

    private const int MaximoGuardadas = 30;
    private const string Inicio = "/home";

    private readonly NavigationManager _nav;
    private readonly LayoutNavigationService _menu;
    private readonly LayoutState _layout;
    private readonly List<Aba> _abas = new();
    private long _relogio;
    private string? _uriDaRota;
    private AbaAberta? _mostrarDepois;
    private Aba? _escolhida;

    public AbasService(NavigationManager nav, LayoutNavigationService menu, LayoutState layout)
    {
        _nav = nav;
        _menu = menu;
        _layout = layout;
        _abas.Add(NovaAba(Inicio, fixa: true));

        // O cabeçalho (título, caminho, botões) é da página da frente: fica guardado na aba dela.
        _layout.StateChanged += () =>
        {
            if (Ativa is { Instancia: not null } ativa && !_restaurandoCabecalho) ativa.Cabecalho = _layout.Atual;
        };
    }

    private bool _restaurandoCabecalho;

    public event Action? Mudou;

    public IReadOnlyList<Aba> Abas => _abas;

    public Aba? Ativa { get; private set; }

    /// <summary>A navegação voltou para uma aba que já estava viva: o cabeçalho dela foi devolvido e o layout não deve remontá-lo pela rota.</summary>
    public bool ReativouAgora { get; private set; }

    /// <summary>
    /// Endereço a pôr na barra sem abrir nada novo: o menu levou a "/liga" e a aba da Liga aberta está em
    /// "/liga?liga=...". O layout corrige depois de desenhar.
    /// </summary>
    public string? UrlParaCorrigir { get; set; }

    /// <summary>
    /// A página desta navegação está numa aba. Falso no "não encontrado" e nas telas de outro layout (telão):
    /// aí o layout mostra o corpo normal, sem aba.
    /// </summary>
    public bool RotaAtualTemAba(string uri) => _uriDaRota is not null && _uriDaRota == uri;

    public IEnumerable<Aba> Vivas => _abas.Where(a => a.Instancia is not null);

    /// <summary>Chamado a cada navegação (e quando o login troca a página por "acesso restrito").</summary>
    public void DefinirRota(RouteData rota, bool usaAbas)
    {
        ReativouAgora = false;
        if (!usaAbas)
        {
            _uriDaRota = null;
            return;
        }

        _uriDaRota = _nav.Uri;
        var url = Normalizar(_nav.Uri);
        var escolhida = _escolhida;
        _escolhida = null;

        // Clique numa aba da faixa: é ela, mesmo que outra tenha o mesmo endereço.
        var aba = escolhida is not null && _abas.Contains(escolhida) && MesmaUrl(escolhida.Url, url) ? escolhida : null;

        // Filtro dentro da própria página (trocar de edição na Liga, de adversário nos confrontos): a aba é
        // a mesma, só o endereço muda. Outra aba que já estava nesse endereço fecha (seria repetida).
        if (aba is null && url.Contains('?') && Ativa is { Instancia: { } daFrente } && daFrente.Tipo == rota.PageType
            && MesmoCaminho(Ativa.Url, url) && !MesmaUrl(Ativa.Url, url))
        {
            aba = Ativa;
            aba.Url = url;
            // A repetida já tinha o nome deste endereço (o título da página pode nem mudar no navegador).
            var repetida = _abas.FirstOrDefault(a => a != aba && !a.Fixa && MesmaUrl(a.Url, url));
            if (repetida is not null) aba.Titulo = repetida.Titulo;
            _abas.RemoveAll(a => a != aba && !a.Fixa && MesmaUrl(a.Url, url));
        }

        aba ??= _abas.FirstOrDefault(a => MesmaUrl(a.Url, url));

        // Do menu (sem parâmetros) para uma página que já está aberta com parâmetros: volta para ela.
        if (aba is null && !url.Contains('?'))
        {
            aba = _abas.Where(a => MesmoCaminho(a.Url, url)).OrderByDescending(a => a.UltimoUso).FirstOrDefault();
            if (aba is not null) UrlParaCorrigir = aba.Url;
        }

        if (aba is null)
        {
            aba = NovaAba(url, fixa: false);
            _abas.Add(aba);
        }

        var anterior = Ativa;
        var jaViva = aba.Instancia is { } viva && viva.Mesma(rota);
        if (!jaViva)
        {
            aba.Instancia = new InstanciaDaAba(rota, ++_relogio);
        }

        Ativa = aba;
        aba.UltimoUso = ++_relogio;
        foreach (var a in _abas)
        {
            if (a.Instancia is { } i) i.Contexto.Escondida = a != aba;
        }

        if (jaViva && anterior != aba)
        {
            ReativouAgora = true;
            if (aba.Cabecalho is { } cabecalho)
            {
                _restaurandoCabecalho = true;
                try { _layout.Restaurar(cabecalho); }
                finally { _restaurandoCabecalho = false; }
            }
            _mostrarDepois = aba.Instancia!.Contexto;
        }

        AdormecerExcesso();
        Mudou?.Invoke();
    }

    /// <summary>A faixa vai navegar para esta aba (clique nela, no menu "…" ou a vizinha da que fechou).</summary>
    public void Escolher(Aba aba) => _escolhida = aba;

    /// <summary>Avisa a página que voltou para a frente; o layout chama depois de desenhar.</summary>
    public void AvisarMostrada()
    {
        var contexto = _mostrarDepois;
        _mostrarDepois = null;
        contexto?.AvisarMostrada();
    }

    /// <summary>Fecha a aba. Se era a ativa, devolve a vizinha (a da direita, senão a da esquerda) para ir.</summary>
    public Aba? Fechar(Aba aba)
    {
        var indice = _abas.IndexOf(aba);
        if (aba.Fixa || indice < 0) return null;

        _abas.RemoveAt(indice);
        aba.Instancia = null;
        Aba? destino = null;
        if (aba == Ativa)
        {
            destino = _abas[Math.Min(indice, _abas.Count - 1)];
        }

        Mudou?.Invoke();
        return destino;
    }

    /// <summary>O login mudou: as páginas escondidas dormem, para nenhuma de admin ficar viva depois de sair.</summary>
    public void AoMudarLogin()
    {
        foreach (var aba in _abas.Where(a => a != Ativa)) aba.Instancia = null;
        Mudou?.Invoke();
    }

    /// <summary>
    /// Título que a página pôs no navegador (o &lt;PageTitle&gt;). O Blazor mostra o da página aberta por último
    /// entre as vivas, então é dela; só vale para aba fora do menu (detalhe de time, jogador, edição da Liga).
    /// </summary>
    public bool DefinirTituloDaPagina(string titulo)
    {
        // No "não encontrado" (sem aba) o título é daquela tela, não de uma aba.
        if (!RotaAtualTemAba(_nav.Uri)) return false;

        var dona = Vivas.OrderByDescending(a => a.Instancia!.Criada).FirstOrDefault();
        var limpo = LimparTitulo(titulo);
        if (dona is null || dona.Fixa || !dona.NomeDaPagina || string.IsNullOrWhiteSpace(limpo) || dona.Titulo == limpo)
            return false;

        dona.Titulo = limpo;
        Mudou?.Invoke();
        return true;
    }

    public string Serializar() => JsonSerializer.Serialize(_abas.Take(MaximoGuardadas)
        .Select(a => new AbaGuardada(a.Url, a.Titulo, a.Icone)));

    /// <summary>Volta as abas guardadas no navegador (depois do F5). As que não são a atual voltam dormindo.</summary>
    public void Restaurar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        List<AbaGuardada>? guardadas;
        try
        {
            guardadas = JsonSerializer.Deserialize<List<AbaGuardada>>(json);
        }
        catch (JsonException)
        {
            return;
        }

        if (guardadas is null || guardadas.Count == 0) return;

        var novas = new List<Aba> { _abas[0] };
        foreach (var g in guardadas.Take(MaximoGuardadas))
        {
            if (!EnderecoValido(g.U)) continue;
            var url = Normalizar(_nav.ToAbsoluteUri(g.U).ToString());
            if (novas.Any(a => MesmaUrl(a.Url, url))) continue;

            var existente = _abas.FirstOrDefault(a => MesmaUrl(a.Url, url));
            if (existente is not null)
            {
                novas.Add(existente);
                continue;
            }

            var aba = NovaAba(url, fixa: false);
            if (aba.NomeDaPagina && !string.IsNullOrWhiteSpace(g.T)) aba.Titulo = g.T!;
            novas.Add(aba);
        }

        // As abertas nesta conexão que não estavam guardadas (a página do F5, por exemplo) vão para o fim.
        novas.AddRange(_abas.Where(a => !novas.Contains(a)));
        _abas.Clear();
        _abas.AddRange(novas);
        Mudou?.Invoke();
    }

    private Aba NovaAba(string url, bool fixa)
    {
        var (titulo, icone, doMenu) = _menu.DescreverAba(Caminho(url));
        return new Aba(url, titulo, icone, fixa, doMenu);
    }

    private void AdormecerExcesso()
    {
        var vivas = Vivas.ToList();
        foreach (var aba in vivas.Where(a => a != Ativa).OrderBy(a => a.UltimoUso).Take(Math.Max(0, vivas.Count - MaximoVivas)))
        {
            aba.Instancia = null;
        }
    }

    private string Normalizar(string uri)
    {
        var relativo = _nav.ToBaseRelativePath(uri);
        var hash = relativo.IndexOf('#');
        if (hash >= 0) relativo = relativo[..hash];

        var partes = relativo.Split('?', 2);
        var caminho = "/" + partes[0].Trim('/');
        if (caminho == "/") caminho = Inicio;
        return partes.Length > 1 && partes[1].Length > 0 ? $"{caminho}?{partes[1]}" : caminho;
    }

    private static string Caminho(string url)
    {
        var q = url.IndexOf('?');
        return q >= 0 ? url[..q] : url;
    }

    private static bool MesmaUrl(string a, string b) =>
        MesmoCaminho(a, b) && string.Equals(a[Caminho(a).Length..], b[Caminho(b).Length..], StringComparison.Ordinal);

    private static bool MesmoCaminho(string a, string b) =>
        string.Equals(Caminho(a), Caminho(b), StringComparison.OrdinalIgnoreCase);

    // Só endereços deste site: nada de "//outro.site" ou "javascript:" vindo do navegador.
    private static bool EnderecoValido(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\') && url.Length <= 500;

    // Tira só o sufixo do site: "Formato da Competição — CBFV Retro 2008/09" → "Formato da Competição",
    // "Diretoria — Admin" → "Diretoria"; "Brasileirão — Série A 2010" fica inteiro.
    private static string LimparTitulo(string titulo)
    {
        var limpo = titulo.Trim();
        foreach (var separador in new[] { " — ", " · ", " | ", " - " })
        {
            var i = limpo.LastIndexOf(separador, StringComparison.Ordinal);
            if (i <= 0) continue;
            var sufixo = limpo[(i + separador.Length)..].Trim();
            if (sufixo.Contains("CBFV", StringComparison.OrdinalIgnoreCase) || sufixo.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                limpo = limpo[..i].Trim();
        }

        return limpo.Equals("CBFV", StringComparison.OrdinalIgnoreCase) ? string.Empty : limpo;
    }

    private sealed record AbaGuardada(string U, string? T, string? I);
}

public sealed class Aba
{
    private static int _proximoId;

    internal Aba(string url, string titulo, string icone, bool fixa, bool doMenu)
    {
        Id = System.Threading.Interlocked.Increment(ref _proximoId);
        Url = url;
        Titulo = titulo;
        TituloDoMenu = titulo;
        Icone = icone;
        Fixa = fixa;
        _doMenu = doMenu;
    }

    private readonly bool _doMenu;

    public int Id { get; }

    /// <summary>Endereço relativo, com a query ("/liga?liga=...").</summary>
    public string Url { get; internal set; }

    public string Titulo { get; internal set; }

    public string Icone { get; }

    /// <summary>A Central CBFV: primeira da faixa, sem X.</summary>
    public bool Fixa { get; }

    internal string TituloDoMenu { get; }

    /// <summary>O nome vem do título que a página põe (nome do time, do jogador, da edição), não do menu.</summary>
    public bool NomeDaPagina => !_doMenu || Url.Contains('?');

    /// <summary>Página montada; nula quando a aba dorme (ainda não aberta nesta conexão, ou sobrou do limite).</summary>
    public InstanciaDaAba? Instancia { get; internal set; }

    internal LayoutState.Retrato? Cabecalho { get; set; }

    internal long UltimoUso { get; set; }
}

public sealed class InstanciaDaAba
{
    internal InstanciaDaAba(RouteData rota, long criada)
    {
        Tipo = rota.PageType;
        Valores = rota.RouteValues;
        Criada = criada;
        // Igual ao RouteView: a página com os valores da rota como parâmetros.
        Conteudo = builder =>
        {
            builder.OpenComponent(0, Tipo);
            foreach (var (nome, valor) in Valores) builder.AddComponentParameter(1, nome, valor);
            builder.CloseComponent();
        };
    }

    public Type Tipo { get; }

    public IReadOnlyDictionary<string, object?> Valores { get; }

    public RenderFragment Conteudo { get; }

    public AbaAberta Contexto { get; } = new();

    public ErrorBoundary? Erro { get; set; }

    internal long Criada { get; }

    internal bool Mesma(RouteData rota) =>
        rota.PageType == Tipo
        && rota.RouteValues.Count == Valores.Count
        && rota.RouteValues.All(kv => Valores.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));
}

/// <summary>
/// Passado em cascata para a página da aba. Enquanto ela está escondida, a query do endereço é a de outra
/// aba: página que reage a [SupplyParameterFromQuery] deve ignorar (Escondida) e pode se atualizar em Mostrada.
/// </summary>
public sealed class AbaAberta
{
    public bool Escondida { get; internal set; }

    /// <summary>A aba voltou para a frente (já desenhada).</summary>
    public event Action? Mostrada;

    internal void AvisarMostrada() => Mostrada?.Invoke();
}
