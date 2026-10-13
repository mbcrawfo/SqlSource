using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

// What the describer of PostgreSQL passes through the exchange, in one file: plain records that a capture can hold.
// A method's name and a member's name are a contract with the captures that exist: a member is added, nullable,
// and none is renamed.  tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs holds each to the rule.

/// <summary>
/// The request of <c>open</c>.  It holds nothing: nothing of a connection may reach a capture.  Its one member is
/// static, so it is written as <c>{}</c>.
/// </summary>
internal sealed record PostgresOpenRequest
{
    public static PostgresOpenRequest Instance { get; } = new();
}

/// <summary>What a session knows of its server, or why none was opened.</summary>
/// <param name="ServerVersion">The server's <c>server_version</c>, cut at its first space.</param>
/// <param name="DriverVersion">The version of the Npgsql that asked.</param>
/// <param name="Settings">The settings of the session that change a description.</param>
/// <param name="Error">Why no session was opened, or null.</param>
internal sealed record PostgresOpenAnswer(
    string? ServerVersion,
    string? DriverVersion,
    IReadOnlyList<PostgresSetting>? Settings,
    PostgresServerError? Error
);

/// <summary>A setting of the session, as <c>current_setting</c> gives it.</summary>
internal sealed record PostgresSetting(string Name, string Value);

/// <summary>
/// What the server said of a call it refused, each part null where the server gave none.  A failure that is not
/// the server's, a connection that was refused for one, has the driver's message alone.
/// </summary>
/// <param name="SqlState">The five characters of the SQLSTATE.</param>
/// <param name="Message">The primary message.</param>
/// <param name="Detail">The detail.</param>
/// <param name="Hint">The hint.</param>
/// <param name="Position">The character of the text as sent that the error is at, counted from one.</param>
internal sealed record PostgresServerError(
    string? SqlState,
    string Message,
    string? Detail,
    string? Hint,
    int? Position
);

/// <summary>Everything that decides the answer of <c>prepare</c>.</summary>
/// <param name="Statement">The name of the prepared statement: a counter within its session, never random.</param>
/// <param name="Sql">The query's one statement, with <c>$n</c> in the place of each parameter.</param>
/// <param name="Types">
/// The type list: a declared type as written or <c>unknown</c>, for each parameter in order.
/// </param>
internal sealed record PostgresPrepareRequest(string Statement, string Sql, IReadOnlyList<string> Types);

/// <summary>The types the server resolved, or why it prepared nothing.</summary>
/// <param name="ParameterTypes">
/// <c>parameter_types</c> of <c>pg_prepared_statements</c> for the statement, as OIDs, in order.
/// </param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresPrepareAnswer(IReadOnlyList<long>? ParameterTypes, PostgresServerError? Error);

/// <summary>The request of <c>deallocate</c>: the statement to free.</summary>
internal sealed record PostgresDeallocateRequest(string Statement);

/// <summary>Null when the statement was freed.  An error here is logged and never reported.</summary>
internal sealed record PostgresDeallocateAnswer(PostgresServerError? Error);
