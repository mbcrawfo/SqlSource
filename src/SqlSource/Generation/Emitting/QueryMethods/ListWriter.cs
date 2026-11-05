using System.Globalization;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes what a query's method does for its lists, which send one parameter for each value: the documentation of the
/// engine's limit and the static local functions the method calls.  <see cref="ListCountWriter" /> writes the count of
/// each list and the check of the limit, <see cref="ListSqlWriter" /> each use of a list in the SQL, and
/// <see cref="ListBindingWriter" /> the binding of each value.
/// </summary>
/// <remarks>
/// Each list is read once: a value that is an <c>ICollection&lt;E&gt;</c> or an <c>IReadOnlyCollection&lt;E&gt;</c>
/// is counted and then enumerated, and any other sequence is copied into an array first.  A list sends its count of
/// parameters, padded under <see cref="QueryMethod.PadsLists" /> to the next of 1, 2, 4, … 512 and then to the next
/// multiple of 512, with its last value repeated, so that a few forms of the statement serve every count; an empty or
/// null list sends one, a typed <c>NULL</c>, for <c>(SELECT @ids_0 WHERE 1 = 0)</c>.  Padding never makes the
/// method pass the engine's limit: if the padded total would, the method sends the counts as they are, and it throws
/// <c>ArgumentException</c> when those pass the limit too.
/// </remarks>
internal static class ListWriter
{
    // The largest padded size that is a power of two; above it, sizes are its multiples.
    private const int PowerOfTwoSizes = 512;

    /// <summary>The culture every number of the generated code is written in.</summary>
    internal const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>
    /// The most parameters the engine takes in one query.
    /// </summary>
    public static int ParameterLimitOf(DatabaseEngine engine) => LimitOf(engine).Parameters;

    /// <summary>
    /// Appends the documentation of the limit, and of a list that changed after it was counted, for a method with a
    /// list.
    /// </summary>
    internal static void AppendDocumentation(in QueryMethodContext context, string indent)
    {
        if (context.Locals.Lists is null)
        {
            return;
        }

        XmlDocWriter.AppendException(
            context.Builder,
            indent,
            "global::System.ArgumentException",
            "The lists of <paramref name=\""
                + MethodArguments.Parameters
                + "\" /> hold more values than "
                + LimitOf(in context).Engine
                + " accepts in one query."
        );
        XmlDocWriter.AppendException(
            context.Builder,
            indent,
            "global::System.InvalidOperationException",
            "A list of <paramref name=\""
                + MethodArguments.Parameters
                + "\" /> changed between the call and the time its values were sent."
        );
    }

    /// <summary>
    /// Appends the static local functions that a method with a list calls, after its body: the one that reads a list
    /// once and counts it, the one that pads a size, under <see cref="QueryMethod.PadsLists" />, and the one that
    /// counts the digits of the indexes the SQL writes.
    /// </summary>
    internal static void AppendHelpers(in QueryMethodContext context, string indent)
    {
        if (context.Locals.Lists is not { } lists)
        {
            return;
        }

        var builder = context.Builder;
        var body = GeneratedText.Deeper(indent);
        var arm = GeneratedText.Deeper(body);
        var statement = GeneratedText.Deeper(arm);
        var element = lists.ElementType;
        var collection = "global::System.Collections.Generic.ICollection<" + element + ">";
        var readOnly = "global::System.Collections.Generic.IReadOnlyCollection<" + element + ">";
        _ = builder
            .Append('\n')
            .Append(indent)
            .Append("static int ")
            .Append(lists.CountOf)
            .Append('<')
            .Append(element)
            .Append(">(ref global::System.Collections.Generic.IEnumerable<")
            .Append(element)
            .Append(">? values)\n")
            .Append(indent)
            .Append("{\n")
            .Append(body)
            .Append("switch (values)\n")
            .Append(body)
            .Append("{\n")
            .Append(arm)
            .Append("case null:\n")
            .Append(statement)
            .Append("return 0;\n")
            .Append(arm)
            .Append("case ")
            .Append(collection)
            .Append(" collection:\n")
            .Append(statement)
            .Append("return collection.Count;\n")
            .Append(arm)
            .Append("case ")
            .Append(readOnly)
            .Append(" collection:\n")
            .Append(statement)
            .Append("return collection.Count;\n")
            .Append(arm)
            .Append("default:\n")
            .Append(statement)
            .Append("var array = global::System.Linq.Enumerable.ToArray(values);\n")
            .Append(statement)
            .Append("values = array;\n")
            .Append(statement)
            .Append("return array.Length;\n")
            .Append(body)
            .Append("}\n")
            .Append(indent)
            .Append("}\n");
        if (context.Method.PadsLists)
        {
            var powers = PowerOfTwoSizes.ToString(CultureInfo.InvariantCulture);
            var beforeMultiple = (PowerOfTwoSizes - 1).ToString(CultureInfo.InvariantCulture);
            _ = builder
                .Append('\n')
                .Append(indent)
                .Append("static int ")
                .Append(lists.PaddedSize)
                .Append("(int size) =>\n")
                .Append(body)
                .Append("size <= ")
                .Append(powers)
                .Append('\n')
                .Append(arm)
                .Append("? (int)global::System.Numerics.BitOperations.RoundUpToPowerOf2((uint)size)\n")
                .Append(arm)
                .Append(": (size + ")
                .Append(beforeMultiple)
                .Append(") / ")
                .Append(powers)
                .Append(" * ")
                .Append(powers)
                .Append(";\n");
        }

        _ = builder
            .Append('\n')
            .Append(indent)
            .Append("static int ")
            .Append(lists.DigitsUpTo)
            .Append("(int size)\n")
            .Append(indent)
            .Append("{\n")
            .Append(body)
            .Append("var digits = size;\n")
            .Append(body)
            .Append("for (var power = 10; power < size; power *= 10)\n")
            .Append(body)
            .Append("{\n")
            .Append(arm)
            .Append("digits += size - power;\n")
            .Append(body)
            .Append("}\n\n")
            .Append(body)
            .Append("return digits;\n")
            .Append(indent)
            .Append("}\n");
    }

    // What the method's engine takes in one query, and what its message of the limit says.
    internal static EngineLimit LimitOf(in QueryMethodContext context) => LimitOf(context.Method.Engine);

    // What the engine takes in one query, and what its message of the limit says.
    private static EngineLimit LimitOf(DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => new EngineLimit(65535, "PostgreSQL", ", or send them as an array with = ANY"),
            DatabaseEngine.SqlServer => new EngineLimit(2100, "SQL Server", ", or use a table-valued parameter"),
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    // The most parameters the engine takes in one query, its name as a message writes it, and the advice that ends the
    // message, after the property's name.
    internal readonly record struct EngineLimit(int Parameters, string Engine, string Advice);
}
