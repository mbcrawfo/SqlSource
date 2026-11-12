using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Repositories;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

// Which member gives a repository's methods their connection for each database its repository queries use, from the
// candidates the reader found, and the diagnostics of a type that has none, more than one, or a member it cannot use.
public sealed class ConnectionResolverTests
{
    private static readonly NeededDatabase Main = new("main", DatabaseEngine.PostgreSql);

    private static readonly NeededDatabase Reports = new("reports", DatabaseEngine.PostgreSql);

    private static readonly NeededDatabase Sales = new("sales", DatabaseEngine.SqlServer);

    [Fact]
    public void Resolve_OneConventionCandidate_IsTheSource()
    {
        var candidate = Candidate("_connectionString", CandidateType.ConnectionString);

        var resolution = Resolve([candidate], Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Database.ShouldBe("main");
        resolved.Asynchronous.ShouldBe(
            new ConnectionSource(
                ConnectionSourceKind.ConnectionString,
                CandidateMember.Field,
                "_connectionString",
                "this._connectionString",
                false,
                false,
                null,
                "App.UserRepository",
                false
            )
        );
    }

    [Fact]
    public void Resolve_TwoConventionCandidates_ReportsSqlsrc029()
    {
        var resolution = Resolve(
            [
                Candidate("_connectionString", CandidateType.ConnectionString),
                Candidate("dataSource", CandidateType.DataSource),
            ],
            Main
        );

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasManyConnections);
        diagnostic.Location.ShouldBe(TestModels.AttributeLocation);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "main", "'_connectionString', 'dataSource'"]);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_NoCandidate_ReportsSqlsrc028AndFailsTheDatabase()
    {
        var resolution = Resolve([], Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasNoConnection);
        diagnostic.Location.ShouldBe(TestModels.AttributeLocation);
        diagnostic.Arguments.Take(2).ShouldBe(["App.UserRepository", "main"]);
        diagnostic.Arguments[2].ShouldContain("connectionString");
        diagnostic.Arguments[2].ShouldContain("DbDataSource");
        diagnostic.Arguments[2].ShouldContain("[SqlSourceConnection]");
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // A parameter that the conventions would take, but that the type passes on or stores, is named with what to do.
    [Theory]
    [InlineData(
        new[] { "connectionString" },
        "The primary-constructor parameter 'connectionString' is not chosen by its name or type, since the type also "
            + "uses it to initialize a field or property, or passes it to its base type: keep the value in a field or "
            + "property named connectionString or of a DbDataSource type, or mark the parameter [SqlSourceConnection]."
    )]
    [InlineData(
        new[] { "connectionString", "dataSource" },
        "The primary-constructor parameters 'connectionString', 'dataSource' are not chosen by their name or type, "
            + "since the type also uses them to initialize a field or property, or passes them to its base type: keep "
            + "the value in a field or property named connectionString or of a DbDataSource type, or mark the "
            + "parameter [SqlSourceConnection]."
    )]
    public void Resolve_HeldParameters_ReportsSqlsrc028NamingThem(string[] held, string advice)
    {
        var shape = Shape() with { HeldParameters = TestSidecars.Of(held) };

        var resolution = ConnectionResolver.Resolve(TestModels.Type(), shape, [Main]);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasNoConnection);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "main", advice]);
        resolution.FailedDatabases.ShouldBe(["main"]);
    }

    // Without a mark, no convention says which database a member is for.
    [Fact]
    public void Resolve_TwoDatabasesWithoutMarks_ReportsSqlsrc028ForEach()
    {
        var resolution = Resolve([Candidate("dataSource", CandidateType.DataSource)], Main, Reports);

        resolution.Diagnostics.Select(diagnostic => diagnostic.Descriptor.Id).ShouldBe(["SQLSRC028", "SQLSRC028"]);
        resolution.Diagnostics.Select(diagnostic => diagnostic.Arguments[1]).ShouldBe(["main", "reports"]);
        resolution.Diagnostics[0].Arguments[2].ShouldContain("[SqlSourceConnection(Database = \"main\")]");
        resolution.FailedDatabases.ShouldBe(["main", "reports"]);
        resolution.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_MarkedWithoutDatabaseOnTwoDatabases_ReportsSqlsrc030()
    {
        var candidate = Candidate("dataSource", CandidateType.DataSource, isMarked: true);

        var resolution = Resolve([candidate], Main, Reports);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionNeedsDatabase);
        diagnostic.Location.ShouldBe(candidate.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "dataSource", "'main', 'reports'"]);
        resolution.FailedDatabases.ShouldBe(["main", "reports"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // A database that a member is marked for keeps it, whatever the unnamed mark of another.
    [Fact]
    public void Resolve_MarkedWithoutDatabaseBesideOneMarkedForADatabase_FailsOnlyTheOther()
    {
        var resolution = Resolve(
            [
                Candidate("dataSource", CandidateType.DataSource, isMarked: true),
                Candidate("_reports", CandidateType.DataSource, isMarked: true, database: "reports"),
            ],
            Main,
            Reports
        );

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.ConnectionNeedsDatabase);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldHaveSingleItem().Database.ShouldBe("reports");
    }

    [Fact]
    public void Resolve_MarkedWithoutDatabaseOnOneDatabase_IsItsSource()
    {
        var resolution = Resolve(
            [Candidate("GetConnectionString", CandidateType.ConnectionString, CandidateMember.Method, isMarked: true)],
            Main
        );

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.AccessText.ShouldBe("this.GetConnectionString");
    }

    [Fact]
    public void Resolve_MarkedPerDatabase_ResolvesEach()
    {
        var resolution = Resolve(
            [
                Candidate("_reports", CandidateType.DataSource, isMarked: true, database: "REPORTS"),
                Candidate("_main", CandidateType.ConnectionString, isMarked: true, database: "main"),
            ],
            Main,
            Reports
        );

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved => (resolved.Database, resolved.Asynchronous.MemberName))
            .ShouldBe([("main", "_main"), ("reports", "_reports")]);
    }

    [Fact]
    public void Resolve_TwoMarkedForOneDatabase_ReportsSqlsrc029()
    {
        var resolution = Resolve(
            [
                Candidate("_first", CandidateType.DataSource, isMarked: true),
                Candidate("_second", CandidateType.DataSource, isMarked: true, database: "main"),
            ],
            Main
        );

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasManyConnections);
        diagnostic.Arguments[2].ShouldBe("'_first', '_second'");
        resolution.FailedDatabases.ShouldBe(["main"]);
    }

    [Fact]
    public void Resolve_MarkedForAnUnusedDatabase_WarnsSqlsrc031AndResolvesTheRest()
    {
        var unused = Candidate("_archive", CandidateType.DataSource, isMarked: true, database: "archive");

        var resolution = Resolve(
            [unused, Candidate("_main", CandidateType.DataSource, isMarked: true, database: "main")],
            Main
        );

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionDatabaseNotUsed);
        diagnostic.Location.ShouldBe(unused.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "_archive", "archive", "'main'"]);
        resolution.FailedDatabases.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.MemberName.ShouldBe("_main");
    }

    [Fact]
    public void Resolve_UnsupportedMarked_ReportsSqlsrc032()
    {
        var unsupported = Candidate("Connect", CandidateType.Unsupported, CandidateMember.Method, isMarked: true);

        var resolution = Resolve([unsupported], Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionMemberNotSupported);
        diagnostic.Location.ShouldBe(unsupported.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", "Connect"]);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // An unsupported member that names no database stands for each, beside a member that names one.
    [Fact]
    public void Resolve_UnsupportedMarkedWithoutDatabaseOnTwoDatabases_FailsEveryDatabase()
    {
        var resolution = Resolve(
            [
                Candidate("Connect", CandidateType.Unsupported, CandidateMember.Method, isMarked: true),
                Candidate("_main", CandidateType.DataSource, isMarked: true, database: "main"),
            ],
            Main,
            Reports
        );

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.ConnectionMemberNotSupported);
        resolution.FailedDatabases.ShouldBe(["main", "reports"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // An unsupported member marked for one database fails that one alone.
    [Fact]
    public void Resolve_UnsupportedMarkedForADatabase_FailsOnlyItsDatabase()
    {
        var resolution = Resolve(
            [
                Candidate(
                    "Connect",
                    CandidateType.Unsupported,
                    CandidateMember.Method,
                    isMarked: true,
                    database: "reports"
                ),
                Candidate("_main", CandidateType.DataSource, isMarked: true, database: "main"),
            ],
            Main,
            Reports
        );

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.ConnectionMemberNotSupported);
        resolution.FailedDatabases.ShouldBe(["reports"]);
        resolution.Sources.ShouldHaveSingleItem().Database.ShouldBe("main");
    }

    [Fact]
    public void Resolve_NpgsqlDataSourceForMssql_ReportsSqlsrc033()
    {
        var candidate = Candidate("dataSource", CandidateType.DataSource, declared: "Npgsql.NpgsqlDataSource");

        var resolution = Resolve([candidate], Sales);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionOfAnotherEngine);
        diagnostic.Location.ShouldBe(candidate.Location);
        diagnostic.Arguments.ShouldBe([
            "App.UserRepository",
            "dataSource",
            "Npgsql.NpgsqlDataSource",
            "sales",
            "SQL Server",
            "Microsoft.Data.SqlClient",
        ]);
        resolution.FailedDatabases.ShouldBe(["sales"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // A repository's method has an argument of the name, which would hide the parameter inside it.
    [Theory]
    [InlineData("parameters")]
    [InlineData("cancellationToken")]
    public void Resolve_ParameterNamedAsAnArgument_ReportsSqlsrc036(string name)
    {
        var candidate = Candidate(name, CandidateType.DataSource, CandidateMember.Parameter);

        var resolution = Resolve([candidate], Main with { TakesParameters = true });

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionParameterHidden);
        diagnostic.Location.ShouldBe(candidate.Location);
        diagnostic.Arguments.ShouldBe(["App.UserRepository", name, "main"]);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // No query of the database has an input model, so no method has an argument named parameters.
    [Fact]
    public void Resolve_ParameterNamedParametersWithoutAnInputModel_IsTheSource()
    {
        var resolution = Resolve([Candidate("parameters", CandidateType.DataSource, CandidateMember.Parameter)], Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.AccessText.ShouldBe("parameters");
    }

    // Every method takes a cancellation token, whatever its query.
    [Fact]
    public void Resolve_ParameterNamedCancellationTokenWithoutAnInputModel_ReportsSqlsrc036()
    {
        var resolution = Resolve(
            [Candidate("cancellationToken", CandidateType.DataSource, CandidateMember.Parameter)],
            Main
        );

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.ConnectionParameterHidden);
    }

    // A member is reached through this, which no argument hides.
    [Fact]
    public void Resolve_FieldNamedAsAnArgument_IsTheSource() =>
        Resolve([Candidate("parameters", CandidateType.DataSource)], Main)
            .Sources.ShouldHaveSingleItem()
            .Asynchronous.AccessText.ShouldBe("this.parameters");

    // The resolution says whether the repository is a struct, whose streaming methods read the source first.
    [Fact]
    public void Resolve_StructRepository_SaysSo()
    {
        var type = TestModels.Type(types: [new TypeDeclaration("struct", "Repo", "Repo", "", 0)]);

        var resolution = ConnectionResolver.Resolve(
            type,
            Shape(Candidate("dataSource", CandidateType.DataSource, CandidateMember.Parameter)),
            [Main]
        );

        resolution.Sources.ShouldHaveSingleItem().Asynchronous.InStruct.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_SqlConnectionForPostgres_ReportsSqlsrc033()
    {
        var candidate = Candidate(
            "_connection",
            CandidateType.Connection,
            isMarked: true,
            declared: "Microsoft.Data.SqlClient.SqlConnection"
        );

        var resolution = Resolve([candidate], Main);

        resolution.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.ConnectionOfAnotherEngine);
        resolution.FailedDatabases.ShouldBe(["main"]);
    }

    // A type of System.Data.Common, or of the engine's own driver, is of no other engine.
    [Theory]
    [InlineData("System.Data.Common.DbDataSource")]
    [InlineData("Npgsql.NpgsqlDataSource")]
    public void Resolve_DataSourceOfNoOtherEngine_IsTheSource(string declared)
    {
        var resolution = Resolve([Candidate("dataSource", CandidateType.DataSource, declared: declared)], Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.Sources.ShouldHaveSingleItem().Asynchronous.Kind.ShouldBe(ConnectionSourceKind.DataSource);
    }

    [Fact]
    public void Resolve_StaticType_ReportsSqlsrc034()
    {
        var resolution = ConnectionResolver.Resolve(TestModels.Type(isStatic: true), Shape(), [Main, Reports]);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryIsStatic);
        diagnostic.Location.ShouldBe(TestModels.AttributeLocation);
        diagnostic.Arguments.ShouldBe(["App.UserRepository"]);
        resolution.FailedDatabases.ShouldBe(["main", "reports"]);
        resolution.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_NoDatabaseNeeded_ReportsNothing()
    {
        var resolution = ConnectionResolver.Resolve(TestModels.Type(isStatic: true), Shape(), []);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        resolution.Sources.ShouldBeEmpty();
    }

    // The cases name the enums' members, which are internal: what the candidate gives, what kind of member it is,
    // and the kind of source it is.
    [Theory]
    [InlineData("ConnectionString", "Field", "ConnectionString")]
    [InlineData("ConnectionString", "Property", "ConnectionString")]
    [InlineData("ConnectionString", "Parameter", "ConnectionString")]
    [InlineData("ConnectionString", "Method", "ConnectionString")]
    [InlineData("DataSource", "Field", "DataSource")]
    [InlineData("DataSource", "Parameter", "DataSource")]
    [InlineData("Factory", "Method", "Factory")]
    [InlineData("Connection", "Field", "Borrowed")]
    [InlineData("Connection", "Property", "Borrowed")]
    [InlineData("Connection", "Parameter", "Borrowed")]
    public void Resolve_MarkedCandidate_HasTheKindOfWhatItGives(string type, string member, string kind)
    {
        var candidate = Candidate(
            "source",
            Enum.Parse<CandidateType>(type),
            Enum.Parse<CandidateMember>(member),
            isMarked: true
        );

        var resolution = Resolve([candidate], Main);

        resolution.Sources.ShouldHaveSingleItem().Asynchronous.Kind.ShouldBe(Enum.Parse<ConnectionSourceKind>(kind));
    }

    [Fact]
    public void Resolve_AsyncFactoryWithAToken_KeepsBoth()
    {
        var candidate = Candidate("OpenAsync", CandidateType.Factory, CandidateMember.Method, isMarked: true) with
        {
            IsAsync = true,
            TakesCancellationToken = true,
        };

        var source = Resolve([candidate], Main).Sources.ShouldHaveSingleItem().Asynchronous;

        source.IsAsync.ShouldBeTrue();
        source.TakesCancellationToken.ShouldBeTrue();
        source.AccessText.ShouldBe("this.OpenAsync");
    }

    [Fact]
    public void Resolve_Parameter_IsAccessedByItsName()
    {
        var resolution = Resolve([Candidate("dataSource", CandidateType.DataSource, CandidateMember.Parameter)], Main);

        var source = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        source.AccessText.ShouldBe("dataSource");
        source.LeadingName.ShouldBe("dataSource");
    }

    // A method's own argument or local of a parameter's name would hide it, so its name is the one the access starts
    // with; an instance member is reached through this, which nothing hides.
    [Fact]
    public void Resolve_InstanceMember_HasNoLeadingName() =>
        Resolve([Candidate("_dataSource", CandidateType.DataSource)], Main)
            .Sources.ShouldHaveSingleItem()
            .Asynchronous.LeadingName.ShouldBeNull();

    // A method returning a connection string is called, so the source says which kind of member it is.
    [Fact]
    public void Resolve_MethodReturningAConnectionString_IsAMethodSource() =>
        Resolve([Candidate("GetConnectionString", CandidateType.ConnectionString, CandidateMember.Method, true)], Main)
            .Sources.ShouldHaveSingleItem()
            .Asynchronous.Member.ShouldBe(CandidateMember.Method);

    [Fact]
    public void Resolve_KeywordName_IsEscaped()
    {
        var resolution = Resolve(
            [Candidate("string", CandidateType.ConnectionString, CandidateMember.Parameter, isMarked: true)],
            Main
        );

        var source = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        source.MemberName.ShouldBe("string");
        source.AccessText.ShouldBe("@string");
    }

    // A static member, the type's own or a base type's, is reached through the type's name as its code writes it.
    [Fact]
    public void Resolve_StaticMemberOfAGenericType_IsAccessedThroughTheType()
    {
        var type = TestModels.Type(types: [new TypeDeclaration("class", "Repo", "Repo", "<T>", 1)]);
        var candidate = Candidate("ConnectionString", CandidateType.ConnectionString, CandidateMember.Property) with
        {
            IsStatic = true,
        };

        var resolution = ConnectionResolver.Resolve(type, Shape(candidate), [Main]);

        var source = resolution.Sources.ShouldHaveSingleItem().Asynchronous;
        source.AccessText.ShouldBe("Repo<T>.ConnectionString");
        source.LeadingName.ShouldBeNull();
        source.TypeName.ShouldBe("App.Repo<T>");
    }

    // A local of the type's name would hide a type that is not generic, which a static member is reached through.
    [Fact]
    public void Resolve_StaticMemberOfATypeThatIsNotGeneric_StartsWithTheType()
    {
        var type = TestModels.Type(types: [new TypeDeclaration("class", "Repo", "Repo", "", 0)]);
        var candidate = Candidate("ConnectionString", CandidateType.ConnectionString, CandidateMember.Property) with
        {
            IsStatic = true,
        };

        var source = ConnectionResolver
            .Resolve(type, Shape(candidate), [Main])
            .Sources.ShouldHaveSingleItem()
            .Asynchronous;

        source.AccessText.ShouldBe("Repo.ConnectionString");
        source.LeadingName.ShouldBe("Repo");
    }

    // The type's name is as its declaration writes it, so a keyword keeps its @.
    [Fact]
    public void Resolve_StaticMemberOfATypeNamedByAKeyword_EscapesTheTypesName()
    {
        var type = TestModels.Type(types: [new TypeDeclaration("class", "@class", "class", "<@int>", 1)]);
        var candidate = Candidate("ConnectionString", CandidateType.ConnectionString, CandidateMember.Property) with
        {
            IsStatic = true,
        };

        var resolution = ConnectionResolver.Resolve(type, Shape(candidate), [Main]);

        resolution.Sources.ShouldHaveSingleItem().Asynchronous.AccessText.ShouldBe("@class<@int>.ConnectionString");
    }

    // A source that returns no task serves both kinds of method.
    [Fact]
    public void Resolve_OneSourceThatReturnsNoTask_IsTheSourceOfBothKindsOfMethod()
    {
        var resolved = Resolve([Candidate("dataSource", CandidateType.DataSource)], Main)
            .Sources.ShouldHaveSingleItem();

        resolved.Asynchronous.MemberName.ShouldBe("dataSource");
        resolved.Synchronous.ShouldBe(resolved.Asynchronous);
    }

    // A factory that returns a task serves asynchronous methods alone.
    [Fact]
    public void Resolve_OneFactoryThatReturnsATask_HasNoSynchronousSource()
    {
        var resolution = Resolve([AsyncFactory("OpenAsync")], Main);

        resolution.Diagnostics.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Asynchronous.MemberName.ShouldBe("OpenAsync");
        resolved.Synchronous.ShouldBeNull();
    }

    // The asynchronous methods take the one that returns a task, whichever is declared first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolve_PairOfFactories_GivesEachKindOfMethodItsOwn(bool asyncFirst)
    {
        var asynchronous = AsyncFactory("OpenAsync");
        var synchronous = Candidate("Open", CandidateType.Factory, CandidateMember.Method, isMarked: true);

        var resolution = Resolve(asyncFirst ? [asynchronous, synchronous] : [synchronous, asynchronous], Main);

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Database.ShouldBe("main");
        resolved.Asynchronous.MemberName.ShouldBe("OpenAsync");
        resolved.Asynchronous.IsAsync.ShouldBeTrue();
        resolved.Synchronous.ShouldNotBeNull().MemberName.ShouldBe("Open");
        resolved.Synchronous.IsAsync.ShouldBeFalse();
    }

    // Any source that returns no task pairs with a factory that returns one.  The cases name the enums' members,
    // which are internal.
    [Theory]
    [InlineData("DataSource", "Field", "DataSource")]
    [InlineData("ConnectionString", "Property", "ConnectionString")]
    [InlineData("Connection", "Field", "Borrowed")]
    public void Resolve_PairOfAFactoryAndAnotherSource_GivesEachKindOfMethodItsOwn(
        string type,
        string member,
        string kind
    )
    {
        var other = Candidate("source", Enum.Parse<CandidateType>(type), Enum.Parse<CandidateMember>(member), true);

        var resolution = Resolve([other, AsyncFactory("OpenAsync")], Main);

        resolution.Diagnostics.ShouldBeEmpty();
        var resolved = resolution.Sources.ShouldHaveSingleItem();
        resolved.Asynchronous.MemberName.ShouldBe("OpenAsync");
        resolved.Synchronous.ShouldNotBeNull().Kind.ShouldBe(Enum.Parse<ConnectionSourceKind>(kind));
    }

    [Fact]
    public void Resolve_TwoFactoriesThatReturnATask_ReportsSqlsrc029()
    {
        var resolution = Resolve([AsyncFactory("OpenAsync"), AsyncFactory("ConnectAsync")], Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasManyConnections);
        diagnostic.Arguments[2].ShouldBe("'OpenAsync', 'ConnectAsync'");
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // A pair is two members: a third is one too many, whichever of them returns a task.
    [Fact]
    public void Resolve_ThreeMarkedOfWhichOneReturnsATask_ReportsSqlsrc029()
    {
        var resolution = Resolve(
            [
                AsyncFactory("OpenAsync"),
                Candidate("_dataSource", CandidateType.DataSource, isMarked: true),
                Candidate("Open", CandidateType.Factory, CandidateMember.Method, isMarked: true),
            ],
            Main
        );

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.RepositoryHasManyConnections);
        diagnostic.Arguments[2].ShouldBe("'OpenAsync', '_dataSource', 'Open'");
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_PairForOneDatabaseBesideAnotherDatabasesOneSource_ResolvesEach()
    {
        var resolution = Resolve(
            [
                AsyncFactory("OpenReportsAsync", "reports"),
                Candidate("_main", CandidateType.DataSource, isMarked: true, database: "main"),
                Candidate("OpenReports", CandidateType.Factory, CandidateMember.Method, true, "Reports"),
            ],
            Main,
            Reports
        );

        resolution.Diagnostics.ShouldBeEmpty();
        resolution.FailedDatabases.ShouldBeEmpty();
        resolution
            .Sources.Select(resolved =>
                (resolved.Database, resolved.Asynchronous.MemberName, Synchronous: resolved.Synchronous?.MemberName)
            )
            .ShouldBe([("main", "_main", "_main"), ("reports", "OpenReportsAsync", "OpenReports")]);
    }

    // A problem of either member of a pair fails the database, and each is checked as a source alone would be.
    [Fact]
    public void Resolve_PairWithASourceOfAnotherEngine_ReportsSqlsrc033AndFailsTheDatabase()
    {
        var other = Candidate(
            "_dataSource",
            CandidateType.DataSource,
            isMarked: true,
            declared: "Npgsql.NpgsqlDataSource"
        );

        var resolution = Resolve([AsyncFactory("OpenAsync"), other], Sales);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionOfAnotherEngine);
        diagnostic.Location.ShouldBe(other.Location);
        resolution.FailedDatabases.ShouldBe(["sales"]);
        resolution.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_PairWithAHiddenParameter_ReportsSqlsrc036AndFailsTheDatabase()
    {
        var hidden = Candidate("cancellationToken", CandidateType.DataSource, CandidateMember.Parameter, true);

        var resolution = Resolve([hidden, AsyncFactory("OpenAsync")], Main);

        var diagnostic = resolution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.ConnectionParameterHidden);
        diagnostic.Location.ShouldBe(hidden.Location);
        resolution.FailedDatabases.ShouldBe(["main"]);
        resolution.Sources.ShouldBeEmpty();
    }

    // Each member's problem is reported, not only the first.
    [Fact]
    public void Resolve_PairWhoseMembersBothHaveAProblem_ReportsEach()
    {
        var asynchronous = AsyncFactory("OpenAsync") with { DeclaredTypeName = "Npgsql.NpgsqlConnection" };
        var hidden = Candidate("cancellationToken", CandidateType.DataSource, CandidateMember.Parameter, true);

        var resolution = Resolve([asynchronous, hidden], Sales);

        resolution
            .Diagnostics.Select(diagnostic => diagnostic.Descriptor)
            .ShouldBe([SqlDiagnostics.ConnectionOfAnotherEngine, SqlDiagnostics.ConnectionParameterHidden]);
        resolution.FailedDatabases.ShouldBe(["sales"]);
        resolution.Sources.ShouldBeEmpty();
    }

    private static ConnectionResolution Resolve(ConnectionCandidate[] candidates, params NeededDatabase[] needed) =>
        ConnectionResolver.Resolve(TestModels.Type(), Shape(candidates), needed);

    private static RepositoryShape Shape(params ConnectionCandidate[] candidates) =>
        new("App.UserRepository", TestSidecars.Of(candidates), [], true, "IUserRepository", null);

    // A candidate declared at a place of its own in the type's file, a field of a string unless the test says
    // otherwise.
    private static ConnectionCandidate Candidate(
        string name,
        CandidateType type,
        CandidateMember member = CandidateMember.Field,
        bool isMarked = false,
        string? database = null,
        string? declared = null
    ) =>
        new(
            name,
            member,
            type,
            IsAsync: false,
            TakesCancellationToken: false,
            IsStatic: false,
            IsInherited: false,
            isMarked,
            database,
            declared ?? DeclaredOf(type),
            new LocationInfo(TestModels.SourceFile, new TextSpan(100 + name.Length, name.Length), default)
        );

    // A marked factory that returns a task of a connection, for the database when the test names one.
    private static ConnectionCandidate AsyncFactory(string name, string? database = null) =>
        Candidate(name, CandidateType.Factory, CandidateMember.Method, isMarked: true, database: database) with
        {
            IsAsync = true,
        };

    private static string DeclaredOf(CandidateType type) =>
        type switch
        {
            CandidateType.DataSource => "System.Data.Common.DbDataSource",
            CandidateType.Connection or CandidateType.Factory => "System.Data.Common.DbConnection",
            CandidateType.ConnectionString or CandidateType.Unsupported => "System.String",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}
