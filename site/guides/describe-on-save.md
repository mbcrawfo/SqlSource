# Describe queries as you save (watch)

<a id="start-the-watcher-with-your-editor"></a>

## Start the watcher with your editor

Have your editor start `dotnet sqlsource describe --watch` for you, so the sidecars stay current as you save.  The rules are in [describe on save](../reference/describing.md#describe-on-save), and what your editor does with a sidecar the watch writes is in [what your editor shows](../reference/describing.md#what-your-editor-shows).

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

- **It starts when you open the folder.**  The first time, VS Code asks whether to allow automatic tasks in every folder you trust, and keeps your answer; it never runs one in a folder it does not trust.  The watch runs in a terminal panel of its own.  If it does not start when you open the folder, run **Tasks: Manage Automatic Tasks** from the Command Palette, choose **Allow Automatic Tasks** and open the folder again, or start it once with **Tasks: Run Task** and `sqlsource watch`.
- **An error at a file is listed in the Problems panel**, at that file, and at the query's line and column when it has one, from the error's first line; the lines under it, such as `see:`, stay in the terminal.  An error about no file, such as [SQLSRC200](../diagnostics.md#sqlsrc200), stays in the watch's terminal, which the task does not bring forward: look there when a pass reports errors and the panel shows none.
- **Open the folder that holds your solution or project.**  If you open a folder above it, add `"options": { "cwd": "${workspaceFolder}/src/App" }` to the task, with the path of that folder.

This recipe has not been checked in VS Code.

<a id="rider"></a>

## Rider

1. **Run > Edit Configurations**, add a **Shell Script** configuration, choose **Script text** and give it `dotnet sqlsource describe --watch`, with the solution's folder as its working directory.
2. **Settings > Tools > Startup Tasks**, add that configuration.

The watch then starts when the solution opens, in a terminal pane, where the path of each error is a link to its file and line.  Checked in Rider 2026.2 on macOS.

<a id="visual-studio"></a>

## Visual Studio

Open **View > Terminal**, the Developer PowerShell, and run `dotnet sqlsource describe --watch` in the solution's folder.  It runs until you stop it with Ctrl+C or close the terminal.  This has not been checked in Visual Studio.

Rules: [describe on save](../reference/describing.md#describe-on-save), [what your editor shows](../reference/describing.md#what-your-editor-shows).
