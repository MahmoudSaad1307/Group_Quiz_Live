using QuizLive.Domain.Entities;

namespace QuizLive.Application.Abstractions;

public interface IAdminRepository
{
    Task<Admin?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<Admin?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task AddAsync(Admin admin, CancellationToken ct = default);
}
