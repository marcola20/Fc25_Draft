namespace Fc25Draft.Core.Entities;

/// <summary>
/// Chaves VAPID do site (identificam o servidor para os serviços de push). Geradas sozinhas na primeira
/// vez e guardadas aqui; trocar as chaves invalida todas as inscrições.
/// </summary>
public class ChavePush
{
    public int ChavePushId { get; set; }

    /// <summary>Chave pública P-256 não comprimida, em base64url (vai para o navegador).</summary>
    public string PublicKey { get; set; } = null!;

    /// <summary>Chave privada P-256 em base64url (fica só no servidor).</summary>
    public string PrivateKey { get; set; } = null!;

    public DateTime CriadaEm { get; set; }
}
