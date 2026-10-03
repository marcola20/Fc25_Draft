using System;
using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Web.Models.Navigation;

namespace Fc25Draft.Web.Services;

public class LayoutNavigationService
{
    // Menu por assunto. O título de cada item vira o título do cabeçalho e o caminho "Início › grupo ›
    // página" das telas que não têm PageDefinition própria (ver GetPageDefinition).
    private static readonly IReadOnlyList<MenuGroup> MenuDefinition = new List<MenuGroup>
    {
        new("Início", new List<MenuItem>
        {
            new("Central CBFV", "/home", "oi oi-home", MatchPrefix: true),
            new("Plantão CBFV", "/plantao", "oi oi-bullhorn"),
            new("Minha Área", "/minha-area", "oi oi-person"),
            new("Manual do Técnico", "/manual", "oi oi-book")
        }),
        new("Competições", new List<MenuItem>
        {
            new("Liga", "/liga", "oi oi-list-rich", MatchPrefix: true),
            new("Bolão da rodada", "/bolao", "oi oi-target", MatchPrefix: true),
            new("Power Ranking", "/power-ranking", "oi oi-pulse"),
            new("Reta final", "/liga/reta-final", "oi oi-flag"),
            new("Simulação", "/liga/simulacao", "oi oi-calculator"),
            new("Sorteio da Copa", "/copa/sorteio", "oi oi-random"),
            new("Edições", "/liga/edicoes", "oi oi-calendar"),
            new("Formato da Competição", "/formato", "oi oi-grid-four-up"),
            new("Regulamento", "/regulamento", "oi oi-document"),
            new("Premiação", "/premiacao", "oi oi-dollar")
        }),
        new("Clubes e Jogadores", new List<MenuItem>
        {
            new("Elencos", "/times/elencos", "oi oi-people"),
            new("Jogadores", "/players", "oi oi-person"),
            new("Comparar Jogadores", "/jogadores/comparar", "oi oi-transfer"),
            new("Treinadores", "/treinadores", "oi oi-briefcase"),
            new("Valor de Elenco", "/times/caixa", "oi oi-dollar"),
            new("Como o overall é calculado", "/overall", "oi oi-calculator")
        }),
        new("Mercado", new List<MenuItem>
        {
            new("Leilão", "/mercado", "oi oi-cart"),
            new("Lista de Transferências", "/mercado/lista", "oi oi-tag"),
            new("Termômetro do Mercado", "/mercado/termometro", "oi oi-graph"),
            new("Histórico de Transferências", "/market/transfers", "oi oi-transfer", MatchPrefix: true),
            new("Histórico de Lances", "/mercado/historico", "oi oi-clock")
        }),
        new("Draft", new List<MenuItem>
        {
            new("Controle do Draft", "/draft/controle", "oi oi-flag", MatchPrefix: true),
            new("Informações do Draft", "/draft/info", "oi oi-document"),
            new("Pré-Draft", "/draft/pre-draft", "oi oi-star"),
            new("Jogadores Protegidos", "/draft/protecao", "oi oi-shield"),
            new("Escolha Automática", "/draft/automatico", "oi oi-list-rich"),
            new("Picks do Draft", "/picks", "oi oi-tag"),
            new("Loteria do Draft", "/loteria", "oi oi-random")
        }),
        new("Álbum", new List<MenuItem>
        {
            new("Álbum de Figurinhas", "/album", "oi oi-image"),
            new("Colecionadores", "/album/colecionadores", "oi oi-badge")
        }),
        new("História", new List<MenuItem>
        {
            new("Hall da Fama", "/hall-of-fame", "oi oi-badge"),
            new("Recordes", "/liga/recordes", "oi oi-star"),
            new("Ranking de Clubes", "/ranking-clubes", "oi oi-bar-chart")
        }),
        new("Admin · Temporada", new List<MenuItem>
        {
            new("Gerenciar Liga", "/admin/liga", "oi oi-wrench"),
            new("Virada de Temporada", "/admin/temporada", "oi oi-loop-circular"),
            new("Resumo da Rodada", "/admin/resumo-rodada", "oi oi-comment-square"),
            new("Gerenciar Hall da Fama", "/admin/hall-of-fame", "oi oi-badge"),
            new("Gerenciar Premiação", "/admin/premiacao", "oi oi-dollar"),
            new("Gerenciar Regulamento", "/admin/regulamento", "oi oi-document")
        }, RequiredRole: "Admin", CollapseByDefault: true),
        new("Admin · Mercado", new List<MenuItem>
        {
            new("Negociações", "/admin/negociacoes", "oi oi-loop"),
            new("Ciclos do Leilão", "/admin/ciclos", "oi oi-cog"),
            new("Log de Ações", "/admin/log-acoes", "oi oi-list")
        }, RequiredRole: "Admin", CollapseByDefault: true),
        new("Admin · Draft", new List<MenuItem>
        {
            new("Rodar a Loteria", "/admin/loteria", "oi oi-random"),
            new("Draft de Expansão", "/admin/draft-expansao", "oi oi-plus"),
            new("Listas do Pré-Draft", "/admin/pre-draft", "oi oi-star")
        }, RequiredRole: "Admin", CollapseByDefault: true),
        new("Admin · Álbum", new List<MenuItem>
        {
            new("Lançar o Álbum", "/admin/album", "oi oi-image"),
            new("Dar Pacotes", "/admin/album/pacotes", "oi oi-box"),
            new("Figurinhas sem Foto", "/admin/album/sem-foto", "oi oi-camera-slr")
        }, RequiredRole: "Admin", CollapseByDefault: true),
        new("Admin · Sistema", new List<MenuItem>
        {
            new("Gerenciar Treinadores", "/admin/treinadores", "oi oi-person"),
            new("Cadastro de Times", "/teams", "oi oi-plus"),
            new("Gerenciar Escalações", "/admin/escalacoes", "oi oi-people"),
            new("Configurações", "/admin/configuracoes", "oi oi-cog"),
            new("Administradores", "/admin/administradores", "oi oi-key", RequiredRole: "AdminPrincipal")
        }, RequiredRole: "Admin", CollapseByDefault: true)
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
        ["/teams"] = CreateDefinition("Cadastro de Times", "Times", "/teams", true),
        ["/teams/edit"] = CreateDefinition("Cadastro de Times", "Times", "/teams", true),
        ["/teams/roster"] = CreateDefinition("Elencos", "Times", "/times/elencos"),
        ["/draft/controle"] = CreateDefinition("Controle do Draft", "Draft", "/draft/controle"),
        ["/draft"] = CreateDefinition("Controle do Draft", "Draft", "/draft/controle"),
        ["/draft/info"] = CreateDefinition("Informações do Draft", "Draft", "/draft/info"),
        ["/drafts/manage"] = CreateDefinition("Informações do Draft", "Draft", "/draft/info"),
        ["/mercado"] = CreateDefinition("Mercado de Transferências", "Mercado", "/mercado"),
        ["/mercado/lista"] = CreateDefinition("Lista de Transferências", "Mercado", "/mercado/lista"),
        ["/mercado/termometro"] = CreateDefinition("Termômetro do Mercado", "Mercado", "/mercado/termometro"),
        ["/market"] = CreateDefinition("Mercado de Transferências", "Mercado", "/mercado"),
        ["/players"] = CreateDefinition("Jogadores", "Mercado", "/players"),
        ["/jogadores/comparar"] = CreateDefinition("Comparar Jogadores", "Mercado", "/jogadores/comparar"),
        ["/overall"] = CreateDefinition("Como o overall é calculado", "Mercado", "/overall"),
        ["/mercado/negociacoes"] = CreateDefinition("Negociações", "Admin", "/mercado/negociacoes", true),
        ["/admin/negociacoes"] = CreateDefinition("Negociações", "Admin", "/admin/negociacoes", true),
        ["/mercado/historico"] = CreateDefinition("Histórico de Mercado", "Admin", "/mercado/historico", true),
        ["/market/historico"] = CreateDefinition("Histórico de Mercado", "Admin", "/market/historico", true),
        ["/market/transfers"] = CreateDefinition("Histórico de Transferências", "Mercado", "/market/transfers"),
        ["/times/caixa"] = CreateDefinition("Valor de Elenco", "Times", "/times/caixa"),
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
            Subtitle = "Prêmios de cada temporada",
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
            Title = "Rodar a Loteria",
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
            Subtitle = "As regras de cada temporada",
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
        ["/admin/regulamento"] = new PageDefinition
        {
            Route = "/admin/regulamento",
            Title = "Gerenciar Regulamento",
            Subtitle = "Texto por temporada — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Regulamento")
            }
        },
        ["/admin/premiacao"] = new PageDefinition
        {
            Route = "/admin/premiacao",
            Title = "Gerenciar Premiação",
            Subtitle = "Valores por temporada e pagamento no caixa — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Premiação")
            }
        },
        ["/liga/edicoes"] = new PageDefinition
        {
            Route = "/liga/edicoes",
            Title = "Edições",
            Subtitle = "Todas as temporadas e competições",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga", "/liga"),
                new("Edições")
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
        ["/liga/reta-final"] = new PageDefinition
        {
            Route = "/liga/reta-final",
            Title = "Reta final",
            Subtitle = "Quem ainda pode o quê",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga", "/liga"),
                new("Reta final")
            }
        },
        ["/liga/simulacao"] = new PageDefinition
        {
            Route = "/liga/simulacao",
            Title = "Simulação",
            Subtitle = "Cenários de classificação",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga", "/liga"),
                new("Simulação")
            }
        },
        ["/draft/protecao"] = CreateDefinition("Jogadores Protegidos", "Draft", "/draft/protecao"),
        ["/draft/automatico"] = CreateDefinition("Escolha Automática", "Draft", "/draft/automatico"),
        ["/copa/sorteio"] = new PageDefinition
        {
            Route = "/copa/sorteio",
            Title = "Sorteio da Copa",
            Subtitle = "Potes e grupos, ao vivo",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga", "/liga"),
                new("Sorteio da Copa")
            }
        },
        ["/admin/temporada"] = new PageDefinition
        {
            Route = "/admin/temporada",
            Title = "Virada de Temporada",
            Subtitle = "Playoff de acesso e próxima temporada — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Virada de Temporada")
            }
        },
        ["/admin/draft-expansao"] = new PageDefinition
        {
            Route = "/admin/draft-expansao",
            Title = "Draft de Expansão",
            Subtitle = "Proteções, escolhas e compensações — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Draft de Expansão")
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
        ["/admin/resumo-rodada"] = new PageDefinition
        {
            Route = "/admin/resumo-rodada",
            Title = "Resumo da Rodada",
            Subtitle = "O texto pronto para colar no grupo",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Resumo da rodada")
            }
        },
        ["/bolao"] = new PageDefinition
        {
            Route = "/bolao",
            Title = "Bolão da rodada",
            Subtitle = "Chute os placares antes da bola rolar",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Bolão")
            }
        },
        ["/bolao/ranking"] = new PageDefinition
        {
            Route = "/bolao/ranking",
            Title = "Ranking do bolão",
            Subtitle = "Quem chuta melhor a rodada da CBFV",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Bolão", "/bolao"),
                new("Ranking")
            }
        },
        ["/treinadores"] = new PageDefinition
        {
            Route = "/treinadores",
            Title = "Treinadores",
            Subtitle = "Quem comanda cada clube da liga",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Treinadores")
            }
        },
        ["/admin/log-acoes"] = new PageDefinition
        {
            Route = "/admin/log-acoes",
            Title = "Log de Ações",
            Subtitle = "O que a organização fez no mercado",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Log de ações")
            }
        },
        ["/admin/administradores"] = new PageDefinition
        {
            Route = "/admin/administradores",
            Title = "Administradores",
            Subtitle = "Quem manda na liga — só o administrador principal vê esta página",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Administradores")
            }
        },
        ["/admin/treinadores"] = new PageDefinition
        {
            Route = "/admin/treinadores",
            Title = "Gerenciar Treinadores",
            Subtitle = "Cadastro das pessoas da liga e das passagens pelos clubes",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Treinadores")
            }
        },
        ["/hall-of-fame"] = new PageDefinition
        {
            Route = "/hall-of-fame",
            Title = "Hall da Fama",
            Subtitle = "Os campeões de todas as competições",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Hall da Fama")
            }
        },
        ["/liga/recordes"] = new PageDefinition
        {
            Route = "/liga/recordes",
            Title = "Recordes",
            Subtitle = "Todas as temporadas, atualizado a cada jogo",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Recordes")
            }
        },
        ["/minha-area"] = new PageDefinition
        {
            Route = "/minha-area",
            Title = "Minha Área",
            Subtitle = "Tudo do seu time num lugar só",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Minha Área")
            }
        },
        ["/plantao"] = new PageDefinition
        {
            Route = "/plantao",
            Title = "Plantão CBFV",
            Subtitle = "Tudo o que acontece na liga, atualizado sozinho",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Plantão CBFV")
            }
        },
        ["/ranking-clubes"] = new PageDefinition
        {
            Route = "/ranking-clubes",
            Title = "Ranking de Clubes",
            Subtitle = "Todas as temporadas, atualizado a cada jogo",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Ranking de Clubes")
            }
        },
        ["/power-ranking"] = new PageDefinition
        {
            Route = "/power-ranking",
            Title = "Power Ranking",
            Subtitle = "Quem está mais forte agora: elenco, rating e forma",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Liga"),
                new("Power Ranking")
            }
        },
        ["/album"] = new PageDefinition
        {
            Route = "/album",
            Title = "Álbum de Figurinhas",
            Subtitle = "Abra pacotes, cole e complete as páginas dos clubes"
        },
        ["/album/colecionadores"] = new PageDefinition
        {
            Route = "/album/colecionadores",
            Title = "Colecionadores",
            Subtitle = "Quem está mais perto de completar o álbum"
        },
        ["/admin/album"] = new PageDefinition
        {
            Route = "/admin/album",
            Title = "Lançar o Álbum",
            Subtitle = "Álbum de figurinhas da temporada e as lendárias — Admin"
        },
        ["/admin/album/pacotes"] = new PageDefinition
        {
            Route = "/admin/album/pacotes",
            Title = "Dar Pacotes",
            Subtitle = "Pacotes do álbum para testes e premiação — Admin"
        },
        ["/admin/album/sem-foto"] = new PageDefinition
        {
            Route = "/admin/album/sem-foto",
            Title = "Figurinhas sem Foto",
            Subtitle = "Jogadores do álbum que ainda saem com a silhueta — Admin"
        },
        ["/admin/hall-of-fame"] = new PageDefinition
        {
            Route = "/admin/hall-of-fame",
            Title = "Gerenciar Hall da Fama",
            Subtitle = "Administração dos campeões — Admin",
            Breadcrumbs = new List<BreadcrumbSegment>
            {
                new("Início", "/home"),
                new("Admin"),
                new("Hall da Fama")
            }
        }
    };

    public IReadOnlyList<MenuGroup> BuildMenu(bool isAdmin, bool isPrincipal = false)
    {
        var groups = new List<MenuGroup>();

        foreach (var group in MenuDefinition)
        {
            if (!IsRoleAllowed(group.RequiredRole, isAdmin, isPrincipal))
            {
                continue;
            }

            var filteredItems = group.Items
                .Where(item => IsRoleAllowed(item.RequiredRole, isAdmin, isPrincipal))
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

        PageDefinitions.TryGetValue(route, out var definition);

        // Página do menu: o caminho segue o grupo do menu e, sem definição própria, o título vem do item.
        var doMenu = MenuDefinition
            .SelectMany(g => g.Items.Select(i => (Grupo: g.Title, Item: i)))
            .FirstOrDefault(x => string.Equals(x.Item.Href, route, StringComparison.OrdinalIgnoreCase));
        if (doMenu.Item is null)
        {
            return definition;
        }

        var titulo = string.IsNullOrWhiteSpace(definition?.Title) ? doMenu.Item.Title : definition.Title;
        return new PageDefinition
        {
            Route = route,
            Title = titulo,
            Subtitle = definition?.Subtitle,
            Breadcrumbs = route.Equals("/home", StringComparison.OrdinalIgnoreCase)
                ? new List<BreadcrumbSegment> { new("Início") }
                : new List<BreadcrumbSegment> { new("Início", "/home"), new(doMenu.Grupo), new(titulo) }
        };
    }

    /// <summary>
    /// Título e descrição da prévia do link de uma rota (WhatsApp etc.): a definição da página ou, sem
    /// ela, o item do menu com esse endereço. Nulo quando a rota não é conhecida.
    /// </summary>
    public (string Titulo, string? Descricao)? DescreverRota(string? route)
    {
        var definicao = GetPageDefinition(route);
        return definicao is null || string.IsNullOrWhiteSpace(definicao.Title)
            ? null
            : (definicao.Title, definicao.Subtitle);
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

    private static bool IsRoleAllowed(string? requiredRole, bool isAdmin, bool isPrincipal)
    {
        if (string.IsNullOrWhiteSpace(requiredRole))
        {
            return true;
        }

        if (requiredRole.Equals("AdminPrincipal", StringComparison.OrdinalIgnoreCase))
        {
            return isPrincipal;
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
