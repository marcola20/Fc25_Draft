using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTokensDoTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rede de segurança: quem entrou depois da criação dos treinadores (ou foi
            // cadastrado direto no banco) ganha o cadastro antes das colunas sumirem.
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
                SELECT gen_random_uuid(), tr.""TreinadorId"", t.""TeamId"", 1, now() AT TIME ZONE 'utc', NULL
                FROM ""Teams"" t
                JOIN ""Treinadores"" tr ON upper(tr.""Token"") = upper(t.""Token"")
                WHERE NOT EXISTS (SELECT 1 FROM ""TreinadorPassagens"" p WHERE p.""TreinadorId"" = tr.""TreinadorId"");

                INSERT INTO ""TreinadorPassagens"" (""PassagemId"", ""TreinadorId"", ""TimeId"", ""Papel"", ""Desde"", ""Ate"")
                SELECT gen_random_uuid(), tr.""TreinadorId"", t.""TeamId"", 2, now() AT TIME ZONE 'utc', NULL
                FROM ""Teams"" t
                JOIN ""Treinadores"" tr ON upper(tr.""Token"") = upper(t.""AuxToken"")
                WHERE t.""AuxToken"" IS NOT NULL AND btrim(t.""AuxToken"") <> ''
                  AND NOT EXISTS (SELECT 1 FROM ""TreinadorPassagens"" p WHERE p.""TreinadorId"" = tr.""TreinadorId"");");

            migrationBuilder.DropIndex(
                name: "IX_Teams_AuxToken",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_Token",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AuxToken",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Teams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuxToken",
                table: "Teams",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Teams",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_AuxToken",
                table: "Teams",
                column: "AuxToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Token",
                table: "Teams",
                column: "Token",
                unique: true);
        }
    }
}
