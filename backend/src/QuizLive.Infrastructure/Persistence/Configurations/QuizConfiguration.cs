using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizLive.Domain.Entities;

namespace QuizLive.Infrastructure.Persistence.Configurations;

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Language).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne<Admin>()
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.OwnerId, x.IsArchived });
    }
}

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions", t =>
        {
            t.HasCheckConstraint("ck_questions_time_limit", "\"TimeLimitSeconds\" >= 5 AND \"TimeLimitSeconds\" <= 120");
            t.HasCheckConstraint("ck_questions_points", "\"Points\" IN (0, 500, 1000, 2000)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuizId).IsRequired();
        builder.Property(x => x.OrderIndex).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.Explanation).HasMaxLength(500);
        builder.Property(x => x.TimeLimitSeconds).IsRequired();
        builder.Property(x => x.Points).IsRequired();

        builder.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.QuizId, x.OrderIndex });
    }
}

public sealed class OptionConfiguration : IEntityTypeConfiguration<Option>
{
    public void Configure(EntityTypeBuilder<Option> builder)
    {
        builder.ToTable("options");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuestionId).IsRequired();
        builder.Property(x => x.OrderIndex).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(120).IsRequired();
        builder.Property(x => x.IsCorrect).IsRequired();

        builder.HasIndex(x => new { x.QuestionId, x.OrderIndex });
    }
}
