using System.ComponentModel.DataAnnotations;
using Fc25Draft.Web.Models.MarketCycles;
using Fc25Draft.Web.Utilities;

namespace Fc25Draft.Tests.Models;

public class MarketItemGenerationFormModelTests
{
    [Fact]
    public void CreateSetsDefaultValues()
    {
        var cycleId = Guid.NewGuid();

        var model = MarketItemGenerationFormModel.Create(cycleId);

        Assert.Equal(cycleId, model.CycleId);
        Assert.Equal(10, model.DesiredCount);
        Assert.Equal(24, model.Lifecycle.DurationHours);
    }

    [Fact]
    public void DesiredCountMustBePositive()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.DesiredCount = 0;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.DesiredCount)));
    }

    [Fact]
    public void FiltersValidateOverallRange()
    {
        var filters = new MarketItemGenerationFiltersModel
        {
            MinOverall = 90,
            MaxOverall = 80
        };

        var results = ValidateModel(filters);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFiltersModel.MinOverall)));
    }

    [Fact]
    public void LifecycleRequiresDuration()
    {
        var lifecycle = new MarketItemGenerationLifecycleModel
        {
            DurationHours = null
        };

        var results = ValidateModel(lifecycle);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationLifecycleModel.DurationHours)));
    }

    [Fact]
    public void LifecycleValidatesPublishBeforeExpire()
    {
        var lifecycle = new MarketItemGenerationLifecycleModel
        {
            PublishAtLocal = new DateTime(2024, 1, 2, 10, 0, 0),
            ExpiresAtLocal = new DateTime(2024, 1, 2, 9, 0, 0),
            DurationHours = 2
        };

        var results = ValidateModel(lifecycle);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationLifecycleModel.PublishAtLocal)));
    }

    [Fact]
    public void ToRequestDtoConvertsValues()
    {
        var cycleId = Guid.NewGuid();
        var model = MarketItemGenerationFormModel.Create(cycleId);
        model.DesiredCount = 5;
        model.Filters.PositionIds.Add(3);
        model.Filters.MinOverall = 70;
        model.Filters.MaxOverall = 90;
        model.Lifecycle.PublishAtLocal = new DateTime(2024, 1, 10, 12, 0, 0);
        model.Lifecycle.DurationHours = 12;

        var dto = model.ToRequestDto();

        Assert.Equal(model.DesiredCount, dto.DesiredCount);
        Assert.Contains((short)3, dto.Filters.PositionIds);
        Assert.Equal(model.Filters.MinOverall, dto.Filters.MinOverall);
        Assert.Equal(BrazilTime.ConvertToUtc(model.Lifecycle.PublishAtLocal!.Value), dto.Lifecycle.PublishAtUtc);
        Assert.Equal(model.Lifecycle.DurationHours, dto.Lifecycle.DurationHours);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
