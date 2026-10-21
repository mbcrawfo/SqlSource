using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using NpgsqlTypes;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The project runs the generator when it builds: the attribute it declares is in the assembly, and so is each model of
// the queries of Queries/, with the name, the shape and the properties that its settings give it.  The tests name each
// model with typeof, so a model that is not generated fails the build, and read its shape by reflection.  No server is
// needed.
public sealed class ModelShapeTests
{
    private const string Namespace = "SqlSource.Tests.Postgres.EndToEnd";

    private enum Shape
    {
        Record,
        SealedRecord,
        Class,
        SealedClass,
    }

    // The columns of the table accounts, by the project's type override for timestamptz.
    private static readonly (string Name, Type Type, bool Nullable)[] AccountsProperties =
    [
        ("Id", typeof(int), false),
        ("Name", typeof(string), false),
        ("Email", typeof(string), false),
        ("Status", typeof(string), false),
        ("CreatedAt", typeof(DateTimeOffset), false),
        ("ClosedAt", typeof(DateTimeOffset?), true),
        ("NameLength", typeof(int?), true),
    ];

    [Fact]
    public void Build_Project_DeclaresTheGeneratorsAttribute() =>
        _ = typeof(ModelShapeTests).Assembly.GetType("SqlSource.SqlSourceGenerateAttribute").ShouldNotBeNull();

    [Fact]
    public void Build_PropertySettings_GiveARecordInputAndASealedRecordOutput()
    {
        ShouldBeModel(
            typeof(GetAccountArgs),
            $"{Namespace}.GetAccountArgs",
            Shape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(GetAccountDto),
            $"{Namespace}.GetAccountDto",
            Shape.SealedRecord,
            ("Name", typeof(string), false),
            ("Id", typeof(int), false),
            ("Email", typeof(string), false)
        );
    }

    [Fact]
    public void Build_WholeTable_IsTheTableModel()
    {
        ShouldBeModel(typeof(AccountsDto), $"{Namespace}.AccountsDto", Shape.SealedRecord, AccountsProperties);
        ModelNamed($"{Namespace}.ListAccountsDto").ShouldBeNull();
        ModelNamed($"{Namespace}.ListAccountsInTableOrderDto").ShouldBeNull();
    }

    [Fact]
    public void Build_NoTableModels_NamesTheModelAfterTheQuery() =>
        ShouldBeModel(
            typeof(ListAccountsUntabledDto),
            $"{Namespace}.ListAccountsUntabledDto",
            Shape.SealedRecord,
            AccountsProperties
        );

    [Fact]
    public void Build_OutputModelMarker_SharesOneModel()
    {
        ShouldBeModel(
            typeof(AccountName),
            $"{Namespace}.AccountName",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false)
        );
        ShouldBeModel(
            typeof(GetAccountNameArgs),
            $"{Namespace}.GetAccountNameArgs",
            Shape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(FindAccountNameArgs),
            $"{Namespace}.FindAccountNameArgs",
            Shape.Record,
            ("Email", typeof(string), false)
        );
        ModelNamed($"{Namespace}.GetAccountNameDto").ShouldBeNull();
        ModelNamed($"{Namespace}.FindAccountNameDto").ShouldBeNull();
    }

    [Fact]
    public void Build_ParamMarker_MakesTheParameterNullable() =>
        ShouldBeModel(
            typeof(ListClosedBeforeArgs),
            $"{Namespace}.ListClosedBeforeArgs",
            Shape.Record,
            ("ClosedBefore", typeof(DateTimeOffset?), true)
        );

    [Fact]
    public void Build_PropertyOverride_MakesTimestamptzADateTimeOffset() =>
        ShouldBeModel(
            typeof(ListClosedBeforeDto),
            $"{Namespace}.ListClosedBeforeDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("ClosedAt", typeof(DateTimeOffset?), true)
        );

    [Fact]
    public void Build_SecondNamespace_GetsItsOwnModels()
    {
        const string ReportingNamespace = $"{Namespace}.Reporting";
        ShouldBeModel(
            typeof(Reporting.GetAccountArgs),
            $"{ReportingNamespace}.GetAccountArgs",
            Shape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(Reporting.GetAccountDto),
            $"{ReportingNamespace}.GetAccountDto",
            Shape.SealedRecord,
            ("Name", typeof(string), false),
            ("Id", typeof(int), false),
            ("Email", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Reporting.AccountsDto),
            $"{ReportingNamespace}.AccountsDto",
            Shape.SealedRecord,
            AccountsProperties
        );
        ShouldBeModel(
            typeof(Reporting.ListAccountsUntabledDto),
            $"{ReportingNamespace}.ListAccountsUntabledDto",
            Shape.SealedRecord,
            AccountsProperties
        );
        ShouldBeModel(
            typeof(Reporting.AccountName),
            $"{ReportingNamespace}.AccountName",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Reporting.GetAccountNameArgs),
            $"{ReportingNamespace}.GetAccountNameArgs",
            Shape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(Reporting.FindAccountNameArgs),
            $"{ReportingNamespace}.FindAccountNameArgs",
            Shape.Record,
            ("Email", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Reporting.ListClosedBeforeArgs),
            $"{ReportingNamespace}.ListClosedBeforeArgs",
            Shape.Record,
            ("ClosedBefore", typeof(DateTimeOffset?), true)
        );
        ShouldBeModel(
            typeof(Reporting.ListClosedBeforeDto),
            $"{ReportingNamespace}.ListClosedBeforeDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("ClosedAt", typeof(DateTimeOffset?), true)
        );
        ModelNamed($"{ReportingNamespace}.ListAccountsDto").ShouldBeNull();
        ModelNamed($"{ReportingNamespace}.ListAccountsInTableOrderDto").ShouldBeNull();
        Reporting.AccountReports.GetAccount.ShouldBe("SELECT name, id, email\nFROM accounts\nWHERE id = @accountId;");
    }

    [Fact]
    public void Build_LeftJoin_MakesTheOptionalSideNullable() =>
        ShouldBeModel(
            typeof(ListAccountNotesDto),
            $"{Namespace}.ListAccountNotesDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("NoteId", typeof(int?), true),
            ("Kind", typeof(NoteKind?), true),
            ("Body", typeof(string), true)
        );

    [Fact]
    public void Build_ColumnMarker_MakesTheCountNotNull() =>
        ShouldBeModel(
            typeof(CountAccountNotesDto),
            $"{Namespace}.CountAccountNotesDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("NoteCount", typeof(long), false)
        );

    [Fact]
    public void Build_SuffixMarks_SetTheNullability() =>
        ShouldBeModel(
            typeof(ListMarkedNamesDto),
            $"{Namespace}.ListMarkedNamesDto",
            Shape.SealedRecord,
            ("Id", typeof(int?), true),
            ("InitcapName", typeof(string), false)
        );

    [Fact]
    public void Build_HeaderOverrides_MapTheEnumAndTheDomainToTypesOfTheProject() =>
        ShouldBeModel(
            typeof(ListNoteKindsDto),
            $"{Namespace}.ListNoteKindsDto",
            Shape.SealedRecord,
            ("Kind", typeof(NoteKind), false),
            ("Email", typeof(Email), false)
        );

    [Fact]
    public void Build_SortInput_OrdersTheInputByName()
    {
        ShouldBeModel(
            typeof(Shapes.SearchAccountsFilter),
            $"{Namespace}.Shapes.SearchAccountsFilter",
            Shape.SealedClass,
            ("AccountId", typeof(int), false),
            ("Name", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Shapes.SearchAccountsView),
            $"{Namespace}.Shapes.SearchAccountsView",
            Shape.Record,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false)
        );
    }

    [Fact]
    public void Build_SortOutput_OrdersTheOutputByName() =>
        ShouldBeModel(
            typeof(Shapes.ListAccountColumnsView),
            $"{Namespace}.Shapes.ListAccountColumnsView",
            Shape.Class,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("Status", typeof(string), false)
        );

    [Fact]
    public void Build_OutputModelMarkerWithoutAPeriod_IsInTheModelNamespace()
    {
        ShouldBeModel(
            typeof(Shapes.AccountHeadline),
            $"{Namespace}.Shapes.AccountHeadline",
            Shape.Record,
            ("Id", typeof(int), false),
            ("Headline", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Shapes.GetHeadlineFilter),
            $"{Namespace}.Shapes.GetHeadlineFilter",
            Shape.SealedClass,
            ("AccountId", typeof(int), false)
        );
    }

    [Fact]
    public void Build_InputModelMarkerWithAPeriod_IsAFullName()
    {
        ShouldBeModel(
            typeof(Shared.AccountLookup),
            $"{Namespace}.Shared.AccountLookup",
            Shape.Class,
            ("Email", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Shapes.LookUpAccountView),
            $"{Namespace}.Shapes.LookUpAccountView",
            Shape.Record,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("Status", typeof(string), false)
        );
        ModelNamed($"{Namespace}.Shapes.AccountLookup").ShouldBeNull();
    }

    [Fact]
    public void Build_ConstantQueriesAtSql_HasTheConstantAndNoSidecar()
    {
        ConstantQueries.CountAccounts.ShouldBe("SELECT count(*) FROM accounts;");
        File.Exists(Path.Combine(RepositoryPaths.EndToEnd, "Queries", "Constants.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(RepositoryPaths.EndToEnd, "Queries", "Constants.sql.json")).ShouldBeFalse();
        ModelNamed($"{Namespace}.CountAccountsDto").ShouldBeNull();
    }

    [Fact]
    public void Build_TableOfAnotherSchema_IsNamedWithItsSchema() =>
        ShouldBeModel(
            typeof(Billing.BillingInvoicesRow),
            $"{Namespace}.Billing.BillingInvoicesRow",
            Shape.SealedClass,
            ("Id", typeof(int), false),
            ("AccountId", typeof(int), false),
            ("Number", typeof(string), false),
            ("Amount", typeof(decimal), false),
            ("IssuedOn", typeof(DateOnly), false),
            ("PaidAt", typeof(DateTime?), true)
        );

    [Fact]
    public void Build_InvoiceQueriesWithADirectLocation_HasTheConstantsOnTheType()
    {
        InvoiceQueries.ListInvoices.ShouldBe("SELECT *\nFROM billing.invoices\nORDER BY id;");
        InvoiceQueries.GetInvoice.ShouldBe(
            "SELECT id, number, amount, paid_at\nFROM billing.invoices\nWHERE id = @invoiceId;"
        );
    }

    [Fact]
    public void Build_OutputTypeByMetadata_IsASealedClass()
    {
        ShouldBeModel(
            typeof(Billing.GetInvoiceArgs),
            $"{Namespace}.Billing.GetInvoiceArgs",
            Shape.Record,
            ("InvoiceId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(Billing.GetInvoiceRow),
            $"{Namespace}.Billing.GetInvoiceRow",
            Shape.SealedClass,
            ("Id", typeof(int), false),
            ("Number", typeof(string), false),
            ("Amount", typeof(decimal), false),
            ("PaidAt", typeof(DateTime?), true)
        );
    }

    [Fact]
    public void Build_FolderOverride_MakesTimestamptzADateTime() =>
        PropertiesOf(typeof(Billing.GetInvoiceRow)).ShouldContain(("PaidAt", typeof(DateTime?), true));

    [Fact]
    public void Build_QueryOverride_ReturnsTimestamptzToDateTime() =>
        PropertiesOf(typeof(AllTypesDto)).ShouldContain(("TimestamptzValue", typeof(DateTime?), true));

    [Fact]
    public void Build_AllTypes_HasNpgsql10sTypes()
    {
        var properties = PropertiesOf(typeof(AllTypesDto));

        properties.ShouldContain(("DateValue", typeof(DateOnly?), true));
        properties.ShouldContain(("TimeValue", typeof(TimeOnly?), true));
        properties.ShouldContain(("CidrValue", typeof(IPNetwork?), true));
        properties.ShouldContain(("DaterangeValue", typeof(NpgsqlRange<DateOnly>?), true));
        properties.ShouldContain(("DatemultirangeValue", typeof(NpgsqlRange<DateOnly>[]), true));
    }

    [Fact]
    public void Build_InsertReturningEveryColumn_SharesTheTableModel()
    {
        ModelNamed($"{Namespace}.InsertAllTypesDto").ShouldBeNull();
        ShapeOf(typeof(InsertAllTypesArgs)).ShouldBe(Shape.Record);

        // A parameter for each column that the query writes, in its order, with the column's type, and none can be
        // null, since no -- param: marker says null.  It writes every column but those whose parameter Dapper cannot
        // send.  A parameter of bit(1) has no length, so it is a BitArray where the column is a bool.
        var columns = PropertiesOf(typeof(AllTypesDto));
        var expected = columns
            .Where(static column => !AllTypesQueryTests.WriteSideLeftOut.ContainsKey(column.Name))
            .Select(column =>
                (
                    column.Name,
                    column.Name == "Bit1Value"
                        ? typeof(BitArray)
                        : Nullable.GetUnderlyingType(column.Type) ?? column.Type,
                    false
                )
            );
        PropertiesOf(typeof(InsertAllTypesArgs)).ShouldBe(expected);
        PropertiesOf(typeof(InsertAllTypesArgs)).Length.ShouldBe(39);
    }

    [Fact]
    public void Build_AllTypes_IsASealedRecordOfEveryColumn() =>
        ShouldBeModel(
            typeof(AllTypesDto),
            $"{Namespace}.AllTypesDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("BoolValue", typeof(bool?), true),
            ("Int2Value", typeof(short?), true),
            ("Int4Value", typeof(int?), true),
            ("Int8Value", typeof(long?), true),
            ("Float4Value", typeof(float?), true),
            ("Float8Value", typeof(double?), true),
            ("NumericValue", typeof(decimal?), true),
            ("MoneyValue", typeof(decimal?), true),
            ("TextValue", typeof(string), true),
            ("VarcharValue", typeof(string), true),
            ("BpcharValue", typeof(string), true),
            ("NameValue", typeof(string), true),
            ("CharValue", typeof(char?), true),
            ("CitextValue", typeof(string), true),
            ("ByteaValue", typeof(byte[]), true),
            ("DateValue", typeof(DateOnly?), true),
            ("TimeValue", typeof(TimeOnly?), true),
            ("TimetzValue", typeof(DateTimeOffset?), true),
            ("TimestampValue", typeof(DateTime?), true),
            ("TimestamptzValue", typeof(DateTime?), true),
            ("IntervalValue", typeof(TimeSpan?), true),
            ("UuidValue", typeof(Guid?), true),
            ("JsonValue", typeof(string), true),
            ("JsonbValue", typeof(string), true),
            ("JsonpathValue", typeof(string), true),
            ("XmlValue", typeof(string), true),
            ("InetValue", typeof(IPAddress), true),
            ("CidrValue", typeof(IPNetwork?), true),
            ("MacaddrValue", typeof(PhysicalAddress), true),
            ("Macaddr8Value", typeof(PhysicalAddress), true),
            ("Bit1Value", typeof(bool?), true),
            ("Bit8Value", typeof(BitArray), true),
            ("VarbitValue", typeof(BitArray), true),
            ("PointValue", typeof(NpgsqlPoint?), true),
            ("LineValue", typeof(NpgsqlLine?), true),
            ("LsegValue", typeof(NpgsqlLSeg?), true),
            ("BoxValue", typeof(NpgsqlBox?), true),
            ("PathValue", typeof(NpgsqlPath?), true),
            ("PolygonValue", typeof(NpgsqlPolygon?), true),
            ("CircleValue", typeof(NpgsqlCircle?), true),
            ("TsvectorValue", typeof(NpgsqlTsVector), true),
            ("TsqueryValue", typeof(NpgsqlTsQuery), true),
            ("OidValue", typeof(uint?), true),
            ("XidValue", typeof(uint?), true),
            ("CidValue", typeof(uint?), true),
            ("RegtypeValue", typeof(uint?), true),
            ("RegconfigValue", typeof(uint?), true),
            ("Xid8Value", typeof(ulong?), true),
            ("TidValue", typeof(NpgsqlTid?), true),
            ("PgLsnValue", typeof(NpgsqlLogSequenceNumber?), true),
            ("Int2vectorValue", typeof(short[]), true),
            ("OidvectorValue", typeof(uint[]), true),
            ("RefcursorValue", typeof(string), true),
            ("HstoreValue", typeof(Dictionary<string, string>), true),
            ("LtreeValue", typeof(string), true),
            ("LqueryValue", typeof(string), true),
            ("LtxtqueryValue", typeof(string), true),
            ("Int4rangeValue", typeof(NpgsqlRange<int>?), true),
            ("Int8rangeValue", typeof(NpgsqlRange<long>?), true),
            ("NumrangeValue", typeof(NpgsqlRange<decimal>?), true),
            ("TsrangeValue", typeof(NpgsqlRange<DateTime>?), true),
            ("TstzrangeValue", typeof(NpgsqlRange<DateTime>?), true),
            ("DaterangeValue", typeof(NpgsqlRange<DateOnly>?), true),
            ("Int4multirangeValue", typeof(NpgsqlRange<int>[]), true),
            ("Int8multirangeValue", typeof(NpgsqlRange<long>[]), true),
            ("NummultirangeValue", typeof(NpgsqlRange<decimal>[]), true),
            ("TsmultirangeValue", typeof(NpgsqlRange<DateTime>[]), true),
            ("TstzmultirangeValue", typeof(NpgsqlRange<DateTime>[]), true),
            ("DatemultirangeValue", typeof(NpgsqlRange<DateOnly>[]), true),
            ("Int4ArrayValue", typeof(int[]), true),
            ("TextArrayValue", typeof(string[]), true),
            ("DateArrayValue", typeof(DateOnly[]), true),
            ("StatusArrayValue", typeof(string[]), true),
            ("EmailValue", typeof(string), true),
            ("StatusValue", typeof(string), true)
        );

    private static Type? ModelNamed(string fullName) => typeof(ModelShapeTests).Assembly.GetType(fullName);

    private static void ShouldBeModel(
        Type model,
        string fullName,
        Shape shape,
        params (string Name, Type Type, bool Nullable)[] properties
    )
    {
        model.FullName.ShouldBe(fullName);
        ShapeOf(model).ShouldBe(shape);
        PropertiesOf(model).ShouldBe(properties);
    }

    // A record has the compiler's clone method, which a class has not.
    private static Shape ShapeOf(Type model) =>
        (model.GetMethod("<Clone>$") is not null, model.IsSealed) switch
        {
            (true, false) => Shape.Record,
            (true, true) => Shape.SealedRecord,
            (false, false) => Shape.Class,
            (false, true) => Shape.SealedClass,
        };

    // The public properties in the order they are declared, each with its type and whether it can be null.
    private static (string Name, Type Type, bool Nullable)[] PropertiesOf(Type model)
    {
        var context = new NullabilityInfoContext();
        return
        [
            .. model
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property =>
                    (
                        property.Name,
                        property.PropertyType,
                        context.Create(property).ReadState == NullabilityState.Nullable
                    )
                ),
        ];
    }
}
