namespace Fc25Draft.Core.Entities;

/// <summary>
/// Item do menu do site que o admin escondeu em /admin/abas (o draft acabou, o álbum não começou...).
/// Uma linha por endereço; sem linha, o item aparece.
/// </summary>
public class AbaEscondida
{
    /// <summary>O endereço do item no menu, como "/draft/info".</summary>
    public string Rota { get; set; } = null!;

    public DateTime EscondidaEm { get; set; }
}
