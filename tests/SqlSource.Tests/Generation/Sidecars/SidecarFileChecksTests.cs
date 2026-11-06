using System;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Sidecars;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Sidecars;

public sealed class SidecarFileChecksTests
{
    private const string Path = "/app/Users.sql.json";

    // A sidecar of this format with an entry of each version, GetUser and ListUsers, read as the generator reads it.
    private static SidecarFile FileOf(string toolVersion, string getUser, string listUsers) =>
        SidecarFileReader.Read(
            new SidecarInput(
                new InMemoryAdditionalText(
                    Path,
                    SidecarWriter.Write(
                        new Sidecar(
                            SidecarFormat.Version,
                            toolVersion,
                            new EquatableArray<SidecarEntry>([
                                TestSidecars.Entry("GetUser", toolVersion: getUser),
                                TestSidecars.Entry("ListUsers", toolVersion: listUsers),
                            ])
                        )
                    )
                ),
                "app/Users.sql"
            ),
            TestContext.Current.CancellationToken
        );

    private static (Sidecar? Sidecar, ImmutableArray<DiagnosticInfo> Diagnostics) Check(SidecarFile file)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var sidecar = SidecarFileChecks.Read(file, diagnostics);
        return (sidecar, diagnostics.ToImmutable());
    }

    [Fact]
    public void Read_FileAndEntriesOfThisTool_DoesNotWarn()
    {
        var file = FileOf(PackageVersion.Prefix, PackageVersion.Prefix, PackageVersion.Prefix);

        var (sidecar, diagnostics) = Check(file);

        diagnostics.ShouldBeEmpty();
        sidecar.ShouldBe(file.Result.Sidecar);
    }

    // What a git merge can leave: a file of this tool that holds an entry of another.
    [Fact]
    public void Read_OneEntryOfAnotherTool_WarnsSqlsrc240AtItsKey()
    {
        var file = FileOf(PackageVersion.Prefix, PackageVersion.Prefix, "0.0.1");

        var (sidecar, diagnostics) = Check(file);

        diagnostics
            .ShouldHaveSingleItem()
            .ShouldBe(
                DiagnosticInfo.Create(
                    SqlDiagnostics.SidecarToolVersionDiffers,
                    file.EntryLocations[1],
                    "0.0.1",
                    PackageVersion.Prefix
                )
            );
        sidecar.ShouldNotBeNull().ShouldBe(file.Result.Sidecar);
    }

    [Fact]
    public void Read_TwoEntriesOfAnotherTool_WarnsOnceAtTheFirst()
    {
        var file = FileOf(PackageVersion.Prefix, "0.0.1", "0.0.2");

        var (sidecar, diagnostics) = Check(file);

        diagnostics
            .ShouldHaveSingleItem()
            .ShouldBe(
                DiagnosticInfo.Create(
                    SqlDiagnostics.SidecarToolVersionDiffers,
                    file.EntryLocations[0],
                    "0.0.1",
                    PackageVersion.Prefix
                )
            );
        _ = sidecar.ShouldNotBeNull();
    }

    [Fact]
    public void Read_FileAndEntryOfAnotherTool_WarnsOnceAtTheStart()
    {
        var file = FileOf("0.0.1", PackageVersion.Prefix, "0.0.2");

        var (sidecar, diagnostics) = Check(file);

        diagnostics
            .ShouldHaveSingleItem()
            .ShouldBe(
                DiagnosticInfo.Create(
                    SqlDiagnostics.SidecarToolVersionDiffers,
                    file.Start,
                    "0.0.1",
                    PackageVersion.Prefix
                )
            );
        sidecar.ShouldNotBeNull().ShouldBe(file.Result.Sidecar);
    }

    // The build compares the prefix alone, as it does the file's.
    [Fact]
    public void Read_EntryVersionWithSuffix_DoesNotWarn()
    {
        var file = FileOf(PackageVersion.Prefix, PackageVersion.Prefix, PackageVersion.Prefix + "-dev");

        var (sidecar, diagnostics) = Check(file);

        diagnostics.ShouldBeEmpty();
        sidecar.ShouldNotBeNull().ShouldBe(file.Result.Sidecar);
    }

    // The kind is passed as its number, since the enum is internal and a test method is public.
    [Theory]
    [InlineData((int)SidecarErrorKind.InvalidJson, null, "it is not one JSON object")]
    [InlineData((int)SidecarErrorKind.MissingKey, "hash", "'hash' is missing")]
    [InlineData((int)SidecarErrorKind.WrongType, "hash", "'hash' has a value of the wrong type")]
    [InlineData((int)SidecarErrorKind.DuplicateKey, "GetUser", "'GetUser' is written twice")]
    [InlineData((int)SidecarErrorKind.OrdinalMismatch, "3", "the ordinal 3 is not the position of its element")]
    [InlineData((int)SidecarErrorKind.ColumnsWithoutRows, null, "it has columns for a query without rows")]
    [InlineData((int)SidecarErrorKind.UnknownResultKind, "some", "'some' is not a result kind")]
    public void ReasonOf_Error_SaysWhatIsWrong(int kind, string? argument, string reason) =>
        SidecarFileChecks.ReasonOf(new SidecarError((SidecarErrorKind)kind, default, argument)).ShouldBe(reason);

    // A kind added to the reader gets a reason of its own, and never reads as another kind.
    [Fact]
    public void ReasonOf_EveryKind_HasAReasonOfItsOwn()
    {
        var reasons = Enum.GetValues<SidecarErrorKind>()
            .Select(kind => SidecarFileChecks.ReasonOf(new SidecarError(kind, default, "x")))
            .ToArray();

        reasons.ShouldBeUnique();
    }
}
