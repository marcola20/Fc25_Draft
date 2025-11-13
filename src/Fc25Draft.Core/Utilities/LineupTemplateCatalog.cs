using System.Collections.ObjectModel;

namespace Fc25Draft.Core.Utilities;

public static class LineupTemplateCatalog
{
    public const string DefaultFormation = "4-3-3";

    private static readonly LineupTemplate DefaultTemplate = new(
        DefaultFormation,
        new List<LineupSlotTemplate>
        {
            new("GK", "Goleiro (GK)", false, 1, new short[] { 1 }),
            new("LB", "Lateral Esquerdo (LE)", false, 2, new short[] { 2, 3, 4, 5, 6 }),
            new("LCB", "Zagueiro 1", false, 3, new short[] { 2, 3, 4, 5, 6 }),
            new("RCB", "Zagueiro 2", false, 4, new short[] { 2, 3, 4, 5, 6 }),
            new("RB", "Lateral Direito (LD)", false, 5, new short[] { 2, 3, 4, 5, 6 }),
            new("CDM", "Volante (VOL)", false, 6, new short[] { 5, 6 }),
            new("CM", "Meia Central (MC)", false, 7, new short[] { 5, 6, 7 }),
            new("CAM", "Meia Atacante (mei)", false, 8, new short[] { 6, 7, 8, 9, 10 }),
            new("LW", "Ponta Esquerda (PE)", false, 9, new short[] { 7, 8, 9, 10 }),
            new("RW", "Ponta Direita (PD)", false, 10, new short[] { 7, 8, 9, 10 }),
            new("ST", "Atacante (ATA)", false, 11, new short[] { 7, 8, 9, 10 })
        },
        new List<LineupSlotTemplate>
        {
            new("SUB1", "Reserva 1", true, 1, Array.Empty<short>()),
            new("SUB2", "Reserva 2", true, 2, Array.Empty<short>()),
            new("SUB3", "Reserva 3", true, 3, Array.Empty<short>()),
            new("SUB4", "Reserva 4", true, 4, Array.Empty<short>()),
            new("SUB5", "Reserva 5", true, 5, Array.Empty<short>()),
            new("SUB6", "Reserva 6", true, 6, Array.Empty<short>()),
            new("SUB7", "Reserva 7", true, 7, Array.Empty<short>())
        });

    private static readonly IReadOnlyDictionary<string, LineupTemplate> Templates = new Dictionary<string, LineupTemplate>(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultFormation] = DefaultTemplate
    };

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
