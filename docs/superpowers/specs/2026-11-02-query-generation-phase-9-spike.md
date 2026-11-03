# The spike of phase 9, before the build - appendix

Date: 2026-11-02

Appendix to [the phase 9 design](2026-11-02-query-generation-phase-9-build-integration-design.md) of the [query generation epic](2026-10-07-query-generation-epic-design.md), written by sub-phase 9.0.  Before any code of the online build or the watcher, throwaway projects ran stand-ins of the design's targets with the packed package and the packed tool, MSBuild's `Exec` read the error forms, `FileSystemWatcher` recorded what saves raise, and the CI services' documentation was read for the variables they set.  The owner ran, from the checklist below, what only an IDE shows.  The projects and scripts were not kept; what they ran and what they answered are here.  Each answer is what was run and what it did; a question the spike could not reach names the sub-phase that must answer it.

## The owner's checklist

Items 8 to 11 of the design's 9.0, and the editors' half of item 6, need an IDE, which an agent cannot drive.  Each is run in the sample below, on the machine and IDE it names, and its answers are written into the tables here.

### Setting up

1. On each machine, from a checkout of this branch, pack the generator into a folder of its own: `dotnet pack src/SqlSource/SqlSource.csproj --output ~/sqlsource-spike-feed`.  It is `SqlSource.0.1.0-dev.nupkg`.
2. Save the script under [The sample](#the-sample) as `make-sample.sh` and run `bash make-sample.sh ~/sqlsource-spike-sample ~/sqlsource-spike-feed`.  On Windows, run it in Git Bash; or run it on the Mac and copy the folder and the feed across, then change the path in the sample's `nuget.config`.  The sample is outside the repository, so nothing of the repository's build applies to it.
3. In the sample's folder, run `dotnet build`.  It fails with one error, on purpose: `Queries/Orders.sql(1,10): error SQLSRC241: The query 'ListOrders' has no entry in the sidecar of its file, ...`, since `Orders.sql` has no sidecar yet.
4. Record the versions:

| | Version |
|----|----|
| Machine and OS | |
| .NET SDK (`dotnet --version` in the sample) | |
| Visual Studio | |
| Rider | |
| VS Code, C# extension, C# Dev Kit | |

The sample holds three queries and a tool that writes their sidecars from a process of its own, as the watcher will.  Every command below is run in a terminal in the sample's folder; a terminal inside the IDE is fine.

- `dotnet run --file tools/swap.cs -- b` rewrites `Queries/Users.sql.json` with a sidecar that says `id` is `bigint`: the model's `Id` becomes a `long`, and line 5 of `Program.cs`, `int id = new UsersDto(1, "name", null).Id;`, gets `CS0266`.  `-- a` writes the first one back, and the error goes.
- `dotnet run --file tools/swap.cs -- new` writes `Queries/Orders.sql.json`, which did not exist: `SQLSRC241` at `Orders.sql` goes.
- `dotnet run --file tools/swap.cs -- edited` writes the sidecar of the edited text of `Queries/Count.sql`, case E below.
- `dotnet run --file tools/swap.cs -- reset` puts everything back: sidecar A, `Count.sql`'s first text and its sidecar, no sidecar for `Orders.sql`.

### Item 8: IntelliSense after another process writes a sidecar

For each IDE, Visual Studio, Rider and VS Code with C# Dev Kit, and in each generator mode, the default and "Automatic": Visual Studio has the setting in Tools > Options (search "source generator"); VS Code in its settings (search "source generator"; record the setting's id); Rider has no such mode, so it has one row for each case.

Before each mode: run `swap.cs -- reset`, build once from the IDE, and wait until the IDE shows exactly one error, `SQLSRC241` at `Orders.sql`.  Open `Program.cs`, `Orders.cs` and `Queries/Count.sql` in the editor.  Then, for each case, run the command, touch nothing, and watch for 30 seconds.  If nothing changed, type a space at the end of a comment in `Program.cs` and save; if nothing changed, build.  Record what changed and after which step, and about how long after.

- **R, rewritten.**  `swap.cs -- b`.  Does `CS0266` appear on line 5 of `Program.cs`?  Then `swap.cs -- a`: does it go?
- **N, new.**  `swap.cs -- new`.  Does `SQLSRC241` go from the error list, and does `OrdersDto` in `Orders.cs` show as a known type on hover?
- **E, edited.**  In the editor, change line 2 of `Queries/Count.sql` to `SELECT count(*) AS total, max(id) AS top_id FROM users;`, exactly, and save.  Does `SQLSRC244`, "The query 'CountUsers' has changed since its entry was written", appear, and when?  Then `swap.cs -- edited`: does `SQLSRC244` go?  This is the race the research foresaw: the editor's save runs the generator before the watcher writes the sidecar.

| IDE | Mode | Case | Changed? | After what (nothing, a save, a build) | About how long |
|----|----|----|----|----|----|
| Visual Studio | default ( ) | R | | | |
| Visual Studio | default | N | | | |
| Visual Studio | default | E | | | |
| Visual Studio | Automatic | R | | | |
| Visual Studio | Automatic | N | | | |
| Visual Studio | Automatic | E | | | |
| Rider | | R | | | |
| Rider | | N | | | |
| Rider | | E | | | |
| VS Code | default ( ) | R | | | |
| VS Code | default | N | | | |
| VS Code | default | E | | | |
| VS Code | Automatic | R | | | |
| VS Code | Automatic | N | | | |
| VS Code | Automatic | E | | | |

Write the default mode's name in the brackets of its first row.

### Item 9: the design-time guard in the IDEs

The sample's `Directory.Build.targets` has two targets in the place of the online target, just before the compiler.  `SpikeProbe` has no condition, and appends a line to `obj/probe.log` each time any build of the project reaches it, with the time, `DesignTimeBuild`, `BuildingProject`, `BuildingInsideVisualStudio`, `SkipCompilerExecution`, `BuildingByReSharper`, `MSBuildRuntimeType` and `MSBuildVersion`.  `SpikeGuarded` has the design's condition, `'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'`, and appends `guarded target ran` to `obj/guarded.log`.

In each IDE:

1. Close the IDE.  Delete `obj/probe.log` and `obj/guarded.log`, if they exist, and nothing else of `obj`.
2. Open the sample in the IDE and wait until it has loaded and is idle, about a minute.  Copy both files.
3. Add a space to the end of line 2 of `Queries/Users.sql` and save; wait 15 seconds.  Copy the lines added since.
4. Add an empty line to `Sample.csproj` and save; wait 30 seconds.  Copy the lines added since.
5. Build from the IDE's own build command.  Copy the lines added since.

| IDE | Step | `obj/probe.log`, the lines added | `obj/guarded.log`, the lines added |
|----|----|----|----|
| Visual Studio | 2. open | | |
| Visual Studio | 3. save a `.sql` file | | |
| Visual Studio | 4. save the project file | | |
| Visual Studio | 5. build | | |
| Rider | 2. open | | |
| Rider | 3. save a `.sql` file | | |
| Rider | 4. save the project file | | |
| Rider | 5. build | | |
| VS Code | 2. open | | |
| VS Code | 3. save a `.sql` file | | |
| VS Code | 4. save the project file | | |
| VS Code | 5. build | | |

The guard is right if `guarded.log` gains a line at step 5 and at no other step.

### Item 10: the VS Code task

`.vscode/tasks.json` holds the design's task: it runs `tools/watcher.cs`, a stand-in for `describe --watch` that describes nothing.  It checks `Queries/*.sql` every 300 ms, and after each change prints `sqlsource: change detected; describing`, then, for each file with a line that holds `-- BROKEN`, one error on one line, `<path>(<line>,1): error SQLSRC999: stand-in error for the spike; detail: the file says BROKEN; see: https://example.invalid/sqlsrc999`, then `sqlsource: watching for changes; press Ctrl+C to stop`.  The problem matcher is `$msCompile`'s form, with the two markers as the background's begin and end.

1. Open the sample's folder in VS Code.  If it asks whether to trust the folder, record the question and say yes.  If it asks whether to allow automatic tasks, record the question's exact text and allow.
2. Does a terminal named `sqlsource watch (stand-in)` start by itself?  (Its first start compiles `watcher.cs`, a few seconds.)
3. Add a line `-- BROKEN` as line 2 of `Queries/Count.sql` and save.  Does a problem `SQLSRC999` appear in the Problems panel at `Count.sql` line 2, and is its text whole, `; detail:` and `; see:` included?
4. Remove the line and save.  Does the problem go?
5. Close and reopen the folder.  Does the task start again without asking?
6. In a copy of the folder that VS Code does not trust (open it and choose "Don't Trust"): does the task start?

| Step | What happened |
|----|----|
| 1. trust and automatic tasks | |
| 2. starts on open | |
| 3. problem appears | |
| 4. problem cleared | |
| 5. reopen | |
| 6. untrusted | |

### Item 11: Rider's startup task

1. Open the sample in Rider.  In Run > Edit Configurations, add a configuration that runs `dotnet run --file tools/watcher.cs` in the sample's folder: a Shell Script one with the script text, or whichever kind the current Rider offers for a command.  Record its kind.
2. In Settings > Tools > Startup Tasks, add it.  Record the setting's exact name and place, if it is not that.
3. Close and reopen the solution.  Does the watcher start, and where does its output show?
4. Add `-- BROKEN` as line 2 of `Queries/Count.sql` and save.  Does the error line show in the output, and is its path a link that opens the file at the line?

| Step | What happened |
|----|----|
| 1. kind of configuration | |
| 2. the setting's name and place | |
| 3. starts on open | |
| 4. the error, and whether it is a link | |

### Item 6, the editors' half: the events of a save

`tools/events.cs` prints every event `FileSystemWatcher` raises under the sample's folder, but `bin` and `obj`, with the time in milliseconds.

1. In a terminal in the sample's folder, run `dotnet run --file tools/events.cs` and wait for `watching ...`.
2. In each editor at hand (VS Code, Rider, Visual Studio), open `Queries/Users.sql`, add a space at the end of line 2, and save.  Copy what the terminal printed for that save.
3. For Rider, record whether Settings > Appearance & Behavior > System Settings > "Use safe write" (or "Back up files before saving") is on.

| OS | Editor | Events printed for one save |
|----|----|----|
| macOS | VS Code | |
| macOS | Rider | |
| Windows | Visual Studio | |
| Windows | VS Code | |
| Windows | Rider | |

### The sample

<details>
<summary><code>make-sample.sh</code></summary>

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

</details>
