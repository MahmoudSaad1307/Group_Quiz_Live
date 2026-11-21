using QuizLive.Domain.Entities;

namespace QuizLive.Application.Abstractions;

public interface IQuizRepository
{
    Task<(IReadOnlyList<Quiz> Items, int TotalCount)> ListByOwnerAsync(
        Guid ownerId,
        string? search,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Quiz?> GetOwnedAsync(Guid quizId, Guid ownerId, CancellationToken ct = default);
    Task AddAsync(Quiz quiz, CancellationToken ct = default);
    Task DeleteAsync(Quiz quiz, CancellationToken ct = default);
    Task<bool> HasSessionsAsync(Guid quizId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
