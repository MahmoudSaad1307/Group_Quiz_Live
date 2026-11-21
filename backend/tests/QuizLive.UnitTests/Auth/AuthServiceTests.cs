using FluentAssertions;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Auth;
using QuizLive.Domain.Entities;

namespace QuizLive.UnitTests.Auth;

public sealed class AuthServiceTests
{
    private sealed class FakeAdminRepo(params Admin[] seed) : IAdminRepository
    {
        private readonly List<Admin> _admins = [.. seed];
        public Task<Admin?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_admins.FirstOrDefault(a => a.NormalizedEmail == Admin.NormalizeEmail(email)));
        public Task<Admin?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_admins.FirstOrDefault(a => a.Id == id));
        public Task<bool> AnyAsync(CancellationToken ct = default) =>
            Task.FromResult(_admins.Count > 0);
        public Task AddAsync(Admin admin, CancellationToken ct = default)
        {
            _admins.Add(admin);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHasher : IPasswordHasherService
    {
        public string HashPassword(Admin admin, string password) => $"HASH:{password}";
        public bool VerifyPassword(Admin admin, string password) => admin.PasswordHash == $"HASH:{password}";
    }

    private sealed class FakeJwt : IJwtTokenService
    {
        public string CreateAdminToken(Admin admin) => $"jwt-{admin.Id}";
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndProfile()
    {
        var admin = new Admin(Guid.NewGuid(), "teacher@quizlive.local", "Teacher", DateTimeOffset.UtcNow);
        admin.SetPasswordHash("HASH:Secret123!");
        var sut = new AuthService(new FakeAdminRepo(admin), new FakeHasher(), new FakeJwt());

        var result = await sut.LoginAsync(new LoginRequest("TEACHER@quizlive.local", "Secret123!"));

        result.Succeeded.Should().BeTrue();
        result.Token.Should().Be($"jwt-{admin.Id}");
        result.Profile.Should().NotBeNull();
        result.Profile!.Email.Should().Be("teacher@quizlive.local");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_Fails()
    {
        var admin = new Admin(Guid.NewGuid(), "teacher@quizlive.local", "Teacher", DateTimeOffset.UtcNow);
        admin.SetPasswordHash("HASH:Secret123!");
        var sut = new AuthService(new FakeAdminRepo(admin), new FakeHasher(), new FakeJwt());

        var result = await sut.LoginAsync(new LoginRequest("teacher@quizlive.local", "Wrong"));

        result.Succeeded.Should().BeFalse();
        result.Token.Should().BeNull();
    }
}
