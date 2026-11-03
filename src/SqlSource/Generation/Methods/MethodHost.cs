using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Where a query's method goes and how visible it is, which <see cref="MethodPlacement" /> mixes.  A later kind of
/// method changes it here (<c>docs/architecture/generator.md</c>, "Adding a kind of method").
/// </summary>
/// <param name="Accessibility">How visible the method is.</param>
/// <param name="IsExtension">Whether the method is in the extension class and extends the connection.</param>
internal readonly record struct MethodHost(MethodAccessibility Accessibility, bool IsExtension)
{
    /// <summary>The host that a type's <paramref name="placement" /> gives its methods.</summary>
    public static MethodHost Of(MethodPlacement placement) =>
        placement switch
        {
            MethodPlacement.ExtensionClass => new MethodHost(MethodAccessibility.Public, IsExtension: true),
            MethodPlacement.Public => new MethodHost(MethodAccessibility.Public, IsExtension: false),
            MethodPlacement.Internal => new MethodHost(MethodAccessibility.Internal, IsExtension: false),
            MethodPlacement.Private => new MethodHost(MethodAccessibility.Private, IsExtension: false),
        };
}
