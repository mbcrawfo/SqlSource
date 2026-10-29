// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// Generated code that gives no warning in a consumer's project, whatever the project turns on or names.
public sealed class GeneratedSourceWarningTests
{
    [Fact]
    public void Run_PublicTypeInAProjectThatDocumentsItsApi_GetsNoWarningForAGeneratedMember()
    {
        // A consumer that treats CS1591, a public member without documentation, as an error must not be broken by
        // generated code.  Here the consumer's own members are documented, so any CS1591 comes from the generator.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            /// <summary>Queries.</summary>
            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Sample;

            /// <summary>More queries.</summary>
            [SqlSourceGenerate]
            public partial struct Other;
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n-- summary: x < y & z\nSELECT 1 WHERE a < b;\n")
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldContain(GeneratorHarness.SampleFile);
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp7_3)]
    [InlineData(LanguageVersion.CSharp8)]
    [InlineData(LanguageVersion.CSharp9)]
    public void Run_ProjectOnAnOlderLanguageVersion_CompilesTheGeneratedAttribute(LanguageVersion languageVersion)
    {
        // The attribute goes into every project that references the package, including one that targets an older
        // framework and so defaults to an older language.  Such a project must get SQLSRC003 if it uses the
        // attribute, not a compiler error in a file it did not write.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            [],
            languageVersion: languageVersion
        );

        run.Sources.Keys.ShouldBe(["SqlSourceGenerateAttribute.g.cs"]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ProjectThatSeesTheInternalsOfAnotherWithTheGenerator_GetsNoWarningInGeneratedCode()
    {
        // Both assemblies hold the generated attribute and enums.  This one sees both copies, and the compiler warns
        // (CS0436) wherever one of their names is used: inside the generated file itself, unless that file says
        // otherwise.
        var other = GeneratorHarness.CreateReference(
            OutputKind.Sql,
            "Other",
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n"
                + "public class InOther { }\n"
        );

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample : InOther { }")],
            [],
            references: [other]
        );

        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Nested", "GetType")]
    [InlineData("Nested", "ToString")]
    [InlineData("Nested", "Equals")]
    [InlineData("Nested", "GetHashCode")]
    [InlineData("Nested", "MemberwiseClone")]
    [InlineData("Nested", "ReferenceEquals")]
    [InlineData("Direct", "GetType")]
    [InlineData("Direct", "ToString")]
    [InlineData("Direct", "Equals")]
    public void Run_QueryNamedLikeAMemberOfObject_GetsNoWarningInGeneratedCode(string location, string name)
    {
        // "GetType" is a likely name for a query on a table of types.  The constant hides the inherited method, which
        // is what was asked for, and the compiler's request to add "new" is one nobody can act on.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            $$"""
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(SqlLocation = SqlLocation.{{location}})]
            public partial class Sample;
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: " + name + "\nSELECT 1;\n")
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("public const string " + name + " = \"SELECT 1;\";");
    }

    [Fact]
    public void Run_MethodWithTheSignatureOfAnInheritedVirtualMethod_GetsNoWarningInGeneratedCode()
    {
        // A method that has the name and the parameters of a virtual method of a base class hides it, and the compiler
        // asks for "override" or "new" (CS0114).  Nobody can add either to generated code.  A constant never gets this
        // warning; a method can.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            public class Base
            {
                public virtual string ListFrom(string table) => table;
            }

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Sample : Base;
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: ListFrom\nSELECT * FROM {{table}};\n")
        );

        run.ShouldBeClean();
    }

    [Fact]
    public void Run_NestedLocationAndAnInheritedMemberNamedSql_GetsNoWarningInGeneratedCode()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            public class Base
            {
                protected static int Sql => 1;
            }

            [SqlSourceGenerate]
            public partial class Sample : Base
            {
                public static string Value => Sql.GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
    }
}
