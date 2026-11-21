using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using QuizLive.Application.Auth;
using QuizLive.Application.Game;
using QuizLive.Application.Players;
using QuizLive.Application.Quizzes;
using QuizLive.Application.Sessions;

namespace QuizLive.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<AuthService>();
        services.AddScoped<QuizService>();
        services.AddScoped<SessionService>();
        services.AddSingleton<GameLoopService>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<NicknameNormalizer>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
