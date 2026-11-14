# Test code that uses a repository (mock or fake)

At the end of this page, a unit test checks code that uses your repository, with a fake or a mock of its interface and no database.

<a id="interface"></a>

## Take the interface

The build generates an interface for the repository, `IUserRepository`, with its public methods.  Write the code that uses the repository against the interface:

```csharp
public sealed class UserGreeter(IUserRepository users)
{
    public async Task<string> GreetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetUser(new GetUserParams(userId), cancellationToken).ConfigureAwait(false);
        return GreetingOf(user);
    }

    private static string GreetingOf(GetUserDto? user) => user is null ? "Hello, stranger." : $"Hello, {user.Name}.";
}
```

Your application registers `UserRepository` as `IUserRepository`, as [Call your queries](call-queries.md#register) shows.  A test gives `UserGreeter` something else.

<a id="fake"></a>

## Write a fake

In your test project, which references the project of the repository, write a class that implements `IUserRepository`:

```csharp
public sealed class FakeUserRepository : IUserRepository
{
    public Dictionary<int, GetUserDto> Users { get; } = [];

    public Task<GetUserDto?> GetUser(GetUserParams parameters, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.GetValueOrDefault(parameters.UserId));

    public Task<ListUserNamesDto[]> ListUserNames(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Select(user => new ListUserNamesDto(user.Id, user.Name)).ToArray());

    public Task<int> RenameUser(RenameUserParams parameters, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
```

The fake needs a method for each query of `UserRepository.sql`.  A method that your tests do not call can throw `NotImplementedException`.  Then test with it:

```csharp
[Fact]
public async Task Greet_KnownUser_SaysTheirName()
{
    var users = new FakeUserRepository();
    users.Users[1] = new GetUserDto(1, "Ada", "ada@example.com");

    var greeting = await new UserGreeter(users).GreetAsync(1);

    Assert.Equal("Hello, Ada.", greeting);
}
```

Run `dotnet test`.  The test passes, and it needs no database.

<a id="mock"></a>

## Use a mocking library

A mocking library makes the fake for you.  This test uses NSubstitute.  Any mocking library works the same way, since `IUserRepository` is a plain interface:

```csharp
[Fact]
public async Task Greet_KnownUser_SaysTheirName_WithAMock()
{
    var users = Substitute.For<IUserRepository>();
    users.GetUser(new GetUserParams(1), Arg.Any<CancellationToken>())
        .Returns(new GetUserDto(1, "Ada", "ada@example.com"));

    Assert.Equal("Hello, Ada.", await new UserGreeter(users).GreetAsync(1));
}
```

`new GetUserParams(1)` matches the call, since a record is equal to another record with the same values.

<a id="your-own-member"></a>

## Add a member of your own

The interface is `partial`.  To add a method of your own to it, declare the method in a `partial interface` of the same name, and write it in the class:

```csharp
public partial interface IUserRepository
{
    Task<int> CountUsers(CancellationToken cancellationToken = default);
}

public sealed partial class UserRepository
{
    public async Task<int> CountUsers(CancellationToken cancellationToken = default) =>
        (await ListUserNames(cancellationToken)).Length;
}
```

Your fake then needs `CountUsers` too.

## If it does not work

- The test project cannot see `IUserRepository`: the interface is `public` only when `UserRepository` is.  Make the class `public`, or see [Use SqlSource in several projects](../guides/several-projects.md#test-project-sees-internals).
- The fake does not compile after you add a query: add a method for the new query to the fake.

## What you have now

Code that takes `IUserRepository`, and a test of it that uses a fake or a mock, with no database.

Rules: [the interface](../reference/repositories.md#repository-interface), and how to rename or turn off the interface in [repository interfaces](../reference/repositories.md#repository-interfaces).

Next: [Keep your queries up to date](keep-up-to-date.md)
