using System;
using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Infra.Services;

public sealed class FormationSlotFactory : IFormationSlotFactory
{
    private static class PositionIds
    {
        public const int Goalkeeper = 1;
        public const int CenterBack = 2;
        public const int LeftBack = 3;
        public const int RightBack = 4;
        public const int DefensiveMidfielder = 5;
        public const int CentralMidfielder = 6;
        public const int AttackingMidfielder = 7;
        public const int LeftWinger = 8;
        public const int RightWinger = 9;
        public const int Striker = 10;
    }

    private static readonly string[] SupportedFormations =
    {
        "4-2-4",
        "4-2-2-2",
        "4-3-3",
        "4-2-3-1",
        "4-4-2"
    };

    public IReadOnlyList<FormationSlotTemplate> Build(string formationCode)
    {
        if (string.IsNullOrWhiteSpace(formationCode))
        {
            throw new ArgumentException("Código de formação inválido.", nameof(formationCode));
        }

        var normalized = formationCode.Trim();
        if (!Supports(normalized))
        {
            throw new ArgumentOutOfRangeException(nameof(formationCode), formationCode, "Formação não suportada.");
        }

        var order = 1;
        var slots = new List<FormationSlotTemplate>(18)
        {
            new(0, order++, PositionIds.Goalkeeper)
        };

        BuildDefensiveLine(slots, ref order);

        switch (normalized)
        {
            case "4-2-4":
                AddMidfieldPair(slots, ref order);
                AddWingers(slots, ref order);
                AddStrikerPair(slots, ref order);
                break;
            case "4-2-2-2":
                AddMidfieldPair(slots, ref order);
                AddAttackingMidfieldPair(slots, ref order);
                AddStrikerPair(slots, ref order);
                break;
            case "4-3-3":
                AddThreeManMidfield(slots, ref order);
                AddWingers(slots, ref order);
                AddStriker(slots, ref order);
                break;
            case "4-2-3-1":
                AddMidfieldPair(slots, ref order);
                AddThreeAttackingMids(slots, ref order);
                AddStriker(slots, ref order);
                break;
            case "4-4-2":
                AddFourMidfield(slots, ref order);
                AddStrikerPair(slots, ref order);
                break;
        }

        AddBench(slots, ref order);

        return slots;
    }

    public bool Supports(string formationCode)
    {
        if (string.IsNullOrWhiteSpace(formationCode))
        {
            return false;
        }

        var normalized = formationCode.Trim();
        return SupportedFormations.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetSupportedFormations() => SupportedFormations;

    private static void BuildDefensiveLine(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.LeftBack));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.CenterBack));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.CenterBack));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.RightBack));
    }

    private static void AddMidfieldPair(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.DefensiveMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.CentralMidfielder));
    }

    private static void AddAttackingMidfieldPair(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.AttackingMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.AttackingMidfielder));
    }

    private static void AddThreeManMidfield(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.DefensiveMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.CentralMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.CentralMidfielder));
    }

    private static void AddThreeAttackingMids(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.LeftWinger));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.AttackingMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.RightWinger));
    }

    private static void AddFourMidfield(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.LeftWinger));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.DefensiveMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.CentralMidfielder));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.RightWinger));
    }

    private static void AddWingers(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.LeftWinger));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.RightWinger));
    }

    private static void AddStrikerPair(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.Striker));
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.Striker));
    }

    private static void AddStriker(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(0, order++, PositionIds.Striker));
    }

    private static void AddBench(ICollection<FormationSlotTemplate> slots, ref int order)
    {
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.Goalkeeper));
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.CenterBack));
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.RightBack));
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.DefensiveMidfielder));
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.CentralMidfielder));
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.LeftWinger));
        slots.Add(new FormationSlotTemplate(1, order++, PositionIds.Striker));
    }
}
