using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Clássicos entre treinadores rivais (regras em <see cref="Classicos"/>).</summary>
public class ClassicoService : IClassicoService
{
    private readonly DraftDbContext _db;
    private readonly ITreinadorService _treinadores;
    private readonly IPerfilTreinadorService _perfis;
    private readonly TimeProvider _time;

    public ClassicoService(DraftDbContext db, ITreinadorService treinadores, IPerfilTreinadorService perfis, TimeProvider? time = null)
    {
        _db = db;
        _treinadores = treinadores;
        _perfis = perfis;
        _time = time ?? TimeProvider.System;
    }

    private DateTime AgoraUtc => _time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<RivalidadeDto>> ListarAsync(CancellationToken ct)
    {
        var rivais = await RivaisAsync(ct);
        if (rivais.Count == 0) return Array.Empty<RivalidadeDto>();

        var ids = rivais.SelectMany(r => new[] { r.A, r.B }).Distinct().ToList();
        var perfis = await _perfis.VariosAsync(ids, ct);
        var nomes = await _db.Treinadores.AsNoTracking().Where(t => ids.Contains(t.TreinadorId))
            .ToDictionaryAsync(t => t.TreinadorId, t => t.Nome, ct);
        PerfilTreinadorDto Perfil(Guid id) => perfis.GetValueOrDefault(id) ?? PerfilTreinadorDto.SoNome(id, nomes.GetValueOrDefault(id) ?? "?");

        // Clube que cada um comanda hoje (técnico principal).
        var clubes = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null && p.Papel == PapelTreinador.Treinador && ids.Contains(p.TreinadorId))
            .Select(p => new { p.TreinadorId, p.Time.TeamName })
            .ToListAsync(ct);
        string? Clube(Guid id) => clubes.FirstOrDefault(c => c.TreinadorId == id)?.TeamName;

        var premios = await _db.ClassicoPremios.AsNoTracking()
            .Where(p => p.TreinadorId != null && p.RivalidadeId != null)
            .GroupBy(p => new { p.RivalidadeId, p.TreinadorId })
            .Select(g => new { g.Key.RivalidadeId, g.Key.TreinadorId, Total = g.Sum(p => p.Valor) })
            .ToListAsync(ct);
        decimal Premios(Guid rivalidade, Guid treinador) =>
            premios.Where(p => p.RivalidadeId == rivalidade && p.TreinadorId == treinador).Sum(p => p.Total);

        var proximos = await ProximosAsync(rivais, ct);

        var lista = new List<RivalidadeDto>();
        foreach (var r in rivais)
        {
            var (retrospecto, jogos) = Classicos.Retrospecto(await _treinadores.GetConfrontosAsync(r.A, ct), r.B);
            lista.Add(new RivalidadeDto(
                r.RivalidadeId, r.Nome, Perfil(r.A), Perfil(r.B), Clube(r.A), Clube(r.B),
                retrospecto, jogos, proximos.GetValueOrDefault(r.RivalidadeId),
                Premios(r.RivalidadeId, r.A), Premios(r.RivalidadeId, r.B), r.CriadaEm));
        }

        // O próximo clássico primeiro; depois os mais jogados.
        return lista
            .OrderBy(r => r.Proximo is null)
            .ThenBy(r => r.Proximo?.Quando ?? DateTime.MaxValue)
            .ThenByDescending(r => r.Retrospecto.Jogos)
            .ThenBy(r => r.Titulo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>O próximo jogo agendado de cada rivalidade (com os técnicos de hoje), pela data da rodada/jogo.</summary>
    private async Task<Dictionary<Guid, ProximoClassicoDto>> ProximosAsync(IReadOnlyList<Classicos.Rival> rivais, CancellationToken ct)
    {
        var agendados = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.Status == PartidaStatus.Agendada && p.Rodada.Liga.Status != LigaStatus.Encerrada)
            .Select(p => new
            {
                p.PartidaId, p.Rodada.LigaId, Competicao = p.Rodada.Liga.Nome, p.TimeCasaId, p.TimeForaId,
                Casa = p.TimeCasa.TeamName, Fora = p.TimeFora.TeamName,
                Quando = p.DataHora ?? p.Rodada.DataHora, p.Rodada.Numero
            })
            .ToListAsync(ct);
        if (agendados.Count == 0) return new();

        var classicos = Classicos.DosJogos(
            agendados.Select(a => new Classicos.Jogo(a.PartidaId, a.TimeCasaId, a.TimeForaId, AgoraUtc)),
            await PassagensAsync(ct), rivais);

        return agendados
            .Where(a => classicos.ContainsKey(a.PartidaId))
            .OrderBy(a => a.Quando ?? DateTime.MaxValue).ThenBy(a => a.Numero)
            .GroupBy(a => classicos[a.PartidaId].Rival.RivalidadeId)
            .ToDictionary(g => g.Key, g =>
            {
                var a = g.First();
                var c = classicos[a.PartidaId];
                return new ProximoClassicoDto(a.PartidaId, a.LigaId, a.Competicao, a.Casa, a.Fora, a.Quando,
                    c.TecnicoCasaId == c.Rival.A);
            });
    }

    public async Task<IReadOnlyDictionary<Guid, ClassicoDaPartidaDto>> DasPartidasAsync(
        IReadOnlyCollection<Guid> partidaIds, CancellationToken ct)
    {
        if (partidaIds.Count == 0) return new Dictionary<Guid, ClassicoDaPartidaDto>();
        var rivais = await RivaisAsync(ct);
        if (rivais.Count == 0) return new Dictionary<Guid, ClassicoDaPartidaDto>();

        var jogos = await _db.LigaPartidas.AsNoTracking()
            .Where(p => partidaIds.Contains(p.PartidaId))
            .Select(p => new { p.PartidaId, p.TimeCasaId, p.TimeForaId, Quando = p.IniciadaEm ?? p.EncerradaEm })
            .ToListAsync(ct);

        var agora = AgoraUtc;
        var classicos = Classicos.DosJogos(
            jogos.Select(j => new Classicos.Jogo(j.PartidaId, j.TimeCasaId, j.TimeForaId, j.Quando ?? agora)),
            await PassagensAsync(ct), rivais);

        var ids = rivais.SelectMany(r => new[] { r.A, r.B }).Distinct().ToList();
        var perfis = await _perfis.VariosAsync(ids, ct);
        var nomes = await _db.Treinadores.AsNoTracking().Where(t => ids.Contains(t.TreinadorId))
            .ToDictionaryAsync(t => t.TreinadorId, t => t.Nome, ct);
        string Curto(Guid id) => perfis.GetValueOrDefault(id)?.NomeCurto ?? nomes.GetValueOrDefault(id) ?? "?";

        return classicos.Values.ToDictionary(c => c.PartidaId, c => new ClassicoDaPartidaDto(
            c.PartidaId, c.Rival.RivalidadeId,
            string.IsNullOrWhiteSpace(c.Rival.Nome) ? $"{Curto(c.TecnicoCasaId)} x {Curto(c.TecnicoForaId)}" : c.Rival.Nome!));
    }

    public async Task SalvarAsync(Guid? rivalidadeId, Guid treinadorA, Guid treinadorB, string? nome, CancellationToken ct)
    {
        nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
        if (nome is { Length: > 80 }) throw new InvalidOperationException("O nome do clássico tem no máximo 80 letras.");

        if (rivalidadeId is Guid id)
        {
            var existente = await _db.Rivalidades.FirstOrDefaultAsync(r => r.RivalidadeId == id, ct)
                ?? throw new InvalidOperationException("Rivalidade não encontrada.");
            existente.Nome = nome;
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (treinadorA == treinadorB) throw new InvalidOperationException("Escolha dois treinadores diferentes.");
        var (a, b) = Classicos.Par(treinadorA, treinadorB);
        if (await _db.Treinadores.CountAsync(t => t.TreinadorId == a || t.TreinadorId == b, ct) != 2)
            throw new InvalidOperationException("Treinador não encontrado.");
        if (await _db.Rivalidades.AnyAsync(r => r.TreinadorAId == a && r.TreinadorBId == b, ct))
            throw new InvalidOperationException("Esses dois já são rivais.");

        _db.Rivalidades.Add(new Rivalidade
        {
            RivalidadeId = Guid.NewGuid(), TreinadorAId = a, TreinadorBId = b, Nome = nome, CriadaEm = AgoraUtc
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Guid rivalidadeId, CancellationToken ct)
    {
        var r = await _db.Rivalidades.FirstOrDefaultAsync(x => x.RivalidadeId == rivalidadeId, ct)
            ?? throw new InvalidOperationException("Rivalidade não encontrada.");
        _db.Rivalidades.Remove(r);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<decimal> PremioAsync(CancellationToken ct) =>
        (await _db.TransferConfigs.AsNoTracking().Select(c => (decimal?)c.PremioClassico).FirstOrDefaultAsync(ct))
        ?? Classicos.PremioPadrao;

    public async Task SalvarPremioAsync(decimal valor, CancellationToken ct)
    {
        if (valor < 0) throw new InvalidOperationException("O prêmio não pode ser negativo.");
        var cfg = await _db.TransferConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = TransferConfig.Default();
            cfg.AtualizadoEm = AgoraUtc;
            _db.TransferConfigs.Add(cfg);
        }
        cfg.PremioClassico = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PagarPremiosAsync(CancellationToken ct)
    {
        var rivais = await RivaisAsync(ct);
        if (rivais.Count == 0) return 0;
        var premio = await PremioAsync(ct);
        if (premio <= 0) return 0;

        // Só os jogos com vencedor, depois da rivalidade mais antiga e ainda sem prêmio. O empate nem entra.
        var desde = rivais.Min(r => r.CriadaEm);
        var jogos = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.Status == PartidaStatus.Encerrada && !p.IsWO && p.GolsCasa != p.GolsFora
                        && (p.IniciadaEm ?? p.EncerradaEm) != null && (p.IniciadaEm ?? p.EncerradaEm) > desde
                        && !_db.ClassicoPremios.Any(c => c.PartidaId == p.PartidaId))
            .Select(p => new
            {
                p.PartidaId, p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.IsWO,
                Quando = (p.IniciadaEm ?? p.EncerradaEm)!.Value,
                Competicao = p.Rodada.Liga.Nome, p.Rodada.LigaId,
                Casa = p.TimeCasa.TeamName, Fora = p.TimeFora.TeamName
            })
            .ToListAsync(ct);
        if (jogos.Count == 0) return 0;

        var classicos = Classicos.DosJogos(
            jogos.Select(j => new Classicos.Jogo(j.PartidaId, j.TimeCasaId, j.TimeForaId, j.Quando)),
            await PassagensAsync(ct), rivais);
        if (classicos.Count == 0) return 0;

        var ids = rivais.SelectMany(r => new[] { r.A, r.B }).Distinct().ToList();
        var perfis = await _perfis.VariosAsync(ids, ct);
        var nomes = await _db.Treinadores.AsNoTracking().Where(t => ids.Contains(t.TreinadorId))
            .ToDictionaryAsync(t => t.TreinadorId, t => t.Nome, ct);
        string Curto(Guid id) => perfis.GetValueOrDefault(id)?.NomeCurto ?? nomes.GetValueOrDefault(id) ?? "?";

        var agora = AgoraUtc;
        var pagos = 0;
        foreach (var j in jogos)
        {
            if (!classicos.TryGetValue(j.PartidaId, out var c) || j.Quando <= c.Rival.CriadaEm) continue;
            if (Classicos.Vencedor(c, j.GolsCasa, j.GolsFora, j.IsWO) is not Guid vencedor) continue;

            var casaVenceu = vencedor == c.TecnicoCasaId;
            var timeVencedorId = casaVenceu ? j.TimeCasaId : j.TimeForaId;
            var timePerdedorId = casaVenceu ? j.TimeForaId : j.TimeCasaId;
            var perdedor = casaVenceu ? c.TecnicoForaId : c.TecnicoCasaId;
            var time = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == timeVencedorId, ct);
            if (time is null) continue;

            var placar = $"{j.Casa} {j.GolsCasa} x {j.GolsFora} {j.Fora}";
            var titulo = string.IsNullOrWhiteSpace(c.Rival.Nome) ? "o clássico" : $"o {c.Rival.Nome}";

            _db.ClassicoPremios.Add(new ClassicoPremio
            {
                PremioId = Guid.NewGuid(), PartidaId = j.PartidaId, RivalidadeId = c.Rival.RivalidadeId,
                TreinadorId = vencedor, TimeId = time.TeamId, Valor = premio, PagoEm = agora
            });
            time.Budget = decimal.Round(time.Budget + premio, 2, MidpointRounding.AwayFromZero);
            ExtratoCaixa.Lancar(_db, time.TeamId, premio, ExtratoCaixa.Classico,
                $"Venceu {titulo} contra {Curto(perdedor)}: {placar} ({j.Competicao})", agora);
            AvisosDoTime.Criar(_db, time.TeamId, AvisosDoTime.Classico,
                $"🔥 {Curto(vencedor)} venceu {titulo} contra {Curto(perdedor)} ({placar}): "
                + $"{Classicos.Rotulo(premio)} para o caixa do {time.TeamName}.",
                "/classicos", agora);
            AvisosDoTime.Criar(_db, timePerdedorId, AvisosDoTime.Classico,
                $"😬 {Curto(perdedor)} perdeu {titulo} para {Curto(vencedor)} ({placar}).",
                "/classicos", agora);

            // Um por vez: se dois processos apurarem juntos, o índice único da partida barra o segundo sem perder os outros.
            try
            {
                await _db.SaveChangesAsync(ct);
                pagos++;
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
        }

        return pagos;
    }

    private async Task<List<Classicos.Rival>> RivaisAsync(CancellationToken ct) =>
        await _db.Rivalidades.AsNoTracking()
            .Select(r => new Classicos.Rival(r.RivalidadeId, r.TreinadorAId, r.TreinadorBId, r.Nome, r.CriadaEm))
            .ToListAsync(ct);

    private async Task<List<ConfrontoTreinadores.Passagem>> PassagensAsync(CancellationToken ct) =>
        await _db.TreinadorPassagens.AsNoTracking()
            .Select(p => new ConfrontoTreinadores.Passagem(p.TreinadorId,
                new ComandoDoJogo.Periodo(p.PassagemId, p.TimeId, p.Papel, p.Desde, p.Ate, p.SaiuEm)))
            .ToListAsync(ct);
}
