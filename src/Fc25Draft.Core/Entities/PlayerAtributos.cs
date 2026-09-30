using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

/// <summary>
/// Atributos do jogador no PES 2021 (escala 40–99), com os nomes da versão em português do jogo.
/// Nem todo jogador tem: só os que foram encontrados no banco do PES ou preenchidos à mão.
/// </summary>
public class PlayerAtributos
{
    public int PlayerId { get; set; }

    /// <summary>Id do jogador no banco do PES, quando veio da importação.</summary>
    public int? PesId { get; set; }

    public int? Altura { get; set; }
    public int? Peso { get; set; }
    public PernaBoa? PernaBoa { get; set; }

    // Dados extras do PES (ver HabilidadesPes para os nomes).
    public int? EstiloDeJogo { get; set; }
    /// <summary>Bit i ligado = habilidade S(i+1).</summary>
    public long? Habilidades { get; set; }
    /// <summary>Bit i ligado = estilo de IA P(i+1).</summary>
    public int? EstilosIa { get; set; }
    /// <summary>Notas A/B/C nas 13 posições, na ordem de HabilidadesPes.Posicoes.</summary>
    public string? Posicoes { get; set; }
    public int? Condicao { get; set; }          // 1–8
    public int? ResistenciaLesao { get; set; }  // 1–3
    public int? PeFracoUso { get; set; }        // 1–4
    public int? PeFracoPrecisao { get; set; }   // 1–4

    // Ataque
    public int TalentoOfensivo { get; set; }
    public int ControleDeBola { get; set; }
    public int Drible { get; set; }
    public int ConducaoFirme { get; set; }
    public int PasseRasteiro { get; set; }
    public int PasseAlto { get; set; }
    public int Finalizacao { get; set; }
    public int Cabeceio { get; set; }
    public int BolaParada { get; set; }
    public int Curva { get; set; }

    // Físico
    public int Velocidade { get; set; }
    public int Aceleracao { get; set; }
    public int ForcaDoChute { get; set; }
    public int Impulsao { get; set; }
    public int ContatoFisico { get; set; }
    public int Equilibrio { get; set; }
    public int Resistencia { get; set; }

    // Defesa
    public int TalentoDefensivo { get; set; }
    public int Desarme { get; set; }
    public int Agressividade { get; set; }

    // Goleiro
    public int TalentoDeGoleiro { get; set; }
    public int FirmezaDoGoleiro { get; set; }
    public int AfastamentoDoGoleiro { get; set; }
    public int ReflexosDoGoleiro { get; set; }
    public int AlcanceDoGoleiro { get; set; }

    public Player Player { get; set; } = null!;
}
