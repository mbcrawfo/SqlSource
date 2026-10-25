using SqlSource.Diagnostics;

namespace SqlSource.Generation.Models;

/// <summary>The models to emit, and the problems the collection of the requests found.</summary>
/// <param name="Models">
/// One model for each full name that can be generated, in the ordinal order of the full names.
/// </param>
/// <param name="Places">Where each model is emitted from: <c>Places[i]</c> is that of <c>Models[i]</c>.</param>
/// <param name="Diagnostics">The problems of the requests, each at the query it is about.</param>
/// <param name="Assignments">
/// The model that each request which joined a generated model got, ordered by <see cref="ModelAssignment.Order" />.
/// </param>
internal sealed record ModelSet(
    EquatableArray<Model> Models,
    EquatableArray<ModelPlace> Places,
    EquatableArray<DiagnosticInfo> Diagnostics,
    EquatableArray<ModelAssignment> Assignments
);
