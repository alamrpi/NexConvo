using Microsoft.AspNetCore.Mvc;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Api.Extensions;

/// <summary>
/// Maps <see cref="Result"/> and <see cref="Result{T}"/> to ASP.NET Core action results,
/// following RFC 7807 problem semantics.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result) =>
        result.Status switch
        {
            ResultStatus.Success      => new NoContentResult(),
            ResultStatus.NotFound     => new NotFoundObjectResult(result.Error),
            ResultStatus.Conflict     => new ConflictObjectResult(result.Error),
            ResultStatus.Validation   => new UnprocessableEntityObjectResult(result.Error),
            ResultStatus.Unauthorized => new UnauthorizedObjectResult(result.Error),
            ResultStatus.Forbidden    => new ForbidResult(),
            _                         => new ObjectResult(result.Error) { StatusCode = 500 },
        };

    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.Status switch
        {
            ResultStatus.Success      => new OkObjectResult(result.Value),
            ResultStatus.NotFound     => new NotFoundObjectResult(result.Error),
            ResultStatus.Conflict     => new ConflictObjectResult(result.Error),
            ResultStatus.Validation   => new UnprocessableEntityObjectResult(result.Error),
            ResultStatus.Unauthorized => new UnauthorizedObjectResult(result.Error),
            ResultStatus.Forbidden    => new ForbidResult(),
            _                         => new ObjectResult(result.Error) { StatusCode = 500 },
        };

    public static IActionResult ToAcceptedResult<T>(this Result<T> result, Func<T, string> locationFactory) =>
        result.IsSuccess
            ? new AcceptedResult(locationFactory(result.Value!), new { id = result.Value })
            : result.ToActionResult();
}
