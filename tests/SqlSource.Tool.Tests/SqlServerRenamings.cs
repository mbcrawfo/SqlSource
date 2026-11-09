using System.IO;
using System.Threading.Tasks;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// SQL Server's bundles as the renaming's tests rename them: a case of the describers' fixtures as a raw bundle, and a
// raw bundle renamed through BundleRenamer with SQL Server's part and its describer.
internal static class SqlServerRenamings
{
    public const string Engine = "mssql";

    public static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer);

    // The schema every case of the fixtures is recorded against.
    public static Task<string> SchemaAsync() =>
        File.ReadAllTextAsync(FixtureCases.SchemaOf(Engine), TestContext.Current.CancellationToken);

    public static FixtureCase Fixture(string name) => FixtureCases.Named(Engine, name);

    // The case of the fixtures as a raw bundle, with an entry outcome.
    public static Bundle Raw(string name) => RawBundles.OfFixture(Fixture(name));

    public static Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(Bundle raw, RenamingKey? key = null) =>
        BundleRenamings.RenameAsync(
            raw,
            Part,
            new SqlServerDescriber(),
            key ?? BundleRenamings.Key(BundleRenamings.AnyKey)
        );

    // The bundle renamed, which must succeed.
    public static Task<RenamedBundle> RenamedAsync(Bundle raw, RenamingKey? key = null) =>
        BundleRenamings.RenamedAsync(
            raw,
            Part,
            new SqlServerDescriber(),
            key ?? BundleRenamings.Key(BundleRenamings.AnyKey)
        );

    // Why the bundle is refused, which it must be.
    public static Task<RenamingRefusal> RefusedAsync(Bundle raw, RenamingKey? key = null) =>
        BundleRenamings.RefusedAsync(
            raw,
            Part,
            new SqlServerDescriber(),
            key ?? BundleRenamings.Key(BundleRenamings.AnyKey)
        );
}
