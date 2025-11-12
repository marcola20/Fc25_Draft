using System.Collections.Generic;

namespace Fc25Draft.Web.Models.Navigation;

public record MenuGroup(
    string Title,
    IReadOnlyCollection<MenuItem> Items,
    string? RequiredRole = null,
    bool CollapseByDefault = false);
