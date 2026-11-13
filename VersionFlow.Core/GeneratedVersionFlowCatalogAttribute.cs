using System.Diagnostics.CodeAnalysis;

namespace VersionFlow;

/// <summary>Generated assembly metadata used by AddVersionFlow. Applications do not apply this attribute manually.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GeneratedVersionFlowCatalogAttribute : Attribute
{
    public GeneratedVersionFlowCatalogAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type catalogType)
        => CatalogType = catalogType;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type CatalogType { get; }
}
