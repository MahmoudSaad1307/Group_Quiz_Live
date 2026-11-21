using System.ComponentModel.DataAnnotations;

namespace QuizLive.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = "QuizLive";

    [Required]
    public string Audience { get; init; } = "QuizLive";

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(5, 1440)]
    public int ExpiryMinutes { get; init; } = 480;
}

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "QuizLive Admin";
}
