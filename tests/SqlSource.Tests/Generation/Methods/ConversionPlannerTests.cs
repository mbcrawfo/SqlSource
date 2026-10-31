using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Types;
using SqlSource.Settings;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

// The converter of one column or parameter, decided from its type, its base map's type and the converters of its
// type's slice, one case for each line of the decision.
public sealed class ConversionPlannerTests
{
    private const string Email = "global::MyApp.Email";
    private const string UserId = "global::MyApp.UserId";
    private const string When = "global::MyApp.When";
    private const string DateTime = "global::System.DateTime";
    private const string DateTimeOffset = "global::System.DateTimeOffset";
    private const string Instant = "global::NodaTime.Instant";
    private const string LocalDate = "global::NodaTime.LocalDate";
    private const string OrderMetadata = "global::MyApp.OrderMetadata";
    private const string Point = "global::MyApp.Point";
    private const string Deserialize = "global::System.Text.Json.JsonSerializer.Deserialize<";
    private const string Serialize = "global::System.Text.Json.JsonSerializer.Serialize<";
    private const string Place = "the typeOverrides of the settings file /app/sqlsource.json";
    private const string LibraryPlace = "the NodaTime library";
    private const string Geometry = "global::NetTopologySuite.Geometries.Geometry";
    private const string GeometryPoint = "global::NetTopologySuite.Geometries.Point";

    private static readonly LocationInfo At = new(
        "/app/Repo/Users.sql",
        new TextSpan(9, 7),
        new LinePositionSpan(new LinePosition(0, 9), new LinePosition(0, 16))
    );

    // public.email=MyApp.Email over a domain of text.
    private static readonly PropertyType EmailType = Overridden(Email, false, "public.email", "string", false);

    [Fact]
    public void Plan_ReadWithTheNeededEdge_GivesItsConversion()
    {
        var converters = Of(Project("global::MyApp.Email.From", "string", false, Email, false));

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBe(new Conversion("global::MyApp.Email.From", "string", false, false, null, false));
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    [Fact]
    public void Plan_ReadWithOnlyTheWriteEdge_ReportsSqlsrc280()
    {
        var converters = Of(Project("global::MyApp.Email.To", Email, false, "string", false));

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC280: The column 'email' has type 'MyApp.Email', which has a converter to 'string' but none from "
                + "it, which a column needs.  Add a static method marked [SqlSourceConverter] that takes 'string' and "
                + "returns 'MyApp.Email'.",
        ]);
    }

    [Fact]
    public void Plan_WriteWithOnlyTheReadEdge_ReportsSqlsrc280()
    {
        var converters = Of(Project("global::MyApp.Email.From", "string", false, Email, false));

        var (conversion, problems, blocked) = Plan(EmailType, isRead: false, converters, "parameter 'email'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC280: The parameter 'email' has type 'MyApp.Email', which has a converter from 'string' but none "
                + "to it, which a parameter needs.  Add a static method marked [SqlSourceConverter] that takes "
                + "'MyApp.Email' and returns 'string'.",
        ]);
    }

    // An int8 column, read as long, mapped to UserId, whose converters are of int.
    [Fact]
    public void Plan_LongColumnWithAnIntConverter_ReportsSqlsrc279()
    {
        var type = Overridden(UserId, true, "int8", "long", true);
        var converters = Of(
            Project("global::MyApp.Ids.ToUserId", "int", true, UserId, true),
            Project("global::MyApp.Ids.FromUserId", UserId, true, "int", true)
        );

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'id'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC279: The column 'id' has type 'MyApp.UserId', whose converters convert from and to 'int', and not "
                + "from and to 'long', the type the database type 'int8' is read as.  Add a converter between "
                + "'MyApp.UserId' and 'long'.",
        ]);
    }

    // The other types are named each once, in ordinal order.
    [Fact]
    public void Plan_ForeignTypeWithConvertersOfTwoOtherTypes_NamesBoth()
    {
        var type = Overridden(UserId, true, "int8", "long", true);
        var converters = Of(
            Project("global::MyApp.Ids.FromShort", "short", true, UserId, true),
            Project("global::MyApp.Ids.ToShort", UserId, true, "short", true),
            Project("global::MyApp.Ids.FromInt", "int", true, UserId, true)
        );

        var (_, problems, _) = Plan(type, isRead: true, converters, "column 'id'");

        problems.ShouldHaveSingleItem().ShouldContain("convert from and to 'int' and 'short', and not");
    }

    [Fact]
    public void Plan_ForeignTypeWithoutConverters_GivesNull()
    {
        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, Of<Converter>());

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // DateTimeOffset is a type the base map gives, so a converter of it to another type does not touch a column
    // that the override reads as one.
    [Fact]
    public void Plan_NotForeignTypeWithConvertersOfAnotherType_GivesNull()
    {
        var type = Overridden(DateTimeOffset, true, "timestamptz", DateTime, true);
        var converters = Of(
            Project("global::MyApp.Moments.ToWhen", DateTimeOffset, true, When, true),
            Project("global::MyApp.Moments.FromWhen", When, true, DateTimeOffset, true)
        );

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters);

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A type of the base map has no converter, whatever the converters of its type.
    [Fact]
    public void Plan_NoPlace_GivesNull()
    {
        var type = new PropertyType("string", false, true, EquatableArray<string>.Empty, "text", null);
        var converters = Of(Project("global::MyApp.Email.To", Email, false, "string", false));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters);

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A composite has no base map type to convert from.
    [Fact]
    public void Plan_NoBaseTypeWithAConverter_ReportsSqlsrc279()
    {
        var type = Overridden("global::MyApp.Address", false, "public.address", null, false);
        var converters = Of(Project("global::MyApp.Address.Parse", "string", false, "global::MyApp.Address", false));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'address'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC279: The column 'address' has type 'MyApp.Address', which has converters, but the database type "
                + "'public.address' has no C# type to convert from.  Choose a C# type without converters for it, or "
                + "remove the converters of 'MyApp.Address'.",
        ]);
    }

    // An array of a composite element that the override maps to a foreign type with converters names the element.
    [Fact]
    public void Plan_NoBaseTypeOfAnElementWithAConverter_ReportsSqlsrc279ForTheElement()
    {
        var type = Overridden(Email + "[]", false, "public.address", null, false);
        var converters = Of(Project("global::MyApp.Email.From", "string", false, Email, false));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'addresses'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC279: The element of column 'addresses' has type 'MyApp.Email', which has converters, but the "
                + "database type 'public.address' has no C# type to convert from.  Choose a C# type without converters "
                + "for it, or remove the converters of 'MyApp.Email'.",
        ]);
    }

    // A converter of a type to itself serves both directions.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Plan_OneTypeToItself_GivesItForBothDirections(bool isRead)
    {
        var type = Overridden(DateTime, true, "public.moment", DateTime, true);
        var converters = Of(Project("global::MyApp.Moments.AsUtc", DateTime, true, DateTime, true));

        var (conversion, problems, _) = Plan(type, isRead, converters);

        conversion.ShouldBe(new Conversion("global::MyApp.Moments.AsUtc", DateTime, true, false, null, false));
        problems.ShouldBeEmpty();
    }

    // An array whose element the override maps, with no converter of the array, is converted by its element's.
    [Theory]
    [InlineData(true, "global::MyApp.Email.From")]
    [InlineData(false, "global::MyApp.Email.To")]
    public void Plan_ArrayByElement_GivesAnElementConversion(bool isRead, string method)
    {
        var type = Overridden(Email + "[]", false, "public.email", "string[]", false);
        var converters = Of(
            Project("global::MyApp.Email.From", "string", false, Email, true),
            Project("global::MyApp.Email.To", Email, true, "string", false)
        );

        var (conversion, problems, blocked) = Plan(type, isRead, converters);

        conversion.ShouldBe(new Conversion(method, "string[]", false, true, Email, true));
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // The element of a value type: the database side of a conversion by element is the element's.
    [Fact]
    public void Plan_ArrayOfAValueTypeByElement_TakesTheElementsValueType()
    {
        var type = Overridden(UserId + "[]", false, "int8", "long[]", false, elementIsValueType: true);
        var converters = Of(Project("global::MyApp.Ids.ToUserId", "long", true, UserId, true));

        var (conversion, _, _) = Plan(type, isRead: true, converters);

        conversion.ShouldBe(new Conversion("global::MyApp.Ids.ToUserId", "long[]", true, true, UserId, true));
    }

    // The element of an array is decided as a type of its own, so a foreign element whose converters are of another
    // type is SQLSRC279, naming the element.
    [Fact]
    public void Plan_ArrayOfAForeignElementWithConvertersOfAnotherType_ReportsSqlsrc279()
    {
        var type = Overridden(UserId + "[]", false, "int8", "long[]", false, elementIsValueType: true);
        var converters = Of(Project("global::MyApp.Ids.ToUserId", "int", true, UserId, true));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'ids'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC279: The element of column 'ids' has type 'MyApp.UserId', whose converters convert from and to "
                + "'int', and not from and to 'long', the type the database type 'int8' is read as.  Add a converter "
                + "between 'MyApp.UserId' and 'long'.",
        ]);
    }

    // An element with the other direction's converter alone is SQLSRC280, naming the element.
    [Fact]
    public void Plan_ArrayOfAnElementWithOnlyTheWriteEdge_ReportsSqlsrc280ForTheElement()
    {
        var type = Overridden(Email + "[]", false, "public.email", "string[]", false);
        var converters = Of(Project("global::MyApp.Email.To", Email, true, "string", false));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'emails'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC280: The element of column 'emails' has type 'MyApp.Email', which has a converter to 'string' but "
                + "none from it, which a column needs.  Add a static method marked [SqlSourceConverter] that takes "
                + "'string' and returns 'MyApp.Email'.",
        ]);
    }

    // An element the override writes nullable, UserId? or Email?, is converted by the edges of the type without its ?,
    // and the conversion keeps the element as the property writes it.
    [Theory]
    [InlineData(UserId + "?", "long", true, true)]
    [InlineData(Email + "?", "string", false, false)]
    public void Plan_ArrayOfANullableElement_UsesTheEdgesOfTheElementWithoutItsQuestionMark(
        string element,
        string databaseElement,
        bool databaseIsValueType,
        bool elementIsValueType
    )
    {
        var type = Overridden(
            element + "[]",
            false,
            "public.ids[]",
            databaseElement + "[]",
            false,
            elementIsValueType: databaseIsValueType
        );
        var bare = element[..^1];
        var converters = Of(
            Project("global::MyApp.Ids.From", databaseElement, databaseIsValueType, bare, elementIsValueType),
            Project("global::MyApp.Ids.To", bare, elementIsValueType, databaseElement, databaseIsValueType)
        );

        foreach (var (isRead, method) in new[] { (true, "global::MyApp.Ids.From"), (false, "global::MyApp.Ids.To") })
        {
            var (conversion, problems, blocked) = Plan(type, isRead, converters);

            conversion.ShouldBe(
                new Conversion(method, databaseElement + "[]", databaseIsValueType, true, element, elementIsValueType)
            );
            conversion!.ElementIsNullable.ShouldBeTrue();
            problems.ShouldBeEmpty();
            blocked.ShouldBeFalse();
        }
    }

    // A nullable element is decided as the type without its ?, so converters of another type are SQLSRC279 for it,
    // and not a read as the driver reads it.
    [Fact]
    public void Plan_ArrayOfANullableElementWithConvertersOfAnotherType_ReportsSqlsrc279()
    {
        var type = Overridden(UserId + "?[]", false, "public.user_id[]", "long[]", false, elementIsValueType: true);
        var converters = Of(Project("global::MyApp.Ids.ToUserId", "int", true, UserId, true));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'ids'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC279: The element of column 'ids' has type 'MyApp.UserId', whose converters convert from and to "
                + "'int', and not from and to 'long', the type the database type 'public.user_id' is read as.  Add a "
                + "converter between 'MyApp.UserId' and 'long'.",
        ]);
    }

    // An override of the array to a type with its own converter takes that one, not the element's.
    [Fact]
    public void Plan_ArrayOverrideWithItsOwnEdge_DoesNotUseTheElements()
    {
        var type = Overridden(Email + "[]", false, "public.email[]", "string[]", false);
        var converters = Of(
            Project("global::MyApp.Email.From", "string", false, Email, false),
            Project("global::MyApp.Emails.FromArray", "string[]", false, Email + "[]", false)
        );

        var (conversion, problems, _) = Plan(type, isRead: true, converters);

        conversion.ShouldBe(new Conversion("global::MyApp.Emails.FromArray", "string[]", false, false, null, false));
        problems.ShouldBeEmpty();
    }

    // A converter of the array type itself, though not to the base map's array, keeps the elements' converters out:
    // the array is foreign with converters of another type.
    [Fact]
    public void Plan_ArrayWithAConverterOfItsOwnToAnotherType_ReportsSqlsrc279ForTheArray()
    {
        var type = Overridden(UserId + "[]", false, "int8[]", "long[]", false, elementIsValueType: true);
        var converters = Of(
            Project("global::MyApp.Ids.ToUserId", "long", true, UserId, true),
            Project("global::MyApp.Ids.FromUserId", UserId, true, "long", true),
            Project("global::MyApp.Ids.Join", UserId + "[]", false, "string", false)
        );

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'ids'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC279: The column 'ids' has type 'MyApp.UserId[]', whose converters convert from and to 'string', "
                + "and not from and to 'long[]', the type the database type 'int8[]' is read as.  Add a converter "
                + "between 'MyApp.UserId[]' and 'long[]'.",
        ]);
    }

    // SQLSRC278 at the methods fails the build already.
    [Fact]
    public void Plan_DoubledEdgeOfTheProject_IsBlockedSilently()
    {
        var converters = Of(
            Project("global::MyApp.Conversions.ToEmail", "string", false, Email, false),
            Project("global::MyApp.Email.From", "string", false, Email, false)
        );

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeTrue();
    }

    [Fact]
    public void Plan_DoubledEdgeOfAReference_ReportsSqlsrc278()
    {
        var converters = Of(
            Reference("global::MyApp.Email.From", "string", Email),
            Reference("global::MyApp.Email.Parse", "string", Email)
        );

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC278: The converters 'MyApp.Email.From(string)' and 'MyApp.Email.Parse(string)' both convert "
                + "'string' to 'MyApp.Email'.  Keep one.",
        ]);
    }

    // A faulty converter of a reference is SQLSRC277, and with it the edge has one converter, so no SQLSRC278.
    [Fact]
    public void Plan_EdgeOfAReferenceWithASoundAndAFaultyConverter_ReportsSqlsrc277Alone()
    {
        var converters = Of(
            Reference("global::MyApp.Email.From", "string", Email),
            Reference("global::MyApp.Email.Parse", "string", Email) with
            {
                Fault = "it is not static",
            }
        );

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC277: The method 'MyApp.Email.Parse(string)' is marked [SqlSourceConverter], but it is not static.  "
                + "A converter is a static method of one argument that returns the converted value.",
        ]);
    }

    // SQLSRC277 at the method fails the build already.
    [Fact]
    public void Plan_FaultOfTheProject_IsBlockedSilently()
    {
        var converters = Of(
            Project("global::MyApp.Email.From", "string", false, Email, false) with
            {
                Fault = "it is not static",
            }
        );

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeTrue();
    }

    [Fact]
    public void Plan_FaultOfAReference_ReportsSqlsrc277()
    {
        var converters = Of(Reference("global::MyApp.Email.From", "string", Email) with { Fault = "it is not static" });

        var (conversion, problems, blocked) = Plan(EmailType, isRead: true, converters);

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldBe([
            "SQLSRC277: The method 'MyApp.Email.From(string)' is marked [SqlSourceConverter], but it is not static.  A "
                + "converter is a static method of one argument that returns the converted value.",
        ]);
    }

    // SQL Server's map gives SqlVector<float>, so it is no foreign type there.
    [Fact]
    public void Plan_TypeOfTheSqlServerMapWithConvertersOfAnotherType_GivesNull()
    {
        const string Vector = "global::Microsoft.Data.SqlTypes.SqlVector<float>";
        var type = Overridden(Vector, true, "dbo.embedding", "string", false);
        var converters = Of(Project("global::MyApp.Vectors.ToArray", Vector, true, "float[]", false));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, engine: DatabaseEngine.SqlServer);

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // PostgreSQL's map gives DateOnly, as Npgsql 10 reads a date, and IPAddress, which SQL Server's never gives, so
    // neither is a foreign type there, even where the override chose it over a type Npgsql 8 reads.
    [Theory]
    [InlineData("global::System.DateOnly", true, "date", DateTime, true)]
    [InlineData("global::System.Net.IPAddress", false, "inet", "global::System.Net.IPAddress", false)]
    public void Plan_TypeOfThePostgresMapWithConvertersOfAnotherType_GivesNull(
        string text,
        bool isValueType,
        string key,
        string databaseText,
        bool databaseIsValueType
    )
    {
        var type = Overridden(text, isValueType, key, databaseText, databaseIsValueType);
        var converters = Of(
            Project("global::MyApp.Texts.From", "string", false, text, isValueType),
            Project("global::MyApp.Texts.To", text, isValueType, "string", false)
        );

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, engine: DatabaseEngine.PostgreSql);

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // NodaTime's edges serve SQL Server alone, so an Instant that an override chose on PostgreSQL goes to the driver.
    [Fact]
    public void Plan_SqlServerEdgeForAPostgresQuery_IsNotUsed()
    {
        var type = Overridden(Instant, true, "timestamptz", DateTime, true);
        var converters = Of(Edge(DateTime, Instant), Edge(Instant, DateTime));

        foreach (var isRead in new[] { true, false })
        {
            var (conversion, problems, blocked) = Plan(type, isRead, converters, engine: DatabaseEngine.PostgreSql);

            conversion.ShouldBeNull();
            problems.ShouldBeEmpty();
            blocked.ShouldBeFalse();
        }
    }

    [Fact]
    public void Plan_LibraryEdgeOfTheTarget_IsUsed()
    {
        var type = OfTheLibrary(LocalDate, "date", DateTime);
        var edge = Edge(DateTime, LocalDate);

        var (conversion, problems, blocked) = Plan(
            type,
            isRead: true,
            Of(edge),
            "column 'day'",
            DatabaseEngine.SqlServer
        );

        conversion.ShouldBe(
            new Conversion(edge.Method, DateTime, true, false, null, false, Library: SqlLibrary.NodaTime)
        );
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A project's converter between Instant and another type does not make every timestamptz the plugin reads an error.
    [Fact]
    public void Plan_LibraryRowWithForeignConverters_GoesToTheDriver()
    {
        var type = OfTheLibrary(Instant, "timestamptz", DateTime);
        var converters = Of(
            Project("global::MyApp.Moments.ToOffset", Instant, true, DateTimeOffset, true),
            Project("global::MyApp.Moments.FromOffset", DateTimeOffset, true, Instant, true)
        );

        foreach (var isRead in new[] { true, false })
        {
            var (conversion, problems, blocked) = Plan(type, isRead, converters, "column 'at'");

            conversion.ShouldBeNull();
            problems.ShouldBeEmpty();
            blocked.ShouldBeFalse();
        }
    }

    [Fact]
    public void Plan_LibraryRowWithOneDirection_ReadsByItAndWritesByTheDriver()
    {
        var type = OfTheLibrary(Instant, "timestamptz", DateTime);
        var converters = Of(Project("global::MyApp.Moments.ToInstant", DateTime, true, Instant, true));

        var (read, readProblems, readBlocked) = Plan(type, isRead: true, converters, "column 'at'");
        var (write, writeProblems, writeBlocked) = Plan(type, isRead: false, converters, "parameter 'at'");

        read.ShouldBe(new Conversion("global::MyApp.Moments.ToInstant", DateTime, true, false, null, false));
        readProblems.ShouldBeEmpty();
        readBlocked.ShouldBeFalse();
        write.ShouldBeNull();
        writeProblems.ShouldBeEmpty();
        writeBlocked.ShouldBeFalse();
    }

    // An array of a library's type is converted by its element's edge, and the other direction goes to the driver.
    [Fact]
    public void Plan_LibraryRowArrayWithOneDirection_ReadsByElementAndWritesByTheDriver()
    {
        var type = OfTheLibrary(Instant + "[]", "timestamptz[]", DateTime + "[]", isValueType: false) with
        {
            Database = new DatabaseType(DateTime + "[]", false, true),
        };
        var converters = Of(Project("global::MyApp.Moments.ToInstant", DateTime, true, Instant, true));

        var (read, readProblems, _) = Plan(type, isRead: true, converters, "column 'ats'");
        var (write, writeProblems, writeBlocked) = Plan(type, isRead: false, converters, "parameter 'ats'");

        read.ShouldBe(new Conversion("global::MyApp.Moments.ToInstant", DateTime + "[]", true, true, Instant, true));
        readProblems.ShouldBeEmpty();
        write.ShouldBeNull();
        writeProblems.ShouldBeEmpty();
        writeBlocked.ShouldBeFalse();
    }

    // A library row whose database type the base map gives no C# type is the driver's too.
    [Fact]
    public void Plan_LibraryRowWithoutABaseType_GoesToTheDriver()
    {
        var type = OfTheLibrary(Instant, "timestamptz", null);
        var converters = Of(Project("global::MyApp.Moments.ToOffset", Instant, true, DateTimeOffset, true));

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'at'");

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A type an override chose keeps the rule that a library row is exempt from.
    [Fact]
    public void Plan_OverrideWithForeignConverters_StillReports279()
    {
        var type = Overridden(Instant, true, "timestamptz", DateTime, true);
        var converters = Of(
            Project("global::MyApp.Moments.ToOffset", Instant, true, DateTimeOffset, true),
            Project("global::MyApp.Moments.FromOffset", DateTimeOffset, true, Instant, true)
        );

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'at'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldHaveSingleItem().ShouldStartWith("SQLSRC279: The column 'at' has type 'NodaTime.Instant'");
    }

    // A class on a type that holds JSON is read and sent through the serializer, whose read of a reference type can
    // give null for a JSON null.
    [Theory]
    [InlineData(true, Deserialize + OrderMetadata + ">")]
    [InlineData(false, Serialize + OrderMetadata + ">")]
    public void Plan_ClassOnJson_GoesThroughTheSerializer(bool isRead, string method)
    {
        var type = HoldingJson(OrderMetadata, false, "jsonb", "string");

        var (conversion, problems, blocked) = Plan(type, isRead, Of<Converter>());

        conversion.ShouldBe(
            new Conversion(method, "string", false, false, null, false, MayReturnNull: isRead, IsSerializer: true)
        );
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A value type is read as the nullable value type, whose read gives null for a JSON null where its own throws, and
    // so can return null; a nullable one is written with one question mark.
    [Theory]
    [InlineData(Point)]
    [InlineData(Point + "?")]
    public void Plan_ValueTypeOnJson_IsReadAsItsNullable(string text)
    {
        var type = HoldingJson(text, true, "json", "string");

        var (conversion, _, _) = Plan(type, isRead: true, Of<Converter>(), engine: DatabaseEngine.SqlServer);

        conversion.ShouldBe(
            new Conversion(
                Deserialize + Point + "?>",
                "string",
                false,
                false,
                null,
                false,
                MayReturnNull: true,
                IsSerializer: true
            )
        );
    }

    // An array of a class over an array of a type that holds JSON is serialized element by element, the element
    // without its question mark; a reference element's read can give null.
    [Theory]
    [InlineData(true, OrderMetadata, false, Deserialize + OrderMetadata + ">")]
    [InlineData(false, OrderMetadata, false, Serialize + OrderMetadata + ">")]
    [InlineData(true, OrderMetadata + "?", false, Deserialize + OrderMetadata + ">")]
    [InlineData(true, Point, true, Deserialize + Point + "?>")]
    [InlineData(true, Point + "?", true, Deserialize + Point + "?>")]
    [InlineData(false, Point + "?", true, Serialize + Point + ">")]
    public void Plan_ClassArrayOnJsonArray_GoesThroughTheSerializerByElement(
        bool isRead,
        string element,
        bool elementIsValueType,
        string method
    )
    {
        var type = HoldingJson(element + "[]", false, "jsonb[]", "string[]") with
        {
            ElementIsValueType = elementIsValueType,
        };

        var (conversion, problems, blocked) = Plan(type, isRead, Of<Converter>());

        conversion.ShouldBe(
            new Conversion(
                method,
                "string[]",
                false,
                true,
                element,
                elementIsValueType,
                MayReturnNull: isRead,
                IsSerializer: true
            )
        );
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // An array of a class over a type that holds JSON whole is a class itself, serialized whole.
    [Fact]
    public void Plan_ClassArrayOnJson_GoesThroughTheSerializerWhole()
    {
        var type = HoldingJson(OrderMetadata + "[]", false, "jsonb", "string");

        var (conversion, _, _) = Plan(type, isRead: true, Of<Converter>());

        conversion.ShouldBe(
            new Conversion(
                Deserialize + OrderMetadata + "[]>",
                "string",
                false,
                false,
                null,
                false,
                MayReturnNull: true,
                IsSerializer: true
            )
        );
    }

    // A class on a string that holds no JSON goes to the driver, as 8.2 has it.
    [Fact]
    public void Plan_ClassOnAStringThatHoldsNoJson_GoesToTheDriver()
    {
        var type = Overridden(OrderMetadata, false, "text", "string", false);

        var (conversion, problems, blocked) = Plan(type, isRead: true, Of<Converter>());

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A type of System.Text.Json, or one the base map gives, is no class.
    [Theory]
    [InlineData("string", false)]
    [InlineData("long", true)]
    [InlineData("global::System.Text.Json.JsonElement", true)]
    [InlineData("global::System.Text.Json.Nodes.JsonNode", false)]
    public void Plan_JsonTypeOrBaseTypeOnJson_IsNotSerialized(string text, bool isValueType)
    {
        var type = HoldingJson(text, isValueType, "jsonb", "string");

        var (conversion, problems, blocked) = Plan(type, isRead: true, Of<Converter>());

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A class whose converters are of another type keeps 8.2's rule, SQLSRC279, with no serializer.
    [Fact]
    public void Plan_ClassOnJsonWithConvertersOfAnotherType_ReportsSqlsrc279()
    {
        var type = HoldingJson(OrderMetadata, false, "jsonb", "string");
        var converters = Of(
            Project("global::MyApp.OrderMetadata.FromBytes", "byte[]", false, OrderMetadata, false),
            Project("global::MyApp.OrderMetadata.ToBytes", OrderMetadata, false, "byte[]", false)
        );

        var (conversion, problems, blocked) = Plan(type, isRead: true, converters, "column 'meta'");

        conversion.ShouldBeNull();
        blocked.ShouldBeTrue();
        problems.ShouldHaveSingleItem().ShouldStartWith("SQLSRC279: The column 'meta' has type 'MyApp.OrderMetadata'");
    }

    // A converter of the edge replaces the serializer.
    [Fact]
    public void Plan_ClassOnJsonWithItsEdge_UsesTheConverter()
    {
        var type = HoldingJson(OrderMetadata, false, "jsonb", "string");
        var converters = Of(Project("global::MyApp.OrderMetadata.FromJson", "string", false, OrderMetadata, false));

        var (conversion, _, _) = Plan(type, isRead: true, converters);

        conversion.ShouldBe(
            new Conversion("global::MyApp.OrderMetadata.FromJson", "string", false, false, null, false)
        );
    }

    // SQL Server's geography, read as Geometry by NetTopologySuite's row, takes the spatial read path: the shared
    // reader of the generated class for a column, its writer for a parameter.
    [Theory]
    [InlineData(true, "GeographyReader")]
    [InlineData(false, "GeographyWriter")]
    public void Plan_GeographyAsGeometry_IsTheSpatialPath(bool isRead, string instance)
    {
        var (conversion, problems, blocked) = PlanSpatial(OfNetTopologySuite(Geometry, "geography"), isRead);

        conversion.ShouldBe(Spatial(instance));
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // A subtype that an override chose takes the path too, whatever chose it, and its key picks the instance.
    [Fact]
    public void Plan_GeometryAsPointByOverride_IsTheSpatialPath()
    {
        var type = Overridden(GeometryPoint, false, "geometry", null, false);

        var (conversion, problems, blocked) = PlanSpatial(type, isRead: true);

        conversion.ShouldBe(Spatial("GeometryReader"));
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // Without NetTopologySuite.IO.SqlServerBytes the path is not taken: the type goes to the driver, as before.
    [Fact]
    public void Plan_SpatialWithoutSqlServerBytes_IsTheDriver()
    {
        foreach (var isRead in new[] { true, false })
        {
            var (conversion, problems, blocked) = PlanSpatial(
                OfNetTopologySuite(Geometry, "geography"),
                isRead,
                sqlServerSpatial: false
            );

            conversion.ShouldBeNull();
            problems.ShouldBeEmpty();
            blocked.ShouldBeFalse();
        }
    }

    // An override to Microsoft.SqlServer.Types's type is no geometry of NetTopologySuite's, and is read and bound as
    // the driver does, as before.
    [Fact]
    public void Plan_OverrideToSqlGeography_IsUnchanged()
    {
        var type = Overridden("global::Microsoft.SqlServer.Types.SqlGeography", false, "geography", null, false);

        foreach (var isRead in new[] { true, false })
        {
            var (conversion, problems, blocked) = PlanSpatial(type, isRead);

            conversion.ShouldBeNull();
            problems.ShouldBeEmpty();
            blocked.ShouldBeFalse();
        }
    }

    // A project's converters of Geometry neither replace the path nor make an error of it, though an override chose
    // the type, which would otherwise be SQLSRC279 for a foreign type whose database type has no base type.
    [Fact]
    public void Plan_ProjectConverterOfGeometry_DoesNotReplaceTheSpatialPath()
    {
        var type = Overridden(Geometry, false, "geography", null, false);
        var converters = Of(
            Project("global::MyApp.Shapes.FromBytes", "byte[]", false, Geometry, false),
            Project("global::MyApp.Shapes.ToBytes", Geometry, false, "byte[]", false)
        );

        var (read, readProblems, readBlocked) = PlanSpatial(type, isRead: true, converters);
        var (write, writeProblems, writeBlocked) = PlanSpatial(type, isRead: false, converters);

        read.ShouldBe(Spatial("GeographyReader"));
        write.ShouldBe(Spatial("GeographyWriter"));
        readProblems.ShouldBeEmpty();
        writeProblems.ShouldBeEmpty();
        readBlocked.ShouldBeFalse();
        writeBlocked.ShouldBeFalse();
    }

    // The path is only for geography and geometry: a varbinary that an override reads as Geometry is converted by the
    // project's converters, as any other type of the project's choice is.
    [Fact]
    public void Plan_GeometryOverVarbinary_IsTheProjectsConverter()
    {
        var type = Overridden(Geometry, false, "varbinary", "byte[]", false);
        var converters = Of(
            Project("global::MyApp.Shapes.FromBytes", "byte[]", false, Geometry, false),
            Project("global::MyApp.Shapes.ToBytes", Geometry, false, "byte[]", false)
        );

        var (read, readProblems, readBlocked) = PlanSpatial(type, isRead: true, converters);
        var (write, writeProblems, writeBlocked) = PlanSpatial(type, isRead: false, converters);

        read.ShouldBe(new Conversion("global::MyApp.Shapes.FromBytes", "byte[]", false, false, null, false));
        write.ShouldBe(new Conversion("global::MyApp.Shapes.ToBytes", "byte[]", false, false, null, false));
        read!.IsSpatial.ShouldBeFalse();
        write!.IsSpatial.ShouldBeFalse();
        readProblems.ShouldBeEmpty();
        writeProblems.ShouldBeEmpty();
        readBlocked.ShouldBeFalse();
        writeBlocked.ShouldBeFalse();
    }

    // PostGIS's geometry is read by the plugin, on PostgreSQL, whatever the project references.
    [Fact]
    public void Plan_PostgresGeometry_IsTheDriver()
    {
        var problems = new List<DiagnosticInfo>();
        var context = new ConversionContext(
            DatabaseEngine.PostgreSql,
            ConverterTargetsExtensions.Of(DatabaseEngine.PostgreSql, true),
            Of<Converter>(),
            At,
            problems,
            SqlServerSpatial: true
        );

        var conversion = ConversionPlanner.Plan(
            OfNetTopologySuite(Geometry, "geometry"),
            true,
            "column 'shape'",
            context,
            out var blocked
        );

        conversion.ShouldBeNull();
        problems.ShouldBeEmpty();
        blocked.ShouldBeFalse();
    }

    // Planned under mssql, with the converters, none by default, and NetTopologySuite.IO.SqlServerBytes referenced
    // unless it says otherwise.
    private static (Conversion? Conversion, List<string> Problems, bool Blocked) PlanSpatial(
        PropertyType type,
        bool isRead,
        EquatableArray<Converter> converters = default,
        bool sqlServerSpatial = true
    ) => Plan(type, isRead, converters, "column 'place'", DatabaseEngine.SqlServer, sqlServerSpatial);

    // The spatial path through the instance of the generated class.
    private static Conversion Spatial(string instance) =>
        new(
            "global::SqlSource.NetTopologySuiteConversions." + instance,
            "global::System.Data.SqlTypes.SqlBytes",
            false,
            false,
            null,
            false,
            Library: SqlLibrary.NetTopologySuite,
            IsSpatial: true
        );

    // A type that NetTopologySuite's row gave, whose database type the base map gives no C# type.
    private static PropertyType OfNetTopologySuite(string text, string key) =>
        new(
            text,
            false,
            true,
            EquatableArray<string>.Empty,
            key,
            "the NetTopologySuite library",
            new DatabaseType(null, false, false),
            Library: SqlLibrary.NetTopologySuite
        );

    private static (Conversion? Conversion, List<string> Problems, bool Blocked) Plan(
        PropertyType type,
        bool isRead,
        EquatableArray<Converter> converters,
        string subject = "column 'email'",
        DatabaseEngine engine = DatabaseEngine.PostgreSql,
        bool sqlServerSpatial = false
    )
    {
        var problems = new List<DiagnosticInfo>();
        var conversion = ConversionPlanner.Plan(
            type,
            isRead,
            subject,
            new ConversionContext(
                engine,
                ConverterTargetsExtensions.Of(engine, true),
                converters,
                At,
                problems,
                sqlServerSpatial
            ),
            out var blocked
        );
        problems.ShouldAllBe(problem => problem.Location == At);
        return (
            conversion,
            [
                .. problems.Select(problem =>
                    problem.Descriptor.Id + ": " + problem.ToDiagnostic().GetMessage(CultureInfo.InvariantCulture)
                ),
            ],
            blocked
        );
    }

    // A type that an override chose, with the base map's type of its database type.
    private static PropertyType Overridden(
        string text,
        bool isValueType,
        string key,
        string? databaseText,
        bool databaseIsValueType,
        bool elementIsValueType = false
    ) =>
        new(
            text,
            isValueType,
            true,
            EquatableArray<string>.Empty,
            key,
            Place,
            new DatabaseType(databaseText, databaseIsValueType, elementIsValueType)
        );

    // A type that a marker chose, over a database type that holds JSON.
    private static PropertyType HoldingJson(string text, bool isValueType, string key, string databaseText) =>
        Overridden(text, isValueType, key, databaseText, false) with
        {
            Database = new DatabaseType(databaseText, false, false, HoldsJson: true),
        };

    // A type that NodaTime's row gave, with the base map's type of its database type.
    private static PropertyType OfTheLibrary(string text, string key, string? databaseText, bool isValueType = true) =>
        new(
            text,
            isValueType,
            true,
            EquatableArray<string>.Empty,
            key,
            LibraryPlace,
            new DatabaseType(databaseText, databaseText is not null, false),
            Library: SqlLibrary.NodaTime
        );

    // NodaTime's edge from the one type to the other.
    private static Converter Edge(string argument, string returns) =>
        NodaTimeConversions.Edges.Single(edge => edge.ArgumentText == argument && edge.ReturnText == returns);

    private static Converter Project(
        string method,
        string argument,
        bool argumentIsValueType,
        string returns,
        bool returnIsValueType
    ) =>
        new(
            method,
            DisplayOf(method, argument),
            argument,
            argumentIsValueType,
            returns,
            returnIsValueType,
            EquatableArray<string>.Empty,
            null,
            ConverterOrigin.Project,
            null
        );

    private static Converter Reference(string method, string argument, string returns) =>
        Project(method, argument, false, returns, false) with
        {
            Origin = ConverterOrigin.MappedType,
        };

    // MyApp.Email.From(string), as a message names a method.
    private static string DisplayOf(string method, string argument) =>
        ClrTypeName.AsWritten(method) + "(" + ClrTypeName.AsWritten(argument) + ")";

    // The converters in the order of the set.
    private static EquatableArray<Converter> Of(params Converter[] converters) =>
        TestSidecars.Of(converters.OrderBy(converter => converter, ConverterSet.Comparer).ToArray());

    private static EquatableArray<T> Of<T>()
        where T : System.IEquatable<T> => EquatableArray<T>.Empty;
}
