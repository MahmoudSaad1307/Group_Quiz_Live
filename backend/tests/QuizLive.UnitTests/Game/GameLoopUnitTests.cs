using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using QuizLive.Application.Game;
using QuizLive.Domain.Enums;

namespace QuizLive.UnitTests.Game;

public sealed class GameLoopUnitTests
{
    private static readonly JsonSerializerOptions HubJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    [Theory]
    [InlineData(true, 0, 20000, 1000, 1, false, 1000)]
    [InlineData(true, 20000, 20000, 1000, 1, false, 500)]
    [InlineData(true, 10000, 20000, 1000, 1, false, 750)]
    [InlineData(false, 500, 20000, 1000, 0, true, 0)]
    [InlineData(true, 0, 20000, 0, 3, true, 0)] // Poll question: 0 pts, no streak bonus
    [InlineData(true, 0, 20000, 1000, 2, true, 1200)] // Streak 2 -> +200
    [InlineData(true, 0, 20000, 1000, 7, true, 1500)] // Streak capped at 5 -> +500
    [InlineData(true, -50, 20000, 1000, 1, false, 1000)] // Negative clamped
    [InlineData(true, 25000, 20000, 1000, 1, false, 500)] // Over duration clamped
    public void ScoringCalculator_ComputesExpectedPoints(
        bool correct,
        int rtMs,
        int durationMs,
        int maxPoints,
        int streak,
        bool streakBonus,
        int expected)
    {
        ScoringCalculator.CalculatePoints(correct, rtMs, durationMs, maxPoints, streak, streakBonus)
            .Should().Be(expected);
    }

    [Fact]
    public void AnswerWindow_AcceptsWithin500msGrace_RejectsAfter()
    {
        AnswerWindow.Evaluate(20_500, 0, 20_000).Should().Be(new AnswerWindowEvaluation(true, 20_000));
        AnswerWindow.Evaluate(20_501, 0, 20_000).Accepted.Should().BeFalse();
    }

    [Fact]
    public void StateMachine_AllowsLegalTransitions_AndRejectsIllegal()
    {
        SessionStateMachine.Transition(SessionStatus.Lobby, SessionTrigger.Start)
            .Should().Be(SessionStatus.QuestionOpen);
        SessionStateMachine.Transition(SessionStatus.QuestionOpen, SessionTrigger.Pause)
            .Should().Be(SessionStatus.Paused);
        SessionStateMachine.Transition(SessionStatus.Paused, SessionTrigger.Resume)
            .Should().Be(SessionStatus.QuestionOpen);
        SessionStateMachine.Transition(SessionStatus.QuestionOpen, SessionTrigger.CloseQuestion)
            .Should().Be(SessionStatus.QuestionClosed);
        SessionStateMachine.Transition(SessionStatus.QuestionClosed, SessionTrigger.ShowLeaderboard)
            .Should().Be(SessionStatus.Leaderboard);
        SessionStateMachine.Transition(SessionStatus.Leaderboard, SessionTrigger.Finish)
            .Should().Be(SessionStatus.Finished);

        var illegal = () => SessionStateMachine.Transition(SessionStatus.Lobby, SessionTrigger.Finish);
        illegal.Should().Throw<InvalidSessionTransitionException>();

        var fromFinal = () => SessionStateMachine.Transition(SessionStatus.Finished, SessionTrigger.Abandon);
        fromFinal.Should().Throw<InvalidSessionTransitionException>();
    }

    [Fact]
    public void LeaderboardRanker_UsesCompetitionRanking_AndTieBreaksByCumulativeRtThenJoinTime()
    {
        var t0 = DateTimeOffset.UtcNow;
        var p1 = new RankablePlayer(Guid.NewGuid(), "Fast", 1000, 2000, t0.AddSeconds(2), 1, 1000);
        var p2 = new RankablePlayer(Guid.NewGuid(), "Slow", 1000, 5000, t0.AddSeconds(1), 1, 1000);
        var p3 = new RankablePlayer(Guid.NewGuid(), "Third", 500, 1000, t0, 1, 500);

        var ranked = LeaderboardRanker.Rank([p2, p3, p1]);

        ranked[0].Nickname.Should().Be("Fast");
        ranked[0].Rank.Should().Be(1);
        ranked[1].Nickname.Should().Be("Slow");
        ranked[1].Rank.Should().Be(1);
        ranked[2].Nickname.Should().Be("Third");
        ranked[2].Rank.Should().Be(3);
        ranked[2].GapToNext.Should().Be(500);
    }

    [Fact]
    public void QuestionOpenedPayload_SerializedJson_ContainsNoAnswerLeakingFields()
    {
        var payload = new QuestionOpenedPayload(
            0,
            5,
            Guid.NewGuid(),
            "Capital of France?",
            null,
            [new SanitizedOptionDto(Guid.NewGuid(), "Paris"), new SanitizedOptionDto(Guid.NewGuid(), "Rome")],
            20000,
            1000,
            1000,
            true);

        var json = JsonSerializer.Serialize(payload, HubJsonOptions);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        foreach (var prop in root.EnumerateObject())
        {
            prop.Name.Should().NotContainEquivalentOf("correct");
            prop.Name.Should().NotContainEquivalentOf("explanation");
            prop.Name.Should().NotContainEquivalentOf("answer");
        }

        foreach (var opt in root.GetProperty("options").EnumerateArray())
        {
            opt.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "text"]);
        }
    }
}
