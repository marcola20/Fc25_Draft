using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Busca do topo do site. A base é pequena (algumas centenas de jogadores), então a comparação é feita em
/// memória, sem acento nem maiúscula: "joao" acha "João". Quem começa com o termo vem antes de quem só contém.
/// </summary>
public class BuscaService : IBuscaService
{
    private const int MaxJogadores = 8;
    private const int MaxTimes = 5;

    private readonly DraftDbContext _db;

    public BuscaService(DraftDbContext db) => _db = db;

    public async Task<BuscaResultadoDto> BuscarAsync(string termo, CancellationToken ct)
    {
        var busca = TextoBusca.Normalizar(termo);
        if (busca.Length < 2) return BuscaResultadoDto.Vazio;

        var jogadores = await _db.Players.AsNoTracking()
            .Select(p => new BuscaJogadorDto(
                p.PlayerId, p.Name, p.Position.Name, p.Overall,
                p.CurrentTeam != null ? p.CurrentTeam.TeamName : null))
            .ToListAsync(ct);

        var times = await _db.Teams.AsNoTracking()
            .Where(t => !t.IsAdmin)
            .Select(t => new BuscaTimeDto(t.TeamId, t.TeamName, t.OwnerName))
            .ToListAsync(ct);

        return new BuscaResultadoDto(
            jogadores
                .Select(j => (j, nota: Nota(j.Nome, busca)))
                .Where(x => x.nota > 0)
                .OrderByDescending(x => x.nota)
                .ThenByDescending(x => x.j.Overall)
                .Take(MaxJogadores)
                .Select(x => x.j)
                .ToList(),
            times
                .Select(t => (t, nota: Math.Max(Nota(t.Nome, busca) * 2, Nota(t.Tecnico, busca))))
                .Where(x => x.nota > 0)
                .OrderByDescending(x => x.nota)
                .ThenBy(x => x.t.Nome, StringComparer.OrdinalIgnoreCase)
                .Take(MaxTimes)
                .Select(x => x.t)
                .ToList());
    }

    /// <summary>3 = começa com o termo; 2 = alguma palavra começa com ele; 1 = contém; 0 = não serve.</summary>
    private static int Nota(string? nome, string busca)
    {
        var texto = TextoBusca.Normalizar(nome);
        if (texto.Length == 0) return 0;
        if (texto.StartsWith(busca, StringComparison.Ordinal)) return 3;
        if (texto.Contains(' ' + busca, StringComparison.Ordinal)) return 2;
        return texto.Contains(busca, StringComparison.Ordinal) ? 1 : 0;
    }
}
