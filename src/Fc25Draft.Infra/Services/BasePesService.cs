using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public class BasePesService : IBasePesService
{
    private readonly DraftDbContext _db;
    private readonly ILogger<BasePesService> _logger;

    public BasePesService(DraftDbContext db, ILogger<BasePesService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Registro do arquivo pes-jogadores.json.gz (nomes curtos para o arquivo ficar pequeno).
    private sealed record Registro(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("n")] string Nome,
        [property: JsonPropertyName("p")] int Posicao,
        [property: JsonPropertyName("f")] int Perna,
        [property: JsonPropertyName("h")] int Altura,
        [property: JsonPropertyName("w")] int Peso,
        [property: JsonPropertyName("i")] int Idade,
        [property: JsonPropertyName("a")] int[] Atributos,
        [property: JsonPropertyName("t")] string[] Times,
        [property: JsonPropertyName("e")] int EstiloDeJogo,
        [property: JsonPropertyName("s")] long Habilidades,
        [property: JsonPropertyName("c")] int EstilosIa,
        [property: JsonPropertyName("pp")] string Posicoes,
        [property: JsonPropertyName("fo")] int Condicao,
        [property: JsonPropertyName("wa")] int PeFracoPrecisao,
        [property: JsonPropertyName("wu")] int PeFracoUso,
        [property: JsonPropertyName("na")] int? Nacionalidade = null)
    {
        public string Chave { get; } = NomesPes.Normalizar(Nome);
    }

    // Carregada uma vez por processo: ~17 mil jogadores, poucos MB em memória.
    private static readonly Lazy<IReadOnlyList<Registro>> Base = new(Carregar);

    private static IReadOnlyList<Registro> Carregar()
    {
        using var recurso = typeof(BasePesService).Assembly.GetManifestResourceStream("pes-jogadores.json.gz")
                            ?? throw new InvalidOperationException("Base do PES (pes-jogadores.json.gz) não encontrada.");
        using var gzip = new GZipStream(recurso, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<List<Registro>>(gzip) ?? [];
    }

    // Posição registrada no PES (0 = GK ... 12 = CF) → posição do site.
    private static readonly PositionType[] PosicaoDoPes =
    [
        PositionType.Goleiro, PositionType.Zagueiro, PositionType.LateralEsquerdo, PositionType.LateralDireito,
        PositionType.Volante, PositionType.MeiaLigacao, PositionType.MeiaEsquerda, PositionType.MeiaDireita,
        PositionType.MeiaAtacante, PositionType.PontaEsquerda, PositionType.PontaDireita,
        PositionType.SegundoAtacante, PositionType.Centroavante,
    ];

    private static short Posicao(Registro r) =>
        (short)(r.Posicao < PosicaoDoPes.Length ? PosicaoDoPes[r.Posicao] : PositionType.None);

    private static PlayerAtributosDto Atributos(Registro r)
    {
        var dto = new PlayerAtributosDto
        {
            PesId = r.Id,
            PosicaoPes = r.Posicao,
            Altura = r.Altura,
            Peso = r.Peso,
            PernaBoa = r.Perna == 1 ? PernaBoa.Esquerda : PernaBoa.Direita,
            EstiloDeJogo = r.EstiloDeJogo,
            Habilidades = r.Habilidades,
            EstilosIa = r.EstilosIa,
            Posicoes = r.Posicoes,
            Condicao = r.Condicao,
            PeFracoPrecisao = r.PeFracoPrecisao,
            PeFracoUso = r.PeFracoUso,
        };
        for (var i = 0; i < AtributosPes.Todos.Count; i++)
            AtributosPes.Todos[i].Set(dto, r.Atributos[i]);
        return dto;
    }

    private static readonly Lazy<Dictionary<int, Registro>> PorId =
        new(() => Base.Value.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First()));

    private static JogadorPesDto ParaDto(Registro r) => new(r.Id, r.Nome, r.Times, Posicao(r), r.Idade, Atributos(r));

    public IReadOnlyList<JogadorPesDto> Buscar(string texto, int maximo = 20)
    {
        // Número = ID do jogador no PES (o que o editor mostra).
        if (int.TryParse(texto.Trim(), out var pesId))
            return Base.Value.Where(r => r.Id == pesId).Select(ParaDto).ToList();

        var alvo = NomesPes.Normalizar(texto);
        if (alvo.Length < 2) return [];

        var termos = alvo.Split(' ');
        return Base.Value
            .Where(r => termos.All(r.Chave.Contains))
            .OrderBy(r => r.Chave == alvo ? 0 : r.Chave.StartsWith(alvo) ? 1 : 2)
            .ThenBy(r => r.Nome)
            .Take(maximo)
            .Select(ParaDto)
            .ToList();
    }

    public async Task<int> PreencherFaltantesAsync(CancellationToken ct = default)
    {
        var faltantes = await _db.Players
            .AsNoTracking()
            .Where(p => p.Atributos == null)
            .Select(p => new
            {
                p.PlayerId,
                p.Name,
                p.PositionId,
                p.Age,
                Time = p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        if (faltantes.Count == 0) return 0;

        var porNome = Base.Value.ToLookup(r => r.Chave);
        var novos = new Dictionary<int, PlayerAtributos>();

        foreach (var jogador in faltantes)
        {
            var chave = NomesPes.Normalizar(jogador.Name);
            var apelido = !chave.Contains(' '); // "Danilo", "Fred": há vários no PES
            var candidatos = porNome[chave].ToList();
            bool MesmoTime(Registro r) => r.Times.Any(t => TimeCombina(t, jogador.Time));
            // A idade do site é a do PES ou um ano a mais (99% dos casos conferidos).
            bool MesmaIdade(Registro r) => jogador.Age is int idade && idade - r.Idade is 0 or 1;

            if (candidatos.Count == 1)
            {
                // Nome completo já é específico; apelido precisa da posição batendo.
                if (apelido && Posicao(candidatos[0]) != jogador.PositionId) continue;
            }
            else if (candidatos.Count > 1)
            {
                candidatos = Desempatar(candidatos, r => Posicao(r) == jogador.PositionId);
                var mesmaIdade = candidatos.Where(MesmaIdade).ToList();
                if (mesmaIdade.Count == 1)
                {
                    candidatos = mesmaIdade;
                }
                else if (apelido)
                {
                    // Vários com o mesmo apelido e idade não resolve: só com o time batendo.
                    candidatos = candidatos.Where(MesmoTime).ToList();
                }
                else
                {
                    candidatos = Desempatar(candidatos, MesmoTime);
                    candidatos = Desempatar(candidatos, r => r.Times.Length > 0); // quem está em time no jogo
                }
            }

            if (candidatos.Count != 1) continue; // na dúvida, fica para a busca manual

            var atributos = new PlayerAtributos { PlayerId = jogador.PlayerId };
            AtributosPes.Aplicar(Atributos(candidatos[0]), atributos);
            _db.PlayerAtributos.Add(atributos);
            novos[jogador.PlayerId] = atributos;
        }

        await RecalcularOverallsAsync(novos, ct);
        await _db.SaveChangesAsync(ct);
        await PreencherPaisesAsync(ct);
        var preenchidos = novos.Count;
        _logger.LogInformation("Atributos do PES: {Preenchidos} de {Faltantes} jogadores sem atributos foram preenchidos.",
            preenchidos, faltantes.Count);
        return preenchidos;
    }

    public async Task<ResultadoLigacoesPesDto> DefinirLigacoesAsync(IReadOnlyList<DefinirLigacaoPesDto> itens,
        bool atualizarAtributos = false, CancellationToken ct = default)
    {
        if (itens.Count == 0) return new ResultadoLigacoesPesDto(0, 0, 0);

        var jogadores = itens.Select(i => i.PlayerId).ToList();
        if (jogadores.Distinct().Count() != jogadores.Count)
            throw new ArgumentException("Jogador repetido na lista.");

        var repetidos = itens.Where(i => i.PesId is not null).GroupBy(i => i.PesId).Where(g => g.Count() > 1)
            .Select(g => g.Key).ToList();
        if (repetidos.Count > 0)
            throw new ArgumentException($"ID do PES em mais de um jogador: {string.Join(", ", repetidos)}.");

        var porId = PorId.Value;
        var inexistentes = itens.Where(i => i.PesId is int id && !porId.ContainsKey(id)).Select(i => i.PesId).ToList();
        if (inexistentes.Count > 0)
            throw new ArgumentException($"ID do PES fora da base: {string.Join(", ", inexistentes)}.");

        var existentes = await _db.Players.Where(p => jogadores.Contains(p.PlayerId)).Select(p => p.PlayerId)
            .ToListAsync(ct);
        var faltam = jogadores.Except(existentes).ToList();
        if (faltam.Count > 0)
            throw new ArgumentException($"Jogador não encontrado: {string.Join(", ", faltam)}.");

        // O mesmo ID do PES não pode ficar em dois jogadores (considerando quem não está nesta lista).
        var pesIds = itens.Where(i => i.PesId is not null).Select(i => i.PesId!.Value).ToList();
        var conflitos = await _db.PlayerAtributos
            .Where(a => a.PesId != null && pesIds.Contains(a.PesId.Value) && !jogadores.Contains(a.PlayerId))
            .Select(a => new { a.PlayerId, a.PesId })
            .ToListAsync(ct);
        if (conflitos.Count > 0)
            throw new ArgumentException("ID do PES já ligado a outro jogador: " +
                                        string.Join(", ", conflitos.Select(c => $"{c.PesId} (jogador {c.PlayerId})")) + ".");

        var atuais = await _db.PlayerAtributos.Where(a => jogadores.Contains(a.PlayerId))
            .ToDictionaryAsync(a => a.PlayerId, ct);
        int ligados = 0, desligados = 0, iguais = 0, atualizados = 0;
        foreach (var item in itens)
        {
            atuais.TryGetValue(item.PlayerId, out var atributos);
            if (item.PesId is not int pesId)
            {
                if (atributos?.PesId is null) { iguais++; continue; }
                atributos.PesId = null;
                desligados++;
                continue;
            }
            if (atributos?.PesId == pesId)
            {
                if (!atualizarAtributos) { iguais++; continue; }
                AtributosPes.Aplicar(Atributos(porId[pesId]), atributos);
                atualizados++;
                continue;
            }
            if (atributos is null)
            {
                atributos = new PlayerAtributos { PlayerId = item.PlayerId };
                _db.PlayerAtributos.Add(atributos);
                atuais[item.PlayerId] = atributos;
            }
            AtributosPes.Aplicar(Atributos(porId[pesId]), atributos);
            ligados++;
        }

        await RecalcularOverallsAsync(atuais, ct);
        await _db.SaveChangesAsync(ct);
        await PreencherPaisesAsync(ct);
        _logger.LogInformation(
            "Ligações do PES pelo editor: {Ligados} ligados, {Desligados} desligados, {Iguais} iguais, {Atualizados} com atributos atualizados.",
            ligados, desligados, iguais, atualizados);
        return new ResultadoLigacoesPesDto(ligados, desligados, iguais, atualizados);
    }

    public async Task<int> PreencherPaisesAsync(CancellationToken ct = default)
    {
        var semPais = await _db.Players
            .Where(p => p.Pais == null && p.Atributos != null && p.Atributos.PesId != null)
            .Select(p => new { Jogador = p, PesId = p.Atributos!.PesId!.Value })
            .ToListAsync(ct);

        var porId = PorId.Value;
        var preenchidos = 0;
        foreach (var s in semPais)
        {
            if (!porId.TryGetValue(s.PesId, out var r) || Paises.DoCodigoPes(r.Nacionalidade) is not { } pais) continue;
            s.Jogador.Pais = pais;
            preenchidos++;
        }

        if (preenchidos > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("País do PES: {Preenchidos} de {SemPais} jogadores sem país foram preenchidos.", preenchidos, semPais.Count);
        }
        return preenchidos;
    }

    /// <summary>Overall pela fórmula do PES de quem teve os atributos mexidos.</summary>
    private async Task RecalcularOverallsAsync(IReadOnlyDictionary<int, PlayerAtributos> atributos, CancellationToken ct)
    {
        var ids = atributos.Keys.ToList();
        var jogadores = await _db.Players.Where(p => ids.Contains(p.PlayerId)).ToListAsync(ct);
        foreach (var jogador in jogadores)
        {
            jogador.Atributos = atributos[jogador.PlayerId];
            OverallPes.Recalcular(jogador);
        }
    }

    /// <summary>Mesmo time, aceitando nome abreviado no site ("Vasco" x "Vasco da Gama", "Sport" x "Sport Recife").</summary>
    private static bool TimeCombina(string timePes, string? timeSite)
    {
        if (string.IsNullOrWhiteSpace(timeSite)) return false;
        if (NomesPes.MesmoTime(timePes, timeSite)) return true;
        var site = NomesPes.Normalizar(timeSite);
        return $" {NomesPes.Normalizar(timePes)} ".Contains($" {site} ");
    }

    /// <summary>Aplica o filtro só se sobrar alguém; senão mantém os candidatos.</summary>
    private static List<Registro> Desempatar(List<Registro> candidatos, Func<Registro, bool> filtro)
    {
        var filtrados = candidatos.Where(filtro).ToList();
        return filtrados.Count > 0 ? filtrados : candidatos;
    }
}
