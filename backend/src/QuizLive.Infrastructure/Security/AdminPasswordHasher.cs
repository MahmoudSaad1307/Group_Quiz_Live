using Microsoft.AspNetCore.Identity;
using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;

namespace QuizLive.Infrastructure.Security;

public sealed class AdminPasswordHasher : IPasswordHasherService
{
    private readonly PasswordHasher<Admin> _hasher = new();

    public string HashPassword(Admin admin, string password) =>
        _hasher.HashPassword(admin, password);

    public bool VerifyPassword(Admin admin, string password) =>
        _hasher.VerifyHashedPassword(admin, admin.PasswordHash, password) != PasswordVerificationResult.Failed;
}
