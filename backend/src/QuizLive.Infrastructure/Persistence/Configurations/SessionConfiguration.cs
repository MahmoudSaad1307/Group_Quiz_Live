using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizLive.Domain.Entities;
using QuizLive.Domain.ValueObjects;

namespace QuizLive.Infrastructure.Persistence.Configurations;

public sealed class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.ToTable("game_sessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Pin).HasMaxLength(6).IsRequired();
        builder.Property(x => x.Language).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.PausedFromStatus).HasConversion<string>().HasMaxLength(24);

        builder.Property(x => x.Settings)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<SessionSettings>(v, JsonOptions) ?? new SessionSettings());

        builder.Property(x => x.QuestionOrder)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<Guid>>(v, JsonOptions) ?? new List<Guid>(),
                new ValueComparer<List<Guid>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

        builder.Property(x => x.OptionOrder)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(v, JsonOptions) ?? new Dictionary<Guid, List<Guid>>(),
                new ValueComparer<Dictionary<Guid, List<Guid>>>(
                    (a, b) => a != null && b != null && a.Count == b.Count,
                    c => c.Count,
                    c => c.ToDictionary(k => k.Key, k => k.Value.ToList())));

        builder.HasOne<Quiz>()
            .WithMany()
            .HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Admin>()
            .WithMany()
            .HasForeignKey(x => x.HostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Players)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Players).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Pin)
            .IsUnique()
            .HasFilter("\"Status\" NOT IN ('Finished', 'Abandoned')");
    }
}

public sealed class SessionQuestionConfiguration : IEntityTypeConfiguration<SessionQuestion>
{
    public void Configure(EntityTypeBuilder<SessionQuestion> builder)
    {
        builder.ToTable("session_questions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.Explanation).HasMaxLength(500);

        builder.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.SessionQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.SessionId, x.OrderIndex });
    }
}

public sealed class SessionOptionConfiguration : IEntityTypeConfiguration<SessionOption>
{
    public void Configure(EntityTypeBuilder<SessionOption> builder)
    {
        builder.ToTable("session_options");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.SessionQuestionId, x.OrderIndex });
    }
}

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nickname).HasMaxLength(40).IsRequired();
        builder.Property(x => x.NicknameNormalized).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(x => new { x.SessionId, x.NicknameNormalized }).IsUnique();
        builder.HasIndex(x => new { x.SessionId, x.TokenHash });
    }
}
