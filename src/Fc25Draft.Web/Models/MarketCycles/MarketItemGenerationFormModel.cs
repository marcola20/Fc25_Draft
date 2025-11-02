using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketItemGenerationFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Selecione um ciclo de mercado.")]
    public Guid? CycleId { get; set; }

    [Required]
    [Range(1, 200, ErrorMessage = "Informe uma quantidade entre 1 e 200.")]
    public int DesiredCount { get; set; } = 10;

    public List<short> PositionIds { get; set; } = new();

    [Range(0, 200, ErrorMessage = "O overall mínimo deve estar entre 0 e 200.")]
    public int? MinOverall { get; set; }

    [Range(0, 200, ErrorMessage = "O overall máximo deve estar entre 0 e 200.")]
    public int? MaxOverall { get; set; }

    [Range(10, 60, ErrorMessage = "A idade mínima deve estar entre 10 e 60 anos.")]
    public int? MinAge { get; set; }

    [Range(10, 60, ErrorMessage = "A idade máxima deve estar entre 10 e 60 anos.")]
    public int? MaxAge { get; set; }

    [Range(0, 50, ErrorMessage = "O máximo por time deve estar entre 0 e 50.")]
    public int? MaxPerTeam { get; set; }

    public bool ExcludeAlreadyListedInOpenCycles { get; set; } = true;

    public bool EnsureUniquePlayerPerCycle { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "Informe uma semente numérica válida.")]
    public int? Seed { get; set; }

    public bool AutoSpreadExpirationsAcrossCycle { get; set; } = true;

    [Range(1, 720, ErrorMessage = "Informe um valor entre 1 e 720 horas.")]
    public int? MinLifespanHours { get; set; }

    [Range(1, 720, ErrorMessage = "Informe um valor entre 1 e 720 horas.")]
    public int? MaxLifespanHours { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CycleId.HasValue || CycleId.Value == Guid.Empty)
        {
            yield return new ValidationResult("Selecione um ciclo válido.", new[] { nameof(CycleId) });
        }

        if (MinOverall.HasValue && MaxOverall.HasValue && MinOverall.Value > MaxOverall.Value)
        {
            yield return new ValidationResult("O overall mínimo deve ser menor ou igual ao máximo.", new[] { nameof(MinOverall), nameof(MaxOverall) });
        }

        if (MinAge.HasValue && MaxAge.HasValue && MinAge.Value > MaxAge.Value)
        {
            yield return new ValidationResult("A idade mínima deve ser menor ou igual à máxima.", new[] { nameof(MinAge), nameof(MaxAge) });
        }

        if (!AutoSpreadExpirationsAcrossCycle)
        {
            if (!MinLifespanHours.HasValue || !MaxLifespanHours.HasValue)
            {
                yield return new ValidationResult("Informe a duração mínima e máxima quando o modo manual estiver selecionado.", new[] { nameof(MinLifespanHours), nameof(MaxLifespanHours) });
            }
            else if (MinLifespanHours.Value > MaxLifespanHours.Value)
            {
                yield return new ValidationResult("A duração mínima deve ser menor ou igual à máxima.", new[] { nameof(MinLifespanHours), nameof(MaxLifespanHours) });
            }
        }
    }

    public MarketItemGenerationRequestDto ToRequestDto()
    {
        return new MarketItemGenerationRequestDto
        {
            DesiredCount = DesiredCount,
            PositionIds = PositionIds,
            MinOverall = MinOverall,
            MaxOverall = MaxOverall,
            MinAge = MinAge,
            MaxAge = MaxAge,
            MaxPerTeam = MaxPerTeam,
            ExcludeAlreadyListedInOpenCycles = ExcludeAlreadyListedInOpenCycles,
            EnsureUniquePlayerPerCycle = EnsureUniquePlayerPerCycle,
            Seed = Seed,
            AutoSpreadExpirationsAcrossCycle = AutoSpreadExpirationsAcrossCycle,
            MinItemLifespan = AutoSpreadExpirationsAcrossCycle ? null : (MinLifespanHours.HasValue ? TimeSpan.FromHours(MinLifespanHours.Value) : null),
            MaxItemLifespan = AutoSpreadExpirationsAcrossCycle ? null : (MaxLifespanHours.HasValue ? TimeSpan.FromHours(MaxLifespanHours.Value) : null)
        };
    }

    public static MarketItemGenerationFormModel Create(Guid cycleId)
    {
        return new MarketItemGenerationFormModel
        {
            CycleId = cycleId,
            DesiredCount = 10,
            ExcludeAlreadyListedInOpenCycles = true,
            EnsureUniquePlayerPerCycle = true,
            AutoSpreadExpirationsAcrossCycle = true
        };
    }
}
