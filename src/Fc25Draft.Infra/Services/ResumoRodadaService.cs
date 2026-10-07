using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Junta o que aconteceu na rodada — resultados, quem marcou, tabela e bolão — no formato
/// que o grupo espera receber.
/// </summary>
public class ResumoRodadaService : IResumoRodadaService
{
    private readonly DraftDbContext _db;
    private readonly IBolaoService _bolao;
    private readonly IDiretoriaService _diretoria;

    public ResumoRodadaService(DraftDbContext db, IBolaoService bolao, IDiretoriaService diretoria)
    {
        _db = db;
        _bolao = bolao;
        _diretoria = diretoria;
    }

    public async Task<IReadOnlyList<ResumoRodadaOpcaoDto>> RodadasAsync(int quantas, CancellationToken ct)
    {
        var rodadas = await _db.LigaRodadas.AsNoTracking()
            .Where(r => r.Partidas.Any(p => p.EncerradaEm != null || p.IniciadaEm != null))
            .Select(r => new
            {
                r.RodadaId,
                r.Numero,
                r.Desempate,
                r.DataHora,
                r.Liga.Tipo,
                r.Liga.Divisao,
                Jogos = r.Partidas.Count,
                Jogados = r.Partidas.Count(p => p.EncerradaEm != null || p.IniciadaEm != null),
                Ultimo = r.Partidas.Max(p => p.EncerradaEm ?? p.IniciadaEm)
            })
            .ToListAsync(ct);

        return rodadas
            .OrderByDescending(r => r.Ultimo)
            .Take(quantas)
            .Select(r => new ResumoRodadaOpcaoDto(
                r.RodadaId,
                Titulo(LigaLabels.Competicao(r.Tipo, r.Divisao), r.Numero, r.Desempate),
                r.DataHora ?? r.Ultimo,
                r.Jogos,
                r.Jogados == r.Jogos))
            .ToArray();
    }

    public async Task<ResumoDaRodadaDto?> MontarAsync(Guid rodadaId, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas.AsNoTracking()
            .Where(r => r.RodadaId == rodadaId)
            .Select(r => new
            {
                r.RodadaId,
                r.LigaId,
                r.Numero,
                r.Desempate,
                r.DataHora,
                r.Liga.Tipo,
                r.Liga.Divisao,
                r.Liga.Temporada
            })
            .FirstOrDefaultAsync(ct);

        if (rodada is null) return null;

        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.RodadaId == rodadaId)
            .Select(p => new
            {
                p.PartidaId,
                p.TimeCasaId,
                Casa = p.TimeCasa.TeamName,
                p.TimeForaId,
                Fora = p.TimeFora.TeamName,
                p.GolsCasa,
                p.GolsFora,
                Jogou = p.EncerradaEm != null || p.IniciadaEm != null,
                Quando = p.EncerradaEm ?? p.IniciadaEm
            })
            .ToListAsync(ct);

        var ids = partidas.Select(p => p.PartidaId).ToList();

        // Gols da rodada, para dizer quem marcou. Gol contra vai para o time adversário.
        var gols = await _db.LigaEventos.AsNoTracking()
            .Where(e => ids.Contains(e.PartidaId) && (e.Tipo == TipoEvento.Gol || e.Tipo == TipoEvento.GolContra))
            .Select(e => new Gol(e.PartidaId, e.TimeId, e.Tipo, e.Jogador.Name, e.Time.TeamName))
            .ToListAsync(ct);

        var jogos = partidas
            .Where(p => p.Jogou)
            .OrderBy(p => p.Quando)
            .Select(p => new ResumoJogoDto(
                p.Casa, p.GolsCasa, p.GolsFora, p.Fora,
                MarcadoresDe(gols, p.PartidaId, p.TimeCasaId, p.TimeForaId),
                MarcadoresDe(gols, p.PartidaId, p.TimeForaId, p.TimeCasaId)))
            .ToArray();

        var artilheiros = gols
            .Where(g => g.Tipo == TipoEvento.Gol)
            .GroupBy(g => new { g.Jogador, g.Time })
            .Select(g => new ResumoArtilheiroDto(g.Key.Jogador, g.Key.Time, g.Count()))
            .OrderByDescending(a => a.Gols).ThenBy(a => a.Nome)
            .Take(5)
            .ToArray();

        // A tabela só faz sentido na liga; na copa o que vale é o chaveamento.
        var tabela = rodada.Tipo == TipoCompetition.Liga
            ? (await _db.LigaClassificacoes.AsNoTracking()
                .Where(c => c.LigaId == rodada.LigaId)
                .Select(c => new { c.Posicao, Time = c.Time.TeamName, c.Pontos, c.Jogos, c.GolsPro, c.GolsContra })
                .ToListAsync(ct))
                .OrderBy(c => c.Posicao)
                .Select(c => new ResumoTabelaLinhaDto(c.Posicao, c.Time, c.Pontos, c.Jogos, c.GolsPro - c.GolsContra))
                .ToArray()
            : Array.Empty<ResumoTabelaLinhaDto>();

        var bolao = (await _bolao.RankingDaRodadaAsync(rodadaId, ct)).Take(3).ToArray();

        // Rodada antiga não tem hora marcada; nesse caso vale quando o último jogo saiu.
        var quando = rodada.DataHora ?? partidas.Max(p => p.Quando);

        return new ResumoDaRodadaDto(
            rodada.RodadaId,
            Titulo(LigaLabels.Competicao(rodada.Tipo, rodada.Divisao), rodada.Numero, rodada.Desempate),
            quando,
            partidas.All(p => p.Jogou),
            jogos,
            artilheiros,
            tabela,
            bolao,
            await ProximaAsync(rodada.LigaId, quando, ct),
            await DiretoriaAsync(rodada.Temporada, ids, ct));
    }

    /// <summary>Quem mudou de faixa na rodada e quem está na corda bamba. Complementar: se falhar, o resumo sai sem.</summary>
    private async Task<DiretoriaResumoRodadaDto?> DiretoriaAsync(int? temporada, IReadOnlyCollection<Guid> partidas, CancellationToken ct)
    {
        if (temporada is null) return null;
        try
        {
            var painel = await _diretoria.GetPainelAsync(temporada, ct);
            return painel is null ? null : Diretoria.ResumoDaRodada(painel.Times, partidas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private record Gol(Guid PartidaId, Guid TimeId, TipoEvento Tipo, string Jogador, string Time);

    /// <summary>Quem marcou pelo time, contando também o gol contra do adversário.</summary>
    private static IReadOnlyList<string> MarcadoresDe(
        IEnumerable<Gol> gols, Guid partidaId, Guid timeId, Guid adversarioId) =>
        gols
            .Where(g => g.PartidaId == partidaId)
            .Where(g => (g.Tipo == TipoEvento.Gol && g.TimeId == timeId)
                        || (g.Tipo == TipoEvento.GolContra && g.TimeId == adversarioId))
            .GroupBy(g => new { g.Jogador, Contra = g.Tipo == TipoEvento.GolContra })
            .Select(g => g.Key.Contra
                ? $"{g.Key.Jogador} (contra){(g.Count() > 1 ? $" x{g.Count()}" : "")}"
                : g.Count() > 1 ? $"{g.Key.Jogador} ({g.Count()})" : g.Key.Jogador)
            .ToArray();

    /// <summary>A próxima rodada marcada, de qualquer competição, para fechar o texto.</summary>
    private async Task<string?> ProximaAsync(Guid ligaId, DateTime? daRodada, CancellationToken ct)
    {
        if (daRodada is not DateTime referencia) return null;

        var proxima = await _db.LigaRodadas.AsNoTracking()
            .Where(r => r.DataHora != null && r.DataHora > referencia && r.Partidas.Any())
            .OrderBy(r => r.DataHora)
            .Select(r => new { r.Numero, r.Desempate, r.DataHora, r.Liga.Tipo, r.Liga.Divisao })
            .FirstOrDefaultAsync(ct);

        if (proxima is null) return null;

        var quando = proxima.DataHora!.Value;
        var titulo = Titulo(LigaLabels.Competicao(proxima.Tipo, proxima.Divisao), proxima.Numero, proxima.Desempate);

        return $"{titulo}, {quando:ddd dd/MM} às {quando:HH'h'}";
    }

    private static string Titulo(string competicao, int numero, bool desempate) => numero switch
    {
        0 => $"{competicao} · mata-mata",
        -1 => $"{competicao} · decisão do título",
        -2 => $"{competicao} · playoff",
        _ when desempate => $"{competicao} · jogo decisivo",
        _ => $"{competicao} · rodada {numero}"
    };
}
