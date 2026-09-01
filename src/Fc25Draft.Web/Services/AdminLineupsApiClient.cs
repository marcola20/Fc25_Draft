using System;
using System.Collections.Generic;
using System.Net;
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

    // Retorna false quando o time alterou a escalação depois que a tela foi carregada
    // (409): nesse caso nada é marcado como visto, para não engolir mudanças não lidas.
    public async Task<bool> AcknowledgeAsync(Guid lineupId, DateTime? seenUpdatedAtUtc, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var url = $"api/admin/lineups/{lineupId}/acknowledge";
        if (seenUpdatedAtUtc.HasValue)
        {
            url += $"?seenUpdatedAtTicks={seenUpdatedAtUtc.Value.Ticks}";
        }

        using var response = await client.PostAsync(url, null, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
