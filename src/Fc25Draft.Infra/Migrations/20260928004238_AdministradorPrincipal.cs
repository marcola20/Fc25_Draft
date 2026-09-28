using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AdministradorPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrincipal",
                table: "AdminTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // O dono da liga é o administrador que já estava cadastrado: o token ativo mais antigo.
            migrationBuilder.Sql(@"
                UPDATE ""AdminTokens""
                   SET ""IsPrincipal"" = TRUE
                 WHERE ""AdminTokenId"" = (
                        SELECT ""AdminTokenId""
                          FROM ""AdminTokens""
                         WHERE ""IsActive""
                         ORDER BY ""CreatedAtUtc""
                         LIMIT 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrincipal",
                table: "AdminTokens");
        }
    }
}
