using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface IPlayerService
{
    Task<(IReadOnlyList<Player> items, int total)> SearchAsync(string? q, short? positionId, int page, int pageSize);
    Task<Player?> GetAsync(int id);
    Task<int> CreateAsync(PlayerCreateDto dto);
    Task UpdateAsync(int id, PlayerUpdateDto dto);
    Task DeleteAsync(int id);
    Task<PlayerImportResultDto> ImportCsvAsync(Stream csvStream, CancellationToken ct = default);
}
