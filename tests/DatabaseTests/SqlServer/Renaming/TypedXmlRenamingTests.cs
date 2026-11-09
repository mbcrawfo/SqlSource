using System.Threading.Tasks;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Renaming;

// A parameter declared as a typed xml, captured on the server as diagnose captures one, then renamed by SQL Server's
// part: CONTENT and DOCUMENT are words of the declaration, so the renamed declaration keeps them as written and
// renames the collection's names, and the proof replays the renamed bundle to its renamed entry.  The declaration
// with no keyword is the control.  Run on the floor image and on the default one.
[Collection(UsesSqlServer.Name)]
public sealed class TypedXmlRenamingTests(SqlServerDatabases sqlServer)
{
    private const string Collection = "TypedXmlRenamingSchemas";

    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer);

    [Theory]
    [InlineData("CONTENT ")]
    [InlineData("DOCUMENT ")]
    [InlineData("content ")]
    [InlineData("Document ")]
    [InlineData("")]
    public async Task RenameAsync_ParameterOfATypedXml_KeepsTheKeywordAndReplaysToItsRenamedEntry(string keyword)
    {
        await sqlServer.ExecuteAsync(
            "IF EXISTS (SELECT 1 FROM sys.xml_schema_collections WHERE name = N'"
                + Collection
                + "') DROP XML SCHEMA COLLECTION dbo."
                + Collection
        );
        await sqlServer.ExecuteAsync(
            "CREATE XML SCHEMA COLLECTION dbo."
                + Collection
                + " AS N'<xsd:schema xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">"
                + "<xsd:element name=\"a\" type=\"xsd:string\"/></xsd:schema>'"
        );
        try
        {
            using var folder = new TempFolder();
            _ = folder.WriteFile(
                FixtureCase.QueryFile,
                "-- name: GetDoc\n-- param: @p xml(" + keyword + "dbo." + Collection + ")\nSELECT @p AS v;\n"
            );
            var raw = await RawBundles.CaptureAsync(
                new FixtureCase("mssql", "typed-xml", folder.Path),
                static _ => new SqlServerDescriber(),
                sqlServer.ConnectionString,
                LiveExchange.Instance
            );
            raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Entry);

            var renamed = await BundleRenamings.RenamedAsync(
                raw,
                Part,
                new SqlServerDescriber(),
                BundleRenamings.Key(BundleRenamings.AnyKey)
            );

            var bundle = renamed.Bundle;
            Provenance.Of(bundle.Expected.ShouldNotBeNull()).ShouldBe(Provenance.Of(raw.Expected.ShouldNotBeNull()));
            bundle.Query.ShouldContain(" xml(" + keyword + "dbo.");
            bundle.Query.ShouldNotContain(Collection, Case.Insensitive);
            bundle.Capture.ShouldNotContain(Collection, Case.Insensitive);
            SqlServerBundleLeaks.Find(bundle, Part, raw, raw.Query).ShouldBeEmpty();
        }
        finally
        {
            await sqlServer.ExecuteAsync("DROP XML SCHEMA COLLECTION dbo." + Collection);
        }
    }
}
