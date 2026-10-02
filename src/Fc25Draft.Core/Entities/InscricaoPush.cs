namespace Fc25Draft.Core.Entities;

/// <summary>
/// Um aparelho (navegador ou app instalado) que aceitou receber notificações no celular. É da pessoa,
/// não do time: quem troca de clube passa a receber os avisos do clube novo.
/// </summary>
public class InscricaoPush
{
    public Guid InscricaoId { get; set; }
    public Guid TreinadorId { get; set; }

    /// <summary>Endereço do serviço de push do navegador (Google, Apple, Mozilla…) para este aparelho.</summary>
    public string Endpoint { get; set; } = null!;

    /// <summary>Chaves do aparelho para criptografar a mensagem (base64url).</summary>
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;

    /// <summary>Descrição curta do aparelho, para a pessoa reconhecer na lista.</summary>
    public string? Aparelho { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime? UltimoEnvioEm { get; set; }

    public Treinador Treinador { get; set; } = null!;
}
