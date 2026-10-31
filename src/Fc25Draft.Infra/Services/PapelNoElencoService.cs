using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Papel no elenco: a conversa que rebaixa o papel de um jogador e a virada que refaz o de todos pelo que jogaram.
/// </summary>
public class PapelNoElencoService : IPapelNoElencoService
{
    private readonly DraftDbContext _db;
    private readonly IParticipacaoService _participacao;
    private readonly TimeProvider _time;

    public PapelNoElencoService(DraftDbContext db, IParticipacaoService participacao, TimeProvider? time = null)
    {
        _db = db;
        _participacao = participacao;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PapelNoElenco> ConversarAsync(Guid teamId, Guid playerGuid, CancellationToken ct)
    {
        if (await _db.TransferConfigs.AsNoTracking().Select(c => c.PapelNoElencoDesde).FirstOrDefaultAsync(ct) is null)
            throw new InvalidOperationException("O papel no elenco ainda não está valendo: começa na virada da temporada.");

        var vinculo = await _db.TeamRosters.Include(r => r.Player).Include(r => r.Team)
            .FirstOrDefaultAsync(r => r.TeamId == teamId && r.Player.PlayerGuid == playerGuid, ct)
            ?? throw new KeyNotFoundException("Jogador não encontrado no seu elenco.");

        var nome = vinculo.Player.Name;
        if (vinculo.SaidaPedidaEm is not null)
            throw new InvalidOperationException($"{nome} já pediu para sair: agora só a promessa de colocar para jogar.");
        if (vinculo.Team.ConversaEm is not null)
            throw new InvalidOperationException("Você já usou a conversa desta temporada.");

        var atual = (await _participacao.GetAsync(vinculo.PlayerId, ct))?.Papel?.Papel
                    ?? throw new InvalidOperationException($"{nome} ainda não tem papel no elenco.");
        var novo = PapelDoJogador.UmDegrauAbaixo(atual)
                   ?? throw new InvalidOperationException(
                       $"{nome} já é {PapelDoJogador.Nome(atual).ToLowerInvariant()}: não tem como descer mais.");

        var agora = _time.GetUtcNow().UtcDateTime;
        vinculo.Papel = novo;
        vinculo.ConversaEm = agora;
        vinculo.Team.ConversaEm = agora;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException($"{nome} acabou de ser negociado com outro time.");
        }
        ParticipacaoService.Esquecer();
        return novo;
    }

    public async Task<int> RefazerNaViradaAsync(int temporadaQueAcabou, CancellationToken ct)
    {
        var config = await _db.TransferConfigs.FirstOrDefaultAsync(ct);
        if (config is null) return 0;

        if (config.PapelNoElencoDesde is null)
        {
            config.PapelNoElencoDesde = _time.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(ct);
        }

        var papeis = await _participacao.PapeisNaViradaAsync(temporadaQueAcabou, ct);
        var vinculos = await _db.TeamRosters.ToListAsync(ct);
        foreach (var v in vinculos)
        {
            if (papeis.TryGetValue(v.PlayerId, out var papel)) v.Papel = papel;
            v.ConversaEm = null;
        }
        await _db.SaveChangesAsync(ct);

        await _db.Teams.Where(t => t.ConversaEm != null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConversaEm, (DateTime?)null), ct);
        ParticipacaoService.Esquecer();
        return papeis.Count;
    }
}
