namespace Fc25Draft.Core.Entities;

/// <summary>
/// Marca de notificação já feita (ex.: "draft:{id}:{escolha}", "bolao:{rodada}:{pessoa}"), para não
/// repetir quando o site reinicia ou a checagem roda de novo.
/// </summary>
public class NotificacaoEnviada
{
    public string Chave { get; set; } = null!;
    public DateTime EnviadaEm { get; set; }
}
