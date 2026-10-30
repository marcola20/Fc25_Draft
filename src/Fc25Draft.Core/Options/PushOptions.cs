namespace Fc25Draft.Core.Options;

public class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>
    /// Desliga o envio das notificações no celular: cada envio vira só uma linha no log. Ligado no
    /// appsettings.Development.json, porque a cópia do banco de produção traz os aparelhos e as chaves
    /// VAPID de verdade, e o site rodando local mandaria notificação para as pessoas da liga.
    /// </summary>
    public bool Desligado { get; set; }
}
