using GitShare.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GitShare.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AnalyzedProfile> AnalyzedProfiles => Set<AnalyzedProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AnalyzedProfile>();

        entity.ToTable("AnalyzedProfiles");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Username).HasMaxLength(256).IsRequired();
        entity.Property(e => e.FullDataJson).IsRequired();
        entity.Property(e => e.AnalyzedAt)
            .IsRequired()
            .HasColumnType(
                Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
                    ? "timestamp with time zone"
                    : "TEXT");
        entity.HasIndex(e => e.Username).IsUnique();
    }
}
