using Microsoft.EntityFrameworkCore;
using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;

namespace QuizLive.Infrastructure.Persistence.Repositories;

public sealed class AdminRepository(QuizLiveDbContext db) : IAdminRepository
{
    public Task<Admin?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = Admin.NormalizeEmail(email);
        return db.Admins.FirstOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
    }

    public Task<Admin?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Admins.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> AnyAsync(CancellationToken ct = default) =>
        db.Admins.AnyAsync(ct);

    public async Task AddAsync(Admin admin, CancellationToken ct = default)
    {
        db.Admins.Add(admin);
        await db.SaveChangesAsync(ct);
    }
}
