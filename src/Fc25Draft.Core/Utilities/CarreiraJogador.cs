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
    bool TemContagem,
    // Período em que a competição foi jogada (primeiro e último jogo encerrado) e os times que a disputaram.
    DateTime? Inicio = null,
    DateTime? Fim = null,
    IReadOnlyCollection<Guid>? Participantes = null);

/// <summary>Evento em que o jogador aparece: como autor (gol, cartão) ou como assistente.</summary>
public record CarreiraEventoInput(TipoEvento Tipo, int JogadorId, int? AssistenteId, Guid TimeId, Guid PartidaId, Guid LigaId);

/// <summary>Partida em que o jogador entrou em campo (titular ou substituto).</summary>
public record CarreiraParticipacaoInput(Guid TimeId, Guid PartidaId, Guid LigaId, bool SemSofrerGol);

/// <summary>Período em que o jogador esteve no elenco de um time.</summary>
public record CarreiraVinculoInput(Guid TimeId, DateTime De, DateTime Ate);

/// <summary>Chegada ou saída do jogador (escolha de draft ou transferência), para reconstruir os vínculos.</summary>
public record CarreiraMovimentoInput(DateTime Data, Guid? DeTimeId, Guid? ParaTimeId);

/// <summary>Soma a carreira do jogador por competição e por time, com os títulos que ele ajudou a ganhar.</summary>
public static class CarreiraJogador
{
    public static JogadorCarreiraEstatisticasDto Calcular(
        int jogadorId,
        bool defensor,
        IReadOnlyDictionary<Guid, CarreiraLigaInput> ligas,
        IEnumerable<CarreiraEventoInput> eventos,
        IEnumerable<CarreiraParticipacaoInput> participacoes,
        IReadOnlyDictionary<Guid, string> nomesTimes,
        IEnumerable<CarreiraVinculoInput>? vinculos = null)
    {
        var periodos = vinculos?.ToList() ?? new List<CarreiraVinculoInput>();
        var evs = eventos.Where(e => ligas.ContainsKey(e.LigaId)).ToList();
        var parts = participacoes.Where(p => ligas.ContainsKey(p.LigaId)).DistinctBy(p => (p.TimeId, p.PartidaId)).ToList();

        var gols = evs.Where(e => e.Tipo == TipoEvento.Gol && e.JogadorId == jogadorId).ToList();
        var assistencias = evs.Where(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == jogadorId).ToList();
        var amarelos = evs.Where(e => e.Tipo == TipoEvento.CartaoAmarelo && e.JogadorId == jogadorId).ToList();
        var vermelhos = evs.Where(e => e.Tipo == TipoEvento.CartaoVermelho && e.JogadorId == jogadorId).ToList();

        // Toda (competição, time) em que o jogador jogou ou deixou marca.
        var emCampo = parts.Select(p => (p.LigaId, p.TimeId))
            .Concat(gols.Concat(assistencias).Concat(amarelos).Concat(vermelhos).Select(e => (e.LigaId, e.TimeId)))
            .ToHashSet();

        // Competições antigas não têm escalação: quem não marcou nem levou cartão sumia (goleiro, reserva).
        // Vale também estar no elenco de um time que disputou a competição enquanto ela era jogada.
        var noElenco = ligas.Values
            .Where(l => l.Inicio is not null && l.Fim is not null && l.Participantes is not null)
            .SelectMany(l => periodos
                .Where(v => l.Participantes!.Contains(v.TimeId) && v.De <= l.Fim && v.Ate >= l.Inicio)
                .Select(v => (l.LigaId, v.TimeId)))
            .ToHashSet();

        // Título "no elenco" só se ainda estava no time quando a competição terminou.
        bool NoElencoNoFim(CarreiraLigaInput l, Guid timeId) =>
            l.Fim is DateTime fim && periodos.Any(v => v.TimeId == timeId && v.De <= fim && v.Ate >= fim);

        var chaves = emCampo.Union(noElenco);

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
                    liga.CampeaoTimeId == k.TimeId && (emCampo.Contains(k) || NoElencoNoFim(liga, k.TimeId)),
                    !emCampo.Contains(k));
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
            titulos,
            periodos.Select(v => v.TimeId).Concat(competicoes.Select(c => c.TimeId)).Distinct().Count());
    }

    /// <summary>
    /// Reconstrói em que time o jogador esteve em cada período, a partir das chegadas e saídas.
    /// Antes da primeira saída conhecida ele estava no time de onde saiu; sem movimento nenhum, no time atual.
    /// </summary>
    public static IReadOnlyList<CarreiraVinculoInput> Vinculos(IEnumerable<CarreiraMovimentoInput> movimentos, Guid? timeAtualId)
    {
        var ordem = movimentos.OrderBy(m => m.Data).ToList();
        var vinculos = new List<CarreiraVinculoInput>();

        if (ordem.Count == 0)
        {
            if (timeAtualId is Guid atual)
                vinculos.Add(new CarreiraVinculoInput(atual, DateTime.MinValue, DateTime.MaxValue));
            return vinculos;
        }

        Guid? time = ordem[0].DeTimeId;
        var desde = DateTime.MinValue;
        foreach (var m in ordem)
        {
            if (time is Guid t)
                vinculos.Add(new CarreiraVinculoInput(t, desde, m.Data));

            time = m.ParaTimeId;
            desde = m.Data;
        }

        if (time is Guid ultimo)
            vinculos.Add(new CarreiraVinculoInput(ultimo, desde, DateTime.MaxValue));

        return vinculos;
    }
}
