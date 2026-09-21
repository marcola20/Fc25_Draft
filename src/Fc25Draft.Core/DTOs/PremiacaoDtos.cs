using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.DTOs;

public record PremiacaoItemDto(
    Guid ItemId,
    TipoCompetition Tipo,
    Divisao? Divisao,
    int? PosicaoDe,
    int? PosicaoAte,
    FasePremiacao? Fase,
    decimal Valor)
{
    public string Rotulo => Fase is FasePremiacao f
        ? PremiacaoLabels.Fase(f)
        : PremiacaoLabels.Posicao(PosicaoDe, PosicaoAte);
}

public record PremiacaoDto(
    Guid PremiacaoId,
    int Temporada,
    string Nome,
    DateTime AtualizadoEm,
    IReadOnlyList<PremiacaoItemDto> Itens)
{
    public decimal Total => Itens.Sum(i => i.Valor);

    public IReadOnlyList<PremiacaoItemDto> Da(TipoCompetition tipo, Divisao? divisao) =>
        Itens.Where(i => i.Tipo == tipo && i.Divisao == divisao)
            .OrderBy(i => i.Fase ?? 0)
            .ThenBy(i => i.PosicaoDe ?? 0)
            .ToArray();
}

/// <summary>Cria a premiação de uma temporada, opcionalmente copiando os valores de outra.</summary>
public record PremiacaoCriarRequest(int Temporada, string? Nome, Guid? CopiarDe);

public record PremiacaoItemInput(
    TipoCompetition Tipo,
    Divisao? Divisao,
    int? PosicaoDe,
    int? PosicaoAte,
    FasePremiacao? Fase,
    decimal Valor);

public record PremiacaoLinhaDto(Guid TimeId, string TimeNome, string Motivo, decimal Valor);

/// <summary>Quem recebe o quê numa competição, antes de creditar no caixa.</summary>
public record PremiacaoPreviaDto(
    Guid LigaId,
    string LigaNome,
    int? Temporada,
    TipoCompetition Tipo,
    Divisao? Divisao,
    bool Encerrada,
    bool JaPago,
    DateTime? PagoEm,
    Guid? PremiacaoId,
    string? Impedimento,
    IReadOnlyList<PremiacaoLinhaDto> Linhas)
{
    public decimal Total => Linhas.Sum(l => l.Valor);

    /// <summary>Só dá para pagar competição encerrada, com premiação cadastrada e ainda não paga.</summary>
    public bool PodePagar => Encerrada && !JaPago && Impedimento is null && Linhas.Count > 0;
}
