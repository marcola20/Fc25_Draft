using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IPartidaChatService
{
    /// <summary>As últimas mensagens do jogo, da mais antiga para a mais nova.</summary>
    Task<IReadOnlyList<PartidaChatMensagemDto>> ListarAsync(Guid partidaId, int limite, CancellationToken ct);

    Task<PartidaChatMensagemDto> EnviarAsync(Guid partidaId, Guid treinadorId, string texto, CancellationToken ct);

    Task ApagarAsync(Guid mensagemId, CancellationToken ct);
}
