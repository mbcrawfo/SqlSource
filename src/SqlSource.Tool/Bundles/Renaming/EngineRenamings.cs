using SqlSource.Settings;
using SqlSource.Tool.Bundles.Renaming.SqlServer;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>The engine's part of the renaming, for each engine.</summary>
internal static class EngineRenamings
{
    /// <summary>The part of the engine, or null while it is not built, when a bundle of it cannot be renamed.</summary>
    public static IEngineRenaming? For(DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => null, // Built by 10.2.
            DatabaseEngine.SqlServer => SqlServerRenaming.Instance,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };
}
