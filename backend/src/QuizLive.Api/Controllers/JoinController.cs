using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/join")]
[AllowAnonymous]
public sealed class JoinController(
    SessionService sessionService,
    IMemoryCache cache) : ControllerBase
{
    private const int MaxFailedPinGuesses = 10;

    [HttpGet("{pin}")]
    [EnableRateLimiting("join")]
    public async Task<IActionResult> CheckPin(string pin, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var lockoutKey = $"pin-lockout:{ip}";
        if (cache.TryGetValue<int>(lockoutKey, out var failures) && failures >= MaxFailedPinGuesses)
        {
            return Problem(
                title: "Too many invalid PIN attempts",
                detail: "Please wait a few minutes before trying again.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (pin.Length != 6 || !pin.All(char.IsDigit))
        {
            RecordFailure(lockoutKey, failures);
            return NotFound();
        }

        var result = await sessionService.CheckPinAsync(pin, ct);
        if (result is null)
        {
            RecordFailure(lockoutKey, failures);
            return NotFound();
        }

        return Ok(result);
    }

    private void RecordFailure(string lockoutKey, int currentFailures)
    {
        cache.Set(lockoutKey, currentFailures + 1, TimeSpan.FromMinutes(5));
    }
}
