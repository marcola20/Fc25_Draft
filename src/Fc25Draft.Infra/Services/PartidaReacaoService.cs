using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class PartidaReacaoService : IPartidaReacaoService
{
    private readonly DraftDbContext _db;

    public PartidaReacaoService(DraftDbContext db) => _db = db;

    public async Task<IReadOnlyList<PartidaReacaoDto>> ListarAsync(Guid partidaId, Guid? treinadorId, CancellationToken ct)
    {
        var reacoes = await _db.PartidaReacoes.AsNoTracking()
            .Where(r => r.PartidaId == partidaId)
            .OrderBy(r => r.CriadaEm)
            .Select(r => new { r.Emoji, r.TreinadorId, r.Treinador.Nome })
            .ToListAsync(ct);

        return reacoes
            .GroupBy(r => r.Emoji)
            .OrderBy(g => OrdemDoBotao(g.Key))
            .Select(g => new PartidaReacaoDto(
                g.Key, g.Count(), g.Select(r => r.Nome).ToArray(),
                treinadorId is Guid eu && g.Any(r => r.TreinadorId == eu)))
            .ToArray();
    }

    public async Task AlternarAsync(Guid partidaId, Guid treinadorId, string emoji, CancellationToken ct)
    {
        if (!ReacoesPartida.Permitido(emoji))
            throw new InvalidOperationException("Emoji não disponível.");

        var apagadas = await _db.PartidaReacoes
            .Where(r => r.PartidaId == partidaId && r.TreinadorId == treinadorId && r.Emoji == emoji)
            .ExecuteDeleteAsync(ct);
        if (apagadas > 0) return;

        if (!await _db.LigaPartidas.AnyAsync(p => p.PartidaId == partidaId, ct))
            throw new InvalidOperationException("Jogo não encontrado.");

        _db.PartidaReacoes.Add(new PartidaReacao
        {
            PartidaId = partidaId,
            TreinadorId = treinadorId,
            Emoji = emoji,
            CriadaEm = DateTime.UtcNow
        });

        try { await _db.SaveChangesAsync(ct); }
        // Clique duplo: a mesma reação chegou duas vezes, a primeira já gravou.
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); }
    }

    private static int OrdemDoBotao(string emoji)
    {
        for (var i = 0; i < ReacoesPartida.Emojis.Count; i++)
            if (ReacoesPartida.Emojis[i] == emoji) return i;
        return int.MaxValue;
    }
}
