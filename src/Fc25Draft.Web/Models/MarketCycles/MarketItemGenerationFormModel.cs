using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketItemGenerationFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Selecione um ciclo válido.")]
    public Guid? CycleId { get; set; }

    [Range(1, 500, ErrorMessage = "Informe uma quantidade positiva.")]
    public int? DesiredCount { get; set; } = 10;

    public List<short> PositionIds { get; } = new();

    [Range(0, 200, ErrorMessage = "Overall mínimo inválido.")]
    public int? MinOverall { get; set; }

    [Range(0, 200, ErrorMessage = "Overall máximo inválido.")]
    public int? MaxOverall { get; set; }

    [Range(1, 50, ErrorMessage = "Limite por time inválido.")]
    public int? MaxPerTeam { get; set; }

    public bool ExcludeAlreadyListed { get; set; } = true;

    public bool EnsureUniqueInCycle { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "Informe uma semente válida.")]
    public int? Seed { get; set; }

    public bool AutoSpreadExpirations { get; set; } = true;

    [Range(0.25, 720, ErrorMessage = "Informe um valor positivo.")]
    public double? MinLifespanHours { get; set; }

    [Range(0.25, 720, ErrorMessage = "Informe um valor positivo.")]
    public double? MaxLifespanHours { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CycleId.HasValue || CycleId == Guid.Empty)
        {
            yield return new ValidationResult("Selecione um ciclo válido.", new[] { nameof(CycleId) });
        }

        if (MinOverall.HasValue && MaxOverall.HasValue && MinOverall > MaxOverall)
        {
            yield return new ValidationResult(
                "O overall mínimo deve ser menor ou igual ao máximo.",
                new[] { nameof(MinOverall), nameof(MaxOverall) });
        }

        if (!AutoSpreadExpirations)
        {
            if (!MinLifespanHours.HasValue || !MaxLifespanHours.HasValue)
            {
                yield return new ValidationResult(
                    "Informe a duração mínima e máxima para o modo manual.",
                    new[] { nameof(MinLifespanHours), nameof(MaxLifespanHours) });
            }
            else if (MinLifespanHours > MaxLifespanHours)
            {
                yield return new ValidationResult(
                    "A duração mínima deve ser menor ou igual à máxima.",
                    new[] { nameof(MinLifespanHours), nameof(MaxLifespanHours) });
            }
        }
    }

    public GenerateItemsRequestDto ToRequestDto()
    {
        return new GenerateItemsRequestDto
        {
            DesiredCount = DesiredCount,
            PositionIds = PositionIds.ToList(),
            MinOverall = MinOverall,
            MaxOverall = MaxOverall,
            MaxPerTeam = MaxPerTeam,
            ExcludeAlreadyListedInOpenCycles = ExcludeAlreadyListed,
            EnsureUniquePlayerPerCycle = EnsureUniqueInCycle,
            Seed = Seed,
            AutoSpreadExpirationsAcrossCycle = AutoSpreadExpirations,
            MinItemLifespan = AutoSpreadExpirations || !MinLifespanHours.HasValue ? null : TimeSpan.FromHours(MinLifespanHours.Value),
            MaxItemLifespan = AutoSpreadExpirations || !MaxLifespanHours.HasValue ? null : TimeSpan.FromHours(MaxLifespanHours.Value)
        };
    }

    public static MarketItemGenerationFormModel Create(Guid cycleId)
    {
        return new MarketItemGenerationFormModel
        {
            CycleId = cycleId,
            DesiredCount = 10,
            ExcludeAlreadyListed = true,
            EnsureUniqueInCycle = true,
            AutoSpreadExpirations = true
        };
    }
}
