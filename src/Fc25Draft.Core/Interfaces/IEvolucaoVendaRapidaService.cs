using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>A tabela de quanto cada overall sobe na venda rápida (/admin/venda-rapida).</summary>
public interface IEvolucaoVendaRapidaService
{
    /// <summary>A tabela que vale agora (a salva ou, sem nada salvo, a de fábrica).</summary>
    Task<TabelaEvolucaoVendaRapidaDto> ObterAsync(CancellationToken ct);

    /// <summary>Troca a tabela inteira pela informada.</summary>
    Task SalvarAsync(IReadOnlyList<EvolucaoVendaRapidaDto> linhas, CancellationToken ct);

    /// <summary>Apaga a tabela salva: volta a valer a de fábrica.</summary>
    Task VoltarParaFabricaAsync(CancellationToken ct);
}
