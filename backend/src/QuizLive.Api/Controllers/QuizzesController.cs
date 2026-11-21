using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizLive.Api.Auth;
using QuizLive.Application.Quizzes;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize(Policy = "Admin")]
public sealed class QuizzesController(
    QuizService quizService,
    IValidator<CreateQuizRequest> createValidator,
    IValidator<UpdateQuizRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await quizService.ListAsync(User.GetAdminId(), search, includeArchived, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateQuizRequest request, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var created = await quizService.CreateAsync(User.GetAdminId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var quiz = await quizService.GetAsync(id, User.GetAdminId(), ct);
        return quiz is null ? NotFound() : Ok(quiz);
    }

    [HttpPut("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuizRequest request, CancellationToken ct)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var updated = await quizService.UpdateAsync(id, User.GetAdminId(), request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await quizService.DeleteOrArchiveAsync(id, User.GetAdminId(), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/duplicate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var copy = await quizService.DuplicateAsync(id, User.GetAdminId(), ct);
        return copy is null ? NotFound() : CreatedAtAction(nameof(Get), new { id = copy.Id }, copy);
    }
}
