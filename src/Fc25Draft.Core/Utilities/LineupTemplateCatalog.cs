using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Fc25Draft.Core.Utilities;

// Position IDs (PES 2021):
//  1=GOL  2=ZAG  3=LE   4=LD   5=VOL
//  6=MLG  7=MAT  8=ME   9=PE   10=MD  11=PD  12=CA  13=SA

public static class LineupTemplateCatalog
{
    public const string DefaultFormation = "4-3-3 (4-2-1-3)";

    private static readonly short[] GOL  = { 1 };
    private static readonly short[] ZAG  = { 2, 3, 4, 5 };
    private static readonly short[] LE   = { 2, 3, 4, 5, 8, 9 };
    private static readonly short[] LD   = { 2, 3, 4, 5, 10, 11 };
    private static readonly short[] VOL  = { 3, 4, 5, 6, 7 };
    private static readonly short[] MLG  = { 5, 6, 7, 8, 9, 10, 11 };
    private static readonly short[] ME   = { 7, 8, 9 };
    private static readonly short[] MD   = { 7, 10, 11 };
    private static readonly short[] MAT  = { 6, 7, 8, 9, 10, 11, 12, 13 };
    private static readonly short[] PE   = { 7, 8, 9, 10, 11, 13 };
    private static readonly short[] PD   = { 7, 8, 9, 10, 11, 13 };
    private static readonly short[] CA   = { 7, 9, 11, 12, 13 };
    private static readonly short[] SA   = { 7, 8, 9, 10, 11, 12, 13 };

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
            ["4-4-2 (4-2-2-2) Padrão"] = new LineupTemplate(
                "4-4-2 (4-2-2-2) Padrão",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",        false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                 false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",         false,  5, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",       false,  6, VOL),
                    new("CDM2", "Volante Direito (VOL)",        false,  7, VOL),
                    new("LM",   "Meia Esquerda (ME)",           false,  8, ME),
                    new("RM",   "Meia Direita (MD)",            false,  9, MD),
                    new("ST1",  "Centroavante (CA)",            false, 10, CA),
                    new("ST2",  "Centroavante (CA)",            false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-5-1 (4-2-3-1)"] = new LineupTemplate(
                "4-5-1 (4-2-3-1)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",        false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                 false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",         false,  5, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",       false,  6, VOL),
                    new("CDM2", "Volante Direito (VOL)",        false,  7, VOL),
                    new("LM",   "Meia Esquerda (ME)",           false,  8, ME),
                    new("RM",   "Meia Direita (MD)",            false,  9, MD),
                    new("CAM",  "Meia Atacante (MAT)",          false, 10, MAT),
                    new("ST",   "Centroavante (CA)",            false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-5-1 (4-1-4-1)"] = new LineupTemplate(
                "4-5-1 (4-1-4-1)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("LM",   "Meia Esquerda (ME)",               false,  7, ME),
                    new("RM",   "Meia Direita (MD)",                false,  8, MD),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)",     false,  9, MAT),
                    new("CAM2", "Meia Atacante Direito (MAT)",      false, 10, MAT),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-5-1 (4-3-2-1)"] = new LineupTemplate(
                "4-5-1 (4-3-2-1)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  7, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  8, MLG),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)",     false,  9, MAT),
                    new("CAM2", "Meia Atacante Direito (MAT)",      false, 10, MAT),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-4-2 (4-2-2-2)"] = new LineupTemplate(
                "4-4-2 (4-2-2-2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  6, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  7, MLG),
                    new("LM",   "Meia Esquerda (ME)",               false,  8, ME),
                    new("RM",   "Meia Direita (MD)",                false,  9, MD),
                    new("ST1",  "Segundo Atacante (SA)",            false, 10, SA),
                    new("ST2",  "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-4-2 (4-3-1-2)"] = new LineupTemplate(
                "4-4-2 (4-3-1-2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  7, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  8, MLG),
                    new("CAM",  "Meia Atacante (MAT)",              false,  9, MAT),
                    new("ST1",  "Segundo Atacante (SA)",            false, 10, SA),
                    new("ST2",  "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (4-2-1-3)"] = new LineupTemplate(
                "4-3-3 (4-2-1-3)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",               false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",       false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",        false,  5, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",      false,  6, VOL),
                    new("CDM2", "Volante Direito (VOL)",       false,  7, VOL),
                    new("CAM",  "Meia Atacante (MAT)",         false,  8, MAT),
                    new("LW",   "Ponta Esquerda (PE)",         false,  9, PE),
                    new("RW",   "Ponta Direita (PD)",          false, 10, PD),
                    new("ST",   "Centroavante (CA)",           false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (4-1-2-3)"] = new LineupTemplate(
                "4-3-3 (4-1-2-3)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)",     false,  7, MAT),
                    new("CAM2", "Meia Atacante Direito (MAT)",      false,  8, MAT),
                    new("LW",   "Ponta Esquerda (PE)",              false,  9, PE),
                    new("RW",   "Ponta Direita (PD)",               false, 10, PD),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["3-6-1 (3-2-4-1)"] = new LineupTemplate(
                "3-6-1 (3-2-4-1)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",                false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",                 false,  4, ZAG),
                    new("CDM1", "Volante Esquerdo (VOL)",           false,  5, VOL),
                    new("CDM2", "Volante Direito (VOL)",            false,  6, VOL),
                    new("LM",   "Meia Esquerda (ME)",               false,  7, ME),
                    new("RM",   "Meia Direita (MD)",                false,  8, MD),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)",     false,  9, MAT),
                    new("CAM2", "Meia Atacante Direito (MAT)",      false, 10, MAT),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["3-5-2 (3-2-3-2)"] = new LineupTemplate(
                "3-5-2 (3-2-3-2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",              false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",          false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",           false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",           false,  4, ZAG),
                    new("CDM1", "Volante Esquerdo (VOL)",     false,  5, VOL),
                    new("CDM2", "Volante Direito (VOL)",      false,  6, VOL),
                    new("LM",   "Meia Esquerda (ME)",         false,  7, ME),
                    new("RM",   "Meia Direita (MD)",          false,  8, MD),
                    new("CAM",  "Meia Atacante (MAT)",        false,  9, MAT),
                    new("ST1",  "Segundo Atacante (SA)",      false, 10, SA),
                    new("ST2",  "Centroavante (CA)",          false, 11, CA)
                },
                CreateBenchTemplate()),

            ["3-5-2 (3-3-2-2)"] = new LineupTemplate(
                "3-5-2 (3-3-2-2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",                false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",                 false,  4, ZAG),
                    new("CDM",  "Volante (VOL)",                    false,  5, VOL),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  6, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  7, MLG),
                    new("LM",   "Meia Esquerda (ME)",               false,  8, ME),
                    new("RM",   "Meia Direita (MD)",                false,  9, MD),
                    new("ST1",  "Segundo Atacante (SA)",            false, 10, SA),
                    new("ST2",  "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["3-4-3 (3-2-2-3)"] = new LineupTemplate(
                "3-4-3 (3-2-2-3)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",                false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",                 false,  4, ZAG),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  5, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  6, MLG),
                    new("LM",   "Meia Esquerda (ME)",               false,  7, ME),
                    new("RM",   "Meia Direita (MD)",                false,  8, MD),
                    new("LW",   "Ponta Esquerda (PE)",              false,  9, PE),
                    new("RW",   "Ponta Direita (PD)",               false, 10, PD),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["5-4-1 (5-2-2-1)"] = new LineupTemplate(
                "5-4-1 (5-2-2-1)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",              false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",          false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",           false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",           false,  4, ZAG),
                    new("ADE",  "Ala Esquerdo (LE)",          false,  5, LE),
                    new("ADD",  "Ala Direito (LD)",           false,  6, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",     false,  7, VOL),
                    new("CDM2", "Volante Direito (VOL)",      false,  8, VOL),
                    new("LM",   "Meia Esquerda (ME)",         false,  9, ME),
                    new("RM",   "Meia Direita (MD)",          false, 10, MD),
                    new("ST",   "Centroavante (CA)",          false, 11, CA)
                },
                CreateBenchTemplate()),

            ["5-3-2 (5-2-1-2)"] = new LineupTemplate(
                "5-3-2 (5-2-1-2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",              false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",          false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",           false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",           false,  4, ZAG),
                    new("ADE",  "Ala Esquerdo (LE)",          false,  5, LE),
                    new("ADD",  "Ala Direito (LD)",           false,  6, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",     false,  7, VOL),
                    new("CDM2", "Volante Direito (VOL)",      false,  8, VOL),
                    new("CAM",  "Meia Atacante (MAT)",        false,  9, MAT),
                    new("ST1",  "Segundo Atacante (SA)",      false, 10, SA),
                    new("ST2",  "Centroavante (CA)",          false, 11, CA)
                },
                CreateBenchTemplate()),

            ["5-3-2 (5-3-2)"] = new LineupTemplate(
                "5-3-2 (5-3-2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LCB",  "Zagueiro Esquerdo",                false,  2, ZAG),
                    new("CCB",  "Zagueiro Central",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro Direito",                 false,  4, ZAG),
                    new("ADE",  "Ala Esquerdo (LE)",                false,  5, LE),
                    new("ADD",  "Ala Direito (LD)",                 false,  6, LD),
                    new("CDM",  "Volante (VOL)",                    false,  7, VOL),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  8, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  9, MLG),
                    new("ST1",  "Segundo Atacante (SA)",            false, 10, SA),
                    new("ST2",  "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-2-1-3 (P)"] = new LineupTemplate(
                "4-2-1-3 (P)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",               false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",       false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",        false,  5, LD),
                    new("CDM",  "Volante (VOL)",               false,  6, VOL),
                    new("CM",   "Meia de Ligação (MLG)",       false,  7, MLG),
                    new("CAM",  "Meia Atacante (MAT)",         false,  8, MAT),
                    new("LW",   "Ponta Esquerda (PE)",         false,  9, PE),
                    new("RW",   "Ponta Direita (PD)",          false, 10, PD),
                    new("ST",   "Centroavante (CA)",           false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-2-2-2 (P)"] = new LineupTemplate(
                "4-2-2-2 (P)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",        false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                 false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                 false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",         false,  5, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",       false,  6, VOL),
                    new("CDM2", "Volante Direito (VOL)",        false,  7, VOL),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)", false,  8, MAT),
                    new("CAM2", "Meia Atacante Direito (MAT)",  false,  9, MAT),
                    new("ST1",  "Centroavante Esquerda (CA)",   false, 10, CA),
                    new("ST2",  "Centroavante Direita (CA)",    false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (P)"] = new LineupTemplate(
                "4-3-3 (P)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  6, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  7, MLG),
                    new("CAM",  "Meia Atacante (MAT)",              false,  8, MAT),
                    new("LW",   "Ponta Esquerda (PE)",              false,  9, PE),
                    new("RW",   "Ponta Direita (PD)",               false, 10, PD),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (P2)"] = new LineupTemplate(
                "4-3-3 (P2)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  6, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  7, MLG),
                    new("CAM",  "Meia Atacante (MAT)",              false,  8, MAT),
                    new("SA",   "Segundo Atacante (SA)",            false,  9, SA),
                    new("RW",   "Ponta Direita (PD)",               false, 10, PD),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (P3)"] = new LineupTemplate(
                "4-3-3 (P3)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  6, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  7, MLG),
                    new("CAM",  "Meia Atacante (MAT)",              false,  8, MAT),
                    new("SA",   "Segundo Atacante (SA)",            false,  9, SA),
                    new("LW",   "Ponta Esquerda (PE)",              false, 10, PE),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-1-2-3 (P)"] = new LineupTemplate(
                "4-1-2-3 (P)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)",     false,  7, MAT),
                    new("CAM2", "Meia Atacante Direito (MAT)",      false,  8, MAT),
                    new("SA",   "Segundo Atacante (SA)",            false,  9, SA),
                    new("RW",   "Ponta Direita (PD)",               false, 10, PD),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-2-3-1 (P)"] = new LineupTemplate(
                "4-2-3-1 (P)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("CM",   "Meia de Ligação (MLG)",            false,  6, MLG),
                    new("CAM1", "Meia Atacante Esquerdo (MAT)",     false,  8, MAT),
                    new("CAM2", "Meia Atacante Central (MAT)",      false,  9, MAT),
                    new("CAM3", "Meia Atacante Direito (MAT)",      false, 10, MAT),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (P4)"] = new LineupTemplate(
                "4-3-3 (P4)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  6, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  7, MLG),
                    new("CAM",  "Meia Atacante (MAT)",              false,  8, MAT),
                    new("SA1",  "Segundo Atacante (SA)",            false,  9, SA),
                    new("SA2",  "Segundo Atacante (SA)",            false,  10, SA),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-3 (P5)"] = new LineupTemplate(
                "4-3-3 (P5)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM1", "Volante Esquerdo (VOL)",           false,  6, VOL),
                    new("CDM2", "Volante Direito (VOL)",            false,  7, VOL),
                    new("CAM",  "Meia Atacante (MAT)",              false,  8, MAT),
                    new("SA1",  "Segundo Atacante (SA)",            false,  9, SA),
                    new("SA2",  "Segundo Atacante (SA)",            false, 10, SA),
                    new("ST",   "Centroavante (CA)",                false, 11, CA)
                },
                CreateBenchTemplate()),

            ["4-3-1-2 (P)"] = new LineupTemplate(
                "4-3-1-2 (P)",
                new List<LineupSlotTemplate>
                {
                    new("GK",   "Goleiro (GOL)",                    false,  1, GOL),
                    new("LB",   "Lateral Esquerdo (LE)",            false,  2, LE),
                    new("LCB",  "Zagueiro (E)",                     false,  3, ZAG),
                    new("RCB",  "Zagueiro (D)",                     false,  4, ZAG),
                    new("RB",   "Lateral Direito (LD)",             false,  5, LD),
                    new("CDM",  "Volante (VOL)",                    false,  6, VOL),
                    new("CM1",  "Meia de Ligação Esquerdo (MLG)",   false,  7, MLG),
                    new("CM2",  "Meia de Ligação Direito (MLG)",    false,  8, MLG),
                    new("CAM",  "Meia Atacante (MAT)",              false,  9, MAT),
                    new("ST1",  "Centroavante Esquerdo (CA)",       false, 10, SA),
                    new("ST2",  "Centroavante Direito (CA)",        false, 11, CA)
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
