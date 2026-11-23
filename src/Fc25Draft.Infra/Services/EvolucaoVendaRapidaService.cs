using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class EvolucaoVendaRapidaService : IEvolucaoVendaRapidaService
{
    private readonly DraftDbContext _db;

    public EvolucaoVendaRapidaService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<TabelaEvolucaoVendaRapidaDto> ObterAsync(CancellationToken ct) => CarregarAsync(_db, ct);

    /// <summary>A tabela que vale agora; usada também pela venda rápida.</summary>
    internal static async Task<TabelaEvolucaoVendaRapidaDto> CarregarAsync(DraftDbContext db, CancellationToken ct)
    {
        var salvas = await db.EvolucoesVendaRapida
            .AsNoTracking()
            .OrderBy(l => l.Overall)
            .Select(l => new EvolucaoVendaRapidaDto(l.Overall, l.Minimo, l.Maximo))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return salvas.Count == 0
            ? new TabelaEvolucaoVendaRapidaDto(EvolucaoNaVendaRapida.Fabrica, true)
            : new TabelaEvolucaoVendaRapidaDto(salvas, false);
    }

    public async Task SalvarAsync(IReadOnlyList<EvolucaoVendaRapidaDto> linhas, CancellationToken ct)
    {
        if (linhas is null || linhas.Count == 0)
            throw new InvalidOperationException("A tabela está vazia.");
        if (EvolucaoNaVendaRapida.Validar(linhas) is { } erro)
            throw new InvalidOperationException(erro);

        // Num SaveChanges só: atualiza o que já existe, cria o que falta e apaga o que saiu da tabela.
        var salvas = await _db.EvolucoesVendaRapida.ToDictionaryAsync(l => l.Overall, ct).ConfigureAwait(false);
        foreach (var l in linhas)
        {
            if (salvas.Remove(l.Overall, out var linha))
            {
                linha.Minimo = l.Minimo;
                linha.Maximo = l.Maximo;
            }
            else
            {
                _db.EvolucoesVendaRapida.Add(new EvolucaoVendaRapida { Overall = l.Overall, Minimo = l.Minimo, Maximo = l.Maximo });
            }
        }
        _db.EvolucoesVendaRapida.RemoveRange(salvas.Values);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task VoltarParaFabricaAsync(CancellationToken ct)
    {
        await _db.EvolucoesVendaRapida.ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
