using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Anúncio da aba Camisas (números dos jogadores), já valendo. Entra como rascunho: nem enviado no grupo nem
    /// publicado no site; quem decide é o admin em /admin/anuncios.
    /// </summary>
    public partial class AnuncioCamisas : Migration
    {
        private const string Id = "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e09";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var criadoEm = new DateTime(2026, 11, 18, 18, 0, 0);

            migrationBuilder.InsertData(
                table: "Anuncios",
                columns: new[]
                {
                    "AnuncioId", "Grupo", "Titulo", "Texto", "Explicacao", "Resumo", "TextoSite",
                    "Ordem", "EnviadoEm", "PublicadoEm", "CriadoEm", "AtualizadoEm"
                },
                values: new object[]
                {
                    Guid.Parse(Id), "Novidades já valendo", "Número das camisas",
                    Normalizar(Texto), Normalizar(Explicacao), Resumo, Normalizar(TextoSite),
                    10, null, null, criadoEm, criadoEm
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
            *Novidade: número das camisas* 👕

            Agora cada técnico escolhe o número dos seus jogadores. Na página do seu time, abra a aba *Camisas*: os números de 1 a 99 aparecem numa grade, 5 por fila, igual no PES.

            *Como colocar*
            - Toque no jogador e depois no número.
            - Ou arraste o jogador até o número (no computador).
            - Se o número já for de alguém, os dois trocam.

            Já deixamos os números que estão hoje no PES. Jogador que chegou e ainda não está no seu time no jogo ficou sem número: é só escolher.

            Os números vão para o PES. E não esquece: só vale depois de clicar em *Salvar as camisas*.
            """;

        private const string Explicacao =
            """
            Aba Camisas na página do time (/teams/details/{id}). Quem não é do time só vê a grade; o técnico e os auxiliares (pelo token) editam.

            O número fica no vínculo do jogador com o time: quando ele muda de clube (venda, troca, empréstimo, volta de empréstimo), chega sem número no clube novo. Não dá para repetir número no mesmo time (o banco recusa) e só vale de 1 a 99.

            Números iniciais: vêm do save do PES pelo botão "Enviar camisas ao site…" do Editor PES. Ele só preenche quem ainda não tem número no site e só se o número estiver livre no time; nunca troca o que o técnico já escolheu. Quem está em outro clube no jogo (transferência ainda não gravada no save) fica sem número.

            Para o jogo: ao abrir, o Editor PES baixa os números do site e deixa prontos para gravar (avisa quais mudaram). Quem estava com um desses números no jogo e não está no site ganha outro número livre.

            O número também aparece antes do nome na lista do elenco (aba Informações).
            """;

        private const string Resumo =
            "Na aba Camisas do time, o técnico escolhe o número de cada jogador numa grade de 1 a 99, igual no PES.";

        private const string TextoSite =
            """
            Cada técnico escolhe o *número da camisa* dos seus jogadores. Na página do seu time, abra a aba *Camisas*: os números de 1 a 99 aparecem numa grade, 5 por fila, igual no PES. Do lado fica o elenco, separado por goleiros, defensores, meio-campo e atacantes.

            ## Como colocar
            - *Tocando*: toque no jogador e depois no número. Se ele ainda não tinha número, o próximo sem número já fica escolhido, para ir preenchendo em sequência.
            - *Arrastando* (no computador): pegue o jogador da lista, ou de uma camisa da grade, e solte no número.
            - *Mudando quem já tem*: toque na camisa dele na grade e depois no número novo.

            Se o número já for de alguém, os dois *trocam*. Se quem estava com o número ficar sem nenhum, ele já fica escolhido para você dar outro.

            > Exemplo: você dá a 10 para o seu meia, que estava com a 20. O dono da 10 fica com a 20.

            Para deixar um jogador sem número, escolha ele e toque em *Tirar o número*.

            ## O que vale saber
            - Os números que estavam no PES já vieram prontos.
            - Quem chega de outro clube vem sem número: é só escolher.
            - Os números vão para o PES.
            - Nada é gravado até você clicar em *Salvar as camisas*.
            """;
    }
}
