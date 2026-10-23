using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Regras do álbum de figurinhas: como as páginas são montadas a partir dos elencos e como o pacotinho
/// é sorteado. Os números ficam aqui para ajustar num lugar só.
/// </summary>
public static class AlbumFigurinhas
{
    /// <summary>Figurinhas por pacote.</summary>
    public const int TamanhoPacote = 5;

    /// <summary>
    /// Figurinhas novas (que a pessoa ainda não tem) garantidas em cada pacote, enquanto faltar alguma da
    /// raridade da vaga ou abaixo: a 1ª sempre, a 2ª com <see cref="ChanceSegundaNova"/> e, depois dela, cada
    /// uma a mais com <see cref="ChanceDeMaisUmaNova"/> — 3 ou mais de vez em quando, sem virar regra.
    /// </summary>
    public const double ChanceSegundaNova = 0.9;

    public const double ChanceDeMaisUmaNova = 0.35;

    /// <summary>Chance de uma das vagas comuns (1 a 4) vir brilhante.</summary>
    public const double ChanceBrilhanteNaComum = 0.05;

    /// <summary>Chance da última vaga (a especial) vir lendária em vez de brilhante.</summary>
    public const double ChanceLendariaNaEspecial = 0.15;

    /// <summary>Maiores overalls de cada clube que viram brilhantes (além do escudo).</summary>
    public const int BrilhantesPorClube = 3;

    public const int MaximoLendarias = 10;

    /// <summary>Figurinhas de Lenda (aposentados) que o admin pode colocar em cada álbum.</summary>
    public const int MaximoLendasPorAlbum = 3;

    /// <summary>Texto da figurinha de Lenda quando o admin não escreve outro.</summary>
    public static string DestaqueDaLenda(int temporada) => $"Pendurou as chuteiras em {temporada}";

    /// <summary>Jogos com nota na temporada passada para entrar nas sugestões de lendária.</summary>
    public const int MinimoJogosParaSugestao = 3;

    public const int TamanhoDestaque = 80;

    /// <summary>Técnico ou auxiliar com passagem aberta no clube no lançamento.</summary>
    public sealed record TecnicoDoAlbum(Guid TreinadorId, string Nome, PapelTreinador Papel);

    public sealed record ClubeDoAlbum(Guid TeamId, string Nome, IReadOnlyList<TecnicoDoAlbum>? Tecnicos = null);

    public sealed record JogadorDoAlbum(int PlayerId, string Nome, int PositionId, int Overall, Guid TeamId);

    /// <summary>Uma figurinha montada, antes de virar entidade.</summary>
    public sealed record FigurinhaMontada(
        int Numero,
        TipoFigurinha Tipo,
        RaridadeFigurinha Raridade,
        Guid TeamId,
        int? PlayerId,
        string NomeImpresso,
        string? PosicaoSigla,
        int? Overall,
        string? Destaque,
        int Ordem,
        Guid? TreinadorId = null,
        PapelTreinador? Papel = null);

    /// <summary>
    /// Monta o álbum: uma página por clube, na ordem recebida, com o escudo, o técnico e o auxiliar (quem
    /// tem passagem aberta) e depois o elenco por posição (GOL, ZAG, LE… na ordem do <c>PositionType</c>) e,
    /// dentro da posição, por overall. Numeração corrida. A figurinha de técnico é sorteada como brilhante.
    /// </summary>
    public static IReadOnlyList<FigurinhaMontada> Montar(
        IReadOnlyList<ClubeDoAlbum> clubes,
        IReadOnlyList<JogadorDoAlbum> jogadores,
        IReadOnlyDictionary<int, string?> lendarias)
    {
        var resultado = new List<FigurinhaMontada>();
        var numero = 0;
        var porClube = jogadores.ToLookup(j => j.TeamId);

        foreach (var clube in clubes)
        {
            resultado.Add(new FigurinhaMontada(++numero, TipoFigurinha.Escudo, RaridadeFigurinha.Brilhante,
                clube.TeamId, null, clube.Nome, null, null, null, 0));

            var ordem = 0;
            foreach (var t in (clube.Tecnicos ?? Array.Empty<TecnicoDoAlbum>()).OrderBy(t => t.Papel))
            {
                resultado.Add(new FigurinhaMontada(++numero, TipoFigurinha.Treinador, RaridadeFigurinha.Brilhante,
                    clube.TeamId, null, t.Nome, t.Papel == PapelTreinador.Auxiliar ? "AUX" : "TÉC", null, null, ++ordem,
                    t.TreinadorId, t.Papel));
            }

            var elenco = porClube[clube.TeamId].ToList();
            var brilhantes = MaioresOveralls(elenco.Select(j => (j.PlayerId, j.Overall, j.Nome)));

            foreach (var j in elenco.OrderBy(j => j.PositionId).ThenByDescending(j => j.Overall).ThenBy(j => j.Nome, StringComparer.CurrentCulture))
            {
                var lendaria = lendarias.TryGetValue(j.PlayerId, out var destaque);
                var raridade = lendaria ? RaridadeFigurinha.Lendaria
                    : brilhantes.Contains(j.PlayerId) ? RaridadeFigurinha.Brilhante
                    : RaridadeFigurinha.Comum;

                resultado.Add(new FigurinhaMontada(++numero, TipoFigurinha.Jogador, raridade, clube.TeamId, j.PlayerId,
                    j.Nome, j.PositionId.ToPositionSigla(), j.Overall, lendaria ? Limpar(destaque) : null, ++ordem));
            }
        }

        return resultado;
    }

    // ---- Atualização do álbum ----

    /// <summary>Uma figurinha que já está no álbum (dentro ou fora), do jeito que a atualização precisa ver.</summary>
    public sealed record FigurinhaExistente(
        Guid FigurinhaId, TipoFigurinha Tipo, Guid TeamId, int? PlayerId, Guid? TreinadorId,
        string Nome, int? Overall, bool Lenda, bool Fora);

    /// <summary>Jogador que chega ao álbum: o elenco de hoje e a raridade que a figurinha nova ganha.</summary>
    public sealed record JogadorQueEntra(JogadorDoAlbum Jogador, RaridadeFigurinha Raridade);

    /// <summary>
    /// O que muda no álbum para ele ficar igual aos elencos e às comissões técnicas de hoje:
    /// <list type="bullet">
    /// <item><b>Mudam</b>: estão no álbum, mas em outro clube agora — a figurinha vai para a página nova (quem tem, continua tendo).</item>
    /// <item><b>Voltam</b>: tinham saído e estão de novo num clube do álbum — a mesma figurinha volta, na página do clube de hoje.</item>
    /// <item><b>Saem</b>: deixaram os clubes do álbum — a figurinha sai das páginas.</item>
    /// <item><b>Entram</b>: chegaram e nunca tiveram figurinha neste álbum — nasce uma nova, com número no fim.</item>
    /// </list>
    /// Escudos e Lendas não mudam nunca.
    /// </summary>
    public sealed record AtualizacaoDoAlbum(
        IReadOnlyList<(FigurinhaExistente Figurinha, Guid Para, PapelTreinador? Papel)> Mudam,
        IReadOnlyList<(FigurinhaExistente Figurinha, Guid Para, PapelTreinador? Papel)> Voltam,
        IReadOnlyList<FigurinhaExistente> Saem,
        IReadOnlyList<JogadorQueEntra> JogadoresEntram,
        IReadOnlyList<(Guid TeamId, TecnicoDoAlbum Tecnico)> TecnicosEntram)
    {
        public int Total => Mudam.Count + Voltam.Count + Saem.Count + JogadoresEntram.Count + TecnicosEntram.Count;
    }

    /// <summary>
    /// Compara o álbum com os elencos (<paramref name="jogadores"/>) e os técnicos de hoje dos clubes do álbum.
    /// Quem chega vira brilhante se ficar entre os <see cref="BrilhantesPorClube"/> maiores overalls do clube
    /// (contando o overall impresso de quem já está lá); as figurinhas que já existem não mudam de raridade.
    /// </summary>
    public static AtualizacaoDoAlbum PlanejarAtualizacao(
        IReadOnlyList<FigurinhaExistente> figurinhas,
        IReadOnlySet<Guid> clubesDoAlbum,
        IReadOnlyList<JogadorDoAlbum> jogadores,
        IReadOnlyDictionary<Guid, List<TecnicoDoAlbum>> tecnicos)
    {
        var mudam = new List<(FigurinhaExistente, Guid, PapelTreinador?)>();
        var voltam = new List<(FigurinhaExistente, Guid, PapelTreinador?)>();
        var saem = new List<FigurinhaExistente>();

        // Jogadores: o clube de hoje de quem está num elenco do álbum.
        var clubeDoJogador = jogadores.Where(j => clubesDoAlbum.Contains(j.TeamId))
            .GroupBy(j => j.PlayerId).ToDictionary(g => g.Key, g => g.First());
        var dosJogadores = figurinhas
            .Where(f => f.Tipo == TipoFigurinha.Jogador && !f.Lenda && f.PlayerId is not null)
            .GroupBy(f => f.PlayerId!.Value)
            // A mesma pessoa tem uma figurinha só; se houver duas, vale a que está no álbum.
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Fora).First());

        foreach (var (playerId, f) in dosJogadores)
            Comparar(f, clubeDoJogador.TryGetValue(playerId, out var hoje) ? hoje.TeamId : null, null);

        // Técnicos: a passagem aberta de hoje num clube do álbum.
        var clubeDoTecnico = tecnicos.Where(t => clubesDoAlbum.Contains(t.Key))
            .SelectMany(t => t.Value.Select(tec => (TeamId: t.Key, Tecnico: tec)))
            .GroupBy(t => t.Tecnico.TreinadorId)
            .ToDictionary(g => g.Key, g => g.First());
        var dosTecnicos = figurinhas
            .Where(f => f.Tipo == TipoFigurinha.Treinador && f.TreinadorId is not null)
            .GroupBy(f => f.TreinadorId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Fora).First());

        foreach (var (treinadorId, f) in dosTecnicos)
        {
            var hoje = clubeDoTecnico.TryGetValue(treinadorId, out var t) ? t : default;
            Comparar(f, hoje.Tecnico is null ? null : hoje.TeamId, hoje.Tecnico?.Papel);
        }

        void Comparar(FigurinhaExistente f, Guid? clubeHoje, PapelTreinador? papel)
        {
            if (clubeHoje is not Guid para)
            {
                if (!f.Fora) saem.Add(f);
            }
            else if (f.Fora) voltam.Add((f, para, papel));
            else if (para != f.TeamId) mudam.Add((f, para, papel));
        }

        // Quem chega: está num elenco ou comissão do álbum e nunca teve figurinha aqui.
        var chegam = clubeDoJogador.Values.Where(j => !dosJogadores.ContainsKey(j.PlayerId)).ToList();
        var tecnicosEntram = clubeDoTecnico.Values
            .Where(t => !dosTecnicos.ContainsKey(t.Tecnico.TreinadorId))
            .Select(t => (t.TeamId, t.Tecnico))
            .ToList();

        // Brilhante para quem chega entre os maiores overalls do clube, contando quem fica, quem muda e quem volta.
        var saemIds = saem.Select(s => s.FigurinhaId).ToHashSet();
        var paraOnde = mudam.Concat(voltam).ToDictionary(m => m.Item1.FigurinhaId, m => m.Item2);
        var ficam = dosJogadores.Values
            .Where(f => !saemIds.Contains(f.FigurinhaId) && (!f.Fora || paraOnde.ContainsKey(f.FigurinhaId)))
            .Select(f => (TeamId: paraOnde.GetValueOrDefault(f.FigurinhaId, f.TeamId), PlayerId: f.PlayerId!.Value, Overall: f.Overall ?? 0, f.Nome));
        var brilhantes = ficam
            .Concat(chegam.Select(j => (j.TeamId, j.PlayerId, j.Overall, j.Nome)))
            .GroupBy(j => j.TeamId)
            .SelectMany(g => MaioresOveralls(g.Select(j => (j.PlayerId, j.Overall, j.Nome))))
            .ToHashSet();

        var jogadoresEntram = chegam
            .Select(j => new JogadorQueEntra(j, brilhantes.Contains(j.PlayerId) ? RaridadeFigurinha.Brilhante : RaridadeFigurinha.Comum))
            .ToList();

        return new AtualizacaoDoAlbum(mudam, voltam, saem, jogadoresEntram, tecnicosEntram);
    }

    /// <summary>Os <see cref="BrilhantesPorClube"/> maiores overalls do elenco (empate pelo nome).</summary>
    public static HashSet<int> MaioresOveralls(IEnumerable<(int PlayerId, int Overall, string Nome)> elenco) =>
        elenco.OrderByDescending(j => j.Overall).ThenBy(j => j.Nome, StringComparer.CurrentCulture)
            .Take(BrilhantesPorClube)
            .Select(j => j.PlayerId)
            .ToHashSet();

    public static string? Limpar(string? destaque)
    {
        var texto = destaque?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        return texto.Length > TamanhoDestaque ? texto[..TamanhoDestaque] : texto;
    }

    /// <summary>
    /// Sorteia um pacote. Vagas 1 a 4: comum, com <see cref="ChanceBrilhanteNaComum"/> de virar brilhante;
    /// a última: brilhante, com <see cref="ChanceLendariaNaEspecial"/> de virar lendária. Sem figurinha da
    /// raridade sorteada no álbum, desce para a de baixo. Não repete figurinha dentro do mesmo pacote
    /// enquanto houver outra da raridade.
    /// Depois garante as novas de <see cref="NovasGarantidas"/>: se veio menos, troca vagas repetidas
    /// (as comuns primeiro, a especial por último) por uma que falta da mesma raridade ou de baixo — a
    /// garantia nunca sobe a raridade da vaga.
    /// </summary>
    public static IReadOnlyList<Guid> SortearPacote(
        IReadOnlyDictionary<RaridadeFigurinha, IReadOnlyList<Guid>> porRaridade, IReadOnlySet<Guid> jaTem, Random rng)
    {
        var sorteadas = new List<Guid>(TamanhoPacote);
        var raridades = new List<RaridadeFigurinha>(TamanhoPacote);
        for (var vaga = 1; vaga <= TamanhoPacote; vaga++)
        {
            var especial = vaga == TamanhoPacote;
            var raridade = especial
                ? rng.NextDouble() < ChanceLendariaNaEspecial ? RaridadeFigurinha.Lendaria : RaridadeFigurinha.Brilhante
                : rng.NextDouble() < ChanceBrilhanteNaComum ? RaridadeFigurinha.Brilhante : RaridadeFigurinha.Comum;

            for (var r = raridade; r >= RaridadeFigurinha.Comum; r--)
            {
                if (!porRaridade.TryGetValue(r, out var opcoes) || opcoes.Count == 0) continue;

                var livres = opcoes.Where(id => !sorteadas.Contains(id)).ToList();
                var lista = livres.Count > 0 ? livres : opcoes;
                sorteadas.Add(lista[rng.Next(lista.Count)]);
                raridades.Add(r);
                break;
            }
        }

        GarantirNovas(sorteadas, raridades, porRaridade, jaTem, rng);
        return sorteadas;
    }

    private static void GarantirNovas(List<Guid> sorteadas, List<RaridadeFigurinha> raridades,
        IReadOnlyDictionary<RaridadeFigurinha, IReadOnlyList<Guid>> porRaridade, IReadOnlySet<Guid> jaTem, Random rng)
    {
        // Repetida: a pessoa já tem, ou a mesma figurinha já saiu numa vaga anterior deste pacote.
        bool Repetida(int i) => jaTem.Contains(sorteadas[i]) || sorteadas.IndexOf(sorteadas[i]) < i;

        var faltam = NovasGarantidas(rng) - Enumerable.Range(0, sorteadas.Count).Count(i => !Repetida(i));
        if (faltam <= 0) return;

        var vagas = Enumerable.Range(0, sorteadas.Count)
            .Where(Repetida)
            .OrderBy(i => raridades[i])
            .ThenBy(i => i)
            .ToList();

        foreach (var i in vagas)
        {
            if (faltam <= 0) break;

            for (var r = raridades[i]; r >= RaridadeFigurinha.Comum; r--)
            {
                if (!porRaridade.TryGetValue(r, out var opcoes)) continue;

                var novas = opcoes.Where(id => !jaTem.Contains(id) && !sorteadas.Contains(id)).ToList();
                if (novas.Count == 0) continue;

                sorteadas[i] = novas[rng.Next(novas.Count)];
                raridades[i] = r;
                faltam--;
                break;
            }
        }
    }

    /// <summary>Sorteia quantas novas este pacote garante (de 1 a <see cref="TamanhoPacote"/>).</summary>
    public static int NovasGarantidas(Random rng)
    {
        if (rng.NextDouble() >= ChanceSegundaNova) return 1;

        var novas = 2;
        while (novas < TamanhoPacote && rng.NextDouble() < ChanceDeMaisUmaNova) novas++;
        return novas;
    }

    /// <summary>"#007": o número como vem impresso.</summary>
    public static string NumeroImpresso(int numero) => $"#{numero:000}";

    // ---- Pacotes ganhos ----

    /// <summary>X inicial do bolão: 1 pacote a cada X pontos (≈ 3 placares cravados ou 6 resultados certos).</summary>
    public const int PontosBolaoPorPacotePadrao = 30;

    /// <summary>Pacotes que o botão do pacote do dia dá, uma vez por pessoa por data de Brasília.</summary>
    public const int PacotesDoDia = 2;

    /// <summary>Pacotes por jogo do seu time: vitória, empate e derrota (quem faz W.O. não ganha nada).</summary>
    public const int PacotesPorVitoria = 3;
    public const int PacotesPorEmpate = 2;
    public const int PacotesPorDerrota = 1;

    /// <summary>
    /// O n-ésimo pacote do dia (1, 2…). O primeiro mantém a chave de quando era um só, então quem já pegou
    /// hoje antes da mudança não pega de novo.
    /// </summary>
    public static string ChaveDiario(DateTime diaEmBrasilia, int n = 1) =>
        n == 1 ? $"diario:{diaEmBrasilia:yyyy-MM-dd}" : $"diario:{diaEmBrasilia:yyyy-MM-dd}:{n}";

    /// <summary>O n-ésimo pacote (1, 2, 3) de um jogo do time da pessoa.</summary>
    public static string ChaveJogo(Guid partidaId, int n) => $"jogo:{partidaId:N}:{n}";

    /// <summary>Todo mundo (com clube ou sem) ganha 1 pacote em cada dia com jogo (data de Brasília).</summary>
    public static string ChaveDiaDeJogo(DateTime diaEmBrasilia) => $"diadejogo:{diaEmBrasilia:yyyy-MM-dd}";

    public static string MotivoDiaDeJogo(DateTime diaEmBrasilia) => $"Dia de jogo · {diaEmBrasilia:dd/MM}";

    /// <summary>O n-ésimo pacote do bolão da temporada (1, 2, 3…).</summary>
    public static string ChaveBolao(int temporada, int n) => $"bolao:{temporada}:{n}";

    /// <summary>Quantos pacotes do bolão os pontos já valem.</summary>
    public static int PacotesDoBolao(int pontos, int pontosPorPacote) =>
        pontosPorPacote <= 0 || pontos <= 0 ? 0 : pontos / pontosPorPacote;

    public static string MotivoVitoria(string adversario) => $"Vitória sobre o {adversario}";
    public static string MotivoEmpate(string adversario) => $"Empate com o {adversario}";
    public static string MotivoDerrota(string adversario) => $"Derrota para o {adversario}";

    /// <summary>"pela vitória sobre o Grêmio", "pelo empate com o Grêmio"…</summary>
    private static string PeloJogo(string motivo) =>
        $"{(motivo.StartsWith("Empate", StringComparison.Ordinal) ? "pelo" : "pela")} {Minuscula(motivo)}";

    // ---- Trocas e reciclagem ----

    /// <summary>Repetidas que viram 1 pacote novo na reciclagem.</summary>
    public const int RepetidasPorPacote = 6;

    /// <summary>Prazo para responder uma proposta de troca.</summary>
    public static readonly TimeSpan PrazoDaTroca = TimeSpan.FromHours(48);

    /// <summary>Figurinhas no máximo de cada lado de uma proposta.</summary>
    public const int MaximoPorLadoDaTroca = 10;

    public static string ChaveReciclagem(Guid pacoteId) => $"reciclagem:{pacoteId:N}";

    public static string MotivoReciclagem => $"{RepetidasPorPacote} repetidas recicladas";

    /// <summary>"2 por 1": quantas a pessoa dá e quantas recebe, do ponto de vista de quem propôs.</summary>
    public static string Placar(int oferecidas, int pedidas) => $"{oferecidas} por {pedidas}";

    public static string MotivoBolao(int pontos, int temporada) => $"{pontos} pontos no bolão de {temporada}";

    /// <summary>De onde veio o pacote, em poucas palavras, para a lista dos fechados.</summary>
    public static string DeOndeVeio(string origem, string? motivo) => origem switch
    {
        PacoteGanho.OrigemDiario => "Pacote do dia",
        PacoteGanho.OrigemJogo => motivo ?? "Jogo do seu time",
        PacoteGanho.OrigemDiaDeJogo => motivo ?? "Dia de jogo",
        PacoteGanho.OrigemBolao => motivo is null ? "Bolão" : $"Bolão · {motivo}",
        PacoteGanho.OrigemAdmin => motivo is null ? "Da organização" : $"Da organização · {motivo}",
        PacoteGanho.OrigemReciclagem => "Reciclagem de repetidas",
        _ => motivo ?? origem
    };

    /// <summary>
    /// Texto do aviso no celular. Um pacote diz de onde veio ("Você ganhou 1 pacote pela vitória sobre o
    /// Grêmio"); vários viram um aviso só ("Você ganhou 4 pacotes: 3 pelos jogos e 1 do bolão").
    /// </summary>
    public static string AvisoDePacotes(IReadOnlyList<(string Origem, string? Motivo)> pacotes)
    {
        if (pacotes.Count == 0) return string.Empty;

        if (pacotes.Count == 1)
        {
            var (origem, motivo) = pacotes[0];
            return origem switch
            {
                PacoteGanho.OrigemJogo when motivo is not null => $"Você ganhou 1 pacote {PeloJogo(motivo)}.",
                PacoteGanho.OrigemDiaDeJogo => "Você ganhou 1 pacote pelo dia de jogo.",
                PacoteGanho.OrigemBolao when motivo is not null => $"Você ganhou 1 pacote pelos {motivo}.",
                PacoteGanho.OrigemAdmin when motivo is not null => $"Você ganhou 1 pacote da organização: {motivo}.",
                PacoteGanho.OrigemAdmin => "Você ganhou 1 pacote da organização.",
                _ => "Você ganhou 1 pacote."
            };
        }

        var origens = pacotes.Select(p => p.Origem).Distinct().ToList();
        if (origens.Count == 1)
        {
            // Os pacotes de um jogo só (3 pela vitória, 2 pelo empate) dizem qual foi o jogo.
            var motivos = pacotes.Select(p => p.Motivo).Distinct().ToList();
            if (origens[0] == PacoteGanho.OrigemJogo && motivos is [{ } motivo])
                return $"Você ganhou {pacotes.Count} pacotes {PeloJogo(motivo)}.";

            var de = origens[0] switch
            {
                PacoteGanho.OrigemJogo => " pelos jogos",
                PacoteGanho.OrigemDiaDeJogo => " pelos dias de jogo",
                PacoteGanho.OrigemBolao => " do bolão",
                PacoteGanho.OrigemAdmin => " da organização",
                _ => ""
            };
            return $"Você ganhou {pacotes.Count} pacotes{de}.";
        }

        var partes = pacotes
            .GroupBy(p => p.Origem)
            .OrderBy(g => g.Key switch
            {
                PacoteGanho.OrigemJogo => 0,
                PacoteGanho.OrigemDiaDeJogo => 1,
                PacoteGanho.OrigemBolao => 2,
                PacoteGanho.OrigemAdmin => 3,
                _ => 4
            })
            .Select(g => (g.Key, g.Count()) switch
            {
                (PacoteGanho.OrigemJogo, 1) => "1 pelo jogo",
                (PacoteGanho.OrigemJogo, var n) => $"{n} pelos jogos",
                (PacoteGanho.OrigemDiaDeJogo, 1) => "1 pelo dia de jogo",
                (PacoteGanho.OrigemDiaDeJogo, var n) => $"{n} pelos dias de jogo",
                (PacoteGanho.OrigemBolao, var n) => $"{n} do bolão",
                (PacoteGanho.OrigemAdmin, var n) => $"{n} da organização",
                (_, var n) => $"{n} {(n == 1 ? "outro" : "outros")}"
            })
            .ToList();

        var detalhe = partes.Count == 1
            ? partes[0]
            : string.Join(", ", partes.Take(partes.Count - 1)) + " e " + partes[^1];
        return $"Você ganhou {pacotes.Count} pacotes: {detalhe}.";
    }

    /// <summary>
    /// Texto do aviso de selo: o álbum completo fala mais alto; páginas viram "Página do Santos completa!" ou,
    /// várias de uma vez, "3 páginas completas: Santos, Grêmio e Vasco."
    /// </summary>
    public static string AvisoDeConquistas(string albumNome, bool albumCompleto, IReadOnlyList<string> paginas)
    {
        if (albumCompleto) return $"🏆 Você completou o {albumNome}! Seu nome está no Hall da Fama.";
        if (paginas.Count == 0) return string.Empty;
        if (paginas.Count == 1) return $"🏅 Página do {paginas[0]} completa!";
        var lista = string.Join(", ", paginas.Take(paginas.Count - 1)) + " e " + paginas[^1];
        return $"🏅 {paginas.Count} páginas completas: {lista}.";
    }

    /// <summary>
    /// Frase do compartilhamento da carta: "Tirei o Totti lendário no Álbum CBFV 2010!".
    /// </summary>
    public static string FraseDeCompartilhar(FigurinhaDto f, string albumNome) => (f.Tipo, f.Raridade) switch
    {
        (TipoFigurinha.Escudo, _) => $"Colei o escudo brilhante do {f.TimeNome} no {albumNome}!",
        (TipoFigurinha.Treinador, _) => $"Tirei o {(f.Papel == PapelTreinador.Auxiliar ? "auxiliar" : "técnico")} {f.Perfil?.NomeCurto ?? f.NomeImpresso} do {f.TimeNome} no {albumNome}!",
        _ when f.Lenda => $"Tirei a lenda {f.NomeImpresso} no {albumNome}!",
        (_, RaridadeFigurinha.Lendaria) => $"Tirei o {f.NomeImpresso} lendário no {albumNome}!",
        (_, RaridadeFigurinha.Brilhante) => $"Tirei o {f.NomeImpresso} brilhante no {albumNome}!",
        _ => $"Colei o {f.NomeImpresso} no {albumNome}!"
    };

    /// <summary>A melhor carta do pacote para compartilhar: a mais rara, a nova antes da repetida, o maior overall.</summary>
    public static FigurinhaDto MelhorDoPacote(IReadOnlyList<(FigurinhaDto Figurinha, bool Nova)> cartas) =>
        cartas
            .OrderByDescending(c => c.Figurinha.Raridade)
            .ThenByDescending(c => c.Figurinha.Tipo == TipoFigurinha.Jogador)
            .ThenByDescending(c => c.Nova)
            .ThenByDescending(c => c.Figurinha.Overall ?? 0)
            .First().Figurinha;

    /// <summary>
    /// Texto do aviso de troca para quem precisa saber: proposta (ou contraproposta) recebida, aceita ou
    /// recusada. Vários de uma vez viram "3 novidades nas suas trocas".
    /// </summary>
    public static string AvisoDeTrocas(IReadOnlyList<(string Evento, string Quem, int Oferecidas, int Pedidas)> eventos)
    {
        if (eventos.Count == 0) return string.Empty;
        if (eventos.Count > 1) return $"🔁 {eventos.Count} novidades nas suas trocas de figurinhas.";

        var (evento, quem, oferecidas, pedidas) = eventos[0];
        // Doação: só um lado, sem pedir nada em troca.
        if (pedidas == 0)
        {
            return evento switch
            {
                EventoTroca.Recebida or EventoTroca.Contraproposta => $"🎁 {quem} quer te dar {Figurinhas(oferecidas)} de presente. Aceite para colar no álbum.",
                EventoTroca.Aceita => $"🎁 {quem} aceitou o seu presente.",
                EventoTroca.Recusada => $"❌ {quem} recusou o seu presente.",
                _ => "🎁 Novidade nas suas trocas de figurinhas."
            };
        }

        return evento switch
        {
            EventoTroca.Recebida => $"🔁 {quem} te propôs uma troca: {Figurinhas(oferecidas)} dele por {pedidas} sua{(pedidas == 1 ? "" : "s")}.",
            EventoTroca.Contraproposta => $"🔁 {quem} fez uma contraproposta: {Figurinhas(oferecidas)} dele por {pedidas} sua{(pedidas == 1 ? "" : "s")}.",
            EventoTroca.Aceita => $"✅ {quem} aceitou a sua troca. As figurinhas já estão no seu álbum.",
            EventoTroca.Recusada => $"❌ {quem} recusou a sua troca.",
            _ => "🔁 Novidade nas suas trocas de figurinhas."
        };
    }

    private static string Figurinhas(int n) => n == 1 ? "1 figurinha" : $"{n} figurinhas";

    /// <summary>Os acontecimentos de uma troca que viram aviso no celular.</summary>
    public static class EventoTroca
    {
        public const string Recebida = "recebida";
        public const string Contraproposta = "contraproposta";
        public const string Aceita = "aceita";
        public const string Recusada = "recusada";
    }

    private static string Minuscula(string texto) =>
        texto.Length == 0 ? texto : char.ToLowerInvariant(texto[0]) + texto[1..];
}
