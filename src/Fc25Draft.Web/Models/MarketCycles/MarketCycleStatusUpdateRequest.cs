using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketCycleStatusUpdateRequest : IValidatableObject
{
    [Required(ErrorMessage = "O status é obrigatório.")]
    [EnumDataType(typeof(MarketCycleStatus), ErrorMessage = "Status do ciclo inválido.")]
    public MarketCycleStatus? Status { get; set; }

    public bool ForceClose { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is null)
        {
            yield break;
        }

        if (Status == MarketCycleStatus.Draft)
        {
            yield return new ValidationResult(
                "Não é possível alterar o status para rascunho por este endpoint.",
                new[] { nameof(Status) });
        }
    }
}
