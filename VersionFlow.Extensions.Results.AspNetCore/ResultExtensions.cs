using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace VersionFlow.Extensions.Results.AspNetCore;

/// <summary>Explicit HTTP boundary policy; diagnostic exceptions never become response details.</summary>
public static partial class ResultExtensions
{
    /// <summary>The safe Problem Details title used when no nonblank failure title is supplied.</summary>
    public const string DefaultFailureTitle = "An unexpected error occurred.";
    /// <summary>The safe Problem Details detail used when no nonblank failure message is supplied.</summary>
    public const string DefaultFailureMessage = "The operation could not be completed.";

    /// <summary>A successful command returns 204.</summary>
    /// <param name="result">The nonnull outcome to map to an HTTP response.</param>
    /// <param name="failureStatusCode">The HTTP status for failure, from 400 through 599; defaults to 500.</param>
    /// <param name="failureTitle">A caller-safe Problem Details title. Null or whitespace uses DefaultFailureTitle.</param>
    /// <param name="failureMessage">A caller-safe Problem Details detail. Null or whitespace uses DefaultFailureMessage; result messages and exceptions are not copied automatically.</param>
    /// <returns>A minimal API response using success status/payload rules or Problem Details on failure.</returns>
    public static IResult AsHttpResult(this TryResult result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        return result.Success ? HttpResults.NoContent() : Failure(failureStatusCode, failureTitle, failureMessage);
    }

    /// <summary>A successful value returns 200 with the payload; null returns 204. Empty collections remain 200.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The nonnull outcome to map to an HTTP response.</param>
    /// <param name="failureStatusCode">The HTTP status for failure, from 400 through 599; defaults to 500.</param>
    /// <param name="failureTitle">A caller-safe Problem Details title. Null or whitespace uses DefaultFailureTitle.</param>
    /// <param name="failureMessage">A caller-safe Problem Details detail. Null or whitespace uses DefaultFailureMessage; result messages and exceptions are not copied automatically.</param>
    /// <returns>A minimal API response using success status/payload rules or Problem Details on failure.</returns>
    public static IResult AsHttpResult<T>(this TryResult<T> result, int failureStatusCode = 500,
        string? failureTitle = null, string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateStatus(failureStatusCode);
        if (!result.Success) return Failure(failureStatusCode, failureTitle, failureMessage);
        return result.Data is null ? HttpResults.NoContent() : HttpResults.Ok(result.Data);
    }

    /// <summary>Successful collections always return 200 with their envelope, including empty pages and cursors.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The nonnull outcome to map to an HTTP response.</param>
    /// <param name="failureStatusCode">The HTTP status for failure, from 400 through 599; defaults to 500.</param>
    /// <param name="failureTitle">A caller-safe Problem Details title. Null or whitespace uses DefaultFailureTitle.</param>
    /// <param name="failureMessage">A caller-safe Problem Details detail. Null or whitespace uses DefaultFailureMessage; result messages and exceptions are not copied automatically.</param>
    /// <returns>A minimal API response using success status/payload rules or Problem Details on failure.</returns>
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
