using SqlSource.Generation.Models;
using SqlSource.Settings;

namespace SqlSource.Generation.Placement;

/// <summary>
/// The rules of placement that need nothing but a query's location, its visibility, its output and its type: the
/// default visibility, the combinations that C# or the generated code cannot have, and how a marker and the code spell
/// each value.
/// </summary>
internal static class PlacementChecks
{
    /// <summary>
    /// The visibility of a query that writes none: private for a nested class, as its members are public inside it;
    /// public on a type that is public with every type around it and internal on any other, under
    /// <see cref="MemberLocation.Class" />; and internal for an extension method, so that a library puts no extension
    /// of the connection before its consumers unless a file asks.  None of them is ever an error.
    /// </summary>
    /// <param name="location">Where the query's members go.</param>
    /// <param name="typeIsPublic">Whether the type and every type around it are public.</param>
    public static Visibility DefaultVisibility(MemberLocation location, bool typeIsPublic) =>
        location switch
        {
            MemberLocation.NestedClass => Visibility.Private,
            MemberLocation.Class => typeIsPublic ? Visibility.Public : Visibility.Internal,
            MemberLocation.ExtensionClass => Visibility.Internal,
        };

    /// <summary>
    /// Why a query's members cannot have the visibility at the location, as <c>SQLSRC123</c> says it, or null when they
    /// can.  Only what C# forbids, or what would not compile, is refused: <c>protected</c> on a sealed type is allowed.
    /// </summary>
    /// <param name="type">The type, which a reason names in full.</param>
    /// <param name="location">Where the query's members go.</param>
    /// <param name="visibility">Their visibility, which is the nested class's under a nested class.</param>
    /// <param name="output">
    /// The query's output: only a query method takes models, so only its visibility depends on theirs.
    /// </param>
    public static string? VisibilityReason(
        TargetType type,
        MemberLocation location,
        Visibility visibility,
        OutputKind output
    ) =>
        location switch
        {
            // A top-level class is public or internal, and so is the extension class.  Its public method cannot take
            // or return the type's internal models (CS0051).  A method with no models is refused too (TD-0101).
            MemberLocation.ExtensionClass => visibility switch
            {
                Visibility.Private or Visibility.Protected => "an extension method is public or internal",
                Visibility.Public when !type.IsPublic => "'"
                    + NameOf(type)
                    + "' is not public, so its models are internal, which a public extension method cannot take or "
                    + "return",
                Visibility.Public or Visibility.Internal => null,
            },
            // CS1057 and CS0666.
            MemberLocation.Class when visibility == Visibility.Protected && type.IsStatic => StaticReason(type),
            MemberLocation.Class when visibility == Visibility.Protected && type.IsStruct => StructReason(type),
            // A protected class nested in a static class compiles; one nested in a struct is CS0666.
            MemberLocation.NestedClass when visibility == Visibility.Protected && type.IsStruct => StructReason(type),
            // A derived type of another assembly reaches a public or protected method there, which cannot take or
            // return the type's internal models (CS0051).  A method with no models is refused too (TD-0101).
            MemberLocation.Class
            or MemberLocation.NestedClass
                when output == OutputKind.CodeGen
                    && type.IsReachableAsProtected
                    && visibility is Visibility.Public or Visibility.Protected => "'"
                + NameOf(type)
                + "' is reachable as protected, so its models are internal, which a method reachable from another "
                + "assembly cannot take or return",
            MemberLocation.Class or MemberLocation.NestedClass => null,
        };

    /// <summary>The visibility as C# writes it, which is also how a marker writes it.</summary>
    public static string KeywordOf(Visibility visibility) =>
        visibility switch
        {
            Visibility.Public => "public",
            Visibility.Internal => "internal",
            Visibility.Protected => "protected",
            Visibility.Private => "private",
        };

    /// <summary>The location as a marker writes it.</summary>
    public static string TextOf(MemberLocation location) =>
        location switch
        {
            MemberLocation.ExtensionClass => "extension-class",
            MemberLocation.Class => "class",
            MemberLocation.NestedClass => "nested-class",
        };

    /// <summary>The type in full, as a diagnostic of placement names it: <c>App.Sample&lt;T&gt;</c>.</summary>
    public static string NameOf(TargetType type) => TypeModels.ClaimingTypeOf(type).Name;

    private static string StaticReason(TargetType type) =>
        "'" + NameOf(type) + "' is a static class, which cannot have protected members";

    private static string StructReason(TargetType type) =>
        "'" + NameOf(type) + "' is a struct, which cannot have protected members";
}
