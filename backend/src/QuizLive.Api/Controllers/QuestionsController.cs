using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizLive.Api.Auth;
using QuizLive.Application.Quizzes;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/quizzes/{quizId:guid}/questions")]
[Authorize(Policy = "Admin")]
public sealed class QuestionsController(
    QuizService quizService,
    IValidator<UpsertQuestionRequest> upsertValidator,
    IValidator<ReorderQuestionsRequest> reorderValidator) : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid quizId, [FromBody] UpsertQuestionRequest request, CancellationToken ct)
    {
        var validation = await upsertValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var created = await quizService.AddQuestionAsync(quizId, User.GetAdminId(), request, ct);
        return created is null ? NotFound() : Ok(created);
    }

    [HttpPut("{questionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid quizId,
        Guid questionId,
        [FromBody] UpsertQuestionRequest request,
        CancellationToken ct)
    {
        var validation = await upsertValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var updated = await quizService.UpdateQuestionAsync(quizId, questionId, User.GetAdminId(), request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{questionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid quizId, Guid questionId, CancellationToken ct)
    {
        var ok = await quizService.DeleteQuestionAsync(quizId, questionId, User.GetAdminId(), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPut("reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(Guid quizId, [FromBody] ReorderQuestionsRequest request, CancellationToken ct)
    {
        var validation = await reorderValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        try
        {
            var updated = await quizService.ReorderQuestionsAsync(quizId, User.GetAdminId(), request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return Problem(title: "Invalid reorder request", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
