namespace Fc25Draft.Core.Utilities;

/// <summary>Jogo de um time numa competição, na ordem em que acontece (<see cref="Ordem"/>).</summary>
/// <param name="MataMata">Jogo do mata-mata da Copa (a contagem de amarelos zera ao entrar nele).</param>
public sealed record JogoDoTime(Guid PartidaId, Guid TimeId, long Ordem, bool MataMata, bool Encerrado);

public sealed record CartaoNoJogo(Guid PartidaId, Guid TimeId, int JogadorId, bool Vermelho);

/// <summary>
/// Uma suspensão: o jogo em que o jogador levou o cartão e o jogo que ele fica fora
/// (<see cref="PartidaCumprida"/> nulo enquanto o próximo jogo do time não existe).
/// </summary>
public sealed record Suspensao(
    Guid TimeId, int JogadorId, string Motivo, Guid PartidaDoCartao, Guid? PartidaCumprida, bool Cumprida);

/// <summary>Jogador com 2 amarelos: o próximo suspende.</summary>
public sealed record Pendurado(Guid TimeId, int JogadorId, int Amarelos);

/// <summary>
/// Suspensão automática numa competição (Liga ou Copa, cada uma com a sua conta): 3 amarelos ou 1
/// vermelho tiram o jogador do próximo jogo do time na competição. Ao completar 3 amarelos a conta
/// recomeça. Amarelo no mesmo jogo do vermelho não entra na conta. Na Copa, a conta de amarelos zera
/// ao entrar no mata-mata (suspensão já aplicada continua valendo).
/// </summary>
public static class Suspensoes
{
    public const int AmarelosParaSuspensao = 3;

    public sealed record Resultado(IReadOnlyList<Suspensao> Suspensoes, IReadOnlyList<Pendurado> Pendurados);

    public static Resultado Calcular(IEnumerable<JogoDoTime> jogos, IEnumerable<CartaoNoJogo> cartoes, bool zerarAmarelosNoMataMata)
    {
        var suspensoes = new List<Suspensao>();
        var pendurados = new List<Pendurado>();

        var cartoesPorJogo = cartoes.ToLookup(c => (c.PartidaId, c.TimeId));

        foreach (var doTime in jogos.GroupBy(j => j.TimeId))
        {
            var ordem = doTime.OrderBy(j => j.Ordem).ToList();
            var jogadores = ordem.SelectMany(j => cartoesPorJogo[(j.PartidaId, j.TimeId)]).Select(c => c.JogadorId).Distinct();

            foreach (var jogadorId in jogadores)
            {
                var amarelos = 0;
                var entrouNoMataMata = false;
                // Índice do próximo jogo ainda livre para cumprir suspensão (um jogo por suspensão).
                var proximoLivre = 0;

                for (int i = 0; i < ordem.Count; i++)
                {
                    var jogo = ordem[i];
                    if (zerarAmarelosNoMataMata && jogo.MataMata && !entrouNoMataMata)
                    {
                        entrouNoMataMata = true;
                        amarelos = 0;
                    }
                    if (!jogo.Encerrado) continue;

                    var doJogo = cartoesPorJogo[(jogo.PartidaId, jogo.TimeId)].Where(c => c.JogadorId == jogadorId).ToList();
                    if (doJogo.Count == 0) continue;

                    string? motivo = null;
                    if (doJogo.Any(c => c.Vermelho))
                        motivo = "Cartão vermelho";
                    else
                    {
                        amarelos += doJogo.Count;
                        if (amarelos >= AmarelosParaSuspensao)
                        {
                            amarelos -= AmarelosParaSuspensao;
                            motivo = $"{AmarelosParaSuspensao}º amarelo";
                        }
                    }
                    if (motivo is null) continue;

                    // Cumpre no primeiro jogo depois deste que ainda não está ocupado por outra suspensão.
                    proximoLivre = Math.Max(proximoLivre, i + 1);
                    var cumpre = proximoLivre < ordem.Count ? ordem[proximoLivre] : null;
                    proximoLivre++;

                    suspensoes.Add(new Suspensao(doTime.Key, jogadorId, motivo, jogo.PartidaId, cumpre?.PartidaId, cumpre?.Encerrado == true));
                }

                if (amarelos == AmarelosParaSuspensao - 1)
                    pendurados.Add(new Pendurado(doTime.Key, jogadorId, amarelos));
            }
        }

        return new Resultado(suspensoes, pendurados);
    }
}
