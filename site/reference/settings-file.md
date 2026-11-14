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

A settings file is a file named `sqlsource.json`, in any folder.  It sets the settings of the `.sql` files of its folder and of every folder below it.  The one above, in the project's folder, reads every `.sql` file of the project as PostgreSQL, gives each query its models and no method, keeps comments and sorts the output models' properties, and gives every `timestamptz` the C# type `System.DateTimeOffset` and every `users.id` column `MyApp.UserId`.  A setting it does not name keeps its default, and a marker still wins over it, as [which place wins](settings.md#precedence) says.

<a id="file-name"></a>

**Its name** is `sqlsource.json`, in lower case.  A file named in another case, such as `SqlSource.json`, is not read when the project file lists it itself as an `AdditionalFiles` item.  When it is not listed, the build finds it and reads it on macOS and Windows, which compare names ignoring case, and does not find it on Linux, so a project built on both would read different settings.

<a id="file-contents"></a>

**What it holds** is one JSON object, whose members are the settings.  It may hold `//` and `/* */` comments, and a comma after the last member of an object or the last element of an array.

- **The keys** are those of [the table](settings.md#all-settings), `root` and `$schema`, matched exactly, case included: `Dialect` is not a key.  A key that is not one is [SQLSRC016](../diagnostics.md#sqlsrc016), whose section lists them all, and a key written twice is [SQLSRC017](../diagnostics.md#sqlsrc017).
- **Each value is a JSON string**, except `root`, which is `true` or `false`, `generatorParameters`, an array of strings, `typeOverrides` and `libraryTypes`, each an object whose values are strings, and `columnTypes`, an object whose values are strings, or objects of a `type` and a `json` for [the JSON opt-in](library-types-and-json.md#json-opt-in).  A value of another JSON type, `null` among them, is [SQLSRC018](../diagnostics.md#sqlsrc018).
- **A string is read as JSON writes it.**  The white space around it is ignored, and every character inside it is kept, a `;` or a `#` among them; there is no escape but JSON's own.  A string that is empty or white space sets nothing, and is no error.  To leave a setting to the files above, or to its default, leave its key out.
- **`$schema`** is for your editor, which then checks the keys and values as you write them and offers them as you type: `https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`.  SqlSource reads nothing of it.

<a id="files-of-a-sql-file"></a>

**The settings files of a `.sql` file** are the `sqlsource.json` of its folder and those of each folder above it, to the top of the drive, nearest first.  For each setting the nearest that sets it wins, so a file in a folder sets only what differs from the folders above it; the entries of `typeOverrides`, `columnTypes` and `libraryTypes` add up instead, key by key, as [type overrides](type-mapping.md#setting-type-overrides) says, and the words of `generatorParameters` parameter by parameter, as [generator parameters](generator-parameters.md#setting-generator-parameters) says.  A file that says `"root": true` is the last: no `sqlsource.json` in a folder above it applies to the files below it.  Put one in the project's folder, or at the top of the repository, so that a settings file above it, elsewhere on the machine, never changes your build.  `"root": false`, or no `root`, lets the search go on.  A file whose text is not JSON ([SQLSRC015](../diagnostics.md#sqlsrc015)) gives nothing, its `root` included, so the search goes past it to the folders above.

- **A `.sql` file that the project lists from another folder**, outside the project's folder, takes the settings files of its own folder and the folders above it, not those of the project's folder.
- **A settings file in a folder that holds no `.sql` file and no C# file of the project**, in it or in a folder below it, is not read, and applies to nothing.

<a id="which-files-are-checked"></a>

**Which files are checked.**  The build reads each settings file that the search of some `.sql` file or C# file of the project reaches, whether or not a type claims that `.sql` file, and reports each mistake in it at its file, line and column.  A `sqlsource.json` beyond the first `"root": true` is never read, so it cannot fail a build.  The C# files that the build writes itself, into its output and intermediate folders, reach no settings file, wherever those folders are, except in a build that names the project through a symbolic link while those folders are outside the project's folder and set by a relative path: see [the limit](#limit-symbolic-link).  Nor do the C# files that a NuGet package adds to your project, as Polyfill does, from the folder where NuGet keeps its packages, so a broken `sqlsource.json` above that folder, in your home folder, say, is not read on their account.  Your own `.sql` and C# files still reach such a file when your project is in a folder below it, unless a `"root": true` stands between them, and its mistakes then fail the build and `sqlsource describe`: put `"root": true` in the settings file of your project's folder, or of your repository's.  A C# file that your project lists itself from another folder still reaches the settings files of its own folder.  A value that is wrong is not set by its file, so a farther settings file, or the default, gives the setting meanwhile.  The `sqlsource` tool reads the same settings files.  It reports the mistakes of [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC018](../diagnostics.md#sqlsrc018) as the build does, and a value of `dialect`, `output` or `database` that is not valid, once in a run however many projects reach the file.  The build reports a value that is not valid for every key but `database`, which only the tool checks.

<a id="settings-file-in-the-build"></a>

**In the build**, SqlSource keeps a settings file out of the output and publish folders where the SDK would copy it, as the Web SDK copies every `.json` file of a web project.  A copy rule of your own still copies it: a rule written for `appsettings.json`, such as `<None Update="**/*.json" CopyToOutputDirectory="PreserveNewest" />`, copies every `sqlsource.json` of the project too.  So name the files such a rule copies, as `Update="appsettings*.json"` does, or follow it with `<None Update="**/sqlsource.json" CopyToOutputDirectory="Never" CopyToPublishDirectory="Never" />`.  Adding, deleting, moving or editing one makes the next build compile again, and an IDE that builds the project reads the same files.  The project file holds no key of a settings file: an MSBuild property or item metadata named after one is not read, and nothing reports it, so move such a value to a settings file.  The MSBuild properties of SqlSource that a project sets are [`SqlSourceIncludeFiles`](msbuild.md#included-files), which says which `.sql` files are included, [`SqlSourcePublicExceptions`](exceptions.md#public-exceptions), which makes the exceptions of the query methods public, [`SqlSourceOnline`](msbuild.md#online-build), which says whether the build runs the `sqlsource` tool, [`SqlSourceEnvFile`](msbuild.md#setting-env-file), which names the `.env` file it reads, and [`SqlSourceRepositoryInterfaces`](repositories.md#repository-interfaces), which says whether a repository gets its interface.

See: [SQLSRC015](../diagnostics.md#sqlsrc015), a file that is not a JSON object; [SQLSRC016](../diagnostics.md#sqlsrc016), a key that is not known; [SQLSRC017](../diagnostics.md#sqlsrc017), a key written twice; [SQLSRC018](../diagnostics.md#sqlsrc018), a value of the wrong JSON type; [SQLSRC019](../diagnostics.md#sqlsrc019), a value that is not valid; [SQLSRC020](../diagnostics.md#sqlsrc020), a key repeated in `typeOverrides`, `columnTypes` or `libraryTypes`, or a generator parameter given two different values in `generatorParameters`.

<a id="limits"></a>

## Limits

<a id="limit-symbolic-link"></a>

### A settings file or a `.sql` file is ignored when the project is built through a symbolic link

- **A build that names the project through a symbolic link can miss a settings file or a `.sql` file.**  When you name the project by a path through a link, as `dotnet build ~/code/App/App.csproj` does where `~/code` leads to `/Volumes/Data/code`, the settings files in the project's folder and in the folders above it up to the link's own, `~/code`, apply.  One in a folder above the link applies only when that folder is also above the folder the link leads to: one in `/` does, and one in `~` does not.  A `.sql` or C# file that your project lists by its full path, through `$(MSBuildThisFileDirectory)` or `$(MSBuildProjectDirectory)`, say, gets only the settings files outside the link, in `~` or `/` here, and none of those in `~/code` or below it, so a query in it may report [SQLSRC209](../diagnostics.md#sqlsrc209), and such a `.sql` file is not found by its type, [SQLSRC004](../diagnostics.md#sqlsrc004) or [SQLSRC005](../diagnostics.md#sqlsrc005), when the type's C# file is listed by a relative path, as the SDK lists it.  And when your build's output or intermediate folder is outside the project's folder, set by a relative path such as `<BaseIntermediateOutputPath>../../obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>`, a broken `sqlsource.json` above your `"root": true` can fail the build.  Name the project by its real path, or build in its folder without naming it, such as `cd "$(pwd -P)"` before `dotnet build`.
