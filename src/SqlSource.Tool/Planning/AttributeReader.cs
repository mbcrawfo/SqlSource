using System;
using System.Collections.Immutable;
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
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Reads <c>[SqlSourceGenerate]</c> from the C# files of a project, as syntax.  The generator's reader works on
/// symbols, and the tool has no compilation, so there is nothing of it to share.
/// </summary>
/// <remarks>
/// It matches the attribute by its name and reads <c>Source</c> and <c>Output</c> alone, each as it is written, and
/// the type's name, which names its file when there is no <c>Source</c>: nothing else on the attribute changes what is
/// described.  What it cannot see is in <c>docs/tech-debt/TD-0031</c>: an alias, a type of the same name in another
/// namespace, and an attribute inside <c>#if</c> under a configuration other than the manifest's.  A <c>Source</c>
/// outside ASCII in a file that is not UTF-8 is not read, nor a type's name outside ASCII when there is no
/// <c>Source</c>: on Windows the compiler reads that file in the system's code page, so the text here would not be the
/// text it resolves.
/// </remarks>
internal static class AttributeReader
{
    private const string ShortName = "SqlSourceGenerate";

    private const string LongName = "SqlSourceGenerateAttribute";

    private const string OutputType = "GeneratorOutput";

    public static ProjectClaims Read(ProjectManifest manifest, CancellationToken cancellationToken)
    {
        var options = ParseOptionsOf(manifest);
        var claims = ImmutableArray.CreateBuilder<TypeClaim>();
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();
        foreach (var path in manifest.CompileFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A file that cannot be read is the build's to report.  Most files do not hold the word, and are not
            // parsed.
            if (
                FileText.Read(path) is not { } contents
                || !contents.Text.ToString().Contains(ShortName, StringComparison.Ordinal)
            )
            {
                continue;
            }

            var root = CSharpSyntaxTree
                .ParseText(contents.Text, options, path, cancellationToken)
                .GetRoot(cancellationToken);
            // Into a namespace and a type, for a nested type, and into nothing else: no body of a member is walked.
            foreach (
                var node in root.DescendantNodes(static node =>
                    node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax
                )
            )
            {
                if (
                    TargetTypeReader.IsCandidate(node)
                    && node is TypeDeclarationSyntax declaration
                    && FindAttribute(declaration) is { } attribute
                )
                {
                    ReadAttribute(declaration, attribute, path, contents.CompilerFallsBack, claims, errors);
                }
            }
        }

        return new ProjectClaims(claims.ToImmutable(), errors.ToImmutable());
    }

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

    // The first attribute of the name in a list with no target or the target "type".
    private static AttributeSyntax? FindAttribute(TypeDeclarationSyntax declaration) =>
        declaration
            .AttributeLists.Where(static list =>
                list.Target is not { } target || target.Identifier.IsKind(SyntaxKind.TypeKeyword)
            )
            .SelectMany(static list => list.Attributes)
            .FirstOrDefault(static attribute => LastIdentifier(attribute.Name) is ShortName or LongName);

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
        OutputKind? output = null;
        var readSource = false;
        var readOutput = false;
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
                        errors.Add(NotALiteral(path, argument.Expression, AttributeSource.SourceProperty));
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
                else if (name == AttributeSource.OutputProperty && !readOutput)
                {
                    readOutput = true;
                    if (!TryReadOutput(argument.Expression, out output))
                    {
                        isRead = false;
                        errors.Add(NotALiteral(path, argument.Expression, AttributeSource.OutputProperty));
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
            claims.Add(new TypeClaim(path, identifier.ValueText, typeSource, output, StartOf(attribute)));
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

    // A member of GeneratorOutput, GeneratorOutput.Models for one, however GeneratorOutput is qualified.  A name
    // that is no member of it is not set: the compiler rejects it.
    private static bool TryReadOutput(ExpressionSyntax expression, out OutputKind? output)
    {
        output = null;
        if (
            expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || LastIdentifier(access.Expression) != OutputType
        )
        {
            return false;
        }

        // An identifier is never a number or a list, the two other things Enum.TryParse reads.
        output = Enum.TryParse<OutputKind>(access.Name.Identifier.ValueText, ignoreCase: false, out var member)
            ? member
            : null;
        return true;
    }

    // The last identifier of a name, however it is qualified: as the name of an attribute, or as an expression.
    private static string? LastIdentifier(ExpressionSyntax name) =>
        name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };

    private static ToolDiagnostic NotALiteral(string path, ExpressionSyntax expression, string argument) =>
        ToolDiagnostic.At(ToolDiagnostics.AttributeArgumentNotLiteral, path, StartOf(expression), argument);

    private static LinePosition StartOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition;

    // Where the type's name is written, when it holds a character outside ASCII, and null otherwise.  A character that
    // is not UTF-8 reads as U+FFFD, which ends the identifier, or, at the name's start, leaves a missing identifier at
    // its own position, so the name is the characters from the identifier's start that can be part of one.
    private static LinePosition? WrittenNameOf(TypeDeclarationSyntax declaration)
    {
        var text = declaration.SyntaxTree.GetText();
        var identifier = declaration.Identifier;
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
