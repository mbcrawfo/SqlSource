using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
/// A member that is not found is a bug of the tool's pin, and not a condition of a server: <see cref="Resolve" />
/// throws, and so does the first use.
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

    private static readonly Members Found = Find();

    /// <summary>
    /// Finds every member this class reads, or throws.  The search runs once, from the static initializer, so a
    /// call after the first only touches its result.
    /// </summary>
    public static void Resolve() => _ = Found;

    /// <summary>
    /// The type modifier of each column of the reader's current row description, in column order, <c>-1</c> for a
    /// column without one.  A reader that has no row description has no columns.
    /// </summary>
    public static IReadOnlyList<int> ModifiersOf(NpgsqlDataReader reader)
    {
        if (Found.RowDescription.GetValue(reader) is not { } description)
        {
            return [];
        }

        var count = (int)Found.Count.GetValue(description)!;
        var modifiers = new int[count];
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            modifiers[ordinal] = ((FieldDescription)Found.Item.GetValue(description, [ordinal])!).TypeModifier;
        }

        return modifiers;
    }

    private static Members Find()
    {
        var field =
            typeof(NpgsqlDataReader).GetField("RowDescription", Instance)
            ?? throw Missing("the field NpgsqlDataReader.RowDescription");
        var count =
            field.FieldType.GetProperty("Count", Instance)
            ?? throw Missing($"the property {field.FieldType.Name}.Count");
        var item =
            field.FieldType.GetProperty("Item", Instance, null, typeof(FieldDescription), [typeof(int)], null)
            ?? throw Missing($"the indexer of {field.FieldType.Name}");
        if (count.PropertyType != typeof(int))
        {
            throw Missing($"an integer {field.FieldType.Name}.Count");
        }

        return new Members(field, count, item);
    }

    private static InvalidOperationException Missing(string member) =>
        new(
            $"Npgsql {typeof(NpgsqlDataReader).Assembly.GetName().Version!.ToString(3)} has no {member}, "
                + "which the describer reads the type modifiers from"
        );

    private sealed record Members(FieldInfo RowDescription, PropertyInfo Count, PropertyInfo Item);
}
