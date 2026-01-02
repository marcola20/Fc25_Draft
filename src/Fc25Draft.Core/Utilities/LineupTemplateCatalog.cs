using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Fc25Draft.Core.Utilities;

public static class LineupTemplateCatalog
{
    public const string DefaultFormation = "4-3-3";

    private static readonly IReadOnlyDictionary<string, LineupTemplate> Templates = BuildTemplates();

    private static readonly LineupTemplate DefaultTemplate = Templates[DefaultFormation];

    public static LineupTemplate GetTemplateOrDefault(string? formation)
    {
        if (string.IsNullOrWhiteSpace(formation))
        {
            return DefaultTemplate;
        }

        if (Templates.TryGetValue(formation.Trim(), out var template))
        {
            return template;
        }

        throw new InvalidOperationException($"Formação '{formation}' não é suportada.");
    }

    public static IReadOnlyList<string> GetSupportedFormations()
    {
        var formations = Templates.Values
            .Select(t => t.Formation)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Array.AsReadOnly(formations);
    }

    private static IReadOnlyDictionary<string, LineupTemplate> BuildTemplates()
    {
        return new Dictionary<string, LineupTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["4-3-3"] = new LineupTemplate(
                "4-3-3",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM1", "Volante Esquerdo (VOL)", false, 6, new short[] { 5, 6, 7 }),
                    new("CDM2", "Volante Direiro (VOL)", false, 7, new short[] { 5, 6, 7 }),
                    new("CAM", "Meia Atacante (MEI)", false, 8, new short[] { 6, 7, 8, 9, 10 }),
                    new("LW", "Ponta Esquerda (PE)", false, 9, new short[] { 7, 8, 9, 10 }),
                    new("RW", "Ponta Direita (PD)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-3-3 Conservador"] = new LineupTemplate(
                "4-3-3 Conservador",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM", "Volante (VOL)", false, 6, new short[] { 5, 6, 7 }),
                    new("CM1", "Meia Central Esquerdo", false, 7, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central Direito (MC)", false, 8, new short[] { 5, 6, 7 }),
                    new("LW", "Ponta Esquerda (PE)", false, 9, new short[] { 7, 8, 9, 10 }),
                    new("RW", "Ponta Direita (PD)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-2-3-1"] = new LineupTemplate(
                "4-2-3-1",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM1", "Volante Esquerdo (VOL)", false, 6, new short[] { 5, 6 }),
                    new("CDM2", "Volante Direito (VOL)", false, 7, new short[] { 5, 6 }),
                    new("LM", "Meia Esquerda (ME)", false, 8, new short[] { 7, 8, 9, 10 }),
                    new("CAM", "Meia Atacante (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("RM", "Meia Direita (MD)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-2-3-1 (2)"] = new LineupTemplate(
                "4-2-3-1 (2)",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CM1", "Meia Central Esquerdo (MC)", false, 6, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central Direito (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("LM", "Meia Esquerda (ME)", false, 8, new short[] { 7, 8, 9, 10 }),
                    new("CAM", "Meia Atacante (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("RM", "Meia Direita (MD)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-2-3-1 (2)"] = new LineupTemplate(
                "4-2-3-1 (2)",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM1", "Volante Esquerdo (VOL)", false, 6, new short[] { 5, 6 }),
                    new("CDM2", "Volante Direito (VOL)", false, 7, new short[] { 5, 6 }),
                    new("CAM1", "Meia Atacante Esquerdo (MEI)", false, 8, new short[] { 7, 8, 9, 10 }),
                    new("CAM2", "Meia Atacante (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("CAM3", "Meia Atacante Direito (MEI)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-2-2-2"] = new LineupTemplate(
                "4-2-2-2",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM1", "Volante Esquerdo (VOL)", false, 6, new short[] { 5, 6 }),
                    new("CDM2", "Volante Direito (VOL)", false, 7, new short[] { 5, 6 }),
                    new("LAM", "Meia Atacante Esquerdo (MEI)", false, 8, new short[] { 6, 7, 8, 9, 10 }),
                    new("RAM", "Meia Atacante Direito (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("ST1", "Atacante Esquerdo (ATA)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST2", "Atacante Direito (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-2-2-2 (2)"] = new LineupTemplate(
                "4-2-2-2 (2)",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM1", "Volante Esquerdo (VOL)", false, 6, new short[] { 5, 6 }),
                    new("CDM2", "Volante Direito (VOL)", false, 7, new short[] { 5, 6 }),
                    new("LM", "Meia Esquerdo (ME)", false, 8, [ 7, 8, 9, 10]),
                    new("RM", "Meia Direito (MD)", false, 9, new short[] { 7, 8, 9, 10 }),
                    new("ST1", "Atacante Esquerdo (ATA)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST2", "Atacante Direito (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-1-2-1-2"] = new LineupTemplate(
                "4-1-2-1-2",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM", "Volante (VOL)", false, 6, new short[] { 5, 6 }),
                    new("LCM", "Meia Central Esquerdo (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("RCM", "Meia Central Direito (MC)", false, 8, new short[] { 5, 6, 7 }),
                    new("CAM", "Meia Atacante (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("ST1", "Atacante Esquerdo (ATA)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST2", "Atacante Direito (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["3-4-2-1"] = new LineupTemplate(
                "3-4-2-1",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LCB", "Zagueiro Esquerdo", false, 2, new short[] { 2, 3, 4, 5 }),
                    new("CCB", "Zagueiro Central", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro Direito", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("LM", "Meia Esquerda (ME)", false, 5, new short[] { 3, 4, 7, 8, 9, 10 }),
                    new("RM", "Meia Direita (MD)", false, 6, new short[] {  3, 4, 7, 8, 9, 10 }),
                    new("CM1", "Meia Central Esquerdo (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central Direito (MC)", false, 8, new short[] { 5, 6, 7 }),
                    new("CAM1", "Meia Atacante Esquerdo (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("CAM2", "Meia Atacante Direito (MEI)", false, 10, new short[] { 6, 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["5-3-2"] = new LineupTemplate(
                "5-3-2",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("ADE", "Ala Esquerdo (AE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro Esquerdo", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("CCB", "Zagueiro Central", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro Direito", false, 5, new short[] { 2, 3, 4, 5 }),
                    new("ADD", "Ala Direito (AD)", false, 6, new short[] { 2, 3, 4, 5, 9 }),
                    new("CM1", "Meia Central Esquerdo (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central (MC)", false, 8, new short[] { 5, 6, 7 }),
                    new("CM3", "Meia Central Direito (MC)", false, 9, new short[] { 5, 6, 7 }),
                    new("ST1", "Atacante Esquerdo (ATA)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST2", "Atacante Direito (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-3-1-2"] = new LineupTemplate(
                "4-3-1-2",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CM1", "Meia Central Esquerdo (MC)", false, 6, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("CM3", "Meia Central Direiro (MC)", false, 8, new short[] { 5, 6, 7 }),
                    new("CAM", "Meia Atacante (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("ST1", "Atacante Esquerdo (ATA)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST2", "Atacante Direito (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-2-1-3"] = new LineupTemplate(
                "4-2-1-3",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CDM1", "Volante Esquerdo (VOL)", false, 6, new short[] { 5, 6 }),
                    new("CDM2", "Volante Direito (VOL)", false, 7, new short[] { 5, 6 }),
                    new("CAM", "Meia Atacante (MEI)", false, 8, new short[] { 6, 7, 8, 9, 10 }),
                    new("LW", "Ponta Esquerda (PE)", false, 9, new short[] { 7, 8, 9, 10 }),
                    new("RW", "Ponta Direita (PD)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-4-1-1"] = new LineupTemplate(
                "4-4-1-1",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CM1", "Meia Central Esquerdo (MC)", false, 6, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central Direito (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("CAM", "Meia Atacante (MEI)", false, 8, new short[] { 6, 7, 8, 9, 10 }),
                    new("LM", "Meia Esquerda (ME)", false, 9, new short[] { 7, 8, 9, 10 }),
                    new("RD", "Meia Direita (MD)", false, 10, new short[] { 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate()),
            ["4-3-2-1"] = new LineupTemplate(
                "4-3-2-1",
                new List<LineupSlotTemplate>
                {
                    new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
                    new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 8 }),
                    new("LCB", "Zagueiro (E)", false, 3, new short[] { 2, 3, 4, 5 }),
                    new("RCB", "Zagueiro (D)", false, 4, new short[] { 2, 3, 4, 5 }),
                    new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 9 }),
                    new("CM1", "Meia Central Esquerdo (MC)", false, 6, new short[] { 5, 6, 7 }),
                    new("CM2", "Meia Central (MC)", false, 7, new short[] { 5, 6, 7 }),
                    new("CM3", "Meia Central Direito (MC)", false, 8, new short[] { 5, 6, 7 }),
                    new("CAM1", "Meia Atacante Esquerdo (MEI)", false, 9, new short[] { 6, 7, 8, 9, 10 }),
                    new("CAM2", "Meia Atacante Direito (MEI)", false, 10, new short[] { 6, 7, 8, 9, 10 }),
                    new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
                },
                CreateBenchTemplate())
        };
    }

    private static List<LineupSlotTemplate> CreateBenchTemplate()
        => new()
        {
            new("SUB1", "Reserva 1", true, 1, Array.Empty<short>()),
            new("SUB2", "Reserva 2", true, 2, Array.Empty<short>()),
            new("SUB3", "Reserva 3", true, 3, Array.Empty<short>()),
            new("SUB4", "Reserva 4", true, 4, Array.Empty<short>()),
            new("SUB5", "Reserva 5", true, 5, Array.Empty<short>()),
            new("SUB6", "Reserva 6", true, 6, Array.Empty<short>()),
            new("SUB7", "Reserva 7", true, 7, Array.Empty<short>())
        };
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
