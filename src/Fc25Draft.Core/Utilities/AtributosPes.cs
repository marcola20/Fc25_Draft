using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Catálogo dos atributos do PES 2021 com os nomes da versão em português do jogo.
/// A ordem e os grupos são os da tela de atributos do jogo.
/// </summary>
public static class AtributosPes
{
    public const int Minimo = 40;
    public const int Maximo = 99;

    public record Atributo(string Grupo, string Nome, Func<PlayerAtributosDto, int> Get, Action<PlayerAtributosDto, int> Set);

    public const string GrupoGoleiro = "Goleiro";

    public static readonly IReadOnlyList<Atributo> Todos =
    [
        new("Ataque", "Talento Ofensivo", a => a.TalentoOfensivo, (a, v) => a.TalentoOfensivo = v),
        new("Ataque", "Controle de Bola", a => a.ControleDeBola, (a, v) => a.ControleDeBola = v),
        new("Ataque", "Drible", a => a.Drible, (a, v) => a.Drible = v),
        new("Ataque", "Condução Firme", a => a.ConducaoFirme, (a, v) => a.ConducaoFirme = v),
        new("Ataque", "Passe Rasteiro", a => a.PasseRasteiro, (a, v) => a.PasseRasteiro = v),
        new("Ataque", "Passe Alto", a => a.PasseAlto, (a, v) => a.PasseAlto = v),
        new("Ataque", "Finalização", a => a.Finalizacao, (a, v) => a.Finalizacao = v),
        new("Ataque", "Cabeceio", a => a.Cabeceio, (a, v) => a.Cabeceio = v),
        new("Ataque", "Bola Parada", a => a.BolaParada, (a, v) => a.BolaParada = v),
        new("Ataque", "Curva", a => a.Curva, (a, v) => a.Curva = v),

        new("Físico", "Velocidade", a => a.Velocidade, (a, v) => a.Velocidade = v),
        new("Físico", "Aceleração", a => a.Aceleracao, (a, v) => a.Aceleracao = v),
        new("Físico", "Força do Chute", a => a.ForcaDoChute, (a, v) => a.ForcaDoChute = v),
        new("Físico", "Impulsão", a => a.Impulsao, (a, v) => a.Impulsao = v),
        new("Físico", "Contato Físico", a => a.ContatoFisico, (a, v) => a.ContatoFisico = v),
        new("Físico", "Equilíbrio", a => a.Equilibrio, (a, v) => a.Equilibrio = v),
        new("Físico", "Resistência", a => a.Resistencia, (a, v) => a.Resistencia = v),

        new("Defesa", "Talento Defensivo", a => a.TalentoDefensivo, (a, v) => a.TalentoDefensivo = v),
        new("Defesa", "Desarme", a => a.Desarme, (a, v) => a.Desarme = v),
        new("Defesa", "Agressividade", a => a.Agressividade, (a, v) => a.Agressividade = v),

        new(GrupoGoleiro, "Talento de Goleiro", a => a.TalentoDeGoleiro, (a, v) => a.TalentoDeGoleiro = v),
        new(GrupoGoleiro, "Firmeza", a => a.FirmezaDoGoleiro, (a, v) => a.FirmezaDoGoleiro = v),
        new(GrupoGoleiro, "Afastamento", a => a.AfastamentoDoGoleiro, (a, v) => a.AfastamentoDoGoleiro = v),
        new(GrupoGoleiro, "Reflexos", a => a.ReflexosDoGoleiro, (a, v) => a.ReflexosDoGoleiro = v),
        new(GrupoGoleiro, "Alcance", a => a.AlcanceDoGoleiro, (a, v) => a.AlcanceDoGoleiro = v),
    ];

    public static IEnumerable<IGrouping<string, Atributo>> PorGrupo => Todos.GroupBy(a => a.Grupo);

    /// <summary>Classe CSS da faixa de valor, como as cores do jogo.</summary>
    public static string Faixa(int valor) => valor switch
    {
        >= 90 => "atr-90",
        >= 80 => "atr-80",
        >= 70 => "atr-70",
        _ => "atr-baixo",
    };

    /// <summary>Mensagem do primeiro valor inválido, ou null se está tudo certo.</summary>
    public static string? Validar(PlayerAtributosDto dto)
    {
        foreach (var a in Todos)
        {
            var v = a.Get(dto);
            if (v is < Minimo or > Maximo)
                return $"{a.Nome} deve estar entre {Minimo} e {Maximo}.";
        }
        if (dto.Altura is < 140 or > 210) return "Altura deve estar entre 140 e 210 cm.";
        if (dto.Peso is < 40 or > 125) return "Peso deve estar entre 40 e 125 kg.";
        if (dto.EstiloDeJogo is { } estilo && (estilo < 0 || estilo >= HabilidadesPes.EstilosDeJogo.Count))
            return "Estilo de jogo inválido.";
        if (dto.Condicao is < 1 or > 8) return "Condição deve estar entre 1 e 8.";
        if (dto.ResistenciaLesao is < 1 or > 3) return "Resistência a lesão deve estar entre 1 e 3.";
        if (dto.PeFracoUso is < 1 or > 4 || dto.PeFracoPrecisao is < 1 or > 4) return "Pé fraco deve estar entre 1 e 4.";
        if (dto.Posicoes is { } p && (p.Length != HabilidadesPes.Posicoes.Count || p.Any(c => c is not ('A' or 'B' or 'C' or '-'))))
            return "Notas de posição inválidas.";
        return null;
    }

    public static PlayerAtributosDto ParaDto(PlayerAtributos e) => new()
    {
        PesId = e.PesId, Altura = e.Altura, Peso = e.Peso, PernaBoa = e.PernaBoa,
        EstiloDeJogo = e.EstiloDeJogo, Habilidades = e.Habilidades, EstilosIa = e.EstilosIa, Posicoes = e.Posicoes,
        Condicao = e.Condicao, ResistenciaLesao = e.ResistenciaLesao, PeFracoUso = e.PeFracoUso,
        PeFracoPrecisao = e.PeFracoPrecisao,
        TalentoOfensivo = e.TalentoOfensivo, ControleDeBola = e.ControleDeBola, Drible = e.Drible,
        ConducaoFirme = e.ConducaoFirme, PasseRasteiro = e.PasseRasteiro, PasseAlto = e.PasseAlto,
        Finalizacao = e.Finalizacao, Cabeceio = e.Cabeceio, BolaParada = e.BolaParada, Curva = e.Curva,
        Velocidade = e.Velocidade, Aceleracao = e.Aceleracao, ForcaDoChute = e.ForcaDoChute, Impulsao = e.Impulsao,
        ContatoFisico = e.ContatoFisico, Equilibrio = e.Equilibrio, Resistencia = e.Resistencia,
        TalentoDefensivo = e.TalentoDefensivo, Desarme = e.Desarme, Agressividade = e.Agressividade,
        TalentoDeGoleiro = e.TalentoDeGoleiro, FirmezaDoGoleiro = e.FirmezaDoGoleiro,
        AfastamentoDoGoleiro = e.AfastamentoDoGoleiro, ReflexosDoGoleiro = e.ReflexosDoGoleiro,
        AlcanceDoGoleiro = e.AlcanceDoGoleiro,
    };

    public static void Aplicar(PlayerAtributosDto d, PlayerAtributos e)
    {
        e.PesId = d.PesId; e.Altura = d.Altura; e.Peso = d.Peso; e.PernaBoa = d.PernaBoa;
        e.EstiloDeJogo = d.EstiloDeJogo; e.Habilidades = d.Habilidades; e.EstilosIa = d.EstilosIa; e.Posicoes = d.Posicoes;
        e.Condicao = d.Condicao; e.ResistenciaLesao = d.ResistenciaLesao; e.PeFracoUso = d.PeFracoUso;
        e.PeFracoPrecisao = d.PeFracoPrecisao;
        e.TalentoOfensivo = d.TalentoOfensivo; e.ControleDeBola = d.ControleDeBola; e.Drible = d.Drible;
        e.ConducaoFirme = d.ConducaoFirme; e.PasseRasteiro = d.PasseRasteiro; e.PasseAlto = d.PasseAlto;
        e.Finalizacao = d.Finalizacao; e.Cabeceio = d.Cabeceio; e.BolaParada = d.BolaParada; e.Curva = d.Curva;
        e.Velocidade = d.Velocidade; e.Aceleracao = d.Aceleracao; e.ForcaDoChute = d.ForcaDoChute; e.Impulsao = d.Impulsao;
        e.ContatoFisico = d.ContatoFisico; e.Equilibrio = d.Equilibrio; e.Resistencia = d.Resistencia;
        e.TalentoDefensivo = d.TalentoDefensivo; e.Desarme = d.Desarme; e.Agressividade = d.Agressividade;
        e.TalentoDeGoleiro = d.TalentoDeGoleiro; e.FirmezaDoGoleiro = d.FirmezaDoGoleiro;
        e.AfastamentoDoGoleiro = d.AfastamentoDoGoleiro; e.ReflexosDoGoleiro = d.ReflexosDoGoleiro;
        e.AlcanceDoGoleiro = d.AlcanceDoGoleiro;
    }
}
