namespace Fc25Draft.Core.Interfaces;

public interface IRoundSeedService
{
    Task EnsureDefaultSeasonAsync(CancellationToken ct);
}
