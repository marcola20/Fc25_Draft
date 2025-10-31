using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Web.Components.MarketCycles;

public class MarketCycleFormModel : IValidatableObject
{
    [Required(ErrorMessage = "O nome do ciclo é obrigatório.")]
    [StringLength(120, ErrorMessage = "O nome do ciclo deve ter no máximo 120 caracteres.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "A data e hora de início são obrigatórias.")]
    public DateTime? StartsAtLocal { get; set; }

    [Required(ErrorMessage = "A data e hora de término são obrigatórias.")]
    public DateTime? EndsAtLocal { get; set; }

    [EnumDataType(typeof(MarketCycleStatus), ErrorMessage = "Status do ciclo inválido.")]
    public MarketCycleStatus Status { get; set; } = MarketCycleStatus.Draft;

    [StringLength(500, ErrorMessage = "As anotações devem ter no máximo 500 caracteres.")]
    public string? Notes { get; set; }

    public Guid? CycleId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAtLocal.HasValue && EndsAtLocal.HasValue && StartsAtLocal.Value >= EndsAtLocal.Value)
        {
            yield return new ValidationResult(
                "A data de início deve ser anterior à data de término.",
                new[] { nameof(StartsAtLocal), nameof(EndsAtLocal) });
        }
    }
}
