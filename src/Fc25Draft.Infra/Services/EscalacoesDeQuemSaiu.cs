using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Quem sai do elenco sai também das escalações do time: vaga de titular ou reserva, capitão e cobranças.
/// Roda antes de cada SaveChanges, então vale para qualquer saída (venda, troca, empréstimo, fim de
/// empréstimo, venda rápida, leilão, organização, draft de expansão) sem cada serviço precisar lembrar.
/// </summary>
internal static class EscalacoesDeQuemSaiu
{
    private static readonly (Func<TeamLineup, int?> Ler, Action<TeamLineup> Limpar)[] Funcoes =
    [
        (l => l.CaptainPlayerId, l => { l.CaptainPlayerId = null; l.CaptainPlayer = null; }),
        (l => l.ShortFreeKick1PlayerId, l => { l.ShortFreeKick1PlayerId = null; l.ShortFreeKick1Player = null; }),
        (l => l.ShortFreeKick2PlayerId, l => { l.ShortFreeKick2PlayerId = null; l.ShortFreeKick2Player = null; }),
        (l => l.LongFreeKickPlayerId, l => { l.LongFreeKickPlayerId = null; l.LongFreeKickPlayer = null; }),
        (l => l.PenaltiesPlayerId, l => { l.PenaltiesPlayerId = null; l.PenaltiesPlayer = null; }),
        (l => l.CornerLeftPlayerId, l => { l.CornerLeftPlayerId = null; l.CornerLeftPlayer = null; }),
        (l => l.CornerRightPlayerId, l => { l.CornerRightPlayerId = null; l.CornerRightPlayer = null; }),
        (l => l.AttackingPlayer1Id, l => { l.AttackingPlayer1Id = null; l.AttackingPlayer1 = null; }),
        (l => l.AttackingPlayer2Id, l => { l.AttackingPlayer2Id = null; l.AttackingPlayer2 = null; }),
        (l => l.AttackingPlayer3Id, l => { l.AttackingPlayer3Id = null; l.AttackingPlayer3 = null; }),
    ];

    public static async Task TirarAsync(DraftDbContext db, CancellationToken ct)
    {
        var vinculos = db.ChangeTracker.Entries<TeamRoster>().ToList();
        if (vinculos.Count == 0) return;

        // Saiu = vínculo apagado que não voltou no mesmo save (tirar e recolocar no mesmo time não conta).
        var ficaram = vinculos.Where(e => e.State != EntityState.Deleted)
            .Select(e => (e.Entity.TeamId, e.Entity.PlayerId)).ToHashSet();
        var sairam = vinculos.Where(e => e.State == EntityState.Deleted)
            .Select(e => (e.Entity.TeamId, e.Entity.PlayerId))
            .Where(v => !ficaram.Contains(v))
            .ToHashSet();
        if (sairam.Count == 0) return;

        var times = sairam.Select(v => v.TeamId).Distinct().ToList();
        var escalacoes = await db.TeamLineups
            .Include(l => l.Slots)
            .Where(l => times.Contains(l.TeamId))
            .ToListAsync(ct);
        if (escalacoes.Count == 0) return;

        var jogadores = sairam.Select(v => v.PlayerId).Distinct().ToList();
        var nomes = await db.Players.AsNoTracking()
            .Where(p => jogadores.Contains(p.PlayerId))
            .ToDictionaryAsync(p => p.PlayerId, p => p.Name, ct);
        var agora = DateTime.UtcNow;

        foreach (var escalacao in escalacoes)
        {
            bool Saiu(int? id) => id is int pid && sairam.Contains((escalacao.TeamId, pid));

            var titularesTirados = new List<string>();
            foreach (var vaga in escalacao.Slots.Where(s => Saiu(s.PlayerId)))
            {
                if (!vaga.IsBench) titularesTirados.Add(nomes.GetValueOrDefault(vaga.PlayerId!.Value, "Um jogador"));
                vaga.PlayerId = null;
                vaga.Player = null;
            }
            foreach (var (ler, limpar) in Funcoes)
                if (Saiu(ler(escalacao))) limpar(escalacao);

            // Só a escalação ativa avisa: é ela que vai a campo, e vaga de titular vazia precisa de alguém.
            if (escalacao.IsActive && titularesTirados.Count > 0)
            {
                var quem = string.Join(", ", titularesTirados);
                var verbo = titularesTirados.Count == 1 ? "saiu do elenco e foi tirado" : "saíram do elenco e foram tirados";
                AvisosDoTime.Criar(db, escalacao.TeamId, AvisosDoTime.Escalacao,
                    $"{quem} {verbo} da escalação \"{escalacao.Name}\". Escolha quem entra no lugar.",
                    $"/teams/{escalacao.TeamId}/lineups", agora);
            }
        }
    }
}
