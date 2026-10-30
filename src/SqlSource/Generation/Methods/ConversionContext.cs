using System.Collections.Generic;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// What deciding the conversions of one query's columns and parameters reads, and where it reports.
/// </summary>
/// <param name="Engine">The engine of the query's file, whose base map says which types are foreign.</param>
/// <param name="Converters">The converters of the type's slice, in the order of the set.</param>
/// <param name="At">The query's name.</param>
/// <param name="Problems">The query's problems, which conversions add theirs to, at <paramref name="At" />.</param>
internal readonly record struct ConversionContext(
    DatabaseEngine Engine,
    EquatableArray<Converter> Converters,
    LocationInfo At,
    List<DiagnosticInfo> Problems
);
