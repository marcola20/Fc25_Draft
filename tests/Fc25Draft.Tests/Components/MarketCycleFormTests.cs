using Bunit;
using Fc25Draft.Core.Entities;
using Fc25Draft.Web.Components.MarketCycles;
using Microsoft.AspNetCore.Components;

namespace Fc25Draft.Tests.Components;

public class MarketCycleFormTests
{
    [Fact]
    public void SubmitButtonStartsDisabledWhenModelIsInvalid()
    {
        using var ctx = new TestContext();
        var model = new MarketCycleFormModel();

        var cut = ctx.RenderComponent<MarketCycleForm>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.ShowCancelButton, false));

        var submit = cut.Find("button[type=submit]");
        Assert.True(submit.HasAttribute("disabled"));
    }

    [Fact]
    public void SubmitButtonEnabledWhenModelIsValid()
    {
        using var ctx = new TestContext();
        var now = DateTime.Now;
        var model = new MarketCycleFormModel
        {
            Name = "Ciclo de Teste",
            Status = MarketCycleStatus.Draft,
            StartsAtLocal = now,
            EndsAtLocal = now.AddHours(2)
        };

        var cut = ctx.RenderComponent<MarketCycleForm>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.ShowCancelButton, false));

        var submit = cut.Find("button[type=submit]");
        Assert.False(submit.HasAttribute("disabled"));
    }

    [Fact]
    public void DirtyStateChangedIsRaisedOnFieldChange()
    {
        using var ctx = new TestContext();
        var now = DateTime.Now;
        var model = new MarketCycleFormModel
        {
            Name = "Ciclo",
            Status = MarketCycleStatus.Draft,
            StartsAtLocal = now,
            EndsAtLocal = now.AddHours(1)
        };

        bool? dirty = null;
        var cut = ctx.RenderComponent<MarketCycleForm>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DirtyStateChanged, EventCallback.Factory.Create<bool>(this, value => dirty = value))
            .Add(p => p.ShowCancelButton, false));

        var nameInput = cut.Find("#cycle-name");
        nameInput.Change("Novo ciclo");

        Assert.True(dirty);
    }

    [Fact]
    public void OnValidSubmitIsInvokedWhenFormIsValid()
    {
        using var ctx = new TestContext();
        var now = DateTime.Now;
        var model = new MarketCycleFormModel
        {
            Name = "Ciclo válido",
            Status = MarketCycleStatus.Active,
            StartsAtLocal = now,
            EndsAtLocal = now.AddHours(4)
        };

        var submitted = false;
        var cut = ctx.RenderComponent<MarketCycleForm>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.ShowCancelButton, false)
            .Add(p => p.OnValidSubmit, EventCallback.Factory.Create<MarketCycleFormModel>(this, _ => submitted = true)));

        cut.Find("form").Submit();

        Assert.True(submitted);
    }
}
