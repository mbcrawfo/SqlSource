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
}
