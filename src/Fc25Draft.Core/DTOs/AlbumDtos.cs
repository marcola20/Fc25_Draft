using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Uma figurinha do álbum, como vem impressa.</summary>
public record FigurinhaDto(
    Guid FigurinhaId,
    int Numero,
    TipoFigurinha Tipo,
    RaridadeFigurinha Raridade,
    Guid TeamId,
    string TimeNome,
    int? PlayerId,
    string NomeImpresso,
    string? PosicaoSigla,
    int? Overall,
    string? Destaque,
    int Ordem);

public record AlbumDto(Guid AlbumId, string Nome, int Temporada, DateTime LancadoEm, bool Ativo, int TotalFigurinhas);

/// <summary>Contagem por raridade (total do álbum ou o que a pessoa já colou).</summary>
public record AlbumContagemDto(int Comuns, int Brilhantes, int Lendarias)
{
    public int Total => Comuns + Brilhantes + Lendarias;

    public int Da(RaridadeFigurinha raridade) => raridade switch
    {
        RaridadeFigurinha.Brilhante => Brilhantes,
        RaridadeFigurinha.Lendaria => Lendarias,
        _ => Comuns
    };
}

/// <summary>Uma vaga do álbum da pessoa: a figurinha e quantas ela tem (0 = vaga vazia).</summary>
public record VagaDoAlbumDto(FigurinhaDto Figurinha, int Quantidade, bool Nova)
{
    public bool Colada => Quantidade > 0;
    public int Repetidas => Math.Max(0, Quantidade - 1);
}

public record PaginaDoAlbumDto(Guid TeamId, string TimeNome, IReadOnlyList<VagaDoAlbumDto> Vagas)
{
    public int Total => Vagas.Count;
    public int Coladas => Vagas.Count(v => v.Colada);
    public bool Completa => Total > 0 && Coladas == Total;
    public int Percentual => Total == 0 ? 0 : (int)Math.Floor(Coladas * 100.0 / Total);
}

/// <summary>O álbum de uma pessoa: as páginas com o que ela colou e os pacotes que tem para abrir.</summary>
public record MeuAlbumDto(
    AlbumDto Album,
    IReadOnlyList<PaginaDoAlbumDto> Paginas,
    AlbumContagemDto TotalPorRaridade,
    AlbumContagemDto ColadasPorRaridade,
    IReadOnlyList<PacoteFechadoDto> Fechados,
    int Repetidas,
    bool PacoteDoDiaDisponivel)
{
    public int PacotesParaAbrir => Fechados.Count;

    public int Total => TotalPorRaridade.Total;
    public int Coladas => ColadasPorRaridade.Total;
    public int Percentual => Total == 0 ? 0 : (int)Math.Floor(Coladas * 100.0 / Total);
}

/// <summary>Um pacote ainda fechado e de onde ele veio, na ordem em que vai ser aberto.</summary>
public record PacoteFechadoDto(Guid PacoteId, string Origem, string DeOndeVeio, DateTime CriadoEm);

/// <summary>O álbum da pessoa em números, para o cartão da Minha Área.</summary>
public record AlbumResumoDoTreinadorDto(AlbumDto Album, int Coladas, int Repetidas, int PacotesParaAbrir, bool PacoteDoDiaDisponivel)
{
    public int Total => Album.TotalFigurinhas;
    public int Percentual => Total == 0 ? 0 : (int)Math.Floor(Coladas * 100.0 / Total);
}

/// <summary>Pacotes criados por uma rodada da reconciliação.</summary>
public record ReconciliacaoPacotesDto(int Vitorias, int Bolao)
{
    public int Total => Vitorias + Bolao;
}

/// <summary>Uma figurinha que saiu no pacote: nova (foi colada) ou repetida.</summary>
public record FigurinhaTiradaDto(FigurinhaDto Figurinha, bool Nova, int QuantidadeAgora);

public record PacoteAbertoDto(Guid PacoteId, string Origem, string? Motivo, IReadOnlyList<FigurinhaTiradaDto> Figurinhas, int PacotesRestantes);

// ---- Admin ----

/// <summary>Uma lendária escolhida pelo admin, com o texto curto opcional.</summary>
public record LendariaEscolhaDto(int PlayerId, string? Destaque);

/// <summary>
/// Jogador que pode virar lendária (está no álbum), com a média de nota da temporada passada e a raridade
/// que ele tem se não for lendária.
/// </summary>
public record CandidatoLendariaDto(
    int PlayerId,
    string Nome,
    Guid TeamId,
    string TimeNome,
    string PosicaoSigla,
    int Overall,
    decimal? MediaNota,
    int Jogos,
    RaridadeFigurinha RaridadeBase);

public record AlbumPreviaClubeDto(Guid TeamId, string Nome, string? Divisao, int Jogadores, int SemFoto);

/// <summary>
/// O que o álbum da temporada vai ter se for lançado agora. A contagem por raridade sai dos candidatos
/// (a raridade base de cada um) e das lendárias escolhidas na tela.
/// </summary>
public record AlbumPreviaDto(
    int Temporada,
    string NomeSugerido,
    string OrigemDosClubes,
    IReadOnlyList<AlbumPreviaClubeDto> Clubes,
    int SemFoto,
    IReadOnlyList<CandidatoLendariaDto> Sugeridas,
    IReadOnlyList<CandidatoLendariaDto> Candidatos);

/// <summary>O álbum lançado, visto pelo admin.</summary>
public record AlbumAdminDto(
    AlbumDto Album,
    int Clubes,
    AlbumContagemDto PorRaridade,
    int SemFoto,
    int PacotesDados,
    int PacotesAbertos,
    int Colecionadores,
    IReadOnlyList<FigurinhaDto> Lendarias,
    IReadOnlyList<CandidatoLendariaDto> Candidatos,
    int PontosBolaoPorPacote,
    IReadOnlyDictionary<string, int> PacotesPorOrigem)
{
    /// <summary>Depois do primeiro pacote aberto as lendárias ficam travadas.</summary>
    public bool LendariasTravadas => PacotesAbertos > 0;
}

public record FigurinhaSemFotoDto(int PlayerId, int Numero, string Nome, Guid TeamId, string TimeNome, string? PosicaoSigla, int? Overall, RaridadeFigurinha Raridade);

/// <summary>Pacotes de uma pessoa, para a tela de dar pacotes.</summary>
public record PacotesDoTreinadorDto(Guid TreinadorId, string Nome, string? TimeAtual, bool Ativo, int ParaAbrir, int Abertos, int Coladas);
