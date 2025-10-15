using Microsoft.EntityFrameworkCore;
using MyShowtime.Api.Entities;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Media> Media => Set<Media>();
    public DbSet<Episode> Episodes => Set<Episode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Media>(entity =>
        {
            entity.HasIndex(e => e.TmdbId).IsUnique();
            entity.Property(e => e.MediaType)
                  .HasConversion<string>()
                  .HasMaxLength(16);
            entity.Property(e => e.WatchState)
                  .HasConversion<string>()
                  .HasMaxLength(16);
            entity.Property(e => e.CreatedAtUtc)
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Episode>(entity =>
        {
            entity.HasIndex(e => new { e.MediaId, e.SeasonNumber, e.EpisodeNumber })
                  .IsUnique();
            entity.Property(e => e.WatchState)
                  .HasConversion<string>()
                  .HasMaxLength(16);
            entity.Property(e => e.CreatedAtUtc)
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
        });
    }
}
