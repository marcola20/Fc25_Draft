using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class HallDaFamaAutomatico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LigaId",
                table: "HallOfFame",
                type: "uuid",
                nullable: true);

            // As competições já encerradas: liga as entradas lançadas à mão (mesmo tipo, campeão e temporada,
            // com 2008 = 3ª temporada) e cria as que faltam, com o treinador do campeão quando ela foi encerrada.
            migrationBuilder.Sql(@"
                UPDATE ""HallOfFame"" h SET ""LigaId"" = l.""LigaId""
                FROM ""Ligas"" l
                JOIN ""Teams"" t ON t.""TeamId"" = l.""CampeaoTimeId""
                WHERE l.""Status"" = 4
                  AND h.""LigaId"" IS NULL
                  AND h.""Tipo"" = l.""Tipo""
                  AND h.""TimeCampeao"" = t.""TeamName""
                  AND h.""Temporada"" = (l.""Temporada"" - 2005)::text;");

            // Lançada à mão logo que a competição acabou, mesmo com outra temporada escrita (ex.: Supercopa 2009).
            migrationBuilder.Sql(@"
                UPDATE ""HallOfFame"" h SET ""LigaId"" = l.""LigaId""
                FROM ""Ligas"" l
                JOIN ""Teams"" t ON t.""TeamId"" = l.""CampeaoTimeId""
                WHERE l.""Status"" = 4
                  AND h.""LigaId"" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM ""HallOfFame"" x WHERE x.""LigaId"" = l.""LigaId"")
                  AND h.""Tipo"" = l.""Tipo""
                  AND h.""TimeCampeao"" = t.""TeamName""
                  AND h.""CriadoEm"" BETWEEN l.""AtualizadoEm"" - interval '1 day' AND l.""AtualizadoEm"" + interval '1 day';");

            migrationBuilder.Sql(@"
                INSERT INTO ""HallOfFame""
                    (""HallOfFameId"", ""LigaId"", ""Descricao"", ""Tipo"", ""Divisao"", ""TimeCampeao"", ""Tecnico"", ""TreinadorId"",
                     ""Ano"", ""Temporada"", ""CriadoEm"", ""AtualizadoEm"")
                SELECT gen_random_uuid(), l.""LigaId"",
                       CASE l.""Tipo"" WHEN 1 THEN 'Copa do Brasil' WHEN 2 THEN 'Supercopa do Brasil' ELSE 'Brasileirão' END,
                       l.""Tipo"", CASE WHEN l.""Tipo"" = 0 THEN l.""Divisao"" END, t.""TeamName"",
                       tec.""Nome"", tec.""TreinadorId"",
                       EXTRACT(YEAR FROM l.""AtualizadoEm"" - interval '3 hours')::int,
                       (l.""Temporada"" - 2005)::text,
                       now() AT TIME ZONE 'utc', now() AT TIME ZONE 'utc'
                FROM ""Ligas"" l
                JOIN ""Teams"" t ON t.""TeamId"" = l.""CampeaoTimeId""
                LEFT JOIN LATERAL (
                    SELECT tr.""TreinadorId"", tr.""Nome""
                    FROM ""TreinadorPassagens"" p
                    JOIN ""Treinadores"" tr ON tr.""TreinadorId"" = p.""TreinadorId""
                    WHERE p.""TimeId"" = l.""CampeaoTimeId"" AND p.""Papel"" = 1
                      AND p.""Desde"" <= l.""AtualizadoEm"" AND (p.""Ate"" IS NULL OR p.""Ate"" > l.""AtualizadoEm"")
                    ORDER BY p.""Desde"" DESC
                    LIMIT 1) tec ON true
                WHERE l.""Status"" = 4
                  AND NOT EXISTS (SELECT 1 FROM ""HallOfFame"" h WHERE h.""LigaId"" = l.""LigaId"");");

            migrationBuilder.CreateIndex(
                name: "IX_HallOfFame_LigaId",
                table: "HallOfFame",
                column: "LigaId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HallOfFame_Ligas_LigaId",
                table: "HallOfFame",
                column: "LigaId",
                principalTable: "Ligas",
                principalColumn: "LigaId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HallOfFame_Ligas_LigaId",
                table: "HallOfFame");

            migrationBuilder.DropIndex(
                name: "IX_HallOfFame_LigaId",
                table: "HallOfFame");

            migrationBuilder.DropColumn(
                name: "LigaId",
                table: "HallOfFame");
        }
    }
}
