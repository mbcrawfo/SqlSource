using SqlSource.Diagnostics;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Types;

/// <summary>The C# types of one query whose entry is usable, every one of which has a C# type.</summary>
/// <param name="Name">The query's name.</param>
/// <param name="Parameters">The type of each parameter of the entry, in order.</param>
/// <param name="Columns">The type of each column of the entry, in order, or null for an entry without rows.</param>
/// <param name="UnavailableLibraries">
/// The entry that turns each library on for the query, of each library whose assemblies the project does not reference
/// for the query's engine, in the order of <see cref="SqlLibrary" />: the query uses none of its rows.  Empty when
/// every library that is on can be used.
/// </param>
/// <param name="WithoutLibraryFailure">
/// The query's first failure without its libraries, or null when it has none: set when a library's row typed a
/// parameter or column that has no C# type without the library, which then has <c>Library</c> set and
/// <c>WithoutLibrary</c> null.  A claim whose output is not <c>CodeGen</c> gets no models of the query and this
/// diagnostic instead; the diagnostics of the file's types do not hold it.
/// </param>
internal sealed record QueryTypes(
    string Name,
    EquatableArray<PropertyType> Parameters,
    EquatableArray<PropertyType>? Columns,
    EquatableArray<LibrarySetting> UnavailableLibraries = default,
    DiagnosticInfo? WithoutLibraryFailure = null
);
