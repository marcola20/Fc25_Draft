using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Fc25Draft.Web.Utilities;

namespace Fc25Draft.Web.Models.Transfers;

public class TransferHistoryFilterModel : IValidatableObject
{
    [Display(Name = "Time")]
    public Guid? TeamId { get; set; }

    [Display(Name = "Jogador")]
    public Guid? PlayerId { get; set; }

    [Display(Name = "Início do período")]
    public DateTime? PeriodStartLocal { get; set; }

    [Display(Name = "Fim do período")]
    public DateTime? PeriodEndLocal { get; set; }

    [Display(Name = "Observações")]
    [StringLength(200, ErrorMessage = "Informe no máximo 200 caracteres.")]
    public string? NotesQuery { get; set; }

    public bool OnlyAcquisitions { get; set; }

    [Range(1, 100, ErrorMessage = "Informe um tamanho de página válido.")]
    public int PageSize { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    public DateTime? PeriodStartUtc => PeriodStartLocal.HasValue ? BrazilTime.ConvertToUtc(PeriodStartLocal.Value) : null;

    public DateTime? PeriodEndUtc => PeriodEndLocal.HasValue ? BrazilTime.ConvertToUtc(PeriodEndLocal.Value) : null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PeriodStartLocal.HasValue && PeriodEndLocal.HasValue && PeriodStartLocal.Value > PeriodEndLocal.Value)
        {
            yield return new ValidationResult(
                "A data inicial deve ser anterior ou igual à data final.",
                new[] { nameof(PeriodStartLocal), nameof(PeriodEndLocal) });
        }
    }

    public void Reset()
    {
        TeamId = null;
        PlayerId = null;
        PeriodStartLocal = null;
        PeriodEndLocal = null;
        NotesQuery = null;
        OnlyAcquisitions = false;
        Page = 1;
    }
}

public class TransferHistoryQueryRequest
{
    public Guid? TeamId { get; init; }
    public Guid? PlayerId { get; init; }
    public Guid? CycleId { get; init; }
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public string? NotesQuery { get; init; }
    public bool OnlyAcquisitions { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
