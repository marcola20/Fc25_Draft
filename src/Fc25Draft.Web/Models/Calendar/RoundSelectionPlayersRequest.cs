using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class RoundSelectionPlayersRequest
{
    [JsonPropertyName("playerIds")]
    public IReadOnlyCollection<Guid> PlayerIds { get; init; } = Array.Empty<Guid>();
}
