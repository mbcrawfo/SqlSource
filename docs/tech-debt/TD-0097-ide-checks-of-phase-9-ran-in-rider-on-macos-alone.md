# TD-0097 - The IDE checks of phase 9 ran in Rider on macOS alone

## Problem

Sub-phase 9.0 of [the phase 9 design](../superpowers/specs/2026-11-02-query-generation-phase-9-build-integration-design.md) left to the owner what only an IDE shows: when IntelliSense picks up a sidecar that another process wrote, whether the online build's guard keeps it out of the IDE's design-time builds, the VS Code task that starts the watcher, Rider's startup task, and the file events of an editor's save.  The owner ran them in Rider 2026.2.4 on macOS; [the spike's record](../superpowers/specs/2026-11-02-query-generation-phase-9-spike.md#the-owners-checklist) holds the answers.  Nothing was run in Visual Studio, in VS Code, in Rider on Windows or Linux, or on Windows at all.

What the design says of those IDEs is therefore what their documentation and source say, not what they did:

- that Visual Studio and VS Code, in their default "Balanced" generator mode, show a sidecar another process wrote only at the next save or build, and in "Automatic" mode when it is written;
- that the guard `'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'` keeps the online run out of Visual Studio's and VS Code's loading and design-time builds while a build from the IDE runs it (Roslyn's BuildHost, behind VS Code's C# tooling, was run from the command line and was kept out; the IDE was not);
- that the VS Code task starts on folder open, and its problem matcher shows each one-line error and clears it at the next pass;
- which `FileSystemWatcher` events a save from VS Code and from Visual Studio raises, on macOS and on Windows.

## Why it exists

The owner ran the checklist on the machine and IDE at hand and chose, on 2026-11-03, to go on from Rider's answers and record the rest here.

## Impact

Sub-phases 9.3 and 9.4 build the watcher and write the manual's recipes and "what each IDE shows" from statements that are unverified outside Rider on macOS.  If one is wrong, the manual tells a Visual Studio or VS Code user something their IDE does not do, such as that a saved file refreshes the models, and the VS Code recipe may not show or clear its problems.

## Proposed fix

Run the procedure below in each IDE and on each system that its tables leave blank, write the answers into the tables of [the spike's checklist](../superpowers/specs/2026-11-02-query-generation-phase-9-spike.md#the-owners-checklist), and correct the design, the recipes and the manual where an answer differs.  Then delete this item.

The procedure needs no database: the sample's sidecars are written in advance, and a tool of the sample writes them from a process of its own, as the watcher will.

### Setting up

1. On each machine, from a checkout of the repository, pack the generator into a folder of its own: `dotnet pack src/SqlSource/SqlSource.csproj --output ~/sqlsource-spike-feed`.  The package is `SqlSource.<VersionPrefix>-dev.nupkg`; the sample's `Sample.csproj` names `0.1.0-dev`, so change it if `VersionPrefix` in `Directory.Build.props` has moved.  The sample's sidecars are of `formatVersion` 1, written by the tool `0.1.0` against `postgres:18.6`.
2. Save the script below as `make-sample.sh` and run `bash make-sample.sh ~/sqlsource-spike-sample ~/sqlsource-spike-feed`.  On Windows, run it in Git Bash, or run it on another machine and copy the folder and the feed across, then change the path in the sample's `nuget.config`.  The sample must be outside the repository.
3. In the sample's folder, run `dotnet build`.  It fails with one error, on purpose: `Queries/Orders.sql(1,10): error SQLSRC241: ...`, since `Orders.sql` has no sidecar yet.
4. Record the machine, the system, `dotnet --version` in the sample, and the version of each IDE and of VS Code's C# extension and C# Dev Kit.

Every command below runs in a terminal in the sample's folder:

- `dotnet run --file tools/swap.cs -- b` rewrites `Queries/Users.sql.json` with a sidecar that says `id` is `bigint`: the model's `Id` becomes a `long`, and line 5 of `Program.cs` gets `CS0266`.  `-- a` writes the first one back.
- `dotnet run --file tools/swap.cs -- new` writes `Queries/Orders.sql.json`, which did not exist: `SQLSRC241` goes.
- `dotnet run --file tools/swap.cs -- edited` writes the sidecar of the edited text of `Queries/Count.sql`, case E below.
- `dotnet run --file tools/swap.cs -- reset` puts everything back.

### IntelliSense after another process writes a sidecar

In each IDE, and in each generator mode, the default and "Automatic" (Visual Studio: Tools > Options, search "source generator"; VS Code: its settings, search "source generator", and record the setting's id): run `swap.cs -- reset`, build once from the IDE, and wait until the IDE shows exactly one error, `SQLSRC241` at `Orders.sql`.  Open `Program.cs`, `Orders.cs` and `Queries/Count.sql`.  For each case, run the command, touch nothing, and watch for 30 seconds; if nothing changed, type a space at the end of a comment in `Program.cs` and save; if nothing changed, build.  Record what changed, after which step, and about how long after.

- **R, rewritten.**  `swap.cs -- b`: does `CS0266` appear on line 5 of `Program.cs`?  Then `swap.cs -- a`: does it go?
- **N, new.**  `swap.cs -- new`: does `SQLSRC241` go, and does `OrdersDto` in `Orders.cs` resolve on hover?
- **E, edited.**  In the editor, change line 2 of `Queries/Count.sql` to `SELECT count(*) AS total, max(id) AS top_id FROM users;`, exactly, and save.  Does `SQLSRC244` appear, and when?  Then `swap.cs -- edited`: does `SQLSRC244` go?

### The design-time guard

The sample's `Directory.Build.targets` has `SpikeProbe`, with no condition, which appends a line to `obj/probe.log` each time a build of the project reaches the place of the online target, with `DesignTimeBuild`, `BuildingProject`, `BuildingInsideVisualStudio`, `SkipCompilerExecution`, `BuildingByReSharper`, `MSBuildRuntimeType` and `MSBuildVersion`; and `SpikeGuarded`, with the design's condition, which appends `guarded target ran` to `obj/guarded.log`.  In each IDE: close it; delete `obj/probe.log` and `obj/guarded.log` and nothing else of `obj`; open the sample and wait about a minute; add a space to line 2 of `Queries/Users.sql` and save, wait 15 seconds; add an empty line to `Sample.csproj` and save, wait 30 seconds; build from the IDE.  After each step, copy the lines added to both files.  The guard is right if `guarded.log` gains a line at the build and at no other step.

### The VS Code task

`.vscode/tasks.json` runs `tools/watcher.cs`, a stand-in for `describe --watch` that checks `Queries/*.sql` every 300 ms and after each change prints `sqlsource: change detected; describing`, then, for each file with a line holding `-- BROKEN`, `<path>(<line>,1): error SQLSRC999: stand-in error for the spike; detail: the file says BROKEN; see: https://example.invalid/sqlsrc999`, then `sqlsource: watching for changes; press Ctrl+C to stop`.

1. Open the sample's folder in VS Code.  Record whether it asks to trust the folder and to allow automatic tasks, with the questions' exact words, and allow.
2. Does the terminal `sqlsource watch (stand-in)` start by itself?
3. Add `-- BROKEN` as line 2 of `Queries/Count.sql` and save.  Does `SQLSRC999` appear in the Problems panel at line 2, with its whole text?
4. Remove the line and save.  Does the problem go?
5. Close and reopen the folder: does the task start again without asking?
6. In a copy of the folder that VS Code does not trust: does the task start?

### The events of a save

Run `dotnet run --file tools/events.cs` and wait for `watching ...`.  In each editor, open `Queries/Users.sql`, add a space at the end of line 2 and save, and copy what the terminal printed for that save.

### The script

```bash
#!/usr/bin/env bash
# Writes the phase 9 spike's IDE sample into <folder>, restoring SqlSource 0.1.0-dev from <feed>.
# Usage: make-sample.sh <folder> <feed>   <feed> is a folder that holds SqlSource.0.1.0-dev.nupkg.
set -euo pipefail
[[ $# -eq 2 ]] || { echo "usage: make-sample.sh <folder> <feed>" >&2; exit 2; }
mkdir -p "$1" && cd "$1" && feed="$(cd "$2" && pwd)"
mkdir -p Queries tools/sidecars .vscode
cat >nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="spike-feed" value="$feed" />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
EOF
cat >Sample.csproj <<'SAMPLE_EOF'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="SqlSource" Version="0.1.0-dev" PrivateAssets="all" />
        <Compile Remove="tools/**" />
        <AdditionalFiles Remove="tools/**" />
    </ItemGroup>
</Project>
SAMPLE_EOF
cat >sqlsource.json <<'SAMPLE_EOF'
{ "root": true, "dialect": "postgres" }
SAMPLE_EOF
cat >Directory.Build.targets <<'SAMPLE_EOF'
<Project>
    <!--
        Two targets in the place of the online target: SqlSourceTrackAdditionalFiles runs just before CoreCompile.
        SpikeProbe has no condition and appends the values its condition would test to obj/probe.log, every time any
        build of the project reaches it.  SpikeGuarded has the design's condition and appends to obj/guarded.log.
    -->
    <Target Name="SpikeProbe" BeforeTargets="SqlSourceTrackAdditionalFiles">
        <WriteLinesToFile File="$(MSBuildProjectDirectory)/obj/probe.log" Lines="$([System.DateTime]::Now.ToString('HH:mm:ss.fff')) DesignTimeBuild=$(DesignTimeBuild) BuildingProject=$(BuildingProject) BuildingInsideVisualStudio=$(BuildingInsideVisualStudio) SkipCompilerExecution=$(SkipCompilerExecution) BuildingByReSharper=$(BuildingByReSharper) MSBuildRuntimeType=$(MSBuildRuntimeType) MSBuildVersion=$(MSBuildVersion)" />
    </Target>
    <Target
        Name="SpikeGuarded"
        BeforeTargets="SqlSourceTrackAdditionalFiles"
        Condition="'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'"
    >
        <WriteLinesToFile File="$(MSBuildProjectDirectory)/obj/guarded.log" Lines="$([System.DateTime]::Now.ToString('HH:mm:ss.fff')) guarded target ran" />
    </Target>
</Project>
SAMPLE_EOF
cat >Queries.cs <<'SAMPLE_EOF'
using SqlSource;

namespace Sample;

[SqlSourceGenerate(Path = "Queries", Output = GeneratorOutput.Models)]
internal static partial class Queries;
SAMPLE_EOF
cat >Program.cs <<'SAMPLE_EOF'
using Sample;

// Rewritten: with sidecar A, Id is an int and this line has no error.  With sidecar B, Id is a long, and the line
// shows CS0266.
int id = new UsersDto(1, "name", null).Id;
System.Console.WriteLine(id);
SAMPLE_EOF
cat >Orders.cs <<'SAMPLE_EOF'
namespace Sample;

// New: until Queries/Orders.sql.json exists, OrdersDto does not, and this line shows CS0246.
internal static class OrdersUse
{
    internal static object Make() => new OrdersDto(1, 1, 1m);
}
SAMPLE_EOF
cat >Queries/Users.sql <<'SAMPLE_EOF'
-- name: GetUser
SELECT id, name, email FROM users WHERE id = @id;
SAMPLE_EOF
cat >Queries/Orders.sql <<'SAMPLE_EOF'
-- name: ListOrders -> many
SELECT id, user_id, total FROM orders WHERE user_id = @userId;
SAMPLE_EOF
cat >Queries/Count.sql <<'SAMPLE_EOF'
-- name: CountUsers
SELECT count(*) AS total FROM users;
SAMPLE_EOF
cat >tools/swap.cs <<'SAMPLE_EOF'
// Writes the sample's sidecars from a process of its own, as the watcher will.  Run from the sample's folder:
//   dotnet run --file tools/swap.cs -- a | b | new | edited | reset
using System;
using System.IO;

var sidecars = Path.Combine("tools", "sidecars");
void Put(string from, string to)
{
    // Written, not copied: a copy keeps the source's time, and an incremental build would not see the change.
    File.WriteAllBytes(Path.Combine("Queries", to), File.ReadAllBytes(Path.Combine(sidecars, from)));
    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} wrote Queries/{to} from {from}");
}

switch (args.Length == 1 ? args[0] : "")
{
    case "a": Put("Users.a.sql.json", "Users.sql.json"); break;
    case "b": Put("Users.b.sql.json", "Users.sql.json"); break;
    case "new": Put("Orders.sql.json", "Orders.sql.json"); break;
    case "edited": Put("Count.after.sql.json", "Count.sql.json"); break;
    case "reset":
        Put("Users.a.sql.json", "Users.sql.json");
        Put("Count.before.sql.json", "Count.sql.json");
        File.WriteAllText(Path.Combine("Queries", "Count.sql"), "-- name: CountUsers\nSELECT count(*) AS total FROM users;\n");
        File.Delete(Path.Combine("Queries", "Orders.sql.json"));
        Console.WriteLine("reset: Users is A, Count is the first text with its sidecar, Orders has no sidecar");
        break;
    default:
        Console.Error.WriteLine("usage: dotnet run --file tools/swap.cs -- a | b | new | edited | reset");
        return 2;
}
return 0;
SAMPLE_EOF
cat >tools/watcher.cs <<'SAMPLE_EOF'
// A stand-in for "dotnet sqlsource describe --watch": it describes nothing.  It polls the .sql files of Queries
// every 300 ms and, after a pass, prints the two markers of the design and, for each file that holds "-- BROKEN",
// one error in the one-line form.  Run from the sample's folder: dotnet run --file tools/watcher.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

const string Begin = "sqlsource: change detected; describing";
const string End = "sqlsource: watching for changes; press Ctrl+C to stop";

Dictionary<string, DateTime> Snapshot() =>
    Directory.GetFiles("Queries", "*.sql").ToDictionary(Path.GetFullPath, File.GetLastWriteTimeUtc);

void Pass()
{
    foreach (var file in Directory.GetFiles("Queries", "*.sql").Select(Path.GetFullPath).Order())
    {
        var lines = File.ReadAllLines(file);
        var at = Array.FindIndex(lines, line => line.Contains("-- BROKEN"));
        if (at >= 0)
        {
            Console.WriteLine(
                $"{file}({at + 1},1): error SQLSRC999: stand-in error for the spike; detail: the file says BROKEN; see: https://example.invalid/sqlsrc999"
            );
        }
    }
    Console.WriteLine($"postgres (postgres): stand-in pass at {DateTime.Now:HH:mm:ss.fff}");
    Console.WriteLine(End);
}

Pass();
var last = Snapshot();
while (true)
{
    Thread.Sleep(300);
    var now = Snapshot();
    if (now.Count != last.Count || now.Any(pair => !last.TryGetValue(pair.Key, out var time) || time != pair.Value))
    {
        Console.WriteLine(Begin);
        Pass();
        last = now;
    }
}
SAMPLE_EOF
cat >tools/events.cs <<'SAMPLE_EOF'
// Prints every event FileSystemWatcher raises under the sample's folder, but obj and bin, with the time.  Run from
// the sample's folder, then save Queries/Users.sql from the editor: dotnet run --file tools/events.cs
using System;
using System.Diagnostics;
using System.IO;

var clock = Stopwatch.StartNew();
using var watcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
{
    IncludeSubdirectories = true,
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.Attributes,
    InternalBufferSize = 64 * 1024,
};
void Print(string kind, string path, string? old = null)
{
    if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
    {
        return;
    }
    Console.WriteLine($"{clock.ElapsedMilliseconds,8} ms {kind,-8} {path}{(old is null ? "" : $" (from {old})")}");
}
watcher.Created += (_, e) => Print("Created", e.FullPath);
watcher.Changed += (_, e) => Print("Changed", e.FullPath);
watcher.Deleted += (_, e) => Print("Deleted", e.FullPath);
watcher.Renamed += (_, e) => Print("Renamed", e.FullPath, e.OldFullPath);
watcher.Error += (_, e) => Console.WriteLine($"{clock.ElapsedMilliseconds,8} ms Error    {e.GetException().Message}");
watcher.EnableRaisingEvents = true;
Console.WriteLine($"watching {watcher.Path}; press Ctrl+C to stop");
System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
SAMPLE_EOF
cat >.vscode/tasks.json <<'SAMPLE_EOF'
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "sqlsource watch (stand-in)",
      "type": "shell",
      "command": "dotnet run --file tools/watcher.cs",
      "isBackground": true,
      "runOptions": { "runOn": "folderOpen" },
      "presentation": { "reveal": "silent", "panel": "dedicated" },
      "problemMatcher": {
        "owner": "sqlsource",
        "fileLocation": "absolute",
        "pattern": {
          "regexp": "^(.+)\\((\\d+),(\\d+)\\): (error|warning) (SQLSRC\\d+): (.*)$",
          "file": 1,
          "line": 2,
          "column": 3,
          "severity": 4,
          "code": 5,
          "message": 6
        },
        "background": {
          "activeBegin": true,
          "beginsPattern": "^sqlsource: change detected; describing$",
          "endsPattern": "^sqlsource: watching for changes; press Ctrl\\+C to stop$"
        }
      }
    }
  ]
}
SAMPLE_EOF
cat >tools/sidecars/Users.a.sql.json <<'SAMPLE_EOF'
{"_WARNING":"This file was generated by the 'dotnet sqlsource' tool.  Do not edit by hand; use the tool to regenerate it.","$schema":"https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json","formatVersion":1,"toolVersion":"0.1.0","queries":{"GetUser":{"hash":"295bdef9619ec0c0710c713894fb719e895cb9a5447d294d1f4a214cd7c67d2a","engine":"postgres","database":"postgres","serverVersion":"18.6","resultKind":"rows","matchesTable":{"schema":"public","table":"users"},"plan":"not-needed","tableMatch":"matched","parameters":[{"name":"id","ordinal":0,"type":{"name":"integer","bindName":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":null,"typeSource":"inferred"}],"columns":[{"ordinal":0,"name":"id","type":{"name":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"users","column":"id"},"identity":false,"computed":false},{"ordinal":1,"name":"name","type":{"name":"text","kind":"base","schema":"pg_catalog","internalName":"text"},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"users","column":"name"},"identity":false,"computed":false},{"ordinal":2,"name":"email","type":{"name":"text","kind":"base","schema":"pg_catalog","internalName":"text"},"nullable":true,"nullableSource":"catalog","origin":{"schema":"public","table":"users","column":"email"},"identity":false,"computed":false}]}}}
SAMPLE_EOF
cat >tools/sidecars/Users.b.sql.json <<'SAMPLE_EOF'
{"_WARNING":"This file was generated by the 'dotnet sqlsource' tool.  Do not edit by hand; use the tool to regenerate it.","$schema":"https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json","formatVersion":1,"toolVersion":"0.1.0","queries":{"GetUser":{"hash":"295bdef9619ec0c0710c713894fb719e895cb9a5447d294d1f4a214cd7c67d2a","engine":"postgres","database":"postgres","serverVersion":"18.6","resultKind":"rows","matchesTable":{"schema":"public","table":"users"},"plan":"not-needed","tableMatch":"matched","parameters":[{"name":"id","ordinal":0,"type":{"name":"integer","bindName":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":null,"typeSource":"inferred"}],"columns":[{"ordinal":0,"name":"id","type":{"name":"bigint","kind":"base","schema":"pg_catalog","internalName":"int8"},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"users","column":"id"},"identity":false,"computed":false},{"ordinal":1,"name":"name","type":{"name":"text","kind":"base","schema":"pg_catalog","internalName":"text"},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"users","column":"name"},"identity":false,"computed":false},{"ordinal":2,"name":"email","type":{"name":"text","kind":"base","schema":"pg_catalog","internalName":"text"},"nullable":true,"nullableSource":"catalog","origin":{"schema":"public","table":"users","column":"email"},"identity":false,"computed":false}]}}}
SAMPLE_EOF
cat >tools/sidecars/Orders.sql.json <<'SAMPLE_EOF'
{"_WARNING":"This file was generated by the 'dotnet sqlsource' tool.  Do not edit by hand; use the tool to regenerate it.","$schema":"https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json","formatVersion":1,"toolVersion":"0.1.0","queries":{"ListOrders":{"hash":"8d4664c6d5e33fddb6f93f23770b7eb59a8c1e7973408246d743cfbe7476f5fd","engine":"postgres","database":"postgres","serverVersion":"18.6","resultKind":"rows","matchesTable":{"schema":"public","table":"orders"},"plan":"not-needed","tableMatch":"matched","parameters":[{"name":"userId","ordinal":0,"type":{"name":"integer","bindName":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":null,"typeSource":"inferred"}],"columns":[{"ordinal":0,"name":"id","type":{"name":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"orders","column":"id"},"identity":false,"computed":false},{"ordinal":1,"name":"user_id","type":{"name":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"orders","column":"user_id"},"identity":false,"computed":false},{"ordinal":2,"name":"total","type":{"name":"numeric(10,2)","kind":"base","schema":"pg_catalog","internalName":"numeric","precision":10,"scale":2},"nullable":false,"nullableSource":"catalog","origin":{"schema":"public","table":"orders","column":"total"},"identity":false,"computed":false}]}}}
SAMPLE_EOF
cat >tools/sidecars/Count.before.sql.json <<'SAMPLE_EOF'
{"_WARNING":"This file was generated by the 'dotnet sqlsource' tool.  Do not edit by hand; use the tool to regenerate it.","$schema":"https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json","formatVersion":1,"toolVersion":"0.1.0","queries":{"CountUsers":{"hash":"2c8ee36daa46b3b6620c7b565e37565d83f49afc2bf60df4ce58c062c71cbfc6","engine":"postgres","database":"postgres","serverVersion":"18.6","resultKind":"rows","matchesTable":null,"plan":"walked","tableMatch":"no-origin","parameters":[],"columns":[{"ordinal":0,"name":"total","type":{"name":"bigint","kind":"base","schema":"pg_catalog","internalName":"int8"},"nullable":false,"nullableSource":"expression","origin":null,"identity":null,"computed":null}]}}}
SAMPLE_EOF
cat >tools/sidecars/Count.after.sql.json <<'SAMPLE_EOF'
{"_WARNING":"This file was generated by the 'dotnet sqlsource' tool.  Do not edit by hand; use the tool to regenerate it.","$schema":"https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json","formatVersion":1,"toolVersion":"0.1.0","queries":{"CountUsers":{"hash":"c04ce736999d34722e41f40d0b8567c60caa173c33f7fc706e44928b869600bc","engine":"postgres","database":"postgres","serverVersion":"18.6","resultKind":"rows","matchesTable":null,"plan":"walked","tableMatch":"no-origin","parameters":[],"columns":[{"ordinal":0,"name":"total","type":{"name":"bigint","kind":"base","schema":"pg_catalog","internalName":"int8"},"nullable":false,"nullableSource":"expression","origin":null,"identity":null,"computed":null},{"ordinal":1,"name":"top_id","type":{"name":"integer","kind":"base","schema":"pg_catalog","internalName":"int4"},"nullable":null,"nullableSource":"no-origin","origin":null,"identity":null,"computed":null}]}}}
SAMPLE_EOF
cp tools/sidecars/Users.a.sql.json Queries/Users.sql.json
cp tools/sidecars/Count.before.sql.json Queries/Count.sql.json
echo "sample written to $(pwd); open it in the IDE, or run: dotnet build"
```

## Trigger

Before sub-phase 9.4 writes the manual's recipes and what each IDE shows, or when a user reports that one of them does not do what the manual says.  If 9.4 ships with this item open, the manual's page on the tool says, as a known issue, which IDEs the statements were checked in.
