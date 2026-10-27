namespace SqlSource.Generation;

/// <summary>
/// The names of the pipeline's steps.  A test reads a step's run reasons by name to check that it was cached.
/// </summary>
internal static class TrackingNames
{
    public const string SettingsFile = nameof(SettingsFile);

    public const string SettingsFiles = nameof(SettingsFiles);

    public const string TargetTypes = nameof(TargetTypes);

    public const string NestedClassCheck = nameof(NestedClassCheck);

    public const string SqlPaths = nameof(SqlPaths);

    public const string CompilePaths = nameof(CompilePaths);

    public const string BuildFolders = nameof(BuildFolders);

    public const string ReachedSettingsFiles = nameof(ReachedSettingsFiles);

    public const string CaseCollisions = nameof(CaseCollisions);

    public const string RepeatedPaths = nameof(RepeatedPaths);

    public const string SupportedFramework = nameof(SupportedFramework);

    public const string UnsupportedLanguageVersion = nameof(UnsupportedLanguageVersion);

    public const string TypeFiles = nameof(TypeFiles);

    public const string ClaimedPaths = nameof(ClaimedPaths);

    public const string CommentPaths = nameof(CommentPaths);

    public const string FileParseInput = nameof(FileParseInput);

    public const string FileClaim = nameof(FileClaim);

    public const string ParsedFile = nameof(ParsedFile);

    public const string ParsedFiles = nameof(ParsedFiles);

    public const string FileLevel = nameof(FileLevel);

    public const string FileLevels = nameof(FileLevels);

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

    public const string SqlClientProbe = nameof(SqlClientProbe);

    public const string UsesSqlServerEntries = nameof(UsesSqlServerEntries);

    public const string MappedTypeNames = nameof(MappedTypeNames);

    public const string ResolvedTypes = nameof(ResolvedTypes);

    public const string MappedTypes = nameof(MappedTypes);

    public const string MappedTypeConverters = nameof(MappedTypeConverters);

    public const string ProjectConverters = nameof(ProjectConverters);

    public const string Converters = nameof(Converters);

    public const string FileTypesInput = nameof(FileTypesInput);

    public const string TypedFile = nameof(TypedFile);

    public const string FileTypes = nameof(FileTypes);

    public const string FileModels = nameof(FileModels);

    public const string AllFileModels = nameof(AllFileModels);

    public const string TypeModels = nameof(TypeModels);

    public const string ModelGroups = nameof(ModelGroups);

    public const string ModelNames = nameof(ModelNames);

    public const string TakenModelNames = nameof(TakenModelNames);

    public const string Models = nameof(Models);

    public const string ModelsByFile = nameof(ModelsByFile);

    public const string FileModelSlots = nameof(FileModelSlots);

    public const string ModelOutput = nameof(ModelOutput);

    public const string TypeMemberNames = nameof(TypeMemberNames);

    public const string MethodDrivers = nameof(MethodDrivers);

    public const string MethodsInput = nameof(MethodsInput);

    public const string TypeMethods = nameof(TypeMethods);

    public const string MethodSignatures = nameof(MethodSignatures);

    public const string ExtensionClassNames = nameof(ExtensionClassNames);

    public const string TakenExtensionClasses = nameof(TakenExtensionClasses);

    public const string MethodConflicts = nameof(MethodConflicts);

    public const string MethodOutput = nameof(MethodOutput);

    public const string MethodDiagnostics = nameof(MethodDiagnostics);

    public const string CodeGenEngines = nameof(CodeGenEngines);
}
