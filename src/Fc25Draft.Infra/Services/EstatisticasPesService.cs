using System.Globalization;
using System.Text.Json;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Lê os números do PES ("estatisticas" do JSON importado: { "posse": { "casa": 43, "fora": 57 }, … }).
/// A importação só aceita a partida com o mesmo mando, então "casa" do JSON é o mandante da partida.
/// </summary>
public class EstatisticasPesService : IEstatisticasPesService
{
    // Ordem e nome de exibição; o que vier a mais no JSON aparece no fim com o nome da chave.
    private static readonly (string Chave, string Rotulo)[] Catalogo =
    [
        ("posse", "Posse de bola"),
        ("chutes", "Chutes"),
        ("chutes_a_gol", "Chutes a gol"),
        ("defesas", "Defesas do goleiro"),
        ("passes", "Passes"),
        ("desarmes", "Desarmes"),
        ("interceptacoes", "Interceptações"),
        ("escanteios", "Escanteios"),
        ("cruzamentos", "Cruzamentos"),
        ("faltas", "Faltas cometidas"),
        ("cobrancas_de_falta", "Cobranças de falta"),
        ("impedimentos", "Impedimentos"),
    ];

    private readonly DraftDbContext _db;

    public EstatisticasPesService(DraftDbContext db) => _db = db;

    public async Task<IReadOnlyList<NumeroDaPartidaDto>> DaPartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var json = await _db.LigaPartidaImportacoes.AsNoTracking()
            .Where(i => i.PartidaId == partidaId)
            .Select(i => i.Json)
            .FirstOrDefaultAsync(ct);
        var numeros = Ler(json);
        if (numeros.Count == 0) return Array.Empty<NumeroDaPartidaDto>();

        var lista = new List<NumeroDaPartidaDto>();
        foreach (var (chave, rotulo) in Catalogo)
        {
            if (!numeros.TryGetValue(chave, out var n)) continue;
            lista.Add(new NumeroDaPartidaDto(rotulo, n.Casa, n.Fora, chave == "posse"));

            // Logo depois dos passes, o acerto (passes certos sobre passes).
            if (chave == "passes" && numeros.TryGetValue("passes_certos", out var certos))
                lista.Add(new NumeroDaPartidaDto("Acerto de passes",
                    Porcento(certos.Casa, n.Casa) ?? 0, Porcento(certos.Fora, n.Fora) ?? 0, Percentual: true));
        }

        var conhecidas = Catalogo.Select(c => c.Chave).Append("passes_certos").ToHashSet();
        foreach (var (chave, n) in numeros.Where(x => !conhecidas.Contains(x.Key)))
            lista.Add(new NumeroDaPartidaDto(Rotular(chave), n.Casa, n.Fora));

        return lista;
    }

    public async Task<IReadOnlyList<NumerosDoTimeDto>> DosTimesAsync(Guid ligaId, CancellationToken ct)
    {
        var partidas = await _db.LigaPartidaImportacoes.AsNoTracking()
            .Where(i => i.Partida.Rodada.LigaId == ligaId && i.Partida.Status == PartidaStatus.Encerrada)
            .Select(i => new
            {
                i.Json,
                i.Partida.TimeCasaId,
                Casa = i.Partida.TimeCasa.TeamName,
                i.Partida.TimeForaId,
                Fora = i.Partida.TimeFora.TeamName,
                i.Partida.GolsCasa,
                i.Partida.GolsFora
            })
            .ToListAsync(ct);

        // Cada partida vira duas linhas: o que o time fez e o que sofreu.
        var linhas = partidas
            .Select(p => (p, n: Ler(p.Json)))
            .Where(x => x.n.Count > 0)
            .SelectMany(x => new[]
            {
                (Id: x.p.TimeCasaId, Nome: x.p.Casa, Gols: x.p.GolsCasa, Pro: Lado(x.n, casa: true), Contra: Lado(x.n, casa: false)),
                (Id: x.p.TimeForaId, Nome: x.p.Fora, Gols: x.p.GolsFora, Pro: Lado(x.n, casa: false), Contra: Lado(x.n, casa: true))
            });

        return linhas
            .GroupBy(l => new { l.Id, l.Nome })
            .Select(g =>
            {
                var jogos = g.Count();
                decimal Pro(string chave) => g.Sum(l => l.Pro.GetValueOrDefault(chave));
                decimal Contra(string chave) => g.Sum(l => l.Contra.GetValueOrDefault(chave));
                decimal PorJogo(decimal total) => Math.Round(total / jogos, 1);

                return new NumerosDoTimeDto(
                    g.Key.Id, g.Key.Nome, jogos,
                    Math.Round(Pro("posse") / jogos, 1),
                    PorJogo(Pro("chutes")),
                    PorJogo(Pro("chutes_a_gol")),
                    Porcento(Pro("chutes_a_gol"), Pro("chutes")),
                    Porcento(Pro("passes_certos"), Pro("passes")),
                    PorJogo(Contra("chutes")),
                    PorJogo(Contra("chutes_a_gol")),
                    PorJogo(Pro("defesas")),
                    PorJogo(Pro("desarmes")),
                    PorJogo(Pro("escanteios")),
                    PorJogo(Pro("faltas")),
                    Porcento(g.Sum(l => l.Gols), Pro("chutes_a_gol")));
            })
            .OrderByDescending(t => t.Posse)
            .ToList();
    }

    private sealed record Par(decimal Casa, decimal Fora);

    /// <summary>"estatisticas" do JSON da importação; vazio quando não tem ou não dá para ler.</summary>
    private static Dictionary<string, Par> Ler(string? json)
    {
        var numeros = new Dictionary<string, Par>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return numeros;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("estatisticas", out var est) || est.ValueKind != JsonValueKind.Object)
                return numeros;

            foreach (var item in est.EnumerateObject())
            {
                if (item.Value.ValueKind == JsonValueKind.Object
                    && Numero(item.Value, "casa") is decimal casa && Numero(item.Value, "fora") is decimal fora)
                    numeros[item.Name] = new Par(casa, fora);
            }
        }
        catch (JsonException)
        {
            // JSON antigo ou quebrado: a partida fica sem números.
        }
        return numeros;
    }

    private static decimal? Numero(JsonElement objeto, string nome) =>
        objeto.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;

    private static IReadOnlyDictionary<string, decimal> Lado(Dictionary<string, Par> numeros, bool casa) =>
        numeros.ToDictionary(n => n.Key, n => casa ? n.Value.Casa : n.Value.Fora, StringComparer.OrdinalIgnoreCase);

    private static decimal? Porcento(decimal parte, decimal todo) => todo > 0 ? Math.Round(parte * 100 / todo, 1) : null;

    private static string Rotular(string chave)
    {
        var texto = chave.Replace('_', ' ');
        return CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(texto);
    }
}
