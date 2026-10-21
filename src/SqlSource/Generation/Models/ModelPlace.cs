namespace SqlSource.Generation.Models;

/// <summary>
/// Where a model is emitted from: the query and the role of the request that gave the model, and the type that
/// asked for it.  It is how the models are arranged by file, so that a model added or removed moves only the models
/// of the same query and role whose types sort after its own.
/// </summary>
/// <param name="NormalizedPath">
/// The path of the query's file, in the form <c>SqlPath.Normalize</c> gives: that of the model's first request.
/// </param>
/// <param name="QueryIndex">The query's index among the queries of its file.</param>
/// <param name="Role">Whether the model is of the query's parameters or of a row.</param>
/// <param name="ClaimingType">The type that asked for the model, as <see cref="ModelSource.ClaimingType" />.</param>
internal sealed record ModelPlace(string NormalizedPath, int QueryIndex, ModelRole Role, string ClaimingType);
