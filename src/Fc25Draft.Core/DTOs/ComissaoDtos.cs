using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.DTOs;

/// <param name="Contratado">O time já contratou esse cargo para a temporada.</param>
public record ComissaoCargoDto(CargoDaComissao Cargo, decimal Preco, bool Contratado);

/// <summary>A comissão técnica do time na temporada em andamento.</summary>
/// <param name="Temporada">Temporada em andamento; nulo se não há.</param>
/// <param name="Vale">A comissão já existe nesta temporada (começa em ComissaoCriterios.ValeAPartirDaTemporada).</param>
/// <param name="ContrataAte">Último dia (Brasília) para contratar nesta temporada.</param>
/// <param name="Caixa">Quanto o time tem livre para gastar agora.</param>
public record ComissaoDoTimeDto(
    int? Temporada,
    bool Vale,
    DateTime? ContrataAte,
    bool Aberta,
    decimal Caixa,
    IReadOnlyList<ComissaoCargoDto> Cargos);
