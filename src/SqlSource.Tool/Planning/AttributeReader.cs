using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Reads <c>[SqlSourceGenerate]</c> from the C# files of a project, as syntax.  The generator's reader works on
/// symbols, and the tool has no compilation, so there is nothing of it to share.
/// </summary>
/// <remarks>
/// It matches the attribute by its name, or by a <c>using</c> alias of it in reach: the project's <c>Using</c> items,
/// a <c>global using</c> of any file, the file's own and a namespace's around the type.  It reads <c>Source</c> alone,
/// as it is written, and the type's name, which names its file when there is no <c>Source</c>: nothing else on the
/// attribute changes what is described, and any other argument, literal or not, is stepped over.  What it reads
/// differently from the generator is in <c>docs/tech-debt/TD-0031</c>: a type of the same name in another namespace.
/// The attribute on two partial declarations of one type is read at the first, in the order of the files, as the
/// generator reads it.  An attribute inside <c>#if</c> is read with the constants of the configuration the run
/// evaluated.  A <c>Source</c> outside ASCII in a file that is not UTF-8 is not read, nor a type's name outside ASCII
/// when there is no <c>Source</c>: on Windows the compiler reads that file in the system's code page, so the text here
/// would not be the text it resolves.
/// </remarks>
internal static class AttributeReader
{
    private const string ShortName = "SqlSourceGenerate";

    private const string LongName = "SqlSourceGenerateAttribute";

    private const string AttributeSuffix = "Attribute";

    public static ProjectClaims Read(ProjectManifest manifest, CancellationToken cancellationToken)
    {
        var options = ParseOptionsOf(manifest);
        var files = manifest.CompileFiles;

        // A file that cannot be read is the build's to report.  Most files do not hold the word, and are not parsed.
        // The aliases of the whole project are the manifest's and every global using of a file that holds the word,
        // since an alias of the attribute names it.
        var parsed = new Parsed?[files.Length];
        var projectAliases = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var alias in manifest.UsingAliases)
        {
            projectAliases[alias.Name] = NamesTheAttribute(SyntaxFactory.ParseName(alias.Target));
        }

        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Parse(files[index], [ShortName], options, cancellationToken) is { } file)
            {
                parsed[index] = file;
                AddAliases(
                    ((CompilationUnitSyntax)file.Root).Usings.Where(static directive =>
                        directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                    ),
                    projectAliases
                );
            }
        }

        // A file that uses a global alias of the attribute need not hold the word: it is parsed when it holds the
        // alias, or the alias without its Attribute, as [Gen] names GenAttribute.
        var words = projectAliases
            .Where(static alias => alias.Value)
            .Select(static alias => WithoutSuffix(alias.Key))
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        if (!words.IsEmpty)
        {
            for (var index = 0; index < files.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (parsed[index] is null)
                {
                    parsed[index] = Parse(files[index], words, options, cancellationToken);
                }
            }
        }

        var claims = ImmutableArray.CreateBuilder<TypeClaim>();
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();
        // The attribute on two partial declarations of one type, which the compiler rejects, is read at the first,
        // in the order of the files, as the generator reads the type's first attribute.
        var types = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < files.Length; index++)
        {
            if (parsed[index] is not { } file)
            {
                continue;
            }

            // Into a namespace and a type, for a nested type, and into nothing else: no body of a member is walked.
            foreach (
                var node in file.Root.DescendantNodes(static node =>
                    node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax
                )
            )
            {
                if (
                    TargetTypeReader.IsCandidate(node)
                    && node is TypeDeclarationSyntax declaration
                    && FindAttribute(declaration, projectAliases) is { } attribute
                    && types.Add(TypeKeyOf(declaration, files[index]))
                )
                {
                    ReadAttribute(declaration, attribute, files[index], file.CompilerFallsBack, claims, errors);
                }
            }
        }

        return new ProjectClaims(claims.ToImmutable(), errors.ToImmutable());
    }

    // The file parsed, when it can be read and holds one of the words, else null.
    private static Parsed? Parse(
        string path,
        ImmutableArray<string> words,
        CSharpParseOptions options,
        CancellationToken cancellationToken
    )
    {
        if (FileText.Read(path) is not { } contents)
        {
            return null;
        }

        var text = contents.Text.ToString();
        if (!words.Any(word => text.Contains(word, StringComparison.Ordinal)))
        {
            return null;
        }

        var root = CSharpSyntaxTree
            .ParseText(contents.Text, options, path, cancellationToken)
            .GetRoot(cancellationToken);
        return new Parsed(root, contents.CompilerFallsBack);
    }

    // Each alias of the directives, and whether it names the attribute, over what an outer scope said of its name.
    private static void AddAliases(IEnumerable<UsingDirectiveSyntax> directives, Dictionary<string, bool> aliases)
    {
        foreach (var directive in directives)
        {
            if (directive.Alias is { } alias && !directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            {
                aliases[alias.Name.Identifier.ValueText] =
                    directive.NamespaceOrType is NameSyntax target && NamesTheAttribute(target);
            }
        }
    }

    // A name of the attribute's type, however it is qualified.  The generic name of a type of the same name is not.
    private static bool NamesTheAttribute(NameSyntax name) =>
        name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == LongName,
            QualifiedNameSyntax qualified => NamesTheAttribute(qualified.Right),
            AliasQualifiedNameSyntax alias => NamesTheAttribute(alias.Name),
            _ => false,
        };

    // "GenAttribute" is written [Gen] too, so a file is searched for the shorter word.
    private static string WithoutSuffix(string alias) =>
        alias.Length > AttributeSuffix.Length && alias.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? alias[..^AttributeSuffix.Length]
            : alias;

    // The aliases in reach of a declaration: the project's, then its file's, then those of each namespace around it,
    // the innermost last, so that an inner alias of a name wins, as the compiler finds it first.
    private static Dictionary<string, bool> AliasesOf(SyntaxNode declaration, Dictionary<string, bool> projectAliases)
    {
        var aliases = new Dictionary<string, bool>(projectAliases, StringComparer.Ordinal);
        foreach (var scope in declaration.Ancestors().Reverse())
        {
            switch (scope)
            {
                case CompilationUnitSyntax unit:
                    AddAliases(
                        unit.Usings.Where(static directive =>
                            !directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                        ),
                        aliases
                    );
                    break;
                case BaseNamespaceDeclarationSyntax space:
                    AddAliases(space.Usings, aliases);
                    break;
                default:
                    break;
            }
        }

        return aliases;
    }

    // The type's full name, on which its partial declarations agree: the namespaces around it, joined by dots however
    // they are nested, then the types it is nested in and its own name, each with its arity, as a metadata name has
    // them.  A type that is file-local, or nested in one, is a type of its file alone.
    private static string TypeKeyOf(TypeDeclarationSyntax declaration, string path)
    {
        var namespaces = new List<string>();
        var types = new List<string>();
        var isFileLocal = false;
        for (SyntaxNode? node = declaration; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case TypeDeclarationSyntax type:
                    types.Add(
                        type.Identifier.ValueText
                            + "`"
                            + (type.TypeParameterList?.Parameters.Count ?? 0).ToString(CultureInfo.InvariantCulture)
                    );
                    isFileLocal |= type.Modifiers.Any(SyntaxKind.FileKeyword);
                    break;
                case BaseNamespaceDeclarationSyntax space:
                    namespaces.Add(
                        string.Join(
                            '.',
                            space
                                .Name.DescendantTokens()
                                .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
                                .Select(static token => token.ValueText)
                        )
                    );
                    break;
                default:
                    break;
            }
        }

        namespaces.Reverse();
        types.Reverse();
        var key = string.Join('.', namespaces) + "/" + string.Join('+', types);
        return isFileLocal ? path + "|" + key : key;
    }

    private sealed record Parsed(SyntaxNode Root, bool CompilerFallsBack);

    // The language version of the project, so that a word is a keyword where the compiler takes it for one, and the
    // project's constants, so that #if is read as the compiler reads it.
    private static CSharpParseOptions ParseOptionsOf(ProjectManifest manifest) =>
        new(
            manifest.LangVersion.Length > 0 && LanguageVersionFacts.TryParse(manifest.LangVersion, out var version)
                ? version
                : LanguageVersion.Latest,
            DocumentationMode.None,
            SourceCodeKind.Regular,
            manifest.DefineConstants
        );

    // The first attribute of the name, or of an alias of it, in a list with no target or the target "type".  [Gen] is
    // the attribute when Gen or GenAttribute is an alias of it, as the compiler looks up both.  The aliases in reach
    // are found only for a declaration with an attribute of a name that is no name of the attribute's own.
    private static AttributeSyntax? FindAttribute(
        TypeDeclarationSyntax declaration,
        Dictionary<string, bool> projectAliases
    )
    {
        Dictionary<string, bool>? aliases = null;
        foreach (
            var attribute in declaration
                .AttributeLists.Where(static list =>
                    list.Target is not { } target || target.Identifier.IsKind(SyntaxKind.TypeKeyword)
                )
                .SelectMany(static list => list.Attributes)
        )
        {
            if (LastIdentifier(attribute.Name) is ShortName or LongName)
            {
                return attribute;
            }

            if (attribute.Name is IdentifierNameSyntax { Identifier.ValueText: var name })
            {
                aliases ??= AliasesOf(declaration, projectAliases);
                if (
                    (aliases.TryGetValue(name, out var named) && named)
                    || (aliases.TryGetValue(name + AttributeSuffix, out var suffixed) && suffixed)
                )
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    private static void ReadAttribute(
        TypeDeclarationSyntax declaration,
        AttributeSyntax attribute,
        string path,
        bool compilerFallsBack,
        ImmutableArray<TypeClaim>.Builder claims,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        string? typeSource = null;
        var readSource = false;
        var isRead = true;
        if (attribute.ArgumentList is { } list)
        {
            foreach (var argument in list.Arguments)
            {
                // The first of a name, as the generator takes it; the compiler rejects a second.
                var name = argument.NameEquals?.Name.Identifier.ValueText;
                if (name == AttributeSource.SourceProperty && !readSource)
                {
                    readSource = true;
                    if (!TryReadSource(argument.Expression, out typeSource))
                    {
                        isRead = false;
                        errors.Add(
                            ToolDiagnostic.At(
                                ToolDiagnostics.AttributeArgumentNotLiteral,
                                path,
                                StartOf(argument.Expression),
                                AttributeSource.SourceProperty
                            )
                        );
                    }
                    // By how it is written: an escape such as \u00e9 is ASCII in the file, so it reads the same.
                    else if (compilerFallsBack && argument.Expression.ToString().Any(static c => c > '\u007F'))
                    {
                        isRead = false;
                        errors.Add(
                            ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, path, StartOf(argument.Expression), path)
                        );
                    }
                }
            }
        }

        // With no Source, the type's file is named for it, and a name outside ASCII is read as the Source is.
        var identifier = declaration.Identifier;
        if (isRead && typeSource is null && compilerFallsBack && WrittenNameOf(declaration) is { } written)
        {
            isRead = false;
            errors.Add(ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, path, written, path));
        }

        // A claim that could not be read is not planned: its files may be described for another type.
        if (isRead)
        {
            claims.Add(new TypeClaim(path, identifier.ValueText, typeSource, StartOf(attribute)));
        }
    }

    // A string literal of any kind.  null, default and the empty string are a Source that is not set, as in the
    // generator, where a string of white space is a source that matches nothing.
    private static bool TryReadSource(ExpressionSyntax expression, out string? source)
    {
        source = null;
        if (expression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var text = ((LiteralExpressionSyntax)expression).Token.ValueText;
            source = text.Length > 0 ? text : null;
            return true;
        }

        return expression.IsKind(SyntaxKind.NullLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultLiteralExpression);
    }

    // The last identifier of an attribute's name, however it is qualified.
    private static string? LastIdentifier(NameSyntax name) =>
        name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null,
        };

    private static LinePosition StartOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition;

    // Where the type's name is written, when it holds a character outside ASCII, and null otherwise.  A character that
    // is not UTF-8 reads as U+FFFD, which ends the identifier, so the name is the characters from the identifier's
    // start that can be part of one.  A U+FFFD first in the name leaves no identifier, missing or empty, and the name
    // is then at the first character outside ASCII after the keyword.
    private static LinePosition? WrittenNameOf(TypeDeclarationSyntax declaration)
    {
        var text = declaration.SyntaxTree.GetText();
        var identifier = declaration.Identifier;
        if (identifier.ValueText.Length == 0)
        {
            var keyword =
                declaration is RecordDeclarationSyntax record && !record.ClassOrStructKeyword.IsKind(SyntaxKind.None)
                    ? record.ClassOrStructKeyword
                    : declaration.Keyword;
            // The character is skipped by the parser, so it is in the trivia of a token after the keyword, not in the
            // declaration's span: up to the end of the next token that is there in the text.
            var next = keyword.GetNextToken().Span.End;
            for (var position = keyword.Span.End; position < next; position++)
            {
                if (text[position] > '\u007F')
                {
                    return text.Lines.GetLinePosition(position);
                }
            }

            return null;
        }

        var start = identifier.SpanStart;
        var end = identifier.Span.End;
        while (end < text.Length && IsInName(text[end]))
        {
            end++;
        }

        for (var position = start; position < end; position++)
        {
            if (text[position] > '\u007F')
            {
                return text.Lines.GetLinePosition(start);
            }
        }

        return null;
    }

    // A letter, a digit, an underscore, the backslash of an escape such as \u00e9, or any character outside ASCII.
    private static bool IsInName(char c) => c > '\u007F' || char.IsAsciiLetterOrDigit(c) || c == '_' || c == '\\';
}
