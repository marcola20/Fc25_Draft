using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCampeaoDoMataMata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nas temporadas em que o título saiu do mata-mata (2008), a liga foi encerrada sem
            // gravar o campeão: ele é quem venceu a final. Fase 10 = Final, Status 4 = Encerrada.
            migrationBuilder.Sql(@"
UPDATE ""Ligas"" l
SET ""CampeaoTimeId"" = k.""VencedorId""
FROM ""LigaKnockoutJogos"" k
WHERE k.""LigaId"" = l.""LigaId""
  AND k.""Fase"" = 10
  AND k.""VencedorId"" IS NOT NULL
  AND l.""CampeaoTimeId"" IS NULL
  AND l.""Status"" = 4;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
