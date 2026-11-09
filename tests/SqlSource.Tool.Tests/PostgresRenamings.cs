using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tool.Tests;

// PostgreSQL's bundles as the renaming's tests rename them: a case of the describers' fixtures, and a raw bundle
// renamed through BundleRenamer with PostgreSQL's part and the describer a replay makes of it.
internal static class PostgresRenamings
{
    public const string Engine = "postgres";

    // The schema every case of the fixtures is recorded against.
    public static string Schema => File.ReadAllText(FixtureCases.SchemaOf(Engine));

    public static FixtureCase Fixture(string name) => FixtureCases.Named(Engine, name);

    public static async Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(
        Bundle raw,
        byte key = BundleRenamings.AnyKey
    )
    {
        var describer = BundleDescribers.For(
            SqlDialect.PostgreSql,
            ToolHost.Create().Describers,
            raw.ExplainSettings,
            raw.Info.ShouldNotBeNull().Engine,
            BundleInfos.Path
        );
        describer.Failure.ShouldBeNull();

        return await BundleRenamings.RenameAsync(
            raw,
            PostgresRenaming.Instance,
            describer.Value.ShouldNotBeNull(),
            BundleRenamings.Key(key)
        );
    }

    // The bundle renamed, which must succeed.
    public static async Task<RenamedBundle> RenamedAsync(Bundle raw, byte key = BundleRenamings.AnyKey)
    {
        var renamed = await RenameAsync(raw, key);
        return renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
    }
}
