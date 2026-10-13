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

/// <summary>The request of <c>types</c>.</summary>
/// <param name="Oids">The types to resolve, sorted and each once.</param>
internal sealed record PostgresTypesRequest(IReadOnlyList<long> Oids);

/// <summary>The rows of the closure, or why the catalog could not be read.</summary>
/// <param name="Rows">
/// One row for each type asked for and for every type they reach: the element of an array, the base of a domain,
/// the subtype of a range, the range of a multirange.  In <c>oid</c> order.
/// </param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresTypesAnswer(IReadOnlyList<PostgresTypeRow>? Rows, PostgresServerError? Error);

/// <summary>What the catalog says of one type.  Each member is named as the catalog names it.</summary>
/// <param name="Oid">The type.</param>
/// <param name="Typname">Its name in <c>pg_type</c>: <c>int4</c>, <c>_text</c>.</param>
/// <param name="Nspname">Its schema.</param>
/// <param name="Typtype"><c>b</c>, <c>c</c>, <c>d</c>, <c>e</c>, <c>m</c>, <c>p</c> or <c>r</c>.</param>
/// <param name="Typcategory">The category: <c>A</c> for an array.</param>
/// <param name="Typelem">The element of an array, or zero.</param>
/// <param name="Typbasetype">The base of a domain, or zero.</param>
/// <param name="Rngsubtype">The subtype of a range, or null for a type that is none.</param>
/// <param name="MultirangeRngsubtype">The subtype of a multirange's range, or null for a type that is none.</param>
/// <param name="FormatType"><c>format_type(oid, -1)</c>: the SQL spelling without a modifier.</param>
/// <param name="EnumLabels">The labels of an enum in their sort order, or null for a type that is none.</param>
internal sealed record PostgresTypeRow(
    long Oid,
    string Typname,
    string Nspname,
    string Typtype,
    string Typcategory,
    long Typelem,
    long Typbasetype,
    long? Rngsubtype,
    long? MultirangeRngsubtype,
    string FormatType,
    IReadOnlyList<string>? EnumLabels
);

/// <summary>Everything that decides the answer of <c>columns</c>.</summary>
/// <param name="Statement">The prepared statement to describe.</param>
/// <param name="Parameters">
/// How many parameters the query's list has: the statement is given a <c>NULL</c> for each.
/// </param>
internal sealed record PostgresColumnsRequest(string Statement, int Parameters);

/// <summary>The columns of a prepared statement, or why it could not be described.</summary>
/// <param name="Columns">The columns in order.  A statement without rows has none.</param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresColumnsAnswer(IReadOnlyList<PostgresColumnRow>? Columns, PostgresServerError? Error);

/// <summary>One column, as the row description of the protocol and Npgsql's reading of it hold it.</summary>
/// <param name="Ordinal">The column's place, from zero.</param>
/// <param name="Name">The name as the server returned it.</param>
/// <param name="TypeOid">The type.  The protocol reports the base of a domain, with the domain's modifier.</param>
/// <param name="TableOid">The table the column comes from, or zero.</param>
/// <param name="AttributeNumber">The column of that table, zero for none and below zero for a system column.</param>
/// <param name="Length">Npgsql's <c>ColumnSize</c>, or null.</param>
/// <param name="Precision">Npgsql's <c>NumericPrecision</c>, or null.</param>
/// <param name="Scale">Npgsql's <c>NumericScale</c>, or null.</param>
internal sealed record PostgresColumnRow(
    int Ordinal,
    string Name,
    long TypeOid,
    long TableOid,
    int AttributeNumber,
    int? Length,
    int? Precision,
    int? Scale
);
