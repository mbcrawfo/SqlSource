using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.Repositories;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

// The shape of a repository as the generator reads it from the compilation into its RepositoryShapes step: the name of
// its interface, whether it gets one and what takes that name, and which types it reads candidates of.
public sealed class RepositoryShapeReaderTests
{
    private const string Repository =
        "[SqlSourceGenerate]\npublic partial class Repository(NpgsqlDataSource dataSource);";

    [Fact]
    public void Read_InterfaceNameTakenByAClass_CarriesTheTakenBy()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                public class IUserRepository;
            }
            """
        );

        shape.InterfaceName.ShouldBe("IUserRepository");
        shape.InterfaceTakenBy.ShouldBe("the class App.IUserRepository");
    }

    // The user adds members to the generated interface by declaring another part of it.
    [Fact]
    public void Read_InterfaceNameIsAPartialInterface_IsNotTaken()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                public partial interface IUserRepository
                {
                    void Extra();
                }
            }
            """
        );

        shape.InterfaceTakenBy.ShouldBeNull();
    }

    [Fact]
    public void Read_InterfaceNameTakenByAReferencedType_CarriesTheTakenBy()
    {
        var library = RepositoryShapes.Library("namespace App { public interface IUserRepository; }");

        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);
            }
            """,
            references: [library]
        );

        shape.InterfaceTakenBy.ShouldBe("the interface App.IUserRepository of the assembly Library");
    }

    [Fact]
    public void Read_InterfaceNameTakenByAMemberOfTheContainingType_CarriesTheTakenBy()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public partial class Outer
                {
                    [SqlSourceGenerate]
                    public partial class Repository(NpgsqlDataSource dataSource);

                    public void IRepository() { }
                }
            }
            """
        );

        shape.InterfaceTakenBy.ShouldBe("the method App.Outer.IRepository()");
    }

    [Fact]
    public void Read_NoInterfaceSettings_IsOnAndNamedForTheType()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);
            }
            """
        );

        shape.TypeKey.ShouldBe("App.UserRepository");
        shape.InterfaceOn.ShouldBeTrue();
        shape.InterfaceName.ShouldBe("IUserRepository");
        shape.InterfaceTakenBy.ShouldBeNull();
    }

    [Fact]
    public void Read_InterfaceNameWritten_ReplacesTheDefault()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate(InterfaceName = "IUsers")]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                public class IUserRepository;
            }
            """
        );

        shape.InterfaceName.ShouldBe("IUsers");
        shape.InterfaceTakenBy.ShouldBeNull();
    }

    [Theory]
    [InlineData("", null, true)]
    [InlineData("", "false", false)]
    [InlineData("GenerateInterface = false", null, false)]
    [InlineData("GenerateInterface = true", "false", true)]
    public void Read_GenerateInterface_OverridesTheProjectSwitchWhenWritten(
        string argument,
        string? property,
        bool interfaceOn
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                [SqlSourceGenerate({{argument}})]
                public partial class UserRepository(NpgsqlDataSource dataSource);
            }
            """,
            new Dictionary<string, string?> { [RepositoryInterfaces.PropertyName] = property }
        );

        shape.InterfaceOn.ShouldBe(interfaceOn);
    }

    [Fact]
    public void Read_StaticType_HasNoCandidates()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public static partial class UserQueries
                {
                    private static readonly string ConnectionString = "";
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_NestedGenericType_IsFoundByItsMetadataName()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public partial class Outer
                {
                    [SqlSourceGenerate]
                    public partial class Repository<T>(NpgsqlDataSource dataSource);

                    public struct IRepository<T>;
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("dataSource");
        shape.InterfaceTakenBy.ShouldBe("the struct App.Outer.IRepository<T>");
    }

    // The type is found by its metadata name, which writes a keyword of its namespace or of a type around it as it is,
    // without the @ that C# needs.
    [Theory]
    [InlineData("App", Repository)]
    [InlineData("App.@event", Repository)]
    [InlineData("App", "public partial class @class\n{\n" + Repository + "\n}")]
    public void Read_TypeInAKeywordNamespace_HasItsCandidates(string ns, string declaration)
    {
        var shape = RepositoryShapes.Of("namespace " + ns + "\n{\n" + declaration + "\n}\n");

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("dataSource");
    }

    // A type of the interface's name is looked for by the same metadata name, beside the type in its namespace or in
    // the type it is nested in.
    [Theory]
    [InlineData("App", Repository + "\npublic class IRepository;", "the class App.IRepository")]
    [InlineData("App.@event", Repository + "\npublic class IRepository;", "the class App.@event.IRepository")]
    [InlineData(
        "App.@event",
        "public partial class @class\n{\n" + Repository + "\npublic class IRepository;\n}",
        "the class App.@event.@class.IRepository"
    )]
    public void Read_InterfaceNameTakenInAKeywordNamespace_CarriesTheTakenBy(
        string ns,
        string declarations,
        string takenBy
    )
    {
        var shape = RepositoryShapes.Of("namespace " + ns + "\n{\n" + declarations + "\n}\n");

        shape.InterfaceTakenBy.ShouldBe(takenBy);
    }
}
