using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlTypes;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The project runs the generator when it builds: the attribute it declares is in the assembly, and so is each model of
// the queries of Queries/, with the name, the shape and the properties that its settings give it.  The tests name each
// model with typeof, so a model that is not generated fails the build, and read its shape by reflection.  No server is
// needed.
public sealed class ModelShapeTests
{
    private const string Namespace = "SqlSource.Tests.SqlServer.EndToEnd";

    private enum Shape
    {
        Record,
        SealedRecord,
        Class,
        SealedClass,
    }

    // The columns of the table accounts, by the project's type override for date.
    private static readonly (string Name, Type Type, bool Nullable)[] AccountsProperties =
    [
        ("Id", typeof(int), false),
        ("Name", typeof(string), false),
        ("Email", typeof(string), false),
        ("Status", typeof(string), false),
        ("OpenedOn", typeof(DateOnly), false),
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
    public void Build_WholeTableOfDbo_IsTheTableModelWithoutItsSchema()
    {
        ShouldBeModel(typeof(AccountsDto), $"{Namespace}.AccountsDto", Shape.SealedRecord, AccountsProperties);
        ModelNamed($"{Namespace}.DboAccountsDto").ShouldBeNull();
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
    public void Build_Datetimeoffset_IsADateTimeOffset() =>
        ShouldBeModel(
            typeof(ListClosedBeforeDto),
            $"{Namespace}.ListClosedBeforeDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("ClosedAt", typeof(DateTimeOffset?), true)
        );

    [Fact]
    public void Build_ParamMarkersWithTypes_GiveTheDeclaredTypes()
    {
        ShouldBeModel(
            typeof(ListOpenedWithinArgs),
            $"{Namespace}.ListOpenedWithinArgs",
            Shape.Record,
            ("Days", typeof(int), false),
            ("AsOf", typeof(DateOnly), false)
        );
        ShouldBeModel(
            typeof(ListOpenedWithinDto),
            $"{Namespace}.ListOpenedWithinDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("OpenedOn", typeof(DateOnly), false)
        );
    }

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
        ShouldBeModel(
            typeof(Reporting.ListOpenedWithinArgs),
            $"{ReportingNamespace}.ListOpenedWithinArgs",
            Shape.Record,
            ("Days", typeof(int), false),
            ("AsOf", typeof(DateOnly), false)
        );
        ModelNamed($"{ReportingNamespace}.ListAccountsDto").ShouldBeNull();
        ModelNamed($"{ReportingNamespace}.ListAccountsInTableOrderDto").ShouldBeNull();
        Reporting.AccountReports.GetAccount.ShouldBe(
            "SELECT name, id, email\nFROM dbo.accounts\nWHERE id = @accountId;"
        );
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
            ("Kind", typeof(string), true),
            ("Body", typeof(string), true),
            ("RemindAt", typeof(TimeOnly?), true)
        );

    [Fact]
    public void Build_ColumnMarker_MakesTheCountNotNull() =>
        ShouldBeModel(
            typeof(CountAccountNotesDto),
            $"{Namespace}.CountAccountNotesDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("NoteCount", typeof(int), false)
        );

    [Fact]
    public void Build_SuffixMarksInBracketsAndInQuotes_SetTheNullability() =>
        ShouldBeModel(
            typeof(ListMarkedNamesDto),
            $"{Namespace}.ListMarkedNamesDto",
            Shape.SealedRecord,
            ("Id", typeof(int?), true),
            ("UpperName", typeof(string), false)
        );

    [Fact]
    public void Build_HeaderOverride_MapsTheAliasTypeToATypeOfTheProject() =>
        ShouldBeModel(
            typeof(ListNoteKindsDto),
            $"{Namespace}.ListNoteKindsDto",
            Shape.SealedRecord,
            ("Kind", typeof(string), false),
            ("Email", typeof(Email), false)
        );

    [Fact]
    public void Build_HeaderOverride_MakesTimeATimeOnly() =>
        PropertiesOf(typeof(ListAccountNotesDto)).ShouldContain(("RemindAt", typeof(TimeOnly?), true));

    [Fact]
    public void Build_QueryOverrideOfSys_ReturnsTimeToTimeSpan() =>
        ShouldBeModel(
            typeof(ListRemindersDto),
            $"{Namespace}.ListRemindersDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("RemindAt", typeof(TimeSpan?), true)
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
        ConstantQueries.CountAccounts.ShouldBe("SELECT COUNT(*) FROM dbo.accounts;");
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
            ("IssuedOn", typeof(DateTime), false),
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
    public void Build_FolderOverride_ReturnsDateToDateTime() =>
        PropertiesOf(typeof(Billing.BillingInvoicesRow)).ShouldContain(("IssuedOn", typeof(DateTime), false));

    [Fact]
    public void Build_QueryOverride_ReturnsDateToDateTime() =>
        PropertiesOf(typeof(AllTypesDto)).ShouldContain(("DateValue", typeof(DateTime?), true));

    [Fact]
    public void Build_InsertOutputOfEveryColumn_SharesTheTableModel()
    {
        ModelNamed($"{Namespace}.InsertAllTypesDto").ShouldBeNull();
        ShapeOf(typeof(InsertAllTypesArgs)).ShouldBe(Shape.Record);

        // A parameter for each column that the query writes, in its order, with the column's type, and none can be
        // null, since no -- param: marker says null.
        var expected = PropertiesOf(typeof(AllTypesDto))
            .Where(static column => !AllTypesQueryTests.WriteSideLeftOut.ContainsKey(column.Name))
            .Select(static column => (column.Name, Nullable.GetUnderlyingType(column.Type) ?? column.Type, false));
        PropertiesOf(typeof(InsertAllTypesArgs)).ShouldBe(expected);
        PropertiesOf(typeof(InsertAllTypesArgs)).Length.ShouldBe(30);
    }

    [Fact]
    public void Build_AllTypes_IsASealedRecordOfEveryColumn() =>
        ShouldBeModel(
            typeof(AllTypesDto),
            $"{Namespace}.AllTypesDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("BigintValue", typeof(long?), true),
            ("IntValue", typeof(int?), true),
            ("SmallintValue", typeof(short?), true),
            ("TinyintValue", typeof(byte?), true),
            ("BitValue", typeof(bool?), true),
            ("DecimalValue", typeof(decimal?), true),
            ("NumericValue", typeof(decimal?), true),
            ("MoneyValue", typeof(decimal?), true),
            ("SmallmoneyValue", typeof(decimal?), true),
            ("FloatValue", typeof(double?), true),
            ("RealValue", typeof(float?), true),
            ("DateValue", typeof(DateTime?), true),
            ("DatetimeValue", typeof(DateTime?), true),
            ("Datetime2Value", typeof(DateTime?), true),
            ("SmalldatetimeValue", typeof(DateTime?), true),
            ("TimeValue", typeof(TimeSpan?), true),
            ("DatetimeoffsetValue", typeof(DateTimeOffset?), true),
            ("CharValue", typeof(string), true),
            ("VarcharValue", typeof(string), true),
            ("TextValue", typeof(string), true),
            ("NcharValue", typeof(string), true),
            ("NvarcharValue", typeof(string), true),
            ("NtextValue", typeof(string), true),
            ("XmlValue", typeof(string), true),
            ("BinaryValue", typeof(byte[]), true),
            ("VarbinaryValue", typeof(byte[]), true),
            ("ImageValue", typeof(byte[]), true),
            ("RowversionValue", typeof(byte[]), true),
            ("UniqueidentifierValue", typeof(Guid?), true),
            ("SqlVariantValue", typeof(object), true),
            ("SysnameValue", typeof(string), true)
        );

    [Fact]
    public void Build_NewTypes_IsASealedRecordOfJsonAndVector() =>
        ShouldBeModel(
            typeof(NewTypesDto),
            $"{Namespace}.NewTypesDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("JsonValue", typeof(string), true),
            ("VectorValue", typeof(SqlVector<float>?), true)
        );

    [Fact]
    public void Build_InsertOutput_GivesTheInsertedColumns()
    {
        ShouldBeModel(
            typeof(AddNoteArgs),
            $"{Namespace}.AddNoteArgs",
            Shape.Record,
            ("AccountId", typeof(int), false),
            ("Kind", typeof(string), false),
            ("Body", typeof(string), false),
            ("WrittenAt", typeof(DateTime), false)
        );
        ShouldBeModel(
            typeof(AddNoteDto),
            $"{Namespace}.AddNoteDto",
            Shape.SealedRecord,
            ("Id", typeof(int), false),
            ("Kind", typeof(string), false)
        );
    }

    [Fact]
    public void Build_UpdateOutput_GivesTheDeletedAndInsertedColumns()
    {
        ShouldBeModel(
            typeof(RenameAccountArgs),
            $"{Namespace}.RenameAccountArgs",
            Shape.Record,
            ("Name", typeof(string), false),
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(RenameAccountDto),
            $"{Namespace}.RenameAccountDto",
            Shape.SealedRecord,
            ("OldName", typeof(string), false),
            ("NewName", typeof(string), false)
        );
    }

    [Fact]
    public void Build_MergeOutput_GivesTheActionAndTheInsertedColumns()
    {
        ShouldBeModel(
            typeof(MergeAccountStatusArgs),
            $"{Namespace}.MergeAccountStatusArgs",
            Shape.Record,
            ("AccountId", typeof(int), false),
            ("Status", typeof(string), false)
        );
        ShouldBeModel(
            typeof(MergeAccountStatusDto),
            $"{Namespace}.MergeAccountStatusDto",
            Shape.SealedRecord,
            ("Change", typeof(string), false),
            ("Id", typeof(int), false)
        );
    }

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
