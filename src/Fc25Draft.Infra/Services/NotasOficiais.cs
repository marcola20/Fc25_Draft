using System.Linq.Expressions;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// A nota oficial do site. Partida com o registro individual do PES (<see cref="LigaEstatisticaJogador"/>) tem a
/// nota da liga (<see cref="NotaDaLiga"/>), gravada em <c>LigaNotasJogadores</c> com <c>DaLiga</c> = true no lugar da
/// nota do PES; assim tudo o que lê as notas (seleção da rodada, médias, ídolos, prêmios…) passa a usá-la. Partida
/// sem registro (as antigas) fica com a nota do PES. Melhor em campo = maior nota da liga do jogo.
/// </summary>
public static class NotasOficiais
{
    /// <summary>Uma linha do registro com a nota da liga calculada agora. <c>Nome</c>: do cadastro, ou o do PES.</summary>
    public sealed record NotaCalculada(LigaEstatisticaJogador Numeros, string Nome, ResultadoNotaDaLiga Nota);

    /// <summary>Calcula a nota da liga das linhas de registro do filtro, com o placar e os eventos de cada partida.</summary>
    public static async Task<List<NotaCalculada>> CalcularAsync(
        DraftDbContext db, Expression<Func<LigaEstatisticaJogador, bool>> filtro, CancellationToken ct)
    {
        var linhas = await db.LigaEstatisticasJogadores.AsNoTracking()
            .Where(filtro)
            .Select(x => new
            {
                Linha = x,
                Nome = x.Jogador != null ? x.Jogador.Name : x.NomePes,
                x.Partida.TimeCasaId,
                x.Partida.GolsCasa,
                x.Partida.GolsFora
            })
            .ToListAsync(ct);
        if (linhas.Count == 0) return [];

        var partidas = linhas.Select(l => l.Linha.PartidaId).Distinct().ToList();
        var eventos = (await db.LigaEventos.AsNoTracking()
                .Where(e => partidas.Contains(e.PartidaId))
                .Select(e => new { e.PartidaId, e.Tipo, e.TimeId, e.JogadorId, e.JogadorSaiuId, e.Minuto })
                .ToListAsync(ct))
            .GroupBy(e => e.PartidaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<EventoParaNota>)g
                .Select(e => new EventoParaNota(e.Tipo, e.TimeId, e.JogadorId, e.JogadorSaiuId, e.Minuto)).ToList());

        return linhas.Select(l =>
        {
            var casa = l.Linha.TimeId == l.TimeCasaId;
            var contexto = NotaDaLiga.ContextoDaPartida(l.Linha,
                casa ? l.GolsCasa : l.GolsFora,
                casa ? l.GolsFora : l.GolsCasa,
                eventos.GetValueOrDefault(l.Linha.PartidaId) ?? Array.Empty<EventoParaNota>());
            return new NotaCalculada(l.Linha, l.Nome, NotaDaLiga.Calcular(l.Linha, contexto));
        }).ToList();
    }

    /// <summary>
    /// O melhor em campo de cada partida: maior nota da liga; no empate, mais gols + assistências, depois quem vem
    /// antes na lista do PES. Só entre os ligados ao cadastro. Devolve o <c>Id</c> da linha do registro.
    /// </summary>
    public static HashSet<Guid> MelhoresEmCampo(IEnumerable<NotaCalculada> notas) =>
        notas.Where(n => n.Numeros.JogadorId is not null)
            .GroupBy(n => n.Numeros.PartidaId)
            .Select(g => g
                .OrderByDescending(n => n.Nota.Nota)
                .ThenByDescending(n => (n.Numeros.Gols ?? 0) + (n.Numeros.Assistencias ?? 0))
                .ThenBy(n => n.Numeros.Ordem)
                .First().Numeros.Id)
            .ToHashSet();

    /// <summary>
    /// Grava a nota oficial das partidas do filtro que têm registro: a nota da liga de cada jogador ligado ao
    /// cadastro substitui a do PES; quem só tinha nota do PES nessas partidas sai. Retorna quantas notas gravou.
    /// </summary>
    public static async Task<int> AtualizarAsync(
        DraftDbContext db, Expression<Func<LigaEstatisticaJogador, bool>> filtro, CancellationToken ct)
    {
        var calculadas = await CalcularAsync(db, filtro, ct);
        if (calculadas.Count == 0) return 0;

        var melhores = MelhoresEmCampo(calculadas);
        var partidas = calculadas.Select(c => c.Numeros.PartidaId).Distinct().ToList();
        var existentes = (await db.LigaNotasJogadores
                .Where(n => partidas.Contains(n.PartidaId))
                .ToListAsync(ct))
            .ToDictionary(n => (n.PartidaId, n.JogadorId));

        var gravadas = new HashSet<(Guid, int)>();
        foreach (var c in calculadas)
        {
            if (c.Numeros.JogadorId is not int jogadorId) continue;
            var chave = (c.Numeros.PartidaId, jogadorId);
            if (!gravadas.Add(chave)) continue;

            if (!existentes.TryGetValue(chave, out var nota))
            {
                nota = new LigaNotaJogador { PartidaId = c.Numeros.PartidaId, JogadorId = jogadorId };
                db.LigaNotasJogadores.Add(nota);
            }
            nota.TimeId = c.Numeros.TimeId;
            nota.Nota = c.Nota.Nota;
            nota.MelhorEmCampo = melhores.Contains(c.Numeros.Id);
            nota.DaLiga = true;
        }

        db.LigaNotasJogadores.RemoveRange(existentes.Where(e => !gravadas.Contains(e.Key)).Select(e => e.Value));
        await db.SaveChangesAsync(ct);
        return gravadas.Count;
    }
}
