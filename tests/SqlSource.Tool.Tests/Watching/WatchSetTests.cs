using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Watching;

public sealed class WatchSetTests
{
    // A rooted path on either system, under which no .git folder is looked for: the tests that need the disk use a
    // temporary folder instead.
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\w" : "/w";

    private static readonly string AppFolder = P("App");
    private static readonly string AppProject = P("App/App.csproj");
    private static readonly string Users = P("App/Queries/Users.sql");
    private static readonly string Common = P("Shared/Common.sql");
    private static readonly string Settings = P("App/sqlsource.json");
    private static readonly string Compiled = P("App/UserQueries.cs");

    private static string P(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static FileChange Changed(string path) => new(FileChangeKind.Changed, path, IsFolder: false);

    private static FileChange Created(string path) => new(FileChangeKind.Created, path, IsFolder: false);

    private static FileChange Deleted(string path) => new(FileChangeKind.Deleted, path, IsFolder: false);

    private static FileChange Renamed(string from, string to, bool isFolder = false) =>
        new(FileChangeKind.Renamed, to, isFolder, from);

    private static ProjectManifest Manifest(string project, string[] files, string[] settings, string[] compile) =>
        new()
        {
            ProjectPath = project,
            TargetFramework = "net8.0",
            LangVersion = "",
            DefineConstants = [],
            BuildFolders = "",
            Files = [.. files],
            SettingsFiles = [.. settings],
            CompileFiles = [.. compile],
        };

    private static WatchSet Of(
        string unitPath,
        string unitFolder,
        ImmutableArray<string> projects,
        ImmutableArray<ProjectManifest> manifests,
        params (string Project, string AssetsFile)[] assetsFiles
    ) => WatchSet.Of(unitPath, unitFolder, new FoundProjects(projects, manifests, [.. assetsFiles]));

    private static WatchSet AppSet() =>
        Of(AppProject, AppFolder, [AppProject], [Manifest(AppProject, [Users, Common], [Settings], [Compiled])]);

    [Theory]
    [InlineData("App/Queries/Users.sql")]
    [InlineData("Shared/Common.sql")]
    [InlineData("App/sqlsource.json")]
    [InlineData("App/UserQueries.cs")]
    public void Classify_ListedPath_IsDescribeForEveryWrite(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Changed(full)).ShouldBe(WatchEffect.Describe);
        set.Classify(Created(full)).ShouldBe(WatchEffect.Describe);
        set.Classify(Renamed(P("App/Other.tmp"), full)).ShouldBe(WatchEffect.Describe);
    }

    [Fact]
    public void Classify_ListedPathInAnotherCase_IsDescribe()
    {
        var set = AppSet();

        set.Classify(Changed(P("app/queries/users.sql"))).ShouldBe(WatchEffect.Describe);
    }

    [Theory]
    [InlineData("App/App.csproj")]
    [InlineData("App/Other.csproj")]
    [InlineData("Sol.sln")]
    [InlineData("Sol.slnx")]
    [InlineData("Directory.Build.props")]
    [InlineData("App/Directory.Build.targets")]
    [InlineData("Directory.Packages.props")]
    [InlineData("App/obj/project.assets.json")]
    public void Classify_ProjectOrBuildFile_IsEvaluateForEveryKind(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Changed(full)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Created(full)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Deleted(full)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Renamed(P("App/Old.txt"), full)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Renamed(full, P("App/New.txt"))).ShouldBe(WatchEffect.Evaluate);
    }

    [Theory]
    [InlineData("App/Queries/New.sql")]
    [InlineData("App/Queries/New.SQL")]
    [InlineData("App/Other.cs")]
    [InlineData("App/Queries/sqlsource.json")]
    public void Classify_CreatedOrRenamedUnlistedFileUnderTheProject_IsEvaluate(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Created(full)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Renamed(P("App/Queries/Old.txt"), full)).ShouldBe(WatchEffect.Evaluate);
    }

    [Fact]
    public void Classify_CreatedUnlistedFileOutsideTheProject_IsNone()
    {
        var set = AppSet();

        set.Classify(Created(P("Elsewhere/New.sql"))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_CreatedOrRenamedFolderUnderTheProject_IsEvaluate()
    {
        var set = AppSet();
        var folder = P("App/Queries/New");

        set.Classify(new FileChange(FileChangeKind.Created, folder, IsFolder: true)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Renamed(P("App/Queries/Old"), folder, isFolder: true)).ShouldBe(WatchEffect.Evaluate);
    }

    [Fact]
    public void Classify_CreatedFolderOutsideTheProject_IsNone()
    {
        var set = AppSet();

        set.Classify(new FileChange(FileChangeKind.Created, P("Elsewhere/New"), IsFolder: true))
            .ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_EnvInAFolderOfTheSearch_IsDescribeForEveryKind()
    {
        var set = AppSet();
        var env = P("App/.env");

        set.Classify(Changed(env)).ShouldBe(WatchEffect.Describe);
        set.Classify(Created(env)).ShouldBe(WatchEffect.Describe);
        set.Classify(Deleted(env)).ShouldBe(WatchEffect.Describe);
        set.Classify(Renamed(P("App/old.env"), env)).ShouldBe(WatchEffect.Describe);
        set.Classify(Renamed(env, P("App/old.env"))).ShouldBe(WatchEffect.Describe);
    }

    [Fact]
    public void Classify_EnvInAFolderOutsideTheSearch_IsNone()
    {
        var set = AppSet();

        set.Classify(Changed(P("App/Queries/.env"))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_ChangedOfAnUnlistedSqlFile_IsNone()
    {
        var set = AppSet();

        set.Classify(Changed(P("App/Queries/Other.sql"))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_DeletedOfAnUnlistedFile_IsNone()
    {
        var set = AppSet();

        set.Classify(Deleted(P("App/Queries/Other.sql"))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_DeletedOfAListedFile_IsNone()
    {
        var set = AppSet();

        set.Classify(Deleted(Users)).ShouldBe(WatchEffect.None);
        set.Classify(Renamed(Users, P("App/Queries/Gone.txt"))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_RenamedFromAListedFileToAnother_IsTheLargerEffect()
    {
        var set = AppSet();

        set.Classify(Renamed(Users, P("App/Queries/Moved.sql"))).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Renamed(P("App/Queries/Moved.sql"), Users)).ShouldBe(WatchEffect.Describe);
    }

    [Theory]
    [InlineData("App/Queries/Users.sql.json")]
    [InlineData("App/Queries/Users.sql.json.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("App/New.sql.json")]
    public void Classify_SidecarOrItsTemporaryFile_IsNone(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Changed(full)).ShouldBe(WatchEffect.None);
        set.Classify(Created(full)).ShouldBe(WatchEffect.None);
        set.Classify(Deleted(full)).ShouldBe(WatchEffect.None);
        set.Classify(Renamed(full, P("App/Queries/Users.sql.json"))).ShouldBe(WatchEffect.None);
    }

    [Theory]
    [InlineData("App/bin/Debug/net8.0/App.dll")]
    [InlineData("App/bin/Queries/Users.sql")]
    [InlineData("App/obj/App.AssemblyInfo.cs")]
    [InlineData("App/obj/Debug/net8.0/Generated.cs")]
    [InlineData("App/obj/App.csproj.nuget.g.props")]
    [InlineData(".git/index")]
    [InlineData("App/Queries/bin/New.sql")]
    public void Classify_UnderBinObjOrGit_IsNone(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Changed(full)).ShouldBe(WatchEffect.None);
        set.Classify(Created(full)).ShouldBe(WatchEffect.None);
        set.Classify(Deleted(full)).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_CreatedFolderUnderBinOrObj_IsNone()
    {
        var set = AppSet();

        set.Classify(new FileChange(FileChangeKind.Created, P("App/bin/Debug"), IsFolder: true))
            .ShouldBe(WatchEffect.None);
        set.Classify(new FileChange(FileChangeKind.Created, P("App/bin"), IsFolder: true)).ShouldBe(WatchEffect.None);
        set.Classify(new FileChange(FileChangeKind.Created, P("App/obj/Debug"), IsFolder: true))
            .ShouldBe(WatchEffect.None);
    }

    // A restore writes the assets file below obj wherever the project puts that folder: an artifacts layout, or a
    // custom MSBuildProjectExtensionsPath; and any project.assets.json outside the ignored folders.
    [Theory]
    [InlineData("App/obj/project.assets.json")]
    [InlineData("artifacts/obj/App/project.assets.json")]
    [InlineData("App/obj/x/project.assets.json")]
    [InlineData("App/obj/x/obj/project.assets.json")]
    [InlineData("App/intermediate/project.assets.json")]
    public void Classify_ProjectAssetsWrittenOutsideBinAndGit_IsEvaluate(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Changed(full)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Created(full)).ShouldBe(WatchEffect.Evaluate);
    }

    [Theory]
    [InlineData("App/bin/obj/project.assets.json")]
    [InlineData("App/obj/bin/project.assets.json")]
    [InlineData("App/bin/project.assets.json")]
    [InlineData(".git/x/project.assets.json")]
    [InlineData("App/obj/.git/project.assets.json")]
    public void Classify_ProjectAssetsBelowABinOrGitFolder_IsNone(string path)
    {
        var set = AppSet();
        var full = P(path);

        set.Classify(Changed(full)).ShouldBe(WatchEffect.None);
        set.Classify(Created(full)).ShouldBe(WatchEffect.None);
    }

    // Only the assets file is let through below obj, at any depth.
    [Theory]
    [InlineData("App/obj/x/project.nuget.cache")]
    [InlineData("artifacts/obj/App/App.csproj.nuget.g.props")]
    public void Classify_OtherFileBelowObj_IsNone(string path)
    {
        var set = AppSet();

        set.Classify(Created(P(path))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Classify_ProjectBelowAFolderNamedBin_IsNotIgnoredForThatFolder()
    {
        var root = P("bin/w");
        var project = Path.Combine(root, "App", "App.csproj");
        var users = Path.Combine(root, "App", "Users.sql");
        var set = Of(project, Path.GetDirectoryName(project)!, [project], [Manifest(project, [users], [], [])]);

        set.Classify(Changed(users)).ShouldBe(WatchEffect.Describe);
    }

    [Fact]
    public void Gone_DeletedListedFile_IsThatFile()
    {
        var set = AppSet();

        set.Gone(Deleted(Users)).ShouldBe([Users]);
    }

    [Fact]
    public void Gone_RenamedFromListedFile_IsThatFile()
    {
        var set = AppSet();

        set.Gone(Renamed(Users, P("App/Queries/Moved.txt"))).ShouldBe([Users]);
    }

    [Fact]
    public void Gone_DeletedFolderAboveListedFiles_IsEach()
    {
        var set = AppSet();

        set.Gone(Deleted(AppFolder)).ShouldBe([Compiled, Users, Settings], ignoreOrder: true);
        set.Gone(Deleted(P("App/Queries"))).ShouldBe([Users]);
        set.Gone(Renamed(P("Shared"), P("Moved"), isFolder: true)).ShouldBe([Common]);
    }

    [Fact]
    public void Gone_ListedFileInAnotherCase_IsTheListedSpelling()
    {
        var set = AppSet();

        set.Gone(Deleted(P("app/queries/users.sql"))).ShouldBe([Users]);
    }

    [Fact]
    public void Gone_FolderWithSimilarName_IsNothing()
    {
        var set = AppSet();

        set.Gone(Deleted(P("Ap"))).ShouldBeEmpty();
        set.Gone(Deleted(P("App/Queries/Users.sq"))).ShouldBeEmpty();
    }

    [Fact]
    public void Gone_AnyOtherChange_IsEmpty()
    {
        var set = AppSet();

        set.Gone(Changed(Users)).ShouldBeEmpty();
        set.Gone(Created(Users)).ShouldBeEmpty();
        set.Gone(Renamed(P("App/Queries/Old.txt"), Users)).ShouldBeEmpty();
        set.Gone(Deleted(P("Elsewhere/Other.sql"))).ShouldBeEmpty();
    }

    [Fact]
    public void Gone_IgnoredPath_IsEmpty()
    {
        var set = AppSet();

        set.Gone(Deleted(P("App/bin"))).ShouldBeEmpty();
    }

    [Fact]
    public void Folders_ProjectUnit_AreItsFolderRecursiveAndTheFoldersOutside()
    {
        using var temp = new TempFolder();
        var app = temp.CreateFolder("w/App");
        var shared = temp.CreateFolder("w/Shared");
        var project = Path.Combine(app, "App.csproj");
        var set = Of(
            project,
            app,
            [project],
            [
                Manifest(
                    project,
                    [Path.Combine(app, "Queries", "Users.sql"), Path.Combine(shared, "Common.sql")],
                    [Path.Combine(app, "sqlsource.json")],
                    [Path.Combine(app, "UserQueries.cs")]
                ),
            ]
        );

        // Outside a repository, with the folder above each, which both share.
        set.Folders.ShouldBe([(temp.PathOf("w"), false), (app, true), (shared, false)]);
    }

    [Fact]
    public void Folders_EnvAndDirectoryFoldersGoUpToTheRepository()
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("repo/.git");
        var repo = temp.PathOf("repo");
        var src = temp.CreateFolder("repo/src");
        var app = temp.CreateFolder("repo/src/App");
        var project = Path.Combine(app, "App.csproj");
        var set = Of(project, app, [project], [Manifest(project, [Path.Combine(app, "Users.sql")], [], [])]);

        set.Folders.ShouldBe([(app, true), (repo, false), (src, false)], ignoreOrder: true);
        set.Folders.Select(entry => entry.Folder)
            .ShouldBe(set.Folders.Select(entry => entry.Folder).Order(StringComparer.Ordinal));
        set.Classify(Changed(Path.Combine(repo, ".env"))).ShouldBe(WatchEffect.Describe);
        set.Classify(Changed(Path.Combine(src, "Directory.Build.props"))).ShouldBe(WatchEffect.Evaluate);
    }

    [Fact]
    public void Folders_SolutionInTheRoot_IsTheRootAloneForEveryProjectBelow()
    {
        using var temp = new TempFolder();
        var root = temp.CreateFolder("w");
        var a = temp.CreateFolder("w/A");
        var b = temp.CreateFolder("w/B");
        var projectA = Path.Combine(a, "A.csproj");
        var projectB = Path.Combine(b, "B.csproj");
        var set = Of(
            Path.Combine(root, "Sol.sln"),
            root,
            [projectA, projectB],
            [
                Manifest(projectA, [Path.Combine(a, "Q", "A.sql")], [], []),
                Manifest(projectB, [Path.Combine(b, "B.sql")], [Path.Combine(root, "sqlsource.json")], []),
            ]
        );

        set.Folders.ShouldBe([(temp.Path, false), (root, true)]);
    }

    [Fact]
    public void Folders_TwoSpellingsOfOneFolder_AreOnce()
    {
        using var temp = new TempFolder();
        var app = temp.CreateFolder("w/App");
        var project = Path.Combine(app, "App.csproj");
        var shared = temp.PathOf("w/Shared");
        var set = Of(
            project,
            app,
            [project],
            [
                Manifest(
                    project,
                    [Path.Combine(shared, "A.sql"), Path.Combine(shared.ToUpperInvariant(), "B.sql")],
                    [],
                    []
                ),
            ]
        );

        set.Folders.Length.ShouldBe(3);
    }

    // A project of the solution outside its folder, which has no manifest: not restored, or without the package yet.
    // Its restore, the package added to its project file, or a Directory.Build.props of its own must still start a
    // pass.
    [Fact]
    public void Folders_ProjectWithoutAManifestOutsideTheSolutionsFolder_IsWatchedWithItsFoldersBelow()
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("r/.git");
        var root = temp.PathOf("r");
        var build = temp.CreateFolder("r/build");
        var src = temp.CreateFolder("r/src");
        var other = temp.CreateFolder("r/src/Other");
        var set = Of(Path.Combine(build, "App.sln"), build, [Path.Combine(other, "Other.csproj")], []);

        set.Folders.ShouldBe([(build, true), (root, false), (src, false), (other, true)], ignoreOrder: true);
        set.Classify(Created(Path.Combine(other, "obj", "project.assets.json"))).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Changed(Path.Combine(other, "Other.csproj"))).ShouldBe(WatchEffect.Evaluate);
        set.Classify(Changed(Path.Combine(src, "Directory.Build.props"))).ShouldBe(WatchEffect.Evaluate);
    }

    // An artifacts layout puts a project's assets file outside every folder of the unit, where its restore must still
    // start a pass.
    [Fact]
    public void Folders_AssetsFolderOutsideTheUnit_IsWatchedWithItsFoldersBelow()
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("r/.git");
        var root = temp.PathOf("r");
        var src = temp.CreateFolder("r/src");
        var app = temp.CreateFolder("r/src/App");
        var assetsFolder = temp.CreateFolder("r/artifacts/obj/App");
        var project = Path.Combine(app, "App.csproj");
        var assets = Path.Combine(assetsFolder, "project.assets.json");

        var set = Of(project, app, [project], [], (project, assets));

        set.Folders.ShouldBe(
            [
                (app, true),
                (assetsFolder, true),
                (temp.PathOf("r/artifacts"), false),
                (temp.PathOf("r/artifacts/obj"), false),
                (root, false),
                (src, false),
            ],
            ignoreOrder: true
        );
        set.Classify(Created(assets)).ShouldBe(WatchEffect.Evaluate);
    }

    // Before the restore makes the assets file's folder, the nearest folder above it that exists is watched with its
    // folders below, the repository's root at most.
    [Theory]
    [InlineData("artifacts/obj", "artifacts/obj")]
    [InlineData("artifacts", "artifacts")]
    [InlineData("", "")]
    public void Folders_AssetsFolderThatDoesNotExistYet_IsItsNearestFolderThatDoes(string existing, string watched)
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("r/.git");
        _ = temp.CreateFolder("r/src/App");
        _ = temp.CreateFolder(Path.Combine("r", existing));
        var app = temp.PathOf("r/src/App");
        var project = Path.Combine(app, "App.csproj");
        var assets = temp.PathOf("r/artifacts/obj/App/project.assets.json");
        var folder = Path.TrimEndingDirectorySeparator(temp.PathOf(Path.Combine("r", watched)));

        var set = Of(project, app, [project], [], (project, assets));

        set.Folders.ShouldContain((folder, true));
        set.Folders.ShouldNotContain(entry =>
            entry.Folder != folder
            && entry.Folder.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        );
        set.Classify(Created(assets)).ShouldBe(WatchEffect.Evaluate);
    }

    // Nothing above the root of the project's repository, or above the project's folder outside one, is watched for an
    // assets folder that does not exist yet.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Folders_AssetsFolderThatDoesNotExistAboveTheRepository_IsNotWatched(bool repository)
    {
        using var temp = new TempFolder();
        if (repository)
        {
            _ = temp.CreateFolder("w/r/.git");
        }

        var app = temp.CreateFolder("w/r/App");
        var project = Path.Combine(app, "App.csproj");
        var assets = temp.PathOf("w/artifacts/obj/App/project.assets.json");

        var set = Of(project, app, [project], [], (project, assets));

        set.Folders.ShouldNotContain(entry => entry.Recursive && entry.Folder != app);
    }

    // Outside a repository the artifacts folder of UseArtifactsOutput is beside the nearest Directory.Build.props, so
    // that folder bounds the search for an assets folder that does not exist yet.  In a repository the repository's
    // root does, wherever a Directory.Build.props is.
    [Theory]
    [InlineData(false, "w", "artifacts", "w/artifacts")]
    [InlineData(false, "w", "", "w")]
    [InlineData(false, "w/src", "artifacts", null)]
    [InlineData(true, "w/src", "artifacts", "w/artifacts")]
    public void Folders_AssetsFolderThatDoesNotExistYet_IsBoundByTheBuildProps(
        bool repository,
        string props,
        string existing,
        string? watched
    )
    {
        using var temp = new TempFolder();
        if (repository)
        {
            _ = temp.CreateFolder("w/.git");
        }

        _ = temp.WriteFile(props + "/Directory.Build.props", "<Project />");
        _ = temp.CreateFolder("w/" + existing);
        var app = temp.CreateFolder("w/src/App");
        var project = Path.Combine(app, "App.csproj");
        var assets = temp.PathOf("w/artifacts/obj/App/project.assets.json");

        var set = Of(project, app, [project], [], (project, assets));

        if (watched is null)
        {
            set.Folders.ShouldNotContain(entry => entry.Recursive && entry.Folder != app);
        }
        else
        {
            set.Folders.ShouldContain((Path.TrimEndingDirectorySeparator(temp.PathOf(watched)), true));
        }
    }

    // Outside a repository nothing above a watched folder is otherwise watched, so the folder above each is, alone, so
    // that a folder deleted and made again is seen: on Linux its own watch says nothing.  In a repository the folders
    // above are watched up to its root, and nothing above that.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Folders_FolderAboveEachWatchedFolder_IsWatchedOutsideARepository(bool repository)
    {
        using var temp = new TempFolder();
        if (repository)
        {
            _ = temp.CreateFolder("w/.git");
        }

        var app = temp.CreateFolder("w/App");
        var shared = temp.CreateFolder("w/libs/Shared");
        var project = Path.Combine(app, "App.csproj");

        var set = Of(project, app, [project], [Manifest(project, [Path.Combine(shared, "Common.sql")], [], [])]);

        set.Folders.ShouldBe(
            [(app, true), (temp.PathOf("w"), false), (temp.PathOf("w/libs"), false), (shared, false)],
            ignoreOrder: true
        );
    }

    // An assets folder that exists is watched wherever it is.
    [Fact]
    public void Folders_AssetsFolderOutsideTheRepositoryThatExists_IsWatched()
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("w/r/.git");
        var app = temp.CreateFolder("w/r/App");
        var assetsFolder = temp.CreateFolder("w/out/obj/App");
        var project = Path.Combine(app, "App.csproj");

        var set = Of(project, app, [project], [], (project, Path.Combine(assetsFolder, "project.assets.json")));

        set.Folders.ShouldContain((assetsFolder, true));
    }

    // The default layout's obj folder, there or not yet, is below the project's folder, which is watched already.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Folders_AssetsFolderBelowTheProject_AddsNoFolder(bool restored)
    {
        using var temp = new TempFolder();
        var app = temp.CreateFolder("w/App");
        if (restored)
        {
            _ = temp.CreateFolder("w/App/obj");
        }

        var project = Path.Combine(app, "App.csproj");

        var set = Of(project, app, [project], [], (project, Path.Combine(app, "obj", "project.assets.json")));

        set.Folders.ShouldBe([(temp.PathOf("w"), false), (app, true)]);
    }

    // A repository with a project in src/App that lists a .sql file of its own folder and one of Shared, on the disk
    // for the .git marker, as a solution in the repository's root or the project alone runs it.
    private static WatchSet RepositorySet(TempFolder temp, bool solution)
    {
        _ = temp.CreateFolder("r/.git");
        var root = temp.PathOf("r");
        var app = temp.CreateFolder("r/src/App");
        _ = temp.CreateFolder("r/Shared");
        var project = Path.Combine(app, "App.csproj");
        ImmutableArray<ProjectManifest> manifests =
        [
            Manifest(project, [Path.Combine(app, "Users.sql"), temp.PathOf("r/Shared/Common.sql")], [], []),
        ];
        return solution
            ? Of(Path.Combine(root, "App.sln"), root, [project], manifests)
            : Of(project, app, [project], manifests);
    }

    // A file or a folder in a folder that holds a listed file, outside every project's folder, which a glob of the
    // project may list now; a settings file in the folder of a listed file or above it, up to the repository's root,
    // which may apply to it now; and a .sql file in the project's own folder, as any unlisted file under the project.
    [Theory]
    [InlineData("sqlsource.json", true, false)]
    [InlineData("sqlsource.json", false, false)]
    [InlineData("src/sqlsource.json", true, false)]
    [InlineData("src/sqlsource.json", false, false)]
    [InlineData("Shared/Orders.sql", true, false)]
    [InlineData("Shared/Orders.sql", false, false)]
    [InlineData("Shared/Orders.cs", false, false)]
    [InlineData("Shared/sqlsource.json", true, false)]
    [InlineData("Shared/sqlsource.json", false, false)]
    [InlineData("Shared/Queries", true, true)]
    [InlineData("Shared/Queries", false, true)]
    [InlineData("src/App/sqlsource.json", false, false)]
    [InlineData("src/App/New.sql", false, false)]
    public void Classify_NewContentForAListedFileOutsideTheProject_IsEvaluate(string path, bool solution, bool isFolder)
    {
        using var temp = new TempFolder();
        var set = RepositorySet(temp, solution);
        var full = temp.PathOf("r/" + path);

        set.Classify(new FileChange(FileChangeKind.Created, full, isFolder)).ShouldBe(WatchEffect.Evaluate);
        set.Classify(new FileChange(FileChangeKind.Renamed, full, isFolder, temp.PathOf("r/Shared/Old.txt")))
            .ShouldBe(WatchEffect.Evaluate);
    }

    // A .sql or C# file, or a folder, only in the folder of a listed file; a settings file only there or above it.
    [Theory]
    [InlineData("src/New.sql", false)]
    [InlineData("New.cs", false)]
    [InlineData("src/Queries", true)]
    [InlineData("Other/sqlsource.json", false)]
    [InlineData("Shared/Queries/sqlsource.json", false)]
    [InlineData("Shared/Orders.txt", false)]
    [InlineData("src/sqlsource.json", true)]
    public void Classify_NewContentThatNoListedFileReaches_IsNone(string path, bool isFolder)
    {
        using var temp = new TempFolder();
        var set = RepositorySet(temp, solution: false);

        set.Classify(new FileChange(FileChangeKind.Created, temp.PathOf("r/" + path), isFolder))
            .ShouldBe(WatchEffect.None);
    }

    // The folders above a listed file outside every project's folder, up to the repository's root, are watched, so
    // that a settings file made there is seen.
    [Fact]
    public void Folders_FoldersAboveAListedFileOutsideTheProject_AreWatched()
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("r/.git");
        var app = temp.CreateFolder("r/App");
        var libs = temp.CreateFolder("r/libs");
        var shared = temp.CreateFolder("r/libs/Shared");
        var project = Path.Combine(app, "App.csproj");

        var set = Of(project, app, [project], [Manifest(project, [Path.Combine(shared, "Common.sql")], [], [])]);

        set.Folders.ShouldBe(
            [(app, true), (temp.PathOf("r"), false), (libs, false), (shared, false)],
            ignoreOrder: true
        );
        set.Classify(Created(Path.Combine(libs, "sqlsource.json"))).ShouldBe(WatchEffect.Evaluate);
    }

    // A new .sql file changes nothing in a project without the package until the package is added, which is a save of
    // its project file.
    [Fact]
    public void Classify_NewSqlFileInAProjectWithoutAManifest_IsNone()
    {
        var other = P("Other");
        var set = Of(AppProject, AppFolder, [AppProject, P("Other/Other.csproj")], []);

        set.Folders.ShouldContain((other, true));
        set.Classify(Created(P("Other/New.sql"))).ShouldBe(WatchEffect.None);
    }

    [Fact]
    public void Of_SameInputs_GiveEqualFolders()
    {
        using var temp = new TempFolder();
        var app = temp.CreateFolder("w/App");
        var project = Path.Combine(app, "App.csproj");
        ImmutableArray<ProjectManifest> manifests =
        [
            Manifest(project, [Path.Combine(temp.PathOf("w/Shared"), "A.sql"), Path.Combine(app, "B.sql")], [], []),
        ];

        Of(project, app, [project], manifests).Folders.ShouldBe(Of(project, app, [project], manifests).Folders);
    }
}
