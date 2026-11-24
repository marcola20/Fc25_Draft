using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AssociacaoDesempate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnteriorId",
                table: "AssociacaoEleicoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisaoPresidencia",
                table: "AssociacaoEleicoes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Desempate",
                table: "AssociacaoEleicoes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnteriorId",
                table: "AssociacaoEleicoes");

            migrationBuilder.DropColumn(
                name: "DecisaoPresidencia",
                table: "AssociacaoEleicoes");

            migrationBuilder.DropColumn(
                name: "Desempate",
                table: "AssociacaoEleicoes");
        }
    }
}
