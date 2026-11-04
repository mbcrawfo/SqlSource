using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Where a query's method goes and how visible it is.  A later kind of method changes it here
/// (<c>docs/architecture/generator.md</c>, "Adding a kind of method").  The default is a public method of the extension
/// class.
/// </summary>
/// <param name="Accessibility">
/// How visible the method is.  In a nested class it is <see cref="Visibility.Public" />, and the class has its own.
/// </param>
/// <param name="Location">Where the method goes: the extension class, the type, or a class nested in it.</param>
/// <param name="NestedClass">
/// The name of the class under <see cref="MemberLocation.NestedClass" />, and null under any other location.
/// </param>
internal readonly record struct MethodHost(Visibility Accessibility, MemberLocation Location, string? NestedClass)
{
    /// <summary>Whether the method is in the extension class and extends the connection.</summary>
    public bool IsExtension => Location == MemberLocation.ExtensionClass;
}
