using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

/// <summary>Título, descrição e imagem da prévia de um link (WhatsApp etc.).</summary>
public sealed record Previa(string? Titulo, string? Descricao, string? Imagem = null);

/// <summary>
/// Monta a prévia do link pela rota, no layout: o WhatsApp lê só o HTML pré-renderizado e, ali, uma
/// prévia posta pela página não substitui a do layout. Por isso quem precisa de dados (time, jogador,
/// liga) é resolvido aqui; as outras páginas usam o título e o subtítulo da navegação.
/// </summary>
public class PreviaDosLinks
{
    private readonly LayoutNavigationService _navegacao;
    private readonly IDbContextFactory<DraftDbContext> _bancos;
    private readonly ILigaPublicService _liga;
    private readonly IPricingService _precos;
    private readonly EscudoService _escudos;
    private readonly ILogger<PreviaDosLinks> _logger;

    public PreviaDosLinks(
        LayoutNavigationService navegacao,
        IDbContextFactory<DraftDbContext> bancos,
        ILigaPublicService liga,
        IPricingService precos,
        EscudoService escudos,
        ILogger<PreviaDosLinks> logger)
    {
        _navegacao = navegacao;
        _bancos = bancos;
        _liga = liga;
        _precos = precos;
        _escudos = escudos;
        _logger = logger;
    }

    /// <param name="caminho">Caminho da página, começando com "/" e sem a query.</param>
    /// <param name="query">Query da URL (sem "?"), para a liga escolhida em /liga?liga=...</param>
    public async Task<Previa?> MontarAsync(string caminho, string? query, CancellationToken ct)
    {
        try
        {
            var partes = caminho.Trim('/').Split('/');
            if (partes is ["teams", "details", var t] && Guid.TryParse(t, out var timeId))
                return await TimeAsync(timeId, ct) ?? PelaNavegacao(caminho);
            if (partes is ["players", "details", var j] && int.TryParse(j, out var jogadorId))
                return await JogadorAsync(jogadorId, ct) ?? PelaNavegacao(caminho);
            if (caminho == "/liga")
                return await LigaAsync(query, ct) ?? PelaNavegacao(caminho);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A prévia nunca derruba a página: cai na genérica.
            _logger.LogWarning(ex, "Prévia do link de {Caminho} não montada", caminho);
        }

        return PelaNavegacao(caminho);
    }

    private Previa? PelaNavegacao(string caminho)
    {
        var rota = _navegacao.DescreverRota(caminho == "/" ? "/home" : caminho);
        return rota is null ? null : new Previa(rota.Value.Titulo, rota.Value.Descricao);
    }

    private async Task<Previa?> TimeAsync(Guid timeId, CancellationToken ct)
    {
        await using var db = await _bancos.CreateDbContextAsync(ct);
        var time = await db.Teams.AsNoTracking()
            .Where(t => t.TeamId == timeId)
            .Select(t => new { t.TeamName, t.OwnerName })
            .FirstOrDefaultAsync(ct);
        if (time is null) return null;

        var de = string.IsNullOrWhiteSpace(time.OwnerName) ? "" : $", de {time.OwnerName}";
        return new Previa(time.TeamName, $"Elenco, jogos, temporada e histórico do {time.TeamName}{de}.",
            _escudos.GetEscudo(time.TeamName));
    }

    private async Task<Previa?> JogadorAsync(int jogadorId, CancellationToken ct)
    {
        await using var db = await _bancos.CreateDbContextAsync(ct);
        var jogador = await db.Players.AsNoTracking()
            .Where(p => p.PlayerId == jogadorId)
            .Select(p => new
            {
                p.Name,
                p.Overall,
                p.Age,
                Posicao = p.Position.Name,
                Time = p.CurrentTeam != null ? p.CurrentTeam.TeamName : null
            })
            .FirstOrDefaultAsync(ct);
        if (jogador is null) return null;

        var valor = (await _precos.CalculateForPlayerAsync(jogadorId, ct)).BasePrice;
        var partes = new List<string> { jogador.Posicao };
        if (jogador.Age is int idade) partes.Add($"{idade} anos");
        partes.Add(jogador.Time ?? "sem time");
        partes.Add($"valor de mercado {Dinheiro.Curto(valor)}");

        return new Previa($"{jogador.Name} ({jogador.Overall})", string.Join(" · ", partes) + ".",
            jogador.Time is null ? null : _escudos.GetEscudo(jogador.Time));
    }

    private async Task<Previa?> LigaAsync(string? query, CancellationToken ct)
    {
        var escolhida = (query ?? "").Split('&')
            .Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p.Length == 2 && p[0] == "liga" && Guid.TryParse(p[1], out _));
        var liga = escolhida is not null
            ? await _liga.GetByIdAsync(Guid.Parse(escolhida[1]), ct)
            : await _liga.GetAtualAsync(ct);
        if (liga is null) return null;

        var lider = (await _liga.GetClassificacaoAsync(liga.LigaId, ct))
            .Where(c => c.Jogos > 0)
            .OrderBy(c => c.Posicao)
            .FirstOrDefault();
        var descricao = lider is null
            ? "Classificação, rodadas, mata-mata e estatísticas."
            : $"Líder: {lider.TimeNome}, com {lider.Pontos} ponto{(lider.Pontos == 1 ? "" : "s")} em {lider.Jogos} jogo{(lider.Jogos == 1 ? "" : "s")}. Classificação, rodadas e estatísticas.";
        return new Previa(liga.Nome, descricao);
    }
}
