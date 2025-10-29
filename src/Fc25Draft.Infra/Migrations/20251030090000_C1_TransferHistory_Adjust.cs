using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class C1_TransferHistory_Adjust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'TransferHistoryId') IS NOT NULL AND COL_LENGTH('TransferHistories', 'TransferId') IS NULL
BEGIN
    EXEC sp_rename 'TransferHistories.TransferHistoryId', 'TransferId', 'COLUMN';
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'DataUtc') IS NOT NULL
BEGIN
    EXEC sp_rename 'TransferHistories.DataUtc', 'PerformedAtUtc', 'COLUMN';
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'Amount') IS NULL
BEGIN
    ALTER TABLE TransferHistories ADD Amount decimal(18,2) NULL;
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'Notes') IS NULL
BEGIN
    ALTER TABLE TransferHistories ADD Notes nvarchar(400) NULL;
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'PerformedBy') IS NULL
BEGIN
    ALTER TABLE TransferHistories ADD PerformedBy nvarchar(120) NULL;
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'Type') IS NULL
BEGIN
    ALTER TABLE TransferHistories ADD [Type] int NULL;
    UPDATE TransferHistories SET [Type] = 1 WHERE [Type] IS NULL;
    ALTER TABLE TransferHistories ALTER COLUMN [Type] int NOT NULL;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransferHistories_PlayerId_DataUtc' AND object_id = OBJECT_ID('[TransferHistories]'))
BEGIN
    DROP INDEX [IX_TransferHistories_PlayerId_DataUtc] ON [TransferHistories];
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransferHistories_PlayerId_PerformedAtUtc' AND object_id = OBJECT_ID('[TransferHistories]'))
BEGIN
    CREATE INDEX [IX_TransferHistories_PlayerId_PerformedAtUtc] ON [TransferHistories] ([PlayerId], [PerformedAtUtc] DESC);
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TransferHistories_Teams_FromTeamId')
BEGIN
    ALTER TABLE [TransferHistories] DROP CONSTRAINT [FK_TransferHistories_Teams_FromTeamId];
END
ALTER TABLE [TransferHistories]  WITH CHECK ADD CONSTRAINT [FK_TransferHistories_Teams_FromTeamId]
FOREIGN KEY([FromTeamId]) REFERENCES [Teams]([TeamId]) ON DELETE NO ACTION;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TransferHistories_Teams_ToTeamId')
BEGIN
    ALTER TABLE [TransferHistories] DROP CONSTRAINT [FK_TransferHistories_Teams_ToTeamId];
END
ALTER TABLE [TransferHistories]  WITH CHECK ADD CONSTRAINT [FK_TransferHistories_Teams_ToTeamId]
FOREIGN KEY([ToTeamId]) REFERENCES [Teams]([TeamId]) ON DELETE NO ACTION;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TransferHistories_Players_PlayerId')
BEGIN
    ALTER TABLE [TransferHistories] DROP CONSTRAINT [FK_TransferHistories_Players_PlayerId];
END
ALTER TABLE [TransferHistories]  WITH CHECK ADD CONSTRAINT [FK_TransferHistories_Players_PlayerId]
FOREIGN KEY([PlayerId]) REFERENCES [Players]([PlayerId]) ON DELETE NO ACTION;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransferHistories_PlayerId_PerformedAtUtc' AND object_id = OBJECT_ID('[TransferHistories]'))
BEGIN
    DROP INDEX [IX_TransferHistories_PlayerId_PerformedAtUtc] ON [TransferHistories];
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransferHistories_PlayerId_DataUtc' AND object_id = OBJECT_ID('[TransferHistories]'))
BEGIN
    CREATE INDEX [IX_TransferHistories_PlayerId_DataUtc] ON [TransferHistories] ([PlayerId], [DataUtc]);
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'Type') IS NOT NULL
BEGIN
    ALTER TABLE TransferHistories ALTER COLUMN [Type] int NULL;
    ALTER TABLE TransferHistories DROP COLUMN [Type];
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'PerformedBy') IS NOT NULL
BEGIN
    ALTER TABLE TransferHistories DROP COLUMN PerformedBy;
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'Notes') IS NOT NULL
BEGIN
    ALTER TABLE TransferHistories DROP COLUMN Notes;
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'Amount') IS NOT NULL
BEGIN
    ALTER TABLE TransferHistories DROP COLUMN Amount;
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'PerformedAtUtc') IS NOT NULL
BEGIN
    EXEC sp_rename 'TransferHistories.PerformedAtUtc', 'DataUtc', 'COLUMN';
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH('TransferHistories', 'TransferId') IS NOT NULL AND COL_LENGTH('TransferHistories', 'TransferHistoryId') IS NULL
BEGIN
    EXEC sp_rename 'TransferHistories.TransferId', 'TransferHistoryId', 'COLUMN';
END");
        }
    }
}
