using Fc25Draft.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Core.DTOs;

public record LigaDto(
    Guid LigaId,
    string Nome,
    int TotalRodadas,
    DateTime DataInicio,
    DateTime DataFim,
    LigaStatus Status,
    TipoCompetition Tipo,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    Guid? CampeaoTimeId = null,
    string? CampeaoNome = null);

public record LigaCreateRequest(
    [Required, MaxLength(120)] string Nome,
    DateTime DataInicio,
    DateTime DataFim,
    TipoCompetition Tipo = TipoCompetition.Liga);

public record LigaUpdateRequest(
    [MaxLength(120)] string? Nome,
    DateTime? DataInicio,
    DateTime? DataFim);

public record LigaRodadaDto(
    Guid RodadaId,
    Guid LigaId,
    int Numero,
    int TotalPartidas);

public record LigaRodadaComPartidasDto(
    Guid RodadaId,
    Guid LigaId,
    int Numero,
    IReadOnlyList<LigaPartidaDto> Partidas);

public record LigaPartidaDto(
    Guid PartidaId,
    Guid RodadaId,
    int RodadaNumero,
    Guid TimeCasaId,
    string TimeCasaNome,
    Guid TimeForaId,
    string TimeForaNome,
    int GolsCasa,
    int GolsFora,
    PartidaStatus Status,
    bool IsWO,
    bool TemPenaltis,
    Guid? PenaltisVencedorId,
    DateTime? IniciadaEm,
    DateTime? EncerradaEm);

public record LigaPartidaCreateRequest(
    [Required] Guid TimeCasaId,
    [Required] Guid TimeForaId);

public record LigaEventoDto(
    Guid EventoId,
    Guid PartidaId,
    TipoEvento Tipo,
    Guid TimeId,
    string TimeNome,
    int JogadorId,
    string JogadorNome,
    int? AssistenteId,
    string? AssistenteNome,
    int? Minuto,
    DateTime CriadoEm);

public record LigaGolRequest(
    [Required] Guid TimeId,
    [Required] int JogadorId,
    int? AssistenteId,
    [Range(1, 120)] int? Minuto);

public record LigaCartaoRequest(
    [Required] Guid TimeId,
    [Required] int JogadorId,
    [Required] TipoEvento Tipo,
    [Range(1, 120)] int? Minuto);

public record LigaClassificacaoItemDto(
    int Posicao,
    Guid TimeId,
    string TimeNome,
    int Pontos,
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra,
    int SaldoGols,
    int CartoesAmarelos,
    int CartoesVermelhos,
    int PontosDescontados,
    GrupoCopa? Grupo = null);

public record LigaGrupoTimeDto(
    Guid LigaId,
    Guid TimeId,
    string TimeNome,
    GrupoCopa Grupo);

public record LigaConfigurarGruposRequest(
    IReadOnlyList<Guid> TimesGrupoA,
    IReadOnlyList<Guid> TimesGrupoB);

public record LigaPunicaoDto(
    Guid PunicaoId,
    Guid LigaId,
    Guid TimeId,
    string TimeNome,
    int PontosSubtraidos,
    string Motivo,
    DateTime CriadaEm);

public record LigaPunicaoRequest(
    [Required] Guid TimeId,
    [Range(1, 999)] int PontosSubtraidos,
    [Required, MaxLength(300)] string Motivo);

public record LigaKnockoutJogoDto(
    Guid KnockoutJogoId,
    FaseKnockout Fase,
    string FaseLabel,
    Guid? TimeCasaId,
    string? TimeCasaNome,
    Guid? TimeForaId,
    string? TimeForaNome,
    Guid? VencedorId,
    string? VencedorNome,
    Guid? PartidaId,
    int? GolsCasa,
    int? GolsFora,
    PartidaStatus? PartidaStatus,
    bool TemPenaltis);

public record LigaEncerrarKnockoutRequest(
    bool TemPenaltis,
    Guid? PenaltisVencedorId);

public record LigaArtilheiroDto(
    int JogadorId,
    string JogadorNome,
    Guid TimeId,
    string TimeNome,
    int Gols,
    int Assistencias);

public record LigaCartaoEstatDto(
    int JogadorId,
    string JogadorNome,
    Guid TimeId,
    string TimeNome,
    int CartoesAmarelos,
    int CartoesVermelhos);

public record HistoricoArtilheiroDto(
    int JogadorId,
    string JogadorNome,
    int TotalGols,
    int TotalAssistencias,
    IReadOnlyList<ArtilheiroCompetitionDetalheDto> Competicoes);

public record ArtilheiroCompetitionDetalheDto(
    Guid LigaId,
    string NomeCompeticion,
    TipoCompetition TipoCompeticion,
    int Gols,
    int Assistencias);

public record HistoricoAssistenciaDto(
    int JogadorId,
    string JogadorNome,
    int TotalAssistencias,
    IReadOnlyList<AssistenciaCompetitionDetalheDto> Competicoes);

public record AssistenciaCompetitionDetalheDto(
    Guid LigaId,
    string NomeCompeticion,
    TipoCompetition TipoCompeticion,
    int Assistencias);

// ---------------------------------------------------------------------------
// Histórico e temporada por time (aba em /teams/details/{id})
// ---------------------------------------------------------------------------

/// <summary>
/// Linha do histórico de um jogador <b>pelo time</b>: só contabiliza eventos
/// registrados enquanto ele defendia esse time (o TimeId do evento).
/// </summary>
public record TimeHistoricoJogadorDto(
    int JogadorId,
    string JogadorNome,
    bool NoElencoAtual,
    int Gols,
    int Assistencias);

public record TimeHistoricoDto(
    Guid TimeId,
    string TimeNome,
    int TotalGols,
    int TotalAssistencias,
    IReadOnlyList<TimeHistoricoJogadorDto> Jogadores);

public record TimeTemporadaCompeticaoDto(
    Guid LigaId,
    string Nome,
    TipoCompetition Tipo,
    LigaStatus Status,
    int? Posicao,
    GrupoCopa? Grupo,
    int Pontos,
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra,
    int SaldoGols,
    string? FaseLabel);

public record TimeTemporadaJogadorDto(
    int JogadorId,
    string JogadorNome,
    short PositionId,
    string Posicao,
    // CleanSheets só é preenchido para goleiros, zagueiros e laterais.
    int? CleanSheets,
    int Gols,
    int Assistencias,
    int CartoesAmarelos,
    int CartoesVermelhos);

public record TimeTemporadaDto(
    Guid TimeId,
    string TimeNome,
    IReadOnlyList<TimeTemporadaCompeticaoDto> Competicoes,
    IReadOnlyList<TimeTemporadaJogadorDto> Jogadores,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra,
    int JogosEncerrados,
    int CleanSheets);
