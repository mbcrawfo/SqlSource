# Describe queries as you save (watch)

Run the watch, `dotnet sqlsource describe --watch`, to describe your queries each time you save, and start it from a VS Code task, Rider or Visual Studio.

**Use this when** you edit queries and want each sidecar current without running `describe` yourself.  Use it too when you want your editor to start the watch for you.

<a id="start-the-watch"></a>

## Start the watch

If you want your sidecars to stay current while you edit your queries, start the watch in the folder of your solution or project:

```console
$ dotnet sqlsource describe --watch
billing (postgres): 0 described, 16 skipped, 0 failed
sqlsource: watching for changes; press Ctrl+C to stop
sqlsource: change detected; describing
billing (postgres): 1 described, 15 skipped, 0 failed
sqlsource: watching for changes; press Ctrl+C to stop
```

**What you get.**  The tool describes once, as `describe` does, and then waits.  When you save a `.sql` file, a settings file or a C# file of your projects, it describes again.  The sidecar of the query you saved is current a moment later.

**The rules you need.**

- Each pass is a whole `describe`, with the same connections.  It describes only the queries whose sidecar entry is no longer current, and prints its summary.  See [Describe on save](../reference/describing.md#describe-on-save).
- A pass that reports errors does not stop the watch.  Fix the error and save again.  See [Describe on save](../reference/describing.md#describe-on-save).
- After every pass, the tool writes `sqlsource: watching for changes; press Ctrl+C to stop` alone on its line.  Before every later pass, it writes `sqlsource: change detected; describing`.  A task of your editor can wait for these two lines.  See [Describe on save](../reference/describing.md#describe-on-save).
- Stop the watch with Ctrl+C, or close its terminal.  It exits with `0`, whatever its passes reported.  See [Describe on save](../reference/describing.md#describe-on-save).
- Run one watch for a repository.  A second watch, or a build while the watch runs, does no harm, but does the work twice.  See [Describe on save](../reference/describing.md#describe-on-save).
- Your editor shows the new models when it reads the sidecar again.  In Rider, if you change a query and save it in Rider, build to see its new models.  See [What your editor shows](../reference/describing.md#what-your-editor-shows).

<a id="start-the-watcher-with-your-editor"></a>

## Start the watch with your editor

Have your editor start `dotnet sqlsource describe --watch` for you, so the sidecars stay current as you save.  The next three sections do this in VS Code, Rider and Visual Studio.

**The rules you need.**

- The watch that your editor starts follows the rules of [Start the watch](#start-the-watch).  See [describe on save](../reference/describing.md#describe-on-save).
- When the watch writes a sidecar, your editor shows what it holds once the editor reads it again.  When that happens depends on the editor, as [Start the watch](#start-the-watch) says for Rider.  See [what your editor shows](../reference/describing.md#what-your-editor-shows).

<a id="vs-code"></a>

## VS Code

```jsonc
// .vscode/tasks.json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "sqlsource watch",
      "type": "shell",
      "command": "dotnet sqlsource describe --watch",
      "isBackground": true,
      "runOptions": { "runOn": "folderOpen" },
      "presentation": { "reveal": "silent", "panel": "dedicated" },
      "problemMatcher": {
        "owner": "sqlsource",
        "source": "sqlsource",
        "fileLocation": ["autoDetect", "${workspaceFolder}"],
        "pattern": {
          "regexp": "^(?!sqlsource )(\\S.*?)(?:\\((\\d+),(\\d+)\\))?\\s?: (error|warning) (SQLSRC\\d+): (.*)$",
          "file": 1, "line": 2, "column": 3, "severity": 4, "code": 5, "message": 6
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
```

**What you get.**  The watch starts when you open the folder, and runs in a terminal panel of its own.  The Problems panel lists each error that names a file, at that file.  If the error also names a line and a column of the query, the panel lists it there.  The panel takes the error from its first line.  The lines under it, such as `see:`, stay in the terminal.

**The rules you need.**

- **The first time, VS Code asks whether to allow automatic tasks in every folder you trust.**  It keeps your answer, and never runs an automatic task in a folder that it does not trust.
- **An error that names no file, such as [SQLSRC200](../diagnostics.md#sqlsrc200), stays in the terminal of the watch.**  The task does not bring that terminal forward.
- **Open the folder that holds your solution or project.**  If you open a folder above it, add `"options": { "cwd": "${workspaceFolder}/src/App" }` to the task, with the path of that folder.
- **This recipe has not been checked in VS Code.**  See [editors that have not been checked](../reference/describing.md#limit-editors-not-checked).

<a id="rider"></a>

## Rider

1. In **Run > Edit Configurations**, add a **Shell Script** configuration.
2. Choose **Script text**.
3. Write `dotnet sqlsource describe --watch` as the script text.
4. Set **Working directory** to the folder of your solution.
5. In **Settings > Tools > Startup Tasks**, add that configuration.

**What you get.**  The watch starts when the solution opens, in a terminal pane.  In that pane, the path of each error is a link to its file and line.

**The rules you need.**

- Checked in Rider 2026.2 on macOS.  See [what your editor shows](../reference/describing.md#what-your-editor-shows).

<a id="visual-studio"></a>

## Visual Studio

1. Open **View > Terminal**, the Developer PowerShell.
2. In the folder of your solution, run `dotnet sqlsource describe --watch`.

**What you get.**  The watch runs in that terminal until you stop it.

**The rules you need.**

- This has not been checked in Visual Studio.  See [editors that have not been checked](../reference/describing.md#limit-editors-not-checked).

## If it does not work

- **Saving a file starts no pass.**  The saves of an editor were checked with Rider on macOS alone.  Stop the watch, set `DOTNET_USE_POLLING_FILE_WATCHER` to `true`, and start it again.  See [saves that are not seen](../reference/describing.md#limit-saves-not-seen).
- **SQLSRC287 on Linux.**  The system's limit on watching folders was reached.  Raise the limit that the message names, or set `DOTNET_USE_POLLING_FILE_WATCHER` to `true`.  See [SQLSRC287](../diagnostics.md#sqlsrc287).
- **In Visual Studio or VS Code, the models do not change after a pass.**  Save any file, or build.  See [editors that have not been checked](../reference/describing.md#limit-editors-not-checked).
- **After a folder is deleted and made again, saves in it start no pass.**  Stop the watch and start it again.  See [folder changes that are not seen](../reference/describing.md#limit-folder-changes-not-seen).
- **In VS Code, the watch does not start when you open the folder.**  From the Command Palette, run **Tasks: Manage Automatic Tasks**, choose **Allow Automatic Tasks** and open the folder again.  Or start the watch once with **Tasks: Run Task** and `sqlsource watch`.  See [VS Code](#vs-code).
- **In VS Code, a pass reports errors and the Problems panel shows none.**  Look in the terminal of the watch.  See [VS Code](#vs-code).
