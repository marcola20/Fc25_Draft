using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fc25Draft.Tests;

public class ContadorJogosTests
{
    private static readonly Guid CasaId = Guid.NewGuid();
    private static readonly Guid ForaId = Guid.NewGuid();

    // Casa: 1 e 2 titulares, 3 e 4 no banco (só o 3 entra). Fora: 11 titular, 12 no banco.
    // Todos são goleiros (posição 1), para exercitar o clean sheet.
    private const int Titular1 = 1, Titular2 = 2, Reserva = 3, ReservaNaoEntrou = 4, TitularFora = 11, ReservaFora = 12;

    [Fact]
    public async Task EncerrarPartida_ContaTitularesESubstitutos_ECompeticaoAntigaFicaSemContagem()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DraftDbContext>().UseSqlite(connection).Options;

        Guid ligaAntigaId = Guid.NewGuid(), ligaNovaId = Guid.NewGuid();
        Guid partidaAntigaId = Guid.NewGuid(), partidaNovaId = Guid.NewGuid();

        await using (var db = new DraftDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            Semear(db);

            // Competição antiga: partida já encerrada, com gol, sem retrato de titulares.
            db.Ligas.Add(NovaLiga(ligaAntigaId, "Antiga", partidaAntigaId, PartidaStatus.Encerrada));
            db.LigaEventos.Add(new LigaEventoPartida
            {
                EventoId = Guid.NewGuid(), PartidaId = partidaAntigaId, Tipo = TipoEvento.Gol,
                TimeId = CasaId, JogadorId = Titular1, CriadoEm = DateTime.UtcNow
            });
            db.Ligas.Add(NovaLiga(ligaNovaId, "Nova", partidaNovaId, PartidaStatus.EmAndamento));
            await db.SaveChangesAsync();
        }

        await using (var db = new DraftDbContext(options))
        {
            var admin = new LigaAdminService(db);
            await admin.AddGolAsync(partidaNovaId, new LigaGolRequest(CasaId, Titular1, null, 10), default);
            // Titular não pode "entrar"; reserva não pode "sair" sem estar em campo.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                admin.AddSubstituicaoAsync(partidaNovaId, new LigaSubstituicaoRequest(CasaId, Titular1, Titular2, 50), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                admin.AddSubstituicaoAsync(partidaNovaId, new LigaSubstituicaoRequest(CasaId, Titular1, Reserva, 50), default));

            await admin.AddSubstituicaoAsync(partidaNovaId, new LigaSubstituicaoRequest(CasaId, Reserva, Titular2, 60), default);

            // Quem saiu não volta, e quem entrou não entra de novo.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                admin.AddSubstituicaoAsync(partidaNovaId, new LigaSubstituicaoRequest(CasaId, Titular2, Reserva, 70), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                admin.AddSubstituicaoAsync(partidaNovaId, new LigaSubstituicaoRequest(CasaId, Reserva, Titular1, 70), default));
            await admin.EncerrarPartidaAsync(partidaNovaId, default);
        }

        await using (var db = new DraftDbContext(options))
        {
            var titulares = await db.LigaEscalacoes.Where(t => t.Titular).Select(t => t.JogadorId).OrderBy(x => x).ToListAsync();
            Assert.Equal(new[] { Titular1, Titular2, TitularFora }, titulares);
            var banco = await db.LigaEscalacoes.Where(t => !t.Titular).Select(t => t.JogadorId).OrderBy(x => x).ToListAsync();
            Assert.Equal(new[] { Reserva, ReservaNaoEntrou, ReservaFora }, banco);

            var publico = new LigaPublicService(db);

            var escalacoes = await publico.GetEscalacoesPartidaAsync(partidaNovaId, default);
            Assert.NotNull(escalacoes);
            var troca = Assert.Single(escalacoes!.Eventos, e => e.Tipo == TipoEvento.Substituicao);
            Assert.Equal("J2", troca.JogadorSaiuNome);
            Assert.Equal(4, escalacoes.Casa.Jogadores.Count);
            var historico = await publico.GetHistoricoArtilheirosAsync(default);
            var artilheiro = Assert.Single(historico, h => h.JogadorId == Titular1);
            Assert.Null(artilheiro.Competicoes.Single(c => c.LigaId == ligaAntigaId).Jogos);
            Assert.Equal(1, artilheiro.Competicoes.Single(c => c.LigaId == ligaNovaId).Jogos);

            var temporada = await publico.GetTemporadaTimeAsync(CasaId, default);
            Assert.NotNull(temporada);
            var jogos = temporada!.Jogadores.ToDictionary(j => j.JogadorId, j => j.Jogos);
            Assert.Equal(1, jogos[Titular1]);
            Assert.Equal(1, jogos[Titular2]);
            Assert.Equal(1, jogos[Reserva]);
            Assert.Equal(0, jogos[ReservaNaoEntrou]);

            // Casa venceu 1 x 0 na partida nova: clean sheet só para quem esteve em campo.
            // A partida antiga (sem escalação, sem gol sofrido) segue a regra antiga e conta para todos.
            var cs = temporada.Jogadores.ToDictionary(j => j.JogadorId, j => j.CleanSheets);
            Assert.Equal(2, cs[Titular1]);
            Assert.Equal(2, cs[Titular2]);
            Assert.Equal(2, cs[Reserva]);
            Assert.Equal(1, cs[ReservaNaoEntrou]);

            var fora = await publico.GetTemporadaTimeAsync(ForaId, default);
            var jogosFora = fora!.Jogadores.ToDictionary(j => j.JogadorId, j => j.Jogos);
            Assert.Equal(1, jogosFora[TitularFora]);
            Assert.Equal(0, jogosFora[ReservaFora]);
        }
    }

    private static void Semear(DraftDbContext db)
    {
        db.Teams.Add(new Team { TeamId = CasaId, TeamName = "Casa", Token = "a" });
        db.Teams.Add(new Team { TeamId = ForaId, TeamName = "Fora", Token = "b" });

        foreach (var (id, time) in new[] { (Titular1, CasaId), (Titular2, CasaId), (Reserva, CasaId), (ReservaNaoEntrou, CasaId), (TitularFora, ForaId), (ReservaFora, ForaId) })
        {
            db.Players.Add(new Player { PlayerId = id, PlayerGuid = Guid.NewGuid(), Name = $"J{id}", Overall = 80, PositionId = 1, CurrentTeamId = time });
            db.TeamRosters.Add(new TeamRoster { TeamId = time, PlayerId = id });
        }

        db.TeamLineups.Add(NovaEscalacao(CasaId, (Titular1, false), (Titular2, false), (Reserva, true), (ReservaNaoEntrou, true)));
        db.TeamLineups.Add(NovaEscalacao(ForaId, (TitularFora, false), (ReservaFora, true)));
    }

    private static TeamLineup NovaEscalacao(Guid timeId, params (int PlayerId, bool Banco)[] slots)
    {
        var lineup = new TeamLineup
        {
            LineupId = Guid.NewGuid(), TeamId = timeId, Name = "Principal", Formation = "4-4-2",
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ordem = 0;
        foreach (var (playerId, banco) in slots)
        {
            lineup.Slots.Add(new TeamLineupSlot
            {
                LineupSlotId = Guid.NewGuid(), SlotCode = $"S{ordem}", DisplayName = $"S{ordem}",
                IsBench = banco, Order = ordem++, PlayerId = playerId
            });
        }
        return lineup;
    }

    private static Liga NovaLiga(Guid ligaId, string nome, Guid partidaId, PartidaStatus status)
    {
        var liga = new Liga
        {
            LigaId = ligaId, Nome = nome, Status = LigaStatus.PrimeiraFase, Tipo = TipoCompetition.Liga,
            DataInicio = DateTime.UtcNow, DataFim = DateTime.UtcNow.AddMonths(1),
            CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow
        };
        var rodada = new LigaRodada { RodadaId = Guid.NewGuid(), LigaId = ligaId, Numero = 1 };
        rodada.Partidas.Add(new LigaPartida
        {
            PartidaId = partidaId, TimeCasaId = CasaId, TimeForaId = ForaId, Status = status,
            IniciadaEm = DateTime.UtcNow
        });
        liga.Rodadas.Add(rodada);
        return liga;
    }
}
