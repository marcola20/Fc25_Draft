using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamIsAdminRemoveAdminToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Token_Administrador");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Teams");

            migrationBuilder.CreateTable(
                name: "Token_Administrador",
                columns: table => new
                {
                    AdminTokenId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Token_Administrador", x => x.AdminTokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Token_Administrador_Token",
                table: "Token_Administrador",
                column: "Token",
                unique: true);
        }
    }
}
