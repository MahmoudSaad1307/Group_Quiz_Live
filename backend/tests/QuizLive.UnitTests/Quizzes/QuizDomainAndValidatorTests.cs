using FluentAssertions;
using QuizLive.Application.Quizzes;
using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;

namespace QuizLive.UnitTests.Quizzes;

public sealed class QuizDomainAndValidatorTests
{
    [Fact]
    public void Quiz_AddRemoveReorderDuplicate_WorksCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        var quiz = new Quiz(Guid.NewGuid(), ownerId, "Science 101", "Basics", QuizLanguage.En, now);

        var q1 = quiz.AddQuestion(
            QuestionType.SingleChoice,
            "Water formula?",
            null,
            "H2O",
            20,
            1000,
            [("H2O", true), ("CO2", false)],
            now);

        var q2 = quiz.AddQuestion(
            QuestionType.TrueFalse,
            "The sun is a star.",
            null,
            null,
            15,
            500,
            [("True", true), ("False", false)],
            now);

        q1.OrderIndex.Should().Be(0);
        q2.OrderIndex.Should().Be(1);

        quiz.ReorderQuestions([q2.Id, q1.Id], now);
        q2.OrderIndex.Should().Be(0);
        q1.OrderIndex.Should().Be(1);

        var copy = quiz.Duplicate(ownerId, now);
        copy.Title.Should().Contain("(Copy)");
        copy.Questions.Should().HaveCount(2);
        copy.Questions.First().Id.Should().NotBe(q1.Id);

        quiz.RemoveQuestion(q2.Id, now);
        quiz.Questions.Should().ContainSingle();
        quiz.Questions.Single().OrderIndex.Should().Be(0);
    }

    [Fact]
    public void Quiz_ReorderWithInvalidPermutation_Throws()
    {
        var now = DateTimeOffset.UtcNow;
        var quiz = new Quiz(Guid.NewGuid(), Guid.NewGuid(), "Quiz", null, QuizLanguage.En, now);
        var q1 = quiz.AddQuestion(QuestionType.TrueFalse, "Q1", null, null, 20, 1000, [("T", true), ("F", false)], now);

        var act = () => quiz.ReorderQuestions([q1.Id, Guid.NewGuid()], now);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(4, 1000, false)]
    [InlineData(5, 1000, true)]
    [InlineData(120, 0, true)]
    [InlineData(121, 1000, false)]
    [InlineData(20, 750, false)]
    [InlineData(20, 2000, true)]
    public void UpsertQuestionValidator_ValidatesTimeAndPoints(int timeLimit, int points, bool expectedValid)
    {
        var validator = new UpsertQuestionRequestValidator();
        var req = new UpsertQuestionRequest(
            QuestionType.SingleChoice,
            "Sample?",
            null,
            null,
            timeLimit,
            points,
            [new("A", true), new("B", false)]);

        validator.Validate(req).IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void UpsertQuestionValidator_RequiresExactlyOneCorrectOption()
    {
        var validator = new UpsertQuestionRequestValidator();
        var req = new UpsertQuestionRequest(
            QuestionType.SingleChoice,
            "Sample?",
            null,
            null,
            20,
            1000,
            [new("A", true), new("B", true)]);

        validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpsertQuestionValidator_TrueFalseRequiresExactlyTwoOptions()
    {
        var validator = new UpsertQuestionRequestValidator();
        var req = new UpsertQuestionRequest(
            QuestionType.TrueFalse,
            "Sample?",
            null,
            null,
            20,
            1000,
            [new("A", true), new("B", false), new("C", false)]);

        validator.Validate(req).IsValid.Should().BeFalse();
    }
}
