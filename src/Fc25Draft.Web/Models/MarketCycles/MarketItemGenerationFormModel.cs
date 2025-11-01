using System.ComponentModel.DataAnnotations;
using Fc25Draft.Web.Utilities;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketItemGenerationFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Selecione um ciclo de mercado.")]
    public Guid? CycleId { get; set; }

    [Required(ErrorMessage = "Informe a quantidade desejada de itens.")]
    [Range(1, 200, ErrorMessage = "Informe uma quantidade entre 1 e 200.")]
    public int DesiredCount { get; set; } = 10;

    [Range(0, int.MaxValue, ErrorMessage = "Informe uma semente numérica válida.")]
    public int? Seed { get; set; }

    [Required(ErrorMessage = "Configure os filtros de geração.")]
    public MarketItemGenerationFiltersModel Filters { get; set; } = new();

    [Required(ErrorMessage = "Informe a configuração de publicação.")]
    public MarketItemGenerationLifecycleModel Lifecycle { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CycleId.HasValue || CycleId.Value == Guid.Empty)
        {
            yield return new ValidationResult(
                "Selecione um ciclo de mercado válido.",
                new[] { nameof(CycleId) });
        }
    }

    public MarketItemGenerationRequestDto ToRequestDto()
    {
        return new MarketItemGenerationRequestDto
        {
            DesiredCount = DesiredCount,
            Seed = Seed,
            Filters = Filters.ToDto(),
            Lifecycle = Lifecycle.ToDto()
        };
    }

    public static MarketItemGenerationFormModel Create(Guid cycleId)
    {
        return new MarketItemGenerationFormModel
        {
            CycleId = cycleId,
            DesiredCount = 10,
            Seed = null,
            Filters = new MarketItemGenerationFiltersModel(),
            Lifecycle = new MarketItemGenerationLifecycleModel()
        };
    }
}

public class MarketItemGenerationFiltersModel : IValidatableObject
{
    public List<int> PlayerIds { get; set; } = new();

    public List<short> PositionIds { get; set; } = new();

    [Range(0, 200, ErrorMessage = "O overall mínimo deve estar entre 0 e 200.")]
    public int? MinOverall { get; set; }

    [Range(0, 200, ErrorMessage = "O overall máximo deve estar entre 0 e 200.")]
    public int? MaxOverall { get; set; }

    [Range(15, 60, ErrorMessage = "A idade mínima deve estar entre 15 e 60 anos.")]
    public int? MinAge { get; set; }

    [Range(15, 60, ErrorMessage = "A idade máxima deve estar entre 15 e 60 anos.")]
    public int? MaxAge { get; set; }

    public bool OnlyFreeAgents { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinOverall.HasValue && MaxOverall.HasValue && MinOverall.Value > MaxOverall.Value)
        {
            yield return new ValidationResult(
                "O overall mínimo deve ser menor ou igual ao máximo.",
                new[] { nameof(MinOverall), nameof(MaxOverall) });
        }

        if (MinAge.HasValue && MaxAge.HasValue && MinAge.Value > MaxAge.Value)
        {
            yield return new ValidationResult(
                "A idade mínima deve ser menor ou igual à máxima.",
                new[] { nameof(MinAge), nameof(MaxAge) });
        }
    }

    public MarketItemGenerationFiltersDto ToDto()
    {
        return new MarketItemGenerationFiltersDto
        {
            PlayerIds = PlayerIds.Where(id => id > 0).Distinct().ToList(),
            PositionIds = PositionIds.Distinct().ToList(),
            MinOverall = MinOverall,
            MaxOverall = MaxOverall,
            MinAge = MinAge,
            MaxAge = MaxAge,
            OnlyFreeAgents = OnlyFreeAgents
        };
    }
}

public class MarketItemGenerationLifecycleModel : IValidatableObject
{
    public DateTime? PublishAtLocal { get; set; }

    public DateTime? ExpiresAtLocal { get; set; }

    [Required(ErrorMessage = "Informe a duração do anúncio em horas.")]
    [Range(1, 720, ErrorMessage = "A duração deve estar entre 1 e 720 horas.")]
    public int? DurationHours { get; set; } = 24;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PublishAtLocal.HasValue && ExpiresAtLocal.HasValue && PublishAtLocal.Value >= ExpiresAtLocal.Value)
        {
            yield return new ValidationResult(
                "A data de publicação deve ser anterior à data de expiração.",
                new[] { nameof(PublishAtLocal), nameof(ExpiresAtLocal) });
        }
    }

    public MarketItemGenerationLifecycleDto ToDto()
    {
        return new MarketItemGenerationLifecycleDto
        {
            PublishAtUtc = PublishAtLocal.HasValue ? BrazilTime.ConvertToUtc(PublishAtLocal.Value) : null,
            ExpiresAtUtc = ExpiresAtLocal.HasValue ? BrazilTime.ConvertToUtc(ExpiresAtLocal.Value) : null,
            DurationHours = DurationHours
        };
    }
}
