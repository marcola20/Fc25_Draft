namespace Fc25Draft.Core.Interfaces;

/// <summary>Assinatura do navegador (PushSubscription) que o aparelho manda ao ativar as notificações.</summary>
public record InscricaoPushRequest(string Endpoint, string P256dh, string Auth, string? Aparelho);

/// <summary>Notificações no celular (Web Push).</summary>
public interface IPushService
{
    /// <summary>Chave pública VAPID para o navegador assinar (gerada na primeira vez).</summary>
    Task<string> ChavePublicaAsync(CancellationToken ct);

    /// <summary>Liga as notificações deste aparelho para a pessoa do token.</summary>
    Task InscreverAsync(string? token, InscricaoPushRequest inscricao, CancellationToken ct);

    /// <summary>Desliga as notificações deste aparelho.</summary>
    Task CancelarAsync(string? token, string endpoint, CancellationToken ct);

    /// <summary>Se este aparelho já está inscrito para a pessoa do token.</summary>
    Task<bool> InscritoAsync(string? token, string endpoint, CancellationToken ct);

    /// <summary>Manda uma notificação de teste para os aparelhos da pessoa. Retorna quantos receberam.</summary>
    Task<int> EnviarTesteAsync(string? token, CancellationToken ct);

    /// <summary>Envia como notificação os avisos dos times que ainda não foram enviados. Retorna quantos.</summary>
    Task<int> EnviarAvisosPendentesAsync(CancellationToken ct);

    /// <summary>Cria (uma vez) o aviso "é a sua vez" para o time da escolha atual do draft.</summary>
    Task AvisarVezNoDraftAsync(Guid draftId, string draftNome, int escolha, Guid timeId, CancellationToken ct);

    /// <summary>
    /// Avisa (uma vez por leilão) quem deu lance e não está na frente de um leilão que fecha em breve. Retorna quantos.
    /// </summary>
    Task<int> AvisarLeiloesFechandoAsync(CancellationToken ct);

    /// <summary>Lembra quem ainda não palpitou numa rodada do bolão que começa em breve. Retorna quantos.</summary>
    Task<int> LembrarBolaoAsync(CancellationToken ct);

    /// <summary>
    /// Avisa (uma vez por pacote) quem ganhou pacote do álbum por vitória, bolão ou da organização; vários
    /// pacotes da mesma pessoa viram um aviso só. Retorna quantas pessoas foram avisadas.
    /// </summary>
    Task<int> AvisarPacotesGanhosAsync(CancellationToken ct);

    /// <summary>
    /// Avisa (uma vez por selo) quem completou página ou o álbum; vários selos da mesma pessoa viram um
    /// aviso só. Retorna quantas pessoas foram avisadas.
    /// </summary>
    Task<int> AvisarConquistasDoAlbumAsync(CancellationToken ct);
}
