using Bunit;
using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Components.MarketCycles;
using Fc25Draft.Web.Services;
using Microsoft.JSInterop;

namespace Fc25Draft.Tests.Components;

public class MarketItemGenerationTabTests
{
    [Fact]
    public void ShowsInfoMessageWhenCycleIsNull()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MarketItemGenerationTab>();

        var alert = cut.Find(".alert-info");
        Assert.Contains("Selecione um ciclo", alert.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WarnsWhenCycleIsNotDraft()
    {
        using var ctx = CreateContext();
        var now = DateTime.UtcNow;
        var cycle = new MarketCycleDto(Guid.NewGuid(), "Ciclo", MarketCycleStatus.Active, now, now.AddHours(2), now, now, null);

        var cut = ctx.RenderComponent<MarketItemGenerationTab>(parameters => parameters
            .Add(p => p.Cycle, cycle));

        var warning = cut.Find(".alert-warning");
        Assert.Contains("Apenas ciclos em rascunho", warning.TextContent, StringComparison.OrdinalIgnoreCase);

        var buttons = cut.FindAll("button");
        Assert.All(buttons.Where(b => b.TextContent.Contains("Gerar", StringComparison.OrdinalIgnoreCase)
                                   || b.TextContent.Contains("Pré-visualizar", StringComparison.OrdinalIgnoreCase)
                                   || b.TextContent.Contains("Limpar", StringComparison.OrdinalIgnoreCase)),
            button => Assert.True(button.HasAttribute("disabled")));
    }

    [Fact]
    public void EnablesPreviewWhenCycleIsDraft()
    {
        using var ctx = CreateContext();
        var now = DateTime.UtcNow;
        var cycle = new MarketCycleDto(Guid.NewGuid(), "Ciclo", MarketCycleStatus.Draft, now, now.AddHours(2), now, now, null);

        var cut = ctx.RenderComponent<MarketItemGenerationTab>(parameters => parameters
            .Add(p => p.Cycle, cycle));

        var previewButton = cut.Find("button[type=submit]");
        Assert.False(previewButton.HasAttribute("disabled"));
    }

    private static TestContext CreateContext()
    {
        var ctx = new TestContext();
        ctx.Services.AddSingleton<IPositionService>(new FakePositionService());
        ctx.Services.AddSingleton<IMarketItemGenerationClient>(new StubGenerationClient());
        ctx.Services.AddSingleton(new ToastService());
        ctx.Services.AddSingleton<IJSRuntime>(ctx.JSInterop.JSRuntime);
        return ctx;
    }

    private sealed class FakePositionService : IPositionService
    {
        public Task<IReadOnlyList<Position>> GetAllAsync()
        {
            IReadOnlyList<Position> positions = new List<Position>
            {
                new() { PositionId = 1, Name = "GOL" },
                new() { PositionId = 2, Name = "ZAG" }
            };
            return Task.FromResult(positions);
        }
    }

    private sealed class StubGenerationClient : IMarketItemGenerationClient
    {
        public Task<MarketItemGenerationPreviewDto> PreviewAsync(Guid cycleId, MarketItemGenerationRequestDto request, CancellationToken ct)
            => Task.FromResult(new MarketItemGenerationPreviewDto(
                request.DesiredCount,
                0,
                request.Seed ?? 0,
                Array.Empty<MarketItemGenerationItemDto>(),
                Array.Empty<MarketItemGenerationSkipDto>(),
                null,
                null));

        public Task<MarketItemGenerationResultDto> GenerateAsync(Guid cycleId, MarketItemGenerationRequestDto request, CancellationToken ct)
            => Task.FromResult(new MarketItemGenerationResultDto(
                request.DesiredCount,
                0,
                request.Seed ?? 0,
                0,
                Array.Empty<MarketItemGenerationItemDto>(),
                Array.Empty<MarketItemGenerationSkipDto>(),
                null,
                null));

        public Task<MarketItemGenerationDeleteResultDto> DeleteDraftsAsync(Guid cycleId, CancellationToken ct)
            => Task.FromResult(new MarketItemGenerationDeleteResultDto(0));
    }
}
