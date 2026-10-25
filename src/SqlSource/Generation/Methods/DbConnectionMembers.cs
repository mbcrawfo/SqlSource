using System.Collections.Immutable;

namespace SqlSource.Generation.Methods;

/// <summary>
/// The names of the public members of <c>System.Data.Common.DbConnection</c> and of the types it derives from,
/// <c>object</c> included, on .NET.  A call of an extension method of one of these names on a connection would find the
/// member instead, so no extension method takes one (<c>SQLSRC274</c>).
/// </summary>
/// <remarks>
/// The generator runs in the compiler, whose runtime is not the project's, so the list is written out.
/// <c>DbConnectionMembersTests</c> compares it with the runtime the tests run on.
/// </remarks>
internal static class DbConnectionMembers
{
    /// <summary>The names, sorted ordinally.</summary>
    public static EquatableArray<string> Names { get; } =
        new(
            ImmutableArray.Create(
                "BeginTransaction",
                "BeginTransactionAsync",
                "CanCreateBatch",
                "ChangeDatabase",
                "ChangeDatabaseAsync",
                "Close",
                "CloseAsync",
                "ConnectionString",
                "ConnectionTimeout",
                "Container",
                "CreateBatch",
                "CreateCommand",
                "DataSource",
                "Database",
                "Dispose",
                "DisposeAsync",
                "Disposed",
                "EnlistTransaction",
                "Equals",
                "GetHashCode",
                "GetLifetimeService",
                "GetSchema",
                "GetSchemaAsync",
                "GetType",
                "InitializeLifetimeService",
                "Open",
                "OpenAsync",
                "ReferenceEquals",
                "ServerVersion",
                "Site",
                "State",
                "StateChange",
                "ToString"
            )
        );
}
