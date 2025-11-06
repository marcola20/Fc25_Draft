using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations;

public partial class ExpandTransferOffers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CounterOfOfferId",
            table: "TransferOffers",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "SellOnPercent",
            table: "TransferOffers",
            type: "numeric(5,2)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ThreadId",
            table: "TransferOffers",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "TransferOfferTargets",
            columns: table => new
            {
                OfferTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferOfferTargets", x => x.OfferTargetId);
                table.ForeignKey(
                    name: "FK_TransferOfferTargets_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalTable: "Players",
                    principalColumn: "PlayerId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TransferOfferTargets_TransferOffers_OfferId",
                    column: x => x.OfferId,
                    principalTable: "TransferOffers",
                    principalColumn: "OfferId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TransferTransactions",
            columns: table => new
            {
                TransferTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                FromTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                ToTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                CashAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                SellOnPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferTransactions", x => x.TransferTransactionId);
                table.ForeignKey(
                    name: "FK_TransferTransactions_Teams_FromTeamId",
                    column: x => x.FromTeamId,
                    principalTable: "Teams",
                    principalColumn: "TeamId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TransferTransactions_Teams_ToTeamId",
                    column: x => x.ToTeamId,
                    principalTable: "Teams",
                    principalColumn: "TeamId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TransferTransactions_TransferOffers_OfferId",
                    column: x => x.OfferId,
                    principalTable: "TransferOffers",
                    principalColumn: "OfferId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TransferTransactionPlayers",
            columns: table => new
            {
                TransferTransactionPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                TransferTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferTransactionPlayers", x => x.TransferTransactionPlayerId);
                table.ForeignKey(
                    name: "FK_TransferTransactionPlayers_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalTable: "Players",
                    principalColumn: "PlayerId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TransferTransactionPlayers_TransferTransactions_TransferTransactionId",
                    column: x => x.TransferTransactionId,
                    principalTable: "TransferTransactions",
                    principalColumn: "TransferTransactionId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PlayerSellOnClauses",
            columns: table => new
            {
                PlayerSellOnClauseId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerId = table.Column<int>(type: "integer", nullable: false),
                BeneficiaryTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                TransferTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerSellOnClauses", x => x.PlayerSellOnClauseId);
                table.ForeignKey(
                    name: "FK_PlayerSellOnClauses_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalTable: "Players",
                    principalColumn: "PlayerId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlayerSellOnClauses_Teams_BeneficiaryTeamId",
                    column: x => x.BeneficiaryTeamId,
                    principalTable: "Teams",
                    principalColumn: "TeamId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlayerSellOnClauses_TransferTransactions_TransferTransactionId",
                    column: x => x.TransferTransactionId,
                    principalTable: "TransferTransactions",
                    principalColumn: "TransferTransactionId",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.Sql("""
            INSERT INTO "TransferOfferTargets" ("OfferTargetId", "OfferId", "PlayerId")
            SELECT "OfferId", "OfferId", "PlayerId"
            FROM "TransferOffers"
            WHERE "PlayerId" IS NOT NULL;
        """);

        migrationBuilder.Sql("""
            UPDATE "TransferOffers"
            SET "ThreadId" = "OfferId"
            WHERE "ThreadId" IS NULL;
        """);

        migrationBuilder.DropIndex(
            name: "IX_TransferOffers_PlayerId_Status",
            table: "TransferOffers");

        migrationBuilder.DropColumn(
            name: "PlayerId",
            table: "TransferOffers");

        migrationBuilder.AlterColumn<Guid>(
            name: "ThreadId",
            table: "TransferOffers",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlayerSellOnClauses_BeneficiaryTeamId",
            table: "PlayerSellOnClauses",
            column: "BeneficiaryTeamId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayerSellOnClauses_PlayerId_BeneficiaryTeamId",
            table: "PlayerSellOnClauses",
            columns: new[] { "PlayerId", "BeneficiaryTeamId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlayerSellOnClauses_TransferTransactionId",
            table: "PlayerSellOnClauses",
            column: "TransferTransactionId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferOfferTargets_OfferId_PlayerId",
            table: "TransferOfferTargets",
            columns: new[] { "OfferId", "PlayerId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TransferOffers_CounterOfOfferId",
            table: "TransferOffers",
            column: "CounterOfOfferId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferOffers_ThreadId",
            table: "TransferOffers",
            column: "ThreadId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferTransactionPlayers_PlayerId",
            table: "TransferTransactionPlayers",
            column: "PlayerId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferTransactionPlayers_TransferTransactionId_PlayerId",
            table: "TransferTransactionPlayers",
            columns: new[] { "TransferTransactionId", "PlayerId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TransferTransactions_FromTeamId",
            table: "TransferTransactions",
            column: "FromTeamId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferTransactions_OfferId",
            table: "TransferTransactions",
            column: "OfferId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TransferTransactions_ToTeamId",
            table: "TransferTransactions",
            column: "ToTeamId");

        migrationBuilder.AddForeignKey(
            name: "FK_TransferOffers_TransferOffers_CounterOfOfferId",
            table: "TransferOffers",
            column: "CounterOfOfferId",
            principalTable: "TransferOffers",
            principalColumn: "OfferId",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TransferOffers_TransferOffers_CounterOfOfferId",
            table: "TransferOffers");

        migrationBuilder.DropIndex(
            name: "IX_TransferOffers_CounterOfOfferId",
            table: "TransferOffers");

        migrationBuilder.DropIndex(
            name: "IX_TransferOffers_ThreadId",
            table: "TransferOffers");

        migrationBuilder.DropColumn(
            name: "CounterOfOfferId",
            table: "TransferOffers");

        migrationBuilder.DropColumn(
            name: "SellOnPercent",
            table: "TransferOffers");

        migrationBuilder.DropColumn(
            name: "ThreadId",
            table: "TransferOffers");

        migrationBuilder.AddColumn<int>(
            name: "PlayerId",
            table: "TransferOffers",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("""
            UPDATE "TransferOffers" o
            SET "PlayerId" = sub."PlayerId"
            FROM (
                SELECT DISTINCT ON ("OfferId") "OfferId", "PlayerId"
                FROM "TransferOfferTargets"
                ORDER BY "OfferId", "PlayerId"
            ) AS sub
            WHERE sub."OfferId" = o."OfferId";
        """);

        migrationBuilder.AlterColumn<int>(
            name: "PlayerId",
            table: "TransferOffers",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_TransferOffers_PlayerId_Status",
            table: "TransferOffers",
            columns: new[] { "PlayerId", "Status" });

        migrationBuilder.DropTable(
            name: "PlayerSellOnClauses");

        migrationBuilder.DropTable(
            name: "TransferTransactionPlayers");

        migrationBuilder.DropTable(
            name: "TransferTransactions");

        migrationBuilder.DropTable(
            name: "TransferOfferTargets");
    }
}
