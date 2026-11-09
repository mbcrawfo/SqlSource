using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// What <see cref="ConnectionResolver" /> found for one repository: the connection of each database it could resolve,
/// the databases whose queries get no method, and the problems that say why.
/// </summary>
/// <param name="Sources">The connection of each database that has one, in the order of the needed databases.</param>
/// <param name="FailedDatabases">
/// The databases that have no connection the methods can use, in the order of the needed databases: a query of one
/// gets no method.
/// </param>
/// <param name="Diagnostics">The problems, at the type's attribute or at the member they are about.</param>
internal sealed record ConnectionResolution(
    EquatableArray<ResolvedConnection> Sources,
    EquatableArray<string> FailedDatabases,
    EquatableArray<DiagnosticInfo> Diagnostics
);
