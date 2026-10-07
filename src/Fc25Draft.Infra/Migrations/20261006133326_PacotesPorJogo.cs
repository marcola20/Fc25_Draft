using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PacotesPorJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O pacote da vitória vira o 1º dos pacotes do jogo (vitoria:{partida} → jogo:{partida}:1), então a
            // reconciliação só dá a diferença (2º e 3º da vitória, empates e derrotas) e nunca repete o que já saiu.
            migrationBuilder.Sql(@"
                UPDATE ""PacotesGanhos""
                SET ""Origem"" = 'jogo', ""Chave"" = 'jogo:' || substring(""Chave"" from 9) || ':1'
                WHERE ""Origem"" = 'vitoria' AND ""Chave"" LIKE 'vitoria:%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Só o 1º pacote de cada vitória tem para onde voltar; os outros pacotes de jogo ficam como estão.
            migrationBuilder.Sql(@"
                UPDATE ""PacotesGanhos""
                SET ""Origem"" = 'vitoria', ""Chave"" = 'vitoria:' || split_part(""Chave"", ':', 2)
                WHERE ""Origem"" = 'jogo' AND ""Chave"" LIKE 'jogo:%:1' AND ""Motivo"" LIKE 'Vitória%';");
        }
    }
}
