using System;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Snapshot;

namespace SqlSource.Tests.Generation.Repositories;

// Builds the candidates and shapes that the tests of ConnectionResolver resolve, each at a place of its own.
internal static class ResolverCandidates
{
    public const string FactoryFile = "/app/Data/IDatabaseFactory.cs";

    public const string GlobalFile = "/app/Data/Db.cs";

    // A shape of the repository's own candidates alone.
    public static RepositoryShape Shape(params ConnectionCandidate[] candidates) =>
        new("App.UserRepository", TestSidecars.Of(candidates), [], [], [], true, "IUserRepository", null);

    // A shape of candidates in each tier.
    public static RepositoryShape Tiers(
        ConnectionCandidate[] own,
        ConnectionCandidate[]? held = null,
        ConnectionCandidate[]? global = null
    ) =>
        Shape(own) with
        {
            HeldCandidates = TestSidecars.Of(held ?? []),
            GlobalCandidates = TestSidecars.Of(global ?? []),
        };

    // A candidate declared at a place of its own in the type's file, a field of a string unless the test says
    // otherwise.
    public static ConnectionCandidate Candidate(
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
    public static ConnectionCandidate AsyncFactory(string name, string? database = null) =>
        Candidate(name, CandidateType.Factory, CandidateMember.Method, isMarked: true, database: database) with
        {
            IsAsync = true,
        };

    // A marked instance member of an IDatabaseFactory, declared in the factory's file, that the repository reaches
    // through the holder: a primary-constructor parameter unless the test says otherwise, declared in the type's file.
    public static ConnectionCandidate Held(
        string holder,
        string name,
        CandidateType type = CandidateType.Factory,
        CandidateMember member = CandidateMember.Method,
        string? database = null,
        bool isAsync = false,
        CandidateMember holderMember = CandidateMember.Parameter,
        bool holderIsStatic = false,
        string? declared = null,
        bool holderCanBeNull = true
    ) =>
        Candidate(name, type, member, isMarked: true, database: database, declared: declared) with
        {
            IsAsync = isAsync,
            Location = new LocationInfo(FactoryFile, new TextSpan(40 + name.Length, name.Length), default),
            Tier = CandidateTier.Held,
            Holder = new CandidateHolder(
                holder,
                holderMember,
                holderIsStatic,
                "IDatabaseFactory",
                new LocationInfo(TestModels.SourceFile, new TextSpan(300 + holder.Length, holder.Length), default),
                holderCanBeNull
            ),
        };

    // A marked static member of the type, declared in its own file, a property unless the test says otherwise.
    public static ConnectionCandidate Global(
        string typeText,
        string name,
        CandidateType type,
        CandidateMember member = CandidateMember.Property,
        string? database = null,
        bool isAsync = false,
        string? declared = null
    ) =>
        Candidate(name, type, member, isMarked: true, database: database, declared: declared) with
        {
            IsAsync = isAsync,
            IsStatic = true,
            Location = new LocationInfo(GlobalFile, new TextSpan(60 + name.Length, name.Length), default),
            Tier = CandidateTier.Global,
            GlobalTypeText = typeText,
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
