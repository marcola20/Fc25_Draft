using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Números das camisas: o técnico escolhe na aba Camisas; o Editor PES traz os que estão no jogo.</summary>
public class CamisasService : ICamisasService
{
    private readonly DraftDbContext _db;

    public CamisasService(DraftDbContext db)
    {
        _db = db;
    }

    public async Task<CamisasDoTimeDto> DoTimeAsync(Guid teamId, CancellationToken ct)
    {
        var jogadores = await _db.TeamRosters.AsNoTracking()
            .Where(r => r.TeamId == teamId)
            .Select(r => new CamisaJogadorDto(
                r.PlayerId,
                r.Player.Name,
                r.Player.PositionId,
                r.Player.Overall,
                r.NumeroCamisa,
                _db.Emprestimos
                    .Where(e => e.PlayerId == r.PlayerId && e.Status == EmprestimoStatus.Ativo)
                    .Select(e => e.DonoTeam.TeamName)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        var aposentadas = (await CamisasAposentadas.DosTimes(_db, [teamId]).ToListAsync(ct))
            .Select(c => new CamisaAposentadaDto(c.Numero, c.JogadorId, c.Nome))
            .ToList();

        return new CamisasDoTimeDto(teamId, jogadores
            .OrderBy(j => j.PositionId)
            .ThenByDescending(j => j.Overall)
            .ThenBy(j => j.Nome)
            .ToList(),
            aposentadas.OrderBy(a => a.Numero).ToList());
    }

    public async Task SalvarAsync(Guid teamId, IReadOnlyList<CamisaRequest> camisas, CancellationToken ct)
    {
        if (camisas.Count == 0) return;

        if (camisas.FirstOrDefault(c => c.Numero is < ICamisasService.Menor or > ICamisasService.Maior) is { } fora)
            throw new InvalidOperationException($"O número {fora.Numero} não vale: só de {ICamisasService.Menor} a {ICamisasService.Maior}.");
        if (camisas.GroupBy(c => c.PlayerId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("O mesmo jogador veio duas vezes.");

        var elenco = await _db.TeamRosters
            .Include(r => r.Player)
            .Where(r => r.TeamId == teamId)
            .ToListAsync(ct);
        var porId = elenco.ToDictionary(r => r.PlayerId);
        if (camisas.FirstOrDefault(c => !porId.ContainsKey(c.PlayerId)) is { } estranho)
            throw new InvalidOperationException($"O jogador {estranho.PlayerId} não é do time (mudou de clube?). Recarregue a página.");

        // Como fica o time inteiro depois da troca: ninguém pode repetir número.
        var depois = elenco.ToDictionary(r => r.PlayerId, r => r.NumeroCamisa);
        foreach (var c in camisas) depois[c.PlayerId] = c.Numero;
        var repetido = depois.Where(d => d.Value is not null).GroupBy(d => d.Value).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
        {
            var nomes = repetido.Select(d => porId[d.Key].Player.Name);
            throw new InvalidOperationException($"A camisa {repetido.Key} ficaria com {string.Join(" e ", nomes)}.");
        }

        var mudam = camisas.Where(c => porId[c.PlayerId].NumeroCamisa != c.Numero).ToList();
        if (mudam.Count == 0) return;

        // Camisa aposentada não volta a ser usada no clube.
        var aposentadas = await CamisasAposentadas.DosTimes(_db, [teamId]).ToListAsync(ct);
        if (mudam.FirstOrDefault(c => aposentadas.Any(a => a.Numero == c.Numero)) is { } bloqueada)
        {
            var lenda = aposentadas.First(a => a.Numero == bloqueada.Numero);
            throw new InvalidOperationException($"A camisa {bloqueada.Numero} está aposentada no clube em homenagem a {lenda.Nome}.");
        }

        // Trocas entre dois jogadores esbarram no índice único no meio do caminho: primeiro tira, depois põe.
        var ids = mudam.Select(c => c.PlayerId).ToList();
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var linhas = await _db.TeamRosters.Where(r => r.TeamId == teamId && ids.Contains(r.PlayerId)).ToListAsync(ct);
            foreach (var r in linhas) r.NumeroCamisa = null;
            await _db.SaveChangesAsync(ct);
            foreach (var r in linhas) r.NumeroCamisa = mudam.First(c => c.PlayerId == r.PlayerId).Numero;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task<CamisasDoPesResultadoDto> PreencherDoPesAsync(IReadOnlyList<CamisaRequest> itens, CancellationToken ct)
    {
        var validos = itens
            .Where(i => i.Numero is >= ICamisasService.Menor and <= ICamisasService.Maior)
            .GroupBy(i => i.PlayerId).Select(g => g.First())
            .ToList();
        var ids = validos.Select(i => i.PlayerId).ToList();
        var vinculos = await _db.TeamRosters
            .Include(r => r.Player)
            .Where(r => ids.Contains(r.PlayerId))
            .ToListAsync(ct);
        var porJogador = vinculos.ToDictionary(r => r.PlayerId);
        var times = vinculos.Select(r => r.TeamId).Distinct().ToList();
        var usados = (await _db.TeamRosters.AsNoTracking()
                .Where(r => times.Contains(r.TeamId) && r.NumeroCamisa != null)
                .Select(r => new { r.TeamId, Numero = r.NumeroCamisa!.Value })
                .ToListAsync(ct))
            .Select(x => (x.TeamId, x.Numero))
            .ToHashSet();
        var aposentadas = (await CamisasAposentadas.DosTimes(_db, times).ToListAsync(ct))
            .GroupBy(c => (c.TimeId, c.Numero))
            .ToDictionary(g => g.Key, g => string.Join(" e ", g.Select(c => c.Nome)));
        var nomesDosTimes = await _db.Teams.AsNoTracking()
            .Where(t => times.Contains(t.TeamId))
            .ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        int gravados = 0, jaTinham = 0, iguais = 0;
        var conflitos = new List<string>();
        foreach (var item in validos)
        {
            if (!porJogador.TryGetValue(item.PlayerId, out var r)) continue;   // sem clube no site
            var numero = item.Numero!.Value;
            if (r.NumeroCamisa == numero) { iguais++; continue; }
            if (r.NumeroCamisa is not null) { jaTinham++; continue; }
            if (aposentadas.TryGetValue((r.TeamId, numero), out var lenda))
            {
                conflitos.Add($"{r.Player.Name} ({nomesDosTimes[r.TeamId]}): a {numero} está aposentada em homenagem a {lenda}.");
                continue;
            }
            if (!usados.Add((r.TeamId, numero)))
            {
                conflitos.Add($"{r.Player.Name} ({nomesDosTimes[r.TeamId]}): a {numero} já é de outro no time.");
                continue;
            }
            r.NumeroCamisa = numero;
            gravados++;
        }

        await _db.SaveChangesAsync(ct);
        return new CamisasDoPesResultadoDto(gravados, jaTinham, iguais, conflitos);
    }
}
