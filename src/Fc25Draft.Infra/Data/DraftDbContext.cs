using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

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
    public DbSet<MarketCycle> MarketCycles => Set<MarketCycle>();
    public DbSet<MarketItem> MarketItems => Set<MarketItem>();
    public DbSet<MarketBid> MarketBids => Set<MarketBid>();
    public DbSet<MarketTransaction> MarketTransactions => Set<MarketTransaction>();
    public DbSet<TransferHistory> TransferHistories => Set<TransferHistory>();
    public DbSet<BudgetLedger> BudgetLedgers => Set<BudgetLedger>();
    public DbSet<AdminActionsLog> AdminActionsLogs => Set<AdminActionsLog>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<SeasonScheduleItem> SeasonSchedule => Set<SeasonScheduleItem>();
    public DbSet<RoundSelection> RoundSelections => Set<RoundSelection>();
    public DbSet<RoundSelectionPlayer> RoundSelectionPlayers => Set<RoundSelectionPlayer>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasPostgresExtension("unaccent");

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
            e.Property(x => x.PlayerGuid)
             .IsRequired();
            e.HasOne(x => x.Position)
             .WithMany(p => p.Players)
             .HasForeignKey(x => x.PositionId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Name, x.PositionId });
            e.HasIndex(x => x.PlayerGuid).IsUnique();
            e.HasOne(p => p.CurrentTeam)
             .WithMany()
             .HasForeignKey(p => p.CurrentTeamId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Team>(e =>
        {
            e.HasKey(x => x.TeamId);
            e.Property(x => x.TeamName).IsRequired().HasMaxLength(80);
            e.Property(x => x.OwnerName).HasMaxLength(80);
            e.Property(x => x.Token).IsRequired().HasMaxLength(80);
            e.Property(x => x.Budget).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            e.Property(x => x.BudgetBlocked).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            e.HasIndex(x => x.TeamName).IsUnique();
            e.HasIndex(x => x.Token).IsUnique();
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
             .HasFilter("\"PlayerId\" IS NOT NULL");
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

        mb.Entity<RoundSelection>(e =>
        {
            e.HasKey(x => x.RoundSelectionId);

            e.Property(x => x.CreatedAt)
             .IsRequired();

            e.HasIndex(x => x.RoundId)
             .IsUnique();

            e.HasOne(x => x.Round)
             .WithOne(r => r.Selection)
             .HasForeignKey<RoundSelection>(x => x.RoundId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RoundSelectionPlayer>(e =>
        {
            e.HasKey(x => new { x.RoundSelectionId, x.PlayerGuid });

            e.Property(x => x.AddedAt)
             .IsRequired();

            e.Property(x => x.TeamName)
             .HasMaxLength(80);

            e.HasIndex(x => x.RoundSelectionId);
            e.HasIndex(x => x.PlayerGuid);

            e.HasOne(x => x.RoundSelection)
             .WithMany(s => s.Players)
             .HasForeignKey(x => x.RoundSelectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Player)
             .WithMany(p => p.RoundSelections)
             .HasForeignKey(x => x.PlayerGuid)
             .HasPrincipalKey(p => p.PlayerGuid)
             .OnDelete(DeleteBehavior.Cascade);
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

        mb.Entity<AdminActionsLog>(e =>
        {
            e.ToTable("AdminActionsLog");
            e.HasKey(x => x.ActionId);

            e.Property(x => x.ActionId)
             .ValueGeneratedNever();

            e.Property(x => x.ActionType)
             .IsRequired();

            e.Property(x => x.PerformedBy)
             .IsRequired()
             .HasMaxLength(120);

            e.Property(x => x.PayloadJson)
             .IsRequired();

            e.Property(x => x.CreatedAtUtc)
             .IsRequired();

            e.HasIndex(x => new { x.ActionType, x.CreatedAtUtc })
             .IsDescending(false, true)
             .HasDatabaseName("IX_AdminActionsLog_ActionType_CreatedAtUtc");
        });

        mb.Entity<MarketCycle>(e =>
        {
            e.HasKey(x => x.CycleId);

            e.Property(x => x.Name)
             .IsRequired()
             .HasMaxLength(120);

            e.Property(x => x.Status)
             .HasConversion<int>()
             .IsRequired();

            e.Property(x => x.StartsAtUtc)
             .HasColumnType("timestamp with time zone")
             .IsRequired();

            e.Property(x => x.EndsAtUtc)
             .HasColumnType("timestamp with time zone")
             .IsRequired();

            e.Property(x => x.Notes)
             .HasMaxLength(500);

            e.Property(x => x.CreatedAtUtc)
             .HasColumnType("timestamp with time zone")
             .IsRequired();

            e.Property(x => x.UpdatedAtUtc)
             .HasColumnType("timestamp with time zone")
             .IsRequired();

            e.HasMany(c => c.Items)
             .WithOne(i => i.Cycle)
             .HasForeignKey(i => i.CycleId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<MarketItem>(e =>
        {
            e.HasKey(x => x.ItemId);

            e.Property(x => x.BasePrice).HasColumnType("numeric(18,2)").IsRequired();
            e.Property(x => x.BuyNowPrice).HasColumnType("numeric(18,2)").IsRequired(false);
            e.Property(x => x.MinIncrement).HasColumnType("numeric(18,2)").IsRequired();
            e.Property(x => x.CurrentLeaderAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Status).HasConversion<int>().IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
            e.Property(x => x.PublishedAtUtc).HasColumnType("timestamp with time zone");
            e.Property(x => x.LastUpdateUtc).HasColumnType("timestamp with time zone").IsRequired();
            e.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone").IsRequired();
            e.Property(x => x.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate();

            e.HasIndex(x => new { x.CycleId, x.PlayerId }).IsUnique();
            e.HasIndex(x => new { x.CycleId, x.Status, x.ExpiresAtUtc });
            e.HasIndex(x => x.PlayerId).HasDatabaseName("IX_MarketItems_Player");

            e.HasOne(x => x.Player)
             .WithMany(p => p.MarketItems)
             .HasForeignKey(x => x.PlayerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CurrentLeaderTeam)
             .WithMany(t => t.LeadingMarketItems)
             .HasForeignKey(x => x.CurrentLeaderTeamId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.WinnerTeam)
             .WithMany(t => t.WonMarketItems)
             .HasForeignKey(x => x.WinnerTeamId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MarketBid>(e =>
        {
            e.HasKey(x => x.BidId);
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.HasIndex(x => new { x.ItemId, x.CreatedAtUtc });
            e.HasIndex(x => x.TeamId);

            e.HasOne(x => x.Item)
             .WithMany(i => i.Bids)
             .HasForeignKey(x => x.ItemId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Team)
             .WithMany(t => t.MarketBids)
             .HasForeignKey(x => x.TeamId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MarketTransaction>(e =>
        {
            e.HasKey(x => x.TransactionId);

            e.Property(x => x.Type)
             .HasConversion<int>()
             .IsRequired();

            e.Property(x => x.Amount)
             .HasColumnType("numeric(18,2)");

            e.Property(x => x.PerformedBy)
             .IsRequired()
             .HasMaxLength(120);

            e.Property(x => x.Notes)
             .HasMaxLength(400);

            e.Property(x => x.CreatedAtUtc)
             .HasColumnType("timestamp with time zone")
             .IsRequired();

            e.Property(x => x.RowVersion)
             .HasColumnName("xmin")
             .HasColumnType("xid")
             .IsConcurrencyToken()
             .ValueGeneratedOnAddOrUpdate();

            e.HasIndex(x => x.ItemId)
             .HasDatabaseName("IX_MarketTransactions_Item");

            e.HasIndex(x => x.CycleId)
             .HasDatabaseName("IX_MarketTransactions_Cycle");

            e.HasIndex(x => x.PlayerId)
             .HasDatabaseName("IX_MarketTransactions_Player");

            e.HasIndex(x => new { x.ItemId, x.Type, x.CreatedAtUtc })
             .HasDatabaseName("IX_MarketTransactions_Item_Type_CreatedAt");

            e.HasIndex(x => new { x.CycleId, x.Type, x.CreatedAtUtc })
             .HasDatabaseName("IX_MarketTransactions_Cycle_Type_CreatedAt");

            e.HasIndex(x => new { x.PlayerId, x.CreatedAtUtc })
             .HasDatabaseName("IX_MarketTransactions_Player_CreatedAt");
        });

        mb.Entity<TransferHistory>(e =>
        {
            e.HasKey(x => x.TransferId);

            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Notes).HasMaxLength(400);
            e.Property(x => x.PerformedBy).HasMaxLength(120);
            e.Property(x => x.PerformedAtUtc).IsRequired();

            e.HasIndex(x => x.PerformedAtUtc);
            e.HasIndex(x => new { x.PlayerId, x.PerformedAtUtc });
            e.HasIndex(x => x.FromTeamId);
            e.HasIndex(x => x.ToTeamId);

            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            e.HasOne(x => x.FromTeam)
                .WithMany()
                .HasForeignKey(x => x.FromTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.ToTeam)
                .WithMany()
                .HasForeignKey(x => x.ToTeamId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Season>(e =>
        {
            e.ToTable("Seasons");
            e.HasKey(x => x.SeasonId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);

            e.HasMany(x => x.Competitions)
                .WithOne(x => x.Season)
                .HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Schedule)
                .WithOne(x => x.Season)
                .HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Competition>(e =>
        {
            e.ToTable("Competitions");
            e.HasKey(x => x.CompetitionId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => new { x.SeasonId, x.Name }).IsUnique();
        });

        mb.Entity<Round>(e =>
        {
            e.ToTable("Rounds");
            e.HasKey(x => x.RoundId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(400);

            e.HasOne(x => x.Competition)
                .WithMany(x => x.Rounds)
                .HasForeignKey(x => x.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.CompetitionId, x.Name }).IsUnique();
        });

        mb.Entity<SeasonScheduleItem>(e =>
        {
            e.ToTable("SeasonSchedule");
            e.HasKey(x => x.SeasonScheduleItemId);

            e.HasOne(x => x.Season)
                .WithMany(x => x.Schedule)
                .HasForeignKey(x => x.SeasonId);

            e.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.SeasonId, x.Order }).IsUnique();
        });

        //mb.ApplyConfigurationsFromAssembly(typeof(DraftDbContext).Assembly);
    }
}
