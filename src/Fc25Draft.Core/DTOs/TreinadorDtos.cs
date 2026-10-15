using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Jogos e campanha de um treinador num recorte (um clube, uma temporada ou a carreira toda).</summary>
public record TreinadorRetrospectoDto(
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra)
{
    public int Pontos => Vitorias * 3 + Empates;
    public int Saldo => GolsPro - GolsContra;

    /// <summary>Pontos ganhos sobre os disputados, como a imprensa mede técnico.</summary>
    public decimal Aproveitamento => Jogos == 0 ? 0m : Math.Round(Pontos * 100m / (Jogos * 3), 1);

    public static readonly TreinadorRetrospectoDto Vazio = new(0, 0, 0, 0, 0, 0);
}

/// <summary>Uma passagem do treinador por um clube.</summary>
public record TreinadorPassagemDto(
    Guid PassagemId,
    Guid TimeId,
    string TimeNome,
    PapelTreinador Papel,
    DateTime Desde,
    DateTime? Ate,
    TreinadorRetrospectoDto? Retrospecto = null,
    MotivoSaidaTreinador? MotivoSaida = null)
{
    public bool Atual => Ate is null;

    /// <summary>"pediu demissão", "demitido" ou nulo.</summary>
    public string? MotivoTexto => MotivoSaida switch
    {
        MotivoSaidaTreinador.PediuDemissao => "pediu demissão",
        MotivoSaidaTreinador.Demitido => "demitido",
        _ => null
    };
}

/// <summary>O treinador com as passagens dele, da mais recente para a mais antiga.</summary>
public record TreinadorDto(
    Guid TreinadorId,
    string Nome,
    string Token,
    bool Ativo,
    IReadOnlyList<TreinadorPassagemDto> Passagens)
{
    public TreinadorPassagemDto? Hoje => Passagens.FirstOrDefault(p => p.Atual);
    public string TimeAtual => Hoje?.TimeNome ?? "Sem time";
}

/// <summary>Uma temporada na carreira do treinador: onde estava e como foi.</summary>
public record TreinadorTemporadaDto(
    int Temporada,
    Guid TimeId,
    string TimeNome,
    PapelTreinador Papel,
    string Competicao,
    int? Posicao,
    int? TotalTimes,
    bool Campeao,
    string? Movimento,
    TreinadorRetrospectoDto? Retrospecto = null,
    /// <summary>Onde parou no mata-mata. Vale para Copa e Supercopa, onde não existe posição na tabela.</summary>
    string? Fase = null,
    /// <summary>"Rebaixado" ou "Acesso", conforme a divisão em que o clube apareceu na temporada seguinte.</summary>
    string? Desfecho = null);

/// <summary>A carreira inteira: passagens, temporadas e o que ganhou.</summary>
public record TreinadorCarreiraDto(
    Guid TreinadorId,
    string Nome,
    bool Ativo,
    IReadOnlyList<TreinadorPassagemDto> Passagens,
    IReadOnlyList<TreinadorTemporadaDto> Temporadas,
    IReadOnlyList<string> Titulos,
    TreinadorRetrospectoDto? Retrospecto = null)
{
    public int TotalDeTitulos => Titulos.Count;
    public IReadOnlyList<string> Clubes => Passagens.Select(p => p.TimeNome).Distinct().ToArray();
}

/// <summary>
/// Um jogo entre dois técnicos, visto do lado de quem é o dono do retrospecto. <see cref="EleAuxiliar"/> e
/// <see cref="AdversarioAuxiliar"/> dizem quem estava no banco como auxiliar (o jogo leva asterisco).
/// </summary>
public record TreinadorConfrontoJogoDto(
    Guid PartidaId,
    DateTime Data,
    string Competicao,
    string TimeNome,
    string AdversarioTimeNome,
    bool EmCasa,
    int GolsPro,
    int GolsContra,
    bool IsWO,
    bool EleAuxiliar = false,
    bool AdversarioAuxiliar = false)
{
    public bool ComAuxiliar => EleAuxiliar || AdversarioAuxiliar;

    /// <summary>"V", "E" ou "D" pelo placar do tempo normal (pênaltis contam como empate).</summary>
    public string Resultado => GolsPro > GolsContra ? "V" : GolsPro < GolsContra ? "D" : "E";
}

/// <summary>Retrospecto contra outro técnico, com os jogos do mais recente para o mais antigo.</summary>
public record TreinadorConfrontoDto(
    Guid AdversarioId,
    string AdversarioNome,
    TreinadorRetrospectoDto Retrospecto,
    IReadOnlyList<TreinadorConfrontoJogoDto> Jogos)
{
    /// <summary>Algum dos jogos foi com um dos dois de auxiliar (o retrospecto leva asterisco).</summary>
    public bool ComAuxiliar => Jogos.Any(j => j.ComAuxiliar);
}

public record TreinadorSalvarRequest(Guid? TreinadorId, string Nome, string? Token, bool Ativo = true);

public record TreinadorPassagemRequest(Guid TreinadorId, Guid TimeId, PapelTreinador Papel, DateTime Desde, DateTime? Ate);
