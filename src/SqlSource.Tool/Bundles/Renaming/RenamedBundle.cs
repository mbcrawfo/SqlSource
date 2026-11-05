using System.Collections.Generic;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>A bundle renamed, and the mapping that says what each replacement stands for.</summary>
/// <param name="Bundle">The renamed bundle, whose <c>bundle.json</c> says it is renamed.</param>
/// <param name="Mapping">
/// Each replacement with its class and its original, in the order they were made: the user's to keep, and never part
/// of the bundle.
/// </param>
internal sealed record RenamedBundle(Bundle Bundle, IReadOnlyList<MappedName> Mapping);
