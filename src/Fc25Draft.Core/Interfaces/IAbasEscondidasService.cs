namespace Fc25Draft.Core.Interfaces;

/// <summary>Guarda quais itens do menu do site estão escondidos (tela /admin/abas).</summary>
public interface IAbasEscondidasService
{
    /// <summary>Os endereços escondidos.</summary>
    Task<IReadOnlyList<string>> ListarAsync(CancellationToken ct);

    /// <summary>Troca a lista inteira: ficam escondidos exatamente estes endereços.</summary>
    Task SalvarAsync(IReadOnlyCollection<string> rotas, CancellationToken ct);
}
