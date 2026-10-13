using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The sidecar's record of a type, from the rows of the closure.
public class PostgresTypesTests
{
    private static readonly PostgresType Integer = TestSidecars.Postgres();

    private static readonly PostgresType CharacterVarying = TestSidecars.Postgres(
        "character varying",
        internalName: "varchar"
    );

    private static readonly PostgresType EmailT = TestSidecars.Postgres(
        "public.email_t",
        SidecarValues.Kind.Domain,
        "public",
        "email_t",
        baseType: CharacterVarying
    );

    [Fact]
    public void Build_BaseType_HasItsNameSchemaAndInternalName() =>
        PostgresTypes.Build(23, PostgresRows.Of(PostgresRows.Int4), PostgresFacets.None).ShouldBe(Integer);

    [Fact]
    public void Build_BaseTypeWithFacets_CarriesThemAndSpellsThem() =>
        PostgresTypes
            .Build(1700, PostgresRows.Of(PostgresRows.Numeric), new PostgresFacets(null, 18, 2))
            .ShouldBe(TestSidecars.Postgres("numeric(18,2)", internalName: "numeric", precision: 18, scale: 2));

    // An element is built without facets, and the array has none.
    [Fact]
    public void Build_Array_HasItsElementAndTheElementsNameWithTheMark() =>
        PostgresTypes
            .Build(1009, PostgresRows.Of(PostgresRows.Text, PostgresRows.TextArray), new PostgresFacets(5, null, null))
            .ShouldBe(
                TestSidecars.Postgres(
                    "text[]",
                    SidecarValues.Kind.Array,
                    internalName: "_text",
                    element: TestSidecars.Postgres("text", internalName: "text")
                )
            );

    [Fact]
    public void Build_Domain_HasItsBase() =>
        PostgresTypes
            .Build(16387, PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT), PostgresFacets.None)
            .ShouldBe(EmailT);

    // The protocol applies a domain's modifier to its base, so the facets land there.
    [Fact]
    public void Build_DomainWithFacets_PutsThemOnItsBase() =>
        PostgresTypes
            .Build(
                16387,
                PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT),
                new PostgresFacets(254, null, null)
            )
            .ShouldBe(
                EmailT with
                {
                    Base = TestSidecars.Postgres("character varying(254)", internalName: "varchar", length: 254),
                }
            );

    [Fact]
    public void Build_DomainOverADomain_HasTheChainOfBases()
    {
        var work = PostgresRows.DomainOf(16400, "work_email", PostgresRows.EmailT, "billing");

        PostgresTypes
            .Build(16400, PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT, work), PostgresFacets.None)
            .ShouldBe(
                TestSidecars.Postgres(
                    "billing.work_email",
                    SidecarValues.Kind.Domain,
                    "billing",
                    "work_email",
                    baseType: EmailT
                )
            );
    }

    [Fact]
    public void Build_ArrayOfADomain_IsNamedByTheDomain()
    {
        var emails = PostgresRows.ArrayOf(16386, "_email_t", PostgresRows.EmailT);

        var built = PostgresTypes.Build(
            16386,
            PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT, emails),
            PostgresFacets.None
        );

        built.Name.ShouldBe("public.email_t[]");
        built.Kind.ShouldBe(SidecarValues.Kind.Array);
        built.Schema.ShouldBe("public");
        built.Element.ShouldBe(EmailT);
    }

    [Fact]
    public void Build_Enum_HasItsLabelsInOrder() =>
        PostgresTypes
            .Build(16390, PostgresRows.Of(PostgresRows.UserStatus), PostgresFacets.None)
            .ShouldBe(
                TestSidecars.Postgres(
                    "public.user_status",
                    SidecarValues.Kind.Enum,
                    "public",
                    "user_status",
                    labels: ["active", "disabled"]
                )
            );

    [Fact]
    public void Build_Range_HasItsSubtype() =>
        PostgresTypes
            .Build(3910, PostgresRows.Of(PostgresRows.Timestamptz, PostgresRows.Tstzrange), PostgresFacets.None)
            .ShouldBe(
                TestSidecars.Postgres(
                    "tstzrange",
                    SidecarValues.Kind.Range,
                    internalName: "tstzrange",
                    subtype: TestSidecars.Postgres("timestamp with time zone", internalName: "timestamptz")
                )
            );

    // The subtype of a multirange is its range's.
    [Fact]
    public void Build_Multirange_HasTheSubtypeOfItsRange() =>
        PostgresTypes
            .Build(
                4451,
                PostgresRows.Of(PostgresRows.Int4, PostgresRows.Int4range, PostgresRows.Int4multirange),
                PostgresFacets.None
            )
            .ShouldBe(
                TestSidecars.Postgres(
                    "int4multirange",
                    SidecarValues.Kind.Multirange,
                    internalName: "int4multirange",
                    subtype: Integer
                )
            );

    [Fact]
    public void Build_Composite_HasItsKindAndNothingMore()
    {
        var address = new PostgresTypeRow(16500, "address", "public", "c", "C", 0, 0, null, null, "address", null);

        PostgresTypes
            .Build(16500, PostgresRows.Of(address), PostgresFacets.None)
            .ShouldBe(TestSidecars.Postgres("public.address", SidecarValues.Kind.Composite, "public", "address"));
    }

    [Fact]
    public void IsPseudo_Type_IsWhetherItsTyptypeIsP()
    {
        var rows = PostgresRows.Of(PostgresRows.Record, PostgresRows.Int4);

        PostgresTypes.IsPseudo(2249, rows).ShouldBeTrue();
        PostgresTypes.IsPseudo(23, rows).ShouldBeFalse();
    }

    // "cstring[]" is a base type of the category A, as "text[]" is, and no value has an array of what no value has.
    [Fact]
    public void IsPseudo_ArrayOfAPseudoType_IsTrueAndOfAnyOtherTypeFalse()
    {
        var cstring = PostgresRows.PseudoOf(2275, "cstring");
        var rows = PostgresRows.Of(
            cstring,
            PostgresRows.ArrayOf(1263, "_cstring", cstring),
            PostgresRows.Text,
            PostgresRows.TextArray
        );

        PostgresTypes.IsPseudo(1263, rows).ShouldBeTrue();
        PostgresTypes.IsPseudo(1009, rows).ShouldBeFalse();
    }

    // A domain follows its base, as an array follows its element.
    [Fact]
    public void IsPseudo_DomainOverAPseudoTypeOrAnArrayOfOne_IsTrueAndOverAnyOtherTypeFalse()
    {
        var cstring = PostgresRows.PseudoOf(2275, "cstring");
        var cstrings = PostgresRows.ArrayOf(1263, "_cstring", cstring);
        var over = PostgresRows.DomainOf(16600, "over_cstring", cstring);
        var overArray = PostgresRows.DomainOf(16601, "over_cstrings", cstrings);
        var rows = PostgresRows.Of(cstring, cstrings, over, overArray, PostgresRows.Varchar, PostgresRows.EmailT);

        PostgresTypes.IsPseudo(over.Oid, rows).ShouldBeTrue();
        PostgresTypes.IsPseudo(overArray.Oid, rows).ShouldBeTrue();
        PostgresTypes.IsPseudo(PostgresRows.EmailT.Oid, rows).ShouldBeFalse();
    }

    // The session asks IsPseudo first, so a pseudo-type here is a bug.
    [Fact]
    public void Build_PseudoType_Throws() =>
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypes.Build(2249, PostgresRows.Of(PostgresRows.Record), PostgresFacets.None)
            )
            .Message.ShouldContain("record");

    [Fact]
    public void Build_OidTheRowsLack_ThrowsNamingIt()
    {
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypes.Build(99, PostgresRows.Of(PostgresRows.Int4), PostgresFacets.None)
            )
            .Message.ShouldContain("99");
        // The element of an array the closure should have reached.
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypes.Build(1009, PostgresRows.Of(PostgresRows.TextArray), PostgresFacets.None)
            )
            .Message.ShouldContain("25");
    }
}
