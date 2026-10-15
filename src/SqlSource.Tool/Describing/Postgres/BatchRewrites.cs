using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Npgsql;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Reads what Npgsql sent for a command of a batch: its rewrite of the SQL, with <c>$n</c> in the place of each
/// parameter it found, and the parameters in the order of their numbers.  No public API of the driver gives either.
/// </summary>
/// <remarks>
/// <para>
/// The rewrite is the internal property <c>FinalCommandText</c> of <see cref="NpgsqlBatchCommand" />, and the order
/// is its internal property <c>PositionalParameters</c>, a list of <see cref="NpgsqlParameter" />.  This reaches both
/// by reflection.  <c>Directory.Packages.props</c> pins the Npgsql of the tool, so the members are those of one
/// version; <c>BatchRewritesTests</c> fails on a bump that moves one, before a session depends on it.
/// </para>
/// <para>
/// A member that is not found, and a value that is null after a describe, are a bug of the tool's pin and not a
/// condition of a server: <see cref="Resolve" /> and the first use throw an <see cref="InvalidOperationException" />
/// that names the member and the version of Npgsql.
/// </para>
/// </remarks>
internal static class BatchRewrites
{
    [SuppressMessage(
        "Major Code Smell",
        "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "Npgsql keeps the text it sent and the order of its parameters in internal properties and "
            + "offers no public way to them; the pinned version and BatchRewritesTests hold the access."
    )]
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const string TextMember = "FinalCommandText";
    private const string ParametersMember = "PositionalParameters";

    // Once for the process; a failure is kept and thrown again, as itself.
    private static readonly Lazy<Members> Found = new(Find, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Finds every member this class reads, or throws an <see cref="InvalidOperationException" />.  The search runs
    /// once, so a call after the first only reads its result.
    /// </summary>
    public static void Resolve() => _ = Found.Value;

    /// <summary>
    /// The text Npgsql sent for the command and the name of each parameter it numbered, in the order of the numbers.
    /// </summary>
    /// <exception cref="InvalidOperationException">Either member is null: the command was not sent.</exception>
    public static PostgresCheckSent Of(NpgsqlBatchCommand command)
    {
        var members = Found.Value;
        var text =
            (string?)members.Text.GetValue(command)
            ?? throw Problem($"NpgsqlBatchCommand.{TextMember}", "is null after a describe");
        var parameters =
            (IEnumerable<NpgsqlParameter>?)members.Parameters.GetValue(command)
            ?? throw Problem($"NpgsqlBatchCommand.{ParametersMember}", "is null after a describe");
        return new PostgresCheckSent(text, [.. parameters.Select(static parameter => parameter.ParameterName)]);
    }

    private static Members Find()
    {
        var text =
            typeof(NpgsqlBatchCommand).GetProperty(TextMember, Instance)
            ?? throw Problem($"NpgsqlBatchCommand.{TextMember}", "is no property");
        var parameters =
            typeof(NpgsqlBatchCommand).GetProperty(ParametersMember, Instance)
            ?? throw Problem($"NpgsqlBatchCommand.{ParametersMember}", "is no property");
        if (text.PropertyType != typeof(string))
        {
            throw Problem($"NpgsqlBatchCommand.{TextMember}", "is not a string");
        }

        if (!typeof(IEnumerable<NpgsqlParameter>).IsAssignableFrom(parameters.PropertyType))
        {
            throw Problem($"NpgsqlBatchCommand.{ParametersMember}", "is not a list of parameters");
        }

        return new Members(text, parameters);
    }

    private static InvalidOperationException Problem(string member, string what) => new(MessageOf(member, what));

    /// <summary>
    /// The message of a failure to read what Npgsql sent: the member, what is wrong with it, and the version of
    /// Npgsql.
    /// </summary>
    internal static string MessageOf(string member, string what) =>
        $"{member} {what}; the describer reads what Npgsql sent from it, "
        + $"and Npgsql is {typeof(NpgsqlBatchCommand).Assembly.GetName().Version!.ToString(3)}";

    private sealed record Members(PropertyInfo Text, PropertyInfo Parameters);
}
