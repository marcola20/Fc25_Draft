using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLineupSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove TacticCode (IF EXISTS — safe to re-run)
            migrationBuilder.Sql(@"ALTER TABLE ""TeamLineups"" DROP COLUMN IF EXISTS ""TacticCode"";");

            // Add AutoSubstitution (IF NOT EXISTS — safe to re-run)
            migrationBuilder.Sql(@"
                ALTER TABLE ""TeamLineups""
                ADD COLUMN IF NOT EXISTS ""AutoSubstitution"" integer NOT NULL DEFAULT 1;
            ");

            // Rename ShortFreeKickLeft -> ShortFreeKick1 (only if old name still exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'TeamLineups'
                          AND column_name = 'ShortFreeKickLeftPlayerId'
                    ) THEN
                        ALTER TABLE ""TeamLineups""
                            RENAME COLUMN ""ShortFreeKickLeftPlayerId"" TO ""ShortFreeKick1PlayerId"";
                        ALTER INDEX IF EXISTS ""IX_TeamLineups_ShortFreeKickLeftPlayerId""
                            RENAME TO ""IX_TeamLineups_ShortFreeKick1PlayerId"";
                    END IF;
                END$$;
            ");

            // Rename ShortFreeKickRight -> ShortFreeKick2 (only if old name still exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'TeamLineups'
                          AND column_name = 'ShortFreeKickRightPlayerId'
                    ) THEN
                        ALTER TABLE ""TeamLineups""
                            RENAME COLUMN ""ShortFreeKickRightPlayerId"" TO ""ShortFreeKick2PlayerId"";
                        ALTER INDEX IF EXISTS ""IX_TeamLineups_ShortFreeKickRightPlayerId""
                            RENAME TO ""IX_TeamLineups_ShortFreeKick2PlayerId"";
                    END IF;
                END$$;
            ");

            // Add AttackingPlayer columns (IF NOT EXISTS — safe to re-run)
            migrationBuilder.Sql(@"
                ALTER TABLE ""TeamLineups"" ADD COLUMN IF NOT EXISTS ""AttackingPlayer1Id"" integer NULL;
                ALTER TABLE ""TeamLineups"" ADD COLUMN IF NOT EXISTS ""AttackingPlayer2Id"" integer NULL;
                ALTER TABLE ""TeamLineups"" ADD COLUMN IF NOT EXISTS ""AttackingPlayer3Id"" integer NULL;
            ");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_TeamLineups_AttackingPlayer1Id"" ON ""TeamLineups"" (""AttackingPlayer1Id"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_TeamLineups_AttackingPlayer2Id"" ON ""TeamLineups"" (""AttackingPlayer2Id"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_TeamLineups_AttackingPlayer3Id"" ON ""TeamLineups"" (""AttackingPlayer3Id"");");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_TeamLineups_Players_AttackingPlayer1Id'
                    ) THEN
                        ALTER TABLE ""TeamLineups""
                            ADD CONSTRAINT ""FK_TeamLineups_Players_AttackingPlayer1Id""
                            FOREIGN KEY (""AttackingPlayer1Id"") REFERENCES ""Players""(""PlayerId"")
                            ON DELETE RESTRICT;
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_TeamLineups_Players_AttackingPlayer2Id'
                    ) THEN
                        ALTER TABLE ""TeamLineups""
                            ADD CONSTRAINT ""FK_TeamLineups_Players_AttackingPlayer2Id""
                            FOREIGN KEY (""AttackingPlayer2Id"") REFERENCES ""Players""(""PlayerId"")
                            ON DELETE RESTRICT;
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_TeamLineups_Players_AttackingPlayer3Id'
                    ) THEN
                        ALTER TABLE ""TeamLineups""
                            ADD CONSTRAINT ""FK_TeamLineups_Players_AttackingPlayer3Id""
                            FOREIGN KEY (""AttackingPlayer3Id"") REFERENCES ""Players""(""PlayerId"")
                            ON DELETE RESTRICT;
                    END IF;
                END$$;
            ");

            // Create TeamLineupOffensiveInstructions table (IF NOT EXISTS — safe to re-run)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""TeamLineupOffensiveInstructions"" (
                    ""LineupId""       uuid    NOT NULL,
                    ""OffensiveStyle"" integer NOT NULL,
                    ""Playmaker""      integer NOT NULL,
                    ""AttackArea""     integer NOT NULL,
                    ""Positioning""    integer NOT NULL,
                    ""SupportRange""   integer NOT NULL,
                    CONSTRAINT ""PK_TeamLineupOffensiveInstructions"" PRIMARY KEY (""LineupId""),
                    CONSTRAINT ""FK_TeamLineupOffensiveInstructions_TeamLineups_LineupId""
                        FOREIGN KEY (""LineupId"") REFERENCES ""TeamLineups""(""LineupId"") ON DELETE CASCADE
                );
            ");

            // Rename old formation names to new naming convention.
            // Any unmapped formation is set to the new default "4-3-3 (4-2-1-3)".
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" SET ""Formation"" = CASE ""Formation""
                    WHEN '4-3-3'              THEN '4-3-3 (4-2-1-3)'
                    WHEN '4-3-3 Conservador'  THEN '4-3-3 (4-1-2-3)'
                    WHEN '4-2-3-1'            THEN '4-5-1 (4-2-3-1)'
                    WHEN '4-2-3-1 (2)'        THEN '4-5-1 (4-2-3-1)'
                    WHEN '4-2-3-1 (3)'        THEN '4-5-1 (4-2-3-1)'
                    WHEN '4-2-2-2'            THEN '4-4-2 (4-2-2-2) Padrão'
                    WHEN '4-2-2-2 (2)'        THEN '4-4-2 (4-2-2-2) Padrão'
                    WHEN '4-1-2-1-2 Aberto'   THEN '4-4-2 (4-3-1-2)'
                    WHEN '4-1-2-1-2'          THEN '4-4-2 (4-3-1-2)'
                    WHEN '4-2-1-3'            THEN '4-3-3 (4-2-1-3)'
                    WHEN '4-4-1-1'            THEN '4-5-1 (4-1-4-1)'
                    WHEN '4-3-2-1'            THEN '4-5-1 (4-3-2-1)'
                    WHEN '4-3-1-2'            THEN '4-4-2 (4-3-1-2)'
                    WHEN '4-5-1'              THEN '4-5-1 (4-2-3-1)'
                    WHEN '3-4-2-1'            THEN '3-4-3 (3-2-2-3)'
                    WHEN '5-3-2'              THEN '5-3-2 (5-3-2)'
                    ELSE '4-3-3 (4-2-1-3)'
                END
                WHERE ""Formation"" NOT IN (
                    '4-4-2 (4-2-2-2) Padrão','4-5-1 (4-2-3-1)','4-5-1 (4-1-4-1)',
                    '4-5-1 (4-3-2-1)','4-4-2 (4-2-2-2)','4-4-2 (4-3-1-2)',
                    '4-3-3 (4-2-1-3)','4-3-3 (4-1-2-3)','3-6-1 (3-2-4-1)',
                    '3-5-2 (3-2-3-2)','3-5-2 (3-3-2-2)','3-4-3 (3-2-2-3)',
                    '5-4-1 (5-2-2-1)','5-3-2 (5-2-1-2)','5-3-2 (5-3-2)'
                );
            ");

            // Also clear slots for lineups that had their formation renamed
            // (slot codes may be invalid for the new template, safer to clear them).
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineupSlots"" s
                SET ""PlayerId"" = NULL
                FROM ""TeamLineups"" l
                WHERE l.""LineupId"" = s.""LineupId"";
            ");

            // Create TeamLineupDefensiveInstructions table (IF NOT EXISTS — safe to re-run)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""TeamLineupDefensiveInstructions"" (
                    ""LineupId""          uuid    NOT NULL,
                    ""DefensiveStyle""    integer NOT NULL,
                    ""ContainmentArea""   integer NOT NULL,
                    ""Pressure""          integer NOT NULL,
                    ""DefensiveLine""     integer NOT NULL,
                    ""Density""           integer NOT NULL,
                    CONSTRAINT ""PK_TeamLineupDefensiveInstructions"" PRIMARY KEY (""LineupId""),
                    CONSTRAINT ""FK_TeamLineupDefensiveInstructions_TeamLineups_LineupId""
                        FOREIGN KEY (""LineupId"") REFERENCES ""TeamLineups""(""LineupId"") ON DELETE CASCADE
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""TeamLineupOffensiveInstructions"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""TeamLineupDefensiveInstructions"";");

            migrationBuilder.Sql(@"ALTER TABLE ""TeamLineups"" DROP COLUMN IF EXISTS ""AttackingPlayer1Id"";");
            migrationBuilder.Sql(@"ALTER TABLE ""TeamLineups"" DROP COLUMN IF EXISTS ""AttackingPlayer2Id"";");
            migrationBuilder.Sql(@"ALTER TABLE ""TeamLineups"" DROP COLUMN IF EXISTS ""AttackingPlayer3Id"";");
            migrationBuilder.Sql(@"ALTER TABLE ""TeamLineups"" DROP COLUMN IF EXISTS ""AutoSubstitution"";");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TeamLineups' AND column_name='ShortFreeKick1PlayerId') THEN
                        ALTER TABLE ""TeamLineups"" RENAME COLUMN ""ShortFreeKick1PlayerId"" TO ""ShortFreeKickLeftPlayerId"";
                        ALTER INDEX IF EXISTS ""IX_TeamLineups_ShortFreeKick1PlayerId"" RENAME TO ""IX_TeamLineups_ShortFreeKickLeftPlayerId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TeamLineups' AND column_name='ShortFreeKick2PlayerId') THEN
                        ALTER TABLE ""TeamLineups"" RENAME COLUMN ""ShortFreeKick2PlayerId"" TO ""ShortFreeKickRightPlayerId"";
                        ALTER INDEX IF EXISTS ""IX_TeamLineups_ShortFreeKick2PlayerId"" RENAME TO ""IX_TeamLineups_ShortFreeKickRightPlayerId"";
                    END IF;
                END$$;
            ");

            migrationBuilder.Sql(@"ALTER TABLE ""TeamLineups"" ADD COLUMN IF NOT EXISTS ""TacticCode"" character varying(40) NULL;");
        }
    }
}
