namespace Fc25Draft.Core.DTOs;

/// <summary>Um administrador da liga, com o token que ele usa para entrar.</summary>
public record AdministradorDto(
    Guid AdminTokenId,
    string Nome,
    string Token,
    bool Ativo,
    bool Principal,
    Guid? TreinadorId,
    string? TreinadorNome,
    DateTime CriadoEmUtc,
    DateTime? UltimoUsoUtc);

/// <summary>Cria (sem id) ou atualiza um administrador. Sem token informado, gera um.</summary>
public record AdministradorSalvarRequest(
    Guid? AdminTokenId,
    string Nome,
    string? Token,
    bool Ativo,
    Guid? TreinadorId);
