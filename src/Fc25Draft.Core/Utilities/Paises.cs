namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Países dos jogadores. O PES guarda a nacionalidade como um código de 9 bits; a base embutida
/// (pes-jogadores.json.gz, campo "na") traz esse código e o site grava o nome do país no jogador.
/// Os códigos seguem a ordem alfabética em inglês dentro de cada continente do jogo (Ásia, África, Américas,
/// Oceania, Europa); conferidos com os elencos das seleções e com jogadores conhecidos de cada país.
/// A bandeira vem do código ISO (imagem do flagcdn.com).
/// </summary>
public static class Paises
{
    public sealed record Pais(int CodigoPes, string Nome, string Iso);

    public static readonly IReadOnlyList<Pais> Todos =
    [
        // Ásia
        new(2, "Bahrein", "bh"), new(7, "China", "cn"), new(9, "Índia", "in"), new(10, "Indonésia", "id"),
        new(11, "Irã", "ir"), new(12, "Iraque", "iq"), new(13, "Japão", "jp"), new(14, "Jordânia", "jo"),
        new(15, "Coreia do Norte", "kp"), new(16, "Coreia do Sul", "kr"), new(17, "Kuwait", "kw"),
        new(19, "Líbano", "lb"), new(21, "Malásia", "my"), new(26, "Omã", "om"), new(28, "Palestina", "ps"),
        new(29, "Filipinas", "ph"), new(30, "Catar", "qa"), new(31, "Arábia Saudita", "sa"), new(32, "Singapura", "sg"),
        new(34, "Síria", "sy"), new(36, "Tailândia", "th"), new(37, "Emirados Árabes", "ae"), new(38, "Vietnã", "vn"),
        // África
        new(44, "Argélia", "dz"), new(45, "Angola", "ao"), new(46, "Benin", "bj"), new(48, "Burkina Faso", "bf"),
        new(49, "Burundi", "bi"), new(50, "Camarões", "cm"), new(51, "Cabo Verde", "cv"),
        new(52, "República Centro-Africana", "cf"), new(54, "Comores", "km"), new(55, "RD Congo", "cd"),
        new(56, "Costa do Marfim", "ci"), new(58, "Egito", "eg"), new(59, "Guiné Equatorial", "gq"),
        new(62, "Gabão", "ga"), new(63, "Gâmbia", "gm"), new(64, "Gana", "gh"), new(65, "Guiné", "gn"),
        new(66, "Guiné-Bissau", "gw"), new(67, "Quênia", "ke"), new(69, "Libéria", "lr"), new(71, "Madagascar", "mg"),
        new(73, "Mali", "ml"), new(74, "Mauritânia", "mr"), new(76, "Marrocos", "ma"), new(77, "Moçambique", "mz"),
        new(78, "Namíbia", "na"), new(80, "Nigéria", "ng"), new(83, "Senegal", "sn"), new(85, "Serra Leoa", "sl"),
        new(87, "África do Sul", "za"), new(88, "Sudão", "sd"), new(91, "Togo", "tg"), new(92, "Tunísia", "tn"),
        new(93, "Uganda", "ug"), new(94, "Zâmbia", "zm"), new(95, "Zimbábue", "zw"), new(98, "Congo", "cg"),
        // América do Norte, Central e Caribe
        new(110, "Canadá", "ca"), new(112, "Costa Rica", "cr"), new(116, "El Salvador", "sv"),
        new(118, "Guadalupe", "gp"), new(119, "Guatemala", "gt"), new(120, "Haiti", "ht"), new(121, "Honduras", "hn"),
        new(122, "Jamaica", "jm"), new(123, "Martinica", "mq"), new(124, "México", "mx"), new(128, "Panamá", "pa"),
        new(129, "Porto Rico", "pr"), new(133, "Trinidad e Tobago", "tt"), new(135, "Estados Unidos", "us"),
        new(140, "Curaçao", "cw"),
        // América do Sul
        new(144, "Argentina", "ar"), new(145, "Bolívia", "bo"), new(146, "Brasil", "br"), new(147, "Chile", "cl"),
        new(148, "Colômbia", "co"), new(149, "Equador", "ec"), new(150, "Paraguai", "py"), new(151, "Peru", "pe"),
        new(152, "Uruguai", "uy"), new(153, "Venezuela", "ve"),
        // Oceania
        new(162, "Austrália", "au"), new(166, "Nova Zelândia", "nz"),
        // Europa
        new(189, "Israel", "il"), new(190, "Turquia", "tr"), new(191, "Albânia", "al"), new(192, "Andorra", "ad"),
        new(193, "Armênia", "am"), new(194, "Áustria", "at"), new(195, "Azerbaijão", "az"), new(196, "Belarus", "by"),
        new(197, "Bélgica", "be"), new(198, "Bósnia", "ba"), new(199, "Bulgária", "bg"), new(200, "Croácia", "hr"),
        new(201, "Chipre", "cy"), new(202, "República Tcheca", "cz"), new(203, "Dinamarca", "dk"),
        new(204, "Inglaterra", "gb-eng"), new(205, "Estônia", "ee"), new(206, "Ilhas Faroé", "fo"),
        new(207, "Finlândia", "fi"), new(208, "França", "fr"), new(209, "Geórgia", "ge"), new(210, "Alemanha", "de"),
        new(211, "Grécia", "gr"), new(212, "Hungria", "hu"), new(213, "Islândia", "is"), new(214, "Irlanda", "ie"),
        new(215, "Itália", "it"), new(216, "Cazaquistão", "kz"), new(217, "Letônia", "lv"),
        new(218, "Liechtenstein", "li"), new(219, "Lituânia", "lt"), new(220, "Luxemburgo", "lu"),
        new(221, "Macedônia do Norte", "mk"), new(222, "Malta", "mt"), new(223, "Moldávia", "md"),
        new(224, "Holanda", "nl"), new(225, "Irlanda do Norte", "gb-nir"), new(226, "Noruega", "no"),
        new(227, "Polônia", "pl"), new(228, "Portugal", "pt"), new(229, "Romênia", "ro"), new(230, "Rússia", "ru"),
        new(231, "San Marino", "sm"), new(232, "Escócia", "gb-sct"), new(234, "Eslováquia", "sk"),
        new(235, "Eslovênia", "si"), new(236, "Espanha", "es"), new(237, "Suécia", "se"), new(238, "Suíça", "ch"),
        new(239, "Ucrânia", "ua"), new(240, "Uzbequistão", "uz"), new(241, "País de Gales", "gb-wls"),
        new(245, "Gibraltar", "gi"), new(303, "Sérvia", "rs"), new(304, "Montenegro", "me"), new(311, "Kosovo", "xk"),
    ];

    private static readonly Dictionary<int, Pais> PorCodigo = Todos.ToDictionary(p => p.CodigoPes);
    private static readonly Dictionary<string, Pais> PorNome = Todos.ToDictionary(p => p.Nome, StringComparer.OrdinalIgnoreCase);

    /// <summary>Nomes em ordem alfabética, para escolher na edição do jogador.</summary>
    public static readonly IReadOnlyList<string> Nomes =
        Todos.Select(p => p.Nome).OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true)).ToList();

    public const int TamanhoNome = 40;

    /// <summary>Nome do país pelo código do PES; nulo se o código não é conhecido.</summary>
    public static string? DoCodigoPes(int? codigo) =>
        codigo is int c && PorCodigo.TryGetValue(c, out var p) ? p.Nome : null;

    /// <summary>Endereço da bandeira (20 px de largura); nulo para país desconhecido.</summary>
    public static string? Bandeira(string? pais) =>
        pais is not null && PorNome.TryGetValue(pais, out var p) ? $"https://flagcdn.com/w40/{p.Iso}.png" : null;
}
