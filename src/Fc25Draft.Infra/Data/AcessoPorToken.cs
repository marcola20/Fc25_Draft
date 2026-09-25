using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

/// <summary>Quem está com o token: a pessoa e o clube onde ela está agora.</summary>
public record AcessoDoToken(Guid TreinadorId, string Nome, PapelTreinador Papel, Guid TimeId, string TimeNome);

/// <summary>
/// O token é da pessoa, não do time. Quem manda no clube é a passagem em aberto:
/// quem saiu não abre mais nada, e quem trocou de time leva o acesso junto.
/// </summary>
public static class AcessoPorToken
{
    public static async Task<AcessoDoToken?> AcessoPorTokenAsync(this DraftDbContext db, string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var limpo = token.Trim().ToUpperInvariant();

        return await db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null && p.Treinador.Ativo && p.Treinador.Token.ToUpper() == limpo)
            .Select(p => new AcessoDoToken(p.TreinadorId, p.Treinador.Nome, p.Papel, p.TimeId, p.Time.TeamName))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>O clube que o token comanda hoje, ou nulo para quem está sem time.</summary>
    public static async Task<Guid?> TimeIdPorTokenAsync(this DraftDbContext db, string? token, CancellationToken ct = default)
        => (await db.AcessoPorTokenAsync(token, ct))?.TimeId;

    /// <summary>Se o token dá acesso a este clube (treinador ou auxiliar, tanto faz).</summary>
    public static async Task<bool> TokenComandaAsync(this DraftDbContext db, string? token, Guid timeId, CancellationToken ct = default)
        => await db.TimeIdPorTokenAsync(token, ct) == timeId;
}
