using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;

namespace QuizLive.Application.Quizzes;

public sealed class QuizService(IQuizRepository quizzes, IClock clock)
{
    public async Task<PagedResult<QuizSummaryDto>> ListAsync(
        Guid ownerId,
        string? search,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var (items, total) = await quizzes.ListByOwnerAsync(ownerId, search, includeArchived, safePage, safeSize, ct);

        return new PagedResult<QuizSummaryDto>(
            items.Select(q => new QuizSummaryDto(
                q.Id,
                q.Title,
                q.Description,
                q.Language,
                q.IsArchived,
                q.Questions.Count,
                q.CreatedAt,
                q.UpdatedAt)).ToList(),
            total,
            safePage,
            safeSize);
    }

    public async Task<QuizDetailDto> CreateAsync(Guid ownerId, CreateQuizRequest request, CancellationToken ct = default)
    {
        var quiz = new Quiz(Guid.NewGuid(), ownerId, request.Title, request.Description, request.Language, clock.UtcNow);
        await quizzes.AddAsync(quiz, ct);
        return ToDetailDto(quiz);
    }

    public async Task<QuizDetailDto?> GetAsync(Guid quizId, Guid ownerId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        return quiz is null ? null : ToDetailDto(quiz);
    }

    public async Task<QuizDetailDto?> UpdateAsync(Guid quizId, Guid ownerId, UpdateQuizRequest request, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return null;

        quiz.UpdateMetadata(request.Title, request.Description, request.Language, clock.UtcNow);
        if (request.IsArchived.HasValue)
        {
            quiz.SetArchived(request.IsArchived.Value, clock.UtcNow);
        }

        await quizzes.SaveChangesAsync(ct);
        return ToDetailDto(quiz);
    }

    public async Task<bool> DeleteOrArchiveAsync(Guid quizId, Guid ownerId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return false;

        if (await quizzes.HasSessionsAsync(quizId, ct))
        {
            quiz.SetArchived(true, clock.UtcNow);
            await quizzes.SaveChangesAsync(ct);
        }
        else
        {
            await quizzes.DeleteAsync(quiz, ct);
        }

        return true;
    }

    public async Task<QuizDetailDto?> DuplicateAsync(Guid quizId, Guid ownerId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return null;

        var copy = quiz.Duplicate(ownerId, clock.UtcNow);
        await quizzes.AddAsync(copy, ct);
        return ToDetailDto(copy);
    }

    public async Task<QuestionDto?> AddQuestionAsync(Guid quizId, Guid ownerId, UpsertQuestionRequest request, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return null;

        var question = quiz.AddQuestion(
            request.Type,
            request.Text,
            request.ImageUrl,
            request.Explanation,
            request.TimeLimitSeconds,
            request.Points,
            request.Options.Select(o => (o.Text, o.IsCorrect)),
            clock.UtcNow);

        await quizzes.SaveChangesAsync(ct);
        return ToQuestionDto(question);
    }

    public async Task<QuestionDto?> UpdateQuestionAsync(
        Guid quizId,
        Guid questionId,
        Guid ownerId,
        UpsertQuestionRequest request,
        CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return null;

        var question = quiz.Questions.FirstOrDefault(q => q.Id == questionId);
        if (question is null) return null;

        question.Update(
            request.Type,
            request.Text,
            request.ImageUrl,
            request.Explanation,
            request.TimeLimitSeconds,
            request.Points);
        question.SetOptions(request.Options.Select(o => (o.Text, o.IsCorrect)));
        quiz.UpdateMetadata(quiz.Title, quiz.Description, quiz.Language, clock.UtcNow);

        await quizzes.SaveChangesAsync(ct);
        return ToQuestionDto(question);
    }

    public async Task<bool> DeleteQuestionAsync(Guid quizId, Guid questionId, Guid ownerId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return false;

        if (quiz.Questions.All(q => q.Id != questionId))
        {
            return false;
        }

        quiz.RemoveQuestion(questionId, clock.UtcNow);
        await quizzes.SaveChangesAsync(ct);
        return true;
    }

    public async Task<QuizDetailDto?> ReorderQuestionsAsync(
        Guid quizId,
        Guid ownerId,
        ReorderQuestionsRequest request,
        CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(quizId, ownerId, ct);
        if (quiz is null) return null;

        quiz.ReorderQuestions(request.OrderedIds, clock.UtcNow);
        await quizzes.SaveChangesAsync(ct);
        return ToDetailDto(quiz);
    }

    private static QuizDetailDto ToDetailDto(Quiz quiz) =>
        new(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.Language,
            quiz.IsArchived,
            quiz.CreatedAt,
            quiz.UpdatedAt,
            quiz.Questions
                .OrderBy(q => q.OrderIndex)
                .Select(ToQuestionDto)
                .ToList());

    private static QuestionDto ToQuestionDto(Question q) =>
        new(
            q.Id,
            q.OrderIndex,
            q.Type,
            q.Text,
            q.ImageUrl,
            q.Explanation,
            q.TimeLimitSeconds,
            q.Points,
            q.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new OptionDto(o.Id, o.OrderIndex, o.Text, o.IsCorrect))
                .ToList());
}
