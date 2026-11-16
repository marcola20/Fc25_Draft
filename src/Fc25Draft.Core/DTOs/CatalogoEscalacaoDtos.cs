namespace Fc25Draft.Core.DTOs;

/// <summary>
/// Tudo o que a escalação oferece: as posições (quem pode jogar em cada uma) e as formações (os 11 lugares,
/// cada um numa posição). Fica no banco e é editado em /admin/formacoes; ver
/// <see cref="Fc25Draft.Core.Utilities.LineupTemplateCatalog"/>.
/// </summary>
/// <param name="Padrao">Nome da formação que uma escalação nova começa usando.</param>
public sealed record CatalogoEscalacao(
    string Padrao,
    IReadOnlyList<PosicaoDaEscalacao> Posicoes,
    IReadOnlyList<FormacaoDaEscalacao> Formacoes);

/// <summary>Uma posição da escalação (LE, VOL, MAT...) e as posições do PES (ids 1 a 13) que podem jogar nela.</summary>
public sealed record PosicaoDaEscalacao(string Codigo, string Nome, IReadOnlyList<short> PodemJogar);

/// <summary>Uma formação. Desativada some da lista dos times, mas quem já usa continua funcionando.</summary>
public sealed record FormacaoDaEscalacao(string Nome, bool Ativa, IReadOnlyList<LugarDaFormacao> Lugares);

/// <summary>
/// Um lugar da formação. O <paramref name="Codigo"/> (GK, LCB, CDM1...) é o que fica gravado na escalação e
/// diz onde o jogador aparece no campo desenhado; <paramref name="Posicao"/> é o código da posição que diz quem pode jogar.
/// </summary>
public sealed record LugarDaFormacao(string Codigo, string Nome, string Posicao);

/// <summary>O catálogo para a tela do admin, com quantas escalações salvas usam cada formação.</summary>
public sealed record CatalogoEscalacaoAdminDto(
    CatalogoEscalacao Catalogo,
    IReadOnlyDictionary<string, int> EscalacoesPorFormacao,
    DateTime? AtualizadoEm);
