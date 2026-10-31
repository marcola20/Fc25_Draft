using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PickDoDraftConfiguration : IEntityTypeConfiguration<PickDoDraft>
{
    public void Configure(EntityTypeBuilder<PickDoDraft> b)
    {
        b.ToTable("PicksDoDraft");
        b.HasKey(x => x.PickDoDraftId);
        b.HasIndex(x => new { x.Temporada, x.Rodada, x.TimeOriginalId }).IsUnique();
        b.HasIndex(x => x.DonoId);

        b.HasOne(x => x.TimeOriginal).WithMany().HasForeignKey(x => x.TimeOriginalId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Dono).WithMany().HasForeignKey(x => x.DonoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransferOfferPickConfiguration : IEntityTypeConfiguration<TransferOfferPick>
{
    public void Configure(EntityTypeBuilder<TransferOfferPick> b)
    {
        b.ToTable("TransferOfferPicks");
        b.HasKey(x => new { x.OfferId, x.Temporada, x.Rodada, x.TimeOriginalId });

        b.HasOne(x => x.Offer).WithMany(o => o.Picks).HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TimeOriginal).WithMany().HasForeignKey(x => x.TimeOriginalId).OnDelete(DeleteBehavior.Cascade);
    }
}
