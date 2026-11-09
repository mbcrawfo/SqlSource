using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Diagnostics;
using SqlSource.Generation.Placement;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the generated file of one type.
/// </summary>
internal static class TypeEmitter
{
    // A query can be named like a member the type inherits: "GetType" hides object.GetType in the nested class, and
    // on the type a query can hide a member of a base class.  That is what was asked for, and the compiler's request
    // to add "new" (CS0108) is one that nobody can act on in generated code.  Neither is its request to add "new" or
    // "override" (CS0114) to a method that has the name and the parameters of an inherited virtual method.  A nested
    // class can be named in lower case, as "queries", or "record", which are legal names that a user chose and which
    // the compiler warns of (CS8981, CS8860) at each part of the class, the generated ones too; the user's own type
    // with such a name is warned of in the user's file all the same.  A file of methods on the type starts with the
    // same lines.
    internal const string HeaderLines =
        GeneratedText.Header
        + "#pragma warning disable CS0108, CS0114 // A query can have the name of an inherited member.\n"
        + "#pragma warning disable CS8860, CS8981 // A nested class can be named in lower case, or record.\n";

    // The lines, and the empty line after them.
    internal const string Header = HeaderLines + "\n";

    // A file may ask for protected members of a sealed type, which is legal C#.  The compiler's warning (CS0628) is
    // about that choice, and would fail a project that treats warnings as errors on a file it did not write.  A file of
    // methods that writes such a member starts with the same line after the header's.
    internal const string ProtectedPragma =
        "#pragma warning disable CS0628 // A file asks for protected members of a sealed type.\n";

    /// <summary>
    /// Writes the file of <paramref name="input" />.
    /// </summary>
    /// <param name="input">The type and its parsed files.</param>
    public static TypeOutput Emit(TypeQueries input)
    {
        if (input.Type.Diagnostics.Count > 0)
        {
            return new TypeOutput(input.HintName, null, input.Type.Diagnostics);
        }

        // The placement's diagnostics are reported here alone: the methods' planner runs it again without them.
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var queries = UsableQueries.Of(input, diagnostics);
        return new TypeOutput(
            input.HintName,
            Write(input, queries),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
    }

    // The members on the type, each with its own accessibility, then each nested class that holds a member, with its
    // accessibility and its members public, then the empty nested class of a type with no usable query.  A query whose
    // output is Methods gets no member here, whether it gets a method or not: the method, in a file of its own, takes
    // the place of its constant, and a nested class that holds only methods is written there alone.
    private static string Write(TypeQueries input, UsableQuerySequence queries)
    {
        var type = input.Type.Type;
        var placement = queries.Placement;
        var (onType, inClasses) = SelectMembers(input, queries);
        var builder = new StringBuilder(HeaderLines);
        if (type.IsSealed && WritesProtected(placement, onType, inClasses))
        {
            _ = builder.Append(ProtectedPragma);
        }

        _ = builder.Append('\n');
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

        var isFirst = true;
        foreach (var member in onType ?? [])
        {
            AppendMember(builder, depth, member, member.Placement.Visibility, ref isFirst);
        }

        for (var index = 0; index < placement.NestedClasses.Count; index++)
        {
            if (inClasses?[index] is not { } members)
            {
                continue;
            }

            Separate(builder, ref isFirst);
            var nestedClass = placement.NestedClasses[index];
            OpenNestedClass(builder, depth, nestedClass, PlacementChecks.NameOf(type));
            var isFirstInClass = true;
            foreach (var member in members)
            {
                AppendMember(builder, depth + 1, member, Visibility.Public, ref isFirstInClass);
            }

            _ = builder.Append(GetIndent(depth)).Append("}\n");
        }

        if (placement.EmptyNestedClass is { } empty)
        {
            Separate(builder, ref isFirst);
            OpenNestedClass(builder, depth, new NestedClassPlacement(empty, Visibility.Private), string.Empty);
            _ = builder.Append(GetIndent(depth)).Append("}\n");
        }

        while (depth > 0)
        {
            _ = builder.Append(GetIndent(--depth)).Append("}\n");
        }

        return builder.ToString();
    }

    // The queries of the files the type can use that get a member here, each with its file's name, its settings and its
    // placement: those on the type, and those of each nested class by its index among the placement's, null for one
    // that holds only methods.  Each list is made only when it gets a member.
    private static (List<Member>? OnType, List<Member>?[]? InClasses) SelectMembers(
        TypeQueries input,
        UsableQuerySequence queries
    )
    {
        var classes = queries.Placement.NestedClasses;
        List<Member>? onType = null;
        List<Member>?[]? inClasses = null;
        foreach (var usable in queries)
        {
            if (usable.Settings.Output == OutputKind.Methods)
            {
                continue;
            }

            // A query that has no method and asks for the extension class leaves its file out, so it never comes here.
            var member = new Member(
                usable.Query,
                input.Files[usable.FileIndex].FileName,
                usable.Settings,
                usable.Placement
            );
            if (usable.Placement.NestedClass is { } nestedClass)
            {
                inClasses ??= new List<Member>?[classes.Count];
                (inClasses[TypePlacement.IndexOf(classes, nestedClass)] ??= []).Add(member);
            }
            else
            {
                (onType ??= []).Add(member);
            }
        }

        return (onType, inClasses);
    }

    // Whether the file writes a protected member on the type or a protected nested class.
    private static bool WritesProtected(TypePlacement placement, List<Member>? onType, List<Member>?[]? inClasses)
    {
        foreach (var member in onType ?? [])
        {
            if (member.Placement.Visibility == Visibility.Protected)
            {
                return true;
            }
        }

        for (var index = 0; index < placement.NestedClasses.Count; index++)
        {
            if (inClasses?[index] is not null && placement.NestedClasses[index].Visibility == Visibility.Protected)
            {
                return true;
            }
        }

        return false;
    }

    // One member at the depth, with an empty line before it unless it is the first of its block.
    private static void AppendMember(
        StringBuilder builder,
        int depth,
        Member member,
        Visibility accessibility,
        ref bool isFirst
    )
    {
        Separate(builder, ref isFirst);
        var indent = GetIndent(depth);
        var (query, fileName, settings, _) = member;
        var summaryXml = GetSummaryXml(query, fileName);

        // The member is built from the form that is emitted.  A form that was not built is the other one.
        var segments = settings.KeepComments ? query.KeptSegments ?? query.Segments : query.Segments;
        if (HasToken(segments))
        {
            MethodWriter.Append(
                builder,
                indent,
                summaryXml,
                query.Name,
                segments,
                settings.ValidateTokens,
                accessibility
            );
            return;
        }

        // Without a token the SQL is its text, a list's uses as written.
        var sql = MethodWriter.GetSql(segments);
        XmlDocWriter.AppendMember(builder, indent, summaryXml, sql);
        _ = builder
            .Append(indent)
            .Append(PlacementChecks.KeywordOf(accessibility))
            .Append(" const string ")
            .Append(query.Name)
            .Append(" = ")
            .Append(SymbolDisplay.FormatLiteral(sql, quote: true))
            .Append(";\n");
    }

    /// <summary>
    /// Opens a nested class of the type at the depth, <c>static partial</c> with its accessibility, as both of a
    /// type's files write it.  A public or protected one has a summary, since a project that documents its API would
    /// otherwise get CS1591 for it in a file it did not write.
    /// </summary>
    /// <param name="builder">The file.</param>
    /// <param name="depth">The depth of the class.</param>
    /// <param name="nestedClass">The class's name and visibility.</param>
    /// <param name="typeName">The type in full, as <c>App.Sample&lt;T&gt;</c>.</param>
    internal static void OpenNestedClass(
        StringBuilder builder,
        int depth,
        NestedClassPlacement nestedClass,
        string typeName
    )
    {
        if (nestedClass.Visibility is Visibility.Public or Visibility.Protected)
        {
            var indent = GetIndent(depth);
            _ = builder
                .Append(indent)
                .Append("/// <summary>\n")
                .Append(indent)
                .Append("/// The members of the queries of <c>")
                .Append(XmlDocWriter.Escape(typeName))
                .Append("</c> that its files put in this class.\n")
                .Append(indent)
                .Append("/// </summary>\n");
        }

        Open(
            builder,
            depth,
            PlacementChecks.KeywordOf(nestedClass.Visibility) + " static partial class " + nestedClass.Name
        );
    }

    /// <summary>Writes an empty line unless what follows is the first of its block.</summary>
    internal static void Separate(StringBuilder builder, ref bool isFirst)
    {
        if (!isFirst)
        {
            _ = builder.Append('\n');
        }

        isFirst = false;
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
    internal static string GetIndent(int depth) => GeneratedText.IndentOf(depth);

    // A query that gets a member here, with its file's name, its settings and its placement.
    private readonly record struct Member(
        SqlQuery Query,
        string FileName,
        QuerySettings Settings,
        QueryPlacement Placement
    );
}
