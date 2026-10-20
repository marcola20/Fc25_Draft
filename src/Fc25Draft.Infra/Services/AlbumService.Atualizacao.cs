using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Atualização do álbum: depois das janelas de mercado e das trocas de técnico, o admin deixa o álbum igual aos
/// elencos de hoje. Ninguém perde figurinha: quem mudou de clube muda de página com a mesma figurinha, e a de
/// quem saiu deixa as páginas mas fica com quem tem (para reciclar).
/// </summary>
public partial class AlbumService
{
    private sealed record Planejamento(
        AlbumFigurinhas.AtualizacaoDoAlbum Plano,
        Dictionary<Guid, string> Clubes,
        Dictionary<Guid, int> Donos,
        Dictionary<Guid, (int Numero, RaridadeFigurinha Raridade, string? Posicao)> Impressas,
        int TotalHoje,
        List<Guid> TrocasQueCaem);

    public async Task<AtualizacaoDoAlbumDto> PreviaDaAtualizacaoAsync(Guid albumId, CancellationToken ct) =>
        Descrever(await PlanejarAsync(albumId, ct));

    public async Task<AtualizacaoAplicadaDto> AtualizarAsync(Guid albumId, string? adminToken, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Trava o álbum: duas atualizações ao mesmo tempo dariam o mesmo número a figurinhas diferentes.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Albuns\" WHERE \"AlbumId\" = {albumId} FOR UPDATE", ct);

            var planejamento = await PlanejarAsync(albumId, ct);
            var plano = planejamento.Plano;
            if (plano.Total == 0)
                throw new InvalidOperationException("O álbum já está igual aos elencos de hoje.");

            var album = await _db.Albuns.FirstAsync(a => a.AlbumId == albumId, ct);
            var agora = Agora;
            var tocadas = plano.Mudam.Select(m => m.Figurinha.FigurinhaId)
                .Concat(plano.Voltam.Select(v => v.Figurinha.FigurinhaId))
                .Concat(plano.Saem.Select(s => s.FigurinhaId))
                .ToList();
            var porId = await _db.Figurinhas.Where(f => tocadas.Contains(f.FigurinhaId)).ToDictionaryAsync(f => f.FigurinhaId, ct);
            var proximaOrdem = (await _db.Figurinhas.Where(f => f.AlbumId == albumId)
                    .GroupBy(f => f.TeamId)
                    .Select(g => new { g.Key, Ordem = g.Max(f => f.Ordem) })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Key, x => x.Ordem);
            var numero = await _db.Figurinhas.Where(f => f.AlbumId == albumId).MaxAsync(f => f.Numero, ct);

            int Ordem(Guid teamId) => proximaOrdem[teamId] = proximaOrdem.GetValueOrDefault(teamId) + 1;

            // Muda de página (e volta, se tinha saído) com o mesmo número: quem tem continua tendo. Vai para o fim
            // da página do clube novo, como figurinha de atualização.
            foreach (var (f, para, papel) in plano.Mudam.Concat(plano.Voltam))
            {
                var figurinha = porId[f.FigurinhaId];
                figurinha.TeamId = para;
                figurinha.Ordem = Ordem(para);
                figurinha.SaiuEm = null;
                figurinha.EntrouEm = agora;
                if (figurinha.Tipo == TipoFigurinha.Treinador && papel is PapelTreinador novoPapel)
                {
                    figurinha.Papel = novoPapel;
                    figurinha.PosicaoSigla = SiglaDoPapel(novoPapel);
                }
            }

            foreach (var f in plano.Saem)
                porId[f.FigurinhaId].SaiuEm = agora;

            // Quem chegou: figurinha nova no fim do álbum. Técnicos primeiro, como na página do lançamento.
            foreach (var (teamId, t) in plano.TecnicosEntram.OrderBy(t => planejamento.Clubes[t.TeamId], StringComparer.CurrentCulture).ThenBy(t => t.Tecnico.Papel))
            {
                _db.Figurinhas.Add(new Figurinha
                {
                    FigurinhaId = Guid.NewGuid(),
                    AlbumId = albumId,
                    Numero = ++numero,
                    Tipo = TipoFigurinha.Treinador,
                    Raridade = RaridadeFigurinha.Brilhante,
                    TeamId = teamId,
                    TreinadorId = t.TreinadorId,
                    Papel = t.Papel,
                    NomeImpresso = t.Nome,
                    PosicaoSigla = SiglaDoPapel(t.Papel),
                    Ordem = Ordem(teamId),
                    EntrouEm = agora
                });
            }

            foreach (var e in plano.JogadoresEntram
                         .OrderBy(e => planejamento.Clubes[e.Jogador.TeamId], StringComparer.CurrentCulture)
                         .ThenBy(e => e.Jogador.PositionId).ThenByDescending(e => e.Jogador.Overall))
            {
                var j = e.Jogador;
                _db.Figurinhas.Add(new Figurinha
                {
                    FigurinhaId = Guid.NewGuid(),
                    AlbumId = albumId,
                    Numero = ++numero,
                    Tipo = TipoFigurinha.Jogador,
                    Raridade = e.Raridade,
                    TeamId = j.TeamId,
                    PlayerId = j.PlayerId,
                    NomeImpresso = j.Nome,
                    PosicaoSigla = j.PositionId.ToPositionSigla(),
                    Overall = j.Overall,
                    Ordem = Ordem(j.TeamId),
                    EntrouEm = agora
                });
            }

            // Proposta pendente com figurinha que saiu não tem mais como ser aceita.
            var saiuPorId = plano.Saem.ToDictionary(s => s.FigurinhaId);
            var trocas = await _db.TrocasFigurinhas.Include(t => t.Itens)
                .Where(t => planejamento.TrocasQueCaem.Contains(t.TrocaId))
                .ToListAsync(ct);
            foreach (var troca in trocas)
            {
                var saiu = troca.Itens.Select(i => saiuPorId.GetValueOrDefault(i.FigurinhaId)).First(s => s is not null)!;
                troca.Status = StatusTroca.NaoValeMais;
                troca.RespondidaEm = agora;
                troca.Motivo = $"{saiu.Nome} saiu do álbum na atualização.";
            }

            var descricao = Descrever(planejamento);
            await RegistrarAsync(AdminActionType.AtualizarAlbum, adminToken, new
            {
                album = album.Nome,
                entram = descricao.Entram.Count,
                mudam = descricao.Mudam.Count,
                voltam = descricao.Voltam.Count,
                saem = descricao.Saem.Count,
                trocasQueCairam = trocas.Count,
                detalhe = string.Join("; ", descricao.Entram.Select(m => $"+ {m.Nome} ({m.Para})")
                    .Concat(descricao.Mudam.Select(m => $"{m.Nome}: {m.De} → {m.Para}"))
                    .Concat(descricao.Voltam.Select(m => $"↩ {m.Nome} ({m.Para})"))
                    .Concat(descricao.Saem.Select(m => $"− {m.Nome} ({m.De})")))
            }, ct);
            await _db.SaveChangesAsync(ct);

            // Saiu a figurinha que faltava, ou a página ganhou alguém que a pessoa já tinha: quem ficou com a página
            // (ou o álbum) completa ganha o selo agora. Página que ganhou figurinha nova volta a ter vaga, e o selo
            // de quem já tinha fica, como sempre.
            var figurinhas = await FigurinhasAsync(albumId, ct);
            var clubes = figurinhas.Select(f => f.TeamId).ToHashSet();
            var colecionadores = await _db.FigurinhasDosTreinadores.AsNoTracking()
                .Where(f => f.Figurinha.AlbumId == albumId)
                .Select(f => f.TreinadorId)
                .Distinct()
                .ToListAsync(ct);
            var selos = 0;
            foreach (var pessoa in colecionadores)
                selos += (await GravarConquistasAsync(pessoa, albumId, figurinhas, clubes, agora, ct)).Count;

            await tx.CommitAsync(ct);

            if (selos > 0) ConquistasGravadas?.Invoke();
            if (trocas.Count > 0) TrocasMudaram?.Invoke();
            return new AtualizacaoAplicadaDto(descricao, selos);
        });
    }

    private static string SiglaDoPapel(PapelTreinador papel) => papel == PapelTreinador.Auxiliar ? "AUX" : "TÉC";

    /// <summary>Compara o álbum com os elencos e técnicos de hoje dos clubes que têm página nele.</summary>
    private async Task<Planejamento> PlanejarAsync(Guid albumId, CancellationToken ct)
    {
        var lidas = await _db.Figurinhas.AsNoTracking()
            .Where(f => f.AlbumId == albumId)
            .Select(f => new
            {
                f.FigurinhaId, f.Tipo, f.TeamId, TimeNome = f.Time.TeamName, f.PlayerId, f.TreinadorId, f.NomeImpresso,
                f.Overall, f.Lenda, f.SaiuEm, f.Numero, f.Raridade, f.PosicaoSigla
            })
            .ToListAsync(ct);
        if (lidas.Count == 0)
            throw new InvalidOperationException("Álbum não encontrado.");

        // As páginas são os clubes que têm escudo no álbum.
        var clubes = lidas.Where(f => f.Tipo == TipoFigurinha.Escudo)
            .GroupBy(f => f.TeamId)
            .ToDictionary(g => g.Key, g => g.First().TimeNome);
        var daTemporada = clubes.Select(c => new ClubeDaTemporada(c.Key, c.Value, null)).ToList();

        var plano = AlbumFigurinhas.PlanejarAtualizacao(
            lidas.Select(f => new AlbumFigurinhas.FigurinhaExistente(
                f.FigurinhaId, f.Tipo, f.TeamId, f.PlayerId, f.TreinadorId, f.NomeImpresso, f.Overall, f.Lenda, f.SaiuEm is not null)).ToList(),
            clubes.Keys.ToHashSet(),
            await ElencosAsync(daTemporada, ct),
            await TecnicosAsync(daTemporada, ct));

        var donos = await _db.FigurinhasDosTreinadores.AsNoTracking()
            .Where(f => f.Figurinha.AlbumId == albumId)
            .GroupBy(f => f.FigurinhaId)
            .Select(g => new { g.Key, Donos = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Donos, ct);

        var saem = plano.Saem.Select(s => s.FigurinhaId).ToList();
        var agora = Agora;
        var trocasQueCaem = saem.Count == 0
            ? new List<Guid>()
            : await _db.TrocasFigurinhas.AsNoTracking()
                .Where(t => t.AlbumId == albumId && t.Status == StatusTroca.Pendente && t.ExpiraEm > agora
                            && t.Itens.Any(i => saem.Contains(i.FigurinhaId)))
                .Select(t => t.TrocaId)
                .ToListAsync(ct);

        return new Planejamento(
            plano, clubes, donos,
            lidas.ToDictionary(f => f.FigurinhaId, f => (f.Numero, f.Raridade, f.PosicaoSigla)),
            lidas.Count(f => f.SaiuEm is null),
            trocasQueCaem);
    }

    /// <summary>A prévia em linhas para a tela (as que entram já com o número que vão ganhar).</summary>
    private static AtualizacaoDoAlbumDto Descrever(Planejamento p)
    {
        var plano = p.Plano;
        string Clube(Guid id) => p.Clubes.GetValueOrDefault(id) ?? "?";

        MudancaDoAlbumDto Existente(AlbumFigurinhas.FigurinhaExistente f, string? de, string? para)
        {
            var impressa = p.Impressas[f.FigurinhaId];
            return new MudancaDoAlbumDto(f.Nome, f.Tipo, impressa.Posicao, f.Overall, impressa.Raridade, impressa.Numero,
                de, para, p.Donos.GetValueOrDefault(f.FigurinhaId));
        }

        // Os números novos seguem a ordem em que o aplicar cria: técnicos e depois jogadores, por clube.
        var numero = p.Impressas.Values.Max(i => i.Numero);
        var entram = plano.TecnicosEntram
            .OrderBy(t => Clube(t.TeamId), StringComparer.CurrentCulture).ThenBy(t => t.Tecnico.Papel)
            .Select(t => new MudancaDoAlbumDto(t.Tecnico.Nome, TipoFigurinha.Treinador, SiglaDoPapel(t.Tecnico.Papel), null,
                RaridadeFigurinha.Brilhante, ++numero, null, Clube(t.TeamId), 0))
            .Concat(plano.JogadoresEntram
                .OrderBy(e => Clube(e.Jogador.TeamId), StringComparer.CurrentCulture)
                .ThenBy(e => e.Jogador.PositionId).ThenByDescending(e => e.Jogador.Overall)
                .Select(e => new MudancaDoAlbumDto(e.Jogador.Nome, TipoFigurinha.Jogador, e.Jogador.PositionId.ToPositionSigla(),
                    e.Jogador.Overall, e.Raridade, ++numero, null, Clube(e.Jogador.TeamId), 0)))
            .ToList();

        var mudam = plano.Mudam.Select(m => Existente(m.Figurinha, Clube(m.Figurinha.TeamId), Clube(m.Para)))
            .OrderBy(m => m.Para, StringComparer.CurrentCulture).ThenBy(m => m.Nome, StringComparer.CurrentCulture).ToList();
        var voltam = plano.Voltam.Select(v => Existente(v.Figurinha, null, Clube(v.Para)))
            .OrderBy(m => m.Para, StringComparer.CurrentCulture).ThenBy(m => m.Nome, StringComparer.CurrentCulture).ToList();
        var saem = plano.Saem.Select(s => Existente(s, Clube(s.TeamId), null))
            .OrderBy(m => m.De, StringComparer.CurrentCulture).ThenBy(m => m.Numero).ToList();

        var totalDepois = p.TotalHoje + entram.Count + voltam.Count - saem.Count;
        return new AtualizacaoDoAlbumDto(entram, mudam, voltam, saem, p.TotalHoje, totalDepois, p.TrocasQueCaem.Count);
    }
}
