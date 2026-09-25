using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTreinadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Treinadores",
                columns: table => new
                {
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Token = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treinadores", x => x.TreinadorId);
                });

            migrationBuilder.CreateTable(
                name: "TreinadorPassagens",
                columns: table => new
                {
                    PassagemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Papel = table.Column<int>(type: "integer", nullable: false),
                    Desde = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Ate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreinadorPassagens", x => x.PassagemId);
                    table.ForeignKey(
                        name: "FK_TreinadorPassagens_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreinadorPassagens_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Treinadores_Token",
                table: "Treinadores",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreinadorPassagens_TimeId_Papel",
                table: "TreinadorPassagens",
                columns: new[] { "TimeId", "Papel" });

            migrationBuilder.CreateIndex(
                name: "IX_TreinadorPassagens_TreinadorId_Desde",
                table: "TreinadorPassagens",
                columns: new[] { "TreinadorId", "Desde" });

            // Quem já está na liga vira cadastro de pessoa, levando o token que já usava: o dono
            // do time vira treinador e o auxiliar, auxiliar. A entrada é a data da competição mais
            // antiga em que o time jogou (dá para ajustar depois na tela).
            migrationBuilder.Sql(@"
INSERT INTO ""Treinadores"" (""TreinadorId"", ""Nome"", ""Token"", ""Ativo"", ""CriadoEm"")
SELECT gen_random_uuid(), btrim(t.""OwnerName""), t.""Token"", true, now() AT TIME ZONE 'utc'
FROM ""Teams"" t
WHERE t.""OwnerName"" IS NOT NULL AND btrim(t.""OwnerName"") <> ''
  AND NOT EXISTS (SELECT 1 FROM ""Treinadores"" x WHERE upper(x.""Token"") = upper(t.""Token""));

INSERT INTO ""Treinadores"" (""TreinadorId"", ""Nome"", ""Token"", ""Ativo"", ""CriadoEm"")
SELECT gen_random_uuid(), btrim(t.""AuxiliarName""), t.""AuxToken"", true, now() AT TIME ZONE 'utc'
FROM ""Teams"" t
WHERE t.""AuxiliarName"" IS NOT NULL AND btrim(t.""AuxiliarName"") <> ''
  AND t.""AuxToken"" IS NOT NULL AND btrim(t.""AuxToken"") <> ''
  AND NOT EXISTS (SELECT 1 FROM ""Treinadores"" x WHERE upper(x.""Token"") = upper(t.""AuxToken""));

INSERT INTO ""TreinadorPassagens"" (""PassagemId"", ""TreinadorId"", ""TimeId"", ""Papel"", ""Desde"", ""Ate"")
SELECT gen_random_uuid(), tr.""TreinadorId"", t.""TeamId"", 1, coalesce(inicio.primeira, now() AT TIME ZONE 'utc'), NULL
FROM ""Teams"" t
JOIN ""Treinadores"" tr ON upper(tr.""Token"") = upper(t.""Token"")
LEFT JOIN LATERAL (
    SELECT min(l.""DataInicio"") AS primeira
    FROM ""Ligas"" l
    WHERE EXISTS (SELECT 1 FROM ""LigaTimes"" lt WHERE lt.""LigaId"" = l.""LigaId"" AND lt.""TimeId"" = t.""TeamId"")
       OR EXISTS (SELECT 1 FROM ""LigaClassificacoes"" lc WHERE lc.""LigaId"" = l.""LigaId"" AND lc.""TimeId"" = t.""TeamId"")
) inicio ON true
WHERE NOT EXISTS (SELECT 1 FROM ""TreinadorPassagens"" p WHERE p.""TreinadorId"" = tr.""TreinadorId"");

INSERT INTO ""TreinadorPassagens"" (""PassagemId"", ""TreinadorId"", ""TimeId"", ""Papel"", ""Desde"", ""Ate"")
SELECT gen_random_uuid(), tr.""TreinadorId"", t.""TeamId"", 2, coalesce(inicio.primeira, now() AT TIME ZONE 'utc'), NULL
FROM ""Teams"" t
JOIN ""Treinadores"" tr ON upper(tr.""Token"") = upper(t.""AuxToken"")
LEFT JOIN LATERAL (
    SELECT min(l.""DataInicio"") AS primeira
    FROM ""Ligas"" l
    WHERE EXISTS (SELECT 1 FROM ""LigaTimes"" lt WHERE lt.""LigaId"" = l.""LigaId"" AND lt.""TimeId"" = t.""TeamId"")
       OR EXISTS (SELECT 1 FROM ""LigaClassificacoes"" lc WHERE lc.""LigaId"" = l.""LigaId"" AND lc.""TimeId"" = t.""TeamId"")
) inicio ON true
WHERE t.""AuxToken"" IS NOT NULL AND btrim(t.""AuxToken"") <> ''
  AND NOT EXISTS (SELECT 1 FROM ""TreinadorPassagens"" p WHERE p.""TreinadorId"" = tr.""TreinadorId"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreinadorPassagens");

            migrationBuilder.DropTable(
                name: "Treinadores");
        }
    }
}
