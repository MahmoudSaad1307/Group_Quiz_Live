namespace QuizLive.Application.Game;

public static class ScoringCalculator
{
    public static int CalculatePoints(
        bool isCorrect,
        int responseTimeMs,
        int durationMs,
        int maxPoints,
        int streakAfterAnswer,
        bool streakBonusEnabled)
    {
        if (!isCorrect || maxPoints <= 0)
        {
            return 0;
        }

        var safeDuration = Math.Max(1, durationMs);
        var clampedRt = Math.Clamp(responseTimeMs, 0, safeDuration);
        var ratio = (double)clampedRt / safeDuration;
        var basePoints = (int)Math.Round(maxPoints * (1.0 - (ratio / 2.0)), MidpointRounding.AwayFromZero);

        var bonus = streakBonusEnabled && streakAfterAnswer >= 2
            ? Math.Min(streakAfterAnswer, 5) * 100
            : 0;

        return basePoints + bonus;
    }
}

public readonly record struct AnswerWindowEvaluation(bool Accepted, int ClampedResponseTimeMs);

public static class AnswerWindow
{
    public const int GraceMs = 500;

    public static AnswerWindowEvaluation Evaluate(long receivedAtMs, long startedAtMs, int durationMs)
    {
        var elapsed = receivedAtMs - startedAtMs;
        if (elapsed > (long)durationMs + GraceMs)
        {
            return new AnswerWindowEvaluation(false, durationMs);
        }

        var clamped = (int)Math.Clamp(elapsed, 0, durationMs);
        return new AnswerWindowEvaluation(true, clamped);
    }
}
