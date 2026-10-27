# SeeSharp

**Deutsch.** SeeSharp ist ein interaktiver C#-Kurs: Lesen, Fragen und Programmieraufgaben, die
per Unit-Tests geprüft werden. Alles läuft lokal; die Oberfläche öffnet sich im Browser, der Code
entsteht in Rider oder VS Code.

**English.** SeeSharp is an interactive C# course: reading, questions and coding exercises graded
by unit tests. Everything runs locally; the UI opens in the browser, the code is written in Rider
or VS Code.

Voraussetzung / requires: .NET 10 SDK.

```
dotnet tool install -g SeeSharp.Cli
seesharp init      # Kurs nach ~/seesharp-course klonen, Buch laden / clone the course, download the book
seesharp           # starten / start
```

`seesharp init --dir <Ordner>` wählt einen anderen Kursordner; erneut aufgerufen aktualisiert es
den Kurs, ohne eigene Änderungen anzutasten. / picks another course folder; run again to update
the course without touching your own changes. Rider- und VS-Code-Plugins: *SeeSharp einrichten* /
*Set up SeeSharp* erledigt alles davon.

Source: https://github.com/wollume-studios/seesharp · Course: https://github.com/wollume-studios/seesharp-course ·
Licence CC BY-NC-SA 4.0.
