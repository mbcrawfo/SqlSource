using System;
using Shouldly;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings.TypeOverrides;

public sealed class TypeKeysTests
{
    [Theory]
    [InlineData("integer", "int4")]
    [InlineData("int", "int4")]
    [InlineData("smallint", "int2")]
    [InlineData("bigint", "int8")]
    [InlineData("real", "float4")]
    [InlineData("float", "float8")]
    [InlineData("boolean", "bool")]
    [InlineData("decimal", "numeric")]
    [InlineData("character", "bpchar")]
    [InlineData("INTEGER", "int4")]
    [InlineData("integer[]", "int4[]")]
    public void TryNormalizePostgres_Alias_IsItsName(string key, string expected) => ShouldNormalize(key, expected);

    [Theory]
    [InlineData("int4", "int4")]
    [InlineData("Timestamptz", "timestamptz")]
    [InlineData("char", "char")]
    [InlineData("int4multirange", "int4multirange")]
    [InlineData("regclass", "regclass")]
    [InlineData("citext", "citext")]
    [InlineData("int4[]", "int4[]")]
    public void TryNormalizePostgres_KnownName_IsValid(string key, string expected) => ShouldNormalize(key, expected);

    // The catalog names an array of a type with _ before the type's name, and that name is the array's key, so the
    // two spellings are one key: for a name the map knows without a schema, and for any name of pg_catalog, where
    // no other type starts with _.  An array of such a name is kept as written.
    [Theory]
    [InlineData("_int4", "int4[]")]
    [InlineData("_TEXT", "text[]")]
    [InlineData("pg_catalog._int4", "int4[]")]
    [InlineData("PG_CATALOG._Citext", "citext[]")]
    [InlineData("pg_catalog._pg_class", "pg_class[]")]
    [InlineData("pg_catalog._newtype", "newtype[]")]
    [InlineData("_int4[]", "_int4[]")]
    [InlineData("pg_catalog._int4[]", "_int4[]")]
    public void TryNormalizePostgres_CatalogNameOfAnArray_IsItsElementsKeyWithBrackets(string key, string expected) =>
        ShouldNormalize(key, expected);

    // What is left once the system schema is taken off is not checked, so a type the map does not know can be named.
    [Theory]
    [InlineData("pg_catalog.int4", "int4")]
    [InlineData("PG_CATALOG.int4[]", "int4[]")]
    [InlineData("pg_catalog.newtype", "newtype")]
    [InlineData("public.user_status", "public.user_status")]
    [InlineData("public.user_status[]", "public.user_status[]")]
    [InlineData("Billing.Invoice$State", "Billing.Invoice$State")]
    [InlineData("public.citext", "public.citext")]
    [InlineData("public._status", "public._status")]
    public void TryNormalizePostgres_QualifiedKey_IsKeptUnchecked(string key, string expected) =>
        ShouldNormalize(key, expected);

    // A plain name of pg_catalog is read in lower case when the map knows it, as it is without the schema, so the two
    // spellings are one key.  A name the map does not know stays as written.
    [Theory]
    [InlineData("pg_catalog.INT4", "int4")]
    [InlineData("PG_CATALOG.Timestamptz[]", "timestamptz[]")]
    [InlineData("pg_catalog._INT4[]", "_int4[]")]
    [InlineData("pg_catalog.NewType", "NewType")]
    [InlineData("pg_catalog.Integer", "Integer")]
    public void TryNormalizePostgres_KnownCatalogNameWithItsSchema_IsItsName(string key, string expected) =>
        ShouldNormalize(key, expected);

    [Theory]
    [InlineData("")]
    [InlineData("timestampz")]
    [InlineData("user_status")]
    [InlineData("\"quoted\"")]
    [InlineData("numeric(18,2)")]
    [InlineData("a.b.c")]
    [InlineData("int4[][]")]
    [InlineData("int4[")]
    [InlineData("[]")]
    [InlineData("_integer")]
    [InlineData("_")]
    [InlineData(".int4")]
    [InlineData("public.")]
    [InlineData("pg_catalog.")]
    [InlineData("timestamp with time zone")]
    [InlineData("int 4")]
    [InlineData("\"\"")]
    [InlineData("public.\"\"")]
    [InlineData("\"unclosed")]
    [InlineData("public.\"a\"b")]
    [InlineData("\"a\".\"b\".\"c\"")]
    [InlineData("public. \"x\"")]
    [InlineData("\"Int4\"")]
    [InlineData("\"integer\"")]
    [InlineData("\"int4\"[][]")]
    public void TryNormalizePostgres_AnythingElse_IsNotAKey(string key) =>
        TypeKeys.TryNormalizePostgres(key.AsSpan(), out _).ShouldBeFalse();

    // A part may be quoted, to hold what a plain one cannot and to keep its case.  A key with a quoted part is kept as
    // written, and the system schema's own names are read as above, whatever way they are written.
    [Theory]
    [InlineData("public.\"Status\"", "public.\"Status\"")]
    [InlineData("\"my schema\".\"order-status\"", "\"my schema\".\"order-status\"")]
    [InlineData("\"my schema\".status[]", "\"my schema\".status[]")]
    [InlineData("\"a.b\".c", "\"a.b\".c")]
    [InlineData("a.\"b.c\"", "a.\"b.c\"")]
    [InlineData("s.\"x[]\"", "s.\"x[]\"")]
    [InlineData("s.\"a=b\"", "s.\"a=b\"")]
    [InlineData("s.\"say \"\"hi\"\"\"", "s.\"say \"\"hi\"\"\"")]
    [InlineData("\"PG_CATALOG\".int4", "\"PG_CATALOG\".int4")]
    public void TryNormalizePostgres_QuotedPart_IsKeptAsWritten(string key, string expected) =>
        ShouldNormalize(key, expected);

    [Theory]
    [InlineData("\"char\"", "char")]
    [InlineData("\"int4\"[]", "int4[]")]
    [InlineData("\"_int4\"", "int4[]")]
    [InlineData("\"pg_catalog\".int4", "int4")]
    [InlineData("pg_catalog.\"int4\"", "int4")]
    [InlineData("pg_catalog.\"_int4\"", "int4[]")]
    [InlineData("pg_catalog.\"New Type\"", "\"New Type\"")]
    [InlineData("pg_catalog.\"_Odd\"", "\"Odd\"[]")]
    [InlineData("pg_catalog.\"_odd\"", "odd[]")]
    [InlineData("pg_catalog.\"newtype\"", "\"newtype\"")]
    [InlineData("\"_int4\"[]", "_int4[]")]
    [InlineData("pg_catalog.\"int4\"[]", "int4[]")]
    [InlineData("pg_catalog.\"_int4\"[]", "_int4[]")]
    [InlineData("pg_catalog.\"_x\"[]", "\"_x\"[]")]
    [InlineData("pg_catalog.\"_a\tb\"", "U&\"a\\0009b\"[]")]
    public void TryNormalizePostgres_QuotedCatalogName_IsItsKey(string key, string expected) =>
        ShouldNormalize(key, expected);

    // An escaped part is read to its text, and given in the quoted form of that text.
    [Theory]
    [InlineData("public.U&\"st\\0061tus\"", "public.\"status\"")]
    [InlineData("public.u&\"St\\+000061tus\"", "public.\"Status\"")]
    [InlineData("U&\"my schema\".x", "\"my schema\".x")]
    [InlineData("s.U&\"a\\\\b\"", "s.\"a\\b\"")]
    [InlineData("s.U&\"a\\0022b\"", "s.\"a\"\"b\"")]
    [InlineData("s.U&\"a\"\"b\"", "s.\"a\"\"b\"")]
    [InlineData("s.U&\"x\"[]", "s.\"x\"[]")]
    [InlineData("U&\"int4\"", "int4")]
    [InlineData("U&\"_int4\"", "int4[]")]
    [InlineData("pg_catalog.U&\"_int4\"", "int4[]")]
    [InlineData("pg_catalog.U&\"New Type\"", "\"New Type\"")]
    [InlineData("U&\"pg_catalog\".int4", "int4")]
    [InlineData("pg_catalog.U&\"_x\"[]", "\"_x\"[]")]
    [InlineData("pg_catalog.U&\"newtype\"[]", "\"newtype\"[]")]
    public void TryNormalizePostgres_EscapedPart_IsItsQuotedForm(string key, string expected) =>
        ShouldNormalize(key, expected);

    [Theory]
    [InlineData("U&\"Int4\"")]
    [InlineData("U&\"\"")]
    [InlineData("s.U&\"\"")]
    [InlineData("s.U&\"\\0000\"")]
    [InlineData("s.U&\"\\00\"")]
    [InlineData("s.U&\"\\00G1\"")]
    [InlineData("s.U&\"a\\\"")]
    [InlineData("s.U&\"\\x\"")]
    [InlineData("s.U&\"\\+11FFFF\"")]
    [InlineData("s.U&\"\\+00006\"")]
    [InlineData("s.U&\"\\D800\"")]
    [InlineData("s.U&\"\\DE00\\D83D\"")]
    [InlineData("s.U&\"\\D83Dx\"")]
    [InlineData("s.U&\"\\D83D\\D83D\"")]
    [InlineData("s.U&x")]
    [InlineData("s.U& \"x\"")]
    [InlineData("s.U&\"x\" UESCAPE '!'")]
    [InlineData("s.U&\"unclosed")]
    public void TryNormalizePostgres_EscapedPartThatIsNotValid_IsNotAKey(string key) =>
        TypeKeys.TryNormalizePostgres(key.AsSpan(), out _).ShouldBeFalse();

    // SQL Server reads a name or one of its aliases ignoring case, as PostgreSQL does its own.
    [Theory]
    [InlineData("int", "int")]
    [InlineData("INT", "int")]
    [InlineData("integer", "int")]
    [InlineData("character", "char")]
    [InlineData("dec", "decimal")]
    [InlineData("rowversion", "timestamp")]
    [InlineData("timestamp", "timestamp")]
    [InlineData("nvarchar", "nvarchar")]
    [InlineData("sysname", "sysname")]
    [InlineData("hierarchyid", "hierarchyid")]
    [InlineData("json", "json")]
    [InlineData("vector", "vector")]
    [InlineData("\"int\"", "int")]
    public void TryNormalizeSqlServer_NameOrAlias_IsTheName(string key, string expected) =>
        ShouldNormalizeSqlServer(key, expected);

    // sys is SQL Server's schema of its types, as pg_catalog is PostgreSQL's: taken off, with a name the map knows in
    // lower case and any other as written.
    [Theory]
    [InlineData("sys.int", "int")]
    [InlineData("SYS.INT", "int")]
    [InlineData("sys.\"int\"", "int")]
    [InlineData("\"sys\".int", "int")]
    [InlineData("sys.integer", "integer")]
    [InlineData("sys.newtype", "newtype")]
    [InlineData("sys.\"Odd\"", "\"Odd\"")]
    [InlineData("sys.U&\"st\\0061tus\"", "\"status\"")]
    public void TryNormalizeSqlServer_KeyOfTheSystemSchema_IsTheNameWithoutIt(string key, string expected) =>
        ShouldNormalizeSqlServer(key, expected);

    // Any other schema is a user's, and its key is the one PostgreSQL reads.
    [Theory]
    [InlineData("dbo.Email", "dbo.Email")]
    [InlineData("\"dbo\".\"Email\"", "\"dbo\".\"Email\"")]
    [InlineData("dbo.U&\"Em\\0061il\"", "dbo.\"Email\"")]
    [InlineData("\"SYS\".int", "\"SYS\".int")]
    public void TryNormalizeSqlServer_KeyOfAUsersSchema_IsPostgreSqlsKey(string key, string expected)
    {
        ShouldNormalizeSqlServer(key, expected);
        ShouldNormalize(key, expected);
    }

    [Theory]
    [InlineData("\"Int\"")]
    [InlineData("\"integer\"")]
    [InlineData("int4")]
    [InlineData("_int4")]
    [InlineData("int[]")]
    [InlineData("dbo.Email[]")]
    [InlineData("pg_catalog.text")]
    [InlineData("\"pg_catalog\".text")]
    [InlineData("[dbo].[Email]")]
    [InlineData("double precision")]
    [InlineData("nosuch")]
    [InlineData("")]
    public void TryNormalizeSqlServer_AnythingElse_IsNotAKey(string key)
    {
        TypeKeys.TryNormalizeSqlServer(key.AsSpan(), out var normalized).ShouldBeFalse();
        normalized.ShouldBeEmpty();
    }

    // A name that SQL Server alone has is no key of PostgreSQL's.
    [Theory]
    [InlineData("nvarchar")]
    [InlineData("rowversion")]
    public void TryNormalizePostgres_NameOfSqlServerAlone_IsNotAKey(string key) =>
        TypeKeys.TryNormalizePostgres(key.AsSpan(), out _).ShouldBeFalse();

    [Fact]
    public void TryNormalizePostgres_SysSchema_IsAUsersSchema() => ShouldNormalize("sys.int", "sys.int");

    // TryNormalizeEach gives what each engine's own reading gives, null for an engine that does not read the key.
    [Theory]
    [InlineData("int", "int4", "int")]
    [InlineData("int4", "int4", null)]
    [InlineData("nvarchar", null, "nvarchar")]
    [InlineData("sys.int", "sys.int", "int")]
    [InlineData("pg_catalog.text", "text", null)]
    [InlineData("\"sys\".\"int\"", "\"sys\".\"int\"", "int")]
    [InlineData("dbo.Email", "dbo.Email", "dbo.Email")]
    [InlineData("dbo.U&\"Em\\0061il\"", "dbo.\"Email\"", "dbo.\"Email\"")]
    [InlineData("dbo.Email[]", "dbo.Email[]", null)]
    [InlineData("_int4", "int4[]", null)]
    public void TryNormalizeEach_Key_IsWhatEachEnginesReadingGives(string key, string? postgreSql, string? sqlServer)
    {
        TypeKeys.TryNormalizeEach(key.AsSpan(), out var first, out var second).ShouldBeTrue();

        first.ShouldBe(postgreSql);
        second.ShouldBe(sqlServer);
        (TypeKeys.TryNormalizePostgres(key.AsSpan(), out var alone) ? alone : null).ShouldBe(postgreSql);
        (TypeKeys.TryNormalizeSqlServer(key.AsSpan(), out var sqlServerAlone) ? sqlServerAlone : null).ShouldBe(
            sqlServer
        );
    }

    [Theory]
    [InlineData("nosuch")]
    [InlineData("[dbo].[Email]")]
    [InlineData("")]
    public void TryNormalizeEach_KeyThatNeitherEngineReads_IsFalse(string key)
    {
        TypeKeys.TryNormalizeEach(key.AsSpan(), out var postgreSql, out var sqlServer).ShouldBeFalse();

        postgreSql.ShouldBeNull();
        sqlServer.ShouldBeNull();
    }

    private static void ShouldNormalizeSqlServer(string key, string expected)
    {
        TypeKeys.TryNormalizeSqlServer(key.AsSpan(), out var normalized).ShouldBeTrue();

        normalized.ShouldBe(expected);
    }

    private static void ShouldNormalize(string key, string expected)
    {
        TypeKeys.TryNormalizePostgres(key.AsSpan(), out var normalized).ShouldBeTrue();

        normalized.ShouldBe(expected);
    }
}
