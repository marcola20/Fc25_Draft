using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>O que o telão de um jogo precisa para montar a tela.</summary>
public record TelaoJogoDto(
    Guid PartidaId,
    Guid LigaId,
    string LigaNome,
    TipoCompetition Tipo,
    /// <summary>"Rodada 5", "Semifinal 1", "Final"...</summary>
    string Rotulo,
    Guid TimeCasaId,
    string TimeCasaNome,
    Guid TimeForaId,
    string TimeForaNome,
    PartidaStatus Status,
    int GolsCasa,
    int GolsFora,
    bool TemPenaltis,
    string? PenaltisVencedorNome,
    DateTime? DataHora,
    string? YoutubeVideoId);

public record PartidaChatMensagemDto(
    Guid MensagemId,
    Guid PartidaId,
    Guid TreinadorId,
    string Autor,
    string? TimeNome,
    string Texto,
    DateTime EnviadaEm);
