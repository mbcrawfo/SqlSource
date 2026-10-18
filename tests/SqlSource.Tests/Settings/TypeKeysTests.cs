using System;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

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
    public void TryNormalize_Alias_IsItsName(string key, string expected) => ShouldNormalize(key, expected);

    [Theory]
    [InlineData("int4", "int4")]
    [InlineData("Timestamptz", "timestamptz")]
    [InlineData("char", "char")]
    [InlineData("int4multirange", "int4multirange")]
    [InlineData("regclass", "regclass")]
    [InlineData("citext", "citext")]
    [InlineData("int4[]", "int4[]")]
    [InlineData("_int4", "_int4")]
    [InlineData("_TEXT", "_text")]
    public void TryNormalize_KnownName_IsValid(string key, string expected) => ShouldNormalize(key, expected);

    // What is left once the system schema is taken off is not checked, so a type the map does not know can be named.
    [Theory]
    [InlineData("pg_catalog.int4", "int4")]
    [InlineData("PG_CATALOG.int4[]", "int4[]")]
    [InlineData("pg_catalog.newtype", "newtype")]
    [InlineData("public.user_status", "public.user_status")]
    [InlineData("public.user_status[]", "public.user_status[]")]
    [InlineData("Billing.Invoice$State", "Billing.Invoice$State")]
    [InlineData("public.citext", "public.citext")]
    public void TryNormalize_QualifiedKey_IsKeptUnchecked(string key, string expected) =>
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
    public void TryNormalize_AnythingElse_IsNotAKey(string key) =>
        TypeKeys.TryNormalize(key.AsSpan(), out _).ShouldBeFalse();

    [Theory]
    [InlineData("citext")]
    [InlineData("hstore")]
    [InlineData("ltree")]
    [InlineData("lquery")]
    [InlineData("ltxtquery")]
    [InlineData("cube")]
    [InlineData("CItext")]
    [InlineData("HSTORE")]
    public void IsExtension_TypeOfAnExtension_IsOne(string internalName) =>
        TypeKeys.IsExtension(internalName).ShouldBeTrue();

    [Theory]
    [InlineData("text")]
    [InlineData("int4")]
    [InlineData("geometry")]
    public void IsExtension_AnyOtherType_IsNotOne(string internalName) =>
        TypeKeys.IsExtension(internalName).ShouldBeFalse();

    private static void ShouldNormalize(string key, string expected)
    {
        TypeKeys.TryNormalize(key.AsSpan(), out var normalized).ShouldBeTrue();

        normalized.ShouldBe(expected);
    }
}
