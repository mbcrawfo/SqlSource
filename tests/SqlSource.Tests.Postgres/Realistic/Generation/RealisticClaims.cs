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

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// A class of RealisticQueries.cs and the case whose query.sql its attribute claims.
internal sealed record RealisticClaim(string TypeName, string Case);

// The claims of RealisticQueries.cs, read from its source with Roslyn and not by reflection, so that a test of them
// names the case and the class and needs no generated type.
internal static partial class RealisticClaims
{
    // The attribute as the generator names it, without its namespace and the suffix Attribute, as the file writes it.
    private static readonly string AttributeName = AttributeSource
        .AttributeMetadataName[(AttributeSource.AttributeMetadataName.LastIndexOf('.') + 1)..]
        .Replace("Attribute", "", StringComparison.Ordinal);

    // Every claim of the file, in its order, read afresh each time.
    public static IReadOnlyList<RealisticClaim> All => Parse(File.ReadAllText(RepositoryPaths.RealisticQueries));

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
                        .Select(attribute => new RealisticClaim(type.Identifier.ValueText, CaseOf(type, attribute)))
                ),
        ];

    // The one claim of the case, which a case with models has.
    public static RealisticClaim Of(RealisticCase found)
    {
        var claims = All.Where(claim => claim.Case == found.Name).ToList();
        if (claims.Count != 1)
        {
            Assert.Fail(ClaimedBy(found, claims.Count));
        }

        return claims[0];
    }

    // How a failure says that a case has a count of claims it should not.
    public static string ClaimedBy(RealisticCase found, int count) =>
        $"{found} is claimed by {count} classes of {RepositoryPaths.RealisticQueries}";

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

    [GeneratedRegex(@"^\.\./Cases/(?<case>[^/]+)/query\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex CasePath();
}
