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
        Assert.True(model.AutoSpreadExpirationsAcrossCycle);
        Assert.True(model.ExcludeAlreadyListedInOpenCycles);
        Assert.True(model.EnsureUniquePlayerPerCycle);
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
    public void ValidateOverallRange()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.MinOverall = 90;
        model.MaxOverall = 80;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinOverall)));
    }

    [Fact]
    public void ValidateAgeRange()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.MinAge = 30;
        model.MaxAge = 20;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinAge)));
    }

    [Fact]
    public void ManualModeRequiresDurations()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.AutoSpreadExpirationsAcrossCycle = false;
        model.MinLifespanHours = null;
        model.MaxLifespanHours = null;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinLifespanHours)));
    }

    [Fact]
    public void ManualModeValidatesDurationRange()
    {
        var model = MarketItemGenerationFormModel.Create(Guid.NewGuid());
        model.AutoSpreadExpirationsAcrossCycle = false;
        model.MinLifespanHours = 24;
        model.MaxLifespanHours = 12;

        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MarketItemGenerationFormModel.MinLifespanHours)));
    }

    [Fact]
    public void ToRequestDtoConvertsValues()
    {
        var cycleId = Guid.NewGuid();
        var model = MarketItemGenerationFormModel.Create(cycleId);
        model.DesiredCount = 5;
        model.PositionIds.Add(3);
        model.MinOverall = 70;
        model.MaxOverall = 90;
        model.MinAge = 22;
        model.MaxAge = 28;
        model.AutoSpreadExpirationsAcrossCycle = false;
        model.MinLifespanHours = 12;
        model.MaxLifespanHours = 24;

        var dto = model.ToRequestDto();

        Assert.Equal(model.DesiredCount, dto.DesiredCount);
        Assert.Contains((short)3, dto.PositionIds);
        Assert.Equal(model.MinOverall, dto.MinOverall);
        Assert.Equal(model.MaxOverall, dto.MaxOverall);
        Assert.Equal(model.MinAge, dto.MinAge);
        Assert.Equal(model.MaxAge, dto.MaxAge);
        Assert.Equal(TimeSpan.FromHours(model.MinLifespanHours!.Value), dto.MinItemLifespan);
        Assert.Equal(TimeSpan.FromHours(model.MaxLifespanHours!.Value), dto.MaxItemLifespan);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
