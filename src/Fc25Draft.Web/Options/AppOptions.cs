namespace Fc25Draft.Web.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public bool EnableDevSeed { get; set; }

    /// <summary>
    /// Feature flag que controla a experiência de navegação/layout de 2025.
    /// </summary>
    public bool EnableNewNavigationLayout { get; set; } = true;
}
