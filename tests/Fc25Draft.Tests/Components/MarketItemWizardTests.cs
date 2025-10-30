using System.Reflection;
using Bunit;
using Fc25Draft.Web.Pages.Market;

namespace Fc25Draft.Tests.Components;

public class MarketItemWizardTests
{
    [Fact]
    public void Submit_ShowsValidationMessage_WhenCycleIdIsInvalid()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<MarketItemWizard>();

        var cycleInput = cut.Find("#cycle-id");
        cycleInput.Change("invalid-guid");

        cut.Find("form").Submit();

        var validationMessage = cut.Find(".validation-message");
        Assert.Contains("Enter a valid GUID.", validationMessage.TextContent);
    }

    [Fact]
    public async Task PublishFromWizardAsync_EnablesPublishButton()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<MarketItemWizard>();

        var publishButton = cut.Find("button.btn-success");
        Assert.True(publishButton.HasAttribute("disabled"));

        var method = typeof(MarketItemWizard).GetMethod(
            "PublishFromWizardAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Method not found");

        await cut.InvokeAsync(async () =>
        {
            var task = (Task?)method.Invoke(cut.Instance, Array.Empty<object?>());
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        });

        publishButton = cut.Find("button.btn-success");
        Assert.False(publishButton.HasAttribute("disabled"));

        var reviewValidField = typeof(MarketItemWizard).GetField(
            "reviewValid",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Field not found");

        Assert.True((bool)reviewValidField.GetValue(cut.Instance)!);

        var submittingField = typeof(MarketItemWizard).GetField(
            "isSubmittingDraft",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Field not found");

        Assert.False((bool)submittingField.GetValue(cut.Instance)!);
    }
}
