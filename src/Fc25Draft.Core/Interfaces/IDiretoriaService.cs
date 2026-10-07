using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Diretoria do clube: meta de cada time na temporada (histórico + elenco), confiança que sobe e
/// desce a cada jogo e bônus no caixa para quem cumpre ou supera a meta.
/// </summary>
public interface IDiretoriaService
{
    /// <summary>Temporada mais recente com liga cadastrada.</summary>
    Task<int?> GetTemporadaAtualAsync(CancellationToken ct);

    /// <summary>Todos os times da temporada (nulo = a atual); nulo quando não há temporada.</summary>
    Task<DiretoriaPainelDto?> GetPainelAsync(int? temporada, CancellationToken ct);

    /// <summary>Diretoria de um time na temporada atual; nulo quando ele não joga nenhuma competição dela.</summary>
    Task<DiretoriaTimeDto?> GetTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Notícias do Plantão: mudanças de faixa da confiança.</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);

    /// <summary>
    /// Avisa (uma vez por jogo) o time que caiu para "pressionado" ou "cadeira balançando" num jogo recente;
    /// o aviso aparece no sino e vira notificação no celular. Retorna quantos.
    /// </summary>
    Task<int> AvisarMudancasDeFaixaAsync(CancellationToken ct);

    /// <summary>Calcula e grava as metas da temporada, trocando as que já existiam.</summary>
    Task<DiretoriaPainelDto> GerarMetasAsync(int temporada, CancellationToken ct);

    /// <summary>Muda a meta de um time (liga: posição; Copa: fase, nula = sem meta).</summary>
    Task AjustarMetaAsync(Guid metaId, int? metaPosicao, FasePremiacao? metaFase, CancellationToken ct);

    /// <summary>Bônus de cada competição da temporada.</summary>
    Task<IReadOnlyList<DiretoriaBonusPreviaDto>> ListBonusAsync(int temporada, CancellationToken ct);

    /// <summary>Credita o bônus da competição no caixa dos times.</summary>
    Task<DiretoriaBonusPreviaDto> PagarBonusAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Desfaz o pagamento do bônus de uma competição.</summary>
    Task EstornarBonusAsync(Guid ligaId, CancellationToken ct);
}
