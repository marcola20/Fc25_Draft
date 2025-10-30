using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Fc25Draft.Web.ViewModels.Admin;

public sealed class MarketItemDraftFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Selecione o ciclo ativo.")]
    public Guid? CycleId { get; set; }

    [Required(ErrorMessage = "Informe o identificador do jogador.")]
    [Range(1, int.MaxValue, ErrorMessage = "Jogador inválido.")]
    public int? PlayerId { get; set; }

    [Required(ErrorMessage = "Informe o valor base.")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "O valor base deve ser positivo.")]
    public decimal? BasePrice { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "O valor de compra imediata deve ser positivo.")]
    public decimal? BuyNowPrice { get; set; }

    [Required(ErrorMessage = "Informe o incremento mínimo.")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "O incremento deve ser positivo.")]
    public decimal? MinIncrement { get; set; }

    [Required(ErrorMessage = "Informe a data e horário de expiração.")]
    public string? ExpiresAtInput { get; set; }

    public DateTime? GetExpirationUtc()
    {
        if (string.IsNullOrWhiteSpace(ExpiresAtInput))
        {
            return null;
        }

        if (!DateTime.TryParse(ExpiresAtInput, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return null;
        }

        if (parsed.Kind == DateTimeKind.Unspecified)
        {
            parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
        }

        return parsed.ToUniversalTime();
    }

    public void PopulateFromUtc(DateTime expiresAtUtc)
    {
        var local = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc).ToLocalTime();
        ExpiresAtInput = local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BasePrice.HasValue && MinIncrement.HasValue && MinIncrement.Value >= BasePrice.Value)
        {
            yield return new ValidationResult(
                "O incremento mínimo deve ser menor que o valor base.",
                new[] { nameof(MinIncrement) });
        }

        if (BuyNowPrice.HasValue && BasePrice.HasValue && BuyNowPrice.Value <= BasePrice.Value)
        {
            yield return new ValidationResult(
                "A compra imediata deve ser maior que o valor base.",
                new[] { nameof(BuyNowPrice) });
        }

        if (!string.IsNullOrWhiteSpace(ExpiresAtInput))
        {
            if (!DateTime.TryParse(ExpiresAtInput, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                yield return new ValidationResult(
                    "Data de expiração inválida. Utilize o formato AAAA-MM-DDTHH:MM.",
                    new[] { nameof(ExpiresAtInput) });
            }
            else
            {
                if (parsed.Kind == DateTimeKind.Unspecified)
                {
                    parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
                }

                if (parsed <= DateTime.Now)
                {
                    yield return new ValidationResult(
                        "A expiração deve estar no futuro.",
                        new[] { nameof(ExpiresAtInput) });
                }
            }
        }
    }
}

public enum MarketItemWizardStep
{
    PlayerSelection = 0,
    Pricing = 1,
    Review = 2
}

public sealed record MarketItemDraftListItemViewModel(
    Guid ItemId,
    string PlayerName,
    string Position,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc,
    string Status,
    uint RowVersion,
    Guid CycleId,
    int PlayerId);
