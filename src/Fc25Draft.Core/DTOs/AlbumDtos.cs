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
    int Ordem,
    Guid? TreinadorId = null,
    PapelTreinador? Papel = null,
    PerfilTreinadorDto? Perfil = null)
{
    /// <summary>"Técnico" ou "Auxiliar", na figurinha de técnico.</summary>
    public string? PapelTexto => Tipo == TipoFigurinha.Treinador ? (Papel == PapelTreinador.Auxiliar ? "Auxiliar" : "Técnico") : null;
}

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
    bool PacoteDoDiaDisponivel,
    IReadOnlyList<ConquistaAlbumDto> Conquistas,
    int TrocasEsperando)
{
    public int PacotesParaAbrir => Fechados.Count;

    public ConquistaAlbumDto? AlbumCompleto => Conquistas.FirstOrDefault(c => c.Tipo == TipoConquistaAlbum.AlbumCompleto);

    /// <summary>O selo da página do clube, se a pessoa já completou.</summary>
    public ConquistaAlbumDto? SeloDaPagina(Guid teamId) =>
        Conquistas.FirstOrDefault(c => c.Tipo == TipoConquistaAlbum.PaginaCompleta && c.TeamId == teamId);

    public int Total => TotalPorRaridade.Total;
    public int Coladas => ColadasPorRaridade.Total;
    public int Percentual => Total == 0 ? 0 : (int)Math.Floor(Coladas * 100.0 / Total);
}

/// <summary>Um pacote ainda fechado e de onde ele veio, na ordem em que vai ser aberto.</summary>
public record PacoteFechadoDto(Guid PacoteId, string Origem, string DeOndeVeio, DateTime CriadoEm);

/// <summary>O álbum da pessoa em números, para o cartão da Minha Área.</summary>
public record AlbumResumoDoTreinadorDto(
    AlbumDto Album, int Coladas, int Repetidas, int PacotesParaAbrir, bool PacoteDoDiaDisponivel,
    IReadOnlyList<ConquistaAlbumDto> Conquistas, int TrocasEsperando)
{
    public IReadOnlyList<ConquistaAlbumDto> PaginasCompletas =>
        Conquistas.Where(c => c.Tipo == TipoConquistaAlbum.PaginaCompleta).ToList();

    public ConquistaAlbumDto? AlbumCompleto => Conquistas.FirstOrDefault(c => c.Tipo == TipoConquistaAlbum.AlbumCompleto);

    public int Total => Album.TotalFigurinhas;
    public int Percentual => Total == 0 ? 0 : (int)Math.Floor(Coladas * 100.0 / Total);
}

/// <summary>Pacotes criados por uma rodada da reconciliação.</summary>
public record ReconciliacaoPacotesDto(int Jogos, int Bolao)
{
    public int Total => Jogos + Bolao;
}

/// <summary>Uma figurinha que saiu no pacote: nova (foi colada) ou repetida.</summary>
public record FigurinhaTiradaDto(FigurinhaDto Figurinha, bool Nova, int QuantidadeAgora);

public record PacoteAbertoDto(
    Guid PacoteId, string Origem, string? Motivo, IReadOnlyList<FigurinhaTiradaDto> Figurinhas, int PacotesRestantes,
    IReadOnlyList<ConquistaAlbumDto> Conquistas);

/// <summary>Um selo do álbum: página completa (com o clube) ou álbum completo.</summary>
public record ConquistaAlbumDto(TipoConquistaAlbum Tipo, Guid? TeamId, string? TimeNome, DateTime Em);

/// <summary>Uma figurinha rara (brilhante de jogador ou lendária) que alguém tirou num pacote.</summary>
public record RaraTiradaDto(Guid TreinadorId, string Nome, FigurinhaDto Figurinha, DateTime Quando);

/// <summary>Uma linha do ranking de colecionadores do álbum.</summary>
public record ColecionadorDto(
    int Posicao,
    Guid TreinadorId,
    string Nome,
    string? TimeAtual,
    int Coladas,
    int TotalDeFigurinhas,
    int Lendarias,
    IReadOnlyList<ConquistaAlbumDto> PaginasCompletas,
    DateTime? AlbumCompletoEm,
    int TotalDoAlbum)
{
    public int Percentual => TotalDoAlbum == 0 ? 0 : (int)Math.Floor(Coladas * 100.0 / TotalDoAlbum);
}

public record ColecionadoresDto(AlbumDto Album, IReadOnlyList<ColecionadorDto> Linhas);

// ---- Trocas ----

public record PessoaDaTrocaDto(Guid TreinadorId, string Nome);

/// <summary>Uma proposta de troca. Oferecidas saem de quem propôs; pedidas, de quem recebeu.</summary>
public record TrocaDto(
    Guid TrocaId,
    PessoaDaTrocaDto De,
    PessoaDaTrocaDto Para,
    StatusTroca Status,
    DateTime CriadaEm,
    DateTime ExpiraEm,
    DateTime? RespondidaEm,
    string? Motivo,
    Guid? ContrapropostaDeId,
    IReadOnlyList<FigurinhaDto> Oferecidas,
    IReadOnlyList<FigurinhaDto> Pedidas)
{
    public bool Pendente => Status == StatusTroca.Pendente;
}

/// <summary>Uma repetida: a figurinha e quantas cópias a pessoa tem (a colada conta, então troca até Quantidade - 1).</summary>
public record RepetidaDto(FigurinhaDto Figurinha, int Quantidade)
{
    public int Sobrando => Quantidade - 1;
}

/// <summary>Uma figurinha que falta e quem a tem repetida.</summary>
public record FaltaDto(FigurinhaDto Figurinha, IReadOnlyList<PessoaDaTrocaDto> QuemTemRepetida);

/// <summary>"Fulano tem 3 que você precisa e precisa de 2 suas".</summary>
public record SugestaoDeTrocaDto(
    PessoaDaTrocaDto Pessoa,
    IReadOnlyList<FigurinhaDto> EleTemQueVocePrecisa,
    IReadOnlyList<FigurinhaDto> VoceTemQueElePrecisa)
{
    /// <summary>Quantas dá para trocar uma por uma.</summary>
    public int Encaixe => Math.Min(EleTemQueVocePrecisa.Count, VoceTemQueElePrecisa.Count);
}

/// <summary>Tudo da página de trocas de uma pessoa.</summary>
public record TrocasDoTreinadorDto(
    AlbumDto Album,
    IReadOnlyList<RepetidaDto> MinhasRepetidas,
    IReadOnlyList<FaltaDto> Faltam,
    IReadOnlyList<TrocaDto> Recebidas,
    IReadOnlyList<TrocaDto> Enviadas,
    IReadOnlyList<TrocaDto> Historico,
    IReadOnlyList<SugestaoDeTrocaDto> Sugestoes,
    IReadOnlyList<PessoaDaTrocaDto> Colecionadores)
{
    public int RepetidasSobrando => MinhasRepetidas.Sum(r => r.Sobrando);
}

/// <summary>Para montar uma proposta com outra pessoa: as repetidas de cada um, marcando o que interessa ao outro.</summary>
public record MontarTrocaDto(
    PessoaDaTrocaDto Outro,
    IReadOnlyList<RepetidaDto> MinhasRepetidas,
    IReadOnlyList<RepetidaDto> RepetidasDele,
    IReadOnlySet<Guid> EleNaoTem,
    IReadOnlySet<Guid> EuNaoTenho);

/// <summary>O que aconteceu ao aceitar: se as figurinhas trocaram de mão e, se não, por quê.</summary>
public record ResultadoDaTrocaDto(bool Executou, string Mensagem, IReadOnlyList<FigurinhaDto> Recebidas, IReadOnlyList<ConquistaAlbumDto> Conquistas);

/// <summary>Quem completou um álbum (Hall da Fama · Colecionadores), na ordem em que completou.</summary>
public record AlbumCompletoDto(int Ordem, Guid TreinadorId, string Nome, string AlbumNome, int Temporada, DateTime Em);

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
    RaridadeFigurinha RaridadeBase,
    /// <summary>Quantas pessoas já têm a figurinha dele (álbum lançado; na prévia é sempre 0).</summary>
    int Donos = 0);

public record AlbumPreviaClubeDto(Guid TeamId, string Nome, string? Divisao, int Jogadores, int SemFoto, int Tecnicos = 0, int TecnicosSemFoto = 0);

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
    IReadOnlyList<CandidatoLendariaDto> Candidatos)
{
    /// <summary>Técnicos e auxiliares que entram (figurinha de técnico, sorteada como brilhante).</summary>
    public int Tecnicos => Clubes.Sum(c => c.Tecnicos);
    public int TecnicosSemFoto => Clubes.Sum(c => c.TecnicosSemFoto);
}

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
    IReadOnlyDictionary<string, int> PacotesPorOrigem,
    int Tecnicos = 0,
    int TecnicosSemFoto = 0)
{
    /// <summary>Lendárias que já saíram para alguém: não podem mais deixar de ser lendárias.</summary>
    public IReadOnlySet<int> LendariasFixas => Lendarias
        .Where(f => f.PlayerId is not null && Candidatos.Any(c => c.PlayerId == f.PlayerId && c.Donos > 0))
        .Select(f => f.PlayerId!.Value)
        .ToHashSet();
}

/// <summary>Figurinha sem foto: de jogador (com <c>PlayerId</c>) ou de técnico (com <c>TreinadorId</c>).</summary>
public record FigurinhaSemFotoDto(int? PlayerId, int Numero, string Nome, Guid TeamId, string TimeNome, string? PosicaoSigla, int? Overall,
    RaridadeFigurinha Raridade, Guid? TreinadorId = null);

/// <summary>Pacotes de uma pessoa, para a tela de dar pacotes.</summary>
public record PacotesDoTreinadorDto(Guid TreinadorId, string Nome, string? TimeAtual, bool Ativo, int ParaAbrir, int Abertos, int Coladas);
