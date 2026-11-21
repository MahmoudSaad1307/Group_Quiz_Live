using Microsoft.EntityFrameworkCore;
using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;

namespace QuizLive.Infrastructure.Persistence.Repositories;

public sealed class QuizRepository(QuizLiveDbContext db) : IQuizRepository
{
    public async Task<(IReadOnlyList<Quiz> Items, int TotalCount)> ListByOwnerAsync(
        Guid ownerId,
        string? search,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .Where(q => q.OwnerId == ownerId);

        if (!includeArchived)
        {
            query = query.Where(q => !q.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(q => EF.Functions.ILike(q.Title, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(q => q.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Quiz?> GetOwnedAsync(Guid quizId, Guid ownerId, CancellationToken ct = default) =>
        db.Quizzes
            .Include(q => q.Questions)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.OwnerId == ownerId, ct);

    public async Task AddAsync(Quiz quiz, CancellationToken ct = default)
    {
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Quiz quiz, CancellationToken ct = default)
    {
        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> HasSessionsAsync(Guid quizId, CancellationToken ct = default) =>
        db.GameSessions.AnyAsync(s => s.QuizId == quizId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
