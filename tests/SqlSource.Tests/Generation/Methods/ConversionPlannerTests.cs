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
    private const string Place = "the typeOverrides of the settings file /app/sqlsource.json";

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
    public void Plan_ReadWithOnlyTheWriteEdge_ReportsSQLSRC280()
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
    public void Plan_WriteWithOnlyTheReadEdge_ReportsSQLSRC280()
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

    // The spec's case: an int8 column mapped to UserId, whose converters are of int.
    [Fact]
    public void Plan_LongColumnWithAnIntConverter_ReportsSQLSRC279()
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
    public void Plan_NoBaseTypeWithAConverter_ReportsSQLSRC279()
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
    public void Plan_NoBaseTypeOfAnElementWithAConverter_ReportsSQLSRC279ForTheElement()
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
    public void Plan_ArrayOfAForeignElementWithConvertersOfAnotherType_ReportsSQLSRC279()
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
    public void Plan_ArrayOfAnElementWithOnlyTheWriteEdge_ReportsSQLSRC280ForTheElement()
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
    public void Plan_ArrayOfANullableElementWithConvertersOfAnotherType_ReportsSQLSRC279()
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
    public void Plan_ArrayWithAConverterOfItsOwnToAnotherType_ReportsSQLSRC279ForTheArray()
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
    public void Plan_DoubledEdgeOfAReference_ReportsSQLSRC278()
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
    public void Plan_EdgeOfAReferenceWithASoundAndAFaultyConverter_ReportsSQLSRC277Alone()
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
    public void Plan_FaultOfAReference_ReportsSQLSRC277()
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

    private static (Conversion? Conversion, List<string> Problems, bool Blocked) Plan(
        PropertyType type,
        bool isRead,
        EquatableArray<Converter> converters,
        string subject = "column 'email'",
        DatabaseEngine engine = DatabaseEngine.PostgreSql
    )
    {
        var problems = new List<DiagnosticInfo>();
        var conversion = ConversionPlanner.Plan(
            type,
            isRead,
            engine,
            converters,
            subject,
            At,
            problems,
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
        PostgresTypeMap.AsWritten(method) + "(" + PostgresTypeMap.AsWritten(argument) + ")";

    // The converters in the order of the set.
    private static EquatableArray<Converter> Of(params Converter[] converters) =>
        TestModels.Array(converters.OrderBy(converter => converter, ConverterSet.Comparer).ToArray());

    private static EquatableArray<T> Of<T>()
        where T : System.IEquatable<T> => EquatableArray<T>.Empty;
}
