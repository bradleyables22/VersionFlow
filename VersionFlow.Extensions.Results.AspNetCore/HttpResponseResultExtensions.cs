using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace VersionFlow.Extensions.Results.AspNetCore;

/// <summary>Reads HTTP responses into result contracts. The caller owns the response.</summary>
public static class HttpResponseResultExtensions
{
    /// <summary>Maps a successful HTTP status to a command result; failures read Problem Details.</summary>
    public static Task<TryResult> FromHttpResponseAsync(this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
        => Read(response, () => Task.FromResult(TryResult.Succeeded()), TryResult.Failed, cancellationToken);

    /// <summary>Reads a successful JSON payload. HTTP 204 produces default(T); use a nullable T when needed.</summary>
    public static Task<TryResult<T>> FromHttpResponseAsync<T>(this HttpResponseMessage response,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        => Read(response, async () => TryResult<T>.Succeeded(response.StatusCode == HttpStatusCode.NoContent
            ? default! : (await response.Content.ReadFromJsonAsync<T>(options, cancellationToken).ConfigureAwait(false))!),
            TryResult<T>.Failed, cancellationToken);

    /// <summary>Reads a complete collection envelope emitted by AsHttpResult or AsActionResult.</summary>
    public static Task<CollectionTryResult<T>> FromHttpCollectionAsync<T>(this HttpResponseMessage response,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        => ReadEnvelope<T, CollectionTryResult<T>>(response, e => CollectionTryResult<T>.Succeeded(e.Data!),
            CollectionTryResult<T>.Failed, options, cancellationToken);

    /// <summary>Reads a numbered page envelope and validates its metadata.</summary>
    public static Task<PagedTryResult<T>> FromHttpPageAsync<T>(this HttpResponseMessage response,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        => ReadEnvelope<T, PagedTryResult<T>>(response, e => PagedTryResult<T>.Succeeded(e.Data!,
            e.PageNumber ?? throw new JsonException("Missing pageNumber."),
            e.PageSize ?? throw new JsonException("Missing pageSize."),
            e.TotalCount ?? throw new JsonException("Missing totalCount.")), PagedTryResult<T>.Failed, options, cancellationToken);

    /// <summary>Reads an offset page envelope and validates its metadata.</summary>
    public static Task<OffsetTryResult<T>> FromHttpOffsetAsync<T>(this HttpResponseMessage response,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        => ReadEnvelope<T, OffsetTryResult<T>>(response, e => OffsetTryResult<T>.Succeeded(e.Data!,
            e.Offset ?? throw new JsonException("Missing offset."),
            e.Limit ?? throw new JsonException("Missing limit."),
            e.TotalCount ?? throw new JsonException("Missing totalCount.")), OffsetTryResult<T>.Failed, options, cancellationToken);

    /// <summary>Reads a cursor page envelope and validates its continuation token.</summary>
    public static Task<CursorTryResult<T>> FromHttpCursorAsync<T>(this HttpResponseMessage response,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        => ReadEnvelope<T, CursorTryResult<T>>(response, e => CursorTryResult<T>.Succeeded(e.Data!, e.NextCursor),
            CursorTryResult<T>.Failed, options, cancellationToken);

    private static Task<TResult> ReadEnvelope<T, TResult>(HttpResponseMessage response,
        Func<Envelope<T>, TResult> success, Func<string, Exception?, TResult> failure,
        JsonSerializerOptions? options, CancellationToken cancellationToken)
        => Read(response, async () =>
        {
            var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(options, cancellationToken).ConfigureAwait(false);
            if (envelope?.IsSuccess != true || envelope.Data is null)
                throw new JsonException("Expected a successful collection envelope with data.");
            try { return success(envelope); }
            catch (ArgumentException exception) { throw new JsonException("Invalid collection metadata.", exception); }
        }, failure, cancellationToken);

    private static async Task<TResult> Read<TResult>(HttpResponseMessage response, Func<Task<TResult>> success,
        Func<string, Exception?, TResult> failure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (response.IsSuccessStatusCode) return await success().ConfigureAwait(false);
            var message = $"HTTP request failed with status {(int)response.StatusCode}.";
            if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(problem?.Detail)) message = problem.Detail;
                    else if (!string.IsNullOrWhiteSpace(problem?.Title)) message = problem.Title;
                }
                catch (JsonException exception) { return failure(message, exception); }
            }
            return failure(message, null);
        }
        catch (JsonException exception) { return failure("The HTTP response contained invalid JSON or result data.", exception); }
        catch (HttpRequestException exception) { return failure("The HTTP response could not be read.", exception); }
        catch (IOException exception) { return failure("The HTTP response could not be read.", exception); }
    }

    private sealed class Envelope<T>
    {
        public bool? IsSuccess { get; set; }
        public T[]? Data { get; set; }
        public int? PageNumber { get; set; }
        public int? PageSize { get; set; }
        public long? TotalCount { get; set; }
        public long? Offset { get; set; }
        public int? Limit { get; set; }
        public string? NextCursor { get; set; }
    }
}
