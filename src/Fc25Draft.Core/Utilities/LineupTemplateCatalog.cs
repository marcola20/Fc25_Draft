using System;
using System.Collections.ObjectModel;
using System.Linq;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// As formações da escalação e quem pode jogar em cada lugar. Os dados vêm do <see cref="CatalogoEscalacao"/>
/// salvo no banco (editado em /admin/formacoes, carregado na subida do site); sem nada salvo, vale o
/// <see cref="CatalogoEscalacaoPadrao"/>. Fica em memória: quem salva o catálogo chama <see cref="Usar"/>.
/// </summary>
public static class LineupTemplateCatalog
{
    public const int TitularesPorFormacao = 11;
    public const int ReservasPorFormacao = 7;

    /// <summary>Limites das colunas onde a escalação grava a formação e o lugar.</summary>
    public const int MaxNomeFormacao = 20, MaxCodigoLugar = 20, MaxNomeLugar = 80;
    public const int MaxCodigoPosicao = 10, MaxNomePosicao = 40;

    /// <summary>Sigla de cada posição do PES 2021 pelo id (1=GOL ... 13=SA).</summary>
    public static readonly IReadOnlyDictionary<short, string> SiglasPes = new Dictionary<short, string>
    {
        [1] = "GOL", [2] = "ZAG", [3] = "LE", [4] = "LD", [5] = "VOL", [6] = "MLG", [7] = "MAT",
        [8] = "ME", [9] = "PE", [10] = "MD", [11] = "PD", [12] = "CA", [13] = "SA"
    };

    private static Snapshot _atual = new(Normalizar(CatalogoEscalacaoPadrao.Criar()));

    /// <summary>O catálogo em uso.</summary>
    public static CatalogoEscalacao Atual => _atual.Catalogo;

    public static string DefaultFormation => _atual.Padrao.Formation;

    /// <summary>Troca o catálogo em uso. Lança se ele não passar em <see cref="Validar"/>.</summary>
    public static void Usar(CatalogoEscalacao catalogo)
    {
        catalogo = Normalizar(catalogo);
        var erros = Validar(catalogo);
        if (erros.Count > 0)
            throw new InvalidOperationException(string.Join(" ", erros));
        _atual = new Snapshot(catalogo);
    }

    /// <summary>A formação pelo nome (inclusive desativada, para quem já usa); vazio dá a padrão.</summary>
    public static LineupTemplate GetTemplateOrDefault(string? formation)
    {
        var atual = _atual;
        if (string.IsNullOrWhiteSpace(formation))
            return atual.Padrao;

        if (atual.Templates.TryGetValue(formation.Trim(), out var template))
            return template;

        throw new InvalidOperationException($"Formação '{formation}' não é suportada.");
    }

    /// <summary>As formações que um time pode escolher (as ativas), em ordem alfabética.</summary>
    public static IReadOnlyList<string> GetSupportedFormations()
        => _atual.Catalogo.Formacoes
            .Where(f => f.Ativa)
            .Select(f => f.Nome)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Tira espaços das pontas, põe os códigos em maiúsculas e ordena quem pode jogar.</summary>
    public static CatalogoEscalacao Normalizar(CatalogoEscalacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        static string T(string? s) => (s ?? string.Empty).Trim();
        static string C(string? s) => T(s).ToUpperInvariant();

        return new CatalogoEscalacao(
            T(catalogo.Padrao),
            (catalogo.Posicoes ?? Array.Empty<PosicaoDaEscalacao>())
                .Select(p => new PosicaoDaEscalacao(C(p.Codigo), T(p.Nome),
                    (p.PodemJogar ?? Array.Empty<short>()).Distinct().OrderBy(id => id).ToArray()))
                .ToList(),
            (catalogo.Formacoes ?? Array.Empty<FormacaoDaEscalacao>())
                .Select(f => new FormacaoDaEscalacao(T(f.Nome), f.Ativa,
                    (f.Lugares ?? Array.Empty<LugarDaFormacao>())
                        .Select(l => new LugarDaFormacao(C(l.Codigo), T(l.Nome), C(l.Posicao)))
                        .ToList()))
                .ToList());
    }

    /// <summary>Os problemas do catálogo (já normalizado), em frases para mostrar ao admin. Vazio = pode usar.</summary>
    public static IReadOnlyList<string> Validar(CatalogoEscalacao catalogo)
    {
        var erros = new List<string>();
        var posicoes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (catalogo.Posicoes.Count == 0)
            erros.Add("Cadastre pelo menos uma posição.");

        foreach (var p in catalogo.Posicoes)
        {
            var nome = string.IsNullOrEmpty(p.Codigo) ? "Uma posição" : $"A posição {p.Codigo}";
            if (string.IsNullOrEmpty(p.Codigo)) erros.Add("Toda posição precisa de um código.");
            else if (p.Codigo.Length > MaxCodigoPosicao) erros.Add($"{nome}: o código vai até {MaxCodigoPosicao} letras.");
            else if (!posicoes.Add(p.Codigo)) erros.Add($"O código de posição {p.Codigo} aparece mais de uma vez.");

            if (string.IsNullOrEmpty(p.Nome)) erros.Add($"{nome} precisa de um nome.");
            else if (p.Nome.Length > MaxNomePosicao) erros.Add($"{nome}: o nome vai até {MaxNomePosicao} letras.");

            if (p.PodemJogar.Count == 0) erros.Add($"{nome}: marque quem pode jogar nela.");
            if (p.PodemJogar.Any(id => !SiglasPes.ContainsKey(id))) erros.Add($"{nome}: posição do PES inválida.");
        }

        var formacoes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!catalogo.Formacoes.Any(f => f.Ativa))
            erros.Add("Deixe pelo menos uma formação ativa.");

        foreach (var f in catalogo.Formacoes)
        {
            var nome = string.IsNullOrEmpty(f.Nome) ? "Uma formação" : $"A formação {f.Nome}";
            if (string.IsNullOrEmpty(f.Nome)) erros.Add("Toda formação precisa de um nome.");
            else if (f.Nome.Length > MaxNomeFormacao) erros.Add($"{nome}: o nome vai até {MaxNomeFormacao} caracteres.");
            else if (!formacoes.Add(f.Nome)) erros.Add($"Há duas formações chamadas {f.Nome}.");

            if (f.Lugares.Count != TitularesPorFormacao)
                erros.Add($"{nome} tem {f.Lugares.Count} lugares; precisa de {TitularesPorFormacao}.");
            if (f.Lugares.Count(l => l.Codigo == "GK") != 1)
                erros.Add($"{nome} precisa de exatamente um lugar com o código GK (o goleiro).");

            var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in f.Lugares)
            {
                if (string.IsNullOrEmpty(l.Codigo)) erros.Add($"{nome}: todo lugar precisa de um código.");
                else if (l.Codigo.Length > MaxCodigoLugar) erros.Add($"{nome}: o código {l.Codigo} passa de {MaxCodigoLugar} letras.");
                else if (l.Codigo.StartsWith("SUB", StringComparison.OrdinalIgnoreCase)) erros.Add($"{nome}: o código {l.Codigo} é reservado para os reservas.");
                else if (!codigos.Add(l.Codigo)) erros.Add($"{nome}: o código {l.Codigo} aparece em dois lugares.");

                if (string.IsNullOrEmpty(l.Nome)) erros.Add($"{nome}: o lugar {l.Codigo} precisa de um nome.");
                else if (l.Nome.Length > MaxNomeLugar) erros.Add($"{nome}: o nome do lugar {l.Codigo} passa de {MaxNomeLugar} letras.");

                if (!posicoes.Contains(l.Posicao))
                    erros.Add($"{nome}: o lugar {l.Codigo} usa a posição {(string.IsNullOrEmpty(l.Posicao) ? "(nenhuma)" : l.Posicao)}, que não existe.");
            }
        }

        var padrao = catalogo.Formacoes.FirstOrDefault(f => string.Equals(f.Nome, catalogo.Padrao, StringComparison.OrdinalIgnoreCase));
        if (padrao is null) erros.Add("Escolha a formação padrão (a das escalações novas).");
        else if (!padrao.Ativa) erros.Add($"A formação padrão ({padrao.Nome}) precisa estar ativa.");

        return erros.Distinct().ToList();
    }

    private sealed class Snapshot
    {
        public Snapshot(CatalogoEscalacao catalogo)
        {
            Catalogo = catalogo;
            var posicoes = catalogo.Posicoes.ToDictionary(p => p.Codigo, StringComparer.OrdinalIgnoreCase);
            Templates = catalogo.Formacoes.ToDictionary(
                f => f.Nome,
                f => new LineupTemplate(
                    f.Nome,
                    f.Lugares.Select((l, i) => new LineupSlotTemplate(
                        l.Codigo, l.Nome, false, i + 1, posicoes[l.Posicao].PodemJogar.ToArray())).ToList(),
                    CreateBenchTemplate()),
                StringComparer.OrdinalIgnoreCase);
            Padrao = Templates[catalogo.Padrao];
        }

        public CatalogoEscalacao Catalogo { get; }
        public IReadOnlyDictionary<string, LineupTemplate> Templates { get; }
        public LineupTemplate Padrao { get; }
    }

    private static List<LineupSlotTemplate> CreateBenchTemplate()
        => Enumerable.Range(1, ReservasPorFormacao)
            .Select(i => new LineupSlotTemplate($"SUB{i}", $"Reserva {i}", true, i, Array.Empty<short>()))
            .ToList();
}

public sealed class LineupTemplate
{
    private readonly IReadOnlyDictionary<string, LineupSlotTemplate> _slotsByCode;

    public LineupTemplate(string formation, IReadOnlyList<LineupSlotTemplate> starters, IReadOnlyList<LineupSlotTemplate> bench)
    {
        Formation = formation ?? throw new ArgumentNullException(nameof(formation));
        Starters = new ReadOnlyCollection<LineupSlotTemplate>(starters?.ToList() ?? throw new ArgumentNullException(nameof(starters)));
        Bench = new ReadOnlyCollection<LineupSlotTemplate>(bench?.ToList() ?? throw new ArgumentNullException(nameof(bench)));
        _slotsByCode = Starters.Concat(Bench).ToDictionary(s => s.SlotCode, StringComparer.OrdinalIgnoreCase);
    }

    public string Formation { get; }

    public IReadOnlyList<LineupSlotTemplate> Starters { get; }

    public IReadOnlyList<LineupSlotTemplate> Bench { get; }

    public IReadOnlyCollection<LineupSlotTemplate> AllSlots => new ReadOnlyCollection<LineupSlotTemplate>(_slotsByCode.Values.ToList());

    public LineupSlotTemplate GetSlot(string slotCode)
    {
        if (string.IsNullOrWhiteSpace(slotCode))
        {
            throw new ArgumentException("Código do slot é obrigatório.", nameof(slotCode));
        }

        return _slotsByCode.TryGetValue(slotCode, out var slot)
            ? slot
            : throw new InvalidOperationException($"Slot '{slotCode}' não pertence à formação '{Formation}'.");
    }
}

public sealed record LineupSlotTemplate(
    string SlotCode,
    string DisplayName,
    bool IsBench,
    int Order,
    IReadOnlyList<short> AllowedPositionIds);
