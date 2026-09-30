using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record PlayerCreateDto(string Name, int? Age, int Overall, short PositionId);

public record PlayerUpdateDto(string Name, int? Age, int Overall, short PositionId);

public record PlayerListItemDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    string Status,
    string? TeamName);

public record PlayerDetailsDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    string Status,
    string? TeamName,
    Guid? TeamId,
    PlayerAtributosDto? Atributos);

/// <summary>Atributos do PES (40–99). Mutável porque também serve de modelo do formulário.</summary>
public class PlayerAtributosDto
{
    /// <summary>Jogador do PES de onde vieram os dados (nulo = preenchido à mão).</summary>
    public int? PesId { get; set; }

    public int? Altura { get; set; }
    public int? Peso { get; set; }
    public Enums.PernaBoa? PernaBoa { get; set; }

    public int? EstiloDeJogo { get; set; }
    public long? Habilidades { get; set; }
    public int? EstilosIa { get; set; }
    public string? Posicoes { get; set; }
    public int? Condicao { get; set; }
    public int? ResistenciaLesao { get; set; }
    public int? PeFracoUso { get; set; }
    public int? PeFracoPrecisao { get; set; }

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

    public int Velocidade { get; set; }
    public int Aceleracao { get; set; }
    public int ForcaDoChute { get; set; }
    public int Impulsao { get; set; }
    public int ContatoFisico { get; set; }
    public int Equilibrio { get; set; }
    public int Resistencia { get; set; }

    public int TalentoDefensivo { get; set; }
    public int Desarme { get; set; }
    public int Agressividade { get; set; }

    public int TalentoDeGoleiro { get; set; }
    public int FirmezaDoGoleiro { get; set; }
    public int AfastamentoDoGoleiro { get; set; }
    public int ReflexosDoGoleiro { get; set; }
    public int AlcanceDoGoleiro { get; set; }

    /// <summary>Valores iniciais do formulário: 40 é o mínimo do PES.</summary>
    public static PlayerAtributosDto Minimos()
    {
        var dto = new PlayerAtributosDto();
        foreach (var a in Utilities.AtributosPes.Todos) a.Set(dto, 40);
        return dto;
    }
}

/// <summary>Jogador da base do PES, como aparece na busca.</summary>
public record JogadorPesDto(int PesId, string Nome, IReadOnlyList<string> Times, short PositionId, int Idade, PlayerAtributosDto Atributos);

public record PlayerExportDto(
    string Nome,
    string Posicao,
    int Overall,
    string Status,
    string? Time);

public record PlayerImportResultDto(int Imported, IReadOnlyList<string> Errors);
