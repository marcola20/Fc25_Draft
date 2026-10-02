using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Prévia de um jogo que ainda não terminou: situação de cada time na competição (tabela, artilheiro, médias do
/// PES, suspensos e pendurados), a forma em todas as competições e o confronto direto. Junta o que já existe no
/// perfil do time e nas telas da liga.
/// </summary>
public class PreviaService : IPreviaService
{
    private readonly DraftDbContext _db;
    private readonly ILigaPublicService _ligas;
    private readonly IEstatisticasPesService _estatisticas;

    public PreviaService(DraftDbContext db, ILigaPublicService ligas, IEstatisticasPesService estatisticas)
    {
        _db = db;
        _ligas = ligas;
        _estatisticas = estatisticas;
    }

    public async Task<PreviaDto?> MontarAsync(Guid partidaId, CancellationToken ct)
    {
        var p = await _db.LigaPartidas.AsNoTracking()
            .Where(x => x.PartidaId == partidaId)
            .Select(x => new
            {
                x.Status, x.RodadaId, x.Rodada.LigaId, Competicao = x.Rodada.Liga.Nome,
                x.TimeCasaId, Casa = x.TimeCasa.TeamName, x.TimeForaId, Fora = x.TimeFora.TeamName
            })
            .FirstOrDefaultAsync(ct);
        if (p is null || p.Status == PartidaStatus.Encerrada) return null;

        var tabela = (await _ligas.GetClassificacaoAsync(p.LigaId, ct)).ToDictionary(c => c.TimeId);
        var artilheiros = await _ligas.GetArtilheirosAsync(p.LigaId, ct);
        var numeros = (await _estatisticas.DosTimesAsync(p.LigaId, ct)).ToDictionary(n => n.TimeId);
        var disciplina = await _ligas.GetDisciplinaAsync(p.LigaId, ct);
        var perfilCasa = await _ligas.GetPerfilTimeAsync(p.TimeCasaId, ct);
        var perfilFora = await _ligas.GetPerfilTimeAsync(p.TimeForaId, ct);

        PreviaTimeDto Lado(Guid timeId, string nome, TimePerfilDto perfil)
        {
            var linha = tabela.GetValueOrDefault(timeId);
            var artilheiro = artilheiros.Where(a => a.TimeId == timeId).OrderByDescending(a => a.Gols).FirstOrDefault();

            // Perfil traz do mais recente para o mais antigo; as bolinhas vão do mais antigo para o mais recente.
            var forma = perfil.Forma
                .Where(j => j.Resultado is not null)
                .Reverse()
                .Select(j => new ResultadoForma(j.Resultado![0], $"{j.Resultado} {j.GolsPro}x{j.GolsContra} {j.AdversarioNome} · {j.Competicao}"))
                .ToList();

            var suspensos = disciplina?.Suspensoes
                .Where(s => s.TimeId == timeId && !s.Cumprida && (s.PartidaCumprida == partidaId || s.RodadaCumprida == p.RodadaId))
                .Select(s => s.JogadorNome).Distinct().ToList() ?? new List<string>();
            var pendurados = disciplina?.Pendurados
                .Where(x => x.TimeId == timeId)
                .Select(x => x.JogadorNome).ToList() ?? new List<string>();

            return new PreviaTimeDto(
                timeId, nome, linha?.Posicao, linha?.Pontos, linha?.Jogos, linha?.GolsPro, linha?.GolsContra,
                forma, perfil.SequenciaAtual, perfil.SequenciaTipo,
                artilheiro?.JogadorNome, artilheiro?.Gols ?? 0,
                numeros.GetValueOrDefault(timeId), suspensos, pendurados);
        }

        // Confronto contado do lado do mandante desta partida.
        var confronto = perfilCasa.Confrontos.FirstOrDefault(c => c.AdversarioId == p.TimeForaId);
        var anteriores = (confronto?.Partidas ?? Array.Empty<TimePerfilJogoDto>())
            .Where(j => j.GolsPro is not null && j.GolsContra is not null)
            .Take(5)
            .Select(j => j.EmCasa
                ? new PreviaJogoAnteriorDto(j.Data, j.Competicao, p.Casa, j.GolsPro!.Value, j.GolsContra!.Value, p.Fora)
                : new PreviaJogoAnteriorDto(j.Data, j.Competicao, p.Fora, j.GolsContra!.Value, j.GolsPro!.Value, p.Casa))
            .ToList();

        return new PreviaDto(
            partidaId, p.Competicao,
            Lado(p.TimeCasaId, p.Casa, perfilCasa),
            Lado(p.TimeForaId, p.Fora, perfilFora),
            new PreviaConfrontoDto(
                confronto?.Jogos ?? 0, confronto?.Vitorias ?? 0, confronto?.Empates ?? 0, confronto?.Derrotas ?? 0,
                confronto?.GolsPro ?? 0, confronto?.GolsContra ?? 0, anteriores));
    }
}
