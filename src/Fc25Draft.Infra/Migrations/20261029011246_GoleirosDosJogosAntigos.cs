using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class GoleirosDosJogosAntigos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LigaGoleirosPartida",
                columns: table => new
                {
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaGoleirosPartida", x => new { x.PartidaId, x.TimeId });
                    table.ForeignKey(
                        name: "FK_LigaGoleirosPartida_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaGoleirosPartida_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaGoleirosPartida_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaGoleirosPartida_JogadorId",
                table: "LigaGoleirosPartida",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaGoleirosPartida_TimeId",
                table: "LigaGoleirosPartida",
                column: "TimeId");

            // Clean sheets de 2008 e 2009 (sem escalação): o goleiro de cada um, apontado pelo admin em 28/10/2026.
            // Só entra o que existe no banco (num banco novo, nada).
            migrationBuilder.Sql("""
                INSERT INTO "LigaGoleirosPartida" ("PartidaId", "TimeId", "JogadorId")
                SELECT v.partida, v.time, v.jogador
                FROM (VALUES
                    ('e36e3d1b-69fa-4ec6-86a3-62ef164ba410'::uuid, '74daf694-bf04-41f2-8d5a-4b1610a52da5'::uuid, 808), -- 2008 Botafogo R1: Jefferson
                    ('73061e85-8d4c-4d7f-b805-51e3884993cc'::uuid, '74daf694-bf04-41f2-8d5a-4b1610a52da5'::uuid, 808), -- 2008 Botafogo R2: Jefferson
                    ('584c5dd8-cd8a-4f23-8aa3-0e8b88009692'::uuid, 'e0e4a5f0-38f5-46a0-abf1-460f1dbc5fc9'::uuid, 805), -- 2008 Corinthians R4: Júlio César
                    ('d81fdab0-d1c4-4714-ae94-4cae7bbff34c'::uuid, 'e0e4a5f0-38f5-46a0-abf1-460f1dbc5fc9'::uuid, 805), -- 2008 Corinthians R6: Júlio César
                    ('ea2f54ee-4832-4dd6-b198-f78e34b99b27'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 810), -- 2008 Coritiba R2: Cássio
                    ('d1a9c670-958c-4436-98e7-5bcdfa961787'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 810), -- 2008 Coritiba R3: Cássio
                    ('9512cade-11b3-4b1b-aef0-34049ab7d867'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 810), -- 2008 Coritiba R4: Cássio
                    ('f69070a4-8487-478e-aa93-1827e0087399'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 810), -- 2008 Coritiba R6: Cássio
                    ('653c7f42-cb1e-48f1-aa93-b5d0f0e775c6'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2008 Cruzeiro R2: Edwin van der Sar
                    ('468526dc-b3be-477f-93b1-ab8257d01e2f'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2008 Cruzeiro R3: Edwin van der Sar
                    ('49cde25e-7e00-44f3-a56e-6c1e0c16dc7e'::uuid, 'db25bdf1-b273-4db0-8370-272d05fbd53f'::uuid, 807), -- 2008 Fluminense R6: Diego Alves
                    ('73061e85-8d4c-4d7f-b805-51e3884993cc'::uuid, 'a93e0bc8-6d33-4da2-9b1b-7fca4d2a1ecf'::uuid, 804), -- 2008 Grêmio R2: Dida
                    ('e36e3d1b-69fa-4ec6-86a3-62ef164ba410'::uuid, '4236a181-622c-4416-9016-96e8ebfa7c27'::uuid, 864), -- 2008 Internacional R1: Tim Howard
                    ('9b80f8e5-caa4-461a-bdd5-d8fe7134c6e0'::uuid, '4236a181-622c-4416-9016-96e8ebfa7c27'::uuid, 864), -- 2008 Internacional R2: Tim Howard
                    ('4c1da031-3bc4-4d7f-b98b-0109201a517e'::uuid, '4236a181-622c-4416-9016-96e8ebfa7c27'::uuid, 864), -- 2008 Internacional R3: Tim Howard
                    ('d63607e3-0653-4d83-acdf-626e4a38513b'::uuid, '4236a181-622c-4416-9016-96e8ebfa7c27'::uuid, 864), -- 2008 Internacional R6: Tim Howard
                    ('4270f7fc-308b-4dcd-bdb3-8e6dafcba64a'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 806), -- 2008 Palmeiras R2: Rogério Ceni
                    ('d32886f2-8ca8-499e-978d-72764d712cb4'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 806), -- 2008 Palmeiras R4: Rogério Ceni
                    ('14de98e8-d9c8-4134-9e76-b75937ebf538'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 813), -- 2008 Palmeiras R6: Santos
                    ('4270f7fc-308b-4dcd-bdb3-8e6dafcba64a'::uuid, 'df8fe96c-e5fe-4eb0-8eed-a31e2d32dcb7'::uuid, 858), -- 2008 Santos R2: Fernando Muslera
                    ('2a277715-1958-4008-ad24-88b8d9d17942'::uuid, 'df8fe96c-e5fe-4eb0-8eed-a31e2d32dcb7'::uuid, 858), -- 2008 Santos R5: Fernando Muslera
                    ('d81fdab0-d1c4-4714-ae94-4cae7bbff34c'::uuid, 'df8fe96c-e5fe-4eb0-8eed-a31e2d32dcb7'::uuid, 858), -- 2008 Santos R6: Fernando Muslera
                    ('9b80f8e5-caa4-461a-bdd5-d8fe7134c6e0'::uuid, '4815fe0a-953e-41bf-b98f-2cc81e42d88b'::uuid, 1656), -- 2008 São Paulo R2: Anthony Lopes
                    ('1732069c-bafd-4cb8-9db0-ed0eddb031ff'::uuid, '4815fe0a-953e-41bf-b98f-2cc81e42d88b'::uuid, 1656), -- 2008 São Paulo R6: Anthony Lopes
                    ('ea2f54ee-4832-4dd6-b198-f78e34b99b27'::uuid, 'fdc1b6c9-a920-4232-b338-28ea0bd54d63'::uuid, 871), -- 2008 Vasco R2: Fernando Prass
                    ('584c5dd8-cd8a-4f23-8aa3-0e8b88009692'::uuid, 'fdc1b6c9-a920-4232-b338-28ea0bd54d63'::uuid, 871), -- 2008 Vasco R4: Fernando Prass
                    ('aa13045b-c56a-4233-b1b3-eb3aaed790b2'::uuid, '74daf694-bf04-41f2-8d5a-4b1610a52da5'::uuid, 1623), -- 2009 Botafogo R1: Petr Čech
                    ('f6241611-7088-4960-88ca-d6bf11a478df'::uuid, '74daf694-bf04-41f2-8d5a-4b1610a52da5'::uuid, 1623), -- 2009 Botafogo R6: Petr Čech
                    ('1f64df8b-ebe7-4f26-be71-aabe6da97afe'::uuid, '74daf694-bf04-41f2-8d5a-4b1610a52da5'::uuid, 1623), -- 2009 Botafogo R8: Petr Čech
                    ('d5d036e4-dcf9-4227-b778-67908808d86e'::uuid, 'e0e4a5f0-38f5-46a0-abf1-460f1dbc5fc9'::uuid, 805), -- 2009 Corinthians R4: Júlio César
                    ('4962909f-a88a-41bb-887b-dd80cc1c0760'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 1700), -- 2009 Coritiba R1: Gianluigi Buffon
                    ('3da18b27-c8c0-47d7-bfd0-06ae3900e519'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 1700), -- 2009 Coritiba R3: Gianluigi Buffon
                    ('be3423c5-8e38-4501-81d8-6e79b9979fd3'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 1700), -- 2009 Coritiba R5: Gianluigi Buffon
                    ('d632285c-2975-4c0a-acab-c8e5fff314cb'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 1700), -- 2009 Coritiba R7: Gianluigi Buffon
                    ('26e6968d-c2c5-48aa-87f4-04bae643538d'::uuid, '31e2d4d3-1f85-4a27-8b86-182e75e63b5c'::uuid, 1700), -- 2009 Coritiba R11: Gianluigi Buffon
                    ('49616493-677e-4b8c-a053-fb199d0776dc'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2009 Cruzeiro R0: Edwin van der Sar
                    ('e9da394f-11fc-4bd2-a44a-021986f45aae'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2009 Cruzeiro R2: Edwin van der Sar
                    ('97f83408-5dea-4063-8d62-6548c5012e65'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2009 Cruzeiro R3: Edwin van der Sar
                    ('22cb52e7-5af8-488f-a22c-44496ab61122'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2009 Cruzeiro R7: Edwin van der Sar
                    ('de032bdc-3381-4219-9e9e-8119826e1e56'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2009 Cruzeiro R7: Edwin van der Sar
                    ('843e8d48-5a5c-4109-92da-9a743bcf58e5'::uuid, '52be4b3b-8ca0-47a2-8847-a02b124f9264'::uuid, 1644), -- 2009 Cruzeiro R8: Edwin van der Sar
                    ('0e47c5a5-5463-4c6c-a4a3-dd58e444c2e8'::uuid, '720a4e6d-5fee-4c39-921d-9e065bc9651f'::uuid, 810), -- 2009 Flamengo R3: Cássio
                    ('53d848b7-41c0-4578-89f0-9cf8781cc1f0'::uuid, '720a4e6d-5fee-4c39-921d-9e065bc9651f'::uuid, 810), -- 2009 Flamengo R4: Cássio
                    ('f31f9ef3-127b-4024-89d7-a452a559478c'::uuid, '720a4e6d-5fee-4c39-921d-9e065bc9651f'::uuid, 810), -- 2009 Flamengo R7: Cássio
                    ('9a45d67c-43a9-4023-ad85-54a6c883189c'::uuid, 'db25bdf1-b273-4db0-8370-272d05fbd53f'::uuid, 807), -- 2009 Fluminense R1: Diego Alves
                    ('e053cfc8-700a-464a-a6f8-c8f1d29f4f0d'::uuid, 'db25bdf1-b273-4db0-8370-272d05fbd53f'::uuid, 807), -- 2009 Fluminense R5: Diego Alves
                    ('6a8505ae-62dd-40ef-a16e-f196e277881c'::uuid, 'db25bdf1-b273-4db0-8370-272d05fbd53f'::uuid, 807), -- 2009 Fluminense R7: Diego Alves
                    ('a226dcdc-6b02-47e0-a036-6511b4565034'::uuid, 'a93e0bc8-6d33-4da2-9b1b-7fca4d2a1ecf'::uuid, 804), -- 2009 Grêmio R4: Dida
                    ('c00b11ff-aca4-4062-a675-607372f7e3bb'::uuid, 'a93e0bc8-6d33-4da2-9b1b-7fca4d2a1ecf'::uuid, 804), -- 2009 Grêmio R9: Dida
                    ('6b621755-5008-4fdf-a8fd-41884c195677'::uuid, '4236a181-622c-4416-9016-96e8ebfa7c27'::uuid, 864), -- 2009 Internacional R4: Tim Howard
                    ('4962909f-a88a-41bb-887b-dd80cc1c0760'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 1699), -- 2009 Palmeiras R1: Iker Casillas
                    ('aa13045b-c56a-4233-b1b3-eb3aaed790b2'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 1699), -- 2009 Palmeiras R1: Iker Casillas
                    ('c308df65-4989-48c0-a8c4-ec50c98c3208'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 1699), -- 2009 Palmeiras R2: Iker Casillas
                    ('62542d3b-b99b-4447-b499-0f5f21685c60'::uuid, '8b58bf8e-e20b-41ee-84c5-7c2e67de290c'::uuid, 1699), -- 2009 Palmeiras R8: Iker Casillas
                    ('f473f33c-3f98-4c2b-907b-38156122246a'::uuid, 'df8fe96c-e5fe-4eb0-8eed-a31e2d32dcb7'::uuid, 858), -- 2009 Santos R11: Fernando Muslera
                    ('85557f78-3237-43b4-95ea-e3dc4f5448a7'::uuid, '4815fe0a-953e-41bf-b98f-2cc81e42d88b'::uuid, 1656), -- 2009 São Paulo R2: Anthony Lopes
                    ('a226dcdc-6b02-47e0-a036-6511b4565034'::uuid, '4815fe0a-953e-41bf-b98f-2cc81e42d88b'::uuid, 1656), -- 2009 São Paulo R4: Anthony Lopes
                    ('656f0364-4906-4050-ad7e-a7b213132861'::uuid, '4815fe0a-953e-41bf-b98f-2cc81e42d88b'::uuid, 1656), -- 2009 São Paulo R10: Anthony Lopes
                    ('47a9cb74-43b1-4ed2-ab20-594f67c23a66'::uuid, 'fdc1b6c9-a920-4232-b338-28ea0bd54d63'::uuid, 871), -- 2009 Vasco R5: Fernando Prass
                    ('6b9ca0ec-cf5c-46f1-8e19-e007e7ac045b'::uuid, 'fdc1b6c9-a920-4232-b338-28ea0bd54d63'::uuid, 871) -- 2009 Vasco R6: Fernando Prass
                ) AS v(partida, time, jogador)
                WHERE EXISTS (SELECT 1 FROM "LigaPartidas" p WHERE p."PartidaId" = v.partida)
                  AND EXISTS (SELECT 1 FROM "Teams" t WHERE t."TeamId" = v.time)
                  AND EXISTS (SELECT 1 FROM "Players" j WHERE j."PlayerId" = v.jogador)
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaGoleirosPartida");
        }
    }
}
