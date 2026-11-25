using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Resultados segurados: com a chave ligada, o envio do Auto_PES21 é validado (simulando) e guardado
/// sem tocar na partida; o admin libera em /admin/resultados-pendentes, e só então a partida é
/// encerrada e aparece para todos.
/// </summary>
public partial class ResultadoPesService
{
    public async Task<ResultadoPesRespostaDto> ReceberAsync(
        ResultadoPesRequest request, string jsonBruto, Guid? partidaAnterior, bool simular, CancellationToken ct)
    {
        if (simular || !await SegurandoAsync(ct))
            return await ImportarAsync(request, jsonBruto, partidaAnterior, simular, ct);

        Validar(request);
        var (previa, alvo, erro) = await ValidarAsync(request, jsonBruto, partidaAnterior, null, ct);

        // Correção de jogo que já saiu: vai direto, senão o resultado público ficaria velho até liberar.
        if (alvo?.PartidaId is Guid jaSaiu && await _db.LigaPartidaImportacoes.AnyAsync(i => i.PartidaId == jaSaiu, ct))
            return await ImportarAsync(request, jsonBruto, partidaAnterior, simular: false, ct);

        // Reenvio substitui o pendente do mesmo jogo. Sem jogo achado, só o do mesmo vídeo que também
        // não achou: o nome do vídeo se repete quando o par volta a se enfrentar em outra competição.
        var pendentes = await _db.ResultadosPesPendentes.ToListAsync(ct);
        var pendente = alvo is not null
            ? pendentes.FirstOrDefault(p => MesmoJogo(p, alvo))
              ?? pendentes.FirstOrDefault(p => SemJogo(p) && MesmoVideo(p.Video, GravadoEm(p.Json), request))
            : pendentes.FirstOrDefault(p => SemJogo(p) && MesmoVideo(p.Video, GravadoEm(p.Json), request));

        var agora = _time.GetUtcNow().UtcDateTime;
        if (pendente is null)
        {
            pendente = new ResultadoPesPendente { Id = Guid.NewGuid(), EnviadoEm = agora };
            _db.ResultadosPesPendentes.Add(pendente);
        }
        Preencher(pendente, request, jsonBruto, previa, alvo, erro, agora);
        await _db.SaveChangesAsync(ct);

        var avisos = new List<string> { "Resultado segurado: só aparece no site depois de liberado em /admin/resultados-pendentes." };
        if (erro is not null) avisos.Add($"Não deu para validar agora (tente de novo ao liberar): {erro}");
        if (previa is not null) avisos.AddRange(previa.Avisos);

        return new ResultadoPesRespostaDto(
            alvo?.PartidaId ?? Guid.Empty, alvo?.Liga ?? "", previa?.Rodada ?? request.Rodada ?? 0,
            pendente.Casa, pendente.Fora, pendente.GolsCasa, pendente.GolsFora,
            "pendente", false, "AguardandoLiberacao", previa?.EventosGravados ?? 0, previa?.TitularesDasNotas ?? false,
            previa?.NaoIdentificados ?? [], avisos);
    }

    // Sem a linha (logo depois do deploy) vale ligada: esquecer de ligar não pode vazar resultado.
    public async Task<bool> SegurandoAsync(CancellationToken ct) =>
        await _db.ResultadosPesConfig.AsNoTracking().Select(c => (bool?)c.Segurar).FirstOrDefaultAsync(ct) ?? true;

    public async Task DefinirSegurarAsync(bool segurar, CancellationToken ct)
    {
        var cfg = await _db.ResultadosPesConfig.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new ResultadoPesConfig { Id = 1 };
            _db.ResultadosPesConfig.Add(cfg);
        }
        cfg.Segurar = segurar;
        cfg.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ResultadoPesPendenteDto>> PendentesAsync(CancellationToken ct)
    {
        var pendentes = await _db.ResultadosPesPendentes.AsNoTracking().ToListAsync(ct);
        return pendentes
            .OrderBy(p => p.DataHora is null).ThenBy(p => p.DataHora).ThenBy(p => p.EnviadoEm)
            .Select(ParaDto)
            .ToList();
    }

    public async Task<ResultadoPesRespostaDto> LiberarAsync(Guid pendenteId, CancellationToken ct)
    {
        var p = await AcharPendenteAsync(pendenteId, ct);
        try
        {
            var (resposta, _) = await ImportarAsync(Ler(p.Json), p.Json, null, Fixo(p), simular: false, ct);
            _db.ChangeTracker.Clear();
            await _db.ResultadosPesPendentes.Where(x => x.Id == pendenteId).ExecuteDeleteAsync(ct);
            return resposta;
        }
        catch (ResultadoPesException ex)
        {
            _db.ChangeTracker.Clear();
            var erro = Mensagem(ex);
            await _db.ResultadosPesPendentes.Where(x => x.Id == pendenteId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Erro, erro), ct);
            throw;
        }
    }

    public async Task<ResultadoPesPendenteDto> RevalidarAsync(Guid pendenteId, CancellationToken ct)
    {
        var p = await AcharPendenteAsync(pendenteId, ct);
        var request = Ler(p.Json);
        var (previa, alvo, erro) = await ValidarAsync(request, p.Json, null, Fixo(p), ct);

        // A simulação limpa o change tracker: carrega de novo para gravar.
        var pendente = await _db.ResultadosPesPendentes.FirstAsync(x => x.Id == pendenteId, ct);
        Preencher(pendente, request, p.Json, previa, alvo, erro, _time.GetUtcNow().UtcDateTime);
        await _db.SaveChangesAsync(ct);
        return ParaDto(pendente);
    }

    public async Task DescartarAsync(Guid pendenteId, CancellationToken ct) =>
        await _db.ResultadosPesPendentes.Where(x => x.Id == pendenteId).ExecuteDeleteAsync(ct);

    public async Task<PendenteDetalhesDto> DetalhesDoPendenteAsync(Guid pendenteId, CancellationToken ct)
    {
        var p = await AcharPendenteAsync(pendenteId, ct);
        var json = p.Json;
        var r = Ler(json);

        // Times do jogo achado na validação: a partida, ou o jogo do mata-mata que ainda não tem partida.
        (Guid Casa, Guid Fora, Guid LigaId)? jogo = null;
        if (p.PartidaId is Guid partidaId
            && await _db.LigaPartidas.AsNoTracking().Where(x => x.PartidaId == partidaId)
                .Select(x => new { x.TimeCasaId, x.TimeForaId, x.Rodada.LigaId }).FirstOrDefaultAsync(ct) is { } partida)
            jogo = (partida.TimeCasaId, partida.TimeForaId, partida.LigaId);
        else if (p.KnockoutJogoId is Guid koId
                 && await _db.LigaKnockoutJogos.AsNoTracking().Where(k => k.KnockoutJogoId == koId)
                     .Select(k => new { k.TimeCasaId, k.TimeForaId, k.LigaId }).FirstOrDefaultAsync(ct) is
                     { TimeCasaId: Guid kc, TimeForaId: Guid kf } ko)
            jogo = (kc, kf, ko.LigaId);

        // Playoff de acesso simulado com o mando trocado: a importação inverte os lados; aqui também.
        if (jogo is { } j0)
        {
            var nomeCasa = await _db.Teams.AsNoTracking().Where(t => t.TeamId == j0.Casa).Select(t => t.TeamName).FirstAsync(ct);
            if (!NomesPes.MesmoTime(r.Casa?.Time, nomeCasa) && NomesPes.MesmoTime(r.Fora?.Time, nomeCasa))
            {
                json = InverterMando(json);
                r = Ler(json);
            }
        }

        var elenco = new Dictionary<string, List<NomesPes.Candidato>>
        {
            [Casa] = jogo is { } jc ? await CandidatosAsync(jc.Casa, jc.LigaId, ct) : [],
            [Fora] = jogo is { } jf ? await CandidatosAsync(jf.Fora, jf.LigaId, ct) : [],
        };
        // Sem jogo achado, ids só para separar os lados (nada é gravado).
        var timeDoLado = new Dictionary<string, Guid>
        {
            [Casa] = jogo?.Casa ?? Guid.NewGuid(),
            [Fora] = jogo?.Fora ?? Guid.NewGuid(),
        };
        int? IdDe(string lado, string? nome) => nome is null ? null : NomesPes.Casar(nome, elenco[lado]).Id;

        // Lances como vieram do PES; os que casam com o elenco também entram na conta da nota, como na liberação.
        var eventos = new List<PendenteEventoDto>();
        var paraNota = new List<EventoParaNota>();
        foreach (var e in r.Eventos ?? Array.Empty<ResultadoPesEvento>())
        {
            if (e.Lado is not (Casa or Fora) || e.Tipo is null) continue;
            var lado = e.Lado;
            int? minuto = e.Minuto is >= 1 and <= 130 ? e.Minuto : null;
            var principal = e.Tipo switch
            {
                "gol_contra" => IdDe(lado == Casa ? Fora : Casa, e.Jogador),
                "substituicao" => IdDe(lado, e.Entrou),
                _ => IdDe(lado, e.Jogador)
            };
            eventos.Add(new PendenteEventoDto(minuto, lado, e.Tipo, e.Jogador, e.Assistencia, e.Entrou, e.Saiu, principal is not null));

            TipoEvento? tipo = e.Tipo switch
            {
                "gol" => TipoEvento.Gol,
                "gol_contra" => TipoEvento.GolContra,
                "cartao_amarelo" => TipoEvento.CartaoAmarelo,
                "cartao_vermelho" => TipoEvento.CartaoVermelho,
                "substituicao" => TipoEvento.Substituicao,
                _ => null
            };
            if (principal is int id && tipo is TipoEvento t)
            {
                var saiu = t == TipoEvento.Substituicao ? IdDe(lado, e.Saiu) : null;
                paraNota.Add(new EventoParaNota(t, timeDoLado[lado], id, saiu == id ? null : saiu, minuto));
            }
        }

        JogadoresDaPartidaDto? jogadores = null;
        if (r.Jogadores is not null)
        {
            var golsCasa = r.Casa?.Gols ?? p.GolsCasa;
            var golsFora = r.Fora?.Gols ?? p.GolsFora;
            var calculadas = CasarEstatisticas(p.PartidaId ?? Guid.Empty, r, elenco, timeDoLado)
                .Select(linha =>
                {
                    var lado = linha.TimeId == timeDoLado[Casa] ? Casa : Fora;
                    var contexto = NotaDaLiga.ContextoDaPartida(linha,
                        lado == Casa ? golsCasa : golsFora, lado == Casa ? golsFora : golsCasa, paraNota);
                    var nome = linha.JogadorId is int jid
                        ? elenco[lado].FirstOrDefault(c => c.Id == jid)?.Nome ?? linha.NomePes
                        : linha.NomePes;
                    return new NotasOficiais.NotaCalculada(linha, nome, NotaDaLiga.Calcular(linha, contexto));
                })
                .ToList();
            var melhores = NotasOficiais.MelhoresEmCampo(calculadas);
            List<DesempenhoJogadorDto> Lado(string lado) => calculadas
                .Where(c => c.Numeros.TimeId == timeDoLado[lado])
                .OrderBy(c => c.Numeros.Ordem)
                .Select(c => new DesempenhoJogadorDto(c.Numeros.JogadorId, c.Nome, c.Nota, c.Numeros, melhores.Contains(c.Numeros.Id)))
                .ToList();
            jogadores = new JogadoresDaPartidaDto(Lado(Casa), Lado(Fora));
        }

        return new PendenteDetalhesDto(
            p.Id, p.Casa, p.Fora, p.GolsCasa, p.GolsFora,
            eventos, EstatisticasPesService.NumerosDoJson(json), jogadores, AvisosDoExtrator(json), jogo is not null);
    }

    /// <summary>"avisos" do JSON (o que o extrator do Auto_PES21 anotou), um texto por aviso.</summary>
    private static IReadOnlyList<string> AvisosDoExtrator(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("avisos", out var avisos) || avisos.ValueKind != JsonValueKind.Array)
                return [];
            return avisos.EnumerateArray()
                .Select(a => a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : a.GetRawText())
                .Where(a => a.Length > 0)
                .ToList();
        }
        catch (JsonException) { return []; }
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>Roda a importação simulando: o que ela acharia e gravaria, ou por que recusaria.</summary>
    private async Task<(ResultadoPesRespostaDto? Previa, Alvo? Alvo, string? Erro)> ValidarAsync(
        ResultadoPesRequest request, string jsonBruto, Guid? partidaAnterior, JogoFixo? fixo, CancellationToken ct)
    {
        try
        {
            var (previa, alvo) = await ImportarAsync(request, jsonBruto, partidaAnterior, fixo, simular: true, ct);
            return (previa, alvo, null);
        }
        catch (ResultadoPesException ex)
        {
            _db.ChangeTracker.Clear();
            return (null, null, Mensagem(ex));
        }
    }

    private void Preencher(ResultadoPesPendente p, ResultadoPesRequest r, string jsonBruto,
        ResultadoPesRespostaDto? previa, Alvo? alvo, string? erro, DateTime agora)
    {
        p.Video = r.Video is { Length: > 500 } v ? v[..500] : r.Video;
        p.Json = jsonBruto;
        p.PartidaId = alvo?.PartidaId;
        p.KnockoutJogoId = alvo?.KnockoutJogoId;
        p.Competicao = alvo?.Liga;
        p.Onde = alvo?.Onde;
        p.DataHora = alvo?.DataHora;
        p.Casa = previa?.Casa ?? r.Casa!.Time!;
        p.Fora = previa?.Fora ?? r.Fora!.Time!;
        // Pela prévia quando houver: no playoff com mando trocado ela já vem com os lados do site.
        p.GolsCasa = previa?.GolsCasa ?? r.Casa!.Gols!.Value;
        p.GolsFora = previa?.GolsFora ?? r.Fora!.Gols!.Value;
        p.Erro = erro;
        p.Avisos = previa is null
            ? null
            : string.Join('\n', previa.NaoIdentificados
                .Select(n => $"{n.Time}: \"{n.Nome}\" ({n.Papel}{(n.Minuto is int m ? $" {m}'" : "")}) não identificado — {n.Motivo}")
                .Concat(previa.Avisos));
        p.AtualizadoEm = agora;
    }

    private async Task<ResultadoPesPendente> AcharPendenteAsync(Guid id, CancellationToken ct) =>
        await _db.ResultadosPesPendentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ResultadoPesException(ResultadoPesErro.NaoEncontrado, "Resultado pendente não encontrado (já foi liberado ou descartado).");

    private static ResultadoPesRequest Ler(string json) =>
        JsonSerializer.Deserialize<ResultadoPesRequest>(json)
        ?? throw new ResultadoPesException(ResultadoPesErro.Invalido, "JSON guardado está vazio.");

    private static JogoFixo? Fixo(ResultadoPesPendente p) =>
        p.PartidaId is null && p.KnockoutJogoId is null ? null : new JogoFixo(p.PartidaId, p.KnockoutJogoId);

    private static bool SemJogo(ResultadoPesPendente p) => p.PartidaId is null && p.KnockoutJogoId is null;

    private static bool MesmoJogo(ResultadoPesPendente p, Alvo alvo) =>
        (alvo.KnockoutJogoId is not null && p.KnockoutJogoId == alvo.KnockoutJogoId)
        || (alvo.PartidaId is not null && p.PartidaId == alvo.PartidaId);

    private static string Mensagem(ResultadoPesException ex) =>
        ex.Candidatos.Count > 0 ? $"{ex.Message} Candidatos: {string.Join("; ", ex.Candidatos)}." : ex.Message;

    private static ResultadoPesPendenteDto ParaDto(ResultadoPesPendente p) => new(
        p.Id, p.Video, p.Competicao, p.Onde, p.Casa, p.Fora, p.GolsCasa, p.GolsFora, p.DataHora,
        string.IsNullOrEmpty(p.Avisos) ? [] : p.Avisos.Split('\n'),
        p.Erro, p.EnviadoEm);
}
