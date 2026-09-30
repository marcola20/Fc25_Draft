using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AtributosDosJogadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerAtributos",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    PesId = table.Column<int>(type: "integer", nullable: true),
                    Altura = table.Column<int>(type: "integer", nullable: true),
                    Peso = table.Column<int>(type: "integer", nullable: true),
                    PernaBoa = table.Column<int>(type: "integer", nullable: true),
                    TalentoOfensivo = table.Column<int>(type: "integer", nullable: false),
                    ControleDeBola = table.Column<int>(type: "integer", nullable: false),
                    Drible = table.Column<int>(type: "integer", nullable: false),
                    ConducaoFirme = table.Column<int>(type: "integer", nullable: false),
                    PasseRasteiro = table.Column<int>(type: "integer", nullable: false),
                    PasseAlto = table.Column<int>(type: "integer", nullable: false),
                    Finalizacao = table.Column<int>(type: "integer", nullable: false),
                    Cabeceio = table.Column<int>(type: "integer", nullable: false),
                    BolaParada = table.Column<int>(type: "integer", nullable: false),
                    Curva = table.Column<int>(type: "integer", nullable: false),
                    Velocidade = table.Column<int>(type: "integer", nullable: false),
                    Aceleracao = table.Column<int>(type: "integer", nullable: false),
                    ForcaDoChute = table.Column<int>(type: "integer", nullable: false),
                    Impulsao = table.Column<int>(type: "integer", nullable: false),
                    ContatoFisico = table.Column<int>(type: "integer", nullable: false),
                    Equilibrio = table.Column<int>(type: "integer", nullable: false),
                    Resistencia = table.Column<int>(type: "integer", nullable: false),
                    TalentoDefensivo = table.Column<int>(type: "integer", nullable: false),
                    Desarme = table.Column<int>(type: "integer", nullable: false),
                    Agressividade = table.Column<int>(type: "integer", nullable: false),
                    TalentoDeGoleiro = table.Column<int>(type: "integer", nullable: false),
                    FirmezaDoGoleiro = table.Column<int>(type: "integer", nullable: false),
                    AfastamentoDoGoleiro = table.Column<int>(type: "integer", nullable: false),
                    ReflexosDoGoleiro = table.Column<int>(type: "integer", nullable: false),
                    AlcanceDoGoleiro = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAtributos", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_PlayerAtributos_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Atributos dos jogadores encontrados no PES (ver .Dados.cs). Os que ficarem sem são
            // preenchidos ao subir o app (BasePesService) ou pela busca no Editar jogador.
            migrationBuilder.Sql(DadosAtributos);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerAtributos");
        }
    }
}
