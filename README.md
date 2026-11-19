# hack.

**Hackdot** is a pocket-sized, educational hacking game. You get a simulated desktop OS – *DotOS* – on your phone
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

## Publishing

Every push to `main` is tested and published to GitHub Pages by `.github/workflows/deploy.yml`.
One-time setup in the GitHub repository: **Settings → Pages → Source: GitHub Actions**.
The site then lives at `https://<user>.github.io/<repo>/` and can be installed as an app (PWA) that also
works offline. `index.html` uses a relative `<base href="./">`, so no path needs to be configured.

## Tech

- Blazor WebAssembly Standalone (.NET 10) as a PWA – installable and works offline
- `Hackdot.Core` – game engine in plain C# (virtual file system, command interpreter, missions) with xUnit tests
- Lightweight custom CSS – no external UI libraries
- English by default, resx-based localization so more languages (Swedish first) can be added

## Status

- [x] Foundation: project structure, PWA, DotOS shell (desktop, windows, dock, start menu)
- [x] Localization infrastructure
- [x] Virtual file system defined in JSON (users, owners, permissions)
- [x] Terminal with command interpreter, pipes, Tab completion and shortcut buttons (ls, cd, cat, head, tail, grep, find,
      wc, sort, uniq, cut, echo, base64, chmod, touch, date, sleep, su, sudo, history, …), set-user-ID programs,
      redirection (`>`, `>>`, `2>`, `2>&1`), runnable scripts and a clock that runs cron jobs
- [x] File explorer with "show hidden files", permissions and the matching terminal command
- [x] Mission app with flag submission, three-step hints, score and saved progress
- [x] Ranks (Intern → Chief Security Officer) and 15 achievements, with a notice when you earn one
- [x] A guided tutorial for first-timers, then nine missions: plain-text passwords, hidden files, log searching, su with a found password,
      counting a brute-force attack, decoding base64, following an attacker's shell history,
      abusing a sudo wildcard, and a final multi-step chain to root
- [x] Chapter 2, four more missions: leftover backup files, chmod and file ownership, a leaky cron job,
      and a set-user-ID program that runs as root
- [x] Chapter 3, two more missions: a root cron script anyone can edit, and a chain through another
      user's scheduled job and sudo rules
- [x] Version number and a link to the changelog at the bottom of the desktop
- [x] Guide app: concepts, command reference built from the terminal, and notes
- [x] Login screen, log out, su/exit with hidden password prompts
