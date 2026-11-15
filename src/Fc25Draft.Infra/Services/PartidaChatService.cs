using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Configurations;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class PartidaChatService : IPartidaChatService
{
    private readonly DraftDbContext _db;

    public PartidaChatService(DraftDbContext db) => _db = db;

    public async Task<IReadOnlyList<PartidaChatMensagemDto>> ListarAsync(Guid salaId, int limite, CancellationToken ct)
    {
        var mensagens = await _db.PartidaChatMensagens.AsNoTracking()
            .Where(m => m.PartidaId == salaId || m.MosaicoId == salaId)
            .OrderByDescending(m => m.EnviadaEm)
            .Take(limite)
            .Select(m => new { m.MensagemId, m.TreinadorId, m.Treinador.Nome, m.Texto, m.EnviadaEm })
            .ToListAsync(ct);

        var clubes = await ClubesAtuaisAsync(mensagens.Select(m => m.TreinadorId).Distinct().ToList(), ct);

        return mensagens
            .OrderBy(m => m.EnviadaEm)
            .Select(m => new PartidaChatMensagemDto(
                m.MensagemId, salaId, m.TreinadorId, m.Nome,
                clubes.GetValueOrDefault(m.TreinadorId), m.Texto, m.EnviadaEm))
            .ToArray();
    }

    public async Task<PartidaChatMensagemDto> EnviarAsync(Guid salaId, Guid treinadorId, string texto, CancellationToken ct)
    {
        var limpo = (texto ?? "").Trim();
        if (limpo.Length == 0)
            throw new InvalidOperationException("Escreva alguma coisa.");
        if (limpo.Length > PartidaChatMensagemConfiguration.TamanhoMaximo)
            limpo = limpo[..PartidaChatMensagemConfiguration.TamanhoMaximo];

        var treinador = await _db.Treinadores.AsNoTracking()
            .Where(t => t.TreinadorId == treinadorId)
            .Select(t => new { t.Nome })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Treinador não encontrado.");

        var ehJogo = await _db.LigaPartidas.AnyAsync(p => p.PartidaId == salaId, ct);
        if (!ehJogo && !await _db.Mosaicos.AnyAsync(m => m.MosaicoId == salaId, ct))
            throw new InvalidOperationException("Jogo não encontrado.");

        var mensagem = new PartidaChatMensagem
        {
            MensagemId = Guid.NewGuid(),
            PartidaId = ehJogo ? salaId : null,
            MosaicoId = ehJogo ? null : salaId,
            TreinadorId = treinadorId,
            Texto = limpo,
            EnviadaEm = DateTime.UtcNow
        };

        _db.PartidaChatMensagens.Add(mensagem);
        await _db.SaveChangesAsync(ct);

        var clubes = await ClubesAtuaisAsync(new List<Guid> { treinadorId }, ct);
        return new PartidaChatMensagemDto(
            mensagem.MensagemId, salaId, treinadorId, treinador.Nome,
            clubes.GetValueOrDefault(treinadorId), mensagem.Texto, mensagem.EnviadaEm);
    }

    public async Task ApagarAsync(Guid mensagemId, CancellationToken ct)
    {
        await _db.PartidaChatMensagens.Where(m => m.MensagemId == mensagemId).ExecuteDeleteAsync(ct);
    }

    /// <summary>O clube em que cada treinador está hoje (passagem sem data de saída).</summary>
    private async Task<Dictionary<Guid, string>> ClubesAtuaisAsync(List<Guid> treinadores, CancellationToken ct)
    {
        var passagens = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => treinadores.Contains(p.TreinadorId) && p.Ate == null)
            .Select(p => new { p.TreinadorId, p.Time.TeamName })
            .ToListAsync(ct);

        return passagens
            .GroupBy(p => p.TreinadorId)
            .ToDictionary(g => g.Key, g => g.First().TeamName);
    }
}
