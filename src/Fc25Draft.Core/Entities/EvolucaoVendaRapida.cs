namespace Fc25Draft.Core.Entities;

/// <summary>
/// Quanto um jogador sobe de overall na venda rápida, pelo overall que ele tinha (editado em /admin/venda-rapida).
/// Uma linha por overall; sem nenhuma linha salva vale a tabela de fábrica (<c>EvolucaoNaVendaRapida.Fabrica</c>).
/// </summary>
public class EvolucaoVendaRapida
{
    public int Overall { get; set; }

    /// <summary>Menor subida sorteada (0 com o máximo 0 = não evolui).</summary>
    public int Minimo { get; set; }

    /// <summary>Maior subida sorteada.</summary>
    public int Maximo { get; set; }
}
