using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the start of a query's method, the first of <see cref="QueryMethodWriter" />'s phases: its documentation
/// comment, its signature, the checks of its arguments and, for a method that streams, the return of its iterator's
/// call and the iterator's signature.  <see cref="CommandWriter" /> writes what follows.  It also writes the method's
/// member of a repository's interface, its documentation comment and its signature alone.
/// </summary>
internal static class HeaderWriter
{
    // The exception as its documentation names it: a documentation ID, which the compiler does not bind.  A project
    // that uses a referenced copy of the exceptions and references another assembly's copy too would find a bound cref
    // ambiguous (CS0419), since a cref is bound without regard to accessibility.
    private const string ExceptionCref = "T:SqlSource.UnexpectedRowCountException";

    private const string ItemExceptionCref = "T:SqlSource.CollectionItemException";

    internal static void AppendDocumentation(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var method = context.Method;
        XmlDocWriter.AppendMember(
            builder,
            indent,
            method.Batch is null ? method.SummaryXml : method.SummaryXml + "\n" + BatchSummaryOf(method.Shape),
            MethodWriter.GetSql(method.Segments)
        );
        foreach (var argument in context.Arguments)
        {
            if (argument.DocumentationXml is { } documentationXml)
            {
                XmlDocWriter.AppendParam(builder, indent, argument.Name, documentationXml);
            }
            else
            {
                XmlDocWriter.AppendParam(builder, indent, argument.Name);
            }
        }

        // A synchronous method of no result returns void, and documents no return.
        if (method.Shape != ResultShape.None || method.Synchrony == MethodSynchrony.Asynchronous)
        {
            XmlDocWriter.AppendReturns(
                builder,
                indent,
                method.Batch is not null
                    ? BatchReturnsOf(method.Shape)
                    : method.Shape switch
                    {
                        ResultShape.Many when context.Streams =>
                            "The rows the query returns, each read as the loop asks for it.",
                        ResultShape.Many => "The rows the query returned.",
                        ResultShape.One => "The row.",
                        ResultShape.OneOptional => "The row, or null when there is none.",
                        ResultShape.RowCount => "The number of rows the query affected.",
                        ResultShape.None => "A task that completes when the query has run.",
                        _ => throw UnexpectedEnumValueException.Of(method.Shape),
                    }
            );
        }
        if (method.Batch is not null && method.Shape is ResultShape.One or ResultShape.OneOptional)
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                ExceptionCref,
                method.Shape == ResultShape.One
                    ? "An item returned no rows, or more than one.  Its <c>ItemIndex</c> is the item's position, "
                        + "counted from 0."
                    : "An item returned more than one row.  Its <c>ItemIndex</c> is the item's position, counted "
                        + "from 0."
            );
        }
        else if (method.Shape == ResultShape.One)
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                ExceptionCref,
                "The query returned no rows, or more than one."
            );
        }
        else if (method.Shape == ResultShape.OneOptional)
        {
            XmlDocWriter.AppendException(builder, indent, ExceptionCref, "The query returned more than one row.");
        }

        if (method.Batch is not null)
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                ItemExceptionCref,
                "The database raised an error for an item.  Its <c>InnerException</c> is the database's exception and "
                    + "its <c>ItemIndex</c> is the item's position, counted from 0."
            );
        }

        ListWriter.AppendDocumentation(in context, indent);
        ExecutionCapabilityWriter.AppendDocumentation(in context, indent);
    }

    // What a collection method does, by what its query returns, after the query's own summary.
    private static string BatchSummaryOf(ResultShape shape) =>
        shape switch
        {
            ResultShape.Many or ResultShape.One or ResultShape.OneOptional =>
                "Runs the query once for each item of <paramref name=\"parameters\" /> and returns the rows of every "
                    + "item, in the order of the items.",
            ResultShape.RowCount =>
                "Runs the query once for each item of <paramref name=\"parameters\" /> and returns the number of rows "
                    + "they affected.",
            ResultShape.None => "Runs the query once for each item of <paramref name=\"parameters\" />, in order.",
            _ => throw UnexpectedEnumValueException.Of(shape),
        };

    private static string BatchReturnsOf(ResultShape shape) =>
        shape switch
        {
            ResultShape.Many => "The rows of every item, in the order of the items.",
            ResultShape.One => "The row of each item, in the order of the items.",
            ResultShape.OneOptional => "The row of each item that returned one, in the order of the items.",
            ResultShape.RowCount => "The number of rows the query affected, summed over the items.",
            ResultShape.None => "A task that completes when the query has run for every item.",
            _ => throw UnexpectedEnumValueException.Of(shape),
        };

    internal static void AppendSignature(in QueryMethodContext context, string indent)
    {
        // A streaming method is not asynchronous itself: it returns its iterator.  It is never synchronous.
        var builder = context.Builder;
        _ = builder.Append(indent).Append(ModifiersOf(context.Host.Accessibility, context.Method.Source is null));
        if (context.Streams)
        {
            _ = builder.Append(' ').Append(RowsWriter.GetResultType(in context)).Append(' ');
        }
        else
        {
            _ = context.Spelling.AppendReturnType(builder, RowsWriter.GetResultType(in context)).Append(' ');
        }

        _ = builder.Append(context.Method.Name);
        context.Arguments.AppendDeclaration(builder, indent, context.Host);
    }

    // The signature of the method as a member of the repository's interface repeats it: no modifiers, the type it
    // returns, its name and its arguments with their defaults, then the semicolon that ends the member.
    internal static void AppendInterfaceSignature(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        _ = builder.Append(indent);
        if (context.Streams)
        {
            _ = builder.Append(RowsWriter.GetResultType(in context)).Append(' ');
        }
        else
        {
            _ = context.Spelling.AppendInterfaceReturnType(builder, RowsWriter.GetResultType(in context)).Append(' ');
        }

        _ = builder.Append(context.Method.Name);
        context.Arguments.AppendInterfaceDeclaration(builder, indent);
    }

    // The method's accessibility, then static unless it is a repository's instance method.
    private static string ModifiersOf(Visibility accessibility, bool isStatic) =>
        (accessibility, isStatic) switch
        {
            (Visibility.Public, true) => "public static",
            (Visibility.Public, false) => "public",
            (Visibility.Internal, true) => "internal static",
            (Visibility.Internal, false) => "internal",
            (Visibility.Protected, true) => "protected static",
            (Visibility.Protected, false) => "protected",
            (Visibility.Private, true) => "private static",
            (Visibility.Private, false) => "private",
            _ => throw UnexpectedEnumValueException.Of(accessibility),
        };

    // The receiver, which a repository's method does not have, and the input are refused when null, and under
    // validate-tokens each token's text when it is null or white space; then each list is read and counted, and the
    // number of parameters checked against the limit, except in a collection method, whose items are checked one by
    // one.
    internal static void AppendChecks(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        foreach (var argument in context.Arguments)
        {
            var check = argument.Role switch
            {
                ArgumentRole.Receiver or ArgumentRole.Input => "global::System.ArgumentNullException.ThrowIfNull(",
                ArgumentRole.Token => context.Method.ValidateTokens
                    ? "global::System.ArgumentException.ThrowIfNullOrWhiteSpace("
                    : null,
                ArgumentRole.Transaction or ArgumentRole.CommandTimeout or ArgumentRole.Cancellation => null,
                _ => throw UnexpectedEnumValueException.Of(argument.Role),
            };
            if (check is not null)
            {
                _ = builder.Append(indent).Append(check);
                MethodArguments.AppendName(builder, argument.Name);
                _ = builder.Append(");\n");
            }
        }

        // A collection method checks each item as it is added to its batch, which its body writes.
        if (context.Method.Batch is null)
        {
            ListCountWriter.AppendCounts(in context, indent);
        }
    }

    // A streaming method's return of a call to its iterator, after a blank line when checks come before it, then the
    // iterator's signature and the brace that opens its body, at the indent of the method's body.  A repository's
    // method of no input and no checked token has no check.  A struct repository's method reads its source, or copies
    // itself, first, into a local that the iterator captures.
    internal static void AppendIteratorCall(in QueryMethodContext context, string indent, bool afterChecks)
    {
        var builder = context.Builder;
        var arguments = context.Arguments;
        var locals = context.Locals;
        var captures = locals.Lists is not null || context.Method.Source is not null;
        if (afterChecks)
        {
            _ = builder.Append('\n');
        }

        if (locals.SourceLocal.Length > 0)
        {
            _ = builder
                .Append(indent)
                .Append("var ")
                .Append(locals.SourceLocal)
                .Append(" = ")
                .Append(locals.SourceValue)
                .Append(";\n");
        }

        _ = builder.Append(indent).Append("return ").Append(locals.Iterator).Append('(');
        arguments.AppendCall(builder);
        _ = builder
            .Append(");\n\n")
            .Append(indent)
            .Append(AsyncCallSpelling.IteratorModifiersOf(captures))
            .Append(RowsWriter.GetResultType(in context))
            .Append(' ')
            .Append(locals.Iterator);
        arguments.AppendIteratorDeclaration(builder, indent);
        _ = builder.Append(indent).Append("{\n");
    }
}
