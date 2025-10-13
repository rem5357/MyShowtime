using Microsoft.EntityFrameworkCore;
using MyShowtime.Api.Entities;

namespace MyShowtime.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Show> Shows => Set<Show>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Show>(entity =>
        {
            entity.HasIndex(e => e.TmdbId).IsUnique();
            entity.Property(e => e.CreatedAtUtc)
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
        });
    }
}
