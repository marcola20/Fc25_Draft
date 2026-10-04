using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Perfil do treinador: apelido, frase, esquema favorito e foto, montados pela própria pessoa.</summary>
public interface IPerfilTreinadorService
{
    /// <summary>O perfil da pessoa (só o nome, se ela ainda não montou nada); nulo se a pessoa não existe.</summary>
    Task<PerfilTreinadorDto?> GetAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>Os perfis de várias pessoas de uma vez, para listas (chat, rankings). Quem não existe fica de fora.</summary>
    Task<IReadOnlyDictionary<Guid, PerfilTreinadorDto>> VariosAsync(IEnumerable<Guid> treinadorIds, CancellationToken ct);

    /// <summary>Técnico e auxiliar com passagem aberta no clube, com o perfil.</summary>
    Task<IReadOnlyList<ComissaoTecnicaDto>> ComissaoTecnicaAsync(Guid teamId, CancellationToken ct);

    /// <summary>Grava apelido, frase e esquema (o esquema tem de ser da lista fixa).</summary>
    Task<PerfilTreinadorDto> SalvarAsync(Guid treinadorId, PerfilSalvarRequest request, CancellationToken ct);

    /// <summary>Troca a foto da pessoa (WebP, PNG ou JPEG, até 300 KB).</summary>
    Task<PerfilTreinadorDto> TrocarFotoAsync(Guid treinadorId, byte[] imagem, CancellationToken ct);

    /// <summary>A própria pessoa tira a foto.</summary>
    Task<PerfilTreinadorDto> RemoverFotoAsync(Guid treinadorId, CancellationToken ct);

    Task<FotoTreinadorArquivo?> ObterFotoAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>O admin apaga a foto, o apelido ou a frase de alguém. Fica no Log de Ações.</summary>
    Task<PerfilTreinadorDto> ApagarPeloAdminAsync(Guid treinadorId, CampoDoPerfil campo, string? adminToken, CancellationToken ct);
}
