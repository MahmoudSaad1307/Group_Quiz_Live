using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;

namespace QuizLive.Infrastructure.Security;

public sealed partial class AdminSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedAdminOptions> seedOptions,
    ILogger<AdminSeeder> logger)
{
    public async Task SeedIfEmptyAsync(CancellationToken ct = default)
    {
        var opts = seedOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.Email) || string.IsNullOrWhiteSpace(opts.Password))
        {
            LogMissingCredentials(logger);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var admins = scope.ServiceProvider.GetRequiredService<IAdminRepository>();
        if (await admins.AnyAsync(ct))
        {
            return;
        }

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        var admin = new Admin(Guid.NewGuid(), opts.Email, opts.DisplayName, clock.UtcNow);
        admin.SetPasswordHash(hasher.HashPassword(admin, opts.Password));
        await admins.AddAsync(admin, ct);

        LogAdminSeeded(logger, admin.Email);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SeedAdmin credentials not configured; skipping initial admin seed.")]
    private static partial void LogMissingCredentials(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded initial admin account for {Email}.")]
    private static partial void LogAdminSeeded(ILogger logger, string email);
}
