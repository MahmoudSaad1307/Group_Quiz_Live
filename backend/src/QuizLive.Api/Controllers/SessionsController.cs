using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizLive.Api.Auth;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(Policy = "Admin")]
public sealed class SessionsController(
    SessionService sessionService,
    ISessionResultsService resultsService,
    IResultsExporter exporter,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        var baseUrl = configuration["NEXT_PUBLIC_APP_URL"]
            ?? $"{Request.Scheme}://{Request.Host}";

        var created = await sessionService.CreateSessionAsync(User.GetAdminId(), request, baseUrl, ct);
        if (created is null)
        {
            return Problem(
                title: "Cannot create session",
                detail: "Quiz not found, archived, or has no questions.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(Get), new { id = created.SessionId }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var session = await sessionService.GetSessionAsync(id, User.GetAdminId(), ct);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost("{id:guid}/screen-token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueScreenToken(Guid id, CancellationToken ct)
    {
        var res = await sessionService.IssueScreenTokenAsync(id, User.GetAdminId(), ct);
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> GetResults(Guid id, CancellationToken ct)
    {
        var res = await resultsService.GetResultsAsync(id, User.GetAdminId(), ct);
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "xlsx", CancellationToken ct = default)
    {
        var res = await resultsService.GetResultsAsync(id, User.GetAdminId(), ct);
        if (res is null) return NotFound();

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = exporter.ExportCsv(res);
            return File(csv, "text/csv; charset=utf-8", $"quizlive-{res.Pin}.csv");
        }

        var xlsx = exporter.ExportXlsx(res);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"quizlive-{res.Pin}.xlsx");
    }
}
