using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public record RecordeJogoDto(
    Guid PartidaId, string Competicao, string Etapa,
    Guid CasaId, string CasaNome, Guid ForaId, string ForaNome,
    int GolsCasa, int GolsFora)
{
    public int Diferenca => Math.Abs(GolsCasa - GolsFora);
    public int TotalGols => GolsCasa + GolsFora;
}

public record RecordeSequenciaDto(Guid TimeId, string TimeNome, int Jogos);

public record RecordeJogadorDto(int JogadorId, string Nome, int Valor);

public record RecordeJogadorEdicaoDto(int JogadorId, string Nome, string TimeNome, string Competicao, int Gols);

public record RecordeHattrickDto(
    int JogadorId, string Nome, string TimeNome, int Gols,
    string AdversarioNome, int GolsPro, int GolsContra, string Competicao, string Etapa);

public record RecordeCampanhaDto(
    Guid TimeId, string TimeNome, string Competicao,
    int Pontos, int Jogos, int Vitorias, int Empates, int Derrotas, int GolsPro, int GolsContra)
{
    public double Aproveitamento => Jogos == 0 ? 0 : Pontos * 100.0 / (Jogos * 3);
    public double GolsProPorJogo => Jogos == 0 ? 0 : (double)GolsPro / Jogos;
    public double GolsContraPorJogo => Jogos == 0 ? 0 : (double)GolsContra / Jogos;
}

public record RecordesLigaDto(
    IReadOnlyList<RecordeJogoDto> MaioresGoleadas,
    IReadOnlyList<RecordeJogoDto> JogosComMaisGols,
    IReadOnlyList<RecordeSequenciaDto> SequenciasDeVitorias,
    IReadOnlyList<RecordeSequenciaDto> Invencibilidades,
    IReadOnlyList<RecordeCampanhaDto> MelhoresCampanhas,
    IReadOnlyList<RecordeCampanhaDto> MelhoresAtaques,
    IReadOnlyList<RecordeCampanhaDto> MelhoresDefesas,
    IReadOnlyList<RecordeJogadorDto> Artilheiros,
    IReadOnlyList<RecordeJogadorDto> Garcons,
    IReadOnlyList<RecordeJogadorEdicaoDto> MaisGolsNumaEdicao,
    IReadOnlyList<RecordeHattrickDto> Hattricks);

/// <summary>Gol a favor (não conta gol contra), com a competição para os recordes por edição.</summary>
public record RecordeGolInput(
    Guid PartidaId, Guid LigaId, string LigaNome, Guid TimeId,
    int JogadorId, string JogadorNome, int? AssistenteId, string? AssistenteNome);

/// <summary>Linha final da tabela de uma liga já encerrada.</summary>
public record RecordeCampanhaInput(
    Guid TimeId, string Competicao, int Pontos, int Jogos, int Vitorias, int Empates, int Derrotas, int GolsPro, int GolsContra);

/// <summary>Recordes de todas as temporadas e competições, calculados na hora.</summary>
public static class RecordesLiga
{
    public const int Top = 5;
    public const int TopJogadores = 10;

    public static RecordesLigaDto Calcular(
        IReadOnlyList<TimePerfilPartidaInput> partidas,
        IEnumerable<RecordeGolInput> gols,
        IEnumerable<RecordeCampanhaInput> campanhas,
        IReadOnlyDictionary<Guid, string> nomes)
    {
        string Nome(Guid id) => nomes.TryGetValue(id, out var n) ? n : "?";

        var encerradas = partidas.Where(p => p.Status == PartidaStatus.Encerrada).ToList();
        var jogos = encerradas
            .Select(p => new RecordeJogoDto(p.PartidaId, p.Competicao, p.Etapa,
                p.CasaId, Nome(p.CasaId), p.ForaId, Nome(p.ForaId), p.GolsCasa, p.GolsFora))
            .ToList();

        // Mais recente primeiro no desempate: o recorde "vigente" aparece antes.
        var recencia = encerradas.ToDictionary(p => p.PartidaId, p => p.EncerradaEm ?? DateTime.MinValue);

        var goleadas = jogos
            .Where(j => j.Diferenca >= 3)
            .OrderByDescending(j => j.Diferenca)
            .ThenByDescending(j => j.TotalGols)
            .ThenByDescending(j => recencia[j.PartidaId])
            .Take(Top)
            .ToList();

        var maisGols = jogos
            .Where(j => j.TotalGols > 0)
            .OrderByDescending(j => j.TotalGols)
            .ThenByDescending(j => j.Diferenca)
            .ThenByDescending(j => recencia[j.PartidaId])
            .Take(Top)
            .ToList();

        // Mesmo cálculo da página do time, para cada clube que já jogou.
        var times = encerradas.SelectMany(p => new[] { p.CasaId, p.ForaId }).Distinct().ToList();
        var perfis = times.Select(t => TimePerfil.Calcular(t, partidas, nomes)).ToList();

        List<RecordeSequenciaDto> Sequencias(Func<Fc25Draft.Core.DTOs.TimePerfilDto, int> valor) => perfis
            .Where(p => valor(p) >= 2)
            .Select(p => new RecordeSequenciaDto(p.TimeId, Nome(p.TimeId), valor(p)))
            .OrderByDescending(s => s.Jogos)
            .ThenBy(s => s.TimeNome, StringComparer.OrdinalIgnoreCase)
            .Take(Top)
            .ToList();

        var camps = campanhas
            .Where(c => c.Jogos > 0)
            .Select(c => new RecordeCampanhaDto(c.TimeId, Nome(c.TimeId), c.Competicao,
                c.Pontos, c.Jogos, c.Vitorias, c.Empates, c.Derrotas, c.GolsPro, c.GolsContra))
            .ToList();

        var golsLista = gols.ToList();
        var partidaPorId = encerradas.ToDictionary(p => p.PartidaId);

        var hattricks = golsLista
            .GroupBy(g => (g.PartidaId, g.JogadorId))
            .Where(g => g.Count() >= 3 && partidaPorId.ContainsKey(g.Key.PartidaId))
            .Select(g =>
            {
                var gol = g.First();
                var p = partidaPorId[g.Key.PartidaId];
                var emCasa = gol.TimeId == p.CasaId;
                return new
                {
                    Data = p.EncerradaEm ?? DateTime.MinValue,
                    Dto = new RecordeHattrickDto(
                        gol.JogadorId, gol.JogadorNome, Nome(gol.TimeId), g.Count(),
                        Nome(emCasa ? p.ForaId : p.CasaId),
                        emCasa ? p.GolsCasa : p.GolsFora,
                        emCasa ? p.GolsFora : p.GolsCasa,
                        p.Competicao, p.Etapa)
                };
            })
            .OrderByDescending(h => h.Dto.Gols)
            .ThenByDescending(h => h.Data)
            .Select(h => h.Dto)
            .ToList();

        return new RecordesLigaDto(
            goleadas,
            maisGols,
            Sequencias(p => p.MaiorSequenciaVitorias),
            Sequencias(p => p.MaiorInvencibilidade),
            camps.OrderByDescending(c => c.Aproveitamento).ThenByDescending(c => c.Pontos).Take(Top).ToList(),
            camps.OrderByDescending(c => c.GolsProPorJogo).ThenByDescending(c => c.GolsPro).Take(Top).ToList(),
            camps.OrderBy(c => c.GolsContraPorJogo).ThenBy(c => c.GolsContra).Take(Top).ToList(),
            Ranking(golsLista.Select(g => (g.JogadorId, g.JogadorNome))),
            Ranking(golsLista.Where(g => g.AssistenteId is not null).Select(g => (g.AssistenteId!.Value, g.AssistenteNome ?? "?"))),
            golsLista
                .GroupBy(g => (g.JogadorId, g.LigaId, g.TimeId))
                .Select(g => new RecordeJogadorEdicaoDto(g.Key.JogadorId, g.First().JogadorNome, Nome(g.Key.TimeId), g.First().LigaNome, g.Count()))
                .OrderByDescending(r => r.Gols)
                .ThenBy(r => r.Nome, StringComparer.OrdinalIgnoreCase)
                .Take(Top)
                .ToList(),
            hattricks);
    }

    private static IReadOnlyList<RecordeJogadorDto> Ranking(IEnumerable<(int Id, string Nome)> itens) =>
        itens
            .GroupBy(i => i.Id)
            .Select(g => new RecordeJogadorDto(g.Key, g.First().Nome, g.Count()))
            .OrderByDescending(r => r.Valor)
            .ThenBy(r => r.Nome, StringComparer.OrdinalIgnoreCase)
            .Take(TopJogadores)
            .ToList();
}
