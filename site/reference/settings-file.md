# sqlsource.json

This page is about the settings file itself: its name, what it holds, and which `.sql` files it reaches.  The keys it can hold are in [the table of all settings](settings.md#all-settings), and which place wins over it is in [which place wins](settings.md#precedence).

<a id="settings-files"></a>

## Settings files

```jsonc
// sqlsource.json, in the project's folder
{
    "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
    // No settings file in a folder above this one applies to the project.
    "root": true,
    "dialect": "postgres",
    "output": "models",
    "generatorParameters": ["keep-comments", "sort-output"],
    "typeOverrides": {
        "timestamptz": "System.DateTimeOffset",
    },
    "columnTypes": {
        "users.id": "MyApp.UserId",
    },
}
```

A settings file is a file named `sqlsource.json`, in any folder.  It sets [settings](settings.md#all-settings), such as the dialect and the output, for the `.sql` files of that folder and of every folder below it.  A settings file in a lower folder sets what differs there.  A file that says `"root": true` keeps the settings files of the folders above it out.  See [which settings files apply to a `.sql` file](#files-of-a-sql-file).

The sample is the settings file of the project's folder.  It gives every `.sql` file of the project these settings:

- SqlSource reads each `.sql` file as PostgreSQL.
- Each query gets its models and no method.
- The generated SQL keeps its comments, and the properties of each output model are sorted.
- Every `timestamptz` gets the C# type `System.DateTimeOffset`.
- Every `users.id` column gets the C# type `MyApp.UserId`.

A setting that the file does not name keeps its default.  A marker still wins over the file, as [which place wins](settings.md#precedence) says.

See: [SQLSRC015](../diagnostics.md#sqlsrc015), a file that is not a JSON object; [SQLSRC016](../diagnostics.md#sqlsrc016), a key that is not known; [SQLSRC017](../diagnostics.md#sqlsrc017), a key written twice; [SQLSRC018](../diagnostics.md#sqlsrc018), a value of the wrong JSON type; [SQLSRC019](../diagnostics.md#sqlsrc019), a value that is not valid; [SQLSRC020](../diagnostics.md#sqlsrc020), a key repeated in `typeOverrides`, `columnTypes` or `libraryTypes`, or a generator parameter given two different values in `generatorParameters`.

<a id="file-name"></a>

### The name of the file

The name of a settings file is `sqlsource.json`, in lower case.  A file named in another case, such as `SqlSource.json`, is not always read:

- **If the project file lists the file itself as an `AdditionalFiles` item, the build does not read it.**
- **If the project file does not list it, the build finds it and reads it on macOS and Windows.**  These systems compare names ignoring case.
- **On Linux, the build does not find a file that the project file does not list.**

So a project built on Linux and on macOS or Windows reads different settings.

<a id="file-contents"></a>

### What the file holds

A settings file holds one JSON object, whose members are the settings.  It can hold `//` and `/* */` comments.  It can also hold a comma after the last member of an object or the last element of an array.

- **The keys** are those of [the table](settings.md#all-settings), `root` and `$schema`, matched exactly, case included: `Dialect` is not a key.  A key that is not one is [SQLSRC016](../diagnostics.md#sqlsrc016), whose section lists them all, and a key written twice is [SQLSRC017](../diagnostics.md#sqlsrc017).
- **Each value is a JSON string, except the values of these keys:**
  - `root` is `true` or `false`.
  - `generatorParameters` is an array of strings.
  - `typeOverrides` and `libraryTypes` are each an object whose values are strings.
  - `columnTypes` is an object whose values are strings, or objects of a `type` and a `json` for [the JSON opt-in](library-types-and-json.md#json-opt-in).
- **A value of another JSON type is [SQLSRC018](../diagnostics.md#sqlsrc018).**  This includes `null`.
- **An array that has elements, none of them a string, sets nothing.**  `[]` sets nothing too.
- **SqlSource reads a string as JSON writes it.**  It ignores the white space around the string, and keeps every character inside it, such as a `;` or a `#`.  There is no escape but the escapes of JSON.
- **A string that is empty or white space sets nothing.**  It is not an error.  To leave a setting to the files above, or to its default, leave its key out.
- **`$schema` is for your editor.**  With it, the editor checks the keys and values as you write them, and offers them as you type.  Its value is `https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`.  SqlSource reads nothing of it.

<a id="files-of-a-sql-file"></a>

### Which settings files apply to a `.sql` file

The settings files of a `.sql` file are the `sqlsource.json` of its folder and of each folder above it.  They go up to the top of the drive.  The nearest comes first.

- **For each setting, the nearest file that sets it wins.**  So a file in a folder sets only what differs from the folders above it.
- **Some settings add up across the files instead.**  The entries of `typeOverrides`, `columnTypes` and `libraryTypes` add up key by key, as [type overrides](type-mapping.md#setting-type-overrides) says.  The words of `generatorParameters` add up parameter by parameter, as [generator parameters](generator-parameters.md#setting-generator-parameters) says.
- **A file that says `"root": true` is the last.**  No `sqlsource.json` in a folder above it applies to the files below it.
- **Put a file with `"root": true` in the project's folder, or at the top of the repository.**  Then a settings file above it, elsewhere on the machine, never changes your build.
- **`"root": false`, or no `root`, lets the search go on.**
- **A file whose text is not JSON gives nothing, its `root` included.**  It is [SQLSRC015](../diagnostics.md#sqlsrc015), and the search goes past it to the folders above.
- **A `.sql` file that the project lists from a folder outside the project's folder takes the settings files of its own folder.**  It takes those of the folders above its own folder too, not those of the project's folder.
- **A settings file applies to nothing if no `.sql` file and no C# file of the project is in its folder or below it.**  The build does not read such a file.

<a id="which-files-are-checked"></a>

### Which settings files the build checks

The build reads each settings file that the search of a `.sql` file or a C# file of the project reaches.  It reads the file whether or not a type claims that `.sql` file.  It reports each mistake in the file at its file, line and column.

- **A `sqlsource.json` beyond the first `"root": true` is never read.**  So it cannot fail a build.
- **The C# files that the build writes into its output and intermediate folders reach no settings file.**  This is true wherever those folders are, with one exception.  The exception is a build that names the project through a symbolic link.  It is the exception only while those folders are outside the project's folder and set by a relative path.  See [the limit](#limit-symbolic-link).
- **The C# files that a NuGet package adds to your project from the folder where NuGet keeps its packages reach no settings file.**  Polyfill is a package that adds such files.  So the build does not read a broken `sqlsource.json` above that folder, such as one in your home folder, because of these files.
- **Your own `.sql` and C# files still reach a settings file in a folder above your project, unless a `"root": true` stands between them.**
- **If your files reach a broken settings file, its mistakes fail the build or `sqlsource describe`, each on the mistakes that it reports.**  A build that [runs the tool](online-build.md#running-in-the-build) fails on the tool's mistakes too.  To prevent this, put `"root": true` in the settings file of the project's folder, or of the repository's.
- **A C# file that your project lists itself from another folder still reaches the settings files of its own folder.**
- **A value that is wrong is not set by its file.**  Until you correct it, a farther settings file, or the default, gives the setting.
- **The `sqlsource` tool reads the same settings files.**  It reports the mistakes of [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC018](../diagnostics.md#sqlsrc018) as the build does.  It also reports a value of `dialect`, `output` or `database` that is not valid, once in a run, however many projects reach the file.
- **The build reports a value that is not valid for every key but `database`, which only the tool checks.**  It also reports a repeated entry, [SQLSRC020](../diagnostics.md#sqlsrc020).

<a id="settings-file-in-the-build"></a>

### Settings files in the build

SqlSource keeps a settings file out of the output and publish folders.  Otherwise the SDK can copy it there: the Web SDK, for example, copies every `.json` file of a web project.

- **A copy rule of your own still copies a settings file.**  A rule written for `appsettings.json`, such as `<None Update="**/*.json" CopyToOutputDirectory="PreserveNewest" />`, copies every `sqlsource.json` of the project too.  To stop it, do one of these:
  - Name the files that the rule copies, as `Update="appsettings*.json"` does.
  - Follow the rule with `<None Update="**/sqlsource.json" CopyToOutputDirectory="Never" CopyToPublishDirectory="Never" />`.
- **If you add, delete, move or edit a settings file, the next build compiles again.**
- **An IDE that builds the project reads the same settings files.**
- **The project file holds no key of a settings file: an MSBuild property or item metadata named after one is not read.**  Nothing reports such a property or metadata, so move its value to a settings file.
- **SqlSource has five MSBuild properties that a project sets.**  The table of [the properties](msbuild.md#properties) lists each one and what it sets.

<a id="limits"></a>

## Limits

<a id="limit-symbolic-link"></a>

### A settings file or a `.sql` file is ignored when the project is built through a symbolic link

**What you see.**  If you name the project by a path through a symbolic link, the build can miss a settings file or a `.sql` file.  With the .NET 11 SDK, a `dotnet build` in a folder that you reached through a link names the project through it too, as `/tmp` on macOS is a link to `/private/tmp`.  Take `dotnet build ~/code/App/App.csproj`, where `~/code` leads to `/Volumes/Data/code`:

- The settings files in the project's folder, and in the folders above it up to the link's own, `~/code`, apply.
- A settings file in a folder above the link applies only if that folder is also above the folder the link leads to.  So one in `/` applies, and one in `~` does not.
- A `.sql` or C# file that your project lists by its full path gets only the settings files outside the link.  Here, those are in `~` or `/`.  It gets none of those in `~/code` or below it.  `$(MSBuildThisFileDirectory)` and `$(MSBuildProjectDirectory)` are two ways to list a file by its full path.
- A query in such a file can report [SQLSRC209](../diagnostics.md#sqlsrc209).
- If the type's C# file is listed by a relative path, the type does not find such a `.sql` file: [SQLSRC004](../diagnostics.md#sqlsrc004) or [SQLSRC005](../diagnostics.md#sqlsrc005).  The SDK lists a C# file by a relative path.
- Your build can put its output or intermediate folder outside the project's folder, by a relative path.  Then a broken `sqlsource.json` above your `"root": true` can fail the build.  `<BaseIntermediateOutputPath>../../obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>` is such a path.

**Why.**  Through a symbolic link, one folder has two paths.  The build does not always match a file by one path with a settings file by the other.

**What to do.**  Do one of these:

- Name the project by its real path.
- Build in the real folder of the project, without naming it: run `cd "$(pwd -P)"`, then `dotnet build`.
