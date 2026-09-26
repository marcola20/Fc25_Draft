namespace Fc25Draft.Core.Entities;

/// <summary>
/// Jogador cedido por empréstimo: fica no elenco do tomador até o fim da temporada e volta para o dono,
/// a não ser que o tomador exerça a opção de compra antes.
/// </summary>
public class Emprestimo
{
    public Guid EmprestimoId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Time dono do jogador, para onde ele volta.</summary>
    public Guid DonoTeamId { get; set; }

    /// <summary>Time que pegou o jogador emprestado.</summary>
    public Guid TomadorTeamId { get; set; }

    /// <summary>Proposta aceita que gerou o empréstimo.</summary>
    public Guid? OfferId { get; set; }

    /// <summary>Preço combinado para o tomador ficar com o jogador em definitivo; nulo = sem opção de compra.</summary>
    public decimal? ValorOpcaoCompra { get; set; }

    public EmprestimoStatus Status { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime? FimUtc { get; set; }

    public Player Player { get; set; } = null!;
    public Team DonoTeam { get; set; } = null!;
    public Team TomadorTeam { get; set; } = null!;
}

public enum EmprestimoStatus
{
    Ativo = 1,
    Devolvido = 2,
    Comprado = 3
}
