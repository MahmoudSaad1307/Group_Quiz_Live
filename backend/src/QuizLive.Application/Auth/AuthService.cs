using QuizLive.Application.Abstractions;

namespace QuizLive.Application.Auth;

public sealed class AuthService(
    IAdminRepository admins,
    IPasswordHasherService passwordHasher,
    IJwtTokenService jwtTokens)
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var admin = await admins.FindByEmailAsync(request.Email, ct);
        if (admin is null || !passwordHasher.VerifyPassword(admin, request.Password))
        {
            return new LoginResult(false, null, null);
        }

        var token = jwtTokens.CreateAdminToken(admin);
        var profile = new AdminProfileDto(admin.Id, admin.Email, admin.DisplayName);
        return new LoginResult(true, token, profile);
    }

    public async Task<AdminProfileDto?> GetProfileAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await admins.FindByIdAsync(adminId, ct);
        return admin is null ? null : new AdminProfileDto(admin.Id, admin.Email, admin.DisplayName);
    }
}
