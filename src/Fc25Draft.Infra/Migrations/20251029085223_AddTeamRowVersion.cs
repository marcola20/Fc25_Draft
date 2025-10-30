using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Teams",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "decode(md5(random()::text || clock_timestamp()::text), 'hex')");

            migrationBuilder.Sql(
                @"CREATE OR REPLACE FUNCTION set_team_rowversion()
RETURNS trigger AS $$
BEGIN
    NEW.\"RowVersion\" = decode(md5(random()::text || clock_timestamp()::text), 'hex');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tr_set_team_rowversion
BEFORE UPDATE ON \"Teams\"
FOR EACH ROW
EXECUTE FUNCTION set_team_rowversion();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP TRIGGER IF EXISTS tr_set_team_rowversion ON \"Teams\";
DROP FUNCTION IF EXISTS set_team_rowversion();");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Teams");
        }
    }
}
