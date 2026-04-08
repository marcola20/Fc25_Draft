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
    public DbSet<TeamLineup> TeamLineups => Set<TeamLineup>();
    public DbSet<TeamLineupSlot> TeamLineupSlots => Set<TeamLineupSlot>();
    public DbSet<TeamLineupOffensiveInstructions> TeamLineupOffensiveInstructions => Set<TeamLineupOffensiveInstructions>();
    public DbSet<TeamLineupDefensiveInstructions> TeamLineupDefensiveInstructions => Set<TeamLineupDefensiveInstructions>();
    public DbSet<AdminToken> AdminTokens => Set<AdminToken>();
    public DbSet<AdminActionsLog> AdminActionsLogs => Set<AdminActionsLog>();
    public DbSet<MarketCycle> MarketCycles => Set<MarketCycle>();
    public DbSet<MarketItem> MarketItems => Set<MarketItem>();
    public DbSet<MarketBid> MarketBids => Set<MarketBid>();
    public DbSet<MarketTransaction> MarketTransactions => Set<MarketTransaction>();
    public DbSet<TransferHistory> TransferHistories => Set<TransferHistory>();
    public DbSet<BudgetLedger> BudgetLedgers => Set<BudgetLedger>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasPostgresExtension("unaccent");
        mb.ApplyConfigurationsFromAssembly(typeof(DraftDbContext).Assembly);
    }
}
