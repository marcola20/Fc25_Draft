using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Anúncio da escalação montada no campo (branch feat/escalacao-campo), na leva da virada para 2011.
    /// Entra como rascunho: nem enviado no grupo nem publicado no site; quem decide é o admin em /admin/anuncios.
    /// </summary>
    public partial class AnuncioEscalacaoNoCampo : Migration
    {
        private const string Id = "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e08";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var criadoEm = new DateTime(2026, 11, 16, 1, 30, 0);

            migrationBuilder.InsertData(
                table: "Anuncios",
                columns: new[]
                {
                    "AnuncioId", "Grupo", "Titulo", "Texto", "Explicacao", "Resumo", "TextoSite",
                    "Ordem", "EnviadoEm", "PublicadoEm", "CriadoEm", "AtualizadoEm"
                },
                values: new object[]
                {
                    Guid.Parse(Id), "Virada para 2011", "Escalação montada no campo",
                    Normalizar(Texto), Normalizar(Explicacao), Resumo, Normalizar(TextoSite),
                    4, null, null, criadoEm, criadoEm
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Anuncios", keyColumn: "AnuncioId", keyValue: Guid.Parse(Id));
        }

        private static string Normalizar(string texto) => texto.Replace("\r\n", "\n");

        private const string Texto =
            """
            *Novidade na temporada 2011: Escalação montada no campo* ⚽

            Chega de escolher jogador em lista por lista. Na página de *Escalações* do seu time, os 11 titulares agora aparecem num campo, cada um no lugar da formação, com foto, nome e overall. O banco e quem está fora da escalação ficam do lado.

            *Como trocar*
            - *Arrastando*: pegue um jogador (do campo, do banco ou de fora) e solte no lugar. Se ele já estava escalado, troca de lugar com quem estava lá.
            - *Tocando no lugar*: abre ali mesmo a lista de quem pode jogar nele, do melhor overall para baixo, dizendo quem já está em campo ou no banco.
            - *No celular*: toque em quem está fora e depois no lugar.

            Enquanto você arrasta, os lugares onde o jogador pode jogar acendem e os outros apagam. Para tirar alguém do time, é só soltar em "Fora da escalação".

            As regras de quem joga onde são as mesmas de sempre. E não esquece: só vale depois de clicar em *Salvar escalação*.
            """;

        private const string Explicacao =
            """
            Muda só a aba Escalação da página Escalações do time (/teams/{id}/lineups). Funções (capitão, cobradores, quem ataca) e as instruções continuam nas outras abas, do mesmo jeito.

            Regra de quem pode jogar onde: é a mesma de antes, a do catálogo de formações (/admin/formacoes). Cada lugar usa uma posição, e a posição diz quais posições do PES podem jogar nela. O reserva aceita qualquer um.

            Troca: arrastar ou escolher um jogador que já está escalado leva quem estava no destino para o lugar de onde ele saiu. O banco aceita qualquer um, então trocar titular com reserva sempre dá. Entre dois titulares, se quem estava no destino não puder jogar no lugar de origem, ele fica fora da escalação e aparece um aviso (ex.: arrastar o volante para o lugar de um zagueiro: o volante entra, mas o zagueiro não joga de volante e vai para fora). Se o jogador arrastado veio de fora, quem estava no lugar vai para fora. Soltar onde o jogador não pode jogar não muda nada e mostra o aviso "Fulano (ME) não pode jogar de Goleiro".

            Quem sai dos titulares perde a função: se o capitão ou um cobrador for para o banco ou para fora, aquela função volta para "Melhor na função". Isso já era assim com as listas; vale avisar quem reclamar que o capitão "sumiu".

            Nada é gravado até clicar em Salvar escalação, como antes. Recarregar escalação desfaz o que não foi salvo.

            Celular: arrastar não funciona no toque. Lá é tocar em quem está fora (fica com borda dourada e aparece "Toque no lugar para colocar...") e depois no lugar; ou tocar no lugar e escolher na lista. Tocar no mesmo lugar de novo, ou na grama, fecha a lista.

            O desenho do campo (quem fica na lateral, onde fica o meia de ligação) é o mesmo do campo dos jogos. Se um lugar aparecer num ponto estranho, é o código do lugar na formação: na 3-4-1-2, os meias pelos lados usam os códigos LW/RW e por isso aparecem na altura dos pontas.

            Também aparece no topo do campo quantos titulares estão escalados (11/11) e a média de overall deles.
            """;

        private const string Resumo =
            "Os titulares aparecem num campo, nos lugares da formação: arraste para trocar ou toque no lugar e escolha ali mesmo.";

        private const string TextoSite =
            """
            A partir da temporada 2011, a escalação do seu time é montada num *campo*. Os 11 titulares aparecem cada um no lugar da formação, com foto, nome, overall e a posição do lugar. Do lado ficam o *banco* (7 reservas) e quem está *fora da escalação*.

            Em cima do campo você vê quantos titulares estão escalados e a média de overall deles.

            ## Arrastando
            Pegue um jogador de qualquer canto (campo, banco ou fora) e solte num lugar.
            - Se o lugar estava vazio, ele entra.
            - Se estava ocupado e o jogador que você arrastou já estava escalado, os dois *trocam de lugar*.
            - Se veio de fora, quem estava no lugar sai da escalação.

            Enquanto você arrasta, os lugares onde aquele jogador pode jogar *acendem* e os outros apagam. Soltou onde ele não pode? Nada muda e aparece um aviso.

            > Exemplo: você arrasta o seu lateral direito do banco para o lugar do lateral titular. O titular vai para o banco, no lugar de onde o reserva saiu.

            Para tirar alguém do time, solte o jogador em *Fora da escalação*.

            ## Tocando no lugar
            Toque num lugar do campo (ou do banco) e abre ali mesmo a lista de quem pode jogar nele, do melhor overall para baixo. A lista avisa quem já está *em campo* (e em qual posição) ou no *banco*.
            - Escolher alguém que já está escalado faz a troca, igual ao arrastar.
            - *Deixar o lugar vazio* tira quem está ali.

            > Exemplo: você toca no lugar do volante e escolhe um meia que está no banco. O meia entra, e o volante vai para o banco no lugar dele.

            ## No celular
            No celular não dá para arrastar. Toque em quem está fora da escalação (ele fica marcado) e depois toque no lugar. Ou toque no lugar e escolha na lista.

            ## O que não mudou
            - As regras de quem joga onde são as mesmas de antes.
            - Se o capitão ou um cobrador sai dos titulares, a função dele volta para "Melhor na função".
            - Funções e instruções continuam nas outras abas.
            - Nada é gravado até você clicar em *Salvar escalação*.
            """;
    }
}
