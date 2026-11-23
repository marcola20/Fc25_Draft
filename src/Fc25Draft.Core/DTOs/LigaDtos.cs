using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Core.DTOs;

/// <summary>Calendário de uma temporada; <paramref name="Abertura"/> nula enquanto não foi definida.</summary>
public record CalendarioTemporadaDto(int Temporada, DateTime? Abertura, IReadOnlyList<DataDaTemporada> Datas);

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
    string? CampeaoNome = null,
    int? Temporada = null,
    Divisao? Divisao = null,
    int? VagasDiretas = null,
    int? VagasPlayoff = null,
    Guid? ConfrontoFinalTimeAId = null,
    Guid? ConfrontoFinalTimeBId = null,
    FormatoCopa FormatoCopa = FormatoCopa.Grupos)
{
    /// <summary>Copa com fase de liga (tabela única) em vez de grupos.</summary>
    public bool FaseDeLiga => Tipo == TipoCompetition.Copa && FormatoCopa == FormatoCopa.FaseDeLiga;
}

/// <summary>
/// Uma edição de competição (atual ou já encerrada) com o resumo do que ela teve —
/// times, grupos, rodadas e fases — porque o formato muda de temporada para temporada.
/// </summary>
public record LigaEdicaoDto(
    Guid LigaId,
    string Nome,
    int? Temporada,
    TipoCompetition Tipo,
    Divisao? Divisao,
    LigaStatus Status,
    Guid? CampeaoTimeId,
    string? CampeaoNome,
    int TotalTimes,
    int Grupos,
    int Rodadas,
    int PartidasJogadas,
    int PartidasTotal,
    IReadOnlyList<FaseKnockout> FasesMataMata,
    int VagasDiretas,
    int VagasPlayoff,
    DateTime DataInicio,
    DateTime DataFim)
{
    public bool Encerrada => Status == LigaStatus.Encerrada;
}

public record LigaCreateRequest(
    [Required, MaxLength(120)] string Nome,
    DateTime DataInicio,
    DateTime DataFim,
    TipoCompetition Tipo = TipoCompetition.Liga,
    int? Temporada = null,
    Divisao? Divisao = null,
    int? VagasDiretas = null,
    int? VagasPlayoff = null);

/// <summary>Campos nulos mantêm o valor atual; <paramref name="RemoverDivisao"/> tira a divisão da liga.</summary>
public record LigaUpdateRequest(
    [MaxLength(120)] string? Nome,
    DateTime? DataInicio,
    DateTime? DataFim,
    int? Temporada = null,
    Divisao? Divisao = null,
    bool RemoverDivisao = false,
    int? VagasDiretas = null,
    int? VagasPlayoff = null);

public record LigaRodadaDto(
    Guid RodadaId,
    Guid LigaId,
    int Numero,
    int TotalPartidas,
    bool Desempate = false,
    DateTime? DataHora = null);

/// <param name="Titulo">Nome dos jogos fora das rodadas normais ("Mini liga do título", "Playoff de acesso"); nulo nas rodadas.</param>
public record LigaRodadaComPartidasDto(
    Guid RodadaId,
    Guid LigaId,
    int Numero,
    IReadOnlyList<LigaPartidaDto> Partidas,
    bool Desempate = false,
    DateTime? DataHora = null,
    string? Titulo = null);

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
    DateTime? EncerradaEm,
    string? YoutubeVideoId = null,
    DateTime? DataHora = null,
    bool HorarioProprio = false);

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
    DateTime CriadoEm,
    // Só em substituições: quem saiu (JogadorId/JogadorNome é quem entrou).
    int? JogadorSaiuId = null,
    string? JogadorSaiuNome = null);

public record LigaGolRequest(
    [Required] Guid TimeId,
    [Required] int JogadorId,
    int? AssistenteId,
    [Range(1, 120)] int? Minuto,
    bool GolContra = false);

public record LigaSubstituicaoRequest(
    [Required] Guid TimeId,
    [Required] int JogadorId,
    int? JogadorSaiuId,
    [Range(1, 120)] int? Minuto);

public record PartidaEscalacaoJogadorDto(
    int JogadorId,
    string JogadorNome,
    string Posicao,
    bool Titular,
    int Ordem,
    // Nota do PES no jogo (nula sem importação ou sem casar o nome).
    decimal? Nota = null,
    bool MelhorEmCampo = false,
    // Lugar na escalação (GK, LCB...) e a posição natural, para desenhar o campo.
    string? SlotCode = null,
    int PosicaoId = 0);

/// <summary>
/// Suspensão automática (3 amarelos ou vermelho). <paramref name="JogoCumprido"/> nulo enquanto o próximo
/// jogo do time na competição não existe; <paramref name="Cumprida"/> quando esse jogo já foi disputado.
/// </summary>
public record SuspensaoDto(
    int JogadorId, string JogadorNome, Guid TimeId, string TimeNome,
    string Motivo, string JogoDoCartao,
    Guid? PartidaCumprida, Guid? RodadaCumprida, string? JogoCumprido, bool Cumprida);

/// <summary>Jogador com 2 amarelos na competição: o próximo suspende.</summary>
public record PenduradoDto(int JogadorId, string JogadorNome, Guid TimeId, string TimeNome, int Amarelos);

public record DisciplinaDto(IReadOnlyList<SuspensaoDto> Suspensoes, IReadOnlyList<PenduradoDto> Pendurados);

/// <summary>Um lançamento do extrato do caixa. <paramref name="Valor"/> com sinal: positivo entrou, negativo saiu.</summary>
public record ExtratoLancamentoDto(DateTime DataUtc, string Origem, string? Descricao, decimal Valor, decimal SaldoDepois);

/// <summary>
/// Extrato do caixa do time, do mais recente para o mais antigo. <paramref name="SaldoAnterior"/> é o caixa
/// antes do primeiro lançamento registrado (caixa inicial e ajustes antigos que não deixaram registro).
/// </summary>
public record ExtratoTimeDto(
    decimal Saldo,
    decimal Entradas,
    decimal Saidas,
    decimal SaldoAnterior,
    IReadOnlyList<ExtratoLancamentoDto> Lancamentos);

/// <summary>Jogador da seleção da rodada, escolhido pela nota do PES.</summary>
public record SelecaoRodadaJogadorDto(
    int JogadorId,
    string JogadorNome,
    string Posicao,
    Guid TimeId,
    string TimeNome,
    decimal Nota,
    bool MelhorEmCampo);

/// <summary>Média das notas do PES de um jogador numa competição.</summary>
public record NotaMediaJogadorDto(
    int JogadorId,
    string JogadorNome,
    string Posicao,
    // Time do jogo mais recente com nota.
    string TimeNome,
    int Jogos,
    decimal Media,
    decimal MelhorNota,
    int VezesMelhorEmCampo);

public record PartidaEscalacaoTimeDto(
    Guid TimeId,
    string TimeNome,
    // Sem escalação registrada (partida antiga ou time sem escalação ativa).
    bool Vazia,
    IReadOnlyList<PartidaEscalacaoJogadorDto> Jogadores);

/// <summary>
/// Escalações dos dois times numa partida. Encerrada: o retrato gravado no
/// encerramento; ainda em jogo: a escalação ativa atual de cada time.
/// </summary>
public record PartidaEscalacoesDto(
    Guid PartidaId,
    PartidaEscalacaoTimeDto Casa,
    PartidaEscalacaoTimeDto Fora,
    IReadOnlyList<LigaEventoDto> Eventos);

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

/// <summary>
/// Empate de Pts/V/SG dentro de um grupo da Copa e o jogo decisivo correspondente,
/// quando já existe.
/// </summary>
public record LigaEmpateCopaDto(
    GrupoCopa Grupo,
    int Posicao,
    Guid TimeAId,
    string TimeANome,
    Guid TimeBId,
    string TimeBNome,
    Guid? PartidaId,
    PartidaStatus? StatusPartida,
    int? GolsTimeA,
    int? GolsTimeB,
    Guid? VencedorId,
    string? VencedorNome);

/// <summary>
/// Empate total (inclusive no confronto direto) entre o <paramref name="Posicao"/>º e o seguinte,
/// numa posição que muda a zona — e o jogo decisivo, quando já criado. Com <paramref name="TimesEmpatados"/>
/// 3+ (Série B), é um dos jogos da mini liga entre os empatados a partir do <paramref name="Posicao"/>º.
/// </summary>
public record LigaEmpateZonaDto(
    int Posicao,
    string ZonaTimeA,
    string ZonaTimeB,
    Guid TimeAId,
    string TimeANome,
    Guid TimeBId,
    string TimeBNome,
    Guid? PartidaId,
    PartidaStatus? StatusPartida,
    int? GolsTimeA,
    int? GolsTimeB,
    Guid? VencedorId,
    string? VencedorNome,
    int TimesEmpatados = 2);

public record LigaGrupoTimeDto(
    Guid LigaId,
    Guid TimeId,
    string TimeNome,
    GrupoCopa Grupo);

public record LigaCopaPoteTimeDto(Guid TimeId, string TimeNome, int Pote, GrupoCopa? Grupo);

/// <summary>Potes do sorteio da Copa e o que falta para sortear.</summary>
public record LigaCopaSorteioDto(
    Guid LigaId,
    IReadOnlyList<int> TamanhosDosGrupos,
    bool JaSorteada,
    bool PodeSortear,
    string? Impedimento,
    IReadOnlyList<LigaCopaPoteTimeDto> Times)
{
    public int TotalGrupos => TamanhosDosGrupos.Count;

    /// <summary>Ex.: "4 grupos: 4, 4, 5 e 5 times".</summary>
    public string ResumoDosGrupos => TamanhosDosGrupos.Count == 0
        ? "—"
        : $"{TotalGrupos} grupos: {string.Join(", ", TamanhosDosGrupos)} times";
}

public record LigaCopaPotesRequest(IReadOnlyDictionary<Guid, int> PotePorTime);

/// <summary>
/// Um time no ranking do sorteio da fase de liga: a posição geral na temporada anterior (Série A
/// primeiro, depois a Série B) e o quanto a Copa o fez subir.
/// </summary>
/// <param name="Ranking">1 = mais forte. Os 9 primeiros vão ao pote 1.</param>
/// <param name="PosicaoGeral">Posição na liga somando as divisões (A: 1º a 10º, B: 11º a 18º); nulo se não jogou.</param>
/// <param name="Origem">Ex.: "Série A 1º · Copa: quartas".</param>
/// <param name="Bonus">Posições que a Copa anterior dá: campeão 4, vice 3, semifinal 2, quartas 1.</param>
public record CopaRankingTimeDto(
    int Ranking,
    Guid TimeId,
    string TimeNome,
    int? PosicaoGeral,
    string Origem,
    int Bonus,
    int Pote);

public record CopaJogoSorteadoDto(int Rodada, Guid CasaId, string CasaNome, Guid ForaId, string ForaNome);

/// <summary>Sorteio da fase de liga da Copa: ranking, potes e os jogos (vazio antes de sortear).</summary>
public record CopaFaseDeLigaSorteioDto(
    Guid LigaId,
    int TemporadaDoRanking,
    bool JaSorteada,
    bool PodeSortear,
    string? Impedimento,
    IReadOnlyList<CopaRankingTimeDto> Ranking,
    IReadOnlyList<CopaJogoSorteadoDto> Jogos)
{
    /// <summary>Média da posição no ranking dos 4 adversários de cada time.</summary>
    public IReadOnlyDictionary<Guid, double> MediaDosAdversarios => Ranking.Count == 0 || Jogos.Count == 0
        ? new Dictionary<Guid, double>()
        : Utilities.SorteioFaseDeLiga.MediaDosAdversarios(
            Jogos.Select(j => new Utilities.JogoDaFaseDeLiga(j.Rodada, j.CasaId, j.ForaId)),
            Ranking.ToDictionary(r => r.TimeId, r => r.Ranking));
}

/// <summary>Grupo de cada time inscrito na Copa (quem não aparece fica fora).</summary>
public record LigaConfigurarGruposRequest(IReadOnlyDictionary<Guid, GrupoCopa> GrupoPorTime);

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
    bool TemPenaltis,
    string? YoutubeVideoId = null);

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

/// <summary>Goleiro com jogos sem sofrer gol na competição (esteve em campo e o time não levou gol).</summary>
public record LigaCleanSheetDto(
    int JogadorId,
    string JogadorNome,
    Guid TimeId,
    string TimeNome,
    int CleanSheets,
    // Null quando a competição não tem contador de jogos nem jogos anotados dele.
    int? Jogos);

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
    IReadOnlyList<ArtilheiroCompetitionDetalheDto> Competicoes,
    int TotalJogos = 0,
    // true quando alguma competição dele não tem contador nem jogos anotados (o total fica menor que o real).
    bool JogosParcial = false);

public record ArtilheiroCompetitionDetalheDto(
    Guid LigaId,
    string NomeCompeticion,
    TipoCompetition TipoCompeticion,
    int Gols,
    int Assistencias,
    // Null quando a competição é anterior ao contador de jogos.
    int? Jogos = null);

public record HistoricoGoleiroDto(
    int JogadorId,
    string JogadorNome,
    int CleanSheets,
    IReadOnlyList<GoleiroCompeticaoDetalheDto> Competicoes,
    int Jogos = 0,
    bool JogosParcial = false);

public record GoleiroCompeticaoDetalheDto(
    Guid LigaId,
    string NomeCompeticao,
    TipoCompetition TipoCompeticao,
    int CleanSheets,
    // Null quando a competição é anterior ao contador e os jogos não foram anotados.
    int? Jogos = null);

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
    int Assistencias,
    // Jogos por este time (nas competições antes do contador, só os anotados à mão).
    int Jogos = 0);

/// <summary>Uma temporada na trajetória do time: onde jogou, como terminou e se subiu ou desceu.</summary>
public record TimeTrajetoriaDto(
    int Temporada,
    Divisao Divisao,
    string LigaNome,
    int Posicao,
    int TotalTimes,
    bool Encerrada,
    bool Campeao,
    ZonaClassificacao Zona,
    string? Movimento);

/// <summary>Título de mata-mata (Copa ou Supercopa), que não entra na trajetória por divisão.</summary>
public record TimeTituloCopaDto(
    int? Temporada,
    TipoCompetition Tipo,
    string LigaNome);

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
    string? FaseLabel,
    bool FaseDeLiga = false);

public record TimeTemporadaJogadorDto(
    int JogadorId,
    string JogadorNome,
    short PositionId,
    string Posicao,
    // CleanSheets só é preenchido para goleiros, zagueiros e laterais, e conta
    // apenas as partidas sem sofrer gol em que o jogador esteve em campo.
    int? CleanSheets,
    // Null quando nenhuma competição ativa tem contador de jogos.
    int? Jogos,
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

/// <summary>Linha do Ranking de Clubes: soma de todas as competições e temporadas.</summary>
public record RankingClubeDto(
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
    int TitulosSerieA,
    int TitulosSerieB,
    int TitulosCopa,
    int TitulosSupercopa,
    int Finais,
    int Semifinais,
    int? MelhorPosicaoSerieA,
    int PontosJogos,
    int PontosTitulos,
    int PontosFases,
    int PontosPosicao)
{
    public int Titulos => TitulosSerieA + TitulosSerieB + TitulosCopa + TitulosSupercopa;
    public int SaldoGols => GolsPro - GolsContra;
}

/// <summary>Linha do Power Ranking: quem está mais forte agora. Notas de 0 a 100.</summary>
public record PowerRankingItemDto(
    int Posicao,
    Guid TimeId,
    string TimeNome,
    // Divisão na temporada atual; nulo se o time não está em nenhuma liga em andamento.
    Divisao? Divisao,
    double Nota,
    // Posições ganhas (+) ou perdidas (-) desde o dia de jogos anterior. Nulo sem comparação.
    int? Variacao,
    // Média de overall dos 11 melhores.
    double ForcaXI,
    double NotaElenco,
    int Rating,
    double NotaRating,
    // "V", "E" ou "D", do mais recente para o mais antigo.
    IReadOnlyList<string> Forma,
    double NotaForma);

public record PowerRankingDto(
    IReadOnlyList<PowerRankingItemDto> Itens,
    // Último dia com jogo encerrado (horário de Brasília).
    DateOnly? AtualizadoAte,
    // Dia de jogos usado na comparação da variação.
    DateOnly? ComparadoCom);
