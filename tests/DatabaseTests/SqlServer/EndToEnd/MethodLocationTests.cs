using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The query of Queries/Locations.sql run through the method of each MethodLocation, and through the method that
// async-method-suffix names, on the seed of "app", by the classes of LocationQueries.cs.  Each test reads where the
// method is and how it can be called, and the rows.
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
        var method = typeof(LocationQueriesExtensions).GetMethod(nameof(LocationQueriesExtensions.ListAccountNames))!;
        method.IsDefined(typeof(ExtensionAttribute)).ShouldBeTrue();
        typeof(LocationQueries).GetMethod(nameof(LocationQueriesExtensions.ListAccountNames), Statics).ShouldBeNull();
    }

    [Fact]
    public async Task ListAccountNames_Public_IsAPublicMethodOfTheType()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await PublicLocationQueries.ListAccountNames(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(PublicLocationQueries)
            .GetMethod(nameof(PublicLocationQueries.ListAccountNames), Statics)!
            .IsPublic.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAccountNames_Internal_IsAnInternalMethodOfTheType()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await InternalLocationQueries.ListAccountNames(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(InternalLocationQueries)
            .GetMethod(nameof(InternalLocationQueries.ListAccountNames), Statics)!
            .IsAssembly.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAccountNames_Private_IsAPrivateMethodThatTheTypeCalls()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await PrivateLocationQueries.ListThroughWrapper(
            connection,
            TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(PrivateLocationQueries).GetMethod("ListAccountNames", Statics)!.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAccountNamesAsync_AsyncMethodSuffix_EndsTheNameInAsync()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await SuffixLocationQueries.ListAccountNamesAsync(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
        typeof(SuffixLocationQueries).GetMethod("ListAccountNames", Statics).ShouldBeNull();
    }
}
