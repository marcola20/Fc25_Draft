using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Um ídolo de um clube. <see cref="NoClube"/>: ainda está lá (emprestado pelo clube também conta).
/// Ídolo aposentado é lenda do clube (<see cref="Lenda"/>); se se aposentou nele, <see cref="CamisaAposentada"/> é o
/// número que ninguém mais usa lá.
/// </summary>
public record IdoloDto(
    Guid IdoloId,
    int JogadorId,
    string JogadorNome,
    string Posicao,
    int Overall,
    Guid TimeId,
    string TimeNome,
    int Jogos,
    int Gols,
    int Assistencias,
    int Titulos,
    IReadOnlyList<string> Motivos,
    DateTime ReconhecidoEm,
    bool NoClube,
    bool Aposentado,
    string? TimeAtualNome,
    DateTime? SaiuEm,
    SaidaDoIdolo? Saida,
    bool PerdeuConfianca,
    short? CamisaAposentada = null)
{
    public bool Lenda => Aposentado;
}

/// <summary>Ídolos dos clubes: quem é, por quê, e o que acontece quando um deles sai.</summary>
public interface IIdoloService
{
    /// <summary>
    /// Recalcula os ídolos, grava os novos (com aviso ao clube) e registra quem saiu. Vendido ou trocado depois de
    /// virar ídolo tira confiança da diretoria; quem já tinha saído não. Retorna quantas mudanças gravou.
    /// </summary>
    Task<int> AtualizarAsync(CancellationToken ct);

    Task<IReadOnlyList<IdoloDto>> ListarAsync(CancellationToken ct);

    /// <summary>Clubes de que o jogador é ídolo.</summary>
    Task<IReadOnlyList<IdoloDto>> DoJogadorAsync(int jogadorId, CancellationToken ct);

    /// <summary>Os ídolos e lendas do clube, com os números.</summary>
    Task<IReadOnlyList<IdoloDto>> DoClubeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Jogadores que são ídolos do clube.</summary>
    Task<IReadOnlySet<int>> DoTimeAsync(Guid timeId, CancellationToken ct);
}
