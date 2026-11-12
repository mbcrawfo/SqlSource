using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// A value as text that does not depend on the culture or the run, so that what the two methods of a pair return can be
// compared as strings.  It uses nothing but the framework: a type of a library the harness does not reference, such as
// a NodaTime value or a NetTopologySuite geometry, is rendered by its own ToString.
internal static partial class Rendering
{
    private const int MaxDepth = 8;

    public static string Of(object? value) => Of(value, 0);

    // The message with every GUID replaced by "<guid>", since a driver puts its connection's id in it.
    public static string Masked(string message) => GuidPattern().Replace(message, "<guid>");

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    private static string Of(object? value, int depth)
    {
        var simple = SimpleOf(value);
        if (simple is not null)
        {
            return simple;
        }

        var type = value!.GetType();
        if (ItemsOfMemory(value, type) is { } memory)
        {
            return "[" + string.Join(", ", memory.Cast<object?>().Select(item => Of(item, depth + 1))) + "]";
        }

        if (HasOwnToString(type))
        {
            return value.ToString() ?? "null";
        }

        if (depth >= MaxDepth)
        {
            return "...";
        }

        return value switch
        {
            ITuple tuple => "("
                + string.Join(", ", Enumerable.Range(0, tuple.Length).Select(i => Of(tuple[i], depth + 1)))
                + ")",
            IEnumerable items => "["
                + string.Join(", ", items.Cast<object?>().Select(item => Of(item, depth + 1)))
                + "]",
            _ => ObjectOf(value, type, depth),
        };
    }

    // The rendering of a value that needs no recursion, or null for one that does.
    private static string? SimpleOf(object? value) =>
        value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            bool flag => flag ? "true" : "false",
            char letter => "'" + letter + "'",
            Enum item => item.GetType().Name + "." + item.ToString(),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            DateTime moment => moment.ToString("O", CultureInfo.InvariantCulture) + " (" + moment.Kind + ")",
            DateTimeOffset moment => moment.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
            Guid id => id.ToString("D", CultureInfo.InvariantCulture),
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            JsonElement element => element.GetRawText(),
            JsonDocument document => document.RootElement.GetRawText(),
            JsonNode node => node.ToJsonString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => null,
        };

    // The items of a Memory<T> or a ReadOnlyMemory<T>, such as the one a SqlClient vector holds, whose own ToString
    // names only its type and length; null for any other value.
    private static Array? ItemsOfMemory(object value, Type type) =>
        type.IsGenericType
        && (
            type.GetGenericTypeDefinition() == typeof(Memory<>)
            || type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>)
        )
            ? (Array?)type.GetMethod("ToArray", Type.EmptyTypes)?.Invoke(value, null)
            : null;

    // A type that writes itself: ToString is declared by a type other than object and ValueType, and is not the one the
    // compiler writes for a record.
    private static bool HasOwnToString(Type type)
    {
        var method = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (method?.DeclaringType is not { } declaring || declaring == typeof(object) || declaring == typeof(ValueType))
        {
            return false;
        }

        return !IsCompilerGenerated(type, method)
            && !typeof(ITuple).IsAssignableFrom(type)
            && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
    }

    private static bool IsCompilerGenerated(Type type, MethodInfo toString) =>
        type.GetMethod("<Clone>$") is not null
        || type.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance) is not null
        || toString.GetCustomAttribute<CompilerGeneratedAttribute>() is not null;

    private static string ObjectOf(object value, Type type, int depth)
    {
        var properties = ModelReflection
            .PropertiesOf(type)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .ToList();
        var text = new StringBuilder(TypeNameOf(type)).Append(" {");
        for (var i = 0; i < properties.Count; i++)
        {
            _ = text.Append(i == 0 ? " " : ", ")
                .Append(properties[i].Name)
                .Append(" = ")
                .Append(Of(properties[i].GetValue(value), depth + 1));
        }

        return text.Append(" }").ToString();
    }

    private static string TypeNameOf(Type type) =>
        type.IsGenericType
            ? type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]
                + "<"
                + string.Join(", ", type.GetGenericArguments().Select(TypeNameOf))
                + ">"
            : type.Name;
}
