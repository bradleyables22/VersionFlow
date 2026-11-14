using Microsoft.AspNetCore.Mvc;

namespace VersionFlow.Extensions.Results.AspNetCore;

public static partial class ResultExtensions
{
    /// <summary>Returns a native MVC result, also usable as IActionResult or ActionResult&lt;T&gt;.</summary>
    public static ActionResult AsActionResult(this TryResult result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        return result.Success ? new NoContentResult() : ActionFailure(failureStatusCode, failureTitle, failureMessage);
    }

    /// <summary>Returns 200 with the payload, 204 for null, or Problem Details on failure.</summary>
    public static ActionResult AsActionResult<T>(this TryResult<T> result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        if (!result.Success) return ActionFailure(failureStatusCode, failureTitle, failureMessage);
        return result.Data is null ? new NoContentResult() : new OkObjectResult(result.Data);
    }

    /// <summary>Returns the full concrete collection/page envelope, including empty pages.</summary>
    public static ActionResult AsActionResult<T>(this CollectionResult<T> result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        return result.Success ? new OkObjectResult(result) : ActionFailure(failureStatusCode, failureTitle, failureMessage);
    }

    private static ObjectResult ActionFailure(int status, string? title, string? detail) => new(new ProblemDetails
    {
        Status = status,
        Title = string.IsNullOrWhiteSpace(title) ? DefaultFailureTitle : title,
        Detail = string.IsNullOrWhiteSpace(detail) ? DefaultFailureMessage : detail
    })
    {
        StatusCode = status,
        ContentTypes = { "application/problem+json" }
    };
}
