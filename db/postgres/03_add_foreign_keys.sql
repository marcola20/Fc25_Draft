-- Foreign keys
alter table "DraftRounds"
    add constraint "FK_DraftRounds_Drafts_DraftId" foreign key ("DraftId") references "Drafts" ("DraftId") on delete cascade;

alter table "Players"
    add constraint "FK_Players_Positions_PositionId" foreign key ("PositionId") references "Positions" ("PositionId") on delete restrict;

alter table "Players"
    add constraint "FK_Players_Teams_CurrentTeamId" foreign key ("CurrentTeamId") references "Teams" ("TeamId") on delete set null;

alter table "BudgetLedgers"
    add constraint "FK_BudgetLedgers_Teams_TeamId" foreign key ("TeamId") references "Teams" ("TeamId") on delete restrict;

alter table "DraftPicks"
    add constraint "FK_DraftPicks_Drafts_DraftId" foreign key ("DraftId") references "Drafts" ("DraftId") on delete restrict;

alter table "DraftPicks"
    add constraint "FK_DraftPicks_DraftRounds_DraftId_RoundNumber" foreign key ("DraftId", "RoundNumber") references "DraftRounds" ("DraftId", "RoundNumber") on delete cascade;

alter table "DraftPicks"
    add constraint "FK_DraftPicks_Players_PlayerId" foreign key ("PlayerId") references "Players" ("PlayerId") on delete restrict;

alter table "DraftPicks"
    add constraint "FK_DraftPicks_Teams_TeamId" foreign key ("TeamId") references "Teams" ("TeamId") on delete restrict;

alter table "MarketItems"
    add constraint "FK_MarketItems_MarketCycles_CycleId" foreign key ("CycleId") references "MarketCycles" ("CycleId") on delete cascade;

alter table "MarketItems"
    add constraint "FK_MarketItems_Players_PlayerId" foreign key ("PlayerId") references "Players" ("PlayerId") on delete restrict;

alter table "MarketItems"
    add constraint "FK_MarketItems_Teams_CurrentLeaderTeamId" foreign key ("CurrentLeaderTeamId") references "Teams" ("TeamId") on delete restrict;

alter table "MarketItems"
    add constraint "FK_MarketItems_Teams_WinnerTeamId" foreign key ("WinnerTeamId") references "Teams" ("TeamId") on delete restrict;

alter table "TeamRosters"
    add constraint "FK_TeamRosters_Players_PlayerId" foreign key ("PlayerId") references "Players" ("PlayerId") on delete cascade;

alter table "TeamRosters"
    add constraint "FK_TeamRosters_Teams_TeamId" foreign key ("TeamId") references "Teams" ("TeamId") on delete cascade;

alter table "TransferHistories"
    add constraint "FK_TransferHistories_Players_PlayerId" foreign key ("PlayerId") references "Players" ("PlayerId") on delete restrict;

alter table "TransferHistories"
    add constraint "FK_TransferHistories_Teams_FromTeamId" foreign key ("FromTeamId") references "Teams" ("TeamId") on delete no action;

alter table "TransferHistories"
    add constraint "FK_TransferHistories_Teams_ToTeamId" foreign key ("ToTeamId") references "Teams" ("TeamId") on delete no action;

alter table "MarketBids"
    add constraint "FK_MarketBids_MarketItems_ItemId" foreign key ("ItemId") references "MarketItems" ("ItemId") on delete cascade;

alter table "MarketBids"
    add constraint "FK_MarketBids_Teams_TeamId" foreign key ("TeamId") references "Teams" ("TeamId") on delete restrict;

alter table "RoundSelections"
    add constraint "FK_RoundSelections_Rounds_RoundId" foreign key ("RoundId") references "Rounds" ("RoundId") on delete cascade;

alter table "RoundSelectionPlayers"
    add constraint "FK_RoundSelectionPlayers_RoundSelections_RoundSelectionId" foreign key ("RoundSelectionId") references "RoundSelections" ("RoundSelectionId") on delete cascade;

alter table "RoundSelectionPlayers"
    add constraint "FK_RoundSelectionPlayers_Players_PlayerGuid" foreign key ("PlayerGuid") references "Players" ("PlayerGuid") on delete cascade;

-- Indexes and unique constraints
create index "IX_AdminActionsLog_ActionType_CreatedAtUtc" on "AdminActionsLog" ("ActionType" asc, "CreatedAtUtc" desc);

create unique index "IX_Token_Administrador_Token" on "Token_Administrador" ("Token");

create unique index "IX_Teams_TeamName" on "Teams" ("TeamName");

create unique index "IX_Teams_Token" on "Teams" ("Token");

create index "IX_BudgetLedger_TeamId_DataUtc" on "BudgetLedgers" ("TeamId" asc, "DataUtc" desc);

create index "IX_Players_CurrentTeamId" on "Players" ("CurrentTeamId");

create unique index "IX_Players_PlayerGuid" on "Players" ("PlayerGuid");

create index "IX_Players_PositionId" on "Players" ("PositionId");

create index "IX_Players_Name_PositionId" on "Players" ("Name", "PositionId");

create unique index "IX_Positions_Name" on "Positions" ("Name");

create unique index "IX_DraftPicks_DraftId_RoundNumber_PickInRound" on "DraftPicks" ("DraftId", "RoundNumber", "PickInRound");

create index "IX_DraftPicks_DraftId_TeamId_RoundNumber" on "DraftPicks" ("DraftId", "TeamId", "RoundNumber");

create unique index "IX_DraftPicks_PlayerId" on "DraftPicks" ("PlayerId") where "PlayerId" is not null;

create index "IX_DraftPicks_TeamId" on "DraftPicks" ("TeamId");

create index "IX_MarketItems_CycleId_Status_ExpiresAtUtc" on "MarketItems" ("CycleId", "Status", "ExpiresAtUtc");

create unique index "IX_MarketItems_CycleId_PlayerId" on "MarketItems" ("CycleId", "PlayerId");

create index "IX_MarketItems_CurrentLeaderTeamId" on "MarketItems" ("CurrentLeaderTeamId");

create index "IX_MarketItems_Player" on "MarketItems" ("PlayerId");

create index "IX_MarketItems_WinnerTeamId" on "MarketItems" ("WinnerTeamId");

create unique index "IX_TeamRosters_PlayerId" on "TeamRosters" ("PlayerId");

create index "IX_TransferHistories_FromTeamId" on "TransferHistories" ("FromTeamId");

create index "IX_TransferHistories_ToTeamId" on "TransferHistories" ("ToTeamId");

create index "IX_TransferHistories_PlayerId_PerformedAtUtc" on "TransferHistories" ("PlayerId", "PerformedAtUtc");

create index "IX_MarketBids_TeamId" on "MarketBids" ("TeamId");

create index "IX_MarketBids_ItemId_CreatedAtUtc" on "MarketBids" ("ItemId", "CreatedAtUtc");

create unique index "IX_RoundSelections_RoundId" on "RoundSelections" ("RoundId");

create index "IX_RoundSelectionPlayers_PlayerGuid" on "RoundSelectionPlayers" ("PlayerGuid");

create index "IX_RoundSelectionPlayers_RoundSelectionId" on "RoundSelectionPlayers" ("RoundSelectionId");
