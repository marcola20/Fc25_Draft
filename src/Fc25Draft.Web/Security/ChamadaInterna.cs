using System.Security.Cryptography;
using System.Text;

namespace Fc25Draft.Web.Security;

/// <summary>
/// As telas chamam a própria API pelo endereço público, então pelo Render todas chegam do mesmo IP.
/// Essas chamadas levam um segredo gerado a cada vez que o site sobe e ficam fora do limite de tentativas
/// (senão um token velho guardado no navegador de alguém bloquearia o site inteiro).
/// </summary>
public static class ChamadaInterna
{
    public const string Cabecalho = "X-Chamada-Interna";

    public static readonly string Segredo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static void Marcar(HttpClient client) =>
        client.DefaultRequestHeaders.TryAddWithoutValidation(Cabecalho, Segredo);

    public static bool EhInterna(HttpRequest request)
    {
        var valor = request.Headers[Cabecalho].FirstOrDefault();
        return valor is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(valor), Encoding.UTF8.GetBytes(Segredo));
    }
}
