using Microsoft.AspNetCore.Mvc;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Identity.Api.Common;

/// <summary>Maps an application <see cref="Result"/> to the matching HTTP response.</summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess ? new NoContentResult() : Problem(result);

    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.IsSuccess ? new OkObjectResult(result.Value) : Problem(result);

    private static IActionResult Problem(Result result)
    {
        var status = result.Status switch
        {
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.Validation => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };

        return new ObjectResult(new ProblemDetails { Status = status, Title = result.Error ?? "Request failed." })
        {
            StatusCode = status,
        };
    }
}
