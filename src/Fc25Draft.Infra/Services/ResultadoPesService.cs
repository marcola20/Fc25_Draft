using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Importa o JSON de uma partida simulada no PES 2021. A partida é achada pelo par ordenado
/// mandante x visitante — único numa competição de pontos corridos, já que o returno inverte
/// o mando. Quando o par se repete em outra competição (Liga e Copa), vale o jogo ainda não
/// jogado com a data marcada mais perto de agora; nada encontrado ou empate nessa regra é erro.
/// Vale para Liga, Copa (grupos e mata-mata) e Supercopa, e também para os jogos fora das
/// rodadas normais (mini liga, jogos decisivos e playoff de acesso). Jogo que precisa de
/// vencedor e termina empatado fica em andamento para registrar os pênaltis na tela.
/// Reenviar o mesmo jogo substitui os eventos (idempotente).
/// </summary>
public class ResultadoPesService : IResultadoPesService
{
    private const string Casa = "casa";
    private const string Fora = "fora";

    // Rodadas sentinela (LigaAdminService / LigaTemporadaService).
    private const int NumeroMiniLiga = 0;
    private const int NumeroJogoDecisivo = -1;
    private const int NumeroPlayoffAcesso = -2;

    private readonly DraftDbContext _db;
    private readonly ILigaAdminService _ligaAdmin;
    private readonly TimeProvider _time;

    public ResultadoPesService(DraftDbContext db, ILigaAdminService ligaAdmin, TimeProvider? time = null)
    {
        _db = db;
        _ligaAdmin = ligaAdmin;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ResultadoPesRespostaDto> ImportarAsync(
        ResultadoPesRequest request, string jsonBruto, Guid? partidaAnterior, bool simular, CancellationToken ct)
    {
        Validar(request);
        var alvo = await AcharPartidaAsync(request, partidaAnterior, ct);

        ResultadoPesRespostaDto resposta = null!;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            resposta = await GravarAsync(alvo, request, jsonBruto, simular, ct);
            if (simular)
                await tx.RollbackAsync(ct);
            else
                await tx.CommitAsync(ct);
        });

        // Simulação: o que ficou no change tracker foi desfeito no banco.
        if (simular) _db.ChangeTracker.Clear();
        return resposta;
    }

    // ── Validação ─────────────────────────────────────────────────────────────

    private static readonly HashSet<string> TiposEvento = new()
    {
        "gol", "gol_contra", "cartao_amarelo", "cartao_vermelho", "substituicao"
    };

    private static void Validar(ResultadoPesRequest r)
    {
        if (!string.Equals(r.Status, "ok", StringComparison.OrdinalIgnoreCase))
            throw new ResultadoPesException(ResultadoPesErro.Invalido,
                $"O JSON está com status \"{r.Status ?? "vazio"}\": só é aceito \"ok\". Confira o vídeo e corrija o JSON antes de enviar.");

        if (string.IsNullOrWhiteSpace(r.Casa?.Time) || string.IsNullOrWhiteSpace(r.Fora?.Time))
            throw new ResultadoPesException(ResultadoPesErro.Invalido, "Faltam os nomes dos times.");

        if (r.Casa!.Gols is not int golsCasa || r.Fora!.Gols is not int golsFora || golsCasa < 0 || golsFora < 0)
            throw new ResultadoPesException(ResultadoPesErro.Invalido, "Placar ausente ou inválido.");

        var eventos = r.Eventos ?? Array.Empty<ResultadoPesEvento>();
        foreach (var e in eventos)
        {
            if (e.Lado is not (Casa or Fora))
                throw new ResultadoPesException(ResultadoPesErro.Invalido, $"Evento com lado inválido: \"{e.Lado}\".");
            if (e.Tipo is null || !TiposEvento.Contains(e.Tipo))
                throw new ResultadoPesException(ResultadoPesErro.Invalido, $"Evento com tipo inválido: \"{e.Tipo}\".");
        }

        // O placar é gravado pelo cabeçalho; os gols da linha do tempo precisam contar a mesma coisa.
        int GolsNosEventos(string lado) => eventos.Count(e => e.Lado == lado && e.Tipo is "gol" or "gol_contra");
        var (evCasa, evFora) = (GolsNosEventos(Casa), GolsNosEventos(Fora));
        if (evCasa != golsCasa || evFora != golsFora)
            throw new ResultadoPesException(ResultadoPesErro.Invalido,
                $"O placar é {golsCasa} x {golsFora}, mas os eventos têm {evCasa} x {evFora} gols.");
    }

    // ── Busca da partida ──────────────────────────────────────────────────────

    private enum CompeticaoDoVideo { Qualquer, Liga, Copa, Supercopa }

    /// <summary>
    /// O nome do vídeo diz a competição quando cita ("… ｜ Série A", "… Brasileirão CBFV",
    /// "… Copa", "… Supercopa"). Sem isso, um clássico da Série A que também é a Supercopa é ambíguo.
    /// </summary>
    private static CompeticaoDoVideo LerCompeticao(string? video)
    {
        var nome = NomesPes.Normalizar(video);
        if (nome.Contains("supercopa")) return CompeticaoDoVideo.Supercopa;
        if (nome.Contains("copa")) return CompeticaoDoVideo.Copa;
        if (nome.Contains("serie") || nome.Contains("brasileirao")) return CompeticaoDoVideo.Liga;
        return CompeticaoDoVideo.Qualquer;
    }

    private static readonly string[] PalavrasDeJogoExtra =
        { "decisivo", "desempate", "mini liga", "miniliga", "playoff", "play off", "acesso", "quartas", "semi", "final" };

    /// <summary>O vídeo diz que é jogo fora das rodadas normais (desempate, mini liga, playoff, mata-mata)?</summary>
    private static bool VideoDeJogoExtra(string? video)
    {
        var nome = NomesPes.Normalizar(video);
        return PalavrasDeJogoExtra.Any(nome.Contains);
    }

    /// <summary>
    /// Partida candidata. <see cref="PartidaId"/> é nulo no jogo de mata-mata cuja partida ainda
    /// não foi criada (a importação cria). <see cref="Extra"/> = fora das rodadas normais.
    /// <see cref="DataHora"/> = data marcada da rodada (horário de Brasília), quando há.
    /// </summary>
    private sealed record Alvo(
        Guid? PartidaId, Guid? KnockoutJogoId, int Numero, bool Desempate,
        string Liga, TipoCompetition Tipo, Divisao? Divisao, bool Extra, string Onde,
        bool Encerrada, DateTime? DataHora);

    private static string OndeNaLiga(int numero, bool desempate) => numero switch
    {
        NumeroMiniLiga => "mini liga",
        NumeroJogoDecisivo => "jogo decisivo do título",
        NumeroPlayoffAcesso => "playoff de acesso",
        _ => desempate ? $"jogo decisivo (rodada {numero})" : $"rodada {numero}"
    };

    private static string OndeNoMataMata(FaseKnockout fase) => fase switch
    {
        FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => "quartas de final",
        FaseKnockout.Semi1 or FaseKnockout.Semi2 => "semifinal",
        FaseKnockout.Final => "final",
        _ => "play-in"
    };

    private async Task<Alvo> AcharPartidaAsync(ResultadoPesRequest r, Guid? partidaAnterior, CancellationToken ct)
    {
        var competicao = LerCompeticao(r.Video);
        Divisao? divisao = r.Serie?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "A" => Divisao.SerieA,
            "B" => Divisao.SerieB,
            _ => throw new ResultadoPesException(ResultadoPesErro.Invalido, $"Série inválida: \"{r.Serie}\".")
        };

        var times = await _db.Teams.AsNoTracking().Select(t => new { t.TeamId, t.TeamName }).ToListAsync(ct);
        Guid AcharTime(string nome, string lado)
        {
            var achados = times.Where(t => NomesPes.MesmoTime(nome, t.TeamName)).ToList();
            return achados.Count == 1
                ? achados[0].TeamId
                : throw new ResultadoPesException(ResultadoPesErro.NaoEncontrado,
                    $"Time da {lado} \"{nome}\" não existe no cadastro.");
        }
        var casaId = AcharTime(r.Casa!.Time!, Casa);
        var foraId = AcharTime(r.Fora!.Time!, Fora);

        // Partidas de competições não encerradas, de qualquer rodada, e os jogos de mata-mata
        // com os times definidos que ainda não têm partida criada.
        async Task<List<Alvo>> Buscar(Guid mandante, Guid visitante)
        {
            var partidas = await _db.LigaPartidas.AsNoTracking()
                .Where(p => p.TimeCasaId == mandante && p.TimeForaId == visitante
                            && p.Rodada.Liga.Status != LigaStatus.Encerrada)
                .Select(p => new { p.PartidaId, p.Status, p.Rodada.Numero, p.Rodada.Desempate, p.Rodada.DataHora, p.Rodada.Liga.Nome, p.Rodada.Liga.Tipo, p.Rodada.Liga.Divisao })
                .ToListAsync(ct);

            var ids = partidas.Select(p => p.PartidaId).ToList();
            var doMataMata = await _db.LigaKnockoutJogos.AsNoTracking()
                .Where(k => k.PartidaId != null && ids.Contains(k.PartidaId.Value))
                .Select(k => new { PartidaId = k.PartidaId!.Value, k.KnockoutJogoId, k.Fase })
                .ToDictionaryAsync(k => k.PartidaId, ct);

            var semPartida = await _db.LigaKnockoutJogos.AsNoTracking()
                .Where(k => k.TimeCasaId == mandante && k.TimeForaId == visitante
                            && k.PartidaId == null && k.VencedorId == null
                            && k.Liga.Status != LigaStatus.Encerrada)
                .Select(k => new { k.KnockoutJogoId, k.Fase, k.Liga.Nome, k.Liga.Tipo, k.Liga.Divisao })
                .ToListAsync(ct);

            var alvos = new List<Alvo>();
            foreach (var p in partidas)
            {
                var encerrada = p.Status == PartidaStatus.Encerrada;
                if (doMataMata.TryGetValue(p.PartidaId, out var ko))
                    alvos.Add(new Alvo(p.PartidaId, ko.KnockoutJogoId, p.Numero, p.Desempate, p.Nome, p.Tipo, p.Divisao, true,
                        OndeNoMataMata(ko.Fase), encerrada, p.DataHora));
                else if (p.Numero > 0 || p.Tipo == TipoCompetition.Liga)
                    // Na Copa a rodada 0 só tem partidas do mata-mata; sem jogo ligado, não é de ninguém.
                    alvos.Add(new Alvo(p.PartidaId, null, p.Numero, p.Desempate, p.Nome, p.Tipo, p.Divisao,
                        p.Numero <= 0 || p.Desempate, OndeNaLiga(p.Numero, p.Desempate), encerrada, p.DataHora));
            }
            alvos.AddRange(semPartida.Select(k =>
                new Alvo(null, k.KnockoutJogoId, 0, false, k.Nome, k.Tipo, k.Divisao, true, OndeNoMataMata(k.Fase), false, null)));

            return alvos
                .Where(a => competicao switch
                {
                    CompeticaoDoVideo.Liga => a.Tipo == TipoCompetition.Liga,
                    CompeticaoDoVideo.Copa => a.Tipo == TipoCompetition.Copa,
                    CompeticaoDoVideo.Supercopa => a.Tipo == TipoCompetition.Supercopa,
                    _ => true
                })
                // A série só filtra a Liga; Copa e Supercopa juntam as duas séries, e o playoff
                // (guardado na Série A) é entre um time de cada.
                .Where(a => divisao is null || a.Tipo != TipoCompetition.Liga || a.Divisao is null
                            || a.Divisao == divisao || a.Numero == NumeroPlayoffAcesso)
                .ToList();
        }

        var candidatos = await Buscar(casaId, foraId);
        var jogo = $"{r.Casa.Time} x {r.Fora!.Time}";
        var ondeSerie = divisao is null ? "" : $" (Série {r.Serie!.Trim().ToUpperInvariant()})";

        if (candidatos.Count == 0)
        {
            var invertido = await Buscar(foraId, casaId);
            var dica = invertido.Count > 0
                ? $" Existe {r.Fora.Time} x {r.Casa.Time} ({invertido[0].Liga}, {invertido[0].Onde}): o mando está invertido?"
                : " Confira se as rodadas (ou o jogo do mata-mata) foram geradas e se a competição não está encerrada.";
            throw new ResultadoPesException(ResultadoPesErro.NaoEncontrado,
                $"Nenhuma partida aberta {jogo}{ondeSerie}.{dica}");
        }

        // Reenvio (JSON corrigido ou reextraído): fica na partida que recebeu o envio anterior,
        // mesmo já encerrada e mesmo havendo outra do mesmo par mais perto de agora.
        if (candidatos.Count > 1 && candidatos.FirstOrDefault(c => partidaAnterior is not null && c.PartidaId == partidaAnterior) is { } anterior)
            candidatos = [anterior];

        // Reenvio sem o id (o primeiro envio gravou, mas a resposta não chegou ao script): a
        // partida que já tem este vídeo. Sem isto ela, já encerrada, perderia para a do mesmo par
        // ainda não jogada na outra competição, e o jogo entraria nas duas.
        if (candidatos.Count > 1)
        {
            var ids = candidatos.Where(c => c.PartidaId is not null).Select(c => c.PartidaId!.Value).ToList();
            var importadas = await _db.LigaPartidaImportacoes.AsNoTracking()
                .Where(i => ids.Contains(i.PartidaId))
                .Select(i => new { i.PartidaId, i.Video, i.Json })
                .ToListAsync(ct);
            var comEsteVideo = importadas
                .Where(i => MesmoVideo(i.Video, GravadoEm(i.Json), r))
                .Select(i => i.PartidaId)
                .ToHashSet();
            if (comEsteVideo.Count == 1)
                candidatos = candidatos.Where(c => c.PartidaId is Guid id && comEsteVideo.Contains(id)).ToList();
        }

        // O mesmo par pode ter o jogo da rodada e um jogo extra (decisivo, mini liga, mata-mata):
        // o nome do vídeo diz se é extra; senão vale a rodada do JSON.
        if (candidatos.Count > 1)
        {
            var filtrados = VideoDeJogoExtra(r.Video)
                ? candidatos.Where(c => c.Extra).ToList()
                : r.Rodada is int numero
                    ? candidatos.Where(c => !c.Extra && c.Numero == numero).ToList()
                    : candidatos;
            if (filtrados.Count > 0) candidatos = filtrados;
        }

        // O par se repete em outra competição (Liga e Copa): vale o jogo ainda não jogado com a
        // data marcada mais perto de agora — é o que está sendo simulado.
        if (candidatos.Count > 1)
        {
            var abertos = candidatos.Where(c => !c.Encerrada).ToList();
            if (abertos.Count > 0) candidatos = abertos;

            var agora = HorarioDeBrasilia.Agora(_time);
            var porData = candidatos
                .Where(c => c.DataHora is not null)
                .Select(c => (Alvo: c, Distancia: (c.DataHora!.Value - agora).Duration()))
                .OrderBy(x => x.Distancia)
                .ToList();
            if (porData.Count == 1 || (porData.Count > 1 && porData[0].Distancia < porData[1].Distancia))
                candidatos = [porData[0].Alvo];
        }

        if (candidatos.Count > 1)
            throw new ResultadoPesException(ResultadoPesErro.Ambiguo,
                $"Mais de uma partida possível para {jogo}{ondeSerie} e nenhuma com data marcada mais perto de agora; "
                + "cite no nome do vídeo a competição (\"Série A\", \"Copa\", \"Supercopa\") "
                + "e, se for jogo decisivo, mini liga, playoff ou mata-mata, diga isso também (ou informe a rodada no JSON).",
                candidatos.Select(c => $"{c.Liga} · {c.Onde}" + (c.DataHora is DateTime d ? $" · {d:dd/MM HH:mm}" : "")).ToList());

        var achada = candidatos[0];
        if (r.Rodada is int rodada && achada.Tipo == TipoCompetition.Liga && !achada.Extra && rodada != achada.Numero)
            throw new ResultadoPesException(ResultadoPesErro.Ambiguo,
                $"A rodada não confere: o JSON diz rodada {rodada}, mas {jogo} é da rodada {achada.Numero} ({achada.Liga}).");

        return achada;
    }

    /// <summary>
    /// O JSON importado antes veio do mesmo vídeo? Pela data de gravação quando os dois a têm
    /// (o nome pode se repetir se o vídeo antigo saiu da pasta); senão, pelo nome do vídeo.
    /// </summary>
    private static bool MesmoVideo(string? videoImportado, string? gravadoEmImportado, ResultadoPesRequest r) =>
        gravadoEmImportado is not null && r.GravadoEm is not null
            ? gravadoEmImportado == r.GravadoEm
            : !string.IsNullOrWhiteSpace(r.Video) && string.Equals(videoImportado, r.Video, StringComparison.OrdinalIgnoreCase);

    private static string? GravadoEm(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("gravado_em", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    // ── Gravação ──────────────────────────────────────────────────────────────

    private async Task<ResultadoPesRespostaDto> GravarAsync(
        Alvo alvo, ResultadoPesRequest r, string jsonBruto, bool simular, CancellationToken ct)
    {
        var agora = _time.GetUtcNow().UtcDateTime;

        // Jogo do mata-mata sem partida: cria como na tela (botão "Criar partida" do chaveamento).
        var partidaId = alvo.PartidaId
            ?? (await _ligaAdmin.CriarPartidaKnockoutAsync(alvo.KnockoutJogoId!.Value, ct)).PartidaId;
        var jogoKo = alvo.KnockoutJogoId is Guid koId
            ? await _db.LigaKnockoutJogos.FirstAsync(k => k.KnockoutJogoId == koId, ct)
            : null;

        var partida = await _db.LigaPartidas
            .Include(p => p.Rodada).ThenInclude(x => x.Liga)
            .Include(p => p.TimeCasa)
            .Include(p => p.TimeFora)
            .FirstAsync(p => p.PartidaId == partidaId, ct);
        var liga = partida.Rodada.Liga;

        var timeDoLado = new Dictionary<string, Team> { [Casa] = partida.TimeCasa, [Fora] = partida.TimeFora };
        var elenco = new Dictionary<string, List<NomesPes.Candidato>>
        {
            [Casa] = await CandidatosAsync(partida.TimeCasaId, liga.LigaId, ct),
            [Fora] = await CandidatosAsync(partida.TimeForaId, liga.LigaId, ct),
        };
        static string Outro(string lado) => lado == Casa ? Fora : Casa;

        var naoIdentificados = new List<ResultadoPesNaoIdentificadoDto>();
        var avisos = new List<string>();

        // Quem estava suspenso para este jogo (pelos cartões dos jogos anteriores).
        var suspensosNoJogo = (await DisciplinaDaCompeticao.CalcularAsync(_db, liga.LigaId, ct))?.Suspensoes
            .Where(s => s.PartidaCumprida == partidaId)
            .ToList() ?? [];

        int? Casar(string ladoDoElenco, string? nome, string papel, int? minuto)
        {
            var c = NomesPes.Casar(nome, elenco[ladoDoElenco]);
            if (c.Id is null)
                naoIdentificados.Add(new ResultadoPesNaoIdentificadoDto(
                    ladoDoElenco, timeDoLado[ladoDoElenco].TeamName, nome, papel, minuto, c.Motivo!));
            return c.Id;
        }

        // Eventos: a importação é a verdade do jogo — apaga os anteriores e grava os do JSON.
        _db.LigaEventos.RemoveRange(await _db.LigaEventos.Where(e => e.PartidaId == partidaId).ToListAsync(ct));

        var novos = new List<LigaEventoPartida>();
        void Adicionar(TipoEvento tipo, string lado, int jogadorId, int? minuto, int? assistenteId = null, int? saiuId = null) =>
            novos.Add(new LigaEventoPartida
            {
                EventoId = Guid.NewGuid(),
                PartidaId = partidaId,
                Tipo = tipo,
                TimeId = timeDoLado[lado].TeamId,
                JogadorId = jogadorId,
                AssistenteId = assistenteId,
                JogadorSaiuId = saiuId,
                Minuto = minuto,
                // Milissegundos a mais mantêm a ordem da linha do tempo no mesmo minuto.
                CriadoEm = agora.AddMilliseconds(novos.Count)
            });

        var entraram = new Dictionary<string, HashSet<int>> { [Casa] = new(), [Fora] = new() };
        var nomesQueEntraram = new Dictionary<string, HashSet<string>> { [Casa] = new(), [Fora] = new() };

        foreach (var e in r.Eventos ?? Array.Empty<ResultadoPesEvento>())
        {
            var lado = e.Lado!;
            int? minuto = e.Minuto is >= 1 and <= 130 ? e.Minuto : null;

            switch (e.Tipo)
            {
                case "gol":
                    // Gol sem autor identificado conta no placar, mas não entra na artilharia.
                    if (Casar(lado, e.Jogador, "gol", minuto) is int autor)
                    {
                        var assistente = e.Assistencia is null ? null : Casar(lado, e.Assistencia, "assistencia", minuto);
                        Adicionar(TipoEvento.Gol, lado, autor, minuto, assistenteId: assistente == autor ? null : assistente);
                    }
                    break;

                case "gol_contra":
                    // Lado = quem ganha o gol; o autor é do time adversário (mesma regra da tela).
                    if (Casar(Outro(lado), e.Jogador, "gol_contra", minuto) is int contra)
                        Adicionar(TipoEvento.GolContra, lado, contra, minuto);
                    break;

                case "cartao_amarelo":
                case "cartao_vermelho":
                    if (Casar(lado, e.Jogador, e.Tipo, minuto) is int punido)
                        Adicionar(e.Tipo == "cartao_amarelo" ? TipoEvento.CartaoAmarelo : TipoEvento.CartaoVermelho,
                            lado, punido, minuto);
                    break;

                case "substituicao":
                    nomesQueEntraram[lado].Add(NomesPes.Normalizar(e.Entrou));
                    var entrou = Casar(lado, e.Entrou, "entrou", minuto);
                    var saiu = e.Saiu is null ? null : Casar(lado, e.Saiu, "saiu", minuto);
                    if (entrou is int idEntrou)
                    {
                        entraram[lado].Add(idEntrou);
                        Adicionar(TipoEvento.Substituicao, lado, idEntrou, minuto, saiuId: saiu == idEntrou ? null : saiu);
                    }
                    break;
            }
        }
        _db.LigaEventos.AddRange(novos);

        // Retrato da escalação (contador de jogos): titulares pelas notas do PES quando os 11
        // foram identificados; senão fica o retrato que já havia, ou a escalação ativa do time.
        var retratoAtual = await _db.LigaEscalacoes.Where(x => x.PartidaId == partidaId).ToListAsync(ct);
        var titularesDasNotas = true;
        foreach (var lado in new[] { Casa, Fora })
        {
            var time = timeDoLado[lado];
            var notas = (lado == Casa ? r.Notas?.Casa : r.Notas?.Fora) ?? Array.Empty<ResultadoPesNota>();
            var titulares = new List<int>();
            var faltou = false;
            foreach (var n in notas)
            {
                // Reserva que entrou pode aparecer nas notas: não é titular.
                if (nomesQueEntraram[lado].Contains(NomesPes.Normalizar(n.Jogador))) continue;
                if (Casar(lado, n.Jogador, "titular", null) is int id)
                {
                    if (!entraram[lado].Contains(id) && !titulares.Contains(id)) titulares.Add(id);
                }
                else faltou = true;
            }

            var doTime = retratoAtual.Where(x => x.TimeId == time.TeamId).ToList();
            if (!faltou && titulares.Count == 11)
            {
                _db.LigaEscalacoes.RemoveRange(doTime);
                _db.LigaEscalacoes.AddRange(titulares.Select((id, i) => new LigaEscalacaoPartida
                {
                    Id = Guid.NewGuid(), PartidaId = partidaId, TimeId = time.TeamId, JogadorId = id, Titular = true, Ordem = i
                }));
                continue;
            }

            titularesDasNotas = false;
            if (doTime.Count > 0)
            {
                avisos.Add($"{time.TeamName}: titulares das notas incompletos ({titulares.Count} de 11 identificados); mantido o retrato de escalação que já existia.");
                continue;
            }
            avisos.Add($"{time.TeamName}: titulares das notas incompletos ({titulares.Count} de 11 identificados); usada a escalação ativa do time.");
            var ativa = await EscalacaoPartidaLoader.EscalacaoAtivaAsync(_db, new[] { time.TeamId }, ct);
            _db.LigaEscalacoes.AddRange(ativa.Select(l => new LigaEscalacaoPartida
            {
                Id = Guid.NewGuid(), PartidaId = partidaId, TimeId = l.TimeId, JogadorId = l.JogadorId, Titular = l.Titular, Ordem = l.Ordem
            }));
        }

        // Notas do PES por jogador (titulares e quem entrou): substituem as que havia.
        _db.LigaNotasJogadores.RemoveRange(await _db.LigaNotasJogadores.Where(x => x.PartidaId == partidaId).ToListAsync(ct));
        var notasDoJogo = CasarNotas(partidaId, r, elenco,
            new Dictionary<string, Guid> { [Casa] = partida.TimeCasaId, [Fora] = partida.TimeForaId });
        _db.LigaNotasJogadores.AddRange(notasDoJogo);

        var emCampo = notasDoJogo.Select(n => n.JogadorId).Concat(novos.Select(e => e.JogadorId)).ToHashSet();
        foreach (var s in suspensosNoJogo.Where(s => emCampo.Contains(s.JogadorId)))
            avisos.Add($"{s.JogadorNome} ({s.TimeNome}) estava suspenso para este jogo ({s.Motivo} em {s.JogoDoCartao}) e entrou em campo.");

        // Placar e situação da partida.
        partida.GolsCasa = r.Casa!.Gols!.Value;
        partida.GolsFora = r.Fora!.Gols!.Value;
        partida.IsWO = false;
        partida.IniciadaEm ??= agora;

        // Jogo único que precisa de vencedor: Supercopa, mata-mata, jogos decisivos e playoff de acesso
        // (que é rodada de desempate). A mini liga é por pontos e aceita empate.
        var rodadaDaPartida = partida.Rodada;
        var precisaDeVencedor = liga.Tipo == TipoCompetition.Supercopa || jogoKo is not null
                                || rodadaDaPartida.Desempate || rodadaDaPartida.Numero == NumeroJogoDecisivo;

        var empate = partida.GolsCasa == partida.GolsFora;
        if (!precisaDeVencedor || !empate)
        {
            partida.TemPenaltis = false;
            partida.PenaltisVencedorId = null;
        }

        // Mata-mata já decidido (reenvio): o placar novo não pode trocar quem avançou.
        if (jogoKo?.VencedorId is Guid jaAvancou)
        {
            Guid? vencedorAgora = !empate ? (partida.GolsCasa > partida.GolsFora ? partida.TimeCasaId : partida.TimeForaId)
                : partida.TemPenaltis ? partida.PenaltisVencedorId : null;
            if (vencedorAgora != jaAvancou)
                throw new ResultadoPesException(ResultadoPesErro.Invalido,
                    $"{liga.Nome} · {alvo.Onde}: o jogo já foi decidido e o vencedor avançou no chaveamento; " +
                    "o placar do JSON muda o vencedor. Corrija o mata-mata na tela.");
        }

        if (precisaDeVencedor && empate && !partida.TemPenaltis)
        {
            // O JSON não traz pênaltis: a partida fica em andamento para registrar o vencedor na tela.
            partida.Status = PartidaStatus.EmAndamento;
            partida.EncerradaEm = null;
            avisos.Add(jogoKo is not null
                ? $"{liga.Nome} · {alvo.Onde}: jogo empatado. Registre o vencedor dos pênaltis no mata-mata (tela da liga) para encerrar e avançar o vencedor."
                : $"{liga.Nome} · {alvo.Onde}: jogo empatado. Registre o vencedor dos pênaltis na tela da liga para encerrar a partida.");
        }
        else
        {
            partida.Status = PartidaStatus.Encerrada;
            // Reimportar não muda a data do jogo (o Plantão ordena por ela).
            partida.EncerradaEm ??= agora;
        }

        var importacao = await _db.LigaPartidaImportacoes.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct);
        var situacao = importacao is null ? "criado" : "atualizado";
        if (importacao is null)
        {
            importacao = new LigaPartidaImportacao { PartidaId = partidaId, ImportadoEm = agora };
            _db.LigaPartidaImportacoes.Add(importacao);
        }
        importacao.Video = r.Video is { Length: > 500 } v ? v[..500] : r.Video;
        importacao.Json = jsonBruto;
        importacao.AtualizadoEm = agora;

        await _db.SaveChangesAsync(ct);
        await _ligaAdmin.RecalcularClassificacaoAsync(partida.RodadaId, ct);

        if (partida.Status == PartidaStatus.Encerrada)
        {
            if (jogoKo is { VencedorId: null })
            {
                // Mesmo caminho do "Encerrar" do chaveamento: grava o vencedor e avança no mata-mata.
                await _ligaAdmin.EncerrarKnockoutJogoAsync(jogoKo.KnockoutJogoId, new LigaEncerrarKnockoutRequest(false, null), ct);
                avisos.Add($"{liga.Nome} · {alvo.Onde}: vencedor avançou no chaveamento.");
            }
            else if (liga.Tipo == TipoCompetition.Liga && rodadaDaPartida.Numero == NumeroJogoDecisivo)
                avisos.Add("Jogo decisivo do título encerrado: defina o campeão na tela da liga.");
            else if (liga.Tipo == TipoCompetition.Liga && rodadaDaPartida.Numero == NumeroMiniLiga)
                avisos.Add("Jogo da mini liga do título: quando todos terminarem, conclua a mini liga na tela da liga.");
        }

        return new ResultadoPesRespostaDto(
            partidaId, liga.Nome, partida.Rodada.Numero,
            partida.TimeCasa.TeamName, partida.TimeFora.TeamName,
            partida.GolsCasa, partida.GolsFora,
            situacao, simular, partida.Status.ToString(), novos.Count, titularesDasNotas,
            naoIdentificados, avisos);
    }

    public async Task<int> ReprocessarNotasAsync(bool somenteSemNotas, CancellationToken ct)
    {
        var importacoes = await _db.LigaPartidaImportacoes.AsNoTracking()
            .Where(i => !somenteSemNotas || !_db.LigaNotasJogadores.Any(n => n.PartidaId == i.PartidaId))
            .Select(i => new { i.PartidaId, i.Json, i.Partida.TimeCasaId, i.Partida.TimeForaId, i.Partida.Rodada.LigaId })
            .ToListAsync(ct);

        var gravadas = 0;
        foreach (var imp in importacoes)
        {
            ResultadoPesRequest? r;
            try { r = System.Text.Json.JsonSerializer.Deserialize<ResultadoPesRequest>(imp.Json); }
            catch (System.Text.Json.JsonException) { continue; }
            if (r?.Notas is null) continue;

            var elenco = new Dictionary<string, List<NomesPes.Candidato>>
            {
                [Casa] = await CandidatosAsync(imp.TimeCasaId, imp.LigaId, ct),
                [Fora] = await CandidatosAsync(imp.TimeForaId, imp.LigaId, ct),
            };
            var notas = CasarNotas(imp.PartidaId, r, elenco,
                new Dictionary<string, Guid> { [Casa] = imp.TimeCasaId, [Fora] = imp.TimeForaId });

            _db.LigaNotasJogadores.RemoveRange(await _db.LigaNotasJogadores.Where(x => x.PartidaId == imp.PartidaId).ToListAsync(ct));
            _db.LigaNotasJogadores.AddRange(notas);
            await _db.SaveChangesAsync(ct);
            gravadas += notas.Count;
        }

        return gravadas;
    }

    /// <summary>
    /// Notas do JSON casadas com quem podia estar em campo (o mesmo critério dos eventos). Nome que não
    /// casa com segurança fica sem nota; quem não casou já aparece nos avisos da importação.
    /// </summary>
    private static List<LigaNotaJogador> CasarNotas(
        Guid partidaId, ResultadoPesRequest r,
        IReadOnlyDictionary<string, List<NomesPes.Candidato>> elenco,
        IReadOnlyDictionary<string, Guid> timeDoLado)
    {
        var notas = new List<LigaNotaJogador>();
        foreach (var lado in new[] { Casa, Fora })
        {
            foreach (var n in (lado == Casa ? r.Notas?.Casa : r.Notas?.Fora) ?? Array.Empty<ResultadoPesNota>())
            {
                if (n.Nota is not double nota || nota is < 0 or > 10) continue;
                if (NomesPes.Casar(n.Jogador, elenco[lado]).Id is not int jogadorId) continue;
                if (notas.Any(x => x.JogadorId == jogadorId)) continue;

                notas.Add(new LigaNotaJogador
                {
                    PartidaId = partidaId,
                    JogadorId = jogadorId,
                    TimeId = timeDoLado[lado],
                    Nota = Math.Round((decimal)nota, 1),
                    MelhorEmCampo = n.MelhorEmCampo == true
                });
            }
        }
        return notas;
    }

    /// <summary>
    /// Quem pode ser do time naquele jogo: o elenco atual e quem já atuou pelo time nesta
    /// competição (o PES grava jogo antigo com o elenco da época; o jogador pode ter saído).
    /// </summary>
    private async Task<List<NomesPes.Candidato>> CandidatosAsync(Guid timeId, Guid ligaId, CancellationToken ct)
    {
        var ids = new HashSet<int>(await _db.TeamRosters.AsNoTracking()
            .Where(x => x.TeamId == timeId).Select(x => x.PlayerId).ToListAsync(ct));

        var eventos = await _db.LigaEventos.AsNoTracking()
            .Where(e => e.TimeId == timeId && e.Partida.Rodada.LigaId == ligaId)
            .Select(e => new { e.Tipo, e.JogadorId, e.AssistenteId, e.JogadorSaiuId })
            .ToListAsync(ct);
        foreach (var e in eventos)
        {
            // No gol contra o autor é do adversário.
            if (e.Tipo != TipoEvento.GolContra) ids.Add(e.JogadorId);
            if (e.AssistenteId is int a) ids.Add(a);
            if (e.JogadorSaiuId is int s) ids.Add(s);
        }

        ids.UnionWith(await _db.LigaEscalacoes.AsNoTracking()
            .Where(x => x.TimeId == timeId && x.Partida.Rodada.LigaId == ligaId)
            .Select(x => x.JogadorId).ToListAsync(ct));

        return await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.PlayerId))
            .Select(p => new NomesPes.Candidato(p.PlayerId, p.Name))
            .ToListAsync(ct);
    }
}
