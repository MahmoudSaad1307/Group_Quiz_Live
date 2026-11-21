using Microsoft.EntityFrameworkCore;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Game;
using QuizLive.Application.Results;
using QuizLive.Infrastructure.Persistence;

namespace QuizLive.Infrastructure.Export;

public sealed class SessionResultsService(QuizLiveDbContext db) : ISessionResultsService
{
    public async Task<SessionResultsDto?> GetResultsAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .AsNoTracking()
            .Include(s => s.Questions)
            .ThenInclude(q => q.Options)
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.HostId == hostId, ct);

        if (session is null) return null;

        var answers = await db.Answers
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .ToListAsync(ct);

        var activePlayers = session.Players.Where(p => !p.IsKicked).ToList();
        var ranked = LeaderboardRanker.Rank(activePlayers.Select(p =>
            new RankablePlayer(p.Id, p.Nickname, p.TotalScore, p.CumulativeResponseTimeMs, p.JoinedAt, 1, 0)));

        var answersByPlayer = answers.GroupBy(a => a.PlayerId).ToDictionary(g => g.Key, g => g.ToList());
        var studentRows = ranked.Select(r =>
        {
            var pAnswers = answersByPlayer.GetValueOrDefault(r.PlayerId, []);
            var correctCount = pAnswers.Count(a => a.IsCorrect);
            var answered = pAnswers.Where(a => a.SelectedOptionId != null).ToList();
            var avgRt = answered.Count == 0 ? 0 : answered.Average(a => a.ResponseTimeMs);
            return new StudentResultRowDto(r.PlayerId, r.Nickname, r.Rank, r.TotalScore, correctCount, Math.Round(avgRt, 1));
        }).ToList();

        var answersByQuestion = answers.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.ToList());
        var questionStats = session.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q =>
            {
                var qAnswers = answersByQuestion.GetValueOrDefault(q.Id, []);
                var total = qAnswers.Count;
                var correctPct = total == 0 ? 0 : Math.Round(qAnswers.Count(a => a.IsCorrect) * 100.0 / total, 1);
                var answered = qAnswers.Where(a => a.SelectedOptionId != null).ToList();
                var avgRt = answered.Count == 0 ? 0 : Math.Round(answered.Average(a => a.ResponseTimeMs), 1);

                var optCounts = qAnswers
                    .Where(a => a.SelectedOptionId.HasValue)
                    .GroupBy(a => a.SelectedOptionId!.Value)
                    .ToDictionary(g => g.Key, g => g.Count());

                var opts = q.Options.OrderBy(o => o.OrderIndex)
                    .Select(o => new OptionStatDto(o.Id, o.Text, o.IsCorrect, optCounts.GetValueOrDefault(o.Id, 0)))
                    .ToList();

                return new QuestionStatDto(q.Id, q.OrderIndex, q.Text, correctPct, avgRt, opts);
            }).ToList();

        return new SessionResultsDto(
            session.Id,
            session.Pin,
            session.Language,
            session.Status,
            session.StartedAt,
            session.FinishedAt,
            studentRows,
            questionStats);
    }
}
