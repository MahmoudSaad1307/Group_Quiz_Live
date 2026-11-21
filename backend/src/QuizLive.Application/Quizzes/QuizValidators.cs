using FluentValidation;
using QuizLive.Domain.Enums;

namespace QuizLive.Application.Quizzes;

public sealed class CreateQuizRequestValidator : AbstractValidator<CreateQuizRequest>
{
    public CreateQuizRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Language).IsInEnum();
    }
}

public sealed class UpdateQuizRequestValidator : AbstractValidator<UpdateQuizRequest>
{
    public UpdateQuizRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Language).IsInEnum();
    }
}

public sealed class UpsertQuestionRequestValidator : AbstractValidator<UpsertQuestionRequest>
{
    private static readonly int[] AllowedPoints = [0, 500, 1000, 2000];

    public UpsertQuestionRequestValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .Must(t => t != QuestionType.MultiChoice)
            .WithMessage("MultiChoice questions are not supported yet.");

        RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Explanation).MaximumLength(500);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
        RuleFor(x => x.TimeLimitSeconds).InclusiveBetween(5, 120);
        RuleFor(x => x.Points)
            .Must(p => AllowedPoints.Contains(p))
            .WithMessage("Points must be 0, 500, 1000, or 2000.");

        RuleFor(x => x.Options).NotNull();
        RuleForEach(x => x.Options).ChildRules(opt =>
        {
            opt.RuleFor(o => o.Text).NotEmpty().MaximumLength(120);
        });

        RuleFor(x => x)
            .Must(x => x.Type == QuestionType.TrueFalse ? x.Options.Count == 2 : x.Options.Count is >= 2 and <= 4)
            .WithMessage("TrueFalse must have 2 options; SingleChoice must have 2 to 4 options.");

        RuleFor(x => x.Options)
            .Must(opts => opts.Count(o => o.IsCorrect) == 1)
            .WithMessage("Exactly one option must be marked as correct.");
    }
}

public sealed class ReorderQuestionsRequestValidator : AbstractValidator<ReorderQuestionsRequest>
{
    public ReorderQuestionsRequestValidator()
    {
        RuleFor(x => x.OrderedIds).NotEmpty();
    }
}
