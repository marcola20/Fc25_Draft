using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class MarketItemPublicationEndpointsTests : IClassFixture<MarketItemPublicationFactory>
{
    private readonly MarketItemPublicationFactory _factory;

    public MarketItemPublicationEndpointsTests(MarketItemPublicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_Returns422_WhenValidationFails()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMarketItemPublicationService>();
                services.AddScoped<IMarketItemPublicationService, ValidationFailureService>();
            });
        });

        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketItemDraftCreateRequest(
            Guid.NewGuid(),
            1,
            10_000m,
            12_000m,
            500m,
            DateTime.UtcNow.AddHours(1));

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/market/items")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://httpstatuses.com/422", problem!.Type);
        Assert.Contains("basePrice", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_Returns412_WhenPreconditionFails()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMarketItemPublicationService>();
                services.AddScoped<IMarketItemPublicationService, PreconditionFailureService>();
            });
        });

        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();
        var itemId = Guid.NewGuid();

        var request = new MarketItemDraftUpdateRequest(10_000m, 12_000m, 500m, DateTime.UtcNow.AddHours(2));
        var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/market/items/{itemId}")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        httpRequest.Headers.TryAddWithoutValidation("If-Match", "W/\"1\"");

        var response = await client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://httpstatuses.com/412", problem!.Type);
    }

    [Fact]
    public async Task Publish_Returns409_WhenConflictOccurs()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMarketItemPublicationService>();
                services.AddScoped<IMarketItemPublicationService, ConflictService>();
            });
        });

        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();
        var itemId = Guid.NewGuid();

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/market/items/{itemId}/publish");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        httpRequest.Headers.TryAddWithoutValidation("If-Match", "W/\"2\"");

        var response = await client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://httpstatuses.com/409", problem!.Type);
    }

    [Fact]
    public async Task CreateUpdatePublishFlow_Succeeds()
    {
        var factory = _factory;
        var token = await SeedAdminTokenAsync(factory);
        var (cycleId, playerId) = await SeedCycleAndPlayersAsync(factory, 10);
        var client = factory.CreateClient();

        var expiresAt = DateTime.UtcNow.AddHours(6);
        var createRequest = new MarketItemDraftCreateRequest(cycleId, playerId, 10_000m, 12_000m, 500m, expiresAt);
        var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/market/items")
        {
            Content = JsonContent.Create(createRequest)
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var createResponse = await client.SendAsync(createMessage);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<MarketItemPublicationDto>();
        Assert.NotNull(created);
        var ifMatch = createResponse.Headers.ETag?.ToString() ?? throw new InvalidOperationException("Missing ETag");

        var updateRequest = new MarketItemDraftUpdateRequest(11_000m, 13_500m, 600m, expiresAt.AddHours(1));
        var updateMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/market/items/{created!.ItemId}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        updateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        updateMessage.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        var updateResponse = await client.SendAsync(updateMessage);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<MarketItemPublicationDto>();
        Assert.NotNull(updated);
        var publishMatch = updateResponse.Headers.ETag?.ToString() ?? throw new InvalidOperationException("Missing update ETag");

        var publishMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/market/items/{created.ItemId}/publish");
        publishMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        publishMessage.Headers.TryAddWithoutValidation("If-Match", publishMatch);

        var publishResponse = await client.SendAsync(publishMessage);

        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var published = await publishResponse.Content.ReadFromJsonAsync<MarketItemPublicationDto>();
        Assert.NotNull(published);
        Assert.Equal("Active", published!.Status);
        Assert.NotNull(published.PublishedAtUtc);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        var entity = await db.MarketItems.AsNoTracking().FirstAsync(i => i.ItemId == created.ItemId);
        Assert.Equal(MarketItemStatus.Active, entity.Status);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPlayerAlreadyHasDraft()
    {
        var factory = _factory;
        var token = await SeedAdminTokenAsync(factory);
        var (cycleId, playerId) = await SeedCycleAndPlayersAsync(factory, 21);
        var client = factory.CreateClient();

        var expiresAt = DateTime.UtcNow.AddHours(3);
        var request = new MarketItemDraftCreateRequest(cycleId, playerId, 8_000m, 9_500m, 400m, expiresAt);
        var firstMessage = new HttpRequestMessage(HttpMethod.Post, "/api/market/items")
        {
            Content = JsonContent.Create(request)
        };
        firstMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var firstResponse = await client.SendAsync(firstMessage);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateMessage = new HttpRequestMessage(HttpMethod.Post, "/api/market/items")
        {
            Content = JsonContent.Create(request)
        };
        duplicateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var duplicateResponse = await client.SendAsync(duplicateMessage);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://httpstatuses.com/409", problem!.Type);
    }

    [Fact]
    public async Task List_ReturnsItemsOrderedForClientPagination()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.Zero));
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fakeTime);
            });
        });

        var token = await SeedAdminTokenAsync(factory);
        var (cycleId, playerId1, playerId2, playerId3) = await SeedCycleAndPlayersAsync(factory, 31, 32, 33);
        var client = factory.CreateClient();

        foreach (var (playerId, basePrice) in new[]
        {
            (playerId1, 7_000m),
            (playerId2, 7_500m),
            (playerId3, 8_000m)
        })
        {
            var expires = fakeTime.GetUtcNow().UtcDateTime.AddHours(5);
            var request = new MarketItemDraftCreateRequest(cycleId, playerId, basePrice, basePrice + 1_000m, 300m, expires);
            var message = new HttpRequestMessage(HttpMethod.Post, "/api/market/items")
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

            var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            fakeTime.Advance(TimeSpan.FromMinutes(10));
        }

        var listResponse = await client.GetAsync("/api/market/items/drafts?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var items = await listResponse.Content.ReadFromJsonAsync<List<MarketItemPublicationDto>>();
        Assert.NotNull(items);
        Assert.Equal(3, items!.Count);

        var firstPage = items.Take(2).Select(i => i.PlayerId).ToArray();
        Assert.Equal(new[] { playerId3, playerId2 }, firstPage);

        var secondPage = items.Skip(2).Take(2).Select(i => i.PlayerId).ToArray();
        Assert.Single(secondPage);
        Assert.Equal(playerId1, secondPage[0]);
    }

    private static async Task<Guid> SeedAdminTokenAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        var token = Guid.NewGuid();
        db.AdminTokens.Add(new AdminToken { Token = token });
        await db.SaveChangesAsync();

        return token;
    }

    private static async Task<(Guid cycleId, int playerId)> SeedCycleAndPlayersAsync(WebApplicationFactory<Program> factory, int playerId)
        => await SeedCycleAndPlayersAsync(factory, new[] { playerId });

    private static async Task<(Guid cycleId, int playerId1, int playerId2, int playerId3)> SeedCycleAndPlayersAsync(
        WebApplicationFactory<Program> factory,
        int playerId1,
        int playerId2,
        int playerId3)
    {
        var ids = await SeedCycleAndPlayersAsync(factory, new[] { playerId1, playerId2, playerId3 });
        return (ids.cycleId, ids.playerIds[0], ids.playerIds[1], ids.playerIds[2]);
    }

    private static async Task<(Guid cycleId, int[] playerIds)> SeedCycleAndPlayersAsync(
        WebApplicationFactory<Program> factory,
        int[] playerIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Positions.AnyAsync(p => p.PositionId == 99))
        {
            db.Positions.Add(new Position { PositionId = 99, Name = "Integração" });
        }

        var cycleId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var endsAt = now.AddDays(7);

        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = $"Ciclo Integração {now:yyyyMMddHHmm}",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now,
            EndsAtUtc = endsAt,
            Notes = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        foreach (var playerId in playerIds)
        {
            if (!await db.Players.AnyAsync(p => p.PlayerId == playerId))
            {
                db.Players.Add(new Player
                {
                    PlayerId = playerId,
                    Name = $"Jogador {playerId}",
                    Overall = 82,
                    PositionId = 99,
                    PlayerGuid = Guid.NewGuid()
                });
            }
        }

        await db.SaveChangesAsync();

        return (cycleId, playerIds);
    }

    private sealed class ValidationFailureService : IMarketItemPublicationService
    {
        public Task<MarketItemPublicationDto> CreateDraftAsync(MarketItemDraftCreateRequest request, CancellationToken ct)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["basePrice"] = new[] { "O valor base é obrigatório." }
            };

            throw new MarketItemValidationException("Falha de validação simulada.", errors);
        }

        public Task<MarketItemPublicationDto?> GetAsync(Guid itemId, CancellationToken ct) => Task.FromResult<MarketItemPublicationDto?>(null);

        public Task<IReadOnlyList<MarketItemPublicationDto>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MarketItemPublicationDto>>(Array.Empty<MarketItemPublicationDto>());

        public Task<MarketItemPublicationDto> PublishAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto> SoftDeleteAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto> UpdateDraftAsync(Guid itemId, MarketItemDraftUpdateRequest request, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed class PreconditionFailureService : IMarketItemPublicationService
    {
        public Task<MarketItemPublicationDto> CreateDraftAsync(MarketItemDraftCreateRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto?> GetAsync(Guid itemId, CancellationToken ct)
            => Task.FromResult<MarketItemPublicationDto?>(null);

        public Task<IReadOnlyList<MarketItemPublicationDto>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MarketItemPublicationDto>>(Array.Empty<MarketItemPublicationDto>());

        public Task<MarketItemPublicationDto> PublishAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto> SoftDeleteAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto> UpdateDraftAsync(Guid itemId, MarketItemDraftUpdateRequest request, uint expectedRowVersion, CancellationToken ct)
            => throw new MarketPreconditionFailedException("Versão inválida.");
    }

    private sealed class ConflictService : IMarketItemPublicationService
    {
        public Task<MarketItemPublicationDto> CreateDraftAsync(MarketItemDraftCreateRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto?> GetAsync(Guid itemId, CancellationToken ct)
            => Task.FromResult<MarketItemPublicationDto?>(null);

        public Task<IReadOnlyList<MarketItemPublicationDto>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MarketItemPublicationDto>>(Array.Empty<MarketItemPublicationDto>());

        public Task<MarketItemPublicationDto> PublishAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
            => throw new MarketConflictException("Conflito simulado.");

        public Task<MarketItemPublicationDto> SoftDeleteAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<MarketItemPublicationDto> UpdateDraftAsync(Guid itemId, MarketItemDraftUpdateRequest request, uint expectedRowVersion, CancellationToken ct)
            => throw new NotImplementedException();
    }
}

public sealed class MarketItemPublicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DraftDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"market-items-{Guid.NewGuid():N}"));
        });
    }
}
