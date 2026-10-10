using System;
using System.Collections.Immutable;
using System.IO;
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
/// It matches the attribute by its name and reads <c>Path</c> and <c>Output</c> alone, each as it is written:
/// nothing else on the attribute changes what is described.  What it cannot see is in <c>docs/tech-debt/TD-0031</c>:
/// an alias, a type of the same name in another namespace, and an attribute inside <c>#if</c> under a configuration
/// other than the manifest's.
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
            if (ReadText(path) is not { } text || !text.ToString().Contains(ShortName, StringComparison.Ordinal))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text, options, path, cancellationToken).GetRoot(cancellationToken);
            // Into a namespace and a type, for a nested type, and into nothing else: no body of a member is walked.
            foreach (
                var node in root.DescendantNodes(static node =>
                    node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax
                )
            )
            {
                if (TargetTypeReader.IsCandidate(node) && FindAttribute((TypeDeclarationSyntax)node) is { } attribute)
                {
                    ReadAttribute(attribute, path, claims, errors);
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

    private static SourceText? ReadText(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SourceText.From(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // The first attribute of the name in a list with no target or the target "type".
    private static AttributeSyntax? FindAttribute(TypeDeclarationSyntax declaration) =>
        declaration
            .AttributeLists.Where(static list =>
                list.Target is not { } target || target.Identifier.IsKind(SyntaxKind.TypeKeyword)
            )
            .SelectMany(static list => list.Attributes)
            .FirstOrDefault(static attribute => LastIdentifier(attribute.Name) is ShortName or LongName);

    private static void ReadAttribute(
        AttributeSyntax attribute,
        string path,
        ImmutableArray<TypeClaim>.Builder claims,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        string? typePath = null;
        OutputKind? output = null;
        var readPath = false;
        var readOutput = false;
        var isRead = true;
        if (attribute.ArgumentList is { } list)
        {
            foreach (var argument in list.Arguments)
            {
                // The first of a name, as the generator takes it; the compiler rejects a second.
                var name = argument.NameEquals?.Name.Identifier.ValueText;
                if (name == AttributeSource.PathProperty && !readPath)
                {
                    readPath = true;
                    if (!TryReadPath(argument.Expression, out typePath))
                    {
                        isRead = false;
                        errors.Add(NotALiteral(path, argument.Expression, AttributeSource.PathProperty));
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

        // A claim that could not be read is not planned: its files may be described for another type.
        if (isRead)
        {
            claims.Add(new TypeClaim(path, typePath, output, StartOf(attribute)));
        }
    }

    // A string literal of any kind.  null, default and the empty string are a Path that is not set, as in the
    // generator, where a string of white space is a path that matches nothing.
    private static bool TryReadPath(ExpressionSyntax expression, out string? path)
    {
        path = null;
        if (expression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var text = ((LiteralExpressionSyntax)expression).Token.ValueText;
            path = text.Length > 0 ? text : null;
            return true;
        }

        return expression.IsKind(SyntaxKind.NullLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultLiteralExpression);
    }

    // GeneratorOutput.Models, however GeneratorOutput is qualified.  Another member is not set: the compiler
    // rejects it.
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

        output = access.Name.Identifier.ValueText switch
        {
            nameof(OutputKind.Sql) => OutputKind.Sql,
            nameof(OutputKind.Models) => OutputKind.Models,
            nameof(OutputKind.CodeGen) => OutputKind.CodeGen,
            _ => null,
        };
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
}
