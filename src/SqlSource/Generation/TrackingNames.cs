namespace SqlSource.Generation;

/// <summary>
/// The names of the pipeline's steps.  A test reads a step's run reasons by name to check that it was cached.
/// </summary>
internal static class TrackingNames
{
    public const string TargetTypes = nameof(TargetTypes);

    public const string SqlPaths = nameof(SqlPaths);

    public const string CaseCollisions = nameof(CaseCollisions);

    public const string SupportedFramework = nameof(SupportedFramework);

    public const string UnsupportedLanguageVersion = nameof(UnsupportedLanguageVersion);

    public const string TypeFiles = nameof(TypeFiles);

    public const string ClaimedPaths = nameof(ClaimedPaths);

    public const string CommentPaths = nameof(CommentPaths);

    public const string ProjectDialect = nameof(ProjectDialect);

    public const string ProjectSettings = nameof(ProjectSettings);

    public const string FileParseInput = nameof(FileParseInput);

    public const string ParsedFile = nameof(ParsedFile);

    public const string ParsedFiles = nameof(ParsedFiles);

    public const string FileSettings = nameof(FileSettings);

    public const string FilesSettings = nameof(FilesSettings);

    public const string AmbiguousHintNames = nameof(AmbiguousHintNames);

    public const string TypeQueries = nameof(TypeQueries);

    public const string TypeOutput = nameof(TypeOutput);

    public const string SidecarInput = nameof(SidecarInput);

    public const string SidecarFile = nameof(SidecarFile);

    public const string Sidecars = nameof(Sidecars);

    public const string TypeNeeds = nameof(TypeNeeds);

    public const string FileNeeds = nameof(FileNeeds);

    public const string SidecarCheckInput = nameof(SidecarCheckInput);

    public const string SidecarCheck = nameof(SidecarCheck);

    public const string NpgsqlProbe = nameof(NpgsqlProbe);

    public const string UsesPostgresEntries = nameof(UsesPostgresEntries);

    public const string MappedTypeNames = nameof(MappedTypeNames);

    public const string MappedTypes = nameof(MappedTypes);

    public const string FileTypesInput = nameof(FileTypesInput);

    public const string FileTypes = nameof(FileTypes);
}
