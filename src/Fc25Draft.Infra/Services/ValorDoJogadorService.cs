using Fc25Draft.Core.DTOs;
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

        return new ValorDoJogadorDto(timeNome, preco.BasePrice, pontos);
    }
}
