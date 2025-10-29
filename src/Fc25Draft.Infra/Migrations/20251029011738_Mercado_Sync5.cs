using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Mercado_Sync5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_TeamToken",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TeamToken",
                table: "Teams");

            migrationBuilder.AddColumn<decimal>(
                name: "Budget",
                table: "Teams",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetBlocked",
                table: "Teams",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Teams",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentTeamId",
                table: "Players",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerGuid",
                table: "Players",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.CreateTable(
                name: "AdminActionsLog",
                columns: table => new
                {
                    ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActionsLog", x => x.ActionId);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLedgers",
                columns: table => new
                {
                    BudgetLedgerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLedgers", x => x.BudgetLedgerId);
                    table.CheckConstraint("CK_BudgetLedger_Tipo", "[Tipo] IN ('CREDIT','DEBIT')");
                    table.CheckConstraint("CK_BudgetLedger_Valor", "[Valor] > 0");
                    table.ForeignKey(
                        name: "FK_BudgetLedgers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCycles",
                columns: table => new
                {
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextCycleAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCycles", x => x.CycleId);
                });

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[TransferHistories]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TransferHistories]
    (
        [TransferId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [PlayerId] int NOT NULL,
        [FromTeamId] uniqueidentifier NULL,
        [ToTeamId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NULL,
        [Notes] nvarchar(400) NULL,
        [PerformedBy] nvarchar(120) NULL,
        [PerformedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TransferHistories] PRIMARY KEY ([TransferId]),
        CONSTRAINT [FK_TransferHistories_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players]([PlayerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TransferHistories_Teams_FromTeamId] FOREIGN KEY ([FromTeamId]) REFERENCES [Teams]([TeamId]),
        CONSTRAINT [FK_TransferHistories_Teams_ToTeamId] FOREIGN KEY ([ToTeamId]) REFERENCES [Teams]([TeamId])
    );
END
ELSE
BEGIN
    -- Ensure expected foreign keys exist
    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TransferHistories_Players_PlayerId'
    )
        ALTER TABLE [dbo].[TransferHistories] WITH CHECK ADD CONSTRAINT [FK_TransferHistories_Players_PlayerId]
            FOREIGN KEY([PlayerId]) REFERENCES [Players]([PlayerId]) ON DELETE NO ACTION;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TransferHistories_Teams_FromTeamId'
    )
        ALTER TABLE [dbo].[TransferHistories] WITH CHECK ADD CONSTRAINT [FK_TransferHistories_Teams_FromTeamId]
            FOREIGN KEY([FromTeamId]) REFERENCES [Teams]([TeamId]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TransferHistories_Teams_ToTeamId'
    )
        ALTER TABLE [dbo].[TransferHistories] WITH CHECK ADD CONSTRAINT [FK_TransferHistories_Teams_ToTeamId]
            FOREIGN KEY([ToTeamId]) REFERENCES [Teams]([TeamId]);
END
");

            migrationBuilder.CreateTable(
                name: "MarketItems",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BuyNowPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinIncrement = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentLeaderTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentLeaderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WinnerTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketItems", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_MarketItems_MarketCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "MarketCycles",
                        principalColumn: "CycleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketItems_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketItems_Teams_CurrentLeaderTeamId",
                        column: x => x.CurrentLeaderTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketItems_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketBids",
                columns: table => new
                {
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketBids", x => x.BidId);
                    table.ForeignKey(
                        name: "FK_MarketBids_MarketItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "MarketItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketBids_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Token",
                table: "Teams",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_CurrentTeamId",
                table: "Players",
                column: "CurrentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players",
                column: "PlayerGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionsLog_ActionType_CreatedAtUtc",
                table: "AdminActionsLog",
                columns: new[] { "ActionType", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLedger_TeamId_DataUtc",
                table: "BudgetLedgers",
                columns: new[] { "TeamId", "DataUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MarketBids_ItemId_CreatedAtUtc",
                table: "MarketBids",
                columns: new[] { "ItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketBids_TeamId",
                table: "MarketBids",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CurrentLeaderTeamId",
                table: "MarketItems",
                column: "CurrentLeaderTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CycleId_Status_ExpiresAtUtc",
                table: "MarketItems",
                columns: new[] { "CycleId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_Player",
                table: "MarketItems",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_WinnerTeamId",
                table: "MarketItems",
                column: "WinnerTeamId");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[TransferHistories]', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'IX_TransferHistories_FromTeamId' AND object_id = OBJECT_ID(N'[dbo].[TransferHistories]')
    )
        CREATE INDEX [IX_TransferHistories_FromTeamId] ON [dbo].[TransferHistories]([FromTeamId]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'IX_TransferHistories_PlayerId_PerformedAtUtc' AND object_id = OBJECT_ID(N'[dbo].[TransferHistories]')
    )
        CREATE INDEX [IX_TransferHistories_PlayerId_PerformedAtUtc] ON [dbo].[TransferHistories]([PlayerId], [PerformedAtUtc]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'IX_TransferHistories_ToTeamId' AND object_id = OBJECT_ID(N'[dbo].[TransferHistories]')
    )
        CREATE INDEX [IX_TransferHistories_ToTeamId] ON [dbo].[TransferHistories]([ToTeamId]);
END
");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Teams_CurrentTeamId",
                table: "Players",
                column: "CurrentTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Teams_CurrentTeamId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "AdminActionsLog");

            migrationBuilder.DropTable(
                name: "BudgetLedgers");

            migrationBuilder.DropTable(
                name: "MarketBids");

            migrationBuilder.DropTable(
                name: "TransferHistories");

            migrationBuilder.DropTable(
                name: "MarketItems");

            migrationBuilder.DropTable(
                name: "MarketCycles");

            migrationBuilder.DropIndex(
                name: "IX_Teams_Token",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Players_CurrentTeamId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Budget",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "BudgetBlocked",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CurrentTeamId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlayerGuid",
                table: "Players");

            migrationBuilder.AddColumn<Guid>(
                name: "TeamToken",
                table: "Teams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TeamToken",
                table: "Teams",
                column: "TeamToken",
                unique: true);
        }
    }
}
