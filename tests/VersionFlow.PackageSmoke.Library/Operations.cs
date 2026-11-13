using VersionFlow;
namespace PackageLibrary;
public interface IExternalOperation { int Read(); }
[Versioned(typeof(IExternalOperation), 1)]
public sealed class ExternalOperation1 : IExternalOperation { public int Read() => 1; }
[Versioned(typeof(IExternalOperation), 4)]
public sealed class ExternalOperation4 : IExternalOperation { public int Read() => 4; }
