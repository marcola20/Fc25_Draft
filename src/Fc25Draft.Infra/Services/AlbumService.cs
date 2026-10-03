using System.Text.Json;
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
/// Álbum de figurinhas. O admin lança o álbum da temporada a partir dos elencos dos clubes da liga (retrato
/// do dia) e dá pacotes; cada pessoa abre os dela e o sorteio é feito aqui, na hora de abrir.
/// </summary>
public class AlbumService : IAlbumService
{
    private const int MaximoPacotesPorVez = 50;

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public AlbumService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => _time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<AlbumDto>> ListarAsync(CancellationToken ct) =>
        await _db.Albuns.AsNoTracking()
            .OrderByDescending(a => a.Temporada)
            .Select(a => new AlbumDto(a.AlbumId, a.Nome, a.Temporada, a.LancadoEm, a.Ativo, a.Figurinhas.Count))
            .ToListAsync(ct);

    public async Task<AlbumDto?> AtivoAsync(CancellationToken ct) =>
        await _db.Albuns.AsNoTracking()
            .Where(a => a.Ativo)
            .OrderByDescending(a => a.Temporada)
            .Select(a => new AlbumDto(a.AlbumId, a.Nome, a.Temporada, a.LancadoEm, a.Ativo, a.Figurinhas.Count))
            .FirstOrDefaultAsync(ct);

    // ---------------------------------------------------------------- Admin

    public async Task<AlbumPreviaDto> PreviaAsync(int temporada, CancellationToken ct)
    {
        var (clubes, origem) = await ClubesDaTemporadaAsync(temporada, ct);
        var jogadores = await ElencosAsync(clubes, ct);
        var candidatos = await CandidatosAsync(temporada, jogadores, clubes.ToDictionary(c => c.TeamId, c => c.Nome), ct);
        var sugeridas = SugerirLendarias(candidatos);

        var comFoto = await ComFotoAsync(jogadores.Select(j => j.PlayerId), ct);
        var semFotoPorClube = jogadores.Where(j => !comFoto.Contains(j.PlayerId)).ToLookup(j => j.TeamId);

        return new AlbumPreviaDto(
            temporada,
            NomePadrao(temporada),
            origem,
            clubes.Select(c => new AlbumPreviaClubeDto(c.TeamId, c.Nome, c.Divisao,
                jogadores.Count(j => j.TeamId == c.TeamId), semFotoPorClube[c.TeamId].Count())).ToList(),
            jogadores.Count(j => !comFoto.Contains(j.PlayerId)),
            sugeridas,
            candidatos);
    }

    public async Task<AlbumDto> LancarAsync(int temporada, string? nome, IReadOnlyList<LendariaEscolhaDto> lendarias, string? adminToken, CancellationToken ct)
    {
        if (await _db.Albuns.AnyAsync(a => a.Temporada == temporada, ct))
            throw new InvalidOperationException($"A temporada {temporada} já tem álbum.");

        var (clubes, _) = await ClubesDaTemporadaAsync(temporada, ct);
        if (clubes.Count == 0)
            throw new InvalidOperationException("Nenhum clube para montar o álbum.");

        var jogadores = await ElencosAsync(clubes, ct);
        var escolhidas = ValidarLendarias(lendarias, jogadores.Select(j => j.PlayerId).ToHashSet());

        var montadas = AlbumFigurinhas.Montar(
            clubes.Select(c => new AlbumFigurinhas.ClubeDoAlbum(c.TeamId, c.Nome)).ToList(),
            jogadores,
            escolhidas);

        // Só um álbum vale por vez; os antigos ficam como coleção.
        foreach (var antigo in await _db.Albuns.Where(a => a.Ativo).ToListAsync(ct))
            antigo.Ativo = false;

        var album = new Album
        {
            AlbumId = Guid.NewGuid(),
            Nome = string.IsNullOrWhiteSpace(nome) ? NomePadrao(temporada) : nome.Trim(),
            Temporada = temporada,
            LancadoEm = Agora,
            Ativo = true
        };
        foreach (var m in montadas)
        {
            album.Figurinhas.Add(new Figurinha
            {
                FigurinhaId = Guid.NewGuid(),
                Numero = m.Numero,
                Tipo = m.Tipo,
                Raridade = m.Raridade,
                TeamId = m.TeamId,
                PlayerId = m.PlayerId,
                NomeImpresso = m.NomeImpresso,
                PosicaoSigla = m.PosicaoSigla,
                Overall = m.Overall,
                Destaque = m.Destaque,
                Ordem = m.Ordem
            });
        }

        _db.Albuns.Add(album);
        await RegistrarAsync(AdminActionType.LancarAlbum, adminToken, new
        {
            album = album.Nome,
            clubes = clubes.Count,
            figurinhas = montadas.Count,
            lendarias = escolhidas.Count
        }, ct);
        await _db.SaveChangesAsync(ct);

        return new AlbumDto(album.AlbumId, album.Nome, album.Temporada, album.LancadoEm, album.Ativo, montadas.Count);
    }

    public async Task<AlbumAdminDto?> GetAdminAsync(Guid albumId, CancellationToken ct)
    {
        var album = await _db.Albuns.AsNoTracking()
            .Where(a => a.AlbumId == albumId)
            .Select(a => new AlbumDto(a.AlbumId, a.Nome, a.Temporada, a.LancadoEm, a.Ativo, a.Figurinhas.Count))
            .FirstOrDefaultAsync(ct);
        if (album is null) return null;

        var figurinhas = await FigurinhasAsync(albumId, ct);
        var jogadores = figurinhas.Where(f => f.PlayerId is not null).ToList();
        var comFoto = await ComFotoAsync(jogadores.Select(f => f.PlayerId!.Value), ct);

        var pacotes = await _db.PacotesGanhos.AsNoTracking()
            .Where(p => p.AlbumId == albumId)
            .GroupBy(_ => 1)
            .Select(g => new { Dados = g.Count(), Abertos = g.Count(p => p.AbertoEm != null) })
            .FirstOrDefaultAsync(ct);
        var colecionadores = await _db.FigurinhasDosTreinadores.AsNoTracking()
            .Where(f => f.Figurinha.AlbumId == albumId)
            .Select(f => f.TreinadorId)
            .Distinct()
            .CountAsync(ct);

        var notas = await NotasDaTemporadaAsync(album.Temporada - 1, jogadores.Select(f => f.PlayerId!.Value).ToList(), ct);
        var brilhantes = BrilhantesPorClube(jogadores.Select(f => (f.TeamId, f.PlayerId!.Value, f.Overall ?? 0, f.NomeImpresso)));
        var candidatos = jogadores
            .Select(f => Candidato(f.PlayerId!.Value, f.NomeImpresso, f.TeamId, f.TimeNome, f.PosicaoSigla ?? "?", f.Overall ?? 0, notas, brilhantes))
            .OrderBy(c => c.Nome, StringComparer.CurrentCulture)
            .ToList();

        return new AlbumAdminDto(
            album,
            figurinhas.Select(f => f.TeamId).Distinct().Count(),
            Contar(figurinhas.Select(f => f.Raridade)),
            jogadores.Count(f => !comFoto.Contains(f.PlayerId!.Value)),
            pacotes?.Dados ?? 0,
            pacotes?.Abertos ?? 0,
            colecionadores,
            figurinhas.Where(f => f.Raridade == RaridadeFigurinha.Lendaria).ToList(),
            candidatos);
    }

    public async Task SalvarLendariasAsync(Guid albumId, IReadOnlyList<LendariaEscolhaDto> lendarias, string? adminToken, CancellationToken ct)
    {
        var album = await _db.Albuns.FirstOrDefaultAsync(a => a.AlbumId == albumId, ct)
            ?? throw new InvalidOperationException("Álbum não encontrado.");
        if (await _db.PacotesGanhos.AnyAsync(p => p.AlbumId == albumId && p.AbertoEm != null, ct))
            throw new InvalidOperationException("Já abriram pacote deste álbum: as lendárias estão travadas.");

        var figurinhas = await _db.Figurinhas
            .Where(f => f.AlbumId == albumId && f.Tipo == TipoFigurinha.Jogador)
            .ToListAsync(ct);
        var escolhidas = ValidarLendarias(lendarias, figurinhas.Select(f => f.PlayerId!.Value).ToHashSet());

        // A raridade de quem deixa de ser lendária volta a ser a de antes: brilhante se estiver entre os
        // maiores overalls do clube no álbum.
        foreach (var clube in figurinhas.GroupBy(f => f.TeamId))
        {
            var brilhantes = AlbumFigurinhas.MaioresOveralls(clube.Select(f => (f.PlayerId!.Value, f.Overall ?? 0, f.NomeImpresso)));
            foreach (var f in clube)
            {
                var lendaria = escolhidas.TryGetValue(f.PlayerId!.Value, out var destaque);
                f.Raridade = lendaria ? RaridadeFigurinha.Lendaria
                    : brilhantes.Contains(f.PlayerId!.Value) ? RaridadeFigurinha.Brilhante
                    : RaridadeFigurinha.Comum;
                f.Destaque = lendaria ? AlbumFigurinhas.Limpar(destaque) : null;
            }
        }

        await RegistrarAsync(AdminActionType.AjustarLendarias, adminToken, new
        {
            album = album.Nome,
            lendarias = string.Join(", ", figurinhas.Where(f => f.Raridade == RaridadeFigurinha.Lendaria).Select(f => f.NomeImpresso))
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FigurinhaSemFotoDto>> SemFotoAsync(Guid albumId, CancellationToken ct) =>
        await _db.Figurinhas.AsNoTracking()
            .Where(f => f.AlbumId == albumId && f.PlayerId != null
                        && !_db.FotosJogadores.Any(foto => foto.PlayerId == f.PlayerId))
            .OrderBy(f => f.Numero)
            .Select(f => new FigurinhaSemFotoDto(f.PlayerId!.Value, f.Numero, f.NomeImpresso, f.TeamId, f.Time.TeamName,
                f.PosicaoSigla, f.Overall, f.Raridade))
            .ToListAsync(ct);

    public async Task<int> DarPacotesAsync(Guid treinadorId, int quantidade, string? motivo, string? adminToken, CancellationToken ct)
    {
        if (quantidade < 1 || quantidade > MaximoPacotesPorVez)
            throw new InvalidOperationException($"Dê de 1 a {MaximoPacotesPorVez} pacotes por vez.");

        var treinador = await _db.Treinadores.AsNoTracking()
            .Where(t => t.TreinadorId == treinadorId)
            .Select(t => t.Nome)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Pessoa não encontrada.");

        var albumId = await _db.Albuns.Where(a => a.Ativo).Select(a => (Guid?)a.AlbumId).FirstOrDefaultAsync(ct);
        var texto = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (texto?.Length > 200) texto = texto[..200];

        var agora = Agora;
        for (var i = 0; i < quantidade; i++)
        {
            var id = Guid.NewGuid();
            _db.PacotesGanhos.Add(new PacoteGanho
            {
                PacoteId = id,
                TreinadorId = treinadorId,
                AlbumId = albumId,
                Origem = PacoteGanho.OrigemAdmin,
                Chave = $"{PacoteGanho.OrigemAdmin}:{id:N}",
                Motivo = texto,
                CriadoEm = agora
            });
        }

        await RegistrarAsync(AdminActionType.DarPacotes, adminToken, new { treinador, quantidade, motivo = texto }, ct);
        await _db.SaveChangesAsync(ct);
        return quantidade;
    }

    public async Task<IReadOnlyList<PacotesDoTreinadorDto>> PacotesPorTreinadorAsync(CancellationToken ct)
    {
        var albumId = await _db.Albuns.Where(a => a.Ativo).Select(a => (Guid?)a.AlbumId).FirstOrDefaultAsync(ct);

        var lista = await _db.Treinadores.AsNoTracking()
            .Select(t => new
            {
                t.TreinadorId,
                t.Nome,
                t.Ativo,
                TimeAtual = t.Passagens.Where(p => p.Ate == null).Select(p => p.Time.TeamName).FirstOrDefault(),
                ParaAbrir = _db.PacotesGanhos.Count(p => p.TreinadorId == t.TreinadorId && p.AbertoEm == null),
                Abertos = _db.PacotesGanhos.Count(p => p.TreinadorId == t.TreinadorId && p.AbertoEm != null),
                Coladas = _db.FigurinhasDosTreinadores.Count(f => f.TreinadorId == t.TreinadorId && f.Figurinha.AlbumId == albumId)
            })
            .ToListAsync(ct);

        return lista
            .OrderByDescending(t => t.Ativo)
            .ThenBy(t => t.Nome, StringComparer.CurrentCulture)
            .Select(t => new PacotesDoTreinadorDto(t.TreinadorId, t.Nome, t.TimeAtual, t.Ativo, t.ParaAbrir, t.Abertos, t.Coladas))
            .ToList();
    }

    // ---------------------------------------------------------------- A pessoa

    public async Task<MeuAlbumDto?> MeuAlbumAsync(Guid treinadorId, Guid? albumId, CancellationToken ct)
    {
        var album = await _db.Albuns.AsNoTracking()
            .Where(a => albumId == null ? a.Ativo : a.AlbumId == albumId)
            .OrderByDescending(a => a.Temporada)
            .Select(a => new AlbumDto(a.AlbumId, a.Nome, a.Temporada, a.LancadoEm, a.Ativo, a.Figurinhas.Count))
            .FirstOrDefaultAsync(ct);
        if (album is null) return null;

        var figurinhas = await FigurinhasAsync(album.AlbumId, ct);
        var minhas = await _db.FigurinhasDosTreinadores.AsNoTracking()
            .Where(f => f.TreinadorId == treinadorId && f.Figurinha.AlbumId == album.AlbumId)
            .ToDictionaryAsync(f => f.FigurinhaId, ct);

        var paginas = figurinhas
            .GroupBy(f => f.TeamId)
            .OrderBy(g => g.Min(f => f.Numero))
            .Select(g => new PaginaDoAlbumDto(g.Key, g.First().TimeNome, g
                .OrderBy(f => f.Numero)
                .Select(f => minhas.TryGetValue(f.FigurinhaId, out var m)
                    ? new VagaDoAlbumDto(f, m.Quantidade, m.Nova)
                    : new VagaDoAlbumDto(f, 0, false))
                .ToList()))
            .ToList();

        var paraAbrir = await _db.PacotesGanhos.AsNoTracking()
            .CountAsync(p => p.TreinadorId == treinadorId && p.AbertoEm == null
                             && (p.AlbumId == album.AlbumId || (p.AlbumId == null && album.Ativo)), ct);

        return new MeuAlbumDto(
            album,
            paginas,
            Contar(figurinhas.Select(f => f.Raridade)),
            Contar(figurinhas.Where(f => minhas.ContainsKey(f.FigurinhaId)).Select(f => f.Raridade)),
            paraAbrir,
            minhas.Values.Sum(m => m.Quantidade - 1));
    }

    public async Task<PacoteAbertoDto> AbrirPacoteAsync(Guid treinadorId, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Um pacote por vez por pessoa: dois cliques ao mesmo tempo não abrem o mesmo pacote nem
            // perdem a contagem das repetidas.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Treinadores\" WHERE \"TreinadorId\" = {treinadorId} FOR UPDATE", ct);

            var pacote = await _db.PacotesGanhos
                .Where(p => p.TreinadorId == treinadorId && p.AbertoEm == null)
                .OrderBy(p => p.CriadoEm).ThenBy(p => p.PacoteId)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Você não tem pacote para abrir.");

            var albumId = pacote.AlbumId
                ?? await _db.Albuns.Where(a => a.Ativo).Select(a => (Guid?)a.AlbumId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("O álbum ainda não foi lançado. Guarde o pacote para quando sair.");

            var figurinhas = await FigurinhasAsync(albumId, ct);
            if (figurinhas.Count == 0)
                throw new InvalidOperationException("O álbum está vazio.");

            var porRaridade = figurinhas
                .GroupBy(f => f.Raridade)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(f => f.FigurinhaId).ToList());
            var sorteadas = AlbumFigurinhas.SortearPacote(porRaridade, Random.Shared);

            var agora = Agora;
            var ids = sorteadas.Distinct().ToList();
            var minhas = await _db.FigurinhasDosTreinadores
                .Where(f => f.TreinadorId == treinadorId && ids.Contains(f.FigurinhaId))
                .ToDictionaryAsync(f => f.FigurinhaId, ct);

            var porId = figurinhas.ToDictionary(f => f.FigurinhaId);
            var tiradas = new List<FigurinhaTiradaDto>();
            foreach (var id in sorteadas)
            {
                bool nova;
                if (minhas.TryGetValue(id, out var minha))
                {
                    minha.Quantidade++;
                    nova = false;
                }
                else
                {
                    minha = new FigurinhaDoTreinador
                    {
                        TreinadorId = treinadorId,
                        FigurinhaId = id,
                        Quantidade = 1,
                        PrimeiraEm = agora,
                        Nova = true
                    };
                    _db.FigurinhasDosTreinadores.Add(minha);
                    minhas[id] = minha;
                    nova = true;
                }

                tiradas.Add(new FigurinhaTiradaDto(porId[id], nova, minha.Quantidade));
            }

            pacote.AbertoEm = agora;
            pacote.AlbumId = albumId;
            pacote.Figurinhas = sorteadas.ToArray();
            await _db.SaveChangesAsync(ct);

            var restantes = await _db.PacotesGanhos.CountAsync(p => p.TreinadorId == treinadorId && p.AbertoEm == null, ct);
            await tx.CommitAsync(ct);

            return new PacoteAbertoDto(pacote.PacoteId, pacote.Origem, pacote.Motivo, tiradas, restantes);
        });
    }

    public async Task MarcarVistasAsync(Guid treinadorId, IReadOnlyCollection<Guid> figurinhas, CancellationToken ct)
    {
        if (figurinhas.Count == 0) return;
        var ids = figurinhas.ToList();
        await _db.FigurinhasDosTreinadores
            .Where(f => f.TreinadorId == treinadorId && f.Nova && ids.Contains(f.FigurinhaId))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Nova, false), ct);
    }

    // ---------------------------------------------------------------- Apoio

    private sealed record ClubeDaTemporada(Guid TeamId, string Nome, string? Divisao);

    private static string NomePadrao(int temporada) => $"Álbum CBFV {temporada}";

    /// <summary>
    /// Os clubes da Série A e da Série B da temporada (A primeiro, depois B, cada uma em ordem alfabética).
    /// Sem elas, todos os clubes que não são da organização.
    /// </summary>
    private async Task<(List<ClubeDaTemporada> Clubes, string Origem)> ClubesDaTemporadaAsync(int temporada, CancellationToken ct)
    {
        var daLiga = await _db.LigaTimes.AsNoTracking()
            .Where(lt => lt.Liga.Temporada == temporada && lt.Liga.Divisao != null && !lt.Time.IsAdmin)
            .Select(lt => new { lt.TimeId, lt.Time.TeamName, Divisao = lt.Liga.Divisao!.Value })
            .ToListAsync(ct);

        if (daLiga.Count > 0)
        {
            var clubes = daLiga
                .GroupBy(x => x.TimeId)
                .Select(g => g.OrderBy(x => x.Divisao).First())
                .OrderBy(x => x.Divisao)
                .ThenBy(x => x.TeamName, StringComparer.CurrentCulture)
                .Select(x => new ClubeDaTemporada(x.TimeId, x.TeamName, x.Divisao == Divisao.SerieA ? "Série A" : "Série B"))
                .ToList();
            var divisoes = daLiga.Select(x => x.Divisao).Distinct().Count();
            return (clubes, divisoes > 1 ? $"Série A e Série B {temporada}" : $"Liga de {temporada}");
        }

        var todos = await _db.Teams.AsNoTracking()
            .Where(t => !t.IsAdmin)
            .Select(t => new { t.TeamId, t.TeamName })
            .ToListAsync(ct);
        return (todos
                .OrderBy(t => t.TeamName, StringComparer.CurrentCulture)
                .Select(t => new ClubeDaTemporada(t.TeamId, t.TeamName, null))
                .ToList(),
            $"Todos os clubes (a temporada {temporada} não tem Série A/B cadastrada)");
    }

    private async Task<List<AlbumFigurinhas.JogadorDoAlbum>> ElencosAsync(List<ClubeDaTemporada> clubes, CancellationToken ct)
    {
        var ids = clubes.Select(c => c.TeamId).ToList();
        var elencos = await _db.TeamRosters.AsNoTracking()
            .Where(r => ids.Contains(r.TeamId))
            .Select(r => new { r.TeamId, r.PlayerId, r.Player.Name, r.Player.PositionId, r.Player.Overall })
            .ToListAsync(ct);

        // Um jogador entra uma vez só, mesmo se aparecer em dois elencos.
        return elencos
            .GroupBy(e => e.PlayerId)
            .Select(g => g.First())
            .Select(e => new AlbumFigurinhas.JogadorDoAlbum(e.PlayerId, e.Name, e.PositionId, e.Overall, e.TeamId))
            .ToList();
    }

    private async Task<HashSet<int>> ComFotoAsync(IEnumerable<int> playerIds, CancellationToken ct)
    {
        var ids = playerIds.Distinct().ToList();
        return (await _db.FotosJogadores.AsNoTracking()
                .Where(f => ids.Contains(f.PlayerId))
                .Select(f => f.PlayerId)
                .ToListAsync(ct))
            .ToHashSet();
    }

    /// <summary>Média de nota do PES e jogos com nota de cada jogador em todas as competições da temporada.</summary>
    private async Task<Dictionary<int, (decimal Media, int Jogos)>> NotasDaTemporadaAsync(int temporada, List<int> playerIds, CancellationToken ct)
    {
        var notas = await _db.LigaNotasJogadores.AsNoTracking()
            .Where(n => n.Partida.Rodada.Liga.Temporada == temporada && playerIds.Contains(n.JogadorId))
            .GroupBy(n => n.JogadorId)
            .Select(g => new { JogadorId = g.Key, Media = g.Average(n => n.Nota), Jogos = g.Count() })
            .ToListAsync(ct);
        return notas.ToDictionary(n => n.JogadorId, n => (Math.Round(n.Media, 2), n.Jogos));
    }

    private async Task<List<CandidatoLendariaDto>> CandidatosAsync(
        int temporada, List<AlbumFigurinhas.JogadorDoAlbum> jogadores, Dictionary<Guid, string> clubes, CancellationToken ct)
    {
        var notas = await NotasDaTemporadaAsync(temporada - 1, jogadores.Select(j => j.PlayerId).ToList(), ct);
        var brilhantes = BrilhantesPorClube(jogadores.Select(j => (j.TeamId, j.PlayerId, j.Overall, j.Nome)));
        return jogadores
            .Select(j => Candidato(j.PlayerId, j.Nome, j.TeamId, clubes[j.TeamId], j.PositionId.ToPositionSigla(), j.Overall, notas, brilhantes))
            .OrderBy(c => c.Nome, StringComparer.CurrentCulture)
            .ToList();
    }

    private static CandidatoLendariaDto Candidato(
        int playerId, string nome, Guid teamId, string timeNome, string posicao, int overall,
        Dictionary<int, (decimal Media, int Jogos)> notas, HashSet<int> brilhantes)
    {
        var raridade = brilhantes.Contains(playerId) ? RaridadeFigurinha.Brilhante : RaridadeFigurinha.Comum;
        return notas.TryGetValue(playerId, out var n)
            ? new CandidatoLendariaDto(playerId, nome, teamId, timeNome, posicao, overall, n.Media, n.Jogos, raridade)
            : new CandidatoLendariaDto(playerId, nome, teamId, timeNome, posicao, overall, null, 0, raridade);
    }

    /// <summary>Quem vira brilhante por estar entre os maiores overalls do clube.</summary>
    private static HashSet<int> BrilhantesPorClube(IEnumerable<(Guid TeamId, int PlayerId, int Overall, string Nome)> jogadores) =>
        jogadores.GroupBy(j => j.TeamId)
            .SelectMany(g => AlbumFigurinhas.MaioresOveralls(g.Select(j => (j.PlayerId, j.Overall, j.Nome))))
            .ToHashSet();

    /// <summary>
    /// Sugestão das lendárias: maiores médias de nota da temporada passada entre quem jogou o mínimo; sem
    /// gente bastante, quem tem nota com menos jogos e, por fim, os maiores overalls. O admin troca à vontade.
    /// </summary>
    private static List<CandidatoLendariaDto> SugerirLendarias(List<CandidatoLendariaDto> candidatos)
    {
        var sugeridas = candidatos
            .Where(c => c.MediaNota is not null && c.Jogos >= AlbumFigurinhas.MinimoJogosParaSugestao)
            .OrderByDescending(c => c.MediaNota).ThenByDescending(c => c.Jogos)
            .Take(AlbumFigurinhas.MaximoLendarias)
            .ToList();

        var resto = candidatos.Except(sugeridas)
            .OrderByDescending(c => c.MediaNota is not null)
            .ThenByDescending(c => c.MediaNota ?? 0).ThenByDescending(c => c.Jogos)
            .ThenByDescending(c => c.Overall)
            .Take(AlbumFigurinhas.MaximoLendarias - sugeridas.Count);

        return sugeridas.Concat(resto).ToList();
    }

    private static Dictionary<int, string?> ValidarLendarias(IReadOnlyList<LendariaEscolhaDto> lendarias, HashSet<int> noAlbum)
    {
        var escolhidas = new Dictionary<int, string?>();
        foreach (var l in lendarias)
        {
            if (!noAlbum.Contains(l.PlayerId))
                throw new InvalidOperationException("Só dá para escolher como lendária quem está no álbum.");
            escolhidas[l.PlayerId] = l.Destaque;
        }

        if (escolhidas.Count > AlbumFigurinhas.MaximoLendarias)
            throw new InvalidOperationException($"Escolha no máximo {AlbumFigurinhas.MaximoLendarias} lendárias.");
        return escolhidas;
    }

    private async Task<List<FigurinhaDto>> FigurinhasAsync(Guid albumId, CancellationToken ct) =>
        await _db.Figurinhas.AsNoTracking()
            .Where(f => f.AlbumId == albumId)
            .OrderBy(f => f.Numero)
            .Select(f => new FigurinhaDto(f.FigurinhaId, f.Numero, f.Tipo, f.Raridade, f.TeamId, f.Time.TeamName,
                f.PlayerId, f.NomeImpresso, f.PosicaoSigla, f.Overall, f.Destaque, f.Ordem))
            .ToListAsync(ct);

    private static AlbumContagemDto Contar(IEnumerable<RaridadeFigurinha> raridades)
    {
        var lista = raridades.ToList();
        return new AlbumContagemDto(
            lista.Count(r => r == RaridadeFigurinha.Comum),
            lista.Count(r => r == RaridadeFigurinha.Brilhante),
            lista.Count(r => r == RaridadeFigurinha.Lendaria));
    }

    private static readonly JsonSerializerOptions JsonLog = new(JsonSerializerDefaults.Web);

    /// <summary>Anota a ação no log do admin; quem fez é o id do token de admin, como no resto do log.</summary>
    private async Task RegistrarAsync(AdminActionType tipo, string? adminToken, object payload, CancellationToken ct)
    {
        var limpo = adminToken?.Trim();
        var adminId = string.IsNullOrEmpty(limpo)
            ? null
            : await _db.AdminTokens.AsNoTracking()
                .Where(t => t.Token == limpo)
                .Select(t => (Guid?)t.AdminTokenId)
                .FirstOrDefaultAsync(ct);

        _db.AdminActionsLogs.Add(new AdminActionsLog
        {
            ActionId = Guid.NewGuid(),
            ActionType = tipo,
            PerformedBy = adminId?.ToString() ?? "admin",
            PayloadJson = JsonSerializer.Serialize(payload, JsonLog),
            CreatedAtUtc = Agora
        });
    }
}
