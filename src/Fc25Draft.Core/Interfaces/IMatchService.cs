using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Core.Interfaces;

public interface IMatchService
{
    Task CaptureLineupsAsync(Guid matchId, CancellationToken ct);
}
