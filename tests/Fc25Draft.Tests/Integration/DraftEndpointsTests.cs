using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class DraftEndpointsTests : IClassFixture<DraftEndpointsFactory>
{
    private readonly DraftEndpointsFactory _factory;

    public DraftEndpointsTests(DraftEndpointsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_ReturnsSetupMode()
    {
        var factory = CreateIsolatedFactory();
        var draftId = await SeedDraftAsync(factory, DraftSetupMode.Manual);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/draft");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<DraftSummaryDto>>();
        Assert.NotNull(payload);
        var draft = Assert.Single(payload!);
        Assert.Equal(draftId, draft.DraftId);
        Assert.Equal(DraftSetupMode.Manual, draft.SetupMode);
    }

    [Fact]
    public async Task Details_ReturnsSetupMode()
    {
        var factory = CreateIsolatedFactory();
        var draftId = await SeedDraftAsync(factory, DraftSetupMode.Automatic);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/admin/draft/{draftId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DraftDetailsDto>();
        Assert.NotNull(payload);
        Assert.Equal(draftId, payload!.DraftId);
        Assert.Equal(DraftSetupMode.Automatic, payload.SetupMode);
    }

    private WebApplicationFactory<Program> CreateIsolatedFactory()
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DraftDbContext>>();
                services.AddDbContext<DraftDbContext>(options =>
                    options.UseInMemoryDatabase($"drafts-{Guid.NewGuid():N}"));
            });
        });

    private static async Task<Guid> SeedDraftAsync(WebApplicationFactory<Program> factory, DraftSetupMode setupMode)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        var draft = new Draft
        {
            DraftId = Guid.NewGuid(),
            Name = "Teste",
            TotalTeams = 4,
            TotalRounds = 2,
            CreatedAtUtc = DateTime.UtcNow,
            SetupMode = setupMode,
            Status = DraftStatus.Setup
        };

        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        return draft.DraftId;
    }
}

public sealed class DraftEndpointsFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DraftDbContext>>();
            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"drafts-{Guid.NewGuid():N}"));
        });
    }
}
