using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The query of Queries/Locations.sql and its copies run through the method of each location their markers give, and
// its copy ListAccountNamesWithSuffix through the method that async-method-suffix names, on the seed of "app", by the
// one claim of LocationQueries.cs.  Each test reads where the method is and how it can be called, and the rows.
[Collection(UsesSqlServer.Name)]
public sealed class MethodLocationTests(SqlServerDatabases sqlServer)
{
    private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly ListAccountNamesDto[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    [Fact]
    public async Task ListAccountNames_ExtensionClass_IsAnExtensionOfTheConnection()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountNames(cancellationToken: TestContext.Current.CancellationToken);

        accounts.ShouldBe(Accounts);
        var method = typeof(LocationQueriesExtensions).GetMethod(
            nameof(LocationQueriesExtensions.ListAccountNames),
            Statics
        )!;
        method.IsDefined(typeof(ExtensionAttribute)).ShouldBeTrue();
        typeof(LocationQueries).GetMethod(nameof(LocationQueriesExtensions.ListAccountNames), Statics).ShouldBeNull();
    }

    [Fact]
    public async Task ListAccountNamesPublic_Public_IsAPublicMethodOfTheType()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await LocationQueries.ListAccountNamesPublic(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(LocationQueries)
            .GetMethod(nameof(LocationQueries.ListAccountNamesPublic), Statics)!
            .IsPublic.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAccountNamesInternal_Internal_IsAnInternalMethodOfTheType()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await LocationQueries.ListAccountNamesInternal(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(LocationQueries)
            .GetMethod(nameof(LocationQueries.ListAccountNamesInternal), Statics)!
            .IsAssembly.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAccountNamesPrivate_Private_IsAPrivateMethodThatTheTypeCalls()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await LocationQueries.ListThroughWrapper(connection, TestContext.Current.CancellationToken);

        accounts.ShouldBe(Accounts);
        typeof(LocationQueries).GetMethod("ListAccountNamesPrivate", Statics)!.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAccountNamesNested_NestedClass_IsAMethodOfTheNestedClass()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await LocationQueries.Sql.ListAccountNamesNested(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(LocationQueries.Sql).IsNestedAssembly.ShouldBeTrue();
        typeof(LocationQueries.Sql)
            .GetMethod(nameof(LocationQueries.Sql.ListAccountNamesNested), Statics)!
            .IsPublic.ShouldBeTrue();
        typeof(LocationQueries).GetMethod(nameof(LocationQueries.Sql.ListAccountNamesNested), Statics).ShouldBeNull();
    }

    [Fact]
    public async Task ListAccountNamesWithSuffixAsync_AsyncMethodSuffixByTheQuerysMarker_EndsTheNameInAsync()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountNamesWithSuffixAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(LocationQueriesExtensions).GetMethod("ListAccountNamesWithSuffix", Statics).ShouldBeNull();
    }
}
