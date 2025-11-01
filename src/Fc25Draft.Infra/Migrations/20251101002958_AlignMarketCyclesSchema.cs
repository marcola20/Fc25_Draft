using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    public partial class AlignMarketCyclesSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='MarketCycles' AND column_name='NextCycleAtUtc'
              ) THEN
                ALTER TABLE "MarketCycles" RENAME COLUMN "NextCycleAtUtc" TO "EndsAtUtc";
              END IF;
            END
            $$;
            """);

            migrationBuilder.Sql("""
            ALTER TABLE "MarketCycles"
              ADD COLUMN IF NOT EXISTS "Name"          varchar(120),
              ADD COLUMN IF NOT EXISTS "StartsAtUtc"   timestamptz,
              ADD COLUMN IF NOT EXISTS "Notes"         varchar(500),
              ADD COLUMN IF NOT EXISTS "EndsAtUtc"     timestamptz,
              ADD COLUMN IF NOT EXISTS "UpdatedAtUtc"  timestamptz;
            """);

            migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='MarketCycles' AND column_name='CreatedAtUtc'
              ) THEN
                ALTER TABLE "MarketCycles" ALTER COLUMN "CreatedAtUtc" TYPE timestamptz;
              END IF;

              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='MarketCycles' AND column_name='StartsAtUtc'
              ) THEN
                ALTER TABLE "MarketCycles" ALTER COLUMN "StartsAtUtc" TYPE timestamptz;
              END IF;

              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='MarketCycles' AND column_name='EndsAtUtc'
              ) THEN
                ALTER TABLE "MarketCycles" ALTER COLUMN "EndsAtUtc" TYPE timestamptz;
              END IF;

              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='MarketCycles' AND column_name='UpdatedAtUtc'
              ) THEN
                ALTER TABLE "MarketCycles" ALTER COLUMN "UpdatedAtUtc" TYPE timestamptz;
              END IF;
            END
            $$;
            """);

            migrationBuilder.Sql("""
            UPDATE "MarketCycles"
            SET
              "Name"         = COALESCE("Name", 'Ciclo'),
              "StartsAtUtc"  = COALESCE("StartsAtUtc", "CreatedAtUtc"),
              "EndsAtUtc"    = COALESCE("EndsAtUtc", "CreatedAtUtc" + interval '7 days'),
              "UpdatedAtUtc" = COALESCE("UpdatedAtUtc","CreatedAtUtc");
            """);

            migrationBuilder.Sql("""
            ALTER TABLE "MarketCycles"
              ALTER COLUMN "Name"         SET NOT NULL,
              ALTER COLUMN "StartsAtUtc"  SET NOT NULL,
              ALTER COLUMN "EndsAtUtc"    SET NOT NULL,
              ALTER COLUMN "UpdatedAtUtc" SET NOT NULL;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
            ALTER TABLE "MarketCycles"
              DROP COLUMN IF EXISTS "Name",
              DROP COLUMN IF EXISTS "StartsAtUtc",
              DROP COLUMN IF EXISTS "Notes",
              DROP COLUMN IF EXISTS "UpdatedAtUtc";
            """);
        }
    }
}
