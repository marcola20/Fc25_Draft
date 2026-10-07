using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class RemoverEnvelhecimentoPulados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A coluna existiu por alguns minutos (envelhecer pulando quem "já estava na idade", desfeito):
            // some se o banco chegou a recebê-la.
            migrationBuilder.Sql("ALTER TABLE \"EnvelhecimentosTemporada\" DROP COLUMN IF EXISTS \"Pulados\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
