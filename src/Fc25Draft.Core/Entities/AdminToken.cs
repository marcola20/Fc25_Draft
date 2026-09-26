namespace Fc25Draft.Core.Entities;

public class AdminToken
{
    public Guid AdminTokenId { get; set; }
    public string Token { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }

    /// <summary>
    /// A pessoa por trás do token de administrador. Serve para o admin participar das coisas
    /// que são por pessoa, como o bolão, sem precisar sair e entrar com outro token.
    /// </summary>
    public Guid? TreinadorId { get; set; }

    public Treinador? Treinador { get; set; }
}
