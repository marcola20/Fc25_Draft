using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Regulamentos",
                columns: table => new
                {
                    RegulamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Conteudo = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regulamentos", x => x.RegulamentoId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Regulamentos_Temporada",
                table: "Regulamentos",
                column: "Temporada",
                unique: true);

            // As duas versões que a liga já teve: o regulamento de 2008/09 e o desta temporada,
            // com Série A e B. Ficam no banco para dar para editar e criar o da temporada seguinte.
            Semear(migrationBuilder, 2008, "Regulamento 2008 (1ª temporada)", "2009.html");
            Semear(migrationBuilder, 2009, "Regulamento 2009 (2ª temporada)", "2009.html");
            Semear(migrationBuilder, 2010, "Regulamento 2010 (3ª temporada)", "2010.html");
        }

        private static void Semear(MigrationBuilder migrationBuilder, int temporada, string titulo, string arquivo)
        {
            var conteudo = LerRecurso(arquivo);
            if (string.IsNullOrWhiteSpace(conteudo)) return;

            var agora = DateTime.UtcNow;

            migrationBuilder.InsertData(
                table: "Regulamentos",
                columns: new[] { "RegulamentoId", "Temporada", "Titulo", "Conteudo", "CriadoEm", "AtualizadoEm" },
                values: new object[] { Guid.NewGuid(), temporada, titulo, conteudo, agora, agora });
        }

        /// <summary>O texto de cada versão fica como arquivo embutido, para não virar string gigante aqui.</summary>
        private static string LerRecurso(string arquivo)
        {
            var assembly = typeof(AddRegulamento).Assembly;
            var nome = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Regulamentos." + arquivo, StringComparison.Ordinal));

            if (nome is null) return string.Empty;

            using var stream = assembly.GetManifestResourceStream(nome);
            if (stream is null) return string.Empty;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Regulamentos");
        }
    }
}
