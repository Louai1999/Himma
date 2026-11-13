using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Himma.Api.Data.Configurations;

public sealed class CoachAccountConfiguration : IEntityTypeConfiguration<CoachAccount>
{
    public void Configure(EntityTypeBuilder<CoachAccount> b)
    {
        b.Property(x => x.BusinessName).HasMaxLength(60).IsRequired();
        b.Property(x => x.Bio).HasMaxLength(300);
        b.Property(x => x.InviteCode).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.InviteCode).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CoachClientConfiguration : IEntityTypeConfiguration<CoachClient>
{
    public void Configure(EntityTypeBuilder<CoachClient> b)
    {
        b.Property(x => x.PrivateNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.CoachUserId, x.Status });
        b.HasIndex(x => new { x.ClientUserId, x.Status });
        // Purging either account removes the link with it.
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CoachUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ClientUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CoachNoteConfiguration : IEntityTypeConfiguration<CoachNote>
{
    public void Configure(EntityTypeBuilder<CoachNote> b)
    {
        b.Property(x => x.Text).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.ClientUserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.CoachUserId, x.ClientUserId });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CoachUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ClientUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
