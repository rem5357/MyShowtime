using Microsoft.EntityFrameworkCore;
using MyShowtime.Api.Entities;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Media> Media => Set<Media>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserMedia> UserMedia => Set<UserMedia>();
    public DbSet<UserEpisode> UserEpisodes => Set<UserEpisode>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Media>(entity =>
        {
            entity.HasIndex(e => e.TmdbId).IsUnique();
            entity.Property(e => e.MediaType)
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
            entity.Property(e => e.CreatedAtUtc)
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_user");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CognitoSub).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CognitoSub).HasColumnName("cognito_sub").IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.CognitoUsername).HasColumnName("cognito_username");
            entity.Property(e => e.Roles).HasColumnName("roles");
            entity.Property(e => e.LastLogin)
                  .HasColumnName("last_login")
                  .HasDefaultValueSql("NOW()");
            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive)
                  .HasColumnName("is_active")
                  .HasDefaultValue(true);
            entity.Property(e => e.Metadata)
                  .HasColumnName("metadata")
                  .HasColumnType("jsonb");
        });

        modelBuilder.Entity<UserMedia>(entity =>
        {
            entity.ToTable("user_media");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.MediaId }).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.MediaId).HasColumnName("media_id").IsRequired();
            entity.Property(e => e.WatchState)
                  .HasColumnName("watch_state")
                  .HasConversion<string>()
                  .HasMaxLength(16);
            entity.Property(e => e.Priority).HasColumnName("priority").HasDefaultValue(3);
            entity.Property(e => e.Hidden).HasColumnName("hidden").HasDefaultValue(false);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.AvailableOn).HasColumnName("available_on");
            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Media)
                  .WithMany()
                  .HasForeignKey(e => e.MediaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserEpisode>(entity =>
        {
            entity.ToTable("user_episode");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.EpisodeId }).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.EpisodeId).HasColumnName("episode_id").IsRequired();
            entity.Property(e => e.WatchState)
                  .HasColumnName("watch_state")
                  .HasConversion<string>()
                  .HasMaxLength(16);
            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Episode)
                  .WithMany()
                  .HasForeignKey(e => e.EpisodeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.ToTable("user_settings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.LastSelectedMediaId).HasColumnName("last_selected_media_id");
            entity.Property(e => e.Preferences)
                  .HasColumnName("preferences")
                  .HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.User)
                  .WithOne()
                  .HasForeignKey<UserSettings>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LastSelectedMedia)
                  .WithMany()
                  .HasForeignKey(e => e.LastSelectedMediaId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
