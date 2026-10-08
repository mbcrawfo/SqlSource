using System;
using Consumer;

// tools/check-package-install.sh compares what this prints with expected-output.txt.

// A constant: the generator was loaded, and the package handed the .sql files to the compiler.
Console.WriteLine($"constant: {Queries.GetUser}");

// Directory.Build.targets turns token validation off for the project.  With it on, the empty argument throws.
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

// The project's dialect is postgres, and the item of ByMetadata.sql says mysql.
Console.WriteLine($"dialect of the project: {Queries.ByProperty}");
Console.WriteLine($"dialect of the item: {Queries.ByMetadata}");

// A target of Directory.Build.targets adds the item of AddedByATarget.sql, and its metadata says mssql.
Console.WriteLine($"dialect of an item that a target adds: {Queries.AddedByATarget}");

// ByMarker.sql names its own dialect, mssql, with a marker.
Console.WriteLine($"dialect of the file's marker: {Queries.ByMarker}");
