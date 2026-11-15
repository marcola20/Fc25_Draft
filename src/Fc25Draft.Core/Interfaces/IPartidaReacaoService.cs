using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Emojis do telão. A sala é o id de um jogo ou de um mosaico.</summary>
public interface IPartidaReacaoService
{
    /// <summary>Os emojis que o jogo recebeu, na ordem dos botões; <paramref name="treinadorId"/> marca os seus.</summary>
    Task<IReadOnlyList<PartidaReacaoDto>> ListarAsync(Guid salaId, Guid? treinadorId, CancellationToken ct);

    /// <summary>Marca o emoji do treinador no jogo, ou tira se já estava marcado.</summary>
    Task AlternarAsync(Guid salaId, Guid treinadorId, string emoji, CancellationToken ct);
}
