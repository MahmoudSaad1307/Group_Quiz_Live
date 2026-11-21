using Microsoft.EntityFrameworkCore;
using QuizLive.Domain.Entities;

namespace QuizLive.Infrastructure.Persistence;

public sealed class QuizLiveDbContext(DbContextOptions<QuizLiveDbContext> options) : DbContext(options)
{
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<SessionQuestion> SessionQuestions => Set<SessionQuestion>();
    public DbSet<SessionOption> SessionOptions => Set<SessionOption>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Answer> Answers => Set<Answer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuizLiveDbContext).Assembly);

        // Domain entities generate their own Guid primary keys in constructors
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProp = entityType.FindProperty("Id");
            if (idProp is not null && idProp.ClrType == typeof(Guid))
            {
                idProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }
    }
}
