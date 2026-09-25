using System;
using System.Collections.Generic;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>Números do jogador numa competição, por um time (quem trocou de time no meio aparece em duas linhas).</summary>
public record JogadorCarreiraCompeticaoDto(
    Guid LigaId,
    string Competicao,
    TipoCompetition Tipo,
    int? Temporada,
    Guid TimeId,
    string TimeNome,
    // Nulo nas competições anteriores ao contador de jogos.
    int? Jogos,
    int Gols,
    int Assistencias,
    int Amarelos,
    int Vermelhos,
    // Só para goleiros, zagueiros e laterais, e só onde há contador de jogos.
    int? CleanSheets,
    bool Campeao,
    // Sem registro em campo (gol, cartão, escalação): o jogador estava no elenco do time durante a competição.
    bool SoElenco = false);

public record JogadorTituloDto(Guid LigaId, string Competicao, TipoCompetition Tipo, int? Temporada, string TimeNome);

public record JogadorCarreiraEstatisticasDto(
    int Jogos,
    // true quando alguma competição não tem contador de jogos (o total fica menor que o real).
    bool JogosParcial,
    int Gols,
    int Assistencias,
    int Amarelos,
    int Vermelhos,
    int? CleanSheets,
    int MaisGolsNumJogo,
    int Hattricks,
    IReadOnlyList<JogadorCarreiraCompeticaoDto> Competicoes,
    IReadOnlyList<JogadorTituloDto> Titulos,
    // Clubes por onde passou (elenco), mesmo sem competição registrada.
    int Clubes = 0);

/// <summary>Uma passagem na trajetória: escolha de draft ou transferência.</summary>
public record JogadorMovimentoDto(
    DateTime Data,
    string Tipo,
    string? DeTime,
    string? ParaTime,
    decimal? Valor,
    string? Detalhe);

public record JogadorCarreiraDto(
    int PlayerId,
    string Nome,
    short PositionId,
    string PositionName,
    int Overall,
    int? Idade,
    Guid? TimeAtualId,
    string? TimeAtualNome,
    JogadorCarreiraEstatisticasDto Estatisticas,
    // Do mais recente para o mais antigo.
    IReadOnlyList<JogadorMovimentoDto> Trajetoria);
