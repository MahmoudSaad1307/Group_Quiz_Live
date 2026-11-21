using QuizLive.Domain.Enums;
using QuizLive.Domain.ValueObjects;

namespace QuizLive.Domain.Entities;

public sealed class GameSession
{
    private readonly List<SessionQuestion> _questions = [];
    private readonly List<Player> _players = [];

    private GameSession() { }

    public GameSession(
        Guid id,
        Guid quizId,
        Guid hostId,
        string pin,
        QuizLanguage language,
        SessionSettings settings,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        QuizId = quizId;
        HostId = hostId;
        Pin = pin;
        Language = language;
        Status = SessionStatus.Lobby;
        CurrentQuestionIndex = 0;
        Settings = settings;
        CreatedAt = createdAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid QuizId { get; private set; }
    public Guid HostId { get; private set; }
    public string Pin { get; private set; } = string.Empty;
    public QuizLanguage Language { get; private set; }
    public SessionStatus Status { get; private set; }
    public SessionStatus? PausedFromStatus { get; private set; }
    public int? PausedRemainingMs { get; private set; }
    public int CurrentQuestionIndex { get; private set; }
    public SessionSettings Settings { get; private set; } = new();
    public List<Guid> QuestionOrder { get; private set; } = [];
    public Dictionary<Guid, List<Guid>> OptionOrder { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public IReadOnlyCollection<SessionQuestion> Questions => _questions;
    public IReadOnlyCollection<Player> Players => _players;

    public void SnapshotQuiz(Quiz quiz, Random rng)
    {
        var sourceQuestions = quiz.Questions.OrderBy(q => q.OrderIndex).ToList();
        var orderedQuestions = Settings.ShuffleQuestions
            ? [.. sourceQuestions.OrderBy(_ => rng.Next())]
            : sourceQuestions;

        QuestionOrder = [];
        OptionOrder = [];

        for (var qi = 0; qi < orderedQuestions.Count; qi++)
        {
            var srcQ = orderedQuestions[qi];
            var sq = new SessionQuestion(
                Guid.NewGuid(),
                Id,
                srcQ.Id,
                qi,
                srcQ.Type,
                srcQ.Text,
                srcQ.ImageUrl,
                srcQ.Explanation,
                srcQ.TimeLimitSeconds,
                srcQ.Points);

            var srcOpts = srcQ.Options.OrderBy(o => o.OrderIndex).ToList();
            var orderedOpts = Settings.ShuffleOptions
                ? [.. srcOpts.OrderBy(_ => rng.Next())]
                : srcOpts;

            var optIds = new List<Guid>(orderedOpts.Count);
            for (var oi = 0; oi < orderedOpts.Count; oi++)
            {
                var so = new SessionOption(
                    Guid.NewGuid(),
                    sq.Id,
                    oi,
                    orderedOpts[oi].Text,
                    orderedOpts[oi].IsCorrect);
                sq.AddOption(so);
                optIds.Add(so.Id);
            }

            _questions.Add(sq);
            QuestionOrder.Add(sq.Id);
            OptionOrder[sq.Id] = optIds;
        }
    }

    public void SetState(SessionStatus status, int questionIndex, DateTimeOffset now, SessionStatus? pausedFrom = null, int? pausedRemainingMs = null)
    {
        Status = status;
        CurrentQuestionIndex = questionIndex;
        PausedFromStatus = pausedFrom;
        PausedRemainingMs = pausedRemainingMs;

        if (status == SessionStatus.QuestionOpen && StartedAt is null)
        {
            StartedAt = now.ToUniversalTime();
        }

        if (status is SessionStatus.Finished or SessionStatus.Abandoned)
        {
            FinishedAt = now.ToUniversalTime();
        }
    }
}
