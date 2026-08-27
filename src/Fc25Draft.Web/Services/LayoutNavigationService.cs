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
            new("Central CBFV", "/home", "oi oi-home", MatchPrefix: true)
        }),
        new("Times", new List<MenuItem>
        {
            new("Elencos", "/times/elencos", "oi oi-people")
        }),
        new("Draft", new List<MenuItem>
        {
            new("Controle do Draft", "/draft/controle", "oi oi-flag", MatchPrefix: true),
            new("Informações do Draft", "/draft/info", "oi oi-document"),
            new("Picks do Draft", "/picks", "oi oi-tag"),
            new("Loteria do Draft", "/loteria", "oi oi-random")
        }),
        new("Mercado de Transferências", new List<MenuItem>
        {
            new("Mercado", "/mercado", "oi oi-cart", MatchPrefix: true),
            new("Jogadores", "/players", "oi oi-person"),
            new("Histórico de Transferências", "/market/transfers", "oi oi-transfer", MatchPrefix: true),
            new("Caixa dos Times", "/times/caixa", "oi oi-dollar")
        }),
        new("Liga", new List<MenuItem>
        {
            new("Liga", "/liga", "oi oi-list-rich", MatchPrefix: true),
            new("Hall of Fame", "/hall-of-fame", "oi oi-badge"),
            new("Formato da Competição", "/formato", "oi oi-grid-four-up"),
            new("Premiação", "/premiacao", "oi oi-dollar"),
            new("Regulamento", "/regulamento", "oi oi-document")
        }),
        new("Admin", new List<MenuItem>
        {
            new("Negociações", "/admin/negociacoes", "oi oi-loop", RequiredRole: "Admin"),
            new("Gerenciar Ciclos", "/admin/ciclos", "oi oi-cog", RequiredRole: "Admin"),
            new("Gerenciar Escalações", "/admin/escalacoes", "oi oi-people", RequiredRole: "Admin"),
            new("Gerenciar Liga", "/admin/liga", "oi oi-wrench", RequiredRole: "Admin"),
            new("Gerenciar Hall of Fame", "/admin/hall-of-fame", "oi oi-badge", RequiredRole: "Admin"),
            new("Loteria do Draft", "/admin/loteria", "oi oi-random", RequiredRole: "Admin"),
            new("Configurações", "/admin/configuracoes", "oi oi-cog", RequiredRole: "Admin")
        }, RequiredRole: "Admin")
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
        ["/players"] = CreateDefinition("Jogadores", "Mercado", "/players"),
        ["/mercado/negociacoes"] = CreateDefinition("Negociações", "Admin", "/mercado/negociacoes", true),
        ["/admin/negociacoes"] = CreateDefinition("Negociações", "Admin", "/admin/negociacoes", true),
        ["/mercado/historico"] = CreateDefinition("Histórico de Mercado", "Admin", "/mercado/historico", true),
        ["/market/historico"] = CreateDefinition("Histórico de Mercado", "Admin", "/market/historico", true),
        ["/market/transfers"] = CreateDefinition("Histórico de Transferências", "Mercado", "/market/transfers"),
        ["/times/caixa"] = CreateDefinition("Caixa dos Times", "Times", "/times/caixa"),
        ["/admin/ciclos"] = CreateDefinition("Gerenciar Ciclos", "Admin", "/admin/ciclos", true),
        ["/admin/mercado/ciclos"] = CreateDefinition("Gerenciar Ciclos", "Admin", "/admin/mercado/ciclos", true),
        ["/mercado/ciclos"] = CreateDefinition("Gerenciar Ciclos", "Admin", "/mercado/ciclos", true),
        ["/admin/itens/gerar"] = CreateDefinition("Gerenciar Ciclos", "Admin", "/admin/itens/gerar", true),
        ["/admin/transferencias/historico"] = CreateDefinition("Histórico de Transferências", "Mercado", "/admin/transferencias/historico"),
        ["/admin/mercado/historico"] = CreateDefinition("Histórico de Mercado", "Admin", "/admin/mercado/historico", true),
        ["/premiacao"] = new PageDefinition
        {
            Route = "/premiacao",
            Title = "Premiação",
            Subtitle = "Liga CBFV Retro 2008/09",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Premiação")
            }
        },
        ["/picks"] = new PageDefinition
        {
            Route = "/picks",
            Title = "Picks do Draft",
            Subtitle = "Liga CBFV Retro 2008/09",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Draft", "/draft/controle"),
                new("Picks")
            }
        },
        ["/loteria"] = new PageDefinition
        {
            Route = "/loteria",
            Title = "Loteria do Draft",
            Subtitle = "Sorteio ao vivo das picks",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Draft", "/draft/controle"),
                new("Loteria")
            }
        },
        ["/admin/loteria"] = new PageDefinition
        {
            Route = "/admin/loteria",
            Title = "Loteria do Draft",
            Subtitle = "Controle do sorteio — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Loteria")
            }
        },
        ["/formato"] = new PageDefinition
        {
            Route = "/formato",
            Title = "Formato da Competição",
            Subtitle = "Liga CBFV Retro 2008/09",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Formato")
            }
        },
        ["/regulamento"] = new PageDefinition
        {
            Route = "/regulamento",
            Title = "Regulamento Oficial",
            Subtitle = "Liga CBFV Retro 2008/09",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Regulamento")
            }
        },
        ["/manual"] = new PageDefinition
        {
            Route = "/manual",
            Title = "Manual do Técnico",
            Subtitle = "Guia para novos técnicos e auxiliares",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Manual do Técnico")
            }
        },
        ["/liga"] = new PageDefinition
        {
            Route = "/liga",
            Title = "Liga",
            Subtitle = "Classificação, Mata-Mata e Estatísticas",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga")
            }
        },
        ["/liga/classificacao"] = new PageDefinition
        {
            Route = "/liga/classificacao",
            Title = "Classificação",
            Subtitle = "Tabela de Classificação",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Classificação")
            }
        },
        ["/liga/estatisticas"] = new PageDefinition
        {
            Route = "/liga/estatisticas",
            Title = "Estatísticas",
            Subtitle = "Artilheiros, Assistências e Cartões",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Estatísticas")
            }
        },
        ["/liga/mata-mata"] = new PageDefinition
        {
            Route = "/liga/mata-mata",
            Title = "Mata-Mata",
            Subtitle = "Chaveamento da Fase Final",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Mata-Mata")
            }
        },
        ["/admin/liga"] = new PageDefinition
        {
            Route = "/admin/liga",
            Title = "Gerenciar Liga",
            Subtitle = "Administração da Liga",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Liga")
            }
        },
        ["/admin/configuracoes"] = new PageDefinition
        {
            Route = "/admin/configuracoes",
            Title = "Configurações",
            Subtitle = "Ajustes do sistema — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Configurações")
            }
        },
        ["/hall-of-fame"] = new PageDefinition
        {
            Route = "/hall-of-fame",
            Title = "Hall of Fame",
            Subtitle = "Os campeões de todas as competições",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Hall of Fame")
            }
        },
        ["/admin/hall-of-fame"] = new PageDefinition
        {
            Route = "/admin/hall-of-fame",
            Title = "Gerenciar Hall of Fame",
            Subtitle = "Administração dos campeões — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Hall of Fame")
            }
        }
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

    public bool HasRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        if (!route.StartsWith('/'))
        {
            route = "/" + route;
        }

        return PageDefinitions.ContainsKey(route);
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
