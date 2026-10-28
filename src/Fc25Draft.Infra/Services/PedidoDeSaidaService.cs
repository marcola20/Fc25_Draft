using System.Globalization;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// O insatisfeito que segue no banco pede para sair: vai sozinho para a lista pelo preço da venda rápida (mesmo com a
/// janela fechada; a compra é que espera a janela abrir). O dono só tira com a promessa de colocar para jogar; promessa
/// quebrada devolve o jogador à lista pela metade.
/// </summary>
public class PedidoDeSaidaService : IPedidoDeSaidaService
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly DraftDbContext _db;
    private readonly IParticipacaoService _participacao;
    private readonly IPricingService _precos;
    private readonly ILogger<PedidoDeSaidaService> _logger;
    private readonly TimeProvider _time;

    public PedidoDeSaidaService(DraftDbContext db, IParticipacaoService participacao, IPricingService precos,
        ILogger<PedidoDeSaidaService> logger, TimeProvider? time = null)
    {
        _db = db;
        _participacao = participacao;
        _precos = precos;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<int> ProcessarAsync(CancellationToken ct)
    {
        var participacoes = await _participacao.GetTodosAsync(ct);
        var agora = _time.GetUtcNow().UtcDateTime;
        var mudaram = 0;

        foreach (var (playerId, p) in participacoes)
        {
            if (p.Saida is not { } saida) continue;

            var pedir = saida.FaltamParaPedir == 0;
            var promessaAcabou = saida.PromessaEm is not null
                                 && (!p.Insatisfeito || saida.JogosDaPromessa >= PedidoDeSaidaCriterios.JogosDaPromessa);
            if (!pedir && !promessaAcabou) continue;

            // A conta fica guardada por um minuto: confere no banco se nada mudou desde então.
            var vinculo = await _db.TeamRosters.Include(r => r.Player)
                .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.TeamId == p.TimeId, ct);
            if (vinculo is null || vinculo.SaidaPedidaEm is not null || vinculo.PromessaEm != saida.PromessaEm) continue;

            var nome = vinculo.Player.Name;
            var link = $"/players/details/{playerId}";

            if (saida.PromessaEm is not null && !p.Insatisfeito)
            {
                vinculo.PromessaEm = null;
                AvisosDoTime.Criar(_db, p.TimeId, AvisosDoTime.Promessa,
                    $"🤝 Promessa cumprida: {nome} voltou a ser titular e ficou no clube.", link, agora);
            }
            else
            {
                var quebrada = saida.PromessaEm is not null;
                var variacao = quebrada ? PedidoDeSaida.VariacaoDaPromessaQuebrada(p.Variacao) : p.Variacao;
                if (await PrecoNaListaAsync(vinculo.Player, variacao, ct) is not decimal preco) continue;

                vinculo.SaidaPedidaEm = agora;
                vinculo.VariacaoCongelada = variacao;
                vinculo.AskingPrice = preco;
                vinculo.ListedAtUtc = agora;
                vinculo.PromessaEm = null;
                vinculo.PromessaQuebrada |= quebrada;

                AvisosDoTime.Criar(_db, p.TimeId, AvisosDoTime.PediuParaSair,
                    quebrada
                        ? $"🚪 Promessa quebrada: {nome} não voltou a ser titular em {PedidoDeSaidaCriterios.JogosDaPromessa} jogos e "
                          + $"voltou à lista por {preco.ToString("C0", Brasil)}, metade da venda rápida (a venda rápida dele também caiu pela metade)."
                        : $"🚪 {nome} pediu para sair e foi para a lista de transferências por {preco.ToString("C0", Brasil)} "
                          + "(o preço da venda rápida). Só sai da lista com a promessa de colocar para jogar.",
                    link, agora);
            }

            mudaram++;
        }

        if (mudaram > 0)
        {
            await _db.SaveChangesAsync(ct);
            ParticipacaoService.Esquecer();
        }
        return mudaram;
    }

    /// <summary>O que a venda rápida pagaria com essa variação; nulo se o jogador não tem preço (sem idade).</summary>
    private async Task<decimal?> PrecoNaListaAsync(Core.Entities.Player jogador, decimal variacao, CancellationToken ct)
    {
        if (jogador.Age is not int idade)
        {
            _logger.LogWarning("{Jogador} pediu para sair mas não tem idade cadastrada: fica fora da lista.", jogador.Name);
            return null;
        }

        var preco = await _precos.CalculateForPositionAsync(null, jogador.PositionId, idade, jogador.Overall, ct);
        return PedidoDeSaida.PrecoNaLista(_precos.ComVariacao(preco, variacao).BasePrice);
    }

    public async Task PrometerAsync(Guid teamId, Guid playerGuid, CancellationToken ct)
    {
        var vinculo = await _db.TeamRosters.Include(r => r.Player)
            .FirstOrDefaultAsync(r => r.TeamId == teamId && r.Player.PlayerGuid == playerGuid, ct)
            ?? throw new KeyNotFoundException("Jogador não encontrado no seu elenco.");

        var nome = vinculo.Player.Name;
        if (vinculo.SaidaPedidaEm is null)
            throw new InvalidOperationException($"{nome} não pediu para sair.");
        if (vinculo.PromessaQuebrada)
            throw new InvalidOperationException($"Você já quebrou uma promessa com {nome}: ele não acredita mais e só sai vendido.");

        var emAndamento = await _db.TeamRosters
            .Where(r => r.TeamId == teamId && r.PromessaEm != null)
            .Select(r => r.Player.Name)
            .ToListAsync(ct);
        if (emAndamento.Count >= PedidoDeSaidaCriterios.PromessasPorTime)
            throw new InvalidOperationException(
                $"Você já tem uma promessa em andamento ({string.Join(", ", emAndamento)}). Cumpra ou quebre essa antes de prometer de novo.");

        vinculo.SaidaPedidaEm = null;
        vinculo.VariacaoCongelada = null;
        vinculo.AskingPrice = null;
        vinculo.ListedAtUtc = null;
        vinculo.PromessaEm = _time.GetUtcNow().UtcDateTime;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException($"{nome} acabou de ser negociado com outro time.");
        }
        ParticipacaoService.Esquecer();
    }
}
