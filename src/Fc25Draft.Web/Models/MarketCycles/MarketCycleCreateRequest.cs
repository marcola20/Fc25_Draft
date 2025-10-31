using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketCycleCreateRequest : IValidatableObject
{
    [Required(ErrorMessage = "O nome do ciclo é obrigatório.")]
    [StringLength(120, ErrorMessage = "O nome do ciclo deve ter no máximo 120 caracteres.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "A data de início é obrigatória.")]
    public DateTime? StartsAtUtc { get; set; }

    [Required(ErrorMessage = "A data de término é obrigatória.")]
    public DateTime? EndsAtUtc { get; set; }

    [EnumDataType(typeof(MarketCycleStatus), ErrorMessage = "Status do ciclo inválido.")]
    public MarketCycleStatus Status { get; set; } = MarketCycleStatus.Draft;

    [StringLength(500, ErrorMessage = "As anotações devem ter no máximo 500 caracteres.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAtUtc.HasValue && EndsAtUtc.HasValue && StartsAtUtc.Value >= EndsAtUtc.Value)
        {
            yield return new ValidationResult(
                "A data de início deve ser anterior à data de término.",
                new[] { nameof(StartsAtUtc), nameof(EndsAtUtc) });
        }
    }
}
