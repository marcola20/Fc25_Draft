using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Valor do elenco ao longo do tempo. Parte do elenco de hoje e volta no tempo desfazendo as transferências
/// do time (quem chegou sai, quem saiu volta), parando no fim de cada janela do mercado. Todo jogador entra pelo
/// valor de mercado de hoje: o gráfico compara elencos, não a inflação dos preços.
/// </summary>
public class ValorElencoService : IValorElencoService
{
    private readonly DraftDbContext _db;
    private readonly ITermometroMercadoService _termometro;

    public ValorElencoService(DraftDbContext db, ITermometroMercadoService termometro)
    {
        _db = db;
        _termometro = termometro;
    }

    public async Task<IReadOnlyList<ValorElencoPontoDto>> HistoricoAsync(Guid timeId, CancellationToken ct)
    {
        var elenco = (await _db.TeamRosters.AsNoTracking()
            .Where(r => r.TeamId == timeId)
            .Select(r => r.PlayerId)
            .ToListAsync(ct)).ToHashSet();

        var movimentos = await _db.TransferHistories.AsNoTracking()
            .Where(t => t.Type != TransferType.None && (t.ToTeamId == timeId || t.FromTeamId == timeId))
            .OrderByDescending(t => t.PerformedAtUtc)
            .Select(t => new { t.PerformedAtUtc, t.PlayerId, Chegou = t.ToTeamId == timeId })
            .ToListAsync(ct);

        // Do mais recente para o mais antigo: hoje, o fim de cada janela e o começo da primeira.
        // A janela que ainda está aberta é o próprio "Hoje".
        var agora = DateTime.UtcNow;
        var janelas = await _termometro.ListarJanelasAsync(ct);
        var marcos = new List<(string Rotulo, DateTime Data)> { ("Hoje", agora) };
        marcos.AddRange(janelas.Where(j => j.Fim < agora).OrderByDescending(j => j.Fim).Select(j => (Curto(j.Nome), j.Fim)));
        if (janelas.Count > 0) marcos.Add(("Início", janelas.Min(j => j.Inicio)));

        var retratos = new List<(string Rotulo, DateTime Data, List<int> Jogadores)>();
        var proximo = 0;
        foreach (var (rotulo, data) in marcos)
        {
            // Desfaz tudo o que aconteceu depois deste momento.
            for (; proximo < movimentos.Count && movimentos[proximo].PerformedAtUtc > data; proximo++)
            {
                var m = movimentos[proximo];
                if (m.Chegou) elenco.Remove(m.PlayerId);
                else elenco.Add(m.PlayerId);
            }
            retratos.Add((rotulo, data, elenco.ToList()));
        }

        // Um preço e um overall por jogador, usados em todos os retratos.
        var ids = retratos.SelectMany(r => r.Jogadores).Distinct().ToList();
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.PositionId, p.Age, p.Overall })
            .ToListAsync(ct);
        var overalls = jogadores.ToDictionary(j => j.PlayerId, j => j.Overall);

        // Mesma conta do valor de mercado da página do jogador (com o mesmo contexto: a config é lida uma vez).
        var pricing = new PricingService(_db);
        var precos = new Dictionary<int, decimal>();
        foreach (var j in jogadores.Where(j => j.Age > 0 && j.Overall > 0))
            precos[j.PlayerId] = (await pricing.CalculateForPositionAsync(null, j.PositionId, j.Age!.Value, j.Overall, ct)).BasePrice;

        return retratos
            .AsEnumerable()
            .Reverse()
            .Select(r =>
            {
                var validos = r.Jogadores.Where(overalls.ContainsKey).ToList();
                return new ValorElencoPontoDto(
                    r.Rotulo, r.Data,
                    validos.Sum(id => precos.GetValueOrDefault(id)),
                    validos.Count,
                    validos.Count == 0 ? 0 : Math.Round((decimal)validos.Average(id => overalls[id]), 1));
            })
            .ToList();
    }

    // "Janela de set/2026 (2)" → "set/2026 (2)"
    private static string Curto(string nome) =>
        nome.StartsWith("Janela de ", StringComparison.OrdinalIgnoreCase) ? nome["Janela de ".Length..] : nome;
}
