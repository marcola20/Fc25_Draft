using System;
using System.Threading.Tasks;

namespace Fc25Draft.Web.Services;

public interface ITeamTokenStore
{
    Task<Guid?> GetAsync();

    Task SetAsync(Guid token);

    Task ClearAsync();

    Task<bool> IsConfiguredAsync();
}
