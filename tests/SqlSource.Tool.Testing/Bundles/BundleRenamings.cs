using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Testing.Bundles;

// A raw bundle renamed through BundleRenamer with an engine's part, a describer and a key of one repeated byte, as the
// renaming's tests rename one.
internal static class BundleRenamings
{
    // The key a test renames with when it compares no two runs.
    public const byte AnyKey = 1;

    // A key of 32 bytes of one value.
    public static RenamingKey Key(byte fill) => RenamingKey.Of(Enumerable.Repeat(fill, RenamingKey.Length).ToArray());

    // The bundle renamed with the engine's part, the describer and the key given.
    public static Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(
        Bundle raw,
        IEngineRenaming engine,
        IQueryDescriber describer,
        RenamingKey key
    ) => BundleRenamer.RenameAsync(raw, engine, describer, key, TestContext.Current.CancellationToken);

    // The bundle renamed, which must succeed, or the test fails with the refusal.
    public static async Task<RenamedBundle> RenamedAsync(
        Bundle raw,
        IEngineRenaming engine,
        IQueryDescriber describer,
        RenamingKey key
    )
    {
        var renamed = await RenameAsync(raw, engine, describer, key);
        return renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
    }

    // Why the bundle is refused, which it must be.
    public static async Task<RenamingRefusal> RefusedAsync(
        Bundle raw,
        IEngineRenaming engine,
        IQueryDescriber describer,
        RenamingKey key
    )
    {
        var renamed = await RenameAsync(raw, engine, describer, key);
        renamed.Value.ShouldBeNull();
        return renamed.Failure.ShouldNotBeNull();
    }
}
