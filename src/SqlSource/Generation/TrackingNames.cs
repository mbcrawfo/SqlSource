namespace SqlSource.Generation;

/// <summary>
/// The names of the pipeline's steps.  A test reads a step's run reasons by name to check that it was cached.
/// </summary>
internal static class TrackingNames
{
    public const string TargetTypes = nameof(TargetTypes);

    public const string SqlPaths = nameof(SqlPaths);

    public const string SupportedFramework = nameof(SupportedFramework);

    public const string TypeFiles = nameof(TypeFiles);

    public const string ClaimedPaths = nameof(ClaimedPaths);

    public const string ParsedFile = nameof(ParsedFile);

    public const string ParsedFiles = nameof(ParsedFiles);

    public const string AmbiguousHintNames = nameof(AmbiguousHintNames);

    public const string TypeQueries = nameof(TypeQueries);

    public const string TokenValidation = nameof(TokenValidation);

    public const string TypeOutput = nameof(TypeOutput);
}
