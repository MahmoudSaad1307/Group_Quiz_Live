using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace QuizLive.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetAdminId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing or invalid admin subject claim.");
    }
}
