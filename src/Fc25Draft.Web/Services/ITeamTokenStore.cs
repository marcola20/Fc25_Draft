using System.Threading.Tasks;

namespace Fc25Draft.Web.Services;

public interface ITeamTokenStore
{
    Task<string?> GetAsync();

    Task SetAsync(string token);

    Task ClearAsync();

    Task<bool> IsConfiguredAsync();
}
