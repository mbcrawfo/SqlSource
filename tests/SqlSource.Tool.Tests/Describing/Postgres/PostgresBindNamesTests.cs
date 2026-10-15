using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The name that check binds a parameter by, from the rows of the types alone.
public class PostgresBindNamesTests
{
    // numeric as a session shows it when a user type of the same name stands before pg_catalog on the search_path.
    private static readonly PostgresTypeRow HiddenNumeric = PostgresRows.BaseOf(
        1700,
        "numeric",
        "pg_catalog.numeric",
        "N"
    );

    private static readonly PostgresTypeRow[] Known =
    [
        PostgresRows.Int4,
        HiddenNumeric,
        PostgresRows.Varchar,
        PostgresRows.Timestamptz,
        PostgresRows.Bit,
        PostgresRows.Varbit,
        PostgresRows.QuotedChar,
        PostgresRows.Text,
        PostgresRows.TextArray,
        PostgresRows.UserStatus,
        PostgresRows.MySchemaStatus,
        PostgresRows.MySchemaStatusArray,
        PostgresRows.UserStatusInReservedSchema,
        PostgresRows.EmailT,
        PostgresRows.D2,
        PostgresRows.CheckPair,
        PostgresRows.InDottedSchema,
        PostgresRows.InDottedSchemaArray,
        PostgresRows.WithDottedName,
        PostgresRows.WithDottedNameArray,
    ];

    public static TheoryData<long, string> Names =>
        new()
        {
            { PostgresRows.Int4.Oid, "integer" },
            { PostgresRows.Varchar.Oid, "character varying" },
            { PostgresRows.Timestamptz.Oid, "timestamp with time zone" },
            // format_type(1560, -1) is "bit" with the quotes, which Npgsql does not take.
            { PostgresRows.Bit.Oid, "bit" },
            { PostgresRows.Varbit.Oid, "bit varying" },
            { HiddenNumeric.Oid, "numeric" },
            // For now the server's spelling; Npgsql may refuse it.
            { PostgresRows.QuotedChar.Oid, "\"char\"" },
            { PostgresRows.TextArray.Oid, "text[]" },
            { PostgresRows.UserStatus.Oid, "public.user_status" },
            { PostgresRows.MySchemaStatus.Oid, "My Schema.Status" },
            { PostgresRows.MySchemaStatusArray.Oid, "My Schema.Status[]" },
            { PostgresRows.UserStatusInReservedSchema.Oid, "user.status" },
            { PostgresRows.EmailT.Oid, "public.email_t" },
            // Npgsql sends a domain as its base and refuses only a domain over a domain.
            { PostgresRows.D2.Oid, "character varying" },
            { PostgresRows.CheckPair.Oid, "public.check_pair" },
        };

    public static TheoryData<long> NamesWithADot =>
        [
            PostgresRows.InDottedSchema.Oid,
            PostgresRows.InDottedSchemaArray.Oid,
            PostgresRows.WithDottedName.Oid,
            PostgresRows.WithDottedNameArray.Oid,
        ];

    [Theory]
    [MemberData(nameof(Names))]
    public void Of_EachKindOfType_IsTheNameNpgsqlBindsBy(long oid, string expected) =>
        PostgresBindNames.Of(oid, PostgresRows.Of(Known)).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(NamesWithADot))]
    public void Of_SchemaOrNameWithADot_IsNull(long oid) =>
        PostgresBindNames.Of(oid, PostgresRows.Of(Known)).ShouldBeNull();

    [Fact]
    public void NoName_Always_SaysThatAnAmbiguousNameCannotBeGivenToNpgsql() =>
        PostgresBindNames.NoName.ShouldContain("'.'");
}
