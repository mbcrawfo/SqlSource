// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

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
    public void Run_ProjectOnAnOlderLanguageVersion_CompilesTheAttributeAndTheExceptions(
        LanguageVersion languageVersion
    )
    {
        // The attribute and the exceptions go into every project that references the package, including one that
        // targets an older framework and so defaults to an older language.  Such a project must get SQLSRC003 if it
        // uses the attribute, not a compiler error in a file it did not write.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            [],
            languageVersion: languageVersion
        );

        run.Sources.Keys.ShouldBe(
            [GeneratedFile.EmbeddedAttributeName, "SqlSourceGenerateAttribute.g.cs", "SqlSource-Exceptions.g.cs"],
            ignoreOrder: true
        );
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
    }

    // On an image, as a build references a project, and on a compilation, as an IDE does.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ProjectThatSeesTheInternalsOfAnotherWithTheGenerator_GetsNoWarningInGeneratedCode(
        bool compilationReference
    )
    {
        // Both assemblies hold the generated attribute and enums, each marked embedded, so this one does not see the
        // other's copies, and the compiler has no conflict (CS0436) to warn about inside the generated file.
        var other = GeneratorHarness
            .CreateProject(
                OutputKind.Sql,
                "Other",
                "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n"
                    + "public class InOther { }\n",
                compilationReference: compilationReference
            )
            .Reference;

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
