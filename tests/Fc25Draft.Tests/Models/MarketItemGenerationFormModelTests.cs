using System;
using System.ComponentModel.DataAnnotations;
using Fc25Draft.Web.Models.MarketCycles;

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
        Assert.True(model.ExcludeAlreadyListed);
        Assert.True(model.AutoSpreadExpirations);
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
    public void OverallRangeIsValidated()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.MinOverall = 90;
        model.MaxOverall = 80;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinOverall)));
    }

    [Fact]
    public void ManualExpirationRequiresDurations()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.AutoSpreadExpirations = false;
        model.MinLifespanHours = null;
        model.MaxLifespanHours = null;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinLifespanHours)));
    }

    [Fact]
    public void ManualExpirationValidatesRange()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.AutoSpreadExpirations = false;
        model.MinLifespanHours = 12;
        model.MaxLifespanHours = 6;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinLifespanHours)));
    }

    [Fact]
    public void ToRequestDtoConvertsTimeSpan()
    {
        var cycleId = Guid.NewGuid();
        var model = MarketItemGenerationFormModel.Create(cycleId);
        model.DesiredCount = 5;
        model.PositionIds.Add(3);
        model.MinOverall = 70;
        model.MaxOverall = 95;
        model.MaxPerTeam = 2;
        model.AutoSpreadExpirations = false;
        model.MinLifespanHours = 5;
        model.MaxLifespanHours = 12;

        var dto = model.ToRequestDto();

        Assert.Equal(model.DesiredCount, dto.DesiredCount);
        Assert.Contains((short)3, dto.PositionIds);
        Assert.Equal(TimeSpan.FromHours(5), dto.MinItemLifespan);
        Assert.Equal(TimeSpan.FromHours(12), dto.MaxItemLifespan);
        Assert.False(dto.AutoSpreadExpirationsAcrossCycle);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
