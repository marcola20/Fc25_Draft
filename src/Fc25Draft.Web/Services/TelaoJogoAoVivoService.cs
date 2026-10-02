using System.Collections.Concurrent;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Singleton que liga as telas abertas no telão de um jogo: repassa as mensagens do chat
/// na hora e conta quantas pessoas estão assistindo. As mensagens em si ficam no banco.
/// </summary>
public sealed class TelaoJogoAoVivoService
{
    private readonly ConcurrentDictionary<Guid, int> _assistindo = new();

    /// <summary>Mensagem nova num jogo.</summary>
    public event Action<PartidaChatMensagemDto>? MensagemEnviada;

    /// <summary>Mensagem apagada pelo admin (jogo, mensagem).</summary>
    public event Action<Guid, Guid>? MensagemApagada;

    /// <summary>Alguém entrou ou saiu do telão do jogo.</summary>
    public event Action<Guid>? PublicoMudou;

    /// <summary>Alguém marcou ou tirou um emoji de reação do jogo.</summary>
    public event Action<Guid>? ReacoesMudaram;

    public int Assistindo(Guid partidaId) => _assistindo.GetValueOrDefault(partidaId);

    public void Entrar(Guid partidaId)
    {
        _assistindo.AddOrUpdate(partidaId, 1, (_, n) => n + 1);
        PublicoMudou?.Invoke(partidaId);
    }

    public void Sair(Guid partidaId)
    {
        _assistindo.AddOrUpdate(partidaId, 0, (_, n) => Math.Max(0, n - 1));
        PublicoMudou?.Invoke(partidaId);
    }

    public void Publicar(PartidaChatMensagemDto mensagem) => MensagemEnviada?.Invoke(mensagem);

    public void Apagar(Guid partidaId, Guid mensagemId) => MensagemApagada?.Invoke(partidaId, mensagemId);

    public void AvisarReacoes(Guid partidaId) => ReacoesMudaram?.Invoke(partidaId);
}
