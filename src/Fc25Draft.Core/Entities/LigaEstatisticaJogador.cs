namespace Fc25Draft.Core.Entities;

/// <summary>
/// Números de um jogador numa partida, tirados do "Registro individual de jogo" do PES (campo "jogadores"
/// do JSON do Auto_PES21). Guarda só os números brutos: a nota da liga é calculada na hora por
/// <see cref="Utilities.NotaDaLiga"/>, então mudar os pesos recalcula o histórico inteiro.
/// Nome do PES que não casa com o cadastro também fica (sem <see cref="JogadorId"/>), para não
/// perder o dado da calibragem. Reimportar a partida substitui todas as linhas dela.
/// Número nulo = ilegível no vídeo.
/// </summary>
public class LigaEstatisticaJogador
{
    public Guid Id { get; set; }
    public Guid PartidaId { get; set; }

    /// <summary>Time pelo qual ele jogou a partida (o lado do JSON).</summary>
    public Guid TimeId { get; set; }

    /// <summary>Jogador do cadastro; nulo quando o nome do PES não casou com segurança.</summary>
    public int? JogadorId { get; set; }

    /// <summary>Nome como veio do PES (já corrigido pelo elenco no Auto_PES21).</summary>
    public string NomePes { get; set; } = "";

    /// <summary>Ordem na lista do JSON (titulares primeiro, depois quem entrou).</summary>
    public int Ordem { get; set; }

    /// <summary>Sigla do PES: GO, ZC, LE, LD, VOL, MLG, MLE, MLD, MAT, SA, CA…</summary>
    public string? Posicao { get; set; }

    /// <summary>goleiro | defesa | meio | ataque.</summary>
    public string? Grupo { get; set; }

    /// <summary>Nota do próprio PES (0 a 10), quando legível.</summary>
    public decimal? NotaPes { get; set; }
    public bool MelhorEmCampo { get; set; }

    public int? Minutos { get; set; }
    public int? Gols { get; set; }
    public int? GolsDePenalti { get; set; }
    public int? GolsDeFalta { get; set; }
    public int? Assistencias { get; set; }
    public int? Chutes { get; set; }
    public int? ChutesAGol { get; set; }
    public int? Passes { get; set; }
    public int? PassesCertos { get; set; }
    public int? Cruzamentos { get; set; }
    public int? Faltas { get; set; }
    public int? Impedimentos { get; set; }
    public int? CobrancasDeFalta { get; set; }
    public int? Escanteios { get; set; }
    public int? Interceptacoes { get; set; }
    public int? Toques { get; set; }

    /// <summary>Metros.</summary>
    public decimal? DistanciaDeDrible { get; set; }

    /// <summary>km/h.</summary>
    public decimal? VelocidadeMedia { get; set; }

    public int? Desarmes { get; set; }
    public int? DesarmesCertos { get; set; }
    public int? Afastamentos { get; set; }

    // Só no goleiro.
    public int? ChutesSofridos { get; set; }
    public int? ChutesSofridosAGol { get; set; }
    public int? Defesas { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Player? Jogador { get; set; }
    public Team Time { get; set; } = null!;
}
