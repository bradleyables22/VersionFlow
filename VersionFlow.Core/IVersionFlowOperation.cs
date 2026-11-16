namespace VersionFlow;

/// <summary>Builds service-based branches for a known operation. Obtain from IVersionFlow.For.</summary>
public interface IVersionFlowOperation
{
    /// <summary>Forces a version, overriding method, class, and default configuration.</summary>
    /// <param name="version">A positive version number that must have a branch at execution.</param>
    /// <returns>A new builder; the original remains unchanged.</returns>
    VersionFlowOperation UseVersion(int version);
    /// <summary>Selects the highest supplied branch version, overriding configuration.</summary>
    /// <returns>A new builder; the original remains unchanged.</returns>
    VersionFlowOperation UseLatest();
    /// <summary>Starts synchronous result-producing branches for this operation.</summary>
    /// <typeparam name="T">The common result type for all branches.</typeparam>
    /// <returns>An empty branch builder. Add Version callbacks before Execute.</returns>
    VersionFlowMatch<T> Match<T>();
    /// <summary>Starts asynchronous result-producing branches for this operation.</summary>
    /// <typeparam name="T">The common result type for all branches.</typeparam>
    /// <returns>An empty branch builder. Add Version callbacks before ExecuteAsync.</returns>
    VersionFlowAsyncMatch<T> MatchAsync<T>();
}
