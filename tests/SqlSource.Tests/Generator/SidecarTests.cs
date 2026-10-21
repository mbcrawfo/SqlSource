// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The checks of each claimed file's sidecar against what the types that claim the file need: each diagnostic, where
// it is, and which entries the checks leave usable, read from what the step of the checks gave.
public sealed class SidecarTests
{
    private const string SidecarPath = GeneratorHarness.UsersPath + ".json";

    // The name of GetUser in Users.
    private const string GetUserName = $"{GeneratorHarness.UsersPath}(1,10)-(1,17)";

    // The start of the sidecar of Users.
    private const string SidecarStart = $"{GeneratorHarness.UsersPath}.json(1,1)-(1,1)";

    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate]
        public partial class Sample;
        """;

    private static readonly SqlFile Users = new(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n");

    [Fact]
    public void Run_MalformedSidecar_IsSqlsrc237AtTheError()
    {
        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Raw(
                Users, /*lang=json,strict*/
                "{\n  \"formatVersion\": \"one\"\n}\n"
            )
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC237 {GeneratorHarness.UsersPath}.json(2,20)-(2,25): The sidecar cannot be read, because "
                + "'formatVersion' has a value of the wrong type.  Run 'sqlsource describe' to write it again.",
        ]);
        run.SeverityOf("SQLSRC237").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_SidecarOfANewerFormat_IsSqlsrc238AtTheStart()
    {
        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Of(Users, SidecarFormat.Version + 1, PackageVersion.Prefix, CurrentEntry(Users, "GetUser"))
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC238 {SidecarStart}: The sidecar has format 2, and this version of SqlSource reads format 1.  "
                + "Update the SqlSource package.",
        ]);
        run.SeverityOf("SQLSRC238").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_SidecarOfAnOlderFormat_IsSqlsrc239AtTheStart()
    {
        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Of(Users, SidecarFormat.Version - 1, PackageVersion.Prefix, CurrentEntry(Users, "GetUser"))
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC239 {SidecarStart}: The sidecar has format 0, and this version of SqlSource reads format 1.  Run "
                + "'sqlsource describe' to write it again.",
        ]);
        run.SeverityOf("SQLSRC239").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_SidecarOfAnotherToolVersion_IsAWarningAndTheEntryIsUsed()
    {
        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Of(Users, SidecarFormat.Version, "99.0.0-dev", CurrentEntry(Users, "GetUser"))
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC240 {SidecarStart}: The sidecar was written by sqlsource 99.0.0-dev, and this project uses "
                + $"SqlSource {PackageVersion.Prefix}.  Bring the tool and the package to one version, and run "
                + "'sqlsource describe'.",
        ]);
        run.SeverityOf("SQLSRC240").ShouldBe(DiagnosticSeverity.Warning);
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    [Fact]
    public void Run_QueryWithoutAnEntry_IsSqlsrc241AtTheQuery()
    {
        const string Expected =
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
            + "'models' needs one.  Run 'sqlsource describe'.";

        var withoutSidecar = Run(OutputKind.Models, Users);
        var withoutEntry = Run(OutputKind.Models, Users, Sidecars.Of(Users));

        withoutSidecar.Diagnostics.ShouldBe([Expected]);
        withoutSidecar.SeverityOf("SQLSRC241").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(withoutSidecar).ShouldBeEmpty();
        withoutEntry.Diagnostics.ShouldBe([Expected]);
        UsableEntries(withoutEntry).ShouldBeEmpty();
    }

    // An engine nobody knows is not a crash, and is never usable.
    [Theory]
    [InlineData("mssql")]
    [InlineData("oracle")]
    public void Run_EntryOfAnotherEngine_IsSqlsrc242(string engine)
    {
        var entry = Sidecars.Entry("GetUser", Sidecars.HashOf(Users, "GetUser", SqlDialect.PostgreSql), engine);

        var run = Run(OutputKind.Models, Users, Sidecars.Of(Users, entry));

        run.Diagnostics.ShouldBe([
            $"SQLSRC242 {GetUserName}: The entry of the query 'GetUser' was described for '{engine}', and the dialect "
                + "of this file is 'postgres'.  Run 'sqlsource describe'.",
        ]);
        run.SeverityOf("SQLSRC242").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBeEmpty();
    }

    // The file is checked as a postgres file is: an entry that is current is usable, a stale one is SQLSRC244, and a
    // query without one is SQLSRC241.
    [Fact]
    public void Run_MssqlFile_IsCheckedAgainstItsSidecar()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1;\n\n-- name: CountUsers\nSELECT 2;\n\n-- name: ListUsers\nSELECT 3;\n"
        );
        var current = Sidecars.Entry("GetUser", Sidecars.HashOf(users, "GetUser", SqlDialect.SqlServer), "mssql");
        var stale = Sidecars.Entry("CountUsers", "stale", "mssql");

        var run = Run(OutputKind.Models, [users, Sidecars.Of(users, current, stale)], "mssql");

        run.Diagnostics.ShouldBe([
            $"SQLSRC244 {GeneratorHarness.UsersPath}(4,10)-(4,20): The query 'CountUsers' has changed since its entry "
                + "was written.  Run 'sqlsource describe'.",
            $"SQLSRC241 {GeneratorHarness.UsersPath}(7,10)-(7,19): The query 'ListUsers' has no entry in the sidecar "
                + "of its file, and its output 'models' needs one.  Run 'sqlsource describe'.",
        ]);
        run.SeverityOf("SQLSRC244").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    [Fact]
    public void Run_MssqlFileWithoutASidecar_IsSqlsrc241AtEachQuery()
    {
        var run = Run(OutputKind.Models, [Users], "mssql");

        run.Diagnostics.ShouldBe([
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                + "'models' needs one.  Run 'sqlsource describe'.",
        ]);
    }

    // An entry of postgres is not one of mssql, and the sidecar's own errors and its orphans are still found.
    [Fact]
    public void Run_MssqlFileWithASidecar_IsCheckedForTheEngineAndForTheSidecarsOwnErrors()
    {
        var current = Sidecars.Entry("GetUser", Sidecars.HashOf(Users, "GetUser", SqlDialect.SqlServer), "mssql");
        var ofPostgres = Sidecars.Entry("GetUser", Sidecars.HashOf(Users, "GetUser", SqlDialect.PostgreSql));

        var withOrphan = Run(
            OutputKind.Models,
            [Users, Sidecars.Of(Users, current, Sidecars.Entry("Gone", "h", "mssql"))],
            "mssql"
        );
        var malformed = Run(OutputKind.Models, [Users, Sidecars.Raw(Users, "")], "mssql");
        var wrongEngine = Run(OutputKind.Models, [Users, Sidecars.Of(Users, ofPostgres)], "mssql");

        withOrphan.Diagnostics.ShouldBe([
            $"SQLSRC245 {GeneratorHarness.UsersPath}.json(15,5)-(15,11): The entry 'Gone' names no query of the file "
                + "that needs one.  Run 'sqlsource describe', which removes it.",
        ]);
        UsableEntries(withOrphan).ShouldBe(["GetUser"]);
        malformed.Diagnostics.ShouldBe([
            $"SQLSRC237 {SidecarStart}: The sidecar cannot be read, because it is not one JSON object.  Run "
                + "'sqlsource describe' to write it again.",
        ]);
        UsableEntries(malformed).ShouldBeEmpty();
        wrongEngine.Diagnostics.ShouldBe([
            $"SQLSRC242 {GetUserName}: The entry of the query 'GetUser' was described for 'postgres', and the "
                + "dialect of this file is 'mssql'.  Run 'sqlsource describe'.",
        ]);
        UsableEntries(wrongEngine).ShouldBeEmpty();
    }

    // SQLSRC243 is kept for an engine whose describer lands before its map, and this release has none.
    [Fact]
    public void Run_EachDescribableDialect_IsNeverSqlsrc243()
    {
        foreach (var dialect in SqlDescribableDialects.All)
        {
            var run = Run(OutputKind.Models, [Users], SqlDialectName.Canonical(dialect));

            run.Diagnostics.ShouldNotBeEmpty();
            run.Diagnostics.ShouldNotContain(diagnostic =>
                diagnostic.StartsWith("SQLSRC243 ", StringComparison.Ordinal)
            );
        }
    }

    // At the first query that needs an entry, as the tool reports it: one setting mends the file, so its queries get
    // no error of their own, and its constants are generated as before.  The entries of those queries are not used,
    // and are no orphans.
    [Fact]
    public void Run_AnsiFileAtModels_IsOneSqlsrc209AtItsFirstQueryThatNeedsAnEntry()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- output: sql\nSELECT 1;\n\n-- name: CountUsers\nSELECT 2;\n\n"
                + "-- name: ListUsers\nSELECT 3;\n"
        );
        const string NotDescribable =
            $"SQLSRC209 {GeneratorHarness.UsersPath}(5,10)-(5,20): The output 'models' needs each query described by "
            + "its database, and the dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or "
            + "'mssql', or the output to 'sql'.";

        var run = Run(OutputKind.Models, [users], "ansi");
        var withEntries = Run(
            OutputKind.Models,
            [users, Sidecars.Of(users, CurrentEntry(users, "CountUsers"), CurrentEntry(users, "ListUsers"))],
            "ansi"
        );

        run.Diagnostics.ShouldBe([NotDescribable]);
        run.SeverityOf("SQLSRC209").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBeEmpty();
        withEntries.Diagnostics.ShouldBe([NotDescribable]);
        UsableEntries(withEntries).ShouldBeEmpty();
        var generated = run.Sources["App.Sample.g.cs"];
        generated.ShouldContain("GetUser =");
        generated.ShouldContain("CountUsers =");
        generated.ShouldContain("ListUsers =");
    }

    // A query whose token has no default cannot be described, so it is not checked against the sidecar, and its
    // entry is neither used nor an orphan.  A token with a default, even an empty one, is no error.
    [Fact]
    public void Run_TokenWithoutADefault_IsSqlsrc210AtTheQueryAndItIsNotChecked()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1 {{where}} {{order}} {{tail:}};\n"
        );
        var withDefaults = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1 {{where:WHERE true}};\n"
        );
        const string NoDefault =
            $"SQLSRC210 {GetUserName}: The token '{{0}}' has no default, which a query whose output is 'models' "
            + "needs.  Give it a default, or set the output to 'sql'.";

        var withoutSidecar = Run(OutputKind.Models, users);
        var withStaleEntry = Run(OutputKind.Models, users, Sidecars.Of(users, Sidecars.Entry("GetUser", "stale")));
        var defaulted = Run(OutputKind.Models, withDefaults);
        var ansi = Run(OutputKind.Models, [users], "ansi");

        string[] noDefaults =
        [
            NoDefault.Replace("{0}", "where", StringComparison.Ordinal),
            NoDefault.Replace("{0}", "order", StringComparison.Ordinal),
        ];
        withoutSidecar.Diagnostics.ShouldBe(noDefaults);
        withoutSidecar.SeverityOf("SQLSRC210").ShouldBe(DiagnosticSeverity.Error);
        withStaleEntry.Diagnostics.ShouldBe(noDefaults);
        UsableEntries(withStaleEntry).ShouldBeEmpty();
        defaulted.Diagnostics.ShouldBe([
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                + "'models' needs one.  Run 'sqlsource describe'.",
        ]);
        ansi.Diagnostics.ShouldBe([
            $"SQLSRC209 {GetUserName}: The output 'models' needs each query described by its database, and the "
                + "dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or 'mssql', or the output to "
                + "'sql'.",
            .. noDefaults,
        ]);
        ansi.Sources["App.Sample.g.cs"].ShouldContain("GetUser");
    }

    // The tool reports a token without a default whatever the file's sidecar and dialect, and so does the build:
    // beside an error of the sidecar itself, and under mssql, whose file is checked as a postgres file is.
    [Fact]
    public void Run_TokenWithoutADefault_IsSqlsrc210WhateverTheSidecarAndTheDialect()
    {
        var users = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1 {{where}};\n");
        const string NoDefault =
            $"SQLSRC210 {GetUserName}: The token 'where' has no default, which a query whose output is 'models' "
            + "needs.  Give it a default, or set the output to 'sql'.";

        var malformed = Run(OutputKind.Models, users, Sidecars.Raw(users, ""));
        var mssql = Run(OutputKind.Models, [users], "mssql");

        malformed.Diagnostics.ShouldBe([
            NoDefault,
            $"SQLSRC237 {SidecarStart}: The sidecar cannot be read, because it is not one JSON object.  Run "
                + "'sqlsource describe' to write it again.",
        ]);
        UsableEntries(malformed).ShouldBeEmpty();
        mssql.Diagnostics.ShouldBe([NoDefault]);
        UsableEntries(mssql).ShouldBeEmpty();
    }

    [Fact]
    public void Run_StaleEntry_IsSqlsrc244()
    {
        var run = Run(OutputKind.Models, Users, Sidecars.Of(Users, Sidecars.Entry("GetUser", "stale")));

        run.Diagnostics.ShouldBe([
            $"SQLSRC244 {GetUserName}: The query 'GetUser' has changed since its entry was written.  Run 'sqlsource "
                + "describe'.",
        ]);
        run.SeverityOf("SQLSRC244").ShouldBe(DiagnosticSeverity.Error);
        UsableEntries(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_OrphanEntry_IsAWarningAtItsKey()
    {
        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Of(Users, CurrentEntry(Users, "GetUser"), Sidecars.Entry("Gone", "h"))
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC245 {GeneratorHarness.UsersPath}.json(15,5)-(15,11): The entry 'Gone' names no query of the file "
                + "that needs one.  Run 'sqlsource describe', which removes it.",
        ]);
        run.SeverityOf("SQLSRC245").ShouldBe(DiagnosticSeverity.Warning);
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    // A file with parse errors has no queries, so every entry would be an orphan on each keystroke that leaves a
    // typo.  Only the checks of the sidecar itself run.
    [Fact]
    public void Run_FileWithParseErrors_GetsNoOrphanAndNoQueryError()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1;\n\n-- name: GetUser\nSELECT 2;\n"
        );
        const string DuplicateName =
            $"SQLSRC104 {GeneratorHarness.UsersPath}(4,10)-(4,17): The query name 'GetUser' is used more than once in "
            + "this file";

        var withEntry = Run(OutputKind.Models, users, Sidecars.Of(users, Sidecars.Entry("GetUser", "h")));
        var malformed = Run(OutputKind.Models, users, Sidecars.Raw(users, ""));

        withEntry.Diagnostics.ShouldBe([DuplicateName]);
        UsableEntries(withEntry).ShouldBeEmpty();
        malformed.Diagnostics.ShouldBe(
            [
                DuplicateName,
                $"SQLSRC237 {SidecarStart}: The sidecar cannot be read, because it is not one JSON object.  Run "
                    + "'sqlsource describe' to write it again.",
            ],
            ignoreOrder: true
        );
    }

    [Fact]
    public void Run_SidecarOfAFileNoTypeClaims_IsNotRead()
    {
        var source = Source.Replace(
            "[SqlSourceGenerate]",
            "[SqlSourceGenerate(Path = \"Users.sql\")]",
            StringComparison.Ordinal
        );
        var orders = new SqlFile("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [Users, Sidecars.Current(Users, SqlDialect.PostgreSql, "GetUser"), orders, Sidecars.Raw(orders, "{")],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs.Of<SidecarFile>(run, TrackingNames.SidecarFile).Select(file => file.Path).ShouldBe([SidecarPath]);
    }

    [Fact]
    public void Run_SidecarWithoutItsSqlFile_IsIgnored()
    {
        var gone = new SqlFile("/app/Repo/Gone.sql", null);

        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Current(Users, SqlDialect.PostgreSql, "GetUser"),
            Sidecars.Raw(gone, "{")
        );

        run.Diagnostics.ShouldBeEmpty();
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    [Fact]
    public void Run_FileAtSqlWithoutASidecar_GetsNothing()
    {
        var run = Run(OutputKind.Sql, Users);

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs
            .Of<SidecarCheckResult>(run, TrackingNames.SidecarCheck)
            .ShouldAllBe(check => check.Diagnostics.Count == 0);
        run.Sources.Keys.ShouldContain("App.Sample.g.cs");
    }

    // Every sidecar of a claimed file is read, whatever its queries need.
    [Fact]
    public void Run_MalformedSidecarOfAFileAtSql_IsSqlsrc237()
    {
        var run = Run(
            OutputKind.Sql,
            Users,
            Sidecars.Raw(
                Users, /*lang=json,strict*/
                "{\n  \"formatVersion\": \"one\"\n}\n"
            )
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC237 {GeneratorHarness.UsersPath}.json(2,20)-(2,25): The sidecar cannot be read, because "
                + "'formatVersion' has a value of the wrong type.  Run 'sqlsource describe' to write it again.",
        ]);
    }

    [Fact]
    public void Run_EmptySidecar_IsSqlsrc237AtTheStart()
    {
        var run = Run(OutputKind.Models, Users, Sidecars.Raw(Users, ""));

        run.Diagnostics.ShouldBe([
            $"SQLSRC237 {SidecarStart}: The sidecar cannot be read, because it is not one JSON object.  Run "
                + "'sqlsource describe' to write it again.",
        ]);
        UsableEntries(run).ShouldBeEmpty();
    }

    // Names are compared ordinally, as C# compares the members they become.
    [Fact]
    public void Run_EntryNamedByAnotherCase_IsMissingAndAnOrphan()
    {
        var entry = Sidecars.Entry("getuser", Sidecars.HashOf(Users, "GetUser", SqlDialect.PostgreSql));

        var run = Run(OutputKind.Models, Users, Sidecars.Of(Users, entry));

        run.Diagnostics.ShouldBe([
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                + "'models' needs one.  Run 'sqlsource describe'.",
            $"SQLSRC245 {GeneratorHarness.UsersPath}.json(7,5)-(7,14): The entry 'getuser' names no query of the file "
                + "that needs one.  Run 'sqlsource describe', which removes it.",
        ]);
        UsableEntries(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_QueryAtModelsAndQueryAtSql_OnlyTheFirstNeedsAnEntry()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1;\n\n-- name: CountUsers\n-- output: sql\nSELECT 2;\n"
        );

        var withoutSidecar = Run(OutputKind.Models, users);
        var run = Run(
            OutputKind.Models,
            users,
            Sidecars.Of(users, CurrentEntry(users, "GetUser"), CurrentEntry(users, "CountUsers"))
        );

        withoutSidecar.Diagnostics.ShouldBe([
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                + "'models' needs one.  Run 'sqlsource describe'.",
        ]);
        run.Diagnostics.ShouldBe([
            $"SQLSRC245 {GeneratorHarness.UsersPath}.json(15,5)-(15,17): The entry 'CountUsers' names no query of the "
                + "file that needs one.  Run 'sqlsource describe', which removes it.",
        ]);
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    // The first type needs nothing, and the second every query: the file needs what the second needs, and is checked
    // once.
    [Fact]
    public void Run_TwoTypesClaimingOneFile_ReportOnceAndNeedTheUnion()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1;\n\n-- name: CountUsers\nSELECT 2;\n"
        );

        var run = RunTwoTypes(
            "Output = GeneratorOutput.Sql",
            "Output = GeneratorOutput.Models",
            users,
            Sidecars.Of(users, CurrentEntry(users, "CountUsers"))
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                + "'models' needs one.  Run 'sqlsource describe'.",
        ]);
        UsableEntries(run).ShouldBe(["CountUsers"]);
        run.Sources.Keys.ShouldContain("App.First.g.cs");
        run.Sources.Keys.ShouldContain("App.Second.g.cs");
    }

    // Both types need the entry: one error, which names the greater of the two outputs.
    [Fact]
    public void Run_TwoTypesThatNeedOneEntry_ReportItOnceWithTheGreatestOutput()
    {
        var run = RunTwoTypes("Output = GeneratorOutput.Models", "Output = GeneratorOutput.CodeGen", Users);

        run.Diagnostics.ShouldBe([
            $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                + "'codegen' needs one.  Run 'sqlsource describe'.",
        ]);
    }

    // A type with an error of its own gets no code, but the tool describes its files all the same, so what it needs
    // counts: its entry is no orphan, and a missing one is reported.
    [Fact]
    public void Run_TypeWithAnErrorOfItsOwn_StillNeedsItsEntries()
    {
        var source = Source.Replace("public partial class", "public class", StringComparison.Ordinal);
        const string NotPartial =
            "SQLSRC001 /app/Repo/Sample.cs(6,14)-(6,20): 'Sample' must be declared partial so that SqlSource can add "
            + "members to it";

        var withEntry = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [Users, Sidecars.Current(Users, SqlDialect.PostgreSql, "GetUser")],
            dialect: "postgres"
        );
        var withoutEntry = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [Users, Sidecars.Of(Users)],
            dialect: "postgres"
        );

        withEntry.Diagnostics.ShouldBe([NotPartial]);
        UsableEntries(withEntry).ShouldBe(["GetUser"]);
        withoutEntry.Diagnostics.ShouldBe(
            [
                NotPartial,
                $"SQLSRC241 {GetUserName}: The query 'GetUser' has no entry in the sidecar of its file, and its output "
                    + "'models' needs one.  Run 'sqlsource describe'.",
            ],
            ignoreOrder: true
        );
    }

    [Fact]
    public void Run_ToolVersionWithASuffix_MatchesThePrefix()
    {
        var run = Run(
            OutputKind.Models,
            Users,
            Sidecars.Of(Users, SidecarFormat.Version, PackageVersion.Prefix + "-dev", CurrentEntry(Users, "GetUser"))
        );

        run.Diagnostics.ShouldBeEmpty();
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    // The positions are those of the file as it is, the byte order mark included.
    [Fact]
    public void Run_SidecarWithAByteOrderMark_IsRead()
    {
        var sidecar = Sidecars.Of(Users, CurrentEntry(Users, "GetUser"), Sidecars.Entry("Gone", "h"));

        var run = Run(OutputKind.Models, Users, Sidecars.Raw(Users, "﻿" + sidecar.Text));

        run.Diagnostics.ShouldBe([
            $"SQLSRC245 {GeneratorHarness.UsersPath}.json(15,5)-(15,11): The entry 'Gone' names no query of the file "
                + "that needs one.  Run 'sqlsource describe', which removes it.",
        ]);
        UsableEntries(run).ShouldBe(["GetUser"]);
    }

    // Of two paths that differ only by case, the one spelled as the .sql file's path with .json after it is read,
    // whichever the project lists first: it is the file the tool reads and writes.
    [Fact]
    public void Run_TwoSidecarsThatDifferOnlyByCase_ReadTheOneSpelledAsItsFile()
    {
        var current = Sidecars.Current(Users, SqlDialect.PostgreSql, "GetUser");
        var other = new SqlFile("/app/Repo/users.sql.json", "{");

        var run = Run(OutputKind.Models, Users, current, other);
        var reversed = Run(OutputKind.Models, Users, other, current);

        run.Diagnostics.ShouldBeEmpty();
        UsableEntries(run).ShouldBe(["GetUser"]);
        reversed.Diagnostics.ShouldBeEmpty();
        UsableEntries(reversed).ShouldBe(["GetUser"]);
    }

    // With neither spelled as the .sql file's path, the first the project lists is read, as the first of two such
    // .sql files is.
    [Fact]
    public void Run_TwoSidecarsNeitherSpelledAsItsFile_ReadTheFirst()
    {
        var current = new SqlFile(
            "/app/Repo/users.sql.json",
            Sidecars.Current(Users, SqlDialect.PostgreSql, "GetUser").Text
        );
        var other = new SqlFile("/app/Repo/USERS.sql.json", "{");

        var run = Run(OutputKind.Models, Users, current, other);
        var reversed = Run(OutputKind.Models, Users, other, current);

        run.Diagnostics.ShouldBeEmpty();
        UsableEntries(run).ShouldBe(["GetUser"]);
        reversed.Diagnostics.ShouldBe([
            "SQLSRC237 /app/Repo/USERS.sql.json(1,2)-(1,2): The sidecar cannot be read, because it is not one JSON "
                + "object.  Run 'sqlsource describe' to write it again.",
        ]);
        UsableEntries(reversed).ShouldBeEmpty();
    }

    // Whatever a sidecar holds, an entry that failed a check is never usable.
    [Fact]
    public void Run_EveryCheck_GivesNoUsableEntryThatFailedOne()
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: Current\nSELECT 1;\n\n-- name: Stale\nSELECT 2;\n\n-- name: Missing\nSELECT 3;\n\n"
                + "-- name: OtherEngine\nSELECT 4;\n"
        );
        var orders = new SqlFile("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT 5;\n");
        var items = new SqlFile("/app/Repo/Items.sql", "-- name: GetItem\nSELECT 6;\n");
        var otherEngine = Sidecars.Entry(
            "OtherEngine",
            Sidecars.HashOf(users, "OtherEngine", SqlDialect.PostgreSql),
            "mssql"
        );

        var run = Run(
            OutputKind.Models,
            users,
            Sidecars.Of(users, CurrentEntry(users, "Current"), Sidecars.Entry("Stale", "stale"), otherEngine),
            orders,
            Sidecars.Raw(orders, WithHashAsANumber(Sidecars.Of(orders, CurrentEntry(orders, "GetOrder")).Text!)),
            items,
            Sidecars.Of(items, SidecarFormat.Version + 1, PackageVersion.Prefix, CurrentEntry(items, "GetItem"))
        );

        run.Diagnostics.Select(diagnostic => diagnostic[..9])
            .ShouldBe(["SQLSRC238", "SQLSRC237", "SQLSRC244", "SQLSRC241", "SQLSRC242"], ignoreOrder: true);
        UsableEntries(run).ShouldBe(["Current"]);
    }

    private static GeneratorRun Run(OutputKind output, params SqlFile[] files) => Run(output, files, "postgres");

    private static GeneratorRun Run(OutputKind output, SqlFile[] files, string dialect) =>
        GeneratorHarness.Run(output, [new SourceFile(GeneratorHarness.SourcePath, Source)], files, dialect: dialect);

    // Two types in one folder, which both claim its files, each with the arguments of its attribute.  The project is
    // at Sql, so the attributes decide.
    private static GeneratorRun RunTwoTypes(string first, string second, params SqlFile[] files) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    $"using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate({first})]\n"
                        + "public partial class First;\n\n"
                        + $"[SqlSourceGenerate({second})]\npublic partial class Second;\n"
                ),
            ],
            files,
            dialect: "postgres"
        );

    // The text of a sidecar whose hash is a number, which makes it malformed: every other key is as it was.
    private static string WithHashAsANumber(string text) =>
        text.Replace("\"hash\": ", "\"hash\": 1, \"was\": ", StringComparison.Ordinal);

    // A current entry of the query under postgres.
    private static SidecarEntry CurrentEntry(SqlFile file, string name) =>
        Sidecars.Entry(name, Sidecars.HashOf(file, name, SqlDialect.PostgreSql));

    // The names of the usable entries of every file, from what the step of the checks gave.
    private static string[] UsableEntries(GeneratorRun run) =>
        [
            .. StepOutputs
                .Of<SidecarCheckResult>(run, TrackingNames.SidecarCheck)
                .SelectMany(check => check.UsableEntries)
                .Select(entry => entry.Name),
        ];
}
