namespace Fc25Draft.Core.Entities;

/// <summary>
/// O perfil que a própria pessoa monta: apelido, frase de efeito, esquema favorito (da lista fixa em
/// <c>EsquemasTaticos</c>) e foto. Tabela própria, uma linha por <see cref="Treinador"/>, para a foto não
/// pesar nas consultas de treinador. O admin pode apagar o que passar do ponto.
/// </summary>
public class PerfilTreinador
{
    public const int TamanhoApelido = 30;
    public const int TamanhoFrase = 120;

    public Guid TreinadorId { get; set; }

    public string? Apelido { get; set; }

    public string? Frase { get; set; }

    /// <summary>Nome do esquema ("4-3-3"), sempre um da lista fixa.</summary>
    public string? Esquema { get; set; }

    public byte[]? Imagem { get; set; }

    /// <summary>Tipo da imagem (image/webp, image/png, image/jpeg).</summary>
    public string? ContentType { get; set; }

    public DateTime? FotoAtualizadaEm { get; set; }

    public DateTime AtualizadoEm { get; set; }

    public Treinador Treinador { get; set; } = null!;
}
