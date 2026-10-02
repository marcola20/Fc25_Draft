namespace Fc25Draft.Web.Services;

/// <summary>
/// IP de quem abriu o site, pego no pedido da página (_Host) e passado ao circuito pelo App:
/// é a chave do limite de tentativas de login nas telas.
/// </summary>
public class OrigemDoCliente
{
    public string Ip { get; set; } = "desconhecida";
}
