namespace Fc25Draft.Web.Services;

/// <summary>
/// Os itens do menu que o admin escondeu em /admin/abas, em memória para o menu de todo mundo. Carregado na
/// subida do site; ao salvar, <see cref="Mudou"/> avisa os circuitos abertos para remontarem o menu na hora.
/// </summary>
public sealed class AbasEscondidas
{
    private volatile HashSet<string> _rotas = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Mudou;

    public IReadOnlyCollection<string> Rotas => _rotas;

    public bool Escondida(string? rota) => !string.IsNullOrWhiteSpace(rota) && _rotas.Contains(rota);

    public void Usar(IEnumerable<string> rotas)
    {
        _rotas = new HashSet<string>(rotas, StringComparer.OrdinalIgnoreCase);
        Mudou?.Invoke();
    }
}
