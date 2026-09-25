using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Uma passagem do treinador por um clube.</summary>
public record TreinadorPassagemDto(
    Guid PassagemId,
    Guid TimeId,
    string TimeNome,
    PapelTreinador Papel,
    DateTime Desde,
    DateTime? Ate)
{
    public bool Atual => Ate is null;
}

/// <summary>O treinador com as passagens dele, da mais recente para a mais antiga.</summary>
public record TreinadorDto(
    Guid TreinadorId,
    string Nome,
    string Token,
    bool Ativo,
    IReadOnlyList<TreinadorPassagemDto> Passagens)
{
    public TreinadorPassagemDto? Hoje => Passagens.FirstOrDefault(p => p.Atual);
    public string TimeAtual => Hoje?.TimeNome ?? "Sem time";
}

/// <summary>Uma temporada na carreira do treinador: onde estava e como foi.</summary>
public record TreinadorTemporadaDto(
    int Temporada,
    Guid TimeId,
    string TimeNome,
    PapelTreinador Papel,
    string Competicao,
    int? Posicao,
    int? TotalTimes,
    bool Campeao,
    string? Movimento);

/// <summary>A carreira inteira: passagens, temporadas e o que ganhou.</summary>
public record TreinadorCarreiraDto(
    Guid TreinadorId,
    string Nome,
    bool Ativo,
    IReadOnlyList<TreinadorPassagemDto> Passagens,
    IReadOnlyList<TreinadorTemporadaDto> Temporadas,
    IReadOnlyList<string> Titulos)
{
    public int TotalDeTitulos => Titulos.Count;
    public IReadOnlyList<string> Clubes => Passagens.Select(p => p.TimeNome).Distinct().ToArray();
}

public record TreinadorSalvarRequest(Guid? TreinadorId, string Nome, string? Token, bool Ativo = true);

public record TreinadorPassagemRequest(Guid TreinadorId, Guid TimeId, PapelTreinador Papel, DateTime Desde, DateTime? Ate);
