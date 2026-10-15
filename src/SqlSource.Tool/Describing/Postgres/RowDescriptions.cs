using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using Npgsql;
using Npgsql.BackendMessages;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Reads the type modifier of each column from the row description Npgsql received, which no public API of the
/// driver gives: <c>NpgsqlDbColumn</c> keeps the precision, the scale and the size a modifier decodes to, and not
/// the modifier.
/// </summary>
/// <remarks>
/// <para>
/// The row description is the internal field <c>RowDescription</c> of <see cref="NpgsqlDataReader" />, of the
/// internal type <c>RowDescriptionMessage</c>, with a <c>Count</c> and an indexer of public
/// <see cref="FieldDescription" />, whose <c>TypeModifier</c> is public.  This reaches the field, the count and the
/// indexer by reflection.  <c>Directory.Packages.props</c> pins the Npgsql of the tool, so the members are those of
/// one version; <c>RowDescriptionsTests</c> fails on a bump that moves one, before a session depends on it.
/// </para>
/// <para>
/// A member that is not found, and a row description that is missing or does not fit the reader's columns, are a bug
/// of the tool's pin and not a condition of a server: <see cref="Resolve" /> and the first use throw an
/// <see cref="InvalidOperationException" /> that names <c>NpgsqlDataReader.RowDescription</c> and the version of
/// Npgsql.
/// </para>
/// </remarks>
internal static class RowDescriptions
{
    [SuppressMessage(
        "Major Code Smell",
        "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "Npgsql keeps the row description in an internal field and offers no public way to the "
            + "type modifier; the pinned version and RowDescriptionsTests hold the access."
    )]
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Once for the process; a failure is kept and thrown again, as itself.
    private static readonly Lazy<Members> Found = new(Find, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Finds every member this class reads, or throws an <see cref="InvalidOperationException" />.  The search runs
    /// once, so a call after the first only reads its result.
    /// </summary>
    public static void Resolve() => _ = Found.Value;

    /// <summary>
    /// The type modifier of each column of the reader's current row description, in column order, <c>-1</c> for a
    /// column without one.  A reader with no columns has no row description and gives none.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The reader has columns and the row description is null or does not count them.
    /// </exception>
    public static IReadOnlyList<int> ModifiersOf(NpgsqlDataReader reader)
    {
        var members = Found.Value;
        var columns = reader.FieldCount;
        var description = members.RowDescription.GetValue(reader);
        if (description is null)
        {
            return columns == 0
                ? []
                : throw Problem(
                    $"NpgsqlDataReader.RowDescription is null after a describe that gave {columns} columns"
                );
        }

        var count = (int)members.Count.GetValue(description)!;
        if (count != columns)
        {
            throw Problem($"NpgsqlDataReader.RowDescription holds {count} fields for {columns} columns");
        }

        var modifiers = new int[count];
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            modifiers[ordinal] = ((FieldDescription)members.Item.GetValue(description, [ordinal])!).TypeModifier;
        }

        return modifiers;
    }

    private static Members Find()
    {
        var field =
            typeof(NpgsqlDataReader).GetField("RowDescription", Instance)
            ?? throw Problem("there is no field NpgsqlDataReader.RowDescription");
        var count =
            field.FieldType.GetProperty("Count", Instance)
            ?? throw Problem($"there is no property {field.FieldType.Name}.Count");
        var item =
            field.FieldType.GetProperty("Item", Instance, null, typeof(FieldDescription), [typeof(int)], null)
            ?? throw Problem($"there is no indexer of {field.FieldType.Name}");
        if (count.PropertyType != typeof(int))
        {
            throw Problem($"{field.FieldType.Name}.Count is not an integer");
        }

        return new Members(field, count, item);
    }

    private static InvalidOperationException Problem(string what) => new(MessageOf(what));

    /// <summary>
    /// The message of a failure to read the row description: what is wrong, then where the describer reads the
    /// type modifiers from and the version of Npgsql that it is.
    /// </summary>
    internal static string MessageOf(string what) =>
        $"{what}; the describer reads the type modifiers from NpgsqlDataReader.RowDescription, "
        + $"and Npgsql is {typeof(NpgsqlDataReader).Assembly.GetName().Version!.ToString(3)}";

    private sealed record Members(FieldInfo RowDescription, PropertyInfo Count, PropertyInfo Item);
}
