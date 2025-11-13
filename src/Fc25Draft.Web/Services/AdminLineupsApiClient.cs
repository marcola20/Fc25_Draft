using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class AdminLineupsApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public AdminLineupsApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<IReadOnlyList<AdminLineupOverviewDto>> GetLineupsAsync(Guid? teamId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var url = "api/admin/lineups";
        if (teamId.HasValue && teamId.Value != Guid.Empty)
        {
            url += $"?teamId={teamId.Value}";
        }

        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<AdminLineupOverviewDto>>(cancellationToken: ct);
        return payload ?? Array.Empty<AdminLineupOverviewDto>();
    }
}
