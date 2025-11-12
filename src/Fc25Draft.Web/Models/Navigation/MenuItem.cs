namespace Fc25Draft.Web.Models.Navigation;

public record MenuItem(
    string Title,
    string Href,
    string Icon,
    string? RequiredRole = null,
    bool IsOptional = false,
    bool MatchPrefix = false);
