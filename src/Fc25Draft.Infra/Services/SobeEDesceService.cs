using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Sobe e Desce do valor de mercado: compara a participação de hoje com a do começo do período (refeita só com os
/// jogos até lá) sobre o mesmo preço base. Quem não tem idade cadastrada fica de fora (não tem preço).
/// </summary>
public class SobeEDesceService : ISobeEDesceService
{
    private readonly DraftDbContext _db;
    private readonly IParticipacaoService _participacao;
    private readonly IPricingService _precos;
    private readonly TimeProvider _time;

    public SobeEDesceService(DraftDbContext db, IParticipacaoService participacao, IPricingService precos, TimeProvider? time = null)
    {
        _db = db;
        _participacao = participacao;
        _precos = precos;
        _time = time ?? TimeProvider.System;
    }

    public async Task<SobeEDesceDto> GetAsync(int dias, int quantos, CancellationToken ct)
    {
        var desde = _time.GetUtcNow().UtcDateTime.AddDays(-dias);
        var agora = await _participacao.GetTodosAsync(ct);
        var antes = await _participacao.GetTodosEmAsync(desde, ct);

        var jogadores = await _db.TeamRosters.AsNoTracking()
            .Where(r => r.Player.Age != null)
            .Select(r => new
            {
                r.PlayerId, r.Player.Name, r.TeamId, TimeNome = r.Team.TeamName, r.Player.PositionId,
                r.Player.Overall, Idade = r.Player.Age!.Value
            })
            .ToListAsync(ct);

        var linhas = new List<SobeEDesceJogadorDto>(jogadores.Count);
        foreach (var j in jogadores)
        {
            // A conta guardada pode ser de até um minuto atrás: só vale se ele ainda está no mesmo clube.
            if (agora.GetValueOrDefault(j.PlayerId) is not { } hoje || hoje.TimeId != j.TeamId) continue;
            var entao = antes.GetValueOrDefault(j.PlayerId);

            var preco = _precos.Calculate(MarketWeightResolver.GetByPositionId(j.PositionId), j.Overall, j.Idade);
            var variacaoAntes = entao?.Variacao ?? 0m;
            linhas.Add(new SobeEDesceJogadorDto(
                j.PlayerId, j.Name, j.TeamId, j.TimeNome, ((int)j.PositionId).ToPositionSigla(), j.Overall,
                _precos.ComVariacao(preco, variacaoAntes).BasePrice,
                _precos.ComVariacao(preco, hoje.Variacao).BasePrice,
                variacaoAntes, hoje.Variacao,
                entao?.Percentual, hoje.Percentual,
                entao?.Insatisfeito == true, hoje.Insatisfeito,
                entao is null || entao.EmCarencia));
        }

        return new SobeEDesceDto(
            desde,
            linhas.Where(l => l.Diferenca > 0).OrderByDescending(l => l.Diferenca).ThenBy(l => l.Nome).Take(quantos).ToList(),
            linhas.Where(l => l.Diferenca < 0).OrderBy(l => l.Diferenca).ThenBy(l => l.Nome).Take(quantos).ToList(),
            linhas.Where(l => l.InsatisfeitoAgora).OrderByDescending(l => l.Overall).ThenBy(l => l.Nome).ToList());
    }
}
