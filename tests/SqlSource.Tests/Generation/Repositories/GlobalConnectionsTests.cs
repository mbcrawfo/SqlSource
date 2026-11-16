using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Repositories;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

// The marked static members of the project that a repository can reach, its global candidates, as the generator reads
// them from the compilation into its RepositoryShapes step.
public sealed class GlobalConnectionsTests
{
    [Fact]
    public void Global_StaticPropertyInStaticClass_IsACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    public static NpgsqlDataSource Main { get; set; } = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        var candidate = shape.GlobalCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Main");
        candidate.Member.ShouldBe(CandidateMember.Property);
        candidate.Type.ShouldBe(CandidateType.DataSource);
        candidate.IsStatic.ShouldBeTrue();
        candidate.IsMarked.ShouldBeTrue();
        candidate.Tier.ShouldBe(CandidateTier.Global);
        candidate.GlobalTypeText.ShouldBe("global::App.Db");
        candidate.DisplayName.ShouldBe("Db.Main");
        candidate.Holder.ShouldBeNull();
        shape.Candidates.ShouldBeEmpty();
        shape.HeldCandidates.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("public static string Main = \"\";", "Field", "ConnectionString", false)]
    [InlineData("public const string Main = \"\";", "Field", "ConnectionString", false)]
    [InlineData("public static DbConnection Main { get; } = null!;", "Property", "Connection", false)]
    [InlineData("public static Task<DbConnection> Main() => null!;", "Method", "Factory", true)]
    public void Global_EachKind_IsRead(string member, string kind, string type, bool isAsync)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public class Db
                {
                    [SqlSourceConnection]
                    {{member}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        var candidate = shape.GlobalCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Main");
        candidate.Member.ShouldBe(Enum.Parse<CandidateMember>(kind));
        candidate.Type.ShouldBe(Enum.Parse<CandidateType>(type));
        candidate.IsAsync.ShouldBe(isAsync);
        candidate.Tier.ShouldBe(CandidateTier.Global);
        candidate.GlobalTypeText.ShouldBe("global::App.Db");
    }

    // Tier 1 reads a repository's own static members, so none of them is a global, of this repository or another.
    [Fact]
    public void Global_StaticMemberOfARepository_IsNotGlobal()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    public static NpgsqlDataSource Main { get; set; } = null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Main");
        candidate.Tier.ShouldBe(CandidateTier.Own);
        shape.GlobalCandidates.ShouldBeEmpty();
    }

    // The mark on one partial declaration and the attribute on another are still one repository's member.
    [Fact]
    public void Global_MarkOnAnotherPartialDeclarationOfTheRepository_IsTheRepositorysOwn()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository;

                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    public static NpgsqlDataSource Main { get; set; } = null!;
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("Main");
        shape.GlobalCandidates.ShouldBeEmpty();
    }

    // A static class nested in a repository is a type of its own, whose marked statics are globals.
    [Fact]
    public void Global_StaticClassNestedInARepository_IsAGlobal()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    public static class Db
                    {
                        [SqlSourceConnection]
                        public static string Main = "";
                    }
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
        var candidate = shape.GlobalCandidates.ShouldHaveSingleItem();
        candidate.GlobalTypeText.ShouldBe("global::App.UserRepository.Db");
        candidate.DisplayName.ShouldBe("Db.Main");
    }

    [Fact]
    public void Global_Unreachable_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    private static string Main = "";

                    public static string Use() => Main;
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        shape.GlobalCandidates.ShouldBeEmpty();
    }

    // A property whose getter the repository cannot call is unreachable as a whole.
    [Fact]
    public void Global_PrivateGetter_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    public static string Main { private get; set; } = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        shape.GlobalCandidates.ShouldBeEmpty();
    }

    // A file-local type is reachable from its own file alone, and the generated code is in another, so its global is
    // broken, which its own stage reports, and never a candidate.
    [Fact]
    public void Global_InFileLocalType_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                file static class Db
                {
                    [SqlSourceConnection]
                    public static string Main = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        shape.GlobalCandidates.ShouldBeEmpty();
    }

    [Fact]
    public void Global_InReferencedAssembly_IsNotACandidate()
    {
        var library = RepositoryShapes.Library(
            """
            namespace Library
            {
                public static class Db
                {
                    [SqlSource.SqlSourceConnection]
                    public static string Main = "";
                }
            }
            """
        );

        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    private static string Used => Library.Db.Main;
                }
            }
            """,
            references: [library]
        );

        shape.GlobalCandidates.ShouldBeEmpty();
    }

    [Fact]
    public void Global_NestedType_IsReachedThroughItsFullName()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class Outer
                {
                    public static class Db
                    {
                        [SqlSourceConnection]
                        public static string ConnectionString = "";
                    }
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        var candidate = shape.GlobalCandidates.ShouldHaveSingleItem();
        candidate.GlobalTypeText.ShouldBe("global::App.Outer.Db");
        candidate.DisplayName.ShouldBe("Db.ConnectionString");
    }

    [Fact]
    public void Global_StaticClassNestedInANonStaticClass_IsReachedThroughItsFullName()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class Outer
                {
                    public static class Db
                    {
                        [SqlSourceConnection]
                        public static string Main = "";
                    }
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        shape.GlobalCandidates.ShouldHaveSingleItem().GlobalTypeText.ShouldBe("global::App.Outer.Db");
    }

    // A type named as a keyword keeps its @, so that the generated code compiles.
    [Fact]
    public void Global_TypeNamedAsAKeyword_KeepsItsEscape()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class @class
                {
                    [SqlSourceConnection]
                    public static string Main = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        var candidate = shape.GlobalCandidates.ShouldHaveSingleItem();
        candidate.GlobalTypeText.ShouldBe("global::App.@class");
        candidate.DisplayName.ShouldBe("class.Main");
    }

    // A broken global, whatever makes it so, is reported once by its own stage and is never a candidate.
    [Theory]
    [InlineData("public static class Db<T> { [SqlSourceConnection] public static string Main = \"\"; }")]
    [InlineData(
        "public class Outer<T> { public static class Db { [SqlSourceConnection] public static string Main = \"\"; } }"
    )]
    [InlineData("public static class Db { [SqlSourceConnection] public static int Main = 0; }")]
    [InlineData("public static class Db { [SqlSourceConnection] public static string Main(int id) => \"\"; }")]
    [InlineData("public interface IDb { [SqlSourceConnection] static abstract string Main { get; } }")]
    public void Global_Broken_IsNotACandidate(string declaration)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                {{declaration}}

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        shape.GlobalCandidates.ShouldBeEmpty();
    }

    // The marks are read in the order of their declarations' ids, the same for every order of the files.
    [Fact]
    public void Global_SeveralMarks_AreInTheOrderOfTheirIds()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class Reports
                {
                    [SqlSourceConnection(Database = "reports")]
                    public static string Main = "";
                }

                public static class Db
                {
                    [SqlSourceConnection]
                    public static string Main = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository;
            }
            """
        );

        shape.GlobalCandidates.Select(candidate => candidate.DisplayName).ShouldBe(["Db.Main", "Reports.Main"]);
        shape.GlobalCandidates[1].Database.ShouldBe("reports");
    }
}
