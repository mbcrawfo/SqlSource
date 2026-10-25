using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// A class of a corpus's RealisticQueries.cs or RealisticMethods.cs, the case whose query.sql its attribute claims and
// the member of GeneratorOutput the attribute names as its Output, null when it sets none.
internal sealed record RealisticClaim(string TypeName, string Case, string? Output);

// The claims of a corpus's RealisticQueries.cs and RealisticMethods.cs, read from their source with Roslyn and not by
// reflection, so that a test of them names the case and the class and needs no generated type.
internal static partial class RealisticClaims
{
    // The output of a claim that the first file makes, which gives the case its models and the SQL of its query.
    public const string ModelsOutput = "Models";

    // The output of a claim that the second file makes, which gives the case its method.
    public const string CodeGenOutput = "CodeGen";

    // The attribute as the generator names it, without its namespace and the suffix Attribute, as the file writes it.
    private static readonly string AttributeName = AttributeSource
        .AttributeMetadataName[(AttributeSource.AttributeMetadataName.LastIndexOf('.') + 1)..]
        .Replace("Attribute", "", StringComparison.Ordinal);

    // Every claim of the corpus's two files, those of Queries then those of Methods, each in its order, read afresh
    // each time.
    public static IReadOnlyList<RealisticClaim> AllOf(RealisticCorpus corpus) =>
        [.. Parse(File.ReadAllText(corpus.Queries)), .. Parse(File.ReadAllText(corpus.Methods))];

    public static IReadOnlyList<RealisticClaim> Parse(string source) =>
        [
            .. CSharpSyntaxTree
                .ParseText(source)
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .SelectMany(static type =>
                    type.AttributeLists.SelectMany(static list => list.Attributes)
                        .Where(static attribute => NameOf(attribute) == AttributeName)
                        .Select(attribute => new RealisticClaim(
                            type.Identifier.ValueText,
                            CaseOf(type, attribute),
                            OutputOf(type, attribute)
                        ))
                ),
        ];

    // The one claim of the case at the output, which a case with models has at each of the two.
    public static RealisticClaim Of(RealisticCorpus corpus, RealisticCase found, string output)
    {
        var claims = AllOf(corpus).Where(claim => claim.Case == found.Name && claim.Output == output).ToList();
        if (claims.Count != 1)
        {
            Assert.Fail(ClaimedBy(corpus, found, output, claims.Count));
        }

        return claims[0];
    }

    // How a failure says that a case has a count of claims at an output it should not.
    public static string ClaimedBy(RealisticCorpus corpus, RealisticCase found, string output, int count) =>
        $"{found} is claimed at {output} by {count} classes of {corpus.Queries} and {corpus.Methods}";

    // The attribute's name without what qualifies it: SqlSource.SqlSourceGenerate and global::SqlSourceGenerate claim
    // as SqlSourceGenerate does.
    private static string NameOf(AttributeSyntax attribute) =>
        attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => attribute.Name.ToString(),
        };

    // The case of the attribute's Path, which names the query of a folder of Cases relative to this file's folder.
    private static string CaseOf(ClassDeclarationSyntax type, AttributeSyntax attribute)
    {
        var path = attribute
            .ArgumentList?.Arguments.Where(static argument =>
                argument.NameEquals?.Name.Identifier.ValueText == AttributeSource.PathProperty
            )
            .Select(static argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(static literal => literal.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(static literal => literal.Token.ValueText)
            .SingleOrDefault();
        var match = path is null ? Match.Empty : CasePath().Match(path);
        return match.Success
            ? match.Groups["case"].Value
            : throw new InvalidOperationException(
                $"The class {type.Identifier.ValueText} claims {path ?? "no path"}, which is not the query.sql of a "
                    + "folder of ../Cases."
            );
    }

    // The member of GeneratorOutput that the attribute's Output names, GeneratorOutput.X or qualified, or null when it
    // sets none.  Any other expression is not a member the file names, so it fails naming the class.
    private static string? OutputOf(ClassDeclarationSyntax type, AttributeSyntax attribute)
    {
        var expressions = attribute
            .ArgumentList?.Arguments.Where(static argument =>
                argument.NameEquals?.Name.Identifier.ValueText == AttributeSource.OutputProperty
            )
            .Select(static argument => argument.Expression)
            .ToList();
        return expressions?.SingleOrDefault() switch
        {
            null => null,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            var other => throw new InvalidOperationException(
                $"The class {type.Identifier.ValueText} claims the Output {other}, which is not a member of "
                    + "GeneratorOutput."
            ),
        };
    }

    [GeneratedRegex(@"^\.\./Cases/(?<case>[^/]+)/query\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex CasePath();
}
