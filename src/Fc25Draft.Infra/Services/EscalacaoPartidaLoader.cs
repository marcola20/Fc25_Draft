using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Carrega as escalações (titulares e banco) dos times de uma partida.</summary>
internal static class EscalacaoPartidaLoader
{
    internal sealed record Linha(Guid TimeId, int JogadorId, string JogadorNome, short PositionId, bool Titular, int Ordem);

    /// <summary>
    /// Retrato gravado no encerramento; se ainda não existe e a partida não foi
    /// encerrada, a escalação ativa atual de cada time. Partida antiga encerrada
    /// sem retrato devolve vazio.
    /// </summary>
    public static async Task<List<Linha>> CarregarAsync(DraftDbContext db, LigaPartida partida, CancellationToken ct)
    {
        var retrato = await db.LigaEscalacoes
            .AsNoTracking()
            .Where(x => x.PartidaId == partida.PartidaId)
            .Select(x => new Linha(x.TimeId, x.JogadorId, x.Jogador.Name, x.Jogador.PositionId, x.Titular, x.Ordem))
            .ToListAsync(ct);

        if (retrato.Count > 0 || partida.Status == PartidaStatus.Encerrada)
            return retrato;

        return await EscalacaoAtivaAsync(db, new[] { partida.TimeCasaId, partida.TimeForaId }, ct);
    }

    public static async Task<List<Linha>> EscalacaoAtivaAsync(DraftDbContext db, Guid[] timeIds, CancellationToken ct)
    {
        var linhas = await db.TeamLineupSlots
            .AsNoTracking()
            .Where(s => s.Lineup.IsActive && timeIds.Contains(s.Lineup.TeamId) && s.PlayerId != null
                        // Ignora jogador que ficou na escalação mas já não é do elenco.
                        && db.TeamRosters.Any(r => r.TeamId == s.Lineup.TeamId && r.PlayerId == s.PlayerId))
            .Select(s => new Linha(s.Lineup.TeamId, s.PlayerId!.Value, s.Player!.Name, s.Player.PositionId, !s.IsBench, s.Order))
            .ToListAsync(ct);

        // Um jogador em dois slots vale uma vez só (titular tem prioridade).
        return linhas
            .GroupBy(l => (l.TimeId, l.JogadorId))
            .Select(g => g.OrderByDescending(l => l.Titular).ThenBy(l => l.Ordem).First())
            .ToList();
    }
}
