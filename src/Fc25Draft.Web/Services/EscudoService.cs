using System.Globalization;
using System.Text;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Resolve o escudo de um time a partir dos arquivos presentes em
/// <c>wwwroot/images/escudos</c>. A correspondência é feita pelo nome do arquivo
/// (sem extensão), ignorando maiúsculas/minúsculas e acentos — assim basta
/// adicionar um arquivo como <c>Randers FC.png</c> para o time "Randers FC"
/// aparecer com o escudo, sem precisar alterar código.
///
/// O mapa de escudos é cacheado e reconstruído automaticamente quando a pasta
/// muda (ao adicionar/remover arquivos), sem exigir reinício da aplicação.
/// </summary>
public class EscudoService
{
    public const string DefaultEscudo = "/images/escudos/escudo.png";

    private static readonly string[] Extensoes = { ".png", ".webp", ".jpg", ".jpeg", ".svg", ".gif" };

    private readonly IWebHostEnvironment _env;
    private readonly object _lock = new();
    private Dictionary<string, string> _mapa = new();
    private DateTime _ultimaLeitura = DateTime.MinValue;
    private bool _carregado;

    public EscudoService(IWebHostEnvironment env) => _env = env;

    /// <summary>Caminho do escudo do time, ou o escudo padrão quando não houver arquivo correspondente.</summary>
    public string GetEscudo(string? nome) => TryGetEscudo(nome, out var path) ? path : DefaultEscudo;

    /// <summary>Indica se existe um arquivo de escudo para o nome informado.</summary>
    public bool HasEscudo(string? nome) => TryGetEscudo(nome, out _);

    public bool TryGetEscudo(string? nome, out string path)
    {
        path = DefaultEscudo;
        if (string.IsNullOrWhiteSpace(nome))
            return false;

        if (ObterMapa().TryGetValue(Normalizar(nome), out var encontrado))
        {
            path = encontrado;
            return true;
        }

        return false;
    }

    private Dictionary<string, string> ObterMapa()
    {
        var webroot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webroot))
            return _mapa;

        var dir = Path.Combine(webroot, "images", "escudos");

        DateTime mtime = DateTime.MinValue;
        try
        {
            if (Directory.Exists(dir))
                mtime = Directory.GetLastWriteTimeUtc(dir);
        }
        catch
        {
            // Se não der para inspecionar a pasta, mantém o cache atual.
        }

        lock (_lock)
        {
            if (_carregado && mtime == _ultimaLeitura)
                return _mapa;

            var novo = new Dictionary<string, string>();
            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (var arquivo in Directory.EnumerateFiles(dir))
                    {
                        var ext = Path.GetExtension(arquivo);
                        if (!Extensoes.Contains(ext, StringComparer.OrdinalIgnoreCase))
                            continue;

                        var nome = Path.GetFileNameWithoutExtension(arquivo);
                        // "escudo" é o placeholder padrão — não é um time.
                        if (string.Equals(nome, "escudo", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var chave = Normalizar(nome);
                        if (!novo.ContainsKey(chave))
                            novo[chave] = "/images/escudos/" + Uri.EscapeDataString(Path.GetFileName(arquivo));
                    }
                }
            }
            catch
            {
                // Em caso de falha de I/O, retorna o que já havia (ou vazio).
            }

            _mapa = novo;
            _ultimaLeitura = mtime;
            _carregado = true;
            return _mapa;
        }
    }

    private static string Normalizar(string valor)
    {
        var decomposto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var ch in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
