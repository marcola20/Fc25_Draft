using System;
using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Web.Models.Navigation;

namespace Fc25Draft.Web.Services;

public class LayoutNavigationService
{
    private static readonly IReadOnlyList<MenuGroup> MenuDefinition = new List<MenuGroup>
    {
        new("Início", new List<MenuItem>
        {
            new("Central CBFV", "/home", "oi oi-home", matchPrefix: true)
        }),
        new("Times", new List<MenuItem>
        {
            new("Elencos", "/times/elencos", "oi oi-people")
        }),
        new("Draft", new List<MenuItem>
        {
            new("Controle do Draft", "/draft/controle", "oi oi-flag", matchPrefix: true),
            new("Informações do Draft", "/draft/info", "oi oi-document")
        }),
        new("Mercado de Transferências", new List<MenuItem>
        {
            new("Mercado", "/mercado", "oi oi-cart", matchPrefix: true),
            new("Negociações", "/mercado/negociacoes", "oi oi-loop"),
            new("Histórico de Mercado", "/mercado/historico", "oi oi-clipboard"),
            new("Ciclos do Mercado", "/mercado/ciclos", "oi oi-calendar", requiredRole: "Admin")
        }),
        new("Admin", new List<MenuItem>
        {
            new("Gerenciar Ciclos", "/admin/ciclos", "oi oi-cog", requiredRole: "Admin"),
            new("Gerar Itens", "/admin/itens/gerar", "oi oi-plus", requiredRole: "Admin"),
            new("Histórico de Transferências", "/admin/transferencias/historico", "oi oi-transfer", requiredRole: "Admin"),
            new("Configurações", "/admin/config", "oi oi-wrench", requiredRole: "Admin", isOptional: true)
        }, requiredRole: "Admin")
    };

    private static readonly Dictionary<string, PageDefinition> PageDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = new PageDefinition
        {
            Route = "/",
            Title = "Central CBFV",
            Subtitle = "Visão geral das áreas principais",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início")
            }
        },
        ["/home"] = new PageDefinition
        {
            Route = "/home",
            Title = "Central CBFV",
            Subtitle = "Visão geral das áreas principais",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início")
            }
        },
        ["/times/elencos"] = CreateDefinition("Elencos", "Times", "/times/elencos"),
        ["/teams/roster"] = CreateDefinition("Elencos", "Times", "/times/elencos"),
        ["/draft/controle"] = CreateDefinition("Controle do Draft", "Draft", "/draft/controle"),
        ["/draft"] = CreateDefinition("Controle do Draft", "Draft", "/draft/controle"),
        ["/draft/info"] = CreateDefinition("Informações do Draft", "Draft", "/draft/info"),
        ["/drafts/manage"] = CreateDefinition("Informações do Draft", "Draft", "/draft/info"),
        ["/mercado"] = CreateDefinition("Mercado de Transferências", "Mercado", "/mercado"),
        ["/market"] = CreateDefinition("Mercado de Transferências", "Mercado", "/mercado"),
        ["/mercado/negociacoes"] = CreateDefinition("Negociações", "Mercado", "/mercado/negociacoes"),
        ["/admin/negociacoes"] = CreateDefinition("Negociações", "Mercado", "/mercado/negociacoes"),
        ["/mercado/historico"] = CreateDefinition("Histórico de Mercado", "Mercado", "/mercado/historico"),
        ["/market/historico"] = CreateDefinition("Histórico de Mercado", "Mercado", "/mercado/historico"),
        ["/market/transfers"] = CreateDefinition("Histórico de Mercado", "Mercado", "/mercado/historico"),
        ["/mercado/ciclos"] = CreateDefinition("Ciclos do Mercado", "Mercado", "/mercado/ciclos", true),
        ["/admin/mercado/ciclos"] = CreateDefinition("Ciclos do Mercado", "Mercado", "/mercado/ciclos", true),
        ["/admin/ciclos"] = CreateDefinition("Gerenciar Ciclos", "Admin", "/admin/ciclos", true),
        ["/admin/itens/gerar"] = CreateDefinition("Gerar Itens", "Admin", "/admin/itens/gerar", true),
        ["/admin/transferencias/historico"] = CreateDefinition("Histórico de Transferências", "Admin", "/admin/transferencias/historico", true),
        ["/admin/mercado/historico"] = CreateDefinition("Histórico de Transferências", "Admin", "/admin/transferencias/historico", true),
        ["/admin/config"] = CreateDefinition("Configurações", "Admin", "/admin/config", true)
    };

    public IReadOnlyList<MenuGroup> BuildMenu(bool isAdmin)
    {
        var groups = new List<MenuGroup>();

        foreach (var group in MenuDefinition)
        {
            if (!IsRoleAllowed(group.RequiredRole, isAdmin))
            {
                continue;
            }

            var filteredItems = group.Items
                .Where(item => IsRoleAllowed(item.RequiredRole, isAdmin))
                .ToList();

            if (filteredItems.Count == 0)
            {
                continue;
            }

            groups.Add(new MenuGroup(group.Title, filteredItems, group.RequiredRole, group.CollapseByDefault));
        }

        return groups;
    }

    public PageDefinition? GetPageDefinition(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        if (!route.StartsWith('/'))
        {
            route = "/" + route;
        }

        if (PageDefinitions.TryGetValue(route, out var definition))
        {
            return definition;
        }

        return null;
    }

    private static bool IsRoleAllowed(string? requiredRole, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(requiredRole))
        {
            return true;
        }

        return requiredRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) && isAdmin;
    }

    private static PageDefinition CreateDefinition(string title, string section, string route, bool admin = false)
    {
        var breadcrumbs = new List<BreadcrumbSegment>
        {
            new("Início", "/home"),
            new(section, GetSectionRoute(section, admin))
        };

        breadcrumbs.Add(new BreadcrumbSegment(title));

        return new PageDefinition
        {
            Route = route,
            Title = title,
            Breadcrumbs = breadcrumbs
        };
    }

    private static string? GetSectionRoute(string section, bool admin)
    {
        return section switch
        {
            "Times" => "/times/elencos",
            "Draft" => "/draft/controle",
            "Mercado" => "/mercado",
            "Admin" when admin => "/admin/ciclos",
            "Admin" => "/home",
            _ => null
        };
    }
}
