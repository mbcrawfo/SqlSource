using System.Collections.Generic;

namespace SqlSource.Tool.Describing.SqlServer;

// What the describer of SQL Server passes through the exchange, in one file: plain records that a capture can hold.
// A method's name and a member's name are a contract with the captures that exist: a member is added, nullable,
// and none is renamed.  tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs holds each to the rule.

/// <summary>
/// The request of <c>open</c>.  It holds nothing: nothing of a connection may reach a capture.  Its one member is
/// static, so it is written as <c>{}</c>.
/// </summary>
internal sealed record SqlServerOpenRequest
{
    public static SqlServerOpenRequest Instance { get; } = new();
}

/// <summary>What a session knows of its server, or why none was opened.</summary>
/// <param name="ProductVersion"><c>SERVERPROPERTY('ProductVersion')</c>: <c>14.0.3560.2</c>.</param>
/// <param name="EngineEdition">
/// <c>SERVERPROPERTY('EngineEdition')</c>: 5 for an Azure SQL Database and 8 for an Azure SQL Managed Instance, which
/// report a version of their own.
/// </param>
/// <param name="DriverVersion">The version of the Microsoft.Data.SqlClient that asked.</param>
/// <param name="Settings">The settings of the database and the session that change a description.</param>
/// <param name="Error">Why no session was opened, or null.</param>
internal sealed record SqlServerOpenAnswer(
    string? ProductVersion,
    int? EngineEdition,
    string? DriverVersion,
    IReadOnlyList<SqlServerSetting>? Settings,
    SqlServerServerError? Error
);

/// <summary>
/// A setting of the database or the session: <c>compatibility_level</c> as its number, <c>ANSI_NULLS</c> and
/// <c>QUOTED_IDENTIFIER</c> as <c>ON</c> or <c>OFF</c>, and <c>collation</c> as the server names it.
/// </summary>
internal sealed record SqlServerSetting(string Name, string Value);

/// <summary>
/// What the server or SqlClient said of a call it refused: every error of the exception, in its order.  An error of
/// <c>open</c>, and one of a connection that was lost or timed out, keep the numbers and have a fixed text of the
/// describer's own in place of the message (<see cref="ConnectionErrors" />).
/// </summary>
internal sealed record SqlServerServerError(IReadOnlyList<SqlServerErrorRow> Errors);

/// <summary>One error, as SQL Server numbers it.  The describer's own errors have 0 for each number.</summary>
/// <param name="Number">The error's number: <c>18456</c>, or <c>-2</c> for SqlClient's timeout.</param>
/// <param name="Class">Its severity, which SQL Server calls its level: 20 and above end the connection.</param>
/// <param name="State">Its state.</param>
/// <param name="Message">The message, or the describer's fixed text.</param>
internal sealed record SqlServerErrorRow(int Number, int Class, int State, string Message);
