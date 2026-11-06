using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// PostgreSQL's bundles as the renaming's tests rename them: a case of the describers' fixtures, and a raw bundle
// renamed through BundleRenamer with PostgreSQL's part and the describer a replay makes of it.
internal static class PostgresRenamings
{
    public const string Engine = "postgres";

    // The key a test renames with when it compares no two runs.
    public const byte Key = 7;

    // Where the bundle's errors would be.  Nothing is read there.
    private const string BundlePath = "/work/report.zip";

    // The schema every case of the fixtures is recorded against.
    public static string Schema => File.ReadAllText(Path.Combine(FixtureCases.Root, Engine, "schema.sql"));

    public static FixtureCase Fixture(string name) => FixtureCases.Of(Engine).Single(found => found.Name == name);

    public static async Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(Bundle raw, byte key = Key)
    {
        var describer = BundleDescribers.For(
            SqlDialect.PostgreSql,
            ToolHost.Create().Describers,
            raw.ExplainSettings,
            raw.Info.ShouldNotBeNull().Engine,
            BundlePath
        );
        describer.Failure.ShouldBeNull();

        return await BundleRenamer.RenameAsync(
            raw,
            PostgresRenaming.Instance,
            describer.Value.ShouldNotBeNull(),
            RenamingBundles.Key(key),
            TestContext.Current.CancellationToken
        );
    }

    // The bundle renamed, which must succeed.
    public static async Task<RenamedBundle> RenamedAsync(Bundle raw, byte key = Key)
    {
        var renamed = await RenameAsync(raw, key);
        return renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
    }
}
