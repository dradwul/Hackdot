# hack.

**Hackdot** is a pocket-sized, educational hacking game. You get a simulated desktop OS – *HackOS* – on your phone
and solve Capture The Flag–style missions: hunt for flags (`FLAG{...}`) by clicking around in the file explorer
or by using real commands like `ls -la`, `cat` and `grep` in the terminal.

Made for all ages and skill levels. Stuck? There's a three-step hint system.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet watch --project src/Hackdot
```

Open <http://localhost:5282>. For a mobile view, open the developer tools (F12) and toggle device mode
(Ctrl+Shift+M in Chrome/Edge).

```bash
dotnet test
```

## Tech

- Blazor WebAssembly Standalone (.NET 10) as a PWA – installable and works offline
- `Hackdot.Core` – game engine in plain C# (virtual file system, command interpreter, missions) with xUnit tests
- Lightweight custom CSS – no external UI libraries
- English by default, resx-based localization so more languages (Swedish first) can be added

## Status

- [x] Foundation: project structure, PWA, HackOS shell (desktop, windows, dock, start menu)
- [x] Localization infrastructure
- [x] Virtual file system defined in JSON (users, owners, permissions)
- [x] Terminal with command interpreter, pipes and shortcut buttons
- [x] File explorer with "show hidden files", permissions and the matching terminal command
- [x] Mission app with flag submission, three-step hints, score and saved progress
- [x] Three starter missions
