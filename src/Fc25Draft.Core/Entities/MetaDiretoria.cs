using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

/// <summary>
/// Meta que a diretoria cobra de um time numa competição da temporada. É calculada antes da
/// 1ª rodada (histórico do clube + elenco) e fica gravada: o que acontece depois não muda a meta,
/// só o admin pode ajustar.
/// </summary>
public class MetaDiretoria
{
    public Guid MetaId { get; set; }
    public int Temporada { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }

    /// <summary>Liga: posição esperada pela nota (1 = favorito). Nulo na Copa.</summary>
    public int? PosicaoEsperada { get; set; }

    /// <summary>Liga: nota da expectativa (0 a 100) que gerou a posição esperada.</summary>
    public double? Nota { get; set; }

    /// <summary>Liga: posição média nas temporadas anteriores (a Série B conta depois da A). Nulo sem histórico.</summary>
    public double? MediaHistorico { get; set; }

    /// <summary>Liga: média de overall dos 11 melhores quando a meta foi gerada.</summary>
    public double? ForcaXI { get; set; }

    /// <summary>Liga: terminar até esta posição.</summary>
    public int? MetaPosicao { get; set; }

    /// <summary>Copa: pote do sorteio que definiu a meta.</summary>
    public int? Pote { get; set; }

    /// <summary>Copa: chegar pelo menos a esta fase. Nulo = sem meta (só conta se superar).</summary>
    public FasePremiacao? MetaFase { get; set; }

    /// <summary>O admin mudou a meta gerada.</summary>
    public bool AjustadaPeloAdmin { get; set; }

    public DateTime CriadaEm { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}

/// <summary>Bônus da diretoria já creditado no caixa por uma competição, para não pagar duas vezes.</summary>
public class DiretoriaPagamento
{
    public Guid PagamentoId { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }

    public decimal Valor { get; set; }

    /// <summary>"Meta cumprida", "Meta superada".</summary>
    public string Motivo { get; set; } = null!;

    public DateTime PagoEm { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}

public enum StatusPedidoDemissao
{
    Pendente = 0,
    Aceito = 1,
    Recusado = 2,
    /// <summary>O treinador saiu (pediu demissão, por exemplo) antes da organização decidir.</summary>
    Encerrado = 3
}

/// <summary>
/// Pedido de demissão da diretoria depois de um ultimato não cumprido. Só a organização vê até decidir:
/// aceito, o treinador sai do clube; recusado, vira voto de confiança.
/// </summary>
public class PedidoDemissao
{
    public Guid PedidoId { get; set; }
    public int Temporada { get; set; }
    public Guid TimeId { get; set; }

    /// <summary>Treinador do time quando o pedido foi feito.</summary>
    public Guid? TreinadorId { get; set; }

    /// <summary>Jogo que levou à cadeira balançando e abriu o ultimato.</summary>
    public Guid PartidaOrigemId { get; set; }

    /// <summary>Jogo em que o ultimato ficou impossível de cumprir.</summary>
    public Guid PartidaFalhaId { get; set; }

    /// <summary>Pontos feitos no ultimato.</summary>
    public int Pontos { get; set; }

    public StatusPedidoDemissao Status { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? DecididoEm { get; set; }

    public Team Time { get; set; } = null!;
    public Treinador? Treinador { get; set; }
}
