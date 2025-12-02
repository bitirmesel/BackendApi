using DktApi.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace DktApi.Models.Db;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DB SETLER
    public DbSet<GameType> GameTypes => Set<GameType>();
    public DbSet<DifficultyLevel> DifficultyLevels => Set<DifficultyLevel>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<AssetSet> AssetSets => Set<AssetSet>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Therapist> Therapists => Set<Therapist>();
    public DbSet<TherapistClient> TherapistClients => Set<TherapistClient>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // therapist_clients composite PK
        modelBuilder.Entity<TherapistClient>()
            .HasKey(tc => new { tc.TherapistId, tc.PlayerId });

        modelBuilder.Entity<TherapistClient>()
            .HasOne(tc => tc.Therapist)
            .WithMany(t => t.TherapistClients)
            .HasForeignKey(tc => tc.TherapistId);

        modelBuilder.Entity<TherapistClient>()
            .HasOne(tc => tc.Player)
            .WithMany(p => p.TherapistClients)
            .HasForeignKey(tc => tc.PlayerId);

        // asset_sets: (game_id, letter_id) unique index
        modelBuilder.Entity<AssetSet>()
            .HasIndex(a => new { a.GameId, a.LetterId })
            .IsUnique();

        // TaskItem ilişkileri
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Player)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.PlayerId);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Therapist)
            .WithMany(th => th.Tasks)
            .HasForeignKey(t => t.TherapistId);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Game)
            .WithMany(g => g.Tasks)
            .HasForeignKey(t => t.GameId);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Letter)
            .WithMany(l => l.Tasks)
            .HasForeignKey(t => t.LetterId);

        // AssetSetId nullable ise (long?) bu ilişki opsiyonel olacak
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.AssetSet)
            .WithMany(a => a.Tasks)
            .HasForeignKey(t => t.AssetSetId);

        // GameSession ilişkileri
        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Player)
            .WithMany(p => p.GameSessions)
            .HasForeignKey(gs => gs.PlayerId);

        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Game)
            .WithMany(g => g.GameSessions)
            .HasForeignKey(gs => gs.GameId);

        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Letter)
            .WithMany(l => l.GameSessions)
            .HasForeignKey(gs => gs.LetterId);

        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.AssetSet)
            .WithMany(a => a.GameSessions)
            .HasForeignKey(gs => gs.AssetSetId);

        // TaskId de nullable (long?) ise optional ilişki
        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Task)
            .WithMany(t => t.GameSessions)
            .HasForeignKey(gs => gs.TaskId);

        // Feedback ilişkileri
        modelBuilder.Entity<Feedback>()
            .HasOne(f => f.Therapist)
            .WithMany(t => t.Feedbacks)
            .HasForeignKey(f => f.TherapistId);

        modelBuilder.Entity<Feedback>()
            .HasOne(f => f.GameSession)
            .WithMany(gs => gs.Feedbacks)
            .HasForeignKey(f => f.GameSessionId);
    }
}
