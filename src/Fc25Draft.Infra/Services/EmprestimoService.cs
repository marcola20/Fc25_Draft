using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class EmprestimoService : IEmprestimoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _timeProvider;
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    public EmprestimoService(DraftDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<EmprestimoDto>> ListarAtivosAsync(Guid? teamId, CancellationToken ct)
    {
        var query = _db.Emprestimos.AsNoTracking().Where(e => e.Status == EmprestimoStatus.Ativo);
        if (teamId is Guid id)
            query = query.Where(e => e.DonoTeamId == id || e.TomadorTeamId == id);

        return await query
            .OrderBy(e => e.TomadorTeam.TeamName).ThenBy(e => e.Player.Name)
            .Select(e => new EmprestimoDto(
                e.EmprestimoId,
                e.PlayerId,
                e.Player.PlayerGuid,
                e.Player.Name,
                e.Player.Position.Name,
                e.Player.Overall,
                e.DonoTeamId,
                e.DonoTeam.TeamName,
                e.TomadorTeamId,
                e.TomadorTeam.TeamName,
                e.ValorOpcaoCompra,
                e.InicioUtc))
            .ToListAsync(ct);
    }

    public async Task<EmprestimoDto> ExercerOpcaoCompraAsync(Guid emprestimoId, string? teamToken, CancellationToken ct)
    {
        // Mesmo esquema das propostas: em Serializable, duas compras ao mesmo tempo não gastam o mesmo saldo.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var resultado = await ExercerOpcaoCompraCoreAsync(emprestimoId, teamToken, ct);
            await tx.CommitAsync(ct);
            return resultado;
        });
    }

    private async Task<EmprestimoDto> ExercerOpcaoCompraCoreAsync(Guid emprestimoId, string? teamToken, CancellationToken ct)
    {
        var teamId = await _db.TimeIdPorTokenAsync(teamToken, ct)
            ?? throw new UnauthorizedAccessException("Token do time inválido.");

        var emprestimo = await CarregarAtivoAsync(emprestimoId, ct);

        if (emprestimo.TomadorTeamId != teamId)
            throw new InvalidOperationException("Só quem pegou o jogador emprestado pode exercer a opção de compra.");

        if (emprestimo.ValorOpcaoCompra is not decimal preco)
            throw new InvalidOperationException("Este empréstimo não tem opção de compra.");

        var tomador = emprestimo.TomadorTeam;
        var dono = emprestimo.DonoTeam;

        // Comprar o emprestado é uma transferência: respeita a janela e o limite de quem compra.
        var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
        if (cfg.MercadoFechado)
            throw new InvalidOperationException("O mercado está fechado: a opção de compra só pode ser exercida com a janela aberta.");
        var limite = cfg.MaxTransfersFor(tomador);
        if (tomador.TransferCount >= limite)
            throw new InvalidOperationException($"O {tomador.TeamName} já atingiu o limite de transferências da janela ({tomador.TransferCount}/{limite}).");

        var disponivel = decimal.Round(tomador.Budget - tomador.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
        if (disponivel < preco)
            throw new InvalidOperationException($"Saldo insuficiente: a opção de compra custa {preco.ToString("C", BrCulture)}.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        tomador.Budget = decimal.Round(tomador.Budget - preco, 2, MidpointRounding.AwayFromZero);
        dono.Budget = decimal.Round(dono.Budget + preco, 2, MidpointRounding.AwayFromZero);
        tomador.TransferCount++;
        var descricao = $"Opção de compra de {emprestimo.Player.Name}: {tomador.TeamName} compra do {dono.TeamName}";
        await ClausulasDeRevenda.PagarAsync(_db, emprestimo.PlayerId, emprestimo.Player.Name, dono, preco, "opção de compra", now, ct);
        ExtratoCaixa.Lancar(_db, tomador.TeamId, -preco, ExtratoCaixa.OpcaoDeCompra, descricao, now);
        ExtratoCaixa.Lancar(_db, dono.TeamId, preco, ExtratoCaixa.OpcaoDeCompra, descricao, now);

        emprestimo.Status = EmprestimoStatus.Comprado;
        emprestimo.FimUtc = now;

        await _db.TransferHistories.AddAsync(new TransferHistory
        {
            TransferId = Guid.NewGuid(),
            Type = TransferType.LoanPurchase,
            PlayerId = emprestimo.PlayerId,
            FromTeamId = dono.TeamId,
            ToTeamId = tomador.TeamId,
            Amount = preco,
            Notes = $"{tomador.TeamName} exerce a opção de compra de {emprestimo.Player.Name} ({emprestimo.Player.Overall}) e paga {preco.ToString("C", BrCulture)} ao {dono.TeamName}",
            PerformedBy = "system",
            PerformedAtUtc = now,
            OldOverall = emprestimo.Player.Overall,
            NewOverall = emprestimo.Player.Overall
        }, ct);

        await _db.SaveChangesAsync(ct);

        return ToDto(emprestimo);
    }

    public async Task DevolverAsync(Guid emprestimoId, string performedBy, CancellationToken ct)
    {
        var emprestimo = await CarregarAtivoAsync(emprestimoId, ct);
        await DevolverAsync(emprestimo, performedBy, "Empréstimo encerrado pela organização", ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> DevolverTodosAsync(string performedBy, CancellationToken ct)
    {
        var ativos = await _db.Emprestimos
            .Include(e => e.Player)
            .Include(e => e.DonoTeam)
            .Include(e => e.TomadorTeam)
            .Where(e => e.Status == EmprestimoStatus.Ativo)
            .ToListAsync(ct);

        foreach (var emprestimo in ativos)
            await DevolverAsync(emprestimo, performedBy, "Fim da temporada", ct);

        await _db.SaveChangesAsync(ct);
        return ativos.Count;
    }

    /// <summary>Tira o jogador do tomador e põe de volta no dono. Não salva.</summary>
    private async Task DevolverAsync(Emprestimo emprestimo, string performedBy, string motivo, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var player = emprestimo.Player;

        var vinculos = await _db.TeamRosters.Where(r => r.PlayerId == emprestimo.PlayerId).ToListAsync(ct);
        _db.TeamRosters.RemoveRange(vinculos.Where(r => r.TeamId != emprestimo.DonoTeamId));
        if (vinculos.All(r => r.TeamId != emprestimo.DonoTeamId))
            await _db.TeamRosters.AddAsync(new TeamRoster { PlayerId = emprestimo.PlayerId, TeamId = emprestimo.DonoTeamId }, ct);

        player.CurrentTeamId = emprestimo.DonoTeamId;

        emprestimo.Status = EmprestimoStatus.Devolvido;
        emprestimo.FimUtc = now;

        await _db.TransferHistories.AddAsync(new TransferHistory
        {
            TransferId = Guid.NewGuid(),
            Type = TransferType.LoanReturn,
            PlayerId = emprestimo.PlayerId,
            FromTeamId = emprestimo.TomadorTeamId,
            ToTeamId = emprestimo.DonoTeamId,
            Amount = 0m,
            Notes = $"{player.Name} volta do {emprestimo.TomadorTeam.TeamName} para o {emprestimo.DonoTeam.TeamName}. {motivo}",
            PerformedBy = performedBy,
            PerformedAtUtc = now,
            OldOverall = player.Overall,
            NewOverall = player.Overall
        }, ct);
    }

    private async Task<Emprestimo> CarregarAtivoAsync(Guid emprestimoId, CancellationToken ct)
    {
        var emprestimo = await _db.Emprestimos
            .Include(e => e.Player).ThenInclude(p => p.Position)
            .Include(e => e.DonoTeam)
            .Include(e => e.TomadorTeam)
            .FirstOrDefaultAsync(e => e.EmprestimoId == emprestimoId, ct)
            ?? throw new KeyNotFoundException("Empréstimo não encontrado.");

        if (emprestimo.Status != EmprestimoStatus.Ativo)
            throw new InvalidOperationException("Este empréstimo já foi encerrado.");

        return emprestimo;
    }

    private static EmprestimoDto ToDto(Emprestimo e) => new(
        e.EmprestimoId,
        e.PlayerId,
        e.Player.PlayerGuid,
        e.Player.Name,
        e.Player.Position?.Name ?? "?",
        e.Player.Overall,
        e.DonoTeamId,
        e.DonoTeam.TeamName,
        e.TomadorTeamId,
        e.TomadorTeam.TeamName,
        e.ValorOpcaoCompra,
        e.InicioUtc);
}
