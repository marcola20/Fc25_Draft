using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Competição encerrada com campeão entra sozinha no Hall da Fama. Roda antes de cada SaveChanges, então
/// vale para qualquer jeito de encerrar (liga com líder isolado, jogo decisivo, mini liga, final da Copa,
/// Supercopa). A entrada fica ligada à competição: se o campeão mudar (resultado corrigido) ela é
/// atualizada, e se a competição for reaberta ela sai. Descrição, ano e temporada editados à mão ficam.
/// </summary>
internal static class HallDaFamaAutomatico
{
    /// <summary>A CBFV de 2008 foi a 3ª temporada (as duas primeiras foram antes do sistema).</summary>
    public const int AnoAntesDaPrimeiraTemporada = 2005;

    public static string Descricao(TipoCompetition tipo) => tipo switch
    {
        TipoCompetition.Copa => "Copa do Brasil",
        TipoCompetition.Supercopa => "Supercopa do Brasil",
        _ => "Brasileirão"
    };

    public static async Task AtualizarAsync(DraftDbContext db, CancellationToken ct)
    {
        var mudaram = db.ChangeTracker.Entries<Liga>()
            .Where(e => e.State == EntityState.Added
                || (e.State == EntityState.Modified
                    && (e.Property(l => l.Status).IsModified || e.Property(l => l.CampeaoTimeId).IsModified)))
            .Select(e => e.Entity)
            .ToList();
        if (mudaram.Count == 0) return;

        var agora = DateTime.UtcNow;
        foreach (var liga in mudaram)
        {
            var entrada = db.HallOfFame.Local.FirstOrDefault(h => h.LigaId == liga.LigaId)
                ?? await db.HallOfFame.FirstOrDefaultAsync(h => h.LigaId == liga.LigaId, ct);

            if (liga.Status != LigaStatus.Encerrada || liga.CampeaoTimeId is not Guid campeaoId)
            {
                if (entrada is not null) db.HallOfFame.Remove(entrada);
                continue;
            }

            var time = await db.Teams.FindAsync([campeaoId], ct);
            if (time is null) continue;

            // Quem dirigia o campeão quando o título saiu.
            var tecnico = await db.TreinadorPassagens.AsNoTracking()
                .Where(p => p.TimeId == campeaoId && p.Papel == PapelTreinador.Treinador && p.Ate == null)
                .OrderByDescending(p => p.Desde)
                .Select(p => new { p.TreinadorId, p.Treinador.Nome })
                .FirstOrDefaultAsync(ct);

            if (entrada is null)
            {
                db.HallOfFame.Add(new HallOfFameEntry
                {
                    HallOfFameId = Guid.NewGuid(),
                    LigaId = liga.LigaId,
                    Descricao = Descricao(liga.Tipo),
                    Tipo = liga.Tipo,
                    Divisao = liga.Tipo == TipoCompetition.Liga ? liga.Divisao : null,
                    TimeCampeao = time.TeamName,
                    Tecnico = tecnico?.Nome,
                    TreinadorId = tecnico?.TreinadorId,
                    Ano = agora.AddHours(-3).Year, // ano do título no horário de Brasília
                    Temporada = liga.Temporada is int ano ? (ano - AnoAntesDaPrimeiraTemporada).ToString() : null,
                    CriadoEm = agora,
                    AtualizadoEm = agora
                });
            }
            else if (entrada.TimeCampeao != time.TeamName)
            {
                entrada.TimeCampeao = time.TeamName;
                entrada.Tecnico = tecnico?.Nome;
                entrada.TreinadorId = tecnico?.TreinadorId;
                entrada.AtualizadoEm = agora;
            }
        }
    }
}
