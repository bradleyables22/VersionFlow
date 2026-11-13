namespace VersionFlow;

public interface IVersionFlowOperation
{
    VersionFlowOperation UseVersion(int version);
    VersionFlowOperation UseLatest();
    VersionFlowMatch<T> Match<T>();
    VersionFlowAsyncMatch<T> MatchAsync<T>();
}
