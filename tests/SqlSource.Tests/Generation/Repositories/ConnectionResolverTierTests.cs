using System;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Repositories;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Tests.Generation.Repositories.ResolverCandidates;

namespace SqlSource.Tests.Generation.Repositories;

// Which tier gives each database its connection: the repository's own members, then the marked members of the
// factories it holds, then the marked statics of the project, and how a source of the two shared tiers is reached and
// reported.
public sealed class ConnectionResolverTierTests
{
    private const string Places =
        ", whether a member of the type, an instance member of a type it holds in a field, property or "
        + "primary-constructor parameter, or a static member of the project.";

    private static readonly NeededDatabase Main = new("main", DatabaseEngine.PostgreSql);

    private static readonly NeededDatabase Reports = new("reports", DatabaseEngine.PostgreSql);

    private static readonly NeededDatabase Sales = new("sales", DatabaseEngine.SqlServer);

    [Fact]
    public void Resolve_OwnConventionBeatsHeldFactory()
    {
        var own = Candidate("connectionString", CandidateType.ConnectionString, CandidateMember.Parameter);

        var resolution = Resolve(Tiers([own], held: [Held("dbf", "Open")]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("connectionString");
    }

    [Fact]
    public void Resolve_OwnMarkBeatsGlobal()
    {
        var global = Global("global::App.Db", "ConnectionString", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([AsyncFactory("OpenAsync")], global: [global]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("OpenAsync");
    }

    [Fact]
    public void Resolve_HeldBeatsGlobal()
    {
        var global = Global("global::App.Db", "ConnectionString", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([], held: [Held("dbf", "Open")], global: [global]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("dbf.Open");
    }

    [Fact]
    public void Resolve_GlobalAlone_IsTheSource()
    {
        var global = Global("global::App.Db", "Reports", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([], global: [global]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Database.ShouldBe("main");
        resolved.Asynchronous.ShouldBe(
            new ConnectionSource(
                ConnectionSourceKind.ConnectionString,
                CandidateMember.Property,
                "Db.Reports",
                "global::App.Db.Reports",
                false,
                false,
                null,
                "App.UserRepository",
                false
            )
        );
        resolved.Synchronous.ShouldBe(resolved.Asynchronous);
    }

    // Each source is reached as the generated method reaches it: a held member through its holder, a static holder and
    // a global through its type in full, which no name of the method can hide.
    [Theory]
    [InlineData("parameter", "dbf.Open", "dbf", "dbf", "dbf", "dbf.Open")]
    [InlineData("field", "this._dbf.Open", null, "this._dbf", "_dbf", "_dbf.Open")]
    [InlineData(
        "static field",
        "global::App.UserRepository.F.Open",
        null,
        "global::App.UserRepository.F",
        "F",
        "F.Open"
    )]
    [InlineData("global", "global::App.Db.Reports", null, null, null, "Db.Reports")]
    public void Resolve_SharedSource_IsReachedThroughItsHolderOrType(
        string source,
        string access,
        string? leading,
        string? holderAccess,
        string? holderName,
        string memberName
    )
    {
        var candidate = source switch
        {
            "parameter" => Held("dbf", "Open"),
            "field" => Held("_dbf", "Open", holderMember: CandidateMember.Field),
            "static field" => Held("F", "Open", holderMember: CandidateMember.Field, holderIsStatic: true),
            "global" => Global("global::App.Db", "Reports", CandidateType.ConnectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
        var shape =
            candidate.Tier == CandidateTier.Held ? Tiers([], held: [candidate]) : Tiers([], global: [candidate]);

        var resolution = Resolve(shape, Main);

        resolution.Diagnostics.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        resolved.AccessText.ShouldBe(access);
        resolved.LeadingName.ShouldBe(leading);
        resolved.HolderAccessText.ShouldBe(holderAccess);
        resolved.HolderName.ShouldBe(holderName);
        resolved.MemberName.ShouldBe(memberName);
    }

    // A holder's name and a member's name that are keywords keep their @ where the code writes them, and lose it where
    // a message names them.
    [Fact]
    public void Resolve_HeldKeywordNames_KeepTheirAtInTheAccess()
    {
        var held = Held("event", "default", holderMember: CandidateMember.Field);

        var resolution = Resolve(Tiers([], held: [held]), Main);

        var resolved = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        resolved.AccessText.ShouldBe("this.@event.@default");
        resolved.HolderAccessText.ShouldBe("this.@event");
        resolved.HolderName.ShouldBe("event");
        resolved.MemberName.ShouldBe("event.default");
    }

    // A static holder of a generic repository is reached through the type in full, with its type parameters.
    [Fact]
    public void Resolve_StaticHolderOfAGenericType_HasNoLeadingName()
    {
        var type = TestModels.Type(types: [new TypeDeclaration("class", "UserRepository", "UserRepository", "<T>", 1)]);
        var held = Held("F", "Open", holderMember: CandidateMember.Field, holderIsStatic: true);

        var resolution = ConnectionResolver.Resolve(type, Tiers([], held: [held]), [Main]);

        var resolved = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        resolved.AccessText.ShouldBe("global::App.UserRepository<T>.F.Open");
        resolved.HolderAccessText.ShouldBe("global::App.UserRepository<T>.F");
        resolved.LeadingName.ShouldBeNull();
    }

    // A holder of a struct cannot be null, so a method reads its member unchecked; any other holder is checked.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolve_HeldSource_ChecksAHolderThatCanBeNull(bool canBeNull)
    {
        var held = Held("dbf", "Open", holderCanBeNull: canBeNull);

        var resolution = Resolve(Tiers([], held: [held]), Main);

        resolution.Sources.ShouldHaveSingleItem().Asynchronous.HolderCanBeNull.ShouldBe(canBeNull);
    }

    // A held member that the holder's class implements explicitly is reached through a cast of the holder to its
    // interface, which the access leaves to the emitter; any other is reached through the holder as it is.  The
    // member's name, which the message of a null member gives, is the holder's and its own, without the interface.
    [Theory]
    [InlineData("global::App.IDatabaseFactory", "IDatabaseFactory")]
    [InlineData(null, null)]
    public void Resolve_HeldSource_CarriesTheInterfaceItIsCastTo(string? cast, string? castName)
    {
        var held = Held("dbf", "Open") with { CastTypeText = cast, CastTypeName = castName };

        var resolution = Resolve(Tiers([], held: [held]), Main);

        var resolved = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        resolved.HolderCastText.ShouldBe(cast);
        resolved.AccessText.ShouldBe("dbf.Open");
        resolved.HolderAccessText.ShouldBe("dbf");
        resolved.MemberName.ShouldBe("dbf.Open");
    }

    [Fact]
    public void Resolve_EachDatabaseFromItsOwnTier()
    {
        var own = Candidate("mainSource", CandidateType.DataSource, isMarked: true, database: "main");
        var global = Global("global::App.Db", "Reports", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([own], global: [global]), Main, Reports);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "mainSource"), ("reports", "Db.Reports")]);
    }

    // In a shared tier, a mark without a Database is the default of every database that no mark of the tier names.
    [Fact]
    public void Resolve_NamedSharedMarkBeatsDefault()
    {
        var main = Global("global::App.Db", "Main", CandidateType.ConnectionString);
        var reports = Global("global::App.Db", "Reports", CandidateType.ConnectionString, database: "reports");

        var resolution = Resolve(Tiers([], global: [main, reports]), Main, Reports);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "Db.Main"), ("reports", "Db.Reports")]);
    }

    [Fact]
    public void Resolve_SharedDefaultOnSeveralDatabases_ServesEach()
    {
        var resolution = Resolve(Tiers([], held: [Held("dbf", "Open")]), Main, Reports);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "dbf.Open"), ("reports", "dbf.Open")]);
    }

    [Fact]
    public void Resolve_TwoSharedDefaults_ReportsSqlsrc029()
    {
        var main = Held("_main", "Open", holderMember: CandidateMember.Field);
        var audit = Held("_audit", "Open", holderMember: CandidateMember.Field);

        var resolution = Resolve(Tiers([], held: [main, audit]), Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasManyConnections);
        diagnostic.Location.ShouldBe(TestModels.AttributeLocation);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "main", "'_main.Open', '_audit.Open'"]);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_SharedPairWithinATier_IsAPair()
    {
        var asynchronous = Held("dbf", "OpenAsync", isAsync: true);
        var synchronous = Held("dbf", "Open");

        var resolution = Resolve(Tiers([], held: [asynchronous, synchronous]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Asynchronous.MemberName.ShouldBe("dbf.OpenAsync");
        resolved.Synchronous.ShouldNotBeNull().MemberName.ShouldBe("dbf.Open");
    }

    // The repository's own source decides the database, so the global's task never pairs with it.
    [Fact]
    public void Resolve_PairAcrossTiers_IsNotFormed()
    {
        var own = Candidate("connectionString", CandidateType.ConnectionString);
        var global = Global(
            "global::App.Db",
            "OpenAsync",
            CandidateType.Factory,
            CandidateMember.Method,
            isAsync: true
        );

        var resolution = Resolve(Tiers([own], global: [global]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Asynchronous.MemberName.ShouldBe("connectionString");
        resolved.Synchronous.ShouldBe(resolved.Asynchronous);
    }

    [Fact]
    public void Resolve_NamedAndDefaultInOneTier_DoNotPair()
    {
        var named = Global(
            "global::App.Db",
            "OpenMainAsync",
            CandidateType.Factory,
            CandidateMember.Method,
            database: "main",
            isAsync: true
        );
        var fallback = Global("global::App.Db", "Open", CandidateType.Factory, CandidateMember.Method);

        var resolution = Resolve(Tiers([], global: [named, fallback]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Asynchronous.MemberName.ShouldBe("Db.OpenMainAsync");
        resolved.Synchronous.ShouldBeNull();
    }

    // A shared mark for a database the repository does not use is not SQLSRC031, and gives no tier a candidate.
    [Fact]
    public void Resolve_SharedMarkForUnusedDatabase_IsIgnoredSilently()
    {
        var held = Held("dbf", "OpenAudit", database: "audit");
        var unused = Global("global::App.Db", "Audit", CandidateType.ConnectionString, database: "audit");
        var global = Global("global::App.Db", "Main", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([], held: [held], global: [unused, global]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("Db.Main");
    }

    [Fact]
    public void Resolve_SharedDefaultOfOtherDriver_IsIgnored()
    {
        var global = Global("global::App.Db", "Source", CandidateType.DataSource, declared: "Npgsql.NpgsqlDataSource");

        var resolution = Resolve(Tiers([], global: [global]), Sales);

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasNoConnection);
        resolution.FailedDatabases.ShouldBe(["sales"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // A default of the other engine's driver is skipped for that database alone, and still serves its own engine's.
    [Fact]
    public void Resolve_SharedDefaultOfOneDriver_ServesItsOwnEngine()
    {
        var global = Global("global::App.Db", "Source", CandidateType.DataSource, declared: "Npgsql.NpgsqlDataSource");
        var sqlServer = Global(
            "global::App.Db",
            "SqlSource",
            CandidateType.DataSource,
            declared: "Microsoft.Data.SqlClient.SqlDataSource"
        );

        var resolution = Resolve(Tiers([], global: [global, sqlServer]), Main, Sales);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "Db.Source"), ("sales", "Db.SqlSource")]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_SharedNamedOfOtherDriver_ReportsSqlsrc033(bool held)
    {
        var candidate = held
            ? Held(
                "dbf",
                "Source",
                CandidateType.DataSource,
                CandidateMember.Property,
                database: "sales",
                declared: "Npgsql.NpgsqlDataSource"
            )
            : Global(
                "global::App.Db",
                "Source",
                CandidateType.DataSource,
                database: "sales",
                declared: "Npgsql.NpgsqlDataSource"
            );
        var shape = held ? Tiers([], held: [candidate]) : Tiers([], global: [candidate]);

        var resolution = Resolve(shape, Sales);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionOfAnotherEngine);
        diagnostic.Location.ShouldBe(held ? candidate.Holder!.Location : TestModels.AttributeLocation);
        diagnostic.Arguments.ShouldBe([
            "App.UserRepository",
            held ? "IDatabaseFactory.Source' reached through 'dbf" : "Db.Source",
            "Npgsql.NpgsqlDataSource",
            "sales",
            Sales.Engine.DatabaseName(),
            Sales.Engine.DriverName(),
        ]);
        resolution.FailedDatabases.ShouldBe(["sales"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // The unsupported member fails the database, so the global below it is not used either.
    [Fact]
    public void Resolve_HeldUnsupported_ReportsSqlsrc032AtTheHolder()
    {
        var held = Held("dbf", "Open", CandidateType.Unsupported);
        var global = Global("global::App.Db", "Main", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([], held: [held], global: [global]), Main, Reports);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionMemberNotSupported);
        diagnostic.Location.ShouldBe(held.Holder!.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "IDatabaseFactory.Open' reached through 'dbf"]);
        resolution.FailedDatabases.ShouldBe(["main", "reports"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // Two members of one name, of two interfaces of the holder's class, are told apart: a member reached through a
    // cast is named by its interface, in SQLSRC029's list and in the messages of SQLSRC032 and SQLSRC033, and any
    // other by the holder's type.
    [Fact]
    public void Resolve_TwoHeldMembersOfOneNameThroughACast_ReportsSqlsrc029NamingTheInterface()
    {
        var direct = Held("dbf", "Open");
        var cast = Held("dbf", "Open") with
        {
            CastTypeText = "global::App.IAuditOpener",
            CastTypeName = "IAuditOpener",
        };

        var resolution = Resolve(Tiers([], held: [direct, cast]), Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasManyConnections);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "main", "'dbf.Open', 'dbf.IAuditOpener.Open'"]);
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("other engine")]
    public void Resolve_HeldProblemReachedThroughACast_NamesTheInterface(string problem)
    {
        var held =
            problem == "unsupported"
                ? Held("dbf", "Open", CandidateType.Unsupported)
                : Held("dbf", "Open", database: "sales", declared: "Npgsql.NpgsqlConnection");
        var cast = held with { CastTypeText = "global::App.IAuditOpener", CastTypeName = "IAuditOpener" };

        var resolution = Resolve(Tiers([], held: [cast]), Sales);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(
            problem == "unsupported"
                ? SqlDiagnostics.ConnectionMemberNotSupported
                : SqlDiagnostics.ConnectionOfAnotherEngine
        );
        diagnostic.Location.ShouldBe(cast.Holder!.Location);
        diagnostic.Arguments[1].ShouldBe("IAuditOpener.Open' reached through 'dbf");
    }

    // An unsupported held member for a database fails that one alone, and the default serves the other.
    [Fact]
    public void Resolve_HeldUnsupportedForADatabase_FailsThatOne()
    {
        var broken = Held("dbf", "OpenReports", CandidateType.Unsupported, database: "reports");

        var resolution = Resolve(Tiers([], held: [broken, Held("dbf", "Open")]), Main, Reports);

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.ConnectionMemberNotSupported);
        resolution.FailedDatabases.ShouldBe(["reports"]);
        resolution.Sources.ShouldHaveSingleItem().Database.ShouldBe("main");
    }

    // A broken default of one driver is skipped by a database of the other engine, as a valid one is, and the global
    // below it serves that database.
    [Fact]
    public void Resolve_HeldUnsupportedDefaultOfOtherDriver_IsIgnored()
    {
        var broken = Held("dbf", "Source", CandidateType.Unsupported, declared: "Npgsql.NpgsqlDataSource");
        var global = Global("global::App.Db", "Main", CandidateType.ConnectionString);

        var resolution = Resolve(Tiers([], held: [broken], global: [global]), Sales);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("Db.Main");
    }

    [Fact]
    public void Resolve_HeldUnsupportedDefaultOfOwnDriver_ReportsSqlsrc032()
    {
        var broken = Held("dbf", "Source", CandidateType.Unsupported, declared: "Npgsql.NpgsqlDataSource");

        var resolution = Resolve(Tiers([], held: [broken]), Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionMemberNotSupported);
        diagnostic.Location.ShouldBe(broken.Holder!.Location);
        resolution.FailedDatabases.ShouldBe(["main"]);
    }

    // A held member is not looked at for a database that the repository's own members decide.
    [Fact]
    public void Resolve_HeldUnsupportedForADecidedDatabase_IsNotReported()
    {
        var own = Candidate("connectionString", CandidateType.ConnectionString);
        var held = Held("dbf", "Open", CandidateType.Unsupported);

        var resolution = Resolve(Tiers([own], held: [held]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("connectionString");
    }

    // A database that the repository's own members fail is decided, and keeps today's problem: the global, which
    // would serve it, is not used.
    [Theory]
    [InlineData("unsupported")]
    [InlineData("without database")]
    [InlineData("held parameters")]
    [InlineData("hidden parameter")]
    [InlineData("other engine")]
    public void Resolve_FailedOwnTierBlocksLowerTiers(string failure)
    {
        var global = Global("global::App.Db", "Main", CandidateType.ConnectionString);
        var (own, needed, descriptor, arguments) = failure switch
        {
            "unsupported" => (
                Tiers([Candidate("Open", CandidateType.Unsupported, CandidateMember.Method, true)], global: [global]),
                new NeededDatabase[] { Main, Reports },
                SqlDiagnostics.ConnectionMemberNotSupported,
                new[] { "App.UserRepository", "Open" }
            ),
            "without database" => (
                Tiers([Candidate("dataSource", CandidateType.DataSource, isMarked: true)], global: [global]),
                [Main, Reports],
                SqlDiagnostics.ConnectionNeedsDatabase,
                ["App.UserRepository", "dataSource", "'main', 'reports'"]
            ),
            "held parameters" => (
                Tiers([], global: [global]) with
                {
                    HeldParameters = TestSidecars.Of("connectionString"),
                },
                [Main],
                SqlDiagnostics.RepositoryHasNoConnection,
                [
                    "App.UserRepository",
                    "main",
                    "The primary-constructor parameter 'connectionString' is not chosen by its name or type, since "
                        + "the type also uses it to initialize a field or property, or passes it to its base type: "
                        + "keep the value in a field or property named connectionString or of a DbDataSource type, "
                        + "or mark the parameter [SqlSourceConnection].",
                ]
            ),
            "hidden parameter" => (
                Tiers(
                    [Candidate("cancellationToken", CandidateType.DataSource, CandidateMember.Parameter)],
                    global: [global]
                ),
                [Main],
                SqlDiagnostics.ConnectionParameterHidden,
                ["App.UserRepository", "cancellationToken", "main"]
            ),
            "other engine" => (
                Tiers(
                    [Candidate("dataSource", CandidateType.DataSource, declared: "Npgsql.NpgsqlDataSource")],
                    global: [global]
                ),
                [Sales],
                SqlDiagnostics.ConnectionOfAnotherEngine,
                [
                    "App.UserRepository",
                    "dataSource",
                    "Npgsql.NpgsqlDataSource",
                    "sales",
                    Sales.Engine.DatabaseName(),
                    Sales.Engine.DriverName(),
                ]
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };

        var resolution = Resolve(own, needed);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(descriptor);
        diagnostic.Arguments.ShouldBe(arguments);
        resolution.FailedDatabases.ShouldBe(needed.Select(database => database.Name));
        resolution.Sources.ShouldBeEmpty();
    }

    // No convention takes a parameter on a type of several databases, so a parameter that the type passes on decides
    // none of them, and the held tier serves each.
    [Fact]
    public void Resolve_HeldParametersOnSeveralDatabases_LeaveThemToTheSharedTiers()
    {
        var shape = Tiers(
            [],
            held: [Held("dbf", "Main", database: "main"), Held("dbf", "Reports", database: "reports")]
        ) with
        {
            HeldParameters = TestSidecars.Of("dataSource"),
        };

        var resolution = Resolve(shape, Main, Reports);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "dbf.Main"), ("reports", "dbf.Reports")]);
    }

    [Fact]
    public void Resolve_HolderParameterNamedCancellationToken_ReportsSqlsrc036()
    {
        var held = Held("cancellationToken", "Open");

        var resolution = Resolve(Tiers([], held: [held]), Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionParameterHidden);
        diagnostic.Location.ShouldBe(held.Holder!.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "cancellationToken", "main"]);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // A pair reached through one hidden holder reports the holder once.
    [Fact]
    public void Resolve_PairThroughHolderParameterNamedCancellationToken_ReportsSqlsrc036Once()
    {
        var asynchronous = Held("cancellationToken", "OpenAsync", isAsync: true);
        var synchronous = Held("cancellationToken", "Open");

        var resolution = Resolve(Tiers([], held: [asynchronous, synchronous]), Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionParameterHidden);
        diagnostic.Location.ShouldBe(asynchronous.Holder!.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "cancellationToken", "main"]);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // Only a holder that is a parameter is hidden by an argument: a field of the name is reached through this.
    [Fact]
    public void Resolve_HolderFieldNamedCancellationToken_IsTheSource()
    {
        var held = Held("cancellationToken", "Open", holderMember: CandidateMember.Field);

        var resolution = Resolve(Tiers([], held: [held]), Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.AccessText.ShouldBe("this.cancellationToken.Open");
    }

    [Fact]
    public void Resolve_NoTierHasACandidate_ReportsSqlsrc028WithTheNewAdvice()
    {
        var resolution = Resolve(Tiers([]), Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasNoConnection);
        diagnostic.Arguments.ShouldBe([
            "App.UserRepository",
            "main",
            "Add a string field, property or primary-constructor parameter named connectionString, or one of a "
                + "DbDataSource type, or mark the member that gives the connection [SqlSourceConnection]"
                + Places,
        ]);
    }

    // A mark of the repository's own for another database leaves this one to the shared tiers, and the advice says
    // where else its mark can be.
    [Fact]
    public void Resolve_OwnMarkForAnotherDatabaseAndNoOtherTier_ReportsSqlsrc028WithTheNewAdvice()
    {
        var own = Candidate("audit", CandidateType.DataSource, isMarked: true, database: "audit");

        var resolution = Resolve(Tiers([own]), Main);

        resolution.Diagnostics.Select(diagnostic => diagnostic.Descriptor.Id).ShouldBe(["SQLSRC031", "SQLSRC028"]);
        resolution
            .Diagnostics[1]
            .Arguments[2]
            .ShouldBe(
                "A member is marked [SqlSourceConnection], so no other is chosen by its name or type: mark the "
                    + "member that gives this database's connection [SqlSourceConnection]"
                    + Places
            );
    }

    [Fact]
    public void Resolve_SharedDatabaseNameComparedIgnoringCase()
    {
        var main = Global("global::App.Db", "Main", CandidateType.ConnectionString);
        var reports = Global("global::App.Db", "Reports", CandidateType.ConnectionString, database: "Reports");

        var resolution = Resolve(Tiers([], global: [main, reports]), Main, Reports);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "Db.Main"), ("reports", "Db.Reports")]);
    }

    [Fact]
    public void Resolve_StaticTypeWithAGlobal_ReportsSqlsrc034()
    {
        var global = Global("global::App.Db", "Main", CandidateType.ConnectionString);

        var resolution = ConnectionResolver.Resolve(
            TestModels.Type(isStatic: true),
            Tiers([], global: [global]),
            [Main]
        );

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.RepositoryIsStatic);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    private static ConnectionResolution Resolve(RepositoryShape shape, params NeededDatabase[] needed) =>
        ConnectionResolver.Resolve(TestModels.Type(), shape, needed);
}
