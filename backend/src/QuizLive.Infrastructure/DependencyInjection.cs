using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuizLive.Application.Abstractions;
using QuizLive.Infrastructure.Export;
using QuizLive.Infrastructure.Persistence;
using QuizLive.Infrastructure.Persistence.Repositories;
using QuizLive.Infrastructure.Profanity;
using QuizLive.Infrastructure.Security;
using QuizLive.Infrastructure.Sessions;
using QuizLive.Infrastructure.Storage;
using QuizLive.Infrastructure.Time;

namespace QuizLive.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SeedAdminOptions>()
            .Bind(configuration.GetSection(SeedAdminOptions.SectionName));

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<QuizLiveDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(QuizLiveDbContext).Assembly.FullName)));

        services.AddHealthChecks()
            .AddDbContextCheck<QuizLiveDbContext>("postgres");

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasherService, AdminPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISessionResultsService, SessionResultsService>();
        services.AddSingleton<IResultsExporter, ResultsExporter>();
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        services.AddSingleton<IPinGenerator, CryptoPinGenerator>();
        services.AddSingleton<IPlayerTokenService, PlayerTokenService>();
        services.AddSingleton<IScreenTokenService, ScreenTokenService>();
        services.AddSingleton<IProfanityFilter, WordListProfanityFilter>();
        services.AddSingleton<AdminSeeder>();
        services.AddHostedService<SessionLifecycleService>();

        return services;
    }
}
