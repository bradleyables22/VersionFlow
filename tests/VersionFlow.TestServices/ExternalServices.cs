using VersionFlow;
namespace VersionFlow.TestServices;
public interface IExternal { string Execute(); }
[Versioned(typeof(IExternal), 1, Key = "External")]
public sealed class External1 : IExternal, IDisposable
{
    public bool Disposed { get; private set; }
    public string Execute() { ObjectDisposedException.ThrowIf(Disposed, this); return "external-one"; }
    public void Dispose() => Disposed = true;
}
[Versioned(typeof(IExternal), 5, Key = "External")]
public sealed class External5 : IExternal { public string Execute() => "external-five"; }

