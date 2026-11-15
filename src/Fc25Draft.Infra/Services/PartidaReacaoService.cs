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

    public async Task<IReadOnlyList<PartidaReacaoDto>> ListarAsync(Guid salaId, Guid? treinadorId, CancellationToken ct)
    {
        // A sala é um jogo ou um mosaico: um dos dois lados volta vazio.
        var doJogo = await _db.PartidaReacoes.AsNoTracking()
            .Where(r => r.PartidaId == salaId)
            .Select(r => new { r.Emoji, r.TreinadorId, r.Treinador.Nome, r.CriadaEm })
            .ToListAsync(ct);
        var doMosaico = await _db.MosaicoReacoes.AsNoTracking()
            .Where(r => r.MosaicoId == salaId)
            .Select(r => new { r.Emoji, r.TreinadorId, r.Treinador.Nome, r.CriadaEm })
            .ToListAsync(ct);
        var reacoes = doJogo.Concat(doMosaico).OrderBy(r => r.CriadaEm).ToList();

        return reacoes
            .GroupBy(r => r.Emoji)
            .OrderBy(g => OrdemDoBotao(g.Key))
            .Select(g => new PartidaReacaoDto(
                g.Key, g.Count(), g.Select(r => r.Nome).ToArray(),
                treinadorId is Guid eu && g.Any(r => r.TreinadorId == eu)))
            .ToArray();
    }

    public async Task AlternarAsync(Guid salaId, Guid treinadorId, string emoji, CancellationToken ct)
    {
        if (!ReacoesPartida.Permitido(emoji))
            throw new InvalidOperationException("Emoji não disponível.");

        if (await _db.Mosaicos.AnyAsync(m => m.MosaicoId == salaId, ct))
        {
            await AlternarNoMosaicoAsync(salaId, treinadorId, emoji, ct);
            return;
        }

        var apagadas = await _db.PartidaReacoes
            .Where(r => r.PartidaId == salaId && r.TreinadorId == treinadorId && r.Emoji == emoji)
            .ExecuteDeleteAsync(ct);
        if (apagadas > 0) return;

        if (!await _db.LigaPartidas.AnyAsync(p => p.PartidaId == salaId, ct))
            throw new InvalidOperationException("Jogo não encontrado.");

        _db.PartidaReacoes.Add(new PartidaReacao
        {
            PartidaId = salaId,
            TreinadorId = treinadorId,
            Emoji = emoji,
            CriadaEm = DateTime.UtcNow
        });

        try { await _db.SaveChangesAsync(ct); }
        // Clique duplo: a mesma reação chegou duas vezes, a primeira já gravou.
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); }
    }

    private async Task AlternarNoMosaicoAsync(Guid mosaicoId, Guid treinadorId, string emoji, CancellationToken ct)
    {
        var apagadas = await _db.MosaicoReacoes
            .Where(r => r.MosaicoId == mosaicoId && r.TreinadorId == treinadorId && r.Emoji == emoji)
            .ExecuteDeleteAsync(ct);
        if (apagadas > 0) return;

        _db.MosaicoReacoes.Add(new MosaicoReacao
        {
            MosaicoId = mosaicoId,
            TreinadorId = treinadorId,
            Emoji = emoji,
            CriadaEm = DateTime.UtcNow
        });

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); }
    }

    private static int OrdemDoBotao(string emoji)
    {
        for (var i = 0; i < ReacoesPartida.Emojis.Count; i++)
            if (ReacoesPartida.Emojis[i] == emoji) return i;
        return int.MaxValue;
    }
}
