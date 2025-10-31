using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketCycleQueryRequest : IValidatableObject
{
    [Range(1, 200, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int PageSize { get; set; } = 20;

    [EnumDataType(typeof(MarketCycleStatus), ErrorMessage = "Status do ciclo inválido.")]
    public MarketCycleStatus? Status { get; set; }

    public DateTime? StartsAfterUtc { get; set; }

    public DateTime? StartsBeforeUtc { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAfterUtc.HasValue && StartsBeforeUtc.HasValue && StartsAfterUtc.Value > StartsBeforeUtc.Value)
        {
            yield return new ValidationResult(
                "A data inicial deve ser anterior ou igual à data final.",
                new[] { nameof(StartsAfterUtc), nameof(StartsBeforeUtc) });
        }
    }
}
