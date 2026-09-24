using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public record CarreiraLigaInput(
    Guid LigaId,
    string Nome,
    TipoCompetition Tipo,
    int? Temporada,
    DateTime CriadaEm,
    Guid? CampeaoTimeId,
    // A competição já tinha o contador de jogos (escalações registradas).
    bool TemContagem);

/// <summary>Evento em que o jogador aparece: como autor (gol, cartão) ou como assistente.</summary>
public record CarreiraEventoInput(TipoEvento Tipo, int JogadorId, int? AssistenteId, Guid TimeId, Guid PartidaId, Guid LigaId);

/// <summary>Partida em que o jogador entrou em campo (titular ou substituto).</summary>
public record CarreiraParticipacaoInput(Guid TimeId, Guid PartidaId, Guid LigaId, bool SemSofrerGol);

/// <summary>Soma a carreira do jogador por competição e por time, com os títulos que ele ajudou a ganhar.</summary>
public static class CarreiraJogador
{
    public static JogadorCarreiraEstatisticasDto Calcular(
        int jogadorId,
        bool defensor,
        IReadOnlyDictionary<Guid, CarreiraLigaInput> ligas,
        IEnumerable<CarreiraEventoInput> eventos,
        IEnumerable<CarreiraParticipacaoInput> participacoes,
        IReadOnlyDictionary<Guid, string> nomesTimes)
    {
        var evs = eventos.Where(e => ligas.ContainsKey(e.LigaId)).ToList();
        var parts = participacoes.Where(p => ligas.ContainsKey(p.LigaId)).DistinctBy(p => (p.TimeId, p.PartidaId)).ToList();

        var gols = evs.Where(e => e.Tipo == TipoEvento.Gol && e.JogadorId == jogadorId).ToList();
        var assistencias = evs.Where(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == jogadorId).ToList();
        var amarelos = evs.Where(e => e.Tipo == TipoEvento.CartaoAmarelo && e.JogadorId == jogadorId).ToList();
        var vermelhos = evs.Where(e => e.Tipo == TipoEvento.CartaoVermelho && e.JogadorId == jogadorId).ToList();

        // Toda (competição, time) em que o jogador jogou ou deixou marca.
        var chaves = parts.Select(p => (p.LigaId, p.TimeId))
            .Concat(gols.Concat(assistencias).Concat(amarelos).Concat(vermelhos).Select(e => (e.LigaId, e.TimeId)))
            .Distinct();

        var competicoes = chaves
            .Select(k =>
            {
                var liga = ligas[k.LigaId];
                var jogou = parts.Where(p => p.LigaId == k.LigaId && p.TimeId == k.TimeId).ToList();
                bool Da(CarreiraEventoInput e) => e.LigaId == k.LigaId && e.TimeId == k.TimeId;

                return new JogadorCarreiraCompeticaoDto(
                    liga.LigaId,
                    liga.Nome,
                    liga.Tipo,
                    liga.Temporada,
                    k.TimeId,
                    nomesTimes.TryGetValue(k.TimeId, out var nome) ? nome : "?",
                    liga.TemContagem ? jogou.Count : null,
                    gols.Count(Da),
                    assistencias.Count(Da),
                    amarelos.Count(Da),
                    vermelhos.Count(Da),
                    defensor && liga.TemContagem ? jogou.Count(p => p.SemSofrerGol) : null,
                    liga.CampeaoTimeId == k.TimeId);
            })
            .OrderByDescending(c => ligas[c.LigaId].CriadaEm)
            .ThenBy(c => c.TimeNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var titulos = competicoes
            .Where(c => c.Campeao)
            .Select(c => new JogadorTituloDto(c.LigaId, c.Competicao, c.Tipo, c.Temporada, c.TimeNome))
            .ToList();

        var golsPorJogo = gols.GroupBy(g => g.PartidaId).Select(g => g.Count()).ToList();

        return new JogadorCarreiraEstatisticasDto(
            competicoes.Sum(c => c.Jogos ?? 0),
            competicoes.Any(c => c.Jogos is null),
            gols.Count,
            assistencias.Count,
            amarelos.Count,
            vermelhos.Count,
            defensor ? competicoes.Sum(c => c.CleanSheets ?? 0) : null,
            golsPorJogo.Count == 0 ? 0 : golsPorJogo.Max(),
            golsPorJogo.Count(n => n >= 3),
            competicoes,
            titulos);
    }
}
