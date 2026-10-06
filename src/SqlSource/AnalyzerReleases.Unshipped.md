; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SQLSRC001 | SqlSource | Error | Type must be partial
SQLSRC002 | SqlSource | Error | Type is file-local
SQLSRC003 | SqlSource | Error | Target framework is not supported
SQLSRC004 | SqlSource | Error | Path matches no SQL file
SQLSRC005 | SqlSource | Error | Folder has no SQL file
SQLSRC006 | SqlSource | Error | Mode is not valid
SQLSRC007 | SqlSource | Error | Type has a member named Sql
SQLSRC008 | SqlSource | Error | Query name is used in two files
SQLSRC009 | SqlSource | Error | Query is named like its containing type
SQLSRC101 | SqlSource | Error | Quote is not closed
SQLSRC102 | SqlSource | Error | Comment is not closed
SQLSRC103 | SqlSource | Error | Query name is not valid
SQLSRC104 | SqlSource | Error | Query name is used twice
SQLSRC105 | SqlSource | Error | File name is not a valid query name
SQLSRC106 | SqlSource | Error | SQL before the first name
SQLSRC107 | SqlSource | Error | Summary before the first name
SQLSRC108 | SqlSource | Error | Marker has no SQL after it
SQLSRC109 | SqlSource | Error | Directive is not known
SQLSRC110 | SqlSource | Error | Directive is missing
SQLSRC111 | SqlSource | Error | Directive value is not valid
SQLSRC112 | SqlSource | Error | Directives conflict
SQLSRC113 | SqlSource | Error | Query has no SQL
SQLSRC114 | SqlSource | Error | Token name is a keyword
