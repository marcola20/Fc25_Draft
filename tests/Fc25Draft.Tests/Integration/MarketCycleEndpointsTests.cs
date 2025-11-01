using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Models.MarketCycles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class MarketCycleEndpointsTests : IClassFixture<MarketCycleEndpointsFactory>
{
    private readonly MarketCycleEndpointsFactory _factory;

    public MarketCycleEndpointsTests(MarketCycleEndpointsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenRequestIsValid()
    {
        var factory = CreateIsolatedFactory();
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketCycleCreateRequest
        {
            Name = "Ciclo Especial",
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(2),
            Status = MarketCycleStatus.Draft,
            Notes = "Observações gerais"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/market/cycles")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MarketCycleDto>();
        Assert.NotNull(dto);
        Assert.Equal(request.Name, dto!.Name);
        Assert.Equal(MarketCycleStatus.Draft, dto.Status);
        Assert.Equal(request.Notes, dto.Notes);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenOpenCycleAlreadyExists()
    {
        var factory = CreateIsolatedFactory();
        await SeedOpenCycleAsync(factory);
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketCycleCreateRequest
        {
            Name = "Novo Ciclo",
            StartsAtUtc = DateTime.UtcNow.AddHours(1),
            EndsAtUtc = DateTime.UtcNow.AddHours(5),
            Status = MarketCycleStatus.Active
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/market/cycles")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Equal("Já existe um ciclo ativo.", payload!["message"]);
    }

    [Fact]
    public async Task Create_ReturnsValidationProblem_WhenDatesAreInvalid()
    {
        var factory = CreateIsolatedFactory();
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketCycleCreateRequest
        {
            Name = "Ciclo Inverso",
            StartsAtUtc = DateTime.UtcNow.AddDays(2),
            EndsAtUtc = DateTime.UtcNow.AddDays(1),
            Status = MarketCycleStatus.Draft
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/market/cycles")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Falha na validação.", problem!.Title);
        Assert.Contains(nameof(request.StartsAtUtc), problem.Errors.Keys);
    }

    [Fact]
    public async Task Get_ReturnsPagedCycles()
    {
        var factory = CreateIsolatedFactory();
        await SeedMultipleCyclesAsync(factory);
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.GetAsync("/api/market/cycles?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MarketCycleDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);
        Assert.Equal(3, result.Total);
        Assert.Equal("Ciclo C", result.Items[0].Name);
        Assert.Equal("Ciclo B", result.Items[1].Name);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenCycleDoesNotExist()
    {
        var factory = CreateIsolatedFactory();
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.GetAsync($"/api/market/cycles/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Equal("Ciclo não encontrado.", payload!["message"]);
    }

    [Fact]
    public async Task Patch_ReturnsConflict_WhenCycleHasActiveItems()
    {
        var factory = CreateIsolatedFactory();
        var (cycleId, _, _) = await SeedCycleWithActiveItemAsync(factory);
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketCycleStatusUpdateRequest
        {
            Status = MarketCycleStatus.Closed,
            ForceClose = false
        };

        var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/market/cycles/{cycleId:D}/status")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Equal("Existem itens ativos neste ciclo. Utilize o fechamento forçado para continuar.", payload!["message"]);
    }

    [Fact]
    public async Task Patch_ClosesCycle_WhenForceCloseIsEnabled()
    {
        var factory = CreateIsolatedFactory();
        var (cycleId, itemId, teamId) = await SeedCycleWithActiveItemAsync(factory);
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketCycleStatusUpdateRequest
        {
            Status = MarketCycleStatus.Closed,
            ForceClose = true
        };

        var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/market/cycles/{cycleId:D}/status")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MarketCycleStatusUpdateResult>();
        Assert.NotNull(payload);
        Assert.Equal(MarketCycleStatus.Closed, payload!.Cycle.Status);
        Assert.NotNull(payload.SettlementSummary);
        Assert.Equal(1, payload.SettlementSummary!.Sold);
        Assert.Equal(0, payload.SettlementSummary!.Expired);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        var item = await db.MarketItems.AsNoTracking().FirstAsync(i => i.ItemId == itemId);
        Assert.Equal(MarketItemStatus.Sold, item.Status);
        Assert.Equal(teamId, item.WinnerTeamId);

        var team = await db.Teams.AsNoTracking().FirstAsync(t => t.TeamId == teamId);
        Assert.Equal(3000m, team.Budget);
        Assert.Equal(0m, team.BudgetBlocked);
    }

    [Fact]
    public async Task Patch_IgnoresDuplicateIdempotencyKey()
    {
        var factory = CreateIsolatedFactory();
        var cycleId = await SeedDraftCycleAsync(factory);
        var token = await SeedAdminTokenAsync(factory);
        var client = factory.CreateClient();

        var request = new MarketCycleStatusUpdateRequest
        {
            Status = MarketCycleStatus.Active,
            ForceClose = false
        };

        var idempotencyKey = Guid.NewGuid().ToString("N");

        var firstMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/market/cycles/{cycleId:D}/status")
        {
            Content = JsonContent.Create(request)
        };
        firstMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        firstMessage.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        var firstResponse = await client.SendAsync(firstMessage);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var dto = await firstResponse.Content.ReadFromJsonAsync<MarketCycleDto>();
        Assert.NotNull(dto);
        var firstUpdatedAt = dto!.UpdatedAtUtc;

        var secondMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/market/cycles/{cycleId:D}/status")
        {
            Content = JsonContent.Create(request)
        };
        secondMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        secondMessage.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        var secondResponse = await client.SendAsync(secondMessage);
        Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        var persisted = await db.MarketCycles.AsNoTracking().SingleAsync(c => c.CycleId == cycleId);
        Assert.Equal(MarketCycleStatus.Active, persisted.Status);
        Assert.Equal(firstUpdatedAt, persisted.UpdatedAtUtc);
    }

    private WebApplicationFactory<Program> CreateIsolatedFactory()
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DraftDbContext>>();
                services.AddDbContext<DraftDbContext>(options =>
                    options.UseInMemoryDatabase($"market-cycles-{Guid.NewGuid():N}"));
            });
        });

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

    private static async Task SeedOpenCycleAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = Guid.NewGuid(),
            Name = "Ciclo Atual",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddHours(5),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedDraftCycleAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();

        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Rascunho",
            Status = MarketCycleStatus.Draft,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddHours(5),
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddHours(-2)
        });

        await db.SaveChangesAsync();
        return cycleId;
    }

    private static async Task SeedMultipleCyclesAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        var baseTime = DateTime.UtcNow.AddDays(-1);

        db.MarketCycles.AddRange(
            new MarketCycle
            {
                CycleId = Guid.NewGuid(),
                Name = "Ciclo A",
                Status = MarketCycleStatus.Closed,
                StartsAtUtc = baseTime.AddHours(-8),
                EndsAtUtc = baseTime.AddHours(-2),
                CreatedAtUtc = baseTime.AddHours(-9),
                UpdatedAtUtc = baseTime.AddHours(-2)
            },
            new MarketCycle
            {
                CycleId = Guid.NewGuid(),
                Name = "Ciclo B",
                Status = MarketCycleStatus.Draft,
                StartsAtUtc = baseTime.AddHours(2),
                EndsAtUtc = baseTime.AddHours(6),
                CreatedAtUtc = baseTime.AddHours(1),
                UpdatedAtUtc = baseTime.AddHours(2)
            },
            new MarketCycle
            {
                CycleId = Guid.NewGuid(),
                Name = "Ciclo C",
                Status = MarketCycleStatus.Active,
                StartsAtUtc = baseTime.AddHours(4),
                EndsAtUtc = baseTime.AddHours(10),
                CreatedAtUtc = baseTime.AddHours(3),
                UpdatedAtUtc = baseTime.AddHours(4)
            });

        await db.SaveChangesAsync();
    }

    private static async Task<(Guid cycleId, Guid itemId, Guid teamId)> SeedCycleWithActiveItemAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Positions.AnyAsync(p => p.PositionId == 777))
        {
            db.Positions.Add(new Position { PositionId = 777, Name = "Teste" });
        }

        if (!await db.Players.AnyAsync(p => p.PlayerId == 9999))
        {
            db.Players.Add(new Player
            {
                PlayerId = 9999,
                Name = "Jogador de Teste",
                PositionId = 777,
                Overall = 85,
                PlayerGuid = Guid.NewGuid()
            });
        }

        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo com Itens",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-2),
            EndsAtUtc = now.AddHours(10),
            CreatedAtUtc = now.AddHours(-3),
            UpdatedAtUtc = now.AddHours(-1)
        });

        if (!await db.Teams.AnyAsync(t => t.TeamId == teamId))
        {
            db.Teams.Add(new Team
            {
                TeamId = teamId,
                TeamName = "Time Teste",
                Token = "TOKEN-TESTE",
                Budget = 5000m,
                BudgetBlocked = 2000m
            });
        }

        db.MarketItems.Add(new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = 9999,
            BasePrice = 1000m,
            BuyNowPrice = 1500m,
            MinIncrement = 100m,
            ExpiresAtUtc = now.AddHours(5),
            Status = MarketItemStatus.Active,
            PublishedAtUtc = now,
            CreatedAtUtc = now,
            LastUpdateUtc = now,
            CurrentLeaderTeamId = teamId,
            CurrentLeaderAmount = 2000m
        });

        await db.SaveChangesAsync();
        return (cycleId, itemId, teamId);
    }
}

public sealed class MarketCycleEndpointsFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DraftDbContext>>();
            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"market-cycles-{Guid.NewGuid():N}"));
        });
    }
}
