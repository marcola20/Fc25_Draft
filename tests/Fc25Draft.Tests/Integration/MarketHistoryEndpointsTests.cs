using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class MarketHistoryEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetHistory_ReturnsPagedData_WithDefaultPaging()
    {
        using var factory = new MarketHistoryFactory();
        var seed = await SeedHistoryAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<MarketTransactionDto>>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Total);
        Assert.Equal(1, payload.Page);
        Assert.Equal(50, payload.PageSize);
        Assert.Equal(3, payload.Items.Count);
        Assert.Equal(seed.OutbidId, payload.Items[0].TransactionId);
        Assert.Equal(seed.BidPlacedId, payload.Items[1].TransactionId);
        Assert.Equal(seed.BuyNowId, payload.Items[2].TransactionId);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenCycleIdIsInvalid()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?cycleId=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("Parâmetro \"cycleId\" deve ser um GUID válido.", error?.Message);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenTypeHasMultipleValues()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?type=1&type=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("Informe apenas um valor para o parâmetro \"type\".", error?.Message);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenTypeIsCommaSeparated()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?type=1,2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("O parâmetro \"type\" não aceita múltiplos valores.", error?.Message);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenFromDateIsInvalid()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?from=2024-99-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("Parâmetro \"from\" possui formato de data/hora inválido. Utilize o padrão ISO 8601 (ex.: 2024-01-31T12:00:00Z).", error?.Message);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenFromIsAfterTo()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?from=2024-01-02T00:00:00Z&to=2024-01-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("A data inicial deve ser menor ou igual à data final.", error?.Message);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenTypeIsUnknown()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?type=999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("Tipo de transação inválido.", error?.Message);
    }

    [Fact]
    public async Task GetHistoryByItem_UsesRouteParameter()
    {
        using var factory = new MarketHistoryFactory();
        var seed = await SeedHistoryAsync(factory);
        var client = factory.CreateClient();
        var bogusItem = Guid.NewGuid();

        var response = await client.GetAsync($"/api/market/items/{seed.ItemId}/history?itemId={bogusItem}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<MarketTransactionDto>>(JsonOptions);

        Assert.NotNull(payload);
        Assert.All(payload!.Items, i => Assert.Equal(seed.ItemId, i.ItemId));
        Assert.Equal(seed.OutbidId, payload.Items[0].TransactionId);
    }

    [Fact]
    public async Task GetHistoryByCycle_UsesRouteParameter()
    {
        using var factory = new MarketHistoryFactory();
        var seed = await SeedHistoryAsync(factory);
        var client = factory.CreateClient();
        var bogusCycle = Guid.NewGuid();

        var response = await client.GetAsync($"/api/market/cycles/{seed.CycleId}/history?cycleId={bogusCycle}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<MarketTransactionDto>>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Total);
        Assert.All(payload.Items, i => Assert.Equal(seed.CycleId, i.CycleId));
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenItemIdIsEmpty()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/market/items/00000000-0000-0000-0000-000000000000/history");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("Identificador do item é obrigatório.", error?.Message);
    }

    [Fact]
    public async Task GetHistory_ReturnsBadRequest_WhenCycleIdIsEmpty()
    {
        using var factory = new MarketHistoryFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/market/cycles/00000000-0000-0000-0000-000000000000/history");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("Identificador do ciclo é obrigatório.", error?.Message);
    }

    [Fact]
    public async Task GetHistory_SanitizesPagingValues()
    {
        using var factory = new MarketHistoryFactory();
        await SeedHistoryAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history?page=0&pageSize=0");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<MarketTransactionDto>>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Page);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(1, payload.Items.Count);
    }

    [Fact]
    public async Task ExportHistory_ReturnsCsvWithUtf8Bom()
    {
        using var factory = new MarketHistoryFactory();
        var seed = await SeedHistoryAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/market/history/export");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();

        Assert.StartsWith("\uFEFFDataHoraUtc;Evento;Jogador;TimeOrigem;TimeDestino;Valor;Observacoes", content);
        Assert.Contains(seed.PlayerName, content);
    }

    private static async Task<SeedResult> SeedHistoryAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();

        var cycleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var position = new Position { PositionId = 1, Name = "Atacante" };
        var player = new Player
        {
            PlayerId = 100,
            PlayerGuid = Guid.NewGuid(),
            Name = "João da Silva",
            Overall = 88,
            PositionId = position.PositionId,
            Position = position
        };

        var secondPlayer = new Player
        {
            PlayerId = 101,
            PlayerGuid = Guid.NewGuid(),
            Name = "Carlos Souza",
            Overall = 85,
            PositionId = position.PositionId,
            Position = position
        };

        var cycle = new MarketCycle
        {
            CycleId = cycleId,
            CreatedAtUtc = baseTime.AddHours(-6),
            NextCycleAtUtc = baseTime.AddHours(18),
            Status = MarketCycleStatus.Active
        };

        var item = new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = player.PlayerId,
            BasePrice = 1_000m,
            MinIncrement = 50m,
            ExpiresAtUtc = baseTime.AddDays(1),
            Status = MarketItemStatus.Published,
            CreatedAtUtc = baseTime.AddHours(-2),
            LastUpdateUtc = baseTime.AddHours(-1),
            Player = player,
            Cycle = cycle
        };

        var secondItem = new MarketItem
        {
            ItemId = secondItemId,
            CycleId = cycleId,
            PlayerId = secondPlayer.PlayerId,
            BasePrice = 900m,
            MinIncrement = 40m,
            ExpiresAtUtc = baseTime.AddDays(1),
            Status = MarketItemStatus.Published,
            CreatedAtUtc = baseTime.AddHours(-3),
            LastUpdateUtc = baseTime.AddHours(-2),
            Player = secondPlayer,
            Cycle = cycle
        };

        var team1 = new Team
        {
            TeamId = teamA,
            TeamName = "Time Azul",
            Token = "token-azul",
            Budget = 100_000m,
            BudgetBlocked = 0m
        };

        var team2 = new Team
        {
            TeamId = teamB,
            TeamName = "Time Vermelho",
            Token = "token-vermelho",
            Budget = 120_000m,
            BudgetBlocked = 0m
        };

        var bidPlaced = new MarketTransaction
        {
            TransactionId = Guid.NewGuid(),
            CycleId = cycleId,
            ItemId = itemId,
            PlayerId = player.PlayerId,
            TeamId = team1.TeamId,
            TargetTeamId = team2.TeamId,
            Type = MarketTransactionType.BidPlaced,
            Amount = 1_200m,
            PerformedBy = team1.TeamId.ToString(),
            Notes = "Lance inicial",
            CreatedAtUtc = baseTime.AddMinutes(1)
        };

        var outbid = new MarketTransaction
        {
            TransactionId = Guid.NewGuid(),
            CycleId = cycleId,
            ItemId = itemId,
            PlayerId = player.PlayerId,
            TeamId = team2.TeamId,
            TargetTeamId = team1.TeamId,
            Type = MarketTransactionType.Outbid,
            Amount = 1_350m,
            PerformedBy = team2.TeamId.ToString(),
            Notes = "Cobriu lance",
            CreatedAtUtc = baseTime.AddMinutes(5)
        };

        var buyNow = new MarketTransaction
        {
            TransactionId = Guid.NewGuid(),
            CycleId = cycleId,
            ItemId = secondItemId,
            PlayerId = secondPlayer.PlayerId,
            TeamId = team1.TeamId,
            TargetTeamId = team2.TeamId,
            Type = MarketTransactionType.BuyNow,
            Amount = 1_800m,
            PerformedBy = team1.TeamId.ToString(),
            Notes = "Compra imediata",
            CreatedAtUtc = baseTime
        };

        db.Positions.Add(position);
        db.Players.AddRange(player, secondPlayer);
        db.MarketCycles.Add(cycle);
        db.MarketItems.AddRange(item, secondItem);
        db.Teams.AddRange(team1, team2);
        db.MarketTransactions.AddRange(bidPlaced, outbid, buyNow);

        await db.SaveChangesAsync();

        return new SeedResult(
            cycleId,
            itemId,
            bidPlaced.TransactionId,
            outbid.TransactionId,
            buyNow.TransactionId,
            player.Name);
    }

    private sealed record MessageResponse(string? Message);

    private sealed record SeedResult(
        Guid CycleId,
        Guid ItemId,
        Guid BidPlacedId,
        Guid OutbidId,
        Guid BuyNowId,
        string PlayerName);
}

public sealed class MarketHistoryFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<DraftDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"market-history-{Guid.NewGuid():N}"));
        });
    }
}
