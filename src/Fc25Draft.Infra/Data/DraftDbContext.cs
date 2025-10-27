using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public class DraftDbContext : DbContext
{
    public DraftDbContext(DbContextOptions<DraftDbContext> options) : base(options)
    {
    }
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<DraftRound> DraftRounds => Set<DraftRound>();
    public DbSet<DraftPick> DraftPicks => Set<DraftPick>();
    public DbSet<TeamRoster> TeamRosters => Set<TeamRoster>();
    public DbSet<AdminToken> AdminTokens => Set<AdminToken>();
    public DbSet<TransferMarketItem> TransferMarketItems => Set<TransferMarketItem>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<TeamBudget> TeamBudgets => Set<TeamBudget>();
    public DbSet<TransferHistory> TransferHistories => Set<TransferHistory>();
    public DbSet<BudgetLedger> BudgetLedgers => Set<BudgetLedger>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Position>(e =>
        {
            e.HasKey(x => x.PositionId);

            e.Property(x => x.PositionId)
             .ValueGeneratedNever();

            e.Property(x => x.Name)
             .IsRequired()
             .HasMaxLength(40);

            e.HasIndex(x => x.Name).IsUnique();

            e.HasData(
                new() { PositionId = 1,  Name = "Goleiro" },
                new() { PositionId = 2,  Name = "Zagueiro" },
                new() { PositionId = 3,  Name = "Lateral/Ala Esquerdo" },
                new() { PositionId = 4,  Name = "Lateral/Ala Direito" },
                new() { PositionId = 5,  Name = "Volante" },
                new() { PositionId = 6,  Name = "Meia Central" },
                new() { PositionId = 7,  Name = "Meia Atacante" },
                new() { PositionId = 8,  Name = "Meia/Ponta Esquerda" },
                new() { PositionId = 9,  Name = "Meia/Ponta Direita" },
                new() { PositionId = 10, Name = "Atacante" }
            );
        });

        mb.Entity<Player>(e =>
        {
            e.HasKey(x => x.PlayerId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.Property(x => x.Overall).IsRequired();
            e.Property(x => x.Age);
            e.HasOne(x => x.Position)
             .WithMany(p => p.Players)
             .HasForeignKey(x => x.PositionId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Name, x.PositionId });
        });

        mb.Entity<Team>(e =>
        {
            e.HasKey(x => x.TeamId);
            e.Property(x => x.TeamName).IsRequired().HasMaxLength(80);
            e.Property(x => x.OwnerName).HasMaxLength(80);
            e.Property(x => x.TeamToken).IsRequired();
            e.HasIndex(x => x.TeamName).IsUnique();
            e.HasIndex(x => x.TeamToken).IsUnique();
        });

        mb.Entity<Draft>(e =>
        {
            e.HasKey(x => x.DraftId);
        });

        mb.Entity<DraftRound>(e =>
        {
            e.HasKey(x => new { x.DraftId, x.RoundNumber });
            e.HasOne(x => x.Draft)
             .WithMany(d => d.Rounds)
             .HasForeignKey(x => x.DraftId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DraftPick>(e =>
        {
            e.HasKey(x => new { x.DraftId, x.OverallPick });
            e.HasIndex(x => new { x.DraftId, x.RoundNumber, x.PickInRound }).IsUnique();
            e.HasIndex(x => new { x.DraftId, x.TeamId, x.RoundNumber });
            e.HasIndex(x => x.PlayerId)
             .IsUnique()
             .HasFilter("[PlayerId] IS NOT NULL");
            e.HasOne(x => x.Draft)
             .WithMany(d => d.Picks)
             .HasForeignKey(x => x.DraftId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Round)
             .WithMany(r => r.Picks)
             .HasForeignKey(x => new { x.DraftId, x.RoundNumber })
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Team)
             .WithMany(t => t.DraftPicks)
             .HasForeignKey(x => x.TeamId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Player)
             .WithMany(pl => pl.DraftPicks)
             .HasForeignKey(x => x.PlayerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TeamRoster>(e =>
        {
            e.HasKey(x => new { x.TeamId, x.PlayerId });
            e.HasIndex(x => x.PlayerId).IsUnique();
            e.HasOne(x => x.Team)
             .WithMany(t => t.Roster)
             .HasForeignKey(x => x.TeamId);
            e.HasOne(x => x.Player)
             .WithMany(p => p.TeamRosters)
             .HasForeignKey(x => x.PlayerId);
        });

        mb.Entity<AdminToken>(e =>
        {
            e.ToTable("Token_Administrador");
            e.HasKey(x => x.AdminTokenId);

            e.Property(x => x.AdminTokenId)
             .ValueGeneratedOnAdd();

            e.Property(x => x.Token)
             .IsRequired();

            e.HasIndex(x => x.Token)
             .IsUnique();
        });

        mb.ApplyConfigurationsFromAssembly(typeof(DraftDbContext).Assembly);
    }
}
