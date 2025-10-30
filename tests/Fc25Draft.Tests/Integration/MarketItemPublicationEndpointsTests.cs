using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
