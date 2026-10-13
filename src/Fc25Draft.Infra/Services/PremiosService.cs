using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Prêmios de destaque da temporada, votados pela liga. Os finalistas saem dos números da temporada (notas do PES,
/// gols, assistências, jogos sem sofrer gol, overall que subiu, campanha do clube) e o admin ajusta; o +1 over dos
/// vencedores vai pelos atributos, como a venda rápida, e o Editor PES leva para o save.
/// </summary>
public class PremiosService : IPremiosService
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantos finalistas o site sugere em cada categoria (o admin tira os que sobrarem).</summary>
    private static readonly IReadOnlyDictionary<CategoriaPremio, int> Sugeridos = new Dictionary<CategoriaPremio, int>
    {
        [CategoriaPremio.MVP] = 5,
        [CategoriaPremio.ROY] = 6,
        [CategoriaPremio.MIP] = 8,
        [CategoriaPremio.DPOY] = 12,
        [CategoriaPremio.GKOY] = 5,
        [CategoriaPremio.COTY] = 4,
    };

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public PremiosService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => HorarioDeBrasilia.Agora(_time);

    // ── Leitura ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<int>> TemporadasAsync(CancellationToken ct) =>
        await _db.VotacoesPremios.AsNoTracking().OrderByDescending(v => v.Temporada).Select(v => v.Temporada).ToListAsync(ct);

    public async Task<VotacaoPremiosDto?> GetVisivelAsync(CancellationToken ct)
    {
        var temporada = await _db.VotacoesPremios.AsNoTracking()
            .Where(v => v.Situacao != SituacaoVotacao.Preparando)
            .OrderByDescending(v => v.Temporada)
            .Select(v => (int?)v.Temporada)
            .FirstOrDefaultAsync(ct);

        return temporada is int t ? await GetAsync(t, false, ct) : null;
    }

    public async Task<VotacaoPremiosDto?> GetAsync(int temporada, bool comVotos, CancellationToken ct)
    {
        var votacao = await _db.VotacoesPremios.AsNoTracking().FirstOrDefaultAsync(v => v.Temporada == temporada, ct);
        if (votacao is null) return null;

        var candidatos = await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => c.Temporada == temporada)
            .Select(c => new
            {
                c.CandidatoId, c.Categoria, c.PlayerId, c.TreinadorId, c.TimeId, c.Resumo, c.Vencedor,
                c.OverParaPlayerId, c.OverAplicadoEm,
                Jogador = c.Player == null ? null : c.Player.Name,
                PositionId = c.Player == null ? (short?)null : c.Player.PositionId,
                Overall = c.Player == null ? (int?)null : c.Player.Overall,
                Treinador = c.Treinador == null ? null : c.Treinador.Nome,
                TimeNome = c.Time == null ? null : c.Time.TeamName,
                OverParaNome = c.OverPara == null ? null : c.OverPara.Name,
            })
            .ToListAsync(ct);

        // O placar só aparece para todo mundo depois de publicado; o admin vê sempre.
        var mostrarVotos = comVotos || votacao.Situacao == SituacaoVotacao.Publicada;
        var votos = mostrarVotos
            ? await _db.PremiosVotos.AsNoTracking()
                .Where(v => v.Candidato.Temporada == temporada)
                .GroupBy(v => v.CandidatoId)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Total, ct)
            : new Dictionary<Guid, int>();

        var categorias = PremiosTemporada.Todas
            .Select(regra => new PremioCategoriaDto(regra, candidatos
                .Where(c => c.Categoria == regra.Categoria)
                .Select(c => new PremioCandidatoDto(
                    c.CandidatoId, c.Categoria, c.PlayerId, c.TreinadorId,
                    c.Jogador ?? c.Treinador ?? "?",
                    c.TimeId, c.TimeNome,
                    c.PositionId is short pos ? ((int)pos).ToPositionSigla() : null,
                    c.Overall, c.Resumo,
                    mostrarVotos ? votos.GetValueOrDefault(c.CandidatoId) : null,
                    c.Vencedor, c.OverParaPlayerId, c.OverParaNome, c.OverAplicadoEm))
                .OrderByDescending(c => c.Votos ?? 0).ThenByDescending(c => c.Vencedor).ThenBy(c => c.Nome)
                .ToList()))
            .ToList();

        var eleitores = await _db.Treinadores.AsNoTracking().CountAsync(t => t.Ativo, ct);
        var votaram = await _db.PremiosVotos.AsNoTracking()
            .Where(v => v.Candidato.Temporada == temporada)
            .Select(v => v.EleitorId).Distinct().CountAsync(ct);

        return new VotacaoPremiosDto(
            temporada, votacao.Situacao, votacao.AbertaEm, votacao.EncerradaEm, votacao.PublicadaEm,
            categorias, eleitores, votaram);
    }

    // ── Admin: montar ────────────────────────────────────────────────────────

    public async Task CriarAsync(int temporada, CancellationToken ct)
    {
        if (await _db.VotacoesPremios.AnyAsync(v => v.Temporada == temporada, ct))
            throw new InvalidOperationException($"A votação de {temporada} já existe.");

        _db.VotacoesPremios.Add(new VotacaoPremios { Temporada = temporada, Situacao = SituacaoVotacao.Preparando, CriadaEm = Agora });

        var numeros = await NumerosAsync(temporada, ct);
        foreach (var regra in PremiosTemporada.Todas)
            _db.PremiosCandidatos.AddRange(await SugerirAsync(temporada, regra.Categoria, numeros, ct));

        await _db.SaveChangesAsync(ct);
    }

    public async Task SugerirDeNovoAsync(int temporada, CategoriaPremio categoria, CancellationToken ct)
    {
        await ExigirAsync(temporada, SituacaoVotacao.Preparando, "Só dá para refazer os finalistas antes de abrir a votação.", ct);

        _db.PremiosCandidatos.RemoveRange(
            await _db.PremiosCandidatos.Where(c => c.Temporada == temporada && c.Categoria == categoria).ToListAsync(ct));
        _db.PremiosCandidatos.AddRange(await SugerirAsync(temporada, categoria, await NumerosAsync(temporada, ct), ct));

        await _db.SaveChangesAsync(ct);
    }

    public async Task AdicionarJogadorAsync(int temporada, CategoriaPremio categoria, int playerId, CancellationToken ct)
    {
        await ExigirAsync(temporada, SituacaoVotacao.Preparando, "Os finalistas só mudam antes de abrir a votação.", ct);
        if (PremiosTemporada.De(categoria).DeTreinador)
            throw new InvalidOperationException("Nessa categoria o finalista é um treinador.");

        if (await _db.PremiosCandidatos.AnyAsync(c => c.Temporada == temporada && c.Categoria == categoria && c.PlayerId == playerId, ct))
            throw new InvalidOperationException("Esse jogador já é finalista.");

        var jogador = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Jogador não encontrado.");

        var numeros = await NumerosAsync(temporada, ct);
        var linha = numeros.Jogadores.GetValueOrDefault(playerId);

        _db.PremiosCandidatos.Add(new PremioCandidato
        {
            CandidatoId = Guid.NewGuid(),
            Temporada = temporada,
            Categoria = categoria,
            PlayerId = playerId,
            TimeId = linha?.TimeId ?? jogador.CurrentTeamId,
            Resumo = linha is null ? null : Resumo(categoria, linha, numeros.Ganho.GetValueOrDefault(playerId)),
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task AdicionarTreinadorAsync(int temporada, Guid treinadorId, CancellationToken ct)
    {
        await ExigirAsync(temporada, SituacaoVotacao.Preparando, "Os finalistas só mudam antes de abrir a votação.", ct);

        if (await _db.PremiosCandidatos.AnyAsync(c => c.Temporada == temporada && c.TreinadorId == treinadorId, ct))
            throw new InvalidOperationException("Esse treinador já é finalista.");

        var clube = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.TreinadorId == treinadorId && p.Ate == null)
            .Select(p => (Guid?)p.TimeId)
            .FirstOrDefaultAsync(ct);

        _db.PremiosCandidatos.Add(new PremioCandidato
        {
            CandidatoId = Guid.NewGuid(),
            Temporada = temporada,
            Categoria = CategoriaPremio.COTY,
            TreinadorId = treinadorId,
            TimeId = clube,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverCandidatoAsync(Guid candidatoId, CancellationToken ct)
    {
        var candidato = await _db.PremiosCandidatos.FirstOrDefaultAsync(c => c.CandidatoId == candidatoId, ct)
            ?? throw new InvalidOperationException("Finalista não encontrado.");
        await ExigirAsync(candidato.Temporada, SituacaoVotacao.Preparando, "Os finalistas só mudam antes de abrir a votação.", ct);

        _db.PremiosCandidatos.Remove(candidato);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PremioJogadorOpcaoDto>> BuscarJogadoresAsync(string termo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 2) return Array.Empty<PremioJogadorOpcaoDto>();
        var padrao = $"%{termo.Trim()}%";

        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, padrao))
            .OrderByDescending(p => p.Overall)
            .Take(15)
            .Select(p => new { p.PlayerId, p.Name, p.PositionId, p.Overall, Time = p.CurrentTeam == null ? null : p.CurrentTeam.TeamName })
            .ToListAsync(ct);

        return jogadores.Select(j => new PremioJogadorOpcaoDto(j.PlayerId, j.Name, ((int)j.PositionId).ToPositionSigla(), j.Overall, j.Time)).ToList();
    }

    public async Task<IReadOnlyList<PremioTreinadorOpcaoDto>> TreinadoresAsync(CancellationToken ct)
    {
        var clubes = await ClubesAtuaisAsync(ct);

        return (await _db.Treinadores.AsNoTracking()
                .Where(t => t.Ativo)
                .OrderBy(t => t.Nome)
                .Select(t => new { t.TreinadorId, t.Nome })
                .ToListAsync(ct))
            .Select(t => new PremioTreinadorOpcaoDto(t.TreinadorId, t.Nome, clubes.GetValueOrDefault(t.TreinadorId).Nome))
            .ToList();
    }

    // ── Admin: andamento ─────────────────────────────────────────────────────

    public async Task AbrirAsync(int temporada, CancellationToken ct)
    {
        var votacao = await ExigirAsync(temporada, SituacaoVotacao.Preparando, "A votação já foi aberta.", ct);

        var comFinalista = (await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => c.Temporada == temporada)
            .Select(c => c.Categoria).Distinct().ToListAsync(ct)).ToHashSet();
        var vazias = PremiosTemporada.Todas.Where(r => !comFinalista.Contains(r.Categoria)).Select(r => r.Sigla).ToList();
        if (vazias.Count > 0)
            throw new InvalidOperationException($"Sem finalistas em: {string.Join(", ", vazias)}.");

        votacao.Situacao = SituacaoVotacao.Aberta;
        votacao.AbertaEm = Agora;
        await _db.SaveChangesAsync(ct);
    }

    public async Task EncerrarAsync(int temporada, CancellationToken ct)
    {
        var votacao = await ExigirAsync(temporada, SituacaoVotacao.Aberta, "A votação não está aberta.", ct);
        votacao.Situacao = SituacaoVotacao.Encerrada;
        votacao.EncerradaEm = Agora;

        // Sugestão de vencedores: os mais votados. Empate na linha de corte fica para o admin decidir.
        var candidatos = await _db.PremiosCandidatos.Where(c => c.Temporada == temporada).ToListAsync(ct);
        var votos = await _db.PremiosVotos.AsNoTracking()
            .Where(v => v.Candidato.Temporada == temporada)
            .GroupBy(v => v.CandidatoId)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

        foreach (var regra in PremiosTemporada.Todas)
        {
            var daCategoria = candidatos.Where(c => c.Categoria == regra.Categoria).ToList();
            foreach (var c in daCategoria) c.Vencedor = false;

            var comVoto = daCategoria.Where(c => votos.GetValueOrDefault(c.CandidatoId) > 0)
                .OrderByDescending(c => votos.GetValueOrDefault(c.CandidatoId)).ToList();
            if (comVoto.Count == 0) continue;

            var corte = votos.GetValueOrDefault(comVoto[Math.Min(regra.Vencedores, comVoto.Count) - 1].CandidatoId);
            var acima = comVoto.Where(c => votos.GetValueOrDefault(c.CandidatoId) > corte).ToList();
            var noCorte = comVoto.Where(c => votos.GetValueOrDefault(c.CandidatoId) == corte).ToList();

            foreach (var c in acima) c.Vencedor = true;
            if (acima.Count + noCorte.Count <= regra.Vencedores)
                foreach (var c in noCorte) c.Vencedor = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ReabrirAsync(int temporada, CancellationToken ct)
    {
        var votacao = await ExigirAsync(temporada, SituacaoVotacao.Encerrada, "Só dá para reabrir uma votação encerrada (e ainda não publicada).", ct);
        votacao.Situacao = SituacaoVotacao.Aberta;
        votacao.EncerradaEm = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task VoltarParaMontagemAsync(int temporada, CancellationToken ct)
    {
        var votacao = await ExigirAsync(temporada, SituacaoVotacao.Aberta, "A votação não está aberta.", ct);
        if (await _db.PremiosVotos.AnyAsync(v => v.Candidato.Temporada == temporada, ct))
            throw new InvalidOperationException("Já tem voto: não dá para voltar a mexer nos finalistas.");

        votacao.Situacao = SituacaoVotacao.Preparando;
        votacao.AbertaEm = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PremioEleitorDto>> EleitoresAsync(int temporada, CancellationToken ct)
    {
        var votaram = (await _db.PremiosVotos.AsNoTracking()
            .Where(v => v.Candidato.Temporada == temporada)
            .Select(v => v.EleitorId).Distinct().ToListAsync(ct)).ToHashSet();
        var clubes = await ClubesAtuaisAsync(ct);

        return (await _db.Treinadores.AsNoTracking()
                .Where(t => t.Ativo)
                .Select(t => new { t.TreinadorId, t.Nome })
                .ToListAsync(ct))
            .Select(t => new PremioEleitorDto(t.TreinadorId, t.Nome, clubes.GetValueOrDefault(t.TreinadorId).Nome, votaram.Contains(t.TreinadorId)))
            .OrderBy(e => e.Votou).ThenBy(e => e.Nome)
            .ToList();
    }

    public async Task<IReadOnlyList<PremioVotosDoEleitorDto>> CedulasAsync(int temporada, CancellationToken ct)
    {
        var votos = await _db.PremiosVotos.AsNoTracking()
            .Where(v => v.Candidato.Temporada == temporada)
            .Select(v => new { v.EleitorId, v.Eleitor.Nome, v.CandidatoId })
            .ToListAsync(ct);

        return votos
            .GroupBy(v => new { v.EleitorId, v.Nome })
            .Select(g => new PremioVotosDoEleitorDto(g.Key.EleitorId, g.Key.Nome, g.Select(v => v.CandidatoId).ToList()))
            .OrderBy(c => c.Nome)
            .ToList();
    }

    public async Task MarcarVencedorAsync(Guid candidatoId, bool vencedor, CancellationToken ct)
    {
        var candidato = await _db.PremiosCandidatos.FirstOrDefaultAsync(c => c.CandidatoId == candidatoId, ct)
            ?? throw new InvalidOperationException("Finalista não encontrado.");
        await ExigirAsync(candidato.Temporada, SituacaoVotacao.Encerrada, "Os vencedores se confirmam com a votação encerrada, antes de publicar.", ct);

        candidato.Vencedor = vencedor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task PublicarAsync(int temporada, CancellationToken ct)
    {
        var votacao = await ExigirAsync(temporada, SituacaoVotacao.Encerrada, "Encerre a votação antes de publicar.", ct);

        var comVencedor = (await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => c.Temporada == temporada && c.Vencedor)
            .Select(c => c.Categoria).Distinct().ToListAsync(ct)).ToHashSet();
        var semVencedor = PremiosTemporada.Todas.Where(r => !comVencedor.Contains(r.Categoria)).Select(r => r.Sigla).ToList();
        if (semVencedor.Count > 0)
            throw new InvalidOperationException($"Falta marcar o vencedor de: {string.Join(", ", semVencedor)}.");

        votacao.Situacao = SituacaoVotacao.Publicada;
        votacao.PublicadaEm = Agora;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> AplicarOverAsync(Guid candidatoId, int? paraPlayerId, CancellationToken ct)
    {
        var candidato = await _db.PremiosCandidatos.Include(c => c.Votacao)
            .FirstOrDefaultAsync(c => c.CandidatoId == candidatoId, ct)
            ?? throw new InvalidOperationException("Finalista não encontrado.");

        var regra = PremiosTemporada.De(candidato.Categoria);
        if (!regra.DaOver) throw new InvalidOperationException($"O {regra.Sigla} é prêmio de honra, sem over.");
        if (candidato.Votacao.Situacao != SituacaoVotacao.Publicada) throw new InvalidOperationException("Publique os vencedores antes de dar o over.");
        if (!candidato.Vencedor) throw new InvalidOperationException("Só o vencedor ganha o over.");
        if (candidato.OverAplicadoEm is not null) throw new InvalidOperationException("O over desse prêmio já foi dado.");

        var alvoId = paraPlayerId ?? candidato.PlayerId ?? throw new InvalidOperationException("Prêmio sem jogador.");
        var jogador = await _db.Players.Include(p => p.Atributos).FirstOrDefaultAsync(p => p.PlayerId == alvoId, ct)
            ?? throw new InvalidOperationException("Jogador não encontrado.");

        var titulo = PremiosTemporada.Titulo(candidato.Categoria, candidato.Temporada);
        var motivo = alvoId == candidato.PlayerId ? $"Prêmio {titulo}" : $"Prêmio {titulo} (repasse)";
        var antesOverall = jogador.Overall;
        var agora = _time.GetUtcNow().UtcDateTime;

        candidato.OverParaPlayerId = alvoId;
        candidato.OverAplicadoEm = Agora;

        if (jogador.Atributos?.PosicaoPes is not int pos)
        {
            // Sem atributos do jogo não há o que mandar ao Editor PES: só o overall do site sobe.
            jogador.Overall = Math.Min(AtributosPes.Maximo, jogador.Overall + 1);
            await _db.SaveChangesAsync(ct);
            return $"{jogador.Name}: overall {antesOverall} → {jogador.Overall} só no site (sem atributos do jogo — dê o over no PES à mão).";
        }

        var dto = AtributosPes.ParaDto(jogador.Atributos);
        var antes = OverallPes.Valores(dto);
        var evolucao = OverallPes.Evoluir(antes, pos, dto.EstiloDeJogo, dto.PeFracoUso, dto.PeFracoPrecisao, antesOverall + 1);
        if (evolucao.Novos.SequenceEqual(antes))
            throw new InvalidOperationException($"{jogador.Name}: os atributos já estão no limite, nada mudou.");

        for (var i = 0; i < antes.Length; i++)
            AtributosPes.Todos[i].Set(dto, evolucao.Novos[i]);
        AtributosPes.Aplicar(dto, jogador.Atributos);
        OverallPes.Recalcular(jogador);

        var registro = new EvolucaoPes
        {
            PlayerId = jogador.PlayerId,
            Motivo = motivo,
            CriadaEmUtc = agora,
            OverallAntes = antesOverall,
            OverallDepois = jogador.Overall,
            Mudancas = EvolucaoPes.EscreverMudancas(antes, evolucao.Novos),
        };
        _db.EvolucoesPes.Add(registro);
        await _db.SaveChangesAsync(ct);

        candidato.EvolucaoPesId = registro.EvolucaoPesId;
        await _db.SaveChangesAsync(ct);

        return $"{jogador.Name}: overall {antesOverall} → {jogador.Overall} · {OverallPes.DescreverMudancas(antes, evolucao.Novos)}. " +
               "Abra o Editor PES para levar ao save.";
    }

    public async Task<string> DesfazerOverAsync(Guid candidatoId, CancellationToken ct)
    {
        var candidato = await _db.PremiosCandidatos.FirstOrDefaultAsync(c => c.CandidatoId == candidatoId, ct)
            ?? throw new InvalidOperationException("Finalista não encontrado.");
        if (candidato.OverAplicadoEm is null || candidato.OverParaPlayerId is not int playerId)
            throw new InvalidOperationException("O over desse prêmio não foi dado.");

        var jogador = await _db.Players.Include(p => p.Atributos).FirstAsync(p => p.PlayerId == playerId, ct);

        if (candidato.EvolucaoPesId is int evolucaoId)
        {
            var evolucao = await _db.EvolucoesPes.FirstAsync(e => e.EvolucaoPesId == evolucaoId, ct);
            if (evolucao.AplicadaNoJogoEmUtc is not null)
                throw new InvalidOperationException("O Editor PES já levou esse over para o save: desfaça no jogo e ajuste na ficha.");

            var dto = AtributosPes.ParaDto(jogador.Atributos!);
            var valores = OverallPes.Valores(dto);
            var mudancas = evolucao.LerMudancas();
            for (var i = 0; i < valores.Length; i++)
                AtributosPes.Todos[i].Set(dto, Math.Clamp(valores[i] - mudancas[i], AtributosPes.Minimo, AtributosPes.Maximo));
            AtributosPes.Aplicar(dto, jogador.Atributos!);
            OverallPes.Recalcular(jogador);
            _db.EvolucoesPes.Remove(evolucao);
        }
        else
        {
            jogador.Overall = Math.Max(1, jogador.Overall - 1);
        }

        candidato.OverParaPlayerId = null;
        candidato.OverAplicadoEm = null;
        candidato.EvolucaoPesId = null;
        await _db.SaveChangesAsync(ct);

        return $"{jogador.Name}: over desfeito, overall {jogador.Overall}.";
    }

    public async Task<IReadOnlyList<PremioJogadorOpcaoDto>> CompanheirosAsync(Guid candidatoId, CancellationToken ct)
    {
        var candidato = await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => c.CandidatoId == candidatoId)
            .Select(c => new { c.PlayerId, Clube = c.Player == null ? c.TimeId : c.Player.CurrentTeamId ?? c.TimeId })
            .FirstOrDefaultAsync(ct);
        if (candidato?.Clube is not Guid clube) return Array.Empty<PremioJogadorOpcaoDto>();

        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => p.CurrentTeamId == clube && p.PlayerId != candidato.PlayerId && p.AposentadoNaTemporada == null)
            .OrderByDescending(p => p.Overall)
            .Select(p => new { p.PlayerId, p.Name, p.PositionId, p.Overall, Time = p.CurrentTeam!.TeamName })
            .ToListAsync(ct);

        return jogadores.Select(j => new PremioJogadorOpcaoDto(j.PlayerId, j.Name, ((int)j.PositionId).ToPositionSigla(), j.Overall, j.Time)).ToList();
    }

    // ── Pessoal ──────────────────────────────────────────────────────────────

    public async Task<PremioCedulaDto> CedulaAsync(int temporada, Guid eleitorId, CancellationToken ct)
    {
        var votos = await _db.PremiosVotos.AsNoTracking()
            .Where(v => v.EleitorId == eleitorId && v.Candidato.Temporada == temporada)
            .Select(v => v.CandidatoId)
            .ToListAsync(ct);

        return new PremioCedulaDto(eleitorId, await ClubeDoEleitorAsync(eleitorId, ct), votos);
    }

    public async Task VotarAsync(int temporada, Guid eleitorId, IReadOnlyCollection<Guid> candidatos, CancellationToken ct)
    {
        await ExigirAsync(temporada, SituacaoVotacao.Aberta, "A votação não está aberta.", ct);

        var eleitor = await _db.Treinadores.AsNoTracking().FirstOrDefaultAsync(t => t.TreinadorId == eleitorId, ct)
            ?? throw new InvalidOperationException("Treinador não encontrado.");
        if (!eleitor.Ativo) throw new InvalidOperationException("Quem não está mais na liga não vota.");

        var marcados = candidatos.Distinct().ToList();
        var escolhidos = await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => marcados.Contains(c.CandidatoId))
            .Select(c => new { c.CandidatoId, c.Temporada, c.Categoria, c.TimeId, c.TreinadorId })
            .ToListAsync(ct);

        if (escolhidos.Count != marcados.Count || escolhidos.Any(c => c.Temporada != temporada))
            throw new InvalidOperationException("Tem voto em quem não é finalista dessa temporada.");

        foreach (var grupo in escolhidos.GroupBy(c => c.Categoria))
        {
            var regra = PremiosTemporada.De(grupo.Key);
            if (grupo.Count() > regra.Votos)
                throw new InvalidOperationException($"{regra.Sigla}: são no máximo {regra.Votos} votos.");
        }

        var clube = await ClubeDoEleitorAsync(eleitorId, ct);
        if (escolhidos.Any(c => c.TreinadorId == eleitorId || (clube is not null && c.TimeId == clube)))
            throw new InvalidOperationException("Não vale votar no próprio clube.");

        _db.PremiosVotos.RemoveRange(await _db.PremiosVotos
            .Where(v => v.EleitorId == eleitorId && v.Candidato.Temporada == temporada)
            .ToListAsync(ct));

        var agora = Agora;
        _db.PremiosVotos.AddRange(marcados.Select(id => new PremioVoto
        {
            VotoId = Guid.NewGuid(),
            CandidatoId = id,
            EleitorId = eleitorId,
            VotadoEm = agora,
        }));

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PremioConquistadoDto>> DoJogadorAsync(int playerId, CancellationToken ct) =>
        (await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => c.PlayerId == playerId && c.Vencedor && c.Votacao.Situacao == SituacaoVotacao.Publicada)
            .OrderByDescending(c => c.Temporada).ThenBy(c => c.Categoria)
            .Select(c => new { c.Temporada, c.Categoria, Time = c.Time == null ? null : c.Time.TeamName })
            .ToListAsync(ct))
        .Select(c => new PremioConquistadoDto(c.Temporada, c.Categoria, c.Time))
        .ToList();

    public async Task<IReadOnlyList<PremioConquistadoDto>> DoTreinadorAsync(Guid treinadorId, CancellationToken ct) =>
        (await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => c.TreinadorId == treinadorId && c.Vencedor && c.Votacao.Situacao == SituacaoVotacao.Publicada)
            .OrderByDescending(c => c.Temporada)
            .Select(c => new { c.Temporada, c.Categoria, Time = c.Time == null ? null : c.Time.TeamName })
            .ToListAsync(ct))
        .Select(c => new PremioConquistadoDto(c.Temporada, c.Categoria, c.Time))
        .ToList();

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var votacoes = await _db.VotacoesPremios.AsNoTracking()
            .Where(v => v.Situacao != SituacaoVotacao.Preparando)
            .ToListAsync(ct);
        if (votacoes.Count == 0) return Array.Empty<PlantaoNoticiaDto>();

        var noticias = new List<PlantaoNoticiaDto>();
        foreach (var v in votacoes.Where(v => v.AbertaEm is not null))
            noticias.Add(new PlantaoNoticiaDto(v.AbertaEm!.Value, PlantaoCategoria.Carreira, "🗳️",
                $"Aberta a votação dos prêmios de {v.Temporada}",
                "MVP, ROY, MIP, DPOY, GKOY e COTY: os treinadores escolhem os destaques da temporada",
                $"/premios?temporada={v.Temporada}", null));

        var publicadas = votacoes.Where(v => v.Situacao == SituacaoVotacao.Publicada && v.PublicadaEm is not null)
            .ToDictionary(v => v.Temporada, v => v.PublicadaEm!.Value);
        if (publicadas.Count == 0) return noticias;

        var temporadas = publicadas.Keys.ToList();
        var vencedores = await _db.PremiosCandidatos.AsNoTracking()
            .Where(c => temporadas.Contains(c.Temporada) && c.Vencedor)
            .Select(c => new
            {
                c.Temporada, c.Categoria, c.PlayerId,
                Nome = c.Player != null ? c.Player.Name : c.Treinador!.Nome,
                Time = c.Time == null ? null : c.Time.TeamName,
            })
            .ToListAsync(ct);

        foreach (var c in vencedores)
        {
            var regra = PremiosTemporada.De(c.Categoria);
            var varios = regra.Vencedores > 1;
            noticias.Add(new PlantaoNoticiaDto(publicadas[c.Temporada], PlantaoCategoria.Carreira, regra.Emoji,
                varios ? $"{c.Nome} está entre os {regra.Sigla} de {c.Temporada}" : $"{c.Nome} é o {regra.Sigla} de {c.Temporada}",
                $"{regra.Nome}, eleito pelos treinadores{(c.Time is null ? "" : $" · {c.Time}")}",
                c.PlayerId is int id ? $"/players/details/{id}" : $"/premios?temporada={c.Temporada}",
                c.Time, c.PlayerId));
        }

        return noticias.OrderByDescending(n => n.Data).ToArray();
    }

    // ── Bastidores ───────────────────────────────────────────────────────────

    private async Task<VotacaoPremios> ExigirAsync(int temporada, SituacaoVotacao situacao, string erro, CancellationToken ct)
    {
        var votacao = await _db.VotacoesPremios.FirstOrDefaultAsync(v => v.Temporada == temporada, ct)
            ?? throw new InvalidOperationException($"Não há votação de {temporada}.");
        if (votacao.Situacao != situacao) throw new InvalidOperationException(erro);
        return votacao;
    }

    private async Task<Guid?> ClubeDoEleitorAsync(Guid treinadorId, CancellationToken ct) =>
        await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.TreinadorId == treinadorId && p.Ate == null)
            .Select(p => (Guid?)p.TimeId)
            .FirstOrDefaultAsync(ct);

    private async Task<Dictionary<Guid, (Guid TimeId, string Nome)>> ClubesAtuaisAsync(CancellationToken ct) =>
        (await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null)
            .Select(p => new { p.TreinadorId, p.TimeId, p.Time.TeamName })
            .ToListAsync(ct))
        .GroupBy(p => p.TreinadorId)
        .ToDictionary(g => g.Key, g => (g.First().TimeId, g.First().TeamName));

    /// <summary>A temporada de um jogador: jogos com nota do PES, média, gols, assistências e jogos sem sofrer gol.</summary>
    private sealed record Linha(
        int PlayerId, string Nome, short PositionId, int Overall, Guid? TimeId,
        int Jogos, decimal Media, int MelhorEmCampo, int Gols, int Assistencias, int SemSofrer);

    /// <summary>
    /// <see cref="TemNotas"/>: a temporada tem as notas do PES (desde 2010). Sem elas só há gols e assistências, e a
    /// defesa sai dos clubes que menos sofreram gol (<see cref="Defesas"/>: gols sofridos e jogos de cada clube).
    /// </summary>
    private sealed record Numeros(
        IReadOnlyDictionary<int, Linha> Jogadores,
        IReadOnlyDictionary<int, (int Antes, int Depois)> Ganho,
        HashSet<int> Estreantes,
        int MinimoDeJogos,
        bool TemNotas,
        IReadOnlyDictionary<Guid, (int Sofridos, int Jogos)> Defesas);

    /// <summary>Os números da temporada: Liga e Copa (a Supercopa com o ano dela abre a temporada seguinte).</summary>
    private async Task<Numeros> NumerosAsync(int temporada, CancellationToken ct)
    {
        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.Rodada.Liga.Temporada == temporada && p.Rodada.Liga.Tipo != TipoCompetition.Supercopa
                        && (p.Status == PartidaStatus.Encerrada || p.EncerradaEm != null) && !p.IsWO)
            .Select(p => new { p.PartidaId, p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, Quando = p.EncerradaEm ?? p.Rodada.DataHora })
            .ToListAsync(ct);
        var ids = partidas.Select(p => p.PartidaId).ToList();

        // Gols sofridos por (partida, time), para os jogos sem sofrer gol.
        var sofreu = partidas
            .SelectMany(p => new[] { (p.PartidaId, Time: p.TimeCasaId, Gols: p.GolsFora), (p.PartidaId, Time: p.TimeForaId, Gols: p.GolsCasa) })
            .ToDictionary(x => (x.PartidaId, x.Time), x => x.Gols);

        var notas = await _db.LigaNotasJogadores.AsNoTracking()
            .Where(n => ids.Contains(n.PartidaId))
            .Select(n => new { n.PartidaId, n.JogadorId, n.TimeId, n.Nota, n.MelhorEmCampo })
            .ToListAsync(ct);

        var gols = await _db.LigaEventos.AsNoTracking()
            .Where(e => ids.Contains(e.PartidaId) && e.Tipo == TipoEvento.Gol)
            .Select(e => new { e.JogadorId, e.AssistenteId, e.TimeId })
            .ToListAsync(ct);
        var golsPorJogador = gols.GroupBy(g => g.JogadorId).ToDictionary(g => g.Key, g => g.Count());
        var assistPorJogador = gols.Where(g => g.AssistenteId != null).GroupBy(g => g.AssistenteId!.Value).ToDictionary(g => g.Key, g => g.Count());

        // Sem nota (temporadas antigas), quem fez gol ou deu assistência entra pelos números de gol.
        var clubePorGol = gols.Select(g => (Id: g.JogadorId, g.TimeId))
            .Concat(gols.Where(g => g.AssistenteId != null).Select(g => (Id: g.AssistenteId!.Value, g.TimeId)))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => x.TimeId).OrderByDescending(t => t.Count()).First().Key);
        var envolvidos = notas.Select(n => n.JogadorId).Concat(clubePorGol.Keys).Distinct().ToList();
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => envolvidos.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.Name, p.PositionId, p.Overall, p.CurrentTeamId })
            .ToDictionaryAsync(p => p.PlayerId, ct);

        var notasPorJogador = notas.GroupBy(n => n.JogadorId).ToDictionary(g => g.Key, g => g.ToList());
        var linhas = envolvidos
            .Where(jogadores.ContainsKey)
            .Select(id =>
            {
                var j = jogadores[id];
                var dele = notasPorJogador.GetValueOrDefault(id) ?? [];
                Guid? clube = dele.Count > 0
                    ? dele.GroupBy(n => n.TimeId).OrderByDescending(t => t.Count()).First().Key
                    : clubePorGol.TryGetValue(id, out var doGol) ? doGol : j.CurrentTeamId;
                return new Linha(
                    id, j.Name, j.PositionId, j.Overall, clube,
                    dele.Count, dele.Count == 0 ? 0 : Math.Round(dele.Average(n => n.Nota), 2), dele.Count(n => n.MelhorEmCampo),
                    golsPorJogador.GetValueOrDefault(id), assistPorJogador.GetValueOrDefault(id),
                    dele.Count(n => sofreu.GetValueOrDefault((n.PartidaId, n.TimeId), 1) == 0));
            })
            .ToDictionary(l => l.PlayerId);

        // Estreantes: nenhuma nota nem escalação em temporada anterior.
        var jaJogaram = (await _db.LigaNotasJogadores.AsNoTracking()
                .Where(n => n.Partida.Rodada.Liga.Temporada < temporada)
                .Select(n => n.JogadorId)
                .Union(_db.LigaEscalacoes.AsNoTracking()
                    .Where(e => e.Partida.Rodada.Liga.Temporada < temporada)
                    .Select(e => e.JogadorId))
                .Union(_db.LigaEventos.AsNoTracking()
                    .Where(e => e.Partida.Rodada.Liga.Temporada < temporada && e.Tipo == TipoEvento.Gol)
                    .Select(e => e.JogadorId))
                .Union(_db.LigaEventos.AsNoTracking()
                    .Where(e => e.Partida.Rodada.Liga.Temporada < temporada && e.Tipo == TipoEvento.Gol && e.AssistenteId != null)
                    .Select(e => e.AssistenteId!.Value))
                .ToListAsync(ct))
            .ToHashSet();
        var estreantes = linhas.Keys.Where(id => !jaJogaram.Contains(id)).ToHashSet();

        // Overall que subiu durante a temporada (venda rápida, ajustes), sem a evolução da virada e sem prêmio.
        var inicio = partidas.Count == 0 ? (DateTime?)null : partidas.Min(p => p.Quando);
        var ganho = new Dictionary<int, (int Antes, int Depois)>();
        if (inicio is DateTime desde)
        {
            var desdeUtc = desde.AddHours(3); // horário de Brasília → UTC
            var evolucoes = await _db.EvolucoesPes.AsNoTracking()
                .Where(e => e.CriadaEmUtc >= desdeUtc && !e.Motivo.StartsWith("Temporada ") && !e.Motivo.StartsWith("Prêmio"))
                .OrderBy(e => e.CriadaEmUtc)
                .Select(e => new { e.PlayerId, e.OverallAntes, e.OverallDepois })
                .ToListAsync(ct);

            ganho = evolucoes
                .GroupBy(e => e.PlayerId)
                .Select(g => (g.Key, Antes: g.First().OverallAntes, Depois: g.Last().OverallDepois))
                .Where(x => x.Depois > x.Antes)
                .ToDictionary(x => x.Key, x => (x.Antes, x.Depois));
        }

        var defesas = sofreu
            .GroupBy(x => x.Key.Time)
            .ToDictionary(g => g.Key, g => (Sofridos: g.Sum(x => x.Value), Jogos: g.Count()));

        var temNotas = notas.Count > 0;
        var maisJogos = linhas.Count == 0 ? 0 : linhas.Values.Max(l => l.Jogos);
        return new Numeros(linhas, ganho, estreantes, temNotas ? Math.Max(3, (int)Math.Ceiling(maisJogos * 0.4)) : 0, temNotas, defesas);
    }

    private async Task<IReadOnlyList<PremioCandidato>> SugerirAsync(int temporada, CategoriaPremio categoria, Numeros n, CancellationToken ct)
    {
        var quantos = Sugeridos[categoria];
        if (categoria == CategoriaPremio.COTY)
            return await SugerirTreinadoresAsync(temporada, quantos, ct);
        if (!n.TemNotas)
            return await SugerirSemNotasAsync(temporada, categoria, quantos, n, ct);

        var comJogos = n.Jogadores.Values.Where(l => l.Jogos >= n.MinimoDeJogos).ToList();

        IEnumerable<Linha> escolhidos = categoria switch
        {
            // Os de melhor nota e os que mais participaram de gol.
            CategoriaPremio.MVP => comJogos.OrderByDescending(l => l.Media).Take(3)
                .Concat(comJogos.OrderByDescending(l => l.Gols + l.Assistencias).ThenByDescending(l => l.Media).Take(3))
                .DistinctBy(l => l.PlayerId).Take(quantos),
            CategoriaPremio.ROY => n.Jogadores.Values
                .Where(l => n.Estreantes.Contains(l.PlayerId) && l.Overall <= PremiosTemporada.OverallMaximoRoy && l.Jogos >= n.MinimoDeJogos / 2)
                .OrderByDescending(l => l.Media).Take(quantos),
            CategoriaPremio.MIP => n.Ganho
                .OrderByDescending(g => g.Value.Depois - g.Value.Antes)
                .ThenByDescending(g => n.Jogadores.GetValueOrDefault(g.Key)?.Media ?? 0)
                .Select(g => n.Jogadores.GetValueOrDefault(g.Key))
                .OfType<Linha>()
                .Take(quantos),
            CategoriaPremio.DPOY => comJogos
                .Where(l => (PositionType)l.PositionId is PositionType.Zagueiro or PositionType.LateralEsquerdo or PositionType.LateralDireito)
                .OrderByDescending(l => l.Media).ThenByDescending(l => l.SemSofrer).Take(quantos),
            CategoriaPremio.GKOY => comJogos
                .Where(l => (PositionType)l.PositionId == PositionType.Goleiro)
                .OrderByDescending(l => l.Media).ThenByDescending(l => l.SemSofrer).Take(quantos),
            _ => Enumerable.Empty<Linha>()
        };

        return escolhidos.Select(l => new PremioCandidato
        {
            CandidatoId = Guid.NewGuid(),
            Temporada = temporada,
            Categoria = categoria,
            PlayerId = l.PlayerId,
            TimeId = l.TimeId,
            Resumo = Resumo(categoria, l, n.Ganho.GetValueOrDefault(l.PlayerId)),
        }).ToList();
    }

    /// <summary>
    /// Temporada sem notas do PES: ataque pelos gols e assistências; defesa e gol pelos clubes que menos sofreram
    /// gol (os de maior overall da posição no elenco de hoje — confira antes de abrir).
    /// </summary>
    private async Task<IReadOnlyList<PremioCandidato>> SugerirSemNotasAsync(
        int temporada, CategoriaPremio categoria, int quantos, Numeros n, CancellationToken ct)
    {
        PremioCandidato Novo(int playerId, Guid? timeId, string resumo) => new()
        {
            CandidatoId = Guid.NewGuid(), Temporada = temporada, Categoria = categoria,
            PlayerId = playerId, TimeId = timeId, Resumo = resumo,
        };

        static string Ga(Linha l) => $"{l.Gols} G · {l.Assistencias} A";

        switch (categoria)
        {
            case CategoriaPremio.MVP:
                return n.Jogadores.Values.OrderByDescending(l => l.Gols + l.Assistencias).ThenByDescending(l => l.Gols)
                    .Take(quantos).Select(l => Novo(l.PlayerId, l.TimeId, Ga(l))).ToList();
            case CategoriaPremio.ROY:
                return n.Jogadores.Values
                    .Where(l => n.Estreantes.Contains(l.PlayerId) && l.Overall <= PremiosTemporada.OverallMaximoRoy)
                    .OrderByDescending(l => l.Gols + l.Assistencias)
                    .Take(quantos).Select(l => Novo(l.PlayerId, l.TimeId, Ga(l))).ToList();
            case CategoriaPremio.MIP:
                var ids = n.Ganho.Keys.ToList();
                var clubeAtual = await _db.Players.AsNoTracking().Where(p => ids.Contains(p.PlayerId))
                    .Select(p => new { p.PlayerId, p.CurrentTeamId }).ToDictionaryAsync(p => p.PlayerId, p => p.CurrentTeamId, ct);
                return n.Ganho.OrderByDescending(g => g.Value.Depois - g.Value.Antes).Take(quantos)
                    .Select(g => Novo(g.Key, clubeAtual.GetValueOrDefault(g.Key), $"overall {g.Value.Antes} → {g.Value.Depois}")).ToList();
        }

        var goleiro = categoria == CategoriaPremio.GKOY;
        var melhoresDefesas = n.Defesas.Where(d => d.Value.Jogos > 0)
            .OrderBy(d => (double)d.Value.Sofridos / d.Value.Jogos)
            .Take(goleiro ? quantos : 6)
            .ToList();
        var clubes = melhoresDefesas.Select(d => d.Key).ToList();

        var posicoes = goleiro
            ? new short[] { (short)PositionType.Goleiro }
            : new short[] { (short)PositionType.Zagueiro, (short)PositionType.LateralEsquerdo, (short)PositionType.LateralDireito };
        var doElenco = await _db.Players.AsNoTracking()
            .Where(p => p.CurrentTeamId != null && clubes.Contains(p.CurrentTeamId.Value) && posicoes.Contains(p.PositionId) && p.AposentadoNaTemporada == null)
            .Select(p => new { p.PlayerId, Time = p.CurrentTeamId!.Value, p.Overall })
            .ToListAsync(ct);

        return melhoresDefesas
            .SelectMany(d => doElenco.Where(p => p.Time == d.Key).OrderByDescending(p => p.Overall).Take(goleiro ? 1 : 2)
                .Select(p => Novo(p.PlayerId, d.Key,
                    $"defesa do clube: {d.Value.Sofridos} gols sofridos em {d.Value.Jogos} jogos")))
            .Take(quantos)
            .ToList();
    }

    /// <summary>Os treinadores dos clubes que melhor terminaram a temporada (Série A primeiro).</summary>
    private async Task<IReadOnlyList<PremioCandidato>> SugerirTreinadoresAsync(int temporada, int quantos, CancellationToken ct)
    {
        var tabela = await _db.LigaClassificacoes.AsNoTracking()
            .Where(c => c.Liga.Temporada == temporada && c.Liga.Tipo == TipoCompetition.Liga)
            .Select(c => new { c.TimeId, c.Posicao, c.Pontos, c.Jogos, c.Liga.Tipo, c.Liga.Divisao })
            .ToListAsync(ct);

        var treinadores = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null && p.Papel == PapelTreinador.Treinador && p.Treinador.Ativo)
            .Select(p => new { p.TimeId, p.TreinadorId })
            .ToListAsync(ct);
        var porClube = treinadores.GroupBy(t => t.TimeId).ToDictionary(g => g.Key, g => g.First().TreinadorId);

        return tabela
            .OrderBy(c => c.Divisao == Divisao.SerieB ? 1 : 0).ThenBy(c => c.Posicao)
            .Where(c => porClube.ContainsKey(c.TimeId))
            .Take(quantos)
            .Select(c => new PremioCandidato
            {
                CandidatoId = Guid.NewGuid(),
                Temporada = temporada,
                Categoria = CategoriaPremio.COTY,
                TreinadorId = porClube[c.TimeId],
                TimeId = c.TimeId,
                Resumo = $"{c.Posicao}º na {LigaLabels.Competicao(c.Tipo, c.Divisao)} · {c.Pontos} pts em {c.Jogos} jogos",
            })
            .ToList();
    }

    private static string Resumo(CategoriaPremio categoria, Linha l, (int Antes, int Depois) ganho)
    {
        if (l.Jogos == 0)
            return categoria == CategoriaPremio.MIP && ganho.Depois > ganho.Antes
                ? $"overall {ganho.Antes} → {ganho.Depois}"
                : $"{l.Gols} G · {l.Assistencias} A";

        var nota = $"nota {l.Media.ToString("0.0", Br)} · {l.Jogos} jogos";
        return categoria switch
        {
            CategoriaPremio.GKOY or CategoriaPremio.DPOY => $"{nota} · {l.SemSofrer} sem sofrer gol",
            CategoriaPremio.MIP when ganho.Depois > ganho.Antes => $"overall {ganho.Antes} → {ganho.Depois} · {nota}",
            _ => $"{nota} · {l.Gols} G · {l.Assistencias} A"
        };
    }
}
