using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuizLive.Application.Abstractions;

namespace QuizLive.Infrastructure.Security;

public sealed class CryptoPinGenerator : IPinGenerator
{
    public string GeneratePin() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}

public sealed class PlayerTokenService : IPlayerTokenService
{
    public (string RawToken, string TokenHash) GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var raw = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (raw, HashToken(raw));
    }

    public string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    public bool FixedTimeEquals(string hashA, string hashB)
    {
        var a = Encoding.UTF8.GetBytes(hashA);
        var b = Encoding.UTF8.GetBytes(hashB);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public sealed class ScreenTokenService(IOptions<JwtOptions> options, IClock clock) : IScreenTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateScreenToken(Guid sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = clock.UtcNow.UtcDateTime;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim("sid", sessionId.ToString()),
                new Claim("role", "screen")
            ],
            notBefore: now,
            expires: now.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool TryValidateScreenToken(string token, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirstValue("role") != "screen")
            {
                return false;
            }

            return Guid.TryParse(principal.FindFirstValue("sid"), out sessionId);
        }
        catch (SecurityTokenException)
        {
            return false;
        }
    }
}
