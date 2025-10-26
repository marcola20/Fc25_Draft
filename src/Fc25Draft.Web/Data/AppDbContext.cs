using Fc25Draft.Web.Data.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Position> Positions => Set<Position>();
        public DbSet<Player> Players => Set<Player>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<Draft> Drafts => Set<Draft>();
        public DbSet<DraftRound> DraftRounds => Set<DraftRound>();
        public DbSet<DraftPick> DraftPicks => Set<DraftPick>();
        public DbSet<TeamRoster> TeamRosters => Set<TeamRoster>();

        protected override void OnModelCreating(ModelBuilder model)
        {
            // Position
            model.Entity<Position>()
                .HasKey(p => p.PositionId);
            model.Entity<Position>()
                .HasIndex(p => p.Name).IsUnique();

            // Player
            model.Entity<Player>()
                .HasKey(p => p.PlayerId);
            model.Entity<Player>()
                .HasOne(p => p.Position)
                .WithMany(x => x.Players)
                .HasForeignKey(p => p.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Team
            model.Entity<Team>()
                .HasKey(t => t.TeamId);
            model.Entity<Team>()
                .HasIndex(t => t.TeamToken).IsUnique();

            // Draft
            model.Entity<Draft>()
                .HasKey(d => d.DraftId);

            // DraftRound
            model.Entity<DraftRound>()
                .HasKey(r => new { r.DraftId, r.RoundNumber });
            model.Entity<DraftRound>()
                .HasOne(r => r.Draft)
                .WithMany(d => d.Rounds)
                .HasForeignKey(r => r.DraftId)
                .OnDelete(DeleteBehavior.Cascade);

            // DraftPick
            model.Entity<DraftPick>()
                .HasKey(p => new { p.DraftId, p.OverallPick });

            model.Entity<DraftPick>()
                .HasIndex(p => new { p.DraftId, p.RoundNumber, p.PickInRound }).IsUnique();

            model.Entity<DraftPick>()
                .HasIndex(p => new { p.DraftId, p.TeamId, p.RoundNumber });

            model.Entity<DraftPick>()
                .HasOne(p => p.Draft)
                .WithMany(d => d.Picks)
                .HasForeignKey(p => p.DraftId)
                .OnDelete(DeleteBehavior.Restrict); 

            model.Entity<DraftPick>()
                .HasOne(p => p.Round)
                .WithMany(r => r.Picks)
                .HasForeignKey(p => new { p.DraftId, p.RoundNumber })
                .OnDelete(DeleteBehavior.Cascade);

            model.Entity<DraftPick>()
                .HasOne(p => p.Team)
                .WithMany(t => t.DraftPicks)
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            model.Entity<DraftPick>()
                .HasOne(p => p.Player)
                .WithMany(pl => pl.DraftPicks)
                .HasForeignKey(p => p.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            model.Entity<TeamRoster>()
                .HasKey(r => new { r.TeamId, r.PlayerId });
            model.Entity<TeamRoster>()
                .HasIndex(r => r.PlayerId).IsUnique();

            model.Entity<TeamRoster>()
                .HasOne(r => r.Team)
                .WithMany(t => t.Roster)
                .HasForeignKey(r => r.TeamId);

            model.Entity<TeamRoster>()
                .HasOne(r => r.Player)
                .WithMany(p => p.TeamRosters)
                .HasForeignKey(r => r.PlayerId);

            model.Entity<Position>().HasData(
                new Position { PositionId = 1, Name = "Goleiro" },
                new Position { PositionId = 2, Name = "Zagueiro" },
                new Position { PositionId = 3, Name = "Lateral/Ala Esquerdo" },
                new Position { PositionId = 4, Name = "Lateral/Ala Direito" },
                new Position { PositionId = 5, Name = "Volante" },
                new Position { PositionId = 6, Name = "Meia Central" },
                new Position { PositionId = 7, Name = "Meia Atacante" },
                new Position { PositionId = 8, Name = "Ponta/Meia Esquerda" },
                new Position { PositionId = 9, Name = "Ponta/Meia Direita" },
                new Position { PositionId = 10, Name = "Centroavante" }
            );
        }
    }
}
