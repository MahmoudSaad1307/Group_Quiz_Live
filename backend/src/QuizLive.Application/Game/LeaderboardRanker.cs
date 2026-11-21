namespace QuizLive.Application.Game;

public sealed record RankablePlayer(
    Guid PlayerId,
    string Nickname,
    int TotalScore,
    long CumulativeResponseTimeMs,
    DateTimeOffset JoinedAt,
    int PreviousRank,
    int LastDelta);

public sealed record RankedPlayer(
    Guid PlayerId,
    string Nickname,
    int TotalScore,
    int Rank,
    int PreviousRank,
    int Delta,
    int? GapToNext,
    string? NextNickname);

public static class LeaderboardRanker
{
    public static IReadOnlyList<RankedPlayer> Rank(IEnumerable<RankablePlayer> players)
    {
        var ordered = players
            .OrderByDescending(p => p.TotalScore)
            .ThenBy(p => p.CumulativeResponseTimeMs)
            .ThenBy(p => p.JoinedAt)
            .ToList();

        var result = new List<RankedPlayer>(ordered.Count);
        var currentRank = 1;

        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            if (i > 0 && ordered[i - 1].TotalScore != p.TotalScore)
            {
                currentRank = i + 1;
            }

            // Nearest player with strictly higher score
            int? gap = null;
            string? nextNickname = null;
            for (var j = i - 1; j >= 0; j--)
            {
                if (ordered[j].TotalScore > p.TotalScore)
                {
                    gap = ordered[j].TotalScore - p.TotalScore;
                    nextNickname = ordered[j].Nickname;
                    break;
                }
            }

            result.Add(new RankedPlayer(
                p.PlayerId,
                p.Nickname,
                p.TotalScore,
                currentRank,
                p.PreviousRank,
                p.LastDelta,
                gap,
                nextNickname));
        }

        return result;
    }
}
