using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Valor de mercado jogo a jogo no clube atual: a participação depois de cada jogo
/// (<see cref="IParticipacaoService.GetHistoricoAsync"/>) sobre o preço base de hoje.
/// </summary>
public class ValorDoJogadorService : IValorDoJogadorService
{
    private readonly DraftDbContext _db;
    private readonly IParticipacaoService _participacao;
    private readonly IPricingService _precos;

    public ValorDoJogadorService(DraftDbContext db, IParticipacaoService participacao, IPricingService precos)
    {
        _db = db;
        _participacao = participacao;
        _precos = precos;
    }

    public async Task<ValorDoJogadorDto?> HistoricoAsync(int playerId, CancellationToken ct)
    {
        var historico = await _participacao.GetHistoricoAsync(playerId, ct);
        if (historico.Count == 0) return null;

        var timeId = historico[0].Depois.TimeId;
        var jogador = await _db.Players.AsNoTracking()
            .Where(p => p.PlayerId == playerId && p.Age != null && p.TeamRosters.Any(r => r.TeamId == timeId))
            .Select(p => new { p.PositionId, p.Overall, Idade = p.Age!.Value })
            .FirstOrDefaultAsync(ct);
        if (jogador is null) return null;

        var partidaIds = historico.Select(h => h.Jogo.PartidaId).ToList();
        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => partidaIds.Contains(p.PartidaId))
            .Select(p => new
            {
                p.PartidaId, p.TimeCasaId, p.GolsCasa, p.GolsFora,
                Casa = p.TimeCasa.TeamName, Fora = p.TimeFora.TeamName
            })
            .ToDictionaryAsync(p => p.PartidaId, ct);
        var timeNome = await _db.Teams.AsNoTracking().Where(t => t.TeamId == timeId).Select(t => t.TeamName).FirstAsync(ct);

        var preco = _precos.Calculate(MarketWeightResolver.GetByPositionId(jogador.PositionId), jogador.Overall, jogador.Idade);
        var pontos = new List<ValorDoJogadorPontoDto>(historico.Count);
        var insatisfeitoAntes = false;
        foreach (var (jogo, depois) in historico)
        {
            var p = partidas[jogo.PartidaId];
            var emCasa = p.TimeCasaId == timeId;
            var humor = depois.Insatisfeito == insatisfeitoAntes ? null : depois.Insatisfeito ? "😤" : "🤝";
            insatisfeitoAntes = depois.Insatisfeito;

            pontos.Add(new ValorDoJogadorPontoDto(
                jogo.PartidaId, jogo.Data,
                emCasa ? p.Fora : p.Casa,
                emCasa ? p.GolsCasa : p.GolsFora,
                emCasa ? p.GolsFora : p.GolsCasa,
                jogo.Titular ? ParticipouComo.Titular : jogo.EntrouDoBanco ? ParticipouComo.EntrouDoBanco : ParticipouComo.NaoJogou,
                _precos.ComVariacao(preco, depois.Variacao).BasePrice,
                depois.Variacao,
                depois.Percentual,
                depois.Insatisfeito,
                humor,
                depois.EmCarencia));
        }

        return new ValorDoJogadorDto(timeNome, preco.BasePrice, pontos, await ChegadaAsync(playerId, timeId, ct));
    }

    /// <summary>
    /// A chegada mais recente ao clube: transferência ou escolha no draft (na expansão as duas saem juntas e a escolha
    /// diz a rodada). A volta de empréstimo não conta: ele já era do clube antes.
    /// </summary>
    private async Task<ValorDoJogadorChegadaDto?> ChegadaAsync(int playerId, Guid timeId, CancellationToken ct)
    {
        var transferencia = await _db.TransferHistories.AsNoTracking()
            .Where(t => t.PlayerId == playerId && t.ToTeamId == timeId && t.Type != TransferType.LoanReturn)
            .OrderByDescending(t => t.PerformedAtUtc)
            .Select(t => new { t.Type, t.Amount, t.PerformedAtUtc, t.CycleId, De = t.FromTeam != null ? t.FromTeam.TeamName : null })
            .FirstOrDefaultAsync(ct);
        var escolha = await _db.DraftPicks.AsNoTracking()
            .Where(p => p.PlayerId == playerId && p.TeamId == timeId && p.PickedAtUtc != null)
            .OrderByDescending(p => p.PickedAtUtc)
            .Select(p => new
            {
                Quando = p.PickedAtUtc!.Value, p.RoundNumber, p.PickInRound, p.Compensacao, p.Draft.Tipo,
                De = p.FromTeam != null ? p.FromTeam.TeamName : null
            })
            .FirstOrDefaultAsync(ct);

        // A escolha vale quando é a mais recente (a transferência da expansão sai no mesmo instante, com tolerância).
        if (escolha is not null && (transferencia is null || escolha.Quando >= transferencia.PerformedAtUtc.AddMinutes(-1)))
        {
            var rodada = $"rodada {escolha.RoundNumber}, escolha {escolha.PickInRound}";
            return escolha.Tipo == DraftTipo.Expansao
                ? new ValorDoJogadorChegadaDto(escolha.Quando,
                    escolha.De is null ? $"no draft de expansão ({rodada})" : $"no draft de expansão, do {escolha.De} ({rodada})",
                    escolha.Compensacao is > 0 ? escolha.Compensacao : null)
                : new ValorDoJogadorChegadaDto(escolha.Quando, $"no draft ({rodada})", null);
        }
        if (transferencia is null) return null;

        var pago = transferencia.Amount is > 0 ? transferencia.Amount : null;
        var de = transferencia.De is null ? "" : $" do {transferencia.De}";
        switch (transferencia.Type)
        {
            case TransferType.MarketAuction:
                var imediata = transferencia.CycleId is Guid ciclo && pago is decimal valor && await _db.MarketItems.AsNoTracking()
                    .AnyAsync(i => i.CycleId == ciclo && i.PlayerId == playerId && i.BuyNowPrice != null && valor >= i.BuyNowPrice, ct);
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, imediata ? "na compra imediata do leilão" : "no leilão", pago);
            case TransferType.TeamSale:
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, $"comprado{de}", pago);
            case TransferType.LoanPurchase:
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, $"na opção de compra{de}", pago);
            case TransferType.TeamTrade:
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, transferencia.De is null ? "numa troca" : $"na troca com o {transferencia.De}", null);
            case TransferType.Loan:
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, $"emprestado{de}", null);
            case TransferType.ExpansionDraft:
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, $"no draft de expansão{de}", pago);
            default:
                return new ValorDoJogadorChegadaDto(transferencia.PerformedAtUtc, de == "" ? "transferido" : $"transferido{de}", pago);
        }
    }
}
