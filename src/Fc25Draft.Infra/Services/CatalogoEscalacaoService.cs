using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <inheritdoc />
public class CatalogoEscalacaoService : ICatalogoEscalacaoService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DraftDbContext _db;

    public CatalogoEscalacaoService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task CarregarAsync(CancellationToken ct)
    {
        var salvo = await _db.CatalogoEscalacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == CatalogoEscalacaoSalvo.IdUnico, ct);
        if (salvo is null) return;

        var catalogo = JsonSerializer.Deserialize<CatalogoEscalacao>(salvo.Json, Json)
            ?? throw new InvalidOperationException("O catálogo da escalação salvo está vazio.");
        LineupTemplateCatalog.Usar(catalogo);
    }

    public async Task<CatalogoEscalacaoAdminDto> ObterAsync(CancellationToken ct)
    {
        var atualizadoEm = await _db.CatalogoEscalacao.AsNoTracking()
            .Where(c => c.Id == CatalogoEscalacaoSalvo.IdUnico)
            .Select(c => (DateTime?)c.AtualizadoEm)
            .FirstOrDefaultAsync(ct);

        return new CatalogoEscalacaoAdminDto(LineupTemplateCatalog.Atual, await EscalacoesPorFormacaoAsync(ct), atualizadoEm);
    }

    public async Task SalvarAsync(CatalogoEscalacao catalogo, CancellationToken ct)
    {
        catalogo = LineupTemplateCatalog.Normalizar(catalogo);
        var erros = LineupTemplateCatalog.Validar(catalogo).ToList();

        // Formação que alguma escalação usa: a escalação guarda o nome dela e os códigos dos lugares.
        var emUso = await EscalacoesPorFormacaoAsync(ct);
        var atual = LineupTemplateCatalog.Atual.Formacoes.ToDictionary(f => f.Nome, StringComparer.OrdinalIgnoreCase);
        foreach (var (nome, escalacoes) in emUso)
        {
            var nova = catalogo.Formacoes.FirstOrDefault(f => string.Equals(f.Nome, nome, StringComparison.OrdinalIgnoreCase));
            if (nova is null)
            {
                erros.Add($"A formação {nome} é usada por {escalacoes} escalação(ões): não dá para excluir nem renomear, só desativar.");
                continue;
            }

            if (!atual.TryGetValue(nome, out var antes)) continue;
            var codigosAntes = antes.Lugares.Select(l => l.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!codigosAntes.SetEquals(nova.Lugares.Select(l => l.Codigo)))
                erros.Add($"A formação {nome} é usada por {escalacoes} escalação(ões): dá para mudar o nome e a posição dos lugares, "
                    + "mas não os códigos. Para outro desenho, duplique a formação.");
        }

        if (erros.Count > 0)
            throw new InvalidOperationException(string.Join("\n", erros.Distinct()));

        var salvo = await _db.CatalogoEscalacao.FirstOrDefaultAsync(c => c.Id == CatalogoEscalacaoSalvo.IdUnico, ct);
        if (salvo is null)
        {
            salvo = new CatalogoEscalacaoSalvo { Id = CatalogoEscalacaoSalvo.IdUnico };
            _db.CatalogoEscalacao.Add(salvo);
        }

        salvo.Json = JsonSerializer.Serialize(catalogo, Json);
        salvo.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        LineupTemplateCatalog.Usar(catalogo);
    }

    private async Task<Dictionary<string, int>> EscalacoesPorFormacaoAsync(CancellationToken ct)
    {
        var contagem = await _db.TeamLineups.AsNoTracking()
            .GroupBy(l => l.Formation)
            .Select(g => new { Formacao = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        var porFormacao = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in contagem)
            porFormacao[c.Formacao.Trim()] = porFormacao.GetValueOrDefault(c.Formacao.Trim()) + c.Total;
        return porFormacao;
    }
}
