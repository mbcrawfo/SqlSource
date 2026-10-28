using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;
using Xunit;
using static SqlSource.Testing.EndToEnd.ModelReflection;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The project runs the generator when it builds: the attribute it declares is in the assembly, and so is each model of
// the queries of Queries/, with the name, the shape and the properties that its settings give it.  The tests name each
// model with typeof, so a model that is not generated fails the build, and read its shape by reflection.  No server is
// needed.
public sealed class ModelShapeTests
{
    private const string Namespace = "SqlSource.DatabaseTests.SqlServer.EndToEnd";

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
    public void Build_ProjectFileSettings_GiveARecordInputAndASealedRecordOutput()
    {
        ShouldBeModel(
            typeof(GetAccountArgs),
            $"{Namespace}.GetAccountArgs",
            ModelShape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(GetAccountDto),
            $"{Namespace}.GetAccountDto",
            ModelShape.SealedRecord,
            ("Name", typeof(string), false),
            ("Id", typeof(int), false),
            ("Email", typeof(string), false)
        );
    }

    [Fact]
    public void Build_WholeTableOfDbo_IsTheTableModelWithoutItsSchema()
    {
        ShouldBeModel(typeof(AccountsDto), $"{Namespace}.AccountsDto", ModelShape.SealedRecord, AccountsProperties);
        ModelNamed($"{Namespace}.DboAccountsDto").ShouldBeNull();
        ModelNamed($"{Namespace}.ListAccountsDto").ShouldBeNull();
        ModelNamed($"{Namespace}.ListAccountsInTableOrderDto").ShouldBeNull();
    }

    [Fact]
    public void Build_NoTableModels_NamesTheModelAfterTheQuery() =>
        ShouldBeModel(
            typeof(ListAccountsUntabledDto),
            $"{Namespace}.ListAccountsUntabledDto",
            ModelShape.SealedRecord,
            AccountsProperties
        );

    [Fact]
    public void Build_OutputModelMarker_SharesOneModel()
    {
        ShouldBeModel(
            typeof(AccountName),
            $"{Namespace}.AccountName",
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false)
        );
        ShouldBeModel(
            typeof(GetAccountNameArgs),
            $"{Namespace}.GetAccountNameArgs",
            ModelShape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(FindAccountNameArgs),
            $"{Namespace}.FindAccountNameArgs",
            ModelShape.Record,
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
            ModelShape.Record,
            ("ClosedBefore", typeof(DateTimeOffset?), true)
        );

    [Fact]
    public void Build_Datetimeoffset_IsADateTimeOffset() =>
        ShouldBeModel(
            typeof(ListClosedBeforeDto),
            $"{Namespace}.ListClosedBeforeDto",
            ModelShape.SealedRecord,
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
            ModelShape.Record,
            ("Days", typeof(int), false),
            ("AsOf", typeof(DateOnly), false)
        );
        ShouldBeModel(
            typeof(ListOpenedWithinDto),
            $"{Namespace}.ListOpenedWithinDto",
            ModelShape.SealedRecord,
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
            ModelShape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(Reporting.GetAccountDto),
            $"{ReportingNamespace}.GetAccountDto",
            ModelShape.SealedRecord,
            ("Name", typeof(string), false),
            ("Id", typeof(int), false),
            ("Email", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Reporting.AccountsDto),
            $"{ReportingNamespace}.AccountsDto",
            ModelShape.SealedRecord,
            AccountsProperties
        );
        ShouldBeModel(
            typeof(Reporting.ListAccountsUntabledDto),
            $"{ReportingNamespace}.ListAccountsUntabledDto",
            ModelShape.SealedRecord,
            AccountsProperties
        );
        ShouldBeModel(
            typeof(Reporting.AccountName),
            $"{ReportingNamespace}.AccountName",
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Reporting.GetAccountNameArgs),
            $"{ReportingNamespace}.GetAccountNameArgs",
            ModelShape.Record,
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(Reporting.FindAccountNameArgs),
            $"{ReportingNamespace}.FindAccountNameArgs",
            ModelShape.Record,
            ("Email", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Reporting.ListClosedBeforeArgs),
            $"{ReportingNamespace}.ListClosedBeforeArgs",
            ModelShape.Record,
            ("ClosedBefore", typeof(DateTimeOffset?), true)
        );
        ShouldBeModel(
            typeof(Reporting.ListClosedBeforeDto),
            $"{ReportingNamespace}.ListClosedBeforeDto",
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false),
            ("ClosedAt", typeof(DateTimeOffset?), true)
        );
        ShouldBeModel(
            typeof(Reporting.ListOpenedWithinArgs),
            $"{ReportingNamespace}.ListOpenedWithinArgs",
            ModelShape.Record,
            ("Days", typeof(int), false),
            ("AsOf", typeof(DateOnly), false)
        );
        ModelNamed($"{ReportingNamespace}.ListAccountsDto").ShouldBeNull();
        ModelNamed($"{ReportingNamespace}.ListAccountsInTableOrderDto").ShouldBeNull();
    }

    [Fact]
    public void Build_LeftJoin_MakesTheOptionalSideNullable() =>
        ShouldBeModel(
            typeof(ListAccountNotesDto),
            $"{Namespace}.ListAccountNotesDto",
            ModelShape.SealedRecord,
            ("Id", typeof(AccountId), false),
            ("Name", typeof(string), false),
            ("NoteId", typeof(int?), true),
            ("AccountId", typeof(AccountId?), true),
            ("Kind", typeof(string), true),
            ("Body", typeof(string), true),
            ("RemindAt", typeof(TimeOnly?), true)
        );

    [Fact]
    public void Build_Count_IsNotNullWithoutANullabilityMarker() =>
        ShouldBeModel(
            typeof(CountAccountNotesDto),
            $"{Namespace}.CountAccountNotesDto",
            ModelShape.SealedRecord,
            ("Id", typeof(AccountId), false),
            ("NoteCount", typeof(NoteCount), false)
        );

    // dbo.accounts.id and dbo.notes.account_id are AccountIds by the header's -- column: markers (dbo.notes.account_id
    // pinned by Build_LeftJoin_MakesTheOptionalSideNullable), the count by the query's, and the parameter by its
    // -- param: marker, which names no database type.
    [Fact]
    public void Build_ColumnTypesAndParamMarker_MapTheIdsAndTheCountToTypesOfTheProject()
    {
        ShouldBeModel(
            typeof(GetAccountNoteCountArgs),
            $"{Namespace}.GetAccountNoteCountArgs",
            ModelShape.Record,
            ("Id", typeof(AccountId), false)
        );
        ShouldBeModel(
            typeof(GetAccountNoteCountDto),
            $"{Namespace}.GetAccountNoteCountDto",
            ModelShape.SealedRecord,
            ("Id", typeof(AccountId), false),
            ("NoteCount", typeof(NoteCount), false)
        );
    }

    [Fact]
    public void Build_ColumnMarker_MakesTheMonthEndNotNull() =>
        ShouldBeModel(
            typeof(ListAccountMonthsDto),
            $"{Namespace}.ListAccountMonthsDto",
            ModelShape.SealedRecord,
            ("Id", typeof(AccountId), false),
            ("MonthEnd", typeof(DateOnly), false)
        );

    [Fact]
    public void Build_SuffixMarksInBracketsAndInQuotes_SetTheNullability() =>
        ShouldBeModel(
            typeof(ListMarkedNamesDto),
            $"{Namespace}.ListMarkedNamesDto",
            ModelShape.SealedRecord,
            ("Id", typeof(AccountId?), true),
            ("UpperName", typeof(string), false)
        );

    [Fact]
    public void Build_HeaderOverride_MapsTheAliasTypeToATypeOfTheProject() =>
        ShouldBeModel(
            typeof(ListNoteKindsDto),
            $"{Namespace}.ListNoteKindsDto",
            ModelShape.SealedRecord,
            ("Kind", typeof(string), false),
            ("Email", typeof(Email), false)
        );

    // SQL Server describes a parameter compared with a column of the alias type, or written to one, as the alias type,
    // so the header's override gives the parameter its type too.
    [Fact]
    public void Build_HeaderOverride_MapsTheAliasTypesParameterToATypeOfTheProject()
    {
        ShouldBeModel(
            typeof(FindAccountByEmailArgs),
            $"{Namespace}.FindAccountByEmailArgs",
            ModelShape.Record,
            ("Email", typeof(Email), false)
        );
        ShouldBeModel(
            typeof(AddAccountArgs),
            $"{Namespace}.AddAccountArgs",
            ModelShape.Record,
            ("Name", typeof(string), false),
            ("Email", typeof(Email), false),
            ("OpenedOn", typeof(DateOnly), false)
        );
    }

    // The alias type over bigint is a value type of the project, as a parameter, as a column of the table model of
    // external_refs, which two queries read the whole of, and as a nullable column of a LEFT JOIN.
    [Fact]
    public void Build_HeaderOverrideOfAnAliasTypeOverBigint_MapsTheColumnAndTheParameterToAValueType()
    {
        ShouldBeModel(
            typeof(GetExternalRefArgs),
            $"{Namespace}.GetExternalRefArgs",
            ModelShape.Record,
            ("ExternalId", typeof(ExternalId), false)
        );
        ShouldBeModel(
            typeof(ExternalRefsDto),
            $"{Namespace}.ExternalRefsDto",
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("AccountId", typeof(int), false),
            ("ExternalId", typeof(ExternalId), false)
        );
        ShouldBeModel(
            typeof(ListAccountExternalIdsDto),
            $"{Namespace}.ListAccountExternalIdsDto",
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("ExternalId", typeof(ExternalId?), true)
        );
    }

    [Fact]
    public void Build_HeaderOverride_MakesTimeATimeOnly() =>
        TypedPropertiesOf(typeof(ListAccountNotesDto)).ShouldContain(("RemindAt", typeof(TimeOnly?), true));

    [Fact]
    public void Build_QueryOverrideOfSys_ReturnsTimeToTimeSpan() =>
        ShouldBeModel(
            typeof(ListRemindersDto),
            $"{Namespace}.ListRemindersDto",
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("RemindAt", typeof(TimeSpan?), true)
        );

    [Fact]
    public void Build_SortInput_OrdersTheInputByName()
    {
        ShouldBeModel(
            typeof(Shapes.SearchAccountsFilter),
            $"{Namespace}.Shapes.SearchAccountsFilter",
            ModelShape.SealedClass,
            ("AccountId", typeof(int), false),
            ("Name", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Shapes.SearchAccountsView),
            $"{Namespace}.Shapes.SearchAccountsView",
            ModelShape.Record,
            ("Id", typeof(int), false),
            ("Name", typeof(string), false)
        );
    }

    [Fact]
    public void Build_SortOutput_OrdersTheOutputByName() =>
        ShouldBeModel(
            typeof(Shapes.ListAccountColumnsView),
            $"{Namespace}.Shapes.ListAccountColumnsView",
            ModelShape.Class,
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
            ModelShape.Record,
            ("Id", typeof(int), false),
            ("Headline", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Shapes.GetHeadlineFilter),
            $"{Namespace}.Shapes.GetHeadlineFilter",
            ModelShape.SealedClass,
            ("AccountId", typeof(int), false)
        );
    }

    [Fact]
    public void Build_InputModelMarkerWithAPeriod_IsAFullName()
    {
        ShouldBeModel(
            typeof(Shared.AccountLookup),
            $"{Namespace}.Shared.AccountLookup",
            ModelShape.Class,
            ("Email", typeof(string), false)
        );
        ShouldBeModel(
            typeof(Shapes.LookUpAccountView),
            $"{Namespace}.Shapes.LookUpAccountView",
            ModelShape.Record,
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
            ModelShape.SealedClass,
            ("Id", typeof(int), false),
            ("AccountId", typeof(int), false),
            ("Number", typeof(string), false),
            ("Amount", typeof(decimal), false),
            ("IssuedOn", typeof(DateTime), false),
            ("PaidAt", typeof(DateTime?), true)
        );

    // A codegen query has a method and no constant: InvoiceQueries is left as it was declared, and its methods are in
    // InvoiceQueriesExtensions.
    [Fact]
    public void Build_CodeGenClaim_HasNoConstantsAndNoNestedClass()
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        typeof(InvoiceQueries).GetFields(Members).ShouldBeEmpty();
        typeof(InvoiceQueries).GetNestedTypes(Members).ShouldBeEmpty();
        typeof(InvoiceQueriesExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(static method => method.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["GetInvoice", "ListInvoices"]);
    }

    // A claim at the output models with a direct location has the constants on the type, which the Dapper tests run.
    [Fact]
    public void Build_ModelsClaimWithADirectLocation_HasTheConstantsOnTheType()
    {
        AllTypesDapperQueries.ListAllTypes.ShouldBe("SELECT *\nFROM dbo.all_types\nORDER BY id;");
        WriteDapperQueries.RenameAccount.ShouldBe(
            "UPDATE dbo.accounts\nSET name = @name\nOUTPUT DELETED.name AS old_name, INSERTED.name AS new_name\n"
                + "WHERE id = @accountId;"
        );
    }

    [Fact]
    public void Build_OutputTypeByFolderFile_IsASealedClass()
    {
        ShouldBeModel(
            typeof(Billing.GetInvoiceArgs),
            $"{Namespace}.Billing.GetInvoiceArgs",
            ModelShape.Record,
            ("InvoiceId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(Billing.GetInvoiceRow),
            $"{Namespace}.Billing.GetInvoiceRow",
            ModelShape.SealedClass,
            ("Id", typeof(int), false),
            ("Number", typeof(string), false),
            ("Amount", typeof(decimal), false),
            ("PaidAt", typeof(DateTime?), true)
        );
    }

    [Fact]
    public void Build_FolderOverride_ReturnsDateToDateTime() =>
        TypedPropertiesOf(typeof(Billing.BillingInvoicesRow)).ShouldContain(("IssuedOn", typeof(DateTime), false));

    [Fact]
    public void Build_QueryOverride_ReturnsDateToDateTime() =>
        TypedPropertiesOf(typeof(AllTypesDto)).ShouldContain(("DateValue", typeof(DateTime?), true));

    [Fact]
    public void Build_InsertOutputOfEveryColumn_SharesTheTableModel()
    {
        ModelNamed($"{Namespace}.InsertAllTypesDto").ShouldBeNull();
        ShapeOf(typeof(InsertAllTypesArgs)).ShouldBe(ModelShape.Record);

        // A parameter for each column that the query writes, in its order, with the column's type, and none can be
        // null, since no -- param: marker says null.
        var expected = TypedPropertiesOf(typeof(AllTypesDto))
            .Where(static column => !AllTypesQueryTests.WriteSideLeftOut.ContainsKey(column.Name))
            .Select(static column => (column.Name, Nullable.GetUnderlyingType(column.Type) ?? column.Type, false));
        TypedPropertiesOf(typeof(InsertAllTypesArgs)).ShouldBe(expected);
        TypedPropertiesOf(typeof(InsertAllTypesArgs)).Length.ShouldBe(30);
    }

    [Fact]
    public void Build_AllTypes_IsASealedRecordOfEveryColumn() =>
        ShouldBeModel(
            typeof(AllTypesDto),
            $"{Namespace}.AllTypesDto",
            ModelShape.SealedRecord,
            [.. SqlServerAllTypesRows.AllTypesProperties]
        );

    [Fact]
    public void Build_NewTypes_IsASealedRecordOfJsonAndVector() =>
        ShouldBeModel(
            typeof(NewTypesDto),
            $"{Namespace}.NewTypesDto",
            ModelShape.SealedRecord,
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
            ModelShape.Record,
            ("AccountId", typeof(int), false),
            ("Kind", typeof(string), false),
            ("Body", typeof(string), false),
            ("WrittenAt", typeof(DateTime), false)
        );
        ShouldBeModel(
            typeof(AddNoteDto),
            $"{Namespace}.AddNoteDto",
            ModelShape.SealedRecord,
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
            ModelShape.Record,
            ("Name", typeof(string), false),
            ("AccountId", typeof(int), false)
        );
        ShouldBeModel(
            typeof(RenameAccountDto),
            $"{Namespace}.RenameAccountDto",
            ModelShape.SealedRecord,
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
            ModelShape.Record,
            ("AccountId", typeof(int), false),
            ("Status", typeof(string), false)
        );
        ShouldBeModel(
            typeof(MergeAccountStatusDto),
            $"{Namespace}.MergeAccountStatusDto",
            ModelShape.SealedRecord,
            ("Change", typeof(string), false),
            ("Id", typeof(int), false)
        );
    }

    private static Type? ModelNamed(string fullName) =>
        ModelReflection.ModelNamed(typeof(ModelShapeTests).Assembly, fullName);
}
