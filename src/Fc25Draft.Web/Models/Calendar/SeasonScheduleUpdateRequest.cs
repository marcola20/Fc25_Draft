using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar
{
    public sealed class SeasonScheduleUpdateRequest : IValidatableObject
    {
        [Required]
        [MinLength(1, ErrorMessage = "Informe ao menos 1 item.")]
        public List<SeasonScheduleUpdateItemRequest> Items { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Items is null || Items.Count == 0)
            {
                yield return new ValidationResult("Informe ao menos 1 item.", new[] { nameof(Items) });
                yield break;
            }

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var results = new List<ValidationResult>();
                var ctx = new ValidationContext(item);

                if (!Validator.TryValidateObject(item, ctx, results, validateAllProperties: true))
                {
                    foreach (var r in results)
                    {
                        yield return new ValidationResult(
                            $"Items[{i}]: {r.ErrorMessage}",
                            new[] { $"{nameof(Items)}[{i}]" }
                        );
                    }
                }
            }
        }
    }

    public sealed class SeasonScheduleUpdateItemRequest
    {
        [Range(1, 999, ErrorMessage = "Order deve ser entre 1 e 999.")]
        public int Order { get; set; }

        [Required(ErrorMessage = "RoundId é obrigatório.")]
        public Guid RoundId { get; set; }
    }
}
