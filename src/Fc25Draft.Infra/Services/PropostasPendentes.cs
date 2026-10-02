using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Propostas pendentes que perderam o sentido porque um jogador delas mudou de time (proposta aceita,
/// compra pela lista, venda rápida, ação do admin): são canceladas e quem mandou é avisado. Não salva.
/// </summary>
internal static class PropostasPendentes
{
    public static async Task CancelarComJogadoresAsync(
        DraftDbContext db, IReadOnlyCollection<int> playerIds, Guid? excetoOfferId, string motivo, DateTime agora, CancellationToken ct)
    {
        if (playerIds.Count == 0) return;

        var pendentes = await db.TransferOffers
            .Include(o => o.ToTeam)
            .Where(o => o.Status == OfferStatus.Pending
                        && o.OfferId != excetoOfferId
                        && o.Players.Any(p => playerIds.Contains(p.PlayerId)))
            .ToListAsync(ct);

        foreach (var offer in pendentes)
        {
            offer.Status = OfferStatus.Cancelled;
            offer.UpdatedAtUtc = agora;
            AvisosDoTime.Criar(db, offer.FromTeamId, AvisosDoTime.PropostaCancelada,
                $"Sua proposta ao {offer.ToTeam.TeamName} foi cancelada: {motivo}", "/minha-area", agora);
        }
    }
}
