namespace SqlSource.Generation.Models;

/// <summary>Every request of one full name, from every type and query that asks for a model of it.</summary>
/// <param name="FullName">The full name the requests share.</param>
/// <param name="Requests">The requests, in the order of <see cref="ModelSource.Comparison" />.  Never empty.</param>
internal sealed record ModelGroup(string FullName, EquatableArray<ModelRequest> Requests);
