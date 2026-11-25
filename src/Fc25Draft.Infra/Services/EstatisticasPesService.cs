using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
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
    private readonly TimeProvider _time;

    public EstatisticasPesService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>O formulário do admin: o catálogo, com os passes certos logo depois dos passes.</summary>
    private static IEnumerable<(string Chave, string Rotulo)> Editaveis() =>
        Catalogo.SelectMany(c => c.Chave == "passes"
            ? new[] { c, ("passes_certos", "Passes certos") }
            : new[] { c });

    public async Task<IReadOnlyList<CampoNumeroDto>> ParaEditarAsync(Guid partidaId, CancellationToken ct)
    {
        var json = await _db.LigaPartidaImportacoes.AsNoTracking()
            .Where(i => i.PartidaId == partidaId)
            .Select(i => i.Json)
            .FirstOrDefaultAsync(ct);
        var numeros = Ler(json);

        return Editaveis()
            .Select(c => numeros.TryGetValue(c.Chave, out var n)
                ? new CampoNumeroDto(c.Chave, c.Rotulo, n.Casa, n.Fora)
                : new CampoNumeroDto(c.Chave, c.Rotulo, null, null))
            .ToList();
    }

    public async Task SalvarAsync(Guid partidaId, IReadOnlyList<CampoNumeroDto> campos, CancellationToken ct)
    {
        var rotulos = Editaveis().ToDictionary(c => c.Chave, c => c.Rotulo);
        foreach (var c in campos.Where(c => rotulos.ContainsKey(c.Chave)))
        {
            if ((c.Casa is null) != (c.Fora is null))
                throw new InvalidOperationException($"Preencha os dois times em \"{rotulos[c.Chave]}\" (ou deixe os dois vazios).");
            if (c.Casa < 0 || c.Fora < 0)
                throw new InvalidOperationException($"\"{rotulos[c.Chave]}\" não pode ser negativo.");
        }

        var importacao = await _db.LigaPartidaImportacoes.FirstOrDefaultAsync(i => i.PartidaId == partidaId, ct);
        if (importacao is null && !await _db.LigaPartidas.AnyAsync(p => p.PartidaId == partidaId, ct))
            throw new InvalidOperationException("Partida não encontrada.");

        // O resto do JSON da importação (eventos, notas, vídeo…) fica como está.
        JsonObject raiz;
        try
        {
            raiz = importacao is null ? new JsonObject() : JsonNode.Parse(importacao.Json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            raiz = new JsonObject();
        }
        var estatisticas = raiz["estatisticas"] as JsonObject ?? new JsonObject();
        raiz["estatisticas"] = estatisticas;

        foreach (var c in campos.Where(c => rotulos.ContainsKey(c.Chave)))
        {
            if (c.Casa is decimal casa && c.Fora is decimal fora)
                estatisticas[c.Chave] = new JsonObject { ["casa"] = casa, ["fora"] = fora };
            else
                estatisticas.Remove(c.Chave);
        }

        if (importacao is null && estatisticas.Count == 0) return;

        var agora = _time.GetUtcNow().UtcDateTime;
        if (importacao is null)
        {
            importacao = new LigaPartidaImportacao { PartidaId = partidaId, ImportadoEm = agora };
            _db.LigaPartidaImportacoes.Add(importacao);
        }
        importacao.Json = raiz.ToJsonString();
        importacao.AtualizadoEm = agora;
        await _db.SaveChangesAsync(ct);
    }

    // Rodada sentinela do playoff de acesso: guardado na Série A, mas é entre times das duas séries.
    private const int NumeroPlayoffAcesso = -2;

    public async Task<IReadOnlyList<RegistroDoJogadorDto>> RegistrosDaCompeticaoAsync(Guid ligaId, CancellationToken ct)
    {
        var calculadas = await NotasOficiais.CalcularAsync(_db,
            x => x.JogadorId != null
                 && x.Partida.Rodada.LigaId == ligaId
                 && x.Partida.Rodada.Numero != NumeroPlayoffAcesso
                 && x.Partida.Status == PartidaStatus.Encerrada, ct);
        if (calculadas.Count == 0) return Array.Empty<RegistroDoJogadorDto>();

        var partidas = calculadas.Select(c => c.Numeros.PartidaId).Distinct().ToList();
        var quando = await _db.LigaPartidas.AsNoTracking()
            .Where(p => partidas.Contains(p.PartidaId))
            .Select(p => new { p.PartidaId, Quando = p.EncerradaEm ?? p.IniciadaEm })
            .ToDictionaryAsync(p => p.PartidaId, p => p.Quando, ct);
        var times = await _db.Teams.AsNoTracking().Select(t => new { t.TeamId, t.TeamName }).ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        return calculadas
            .GroupBy(x => x.Numeros.JogadorId!.Value)
            .Select(g =>
            {
                var ultimo = g.OrderByDescending(x => quando.GetValueOrDefault(x.Numeros.PartidaId)).First();
                int Soma(Func<LigaEstatisticaJogador, int?> campo) => g.Sum(x => campo(x.Numeros) ?? 0);
                var noGol = g.Where(x => NotaDaLiga.GrupoDe(x.Numeros.Grupo, x.Numeros.Posicao) == NotaDaLiga.Goleiro).ToList();
                return new RegistroDoJogadorDto(
                    g.Key, ultimo.Nome, ultimo.Numeros.Posicao, ultimo.Numeros.TimeId,
                    times.GetValueOrDefault(ultimo.Numeros.TimeId, ""),
                    g.Count(), Soma(x => x.Minutos),
                    Math.Round(g.Average(x => x.Nota.Nota), 2), g.Max(x => x.Nota.Nota),
                    Soma(x => x.Gols), Soma(x => x.Assistencias),
                    Soma(x => x.Chutes), Soma(x => x.ChutesAGol),
                    Soma(x => x.Passes), Soma(x => x.PassesCertos),
                    Soma(x => x.Desarmes), Soma(x => x.DesarmesCertos),
                    Soma(x => x.Interceptacoes), Soma(x => x.Afastamentos),
                    Soma(x => x.Cruzamentos), Soma(x => x.Toques), Soma(x => x.Faltas),
                    g.Sum(x => x.Numeros.DistanciaDeDrible ?? 0),
                    noGol.Count > 0 ? noGol.Sum(x => x.Numeros.Defesas ?? 0) : null,
                    noGol.Count > 0 ? noGol.Count : null);
            })
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, DesempenhoJogadorDto>> RegistrosDoJogadorAsync(int playerId, CancellationToken ct)
    {
        // A partida inteira entra no cálculo só para achar o melhor em campo; devolve a linha do jogador.
        var partidas = await _db.LigaEstatisticasJogadores.AsNoTracking()
            .Where(x => x.JogadorId == playerId)
            .Select(x => x.PartidaId)
            .ToListAsync(ct);
        if (partidas.Count == 0) return new Dictionary<Guid, DesempenhoJogadorDto>();

        var calculadas = await NotasOficiais.CalcularAsync(_db, x => partidas.Contains(x.PartidaId), ct);
        var melhores = NotasOficiais.MelhoresEmCampo(calculadas);
        return calculadas
            .Where(c => c.Numeros.JogadorId == playerId)
            .GroupBy(c => c.Numeros.PartidaId)
            .ToDictionary(g => g.Key, g => Desempenho(g.First(), melhores));
    }

    private static DesempenhoJogadorDto Desempenho(NotasOficiais.NotaCalculada c, HashSet<Guid> melhores) =>
        new(c.Numeros.JogadorId, c.Nome, c.Nota, c.Numeros, melhores.Contains(c.Numeros.Id));

    public async Task<JogadoresDaPartidaDto?> JogadoresDaPartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var calculadas = await NotasOficiais.CalcularAsync(_db, x => x.PartidaId == partidaId, ct);
        if (calculadas.Count == 0) return null;

        var casaId = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.PartidaId == partidaId)
            .Select(p => p.TimeCasaId)
            .FirstAsync(ct);
        var melhores = NotasOficiais.MelhoresEmCampo(calculadas);
        var ordenadas = calculadas.OrderBy(c => c.Numeros.Ordem).ToList();

        return new JogadoresDaPartidaDto(
            ordenadas.Where(c => c.Numeros.TimeId == casaId).Select(c => Desempenho(c, melhores)).ToList(),
            ordenadas.Where(c => c.Numeros.TimeId != casaId).Select(c => Desempenho(c, melhores)).ToList());
    }

    public async Task<IReadOnlyList<NumeroDaPartidaDto>> DaPartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var json = await _db.LigaPartidaImportacoes.AsNoTracking()
            .Where(i => i.PartidaId == partidaId)
            .Select(i => i.Json)
            .FirstOrDefaultAsync(ct);
        return DoJson(json);
    }

    public IReadOnlyList<NumeroDaPartidaDto> DoJson(string? json) => NumerosDoJson(json);

    /// <summary>Números dos times no JSON do PES, na ordem de exibição; vazio quando não tem.</summary>
    public static IReadOnlyList<NumeroDaPartidaDto> NumerosDoJson(string? json)
    {
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
