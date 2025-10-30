using System;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace Fc25Draft.Web.Services;

public class ApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigationManager;
    private readonly AdminAuthService _adminAuthService;

    public ApiClientFactory(
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager,
        AdminAuthService adminAuthService)
    {
        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
        _adminAuthService = adminAuthService;
    }

    public async Task<HttpClient> CreateAsync(bool includeAdminToken = false)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_navigationManager.BaseUri);

        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));

        if (includeAdminToken)
        {
            var token = await _adminAuthService.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return client;
    }
}
