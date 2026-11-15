namespace VersionFlow.Extensions.Railway;

/// <summary>Starts railway chains from existing values.</summary>
public static class RailResultValueExtensions
{
    /// <summary>Wraps a value in a successful result, including an explicitly nullable value.</summary>
    /// <remarks>Equivalent to RailResult&lt;T&gt;.Passed(value). Does not validate or clone the value.</remarks>
    public static RailResult<T> ToRailResult<T>(this T value) => RailResult<T>.Passed(value);
}
