using Microsoft.EntityFrameworkCore;

namespace DktApi.Models.Db;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Therapist> Therapists => Set<Therapist>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<AssetSet> AssetSets => Set<AssetSet>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Player)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.PlayerId);

        modelBuilder.Entity<Note>()
            .HasOne(n => n.Player)
            .WithMany(p => p.Notes)
            .HasForeignKey(n => n.PlayerId);

        modelBuilder.Entity<Badge>()
            .HasOne(b => b.Player)
            .WithMany(p => p.Badges)
            .HasForeignKey(b => b.PlayerId);

    }
}
