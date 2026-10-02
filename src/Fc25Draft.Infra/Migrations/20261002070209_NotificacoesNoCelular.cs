using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesNoCelular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PushEnviadoEm",
                table: "AvisosTimes",
                type: "timestamp without time zone",
                nullable: true);

            // Os avisos que já existem não viram notificação: só os criados daqui em diante.
            migrationBuilder.Sql(@"UPDATE ""AvisosTimes"" SET ""PushEnviadoEm"" = ""CriadoEm"";");

            migrationBuilder.CreateTable(
                name: "ChavesPush",
                columns: table => new
                {
                    ChavePushId = table.Column<int>(type: "integer", nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrivateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChavesPush", x => x.ChavePushId);
                });

            migrationBuilder.CreateTable(
                name: "InscricoesPush",
                columns: table => new
                {
                    InscricaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Aparelho = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UltimoEnvioEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricoesPush", x => x.InscricaoId);
                    table.ForeignKey(
                        name: "FK_InscricoesPush_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificacoesEnviadas",
                columns: table => new
                {
                    Chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacoesEnviadas", x => x.Chave);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisosTimes_PushEnviadoEm",
                table: "AvisosTimes",
                column: "PushEnviadoEm",
                filter: "\"PushEnviadoEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesPush_Endpoint",
                table: "InscricoesPush",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesPush_TreinadorId",
                table: "InscricoesPush",
                column: "TreinadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChavesPush");

            migrationBuilder.DropTable(
                name: "InscricoesPush");

            migrationBuilder.DropTable(
                name: "NotificacoesEnviadas");

            migrationBuilder.DropIndex(
                name: "IX_AvisosTimes_PushEnviadoEm",
                table: "AvisosTimes");

            migrationBuilder.DropColumn(
                name: "PushEnviadoEm",
                table: "AvisosTimes");
        }
    }
}
