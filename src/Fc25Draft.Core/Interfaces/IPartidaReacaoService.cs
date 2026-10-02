using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IPartidaReacaoService
{
    /// <summary>Os emojis que o jogo recebeu, na ordem dos botões; <paramref name="treinadorId"/> marca os seus.</summary>
    Task<IReadOnlyList<PartidaReacaoDto>> ListarAsync(Guid partidaId, Guid? treinadorId, CancellationToken ct);

    /// <summary>Marca o emoji do treinador no jogo, ou tira se já estava marcado.</summary>
    Task AlternarAsync(Guid partidaId, Guid treinadorId, string emoji, CancellationToken ct);
}
