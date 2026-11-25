using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ClassicosEntreTreinadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PremioClassico",
                table: "TransferConfigs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 3000000m);

            migrationBuilder.CreateTable(
                name: "Rivalidades",
                columns: table => new
                {
                    RivalidadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorAId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorBId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rivalidades", x => x.RivalidadeId);
                    table.ForeignKey(
                        name: "FK_Rivalidades_Treinadores_TreinadorAId",
                        column: x => x.TreinadorAId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rivalidades_Treinadores_TreinadorBId",
                        column: x => x.TreinadorBId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassicoPremios",
                columns: table => new
                {
                    PremioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    RivalidadeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PagoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassicoPremios", x => x.PremioId);
                    table.ForeignKey(
                        name: "FK_ClassicoPremios_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassicoPremios_Rivalidades_RivalidadeId",
                        column: x => x.RivalidadeId,
                        principalTable: "Rivalidades",
                        principalColumn: "RivalidadeId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassicoPremios_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassicoPremios_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassicoPremios_PartidaId",
                table: "ClassicoPremios",
                column: "PartidaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassicoPremios_RivalidadeId",
                table: "ClassicoPremios",
                column: "RivalidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassicoPremios_TimeId",
                table: "ClassicoPremios",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassicoPremios_TreinadorId",
                table: "ClassicoPremios",
                column: "TreinadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Rivalidades_TreinadorAId_TreinadorBId",
                table: "Rivalidades",
                columns: new[] { "TreinadorAId", "TreinadorBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rivalidades_TreinadorBId",
                table: "Rivalidades",
                column: "TreinadorBId");

            // Os rivais combinados com a liga (pelo nome do cadastro). O prêmio vale dos jogos depois daqui.
            migrationBuilder.Sql("""
                INSERT INTO "Rivalidades" ("RivalidadeId", "TreinadorAId", "TreinadorBId", "Nome", "CriadaEm")
                SELECT gen_random_uuid(), LEAST(a."TreinadorId", b."TreinadorId"), GREATEST(a."TreinadorId", b."TreinadorId"),
                       NULL, now() AT TIME ZONE 'UTC'
                FROM (VALUES ('João Pedro', 'Pedro Boka'), ('Marcola', 'Kaio'), ('João Vilas', 'Gui Gomes'),
                             ('L. Felipe', 'Portuga'), ('Guilherme', 'Teuzin')) AS par(x, y)
                JOIN "Treinadores" a ON a."Nome" = par.x
                JOIN "Treinadores" b ON b."Nome" = par.y
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassicoPremios");

            migrationBuilder.DropTable(
                name: "Rivalidades");

            migrationBuilder.DropColumn(
                name: "PremioClassico",
                table: "TransferConfigs");
        }
    }
}
