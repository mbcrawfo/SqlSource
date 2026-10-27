using System;
using Consumer;

// tools/check-package-install.sh compares what this prints with expected-output.txt.

// A constant: the generator was loaded, and the package handed the .sql files to the compiler.
Console.WriteLine($"constant: {Queries.GetUser}");

// sqlsource.json sets the project's generator parameters, to a list whose second word is no-token-validation.
// Without it, the empty argument throws.
Console.WriteLine($"validation off: {Queries.ListUsers("users", "")}");

// The query's own list, `default`, replaces the project's, so this one checks.
try
{
    _ = Queries.ListChecked("users", "");
    Console.WriteLine("validation on: nothing was thrown");
}
catch (ArgumentException exception)
{
    Console.WriteLine($"validation on: {exception.GetType().Name} for {exception.ParamName}");
}

// The header of Kept.sql says keep-comments, as the second word of its list, and so does the attribute of
// KeptQueries, which claims Users.sql again.
Console.WriteLine($"comments kept by the file header: {Queries.Kept}");
Console.WriteLine($"comments kept by the attribute: {KeptQueries.GetUser}");

// The project's dialect is postgres, and the header of ByHeader.sql says mysql.
Console.WriteLine($"dialect of the project: {Queries.ByProject}");
Console.WriteLine($"dialect of the file header: {Queries.ByHeader}");

// The header of ByOption.sql says mysql, with the option no-backslash-escapes: plain MySQL would not close the string.
Console.WriteLine($"dialect of the file header, with an option: {Queries.ByOption}");

// A target of Directory.Build.targets adds AddedByATarget.sql, and its header says mssql.
Console.WriteLine($"dialect of a file that a target adds: {Queries.AddedByATarget}");

// ByMarker.sql names its own dialect, mssql, with a marker below a comment that the project's dialect reads.
Console.WriteLine($"dialect of a marker below a comment: {Queries.ByMarker}");
