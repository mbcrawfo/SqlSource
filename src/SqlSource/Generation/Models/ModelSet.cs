using SqlSource.Diagnostics;

namespace SqlSource.Generation.Models;

/// <summary>The models to emit, and the problems the collection of the requests found.</summary>
/// <param name="Models">
/// One model for each full name that can be generated, in the ordinal order of the full names.
/// </param>
/// <param name="Diagnostics">The problems of the requests, each at the query it is about.</param>
internal sealed record ModelSet(EquatableArray<Model> Models, EquatableArray<DiagnosticInfo> Diagnostics);
