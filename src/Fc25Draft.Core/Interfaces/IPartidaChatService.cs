using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Chat do telão. A sala é o id de um jogo ou de um mosaico: o chat descobre qual é, e as mensagens
/// voltam com a sala em <see cref="PartidaChatMensagemDto.PartidaId"/>.
/// </summary>
public interface IPartidaChatService
{
    /// <summary>As últimas mensagens da sala, da mais antiga para a mais nova.</summary>
    Task<IReadOnlyList<PartidaChatMensagemDto>> ListarAsync(Guid salaId, int limite, CancellationToken ct);

    Task<PartidaChatMensagemDto> EnviarAsync(Guid salaId, Guid treinadorId, string texto, CancellationToken ct);

    Task ApagarAsync(Guid mensagemId, CancellationToken ct);
}
