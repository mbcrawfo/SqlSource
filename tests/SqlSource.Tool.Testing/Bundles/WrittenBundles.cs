using Shouldly;
using SqlSource.Tool.Bundles;

namespace SqlSource.Tool.Testing.Bundles;

// A bundle a run wrote, read as replay reads it.
internal static class WrittenBundles
{
    // The bundle at the path, a zip or a folder, which must read.
    public static Bundle Read(string path)
    {
        var read = BundleReader.Read(path, ToolHost.OpenRead, ToolHost.ReadText);
        read.Failure.ShouldBeNull();
        return read.Value.ShouldNotBeNull();
    }
}
