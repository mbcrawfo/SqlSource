namespace SqlSource.Generation.Types;

/// <summary>
/// Whether a C# type that the type override names is found, and if not, why.  Of two failures in one name, the
/// first in this order is the name's.
/// </summary>
internal enum MappedTypeStatus
{
    /// <summary>The type is found, and can be a property's type.</summary>
    Found,

    /// <summary>No type of the name and arity is found.</summary>
    NotFound,

    /// <summary>
    /// Several types of the name and arity that the project can see are found across references, and none in the
    /// project.
    /// </summary>
    Ambiguous,

    /// <summary>The type, or one of its arguments, is not accessible from the project.</summary>
    NotAccessible,

    /// <summary>
    /// The type cannot be a property's type: a static class, a <c>ref struct</c>, an open generic,
    /// <c>System.Void</c>, <c>System.Nullable&lt;T&gt;</c> as the whole type, a type marked <c>[Obsolete]</c> as an
    /// error, whose use no pragma lets compile, or a type marked under an id that is not an identifier, whose
    /// diagnostic no pragma can name.
    /// </summary>
    NotAPropertyType,
}
