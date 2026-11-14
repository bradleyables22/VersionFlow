using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace VersionFlow.Extensions.Results.AspNetCore;

/// <summary>Explicit HTTP boundary policy; diagnostic exceptions never become response details.</summary>
public static partial class ResultExtensions
{
    public const string DefaultFailureTitle = "An unexpected error occurred.";
    public const string DefaultFailureMessage = "The operation could not be completed.";

    /// <summary>A successful command returns 204.</summary>
    public static IResult AsHttpResult(this TryResult result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        return result.Success ? HttpResults.NoContent() : Failure(failureStatusCode, failureTitle, failureMessage);
    }

    /// <summary>A successful value returns 200 with the payload; null returns 204. Empty collections remain 200.</summary>
    public static IResult AsHttpResult<T>(this TryResult<T> result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        if (!result.Success) return Failure(failureStatusCode, failureTitle, failureMessage);
        return result.Data is null ? HttpResults.NoContent() : HttpResults.Ok(result.Data);
    }

    /// <summary>Successful collections always return 200 with their envelope, including empty pages and cursors.</summary>
    public static IResult AsHttpResult<T>(this CollectionResult<T> result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        // Object preserves the concrete page's metadata during JSON serialization.
        return result.Success ? HttpResults.Ok<object>(result) : Failure(failureStatusCode, failureTitle, failureMessage);
    }

    private static IResult Failure(int status, string? title, string? detail) => HttpResults.Problem(
        statusCode: status,
        title: string.IsNullOrWhiteSpace(title) ? DefaultFailureTitle : title,
        detail: string.IsNullOrWhiteSpace(detail) ? DefaultFailureMessage : detail);

    private static void ValidateStatus(int status)
    {
        if (status is < 400 or > 599)
            throw new ArgumentOutOfRangeException(nameof(status), "Failure status must be between 400 and 599.");
    }
}
