using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Suspensões e pendurados de uma Liga ou Copa, calculados dos cartões registrados
/// (<see cref="Suspensoes"/>). Só Liga e Copa têm suspensão automática.
/// </summary>
internal static class DisciplinaDaCompeticao
{
    public static async Task<DisciplinaDto?> CalcularAsync(DraftDbContext db, Guid ligaId, CancellationToken ct)
    {
        var tipo = await db.Ligas.AsNoTracking().Where(l => l.LigaId == ligaId).Select(l => (TipoCompetition?)l.Tipo).FirstOrDefaultAsync(ct);
        if (tipo is not (TipoCompetition.Liga or TipoCompetition.Copa)) return null;

        var partidas = await db.LigaPartidas.AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId)
            .Select(p => new
            {
                p.PartidaId, p.RodadaId, p.Rodada.Numero, p.Rodada.Desempate, p.Rodada.DataHora, p.IniciadaEm, p.Status,
                p.TimeCasaId, CasaNome = p.TimeCasa.TeamName, p.TimeForaId, ForaNome = p.TimeFora.TeamName
            })
            .ToListAsync(ct);
        if (partidas.Count == 0) return new DisciplinaDto([], []);

        var ids = partidas.Select(p => p.PartidaId).ToList();
        var fases = await db.LigaKnockoutJogos.AsNoTracking()
            .Where(k => k.PartidaId != null && ids.Contains(k.PartidaId.Value))
            .ToDictionaryAsync(k => k.PartidaId!.Value, k => k.Fase, ct);

        // Ordem dos jogos: rodadas, jogos decisivos dos grupos, mata-mata pela fase e, na Liga, mini liga,
        // decisão do título e playoff de acesso.
        int Etapa(Guid partidaId, int numero, bool desempate) =>
            fases.TryGetValue(partidaId, out var fase) ? 2000 + (int)fase
            : numero > 0 ? (desempate ? 1000 + numero : numero)
            : 3000 - numero * 100;

        string Rotulo(Guid partidaId, int numero, bool desempate) =>
            fases.TryGetValue(partidaId, out var fase)
                ? fase switch
                {
                    FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => "Quartas",
                    FaseKnockout.Semi1 or FaseKnockout.Semi2 => "Semifinal",
                    FaseKnockout.Final => "Final",
                    _ => "Play-in"
                }
            : numero switch
            {
                0 => "Mini liga",
                -1 => "Jogo decisivo",
                -2 => "Playoff de acesso",
                _ => desempate ? "Jogo decisivo" : $"Rodada {numero}"
            };

        var ordenadas = partidas
            .OrderBy(p => Etapa(p.PartidaId, p.Numero, p.Desempate))
            .ThenBy(p => p.DataHora ?? p.IniciadaEm ?? DateTime.MaxValue)
            .ThenBy(p => p.PartidaId)
            .ToList();
        var porId = ordenadas.ToDictionary(p => p.PartidaId);

        var jogos = ordenadas.SelectMany((p, i) => new[]
        {
            new JogoDoTime(p.PartidaId, p.TimeCasaId, i, fases.ContainsKey(p.PartidaId), p.Status == PartidaStatus.Encerrada),
            new JogoDoTime(p.PartidaId, p.TimeForaId, i, fases.ContainsKey(p.PartidaId), p.Status == PartidaStatus.Encerrada),
        });

        var cartoes = await db.LigaEventos.AsNoTracking()
            .Where(e => ids.Contains(e.PartidaId) && (e.Tipo == TipoEvento.CartaoAmarelo || e.Tipo == TipoEvento.CartaoVermelho))
            .Select(e => new CartaoNoJogo(e.PartidaId, e.TimeId, e.JogadorId, e.Tipo == TipoEvento.CartaoVermelho))
            .ToListAsync(ct);

        var resultado = Suspensoes.Calcular(jogos, cartoes, zerarAmarelosNoMataMata: tipo == TipoCompetition.Copa);

        var jogadorIds = resultado.Suspensoes.Select(s => s.JogadorId).Concat(resultado.Pendurados.Select(p => p.JogadorId)).Distinct().ToList();
        var nomes = await db.Players.AsNoTracking().Where(p => jogadorIds.Contains(p.PlayerId)).ToDictionaryAsync(p => p.PlayerId, p => p.Name, ct);
        var times = partidas.SelectMany(p => new[] { (p.TimeCasaId, p.CasaNome), (p.TimeForaId, p.ForaNome) })
            .DistinctBy(t => t.Item1).ToDictionary(t => t.Item1, t => t.Item2);

        // "Rodada 3 x Santos", do ponto de vista do time.
        string Jogo(Guid partidaId, Guid timeId)
        {
            var p = porId[partidaId];
            var adversario = p.TimeCasaId == timeId ? p.ForaNome : p.CasaNome;
            return $"{Rotulo(p.PartidaId, p.Numero, p.Desempate)} x {adversario}";
        }

        return new DisciplinaDto(
            resultado.Suspensoes
                .Select(s => new SuspensaoDto(
                    s.JogadorId, nomes.GetValueOrDefault(s.JogadorId, "?"), s.TimeId, times.GetValueOrDefault(s.TimeId, "?"),
                    s.Motivo, Jogo(s.PartidaDoCartao, s.TimeId),
                    s.PartidaCumprida,
                    s.PartidaCumprida is Guid c ? porId[c].RodadaId : null,
                    s.PartidaCumprida is Guid j ? Jogo(j, s.TimeId) : null,
                    s.Cumprida))
                .OrderBy(s => s.Cumprida)
                .ThenBy(s => s.TimeNome)
                .ThenBy(s => s.JogadorNome)
                .ToList(),
            resultado.Pendurados
                .Select(p => new PenduradoDto(p.JogadorId, nomes.GetValueOrDefault(p.JogadorId, "?"), p.TimeId, times.GetValueOrDefault(p.TimeId, "?"), p.Amarelos))
                .OrderBy(p => p.TimeNome)
                .ThenBy(p => p.JogadorNome)
                .ToList());
    }
}
