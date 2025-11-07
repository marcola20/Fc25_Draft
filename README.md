# FC25 Draft API Overview

## Quick Sell

`POST /api/teams/{teamId}/quick-sell/{playerId}` allows a team to immediately release a player in exchange for an automatic payout. The request must provide a valid team token, either in the JSON payload or via the `X-Team-Token` header.

### Request

```
POST /api/teams/{teamId}/quick-sell/{playerId}
X-Team-Token: {team-token}
Content-Type: application/json

{
  "teamToken": "{team-token}" // optional when header is present
}
```

### Responses

- `200 OK` – returns a `QuickSellResultDto` with payout, new player status and the updated team budget.
- `401 Unauthorized` – token missing.
- `403 Forbidden` – token invalid for the team.
- `404 Not Found` – team or player not found.
- `409 Conflict` – roster minimum would be violated or player already detached.

Sample success payload:

```json
{
  "teamId": "d5b8deed-5e74-4a66-8877-341f0d75b6dd",
  "teamName": "Time Azul",
  "playerId": 12,
  "playerGuid": "1c8d1c43-2a3f-4b26-9bc8-4b92ad197998",
  "playerName": "Jogador 12",
  "oldOverall": 81,
  "newOverall": 83,
  "status": 2,
  "basePrice": 15000000,
  "payout": 12000000,
  "budgetAfter": 198000000,
  "occurredAtUtc": "2024-11-25T18:30:22.415Z"
}
```

### Roster & Accounting Rules

- Teams must keep at least 18 players after the transaction.
- The payout equals 80% of the deterministic base price calculated for the player.
- The player's overall receives a deterministic bump and the status changes to `FreeAgent`.
- The transfer history records the quick sell with `OldOverall`, `NewOverall`, `Payout` and `OccurredAtUtc`.

## Team Tokens

All authenticated team operations must supply the team token, either through the `X-Team-Token` header or the `teamToken` field in the request body (when supported). The backend normalizes tokens by trimming whitespace and ignoring casing.
