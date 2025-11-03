using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Transfers;

public class OfferEditorModel
{
    [Required(ErrorMessage = "Informe o token do time emissor.")]
    [Display(Name = "Token do time")]
    public string? TeamToken { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "O valor deve ser maior ou igual a zero.")]
    [Display(Name = "Valor em dinheiro")]
    public decimal? Cash { get; set; }

    [Range(0, 100, ErrorMessage = "O percentual de sell-on deve estar entre 0% e 100%.")]
    [Display(Name = "Sell-on (%)")]
    public decimal? SellOnPercentage { get; set; }

    [MaxLength(500, ErrorMessage = "A mensagem deve ter no máximo 500 caracteres.")]
    [Display(Name = "Mensagem (opcional)")]
    public string? Message { get; set; }

    public HashSet<int> SwapPlayerIds { get; set; } = new();

    public int TargetPlayerId { get; set; }

    public Guid TargetPlayerGuid { get; set; }

    public string? TargetPlayerName { get; set; }

    public void ResetSwapPlayers(IEnumerable<int> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        SwapPlayerIds = new HashSet<int>(playerIds);
    }
}
