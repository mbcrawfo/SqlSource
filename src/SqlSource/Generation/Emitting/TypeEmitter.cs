using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Diagnostics;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the generated file of one type.
/// </summary>
internal static class TypeEmitter
{
    // A query can be named like a member the type inherits: "GetType" hides object.GetType in the nested class, and
    // under SqlLocation.Direct a query can hide a member of a base class.  That is what was asked for, and the
    // compiler's request to add "new" (CS0108) is one that nobody can act on in generated code.  Neither is its
    // request to add "new" or "override" (CS0114) to a method that has the name and the parameters of an inherited
    // virtual method.  A file of methods on the type starts with the same lines.
    internal const string HeaderLines =
        GeneratedText.Header
        + "#pragma warning disable CS0108, CS0114 // A query can have the name of an inherited member.\n";

    // The lines, and the empty line after them.
    internal const string Header = HeaderLines + "\n";

    /// <summary>
    /// Writes the file of <paramref name="input" />.
    /// </summary>
    /// <param name="input">The type and its parsed files.</param>
    public static TypeOutput Emit(TypeQueries input)
    {
        var type = input.Type.Type;
        if (input.Type.Diagnostics.Count > 0)
        {
            return new TypeOutput(input.HintName, null, input.Type.Diagnostics);
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var members = SelectMembers(input, diagnostics, out var hasCodeGen);
        return new TypeOutput(
            input.HintName,
            Write(type, members, hasCodeGen),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
    }

    // The queries of the files the type can use that get a member here, each with its file's name and its settings.  A
    // query whose output is CodeGen gets none, whether it gets a method or not: the method, in a file of its own, takes
    // the place of its constant.  hasCodeGen says whether one was left out so.
    private static List<(SqlQuery Query, string FileName, QuerySettings Settings)> SelectMembers(
        TypeQueries input,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out bool hasCodeGen
    )
    {
        var members = new List<(SqlQuery Query, string FileName, QuerySettings Settings)>();
        hasCodeGen = false;
        foreach (var usable in UsableQueries.Of(input, diagnostics))
        {
            if (usable.Settings.Output == OutputKind.CodeGen)
            {
                hasCodeGen = true;
            }
            else
            {
                members.Add((usable.Query, input.Files[usable.FileIndex].FileName, usable.Settings));
            }
        }

        return members;
    }

    // Under SqlLocation.Nested the nested class is not written when the type's usable queries are all CodeGen, so
    // that a method may have its name, as the methods' planner expects.  A type with no usable query at all keeps it
    // empty, so that a reference to it reports the missing member and not a missing type.
    private static string Write(
        TargetType type,
        List<(SqlQuery Query, string FileName, QuerySettings Settings)> members,
        bool hasCodeGen
    )
    {
        var builder = new StringBuilder(Header);
        var depth = 0;
        if (type.Namespace.Length > 0)
        {
            Open(builder, depth++, "namespace " + type.Namespace);
        }

        foreach (var declaration in type.Types)
        {
            Open(
                builder,
                depth++,
                "partial " + declaration.Keyword + " " + declaration.Name + declaration.TypeParameters
            );
        }

        if (type.Placement == MemberPlacement.Nested && (members.Count > 0 || !hasCodeGen))
        {
            Open(builder, depth++, "private static class " + type.NestedClass);
        }

        var indent = GetIndent(depth);
        for (var index = 0; index < members.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append('\n');
            }

            var (query, fileName, settings) = members[index];
            var summaryXml = GetSummaryXml(query, fileName);

            // The member is built from the form that is emitted.  A form that was not built is the other one.
            var segments = settings.KeepComments ? query.KeptSegments ?? query.Segments : query.Segments;
            if (HasToken(segments))
            {
                MethodWriter.Append(builder, indent, summaryXml, query.Name, segments, settings.ValidateTokens);
                continue;
            }

            // Without a token the SQL is one literal segment.
            var sql = segments[0].Text;
            XmlDocWriter.AppendMember(builder, indent, summaryXml, sql);
            _ = builder
                .Append(indent)
                .Append("public const string ")
                .Append(query.Name)
                .Append(" = ")
                .Append(SymbolDisplay.FormatLiteral(sql, quote: true))
                .Append(";\n");
        }

        while (depth > 0)
        {
            _ = builder.Append(GetIndent(--depth)).Append("}\n");
        }

        return builder.ToString();
    }

    private static bool HasToken(EquatableArray<SqlSegment> segments)
    {
        foreach (var segment in segments)
        {
            if (segment.Kind == SqlSegmentKind.Token)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The summary of a query's member, as escaped XML: its <c>-- summary:</c> text, or a sentence that names the query
    /// and its file.
    /// </summary>
    internal static string GetSummaryXml(SqlQuery query, string fileName) =>
        query.Summary is null
            ? "The <c>" + query.Name + "</c> query from <c>" + XmlDocWriter.Escape(fileName) + "</c>."
            : XmlDocWriter.Escape(query.Summary);

    /// <summary>Opens a block at the depth: its header, and a brace on the next line.</summary>
    internal static void Open(StringBuilder builder, int depth, string header)
    {
        var indent = GetIndent(depth);
        _ = builder.Append(indent).Append(header).Append('\n').Append(indent).Append("{\n");
    }

    /// <summary>The indentation of the depth.</summary>
    internal static string GetIndent(int depth) => string.Concat(Enumerable.Repeat(GeneratedText.Indent, depth));
}
