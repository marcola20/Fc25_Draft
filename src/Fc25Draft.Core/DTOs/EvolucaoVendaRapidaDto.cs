namespace Fc25Draft.Core.DTOs;

/// <summary>Uma linha da tabela de evolução da venda rápida: o overall e quanto ele pode subir.</summary>
public record EvolucaoVendaRapidaDto(int Overall, int Minimo, int Maximo);

/// <summary>A tabela que vale agora.</summary>
/// <param name="Fabrica">true = nada salvo no banco, vale a tabela de fábrica.</param>
public record TabelaEvolucaoVendaRapidaDto(IReadOnlyList<EvolucaoVendaRapidaDto> Linhas, bool Fabrica);
