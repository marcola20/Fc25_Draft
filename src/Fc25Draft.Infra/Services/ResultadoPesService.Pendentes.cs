using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
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
        p.GolsCasa = r.Casa!.Gols!.Value;
        p.GolsFora = r.Fora!.Gols!.Value;
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
