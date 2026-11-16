using System.Diagnostics.CodeAnalysis;

namespace VersionFlow;

/// <summary>Generated assembly metadata used by AddVersionFlow. Applications do not apply this attribute manually.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GeneratedVersionFlowCatalogAttribute : Attribute
{
    /// <summary>Identifies a generated catalog for automatic discovery.</summary>
    /// <param name="catalogType">A generated IVersionFlowCatalog type with a public parameterless constructor.</param>
    public GeneratedVersionFlowCatalogAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type catalogType)
        => CatalogType = catalogType;

    /// <summary>The generated catalog type to instantiate during discovery.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type CatalogType { get; }
}
