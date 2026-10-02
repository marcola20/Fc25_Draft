using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Como o time terminou uma competição da temporada ("3º de 10", "Campeão", "Semifinal"…).</summary>
public record RetrospectivaCompeticaoDto(Guid LigaId, string Nome, TipoCompetition Tipo, string Resultado, bool Campeao, bool EmAndamento);

/// <summary>Jogador em destaque; <c>Valor</c> = gols, assistências ou nota média, conforme o destaque.</summary>
public record RetrospectivaJogadorDto(int PlayerId, string Nome, decimal Valor, int Jogos = 0);

public record RetrospectivaJogoDto(Guid PartidaId, string Adversario, int GolsPro, int GolsContra, bool EmCasa, string Competicao);

public record RetrospectivaTransferenciaDto(int PlayerId, string Nome, decimal Valor, string? OutroTime);

/// <summary>Resumo de uma temporada do time, feito para virar imagem no WhatsApp.</summary>
public record RetrospectivaDto(
    Guid TimeId,
    string TimeNome,
    int Temporada,
    bool EmAndamento,
    string? Tecnico,
    IReadOnlyList<RetrospectivaCompeticaoDto> Competicoes,
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra,
    RetrospectivaJogadorDto? Artilheiro,
    RetrospectivaJogadorDto? Garcom,
    RetrospectivaJogadorDto? MelhorNota,
    RetrospectivaJogoDto? MaiorVitoria,
    RetrospectivaJogoDto? PiorDerrota,
    RetrospectivaTransferenciaDto? MaiorContratacao,
    RetrospectivaTransferenciaDto? MaiorVenda,
    int Chegadas,
    int Saidas,
    decimal Gasto,
    decimal Recebido)
{
    /// <summary>Pontos ganhos sobre os disputados, em %; nulo sem jogos.</summary>
    public decimal? Aproveitamento => Jogos == 0 ? null : Math.Round((3m * Vitorias + Empates) * 100 / (3m * Jogos), 1);
}

/// <summary>Retrospectiva do time por temporada (aba da página do time).</summary>
public interface IRetrospectivaService
{
    /// <summary>Temporadas em que o time jogou alguma competição, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<int>> TemporadasAsync(Guid timeId, CancellationToken ct);

    Task<RetrospectivaDto?> MontarAsync(Guid timeId, int temporada, CancellationToken ct);
}
