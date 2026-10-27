# SeeSharp

Interactive C# training tool for small teams of trainees. Installed as a .NET global
tool (`seesharp`), it launches a local Blazor Server UI, reads course content
from a separate repository on disk, and grades coding exercises by running unit
tests. Trainees write code in JetBrains Rider, VS Code or Visual Studio; SeeSharp
opens the right file at the right line.

Course content lives in a separate repository
([wollume-studios/seesharp-course](https://github.com/wollume-studios/seesharp-course))
and is never compiled into this tool.

## Install

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Git is not needed.

```
dotnet tool install -g SeeSharp.Cli
seesharp init
seesharp
```

`seesharp init` clones the course to `%USERPROFILE%\seesharp-course` (change it with
`--dir <folder>`), downloads the book PDFs and writes the settings. `seesharp`
starts the app and opens it in the browser. Update later with
`dotnet tool update -g SeeSharp.Cli`.

### IDE plugins (optional)

- **Rider:** Settings → Plugins → ⚙ → Manage Plugin Repositories → add
  `https://wollume-studios.github.io/seesharp/updatePlugins.xml`, then install
  "SeeSharp" from the Marketplace tab.
- **VS Code:** download `seesharp-*.vsix` from the
  [latest release](https://github.com/wollume-studios/seesharp/releases/latest) and
  run `code --install-extension <file>.vsix`.

Both plugins can also run the setup above for you (*Set up SeeSharp*).

## Licence

This repository is licensed under
[Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International](https://creativecommons.org/licenses/by-nc-sa/4.0/)
(see [LICENSE](LICENSE)).

The curriculum is based on *Einführung in das Programmieren mit C# 14* by
Bernhard Baltes-Götz and Paul Frischknecht (© 2026), published under the same
licence. Level summaries and exercises in the course content are condensed
derivative works of that manuscript; the licence requires attribution of the
authors, non-commercial use, and share-alike distribution of derivatives.
