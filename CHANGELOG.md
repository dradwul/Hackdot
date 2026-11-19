# Changelog

What's new in each version of Hackdot. The version number is shown at the bottom of the desktop.

Versions follow [semantic versioning](https://semver.org/): a new **minor** version (1.**4**.0) adds missions or
features, a **patch** version (1.4.**1**) only fixes things.

## 1.5.0 – 2026-11-19

**Chapter 3** – two new missions:

- **Tick Tock** – root runs a cleanup script every minute, and anyone can edit it.
- **Night Shift** – make the backup account tell you what it may do, then use it.

In the terminal:

- `>` and `>>` write a command's output to a file; `2>` sends error messages to a file and `2>&1` sends them
  wherever the output goes.
- The computer has a clock: it moves on a minute with every command. New commands `date` and `sleep`
  (`sleep 60`, `sleep 5m`).
- Cron jobs in `/etc/crontab` really run when their time comes – as their user.
- Scripts can be run, e.g. `./script.sh` (they need `x` permission).
- New command `touch` creates an empty file.
- New guide topic: writing to files.

## 1.4.1 – 2026-11-19

- On phones, the "new version" banner now appears at the top, so it no longer covers the terminal's input.
- `history` no longer remembers the space that Tab completion leaves at the end of a command.
- If you were already past the tutorial when it was added, it is marked as optional in the mission list.

## 1.4.0 – 2026-11-18

- The version number is shown at the bottom of the desktop, with a link to this changelog.

## 1.3.0 – 2026-11-18

**Chapter 2** – four new missions:

- **Leftovers** – a locked config file, and a forgotten backup copy that isn't.
- **Locked Out** – everyone is locked out of their own files. What can the owner always do?
- **Clockwork** – follow root's scheduled jobs to where the secrets end up.
- **Set User ID** – find the one program that runs as root for everyone.

In the terminal:

- New command `chmod` – with numbers (`600`) or letters (`u+r`, `go-w`).
- Set-user-ID programs run as their owner, and show as `s` in `ls -l`.
- `find -perm` searches by permissions, e.g. `find / -perm -4000`.
- `2>/dev/null` hides error messages like "Permission denied".
- Commands can be run by their program path, e.g. `/usr/bin/base64`.
- New guide topics: leftover files, scheduled tasks (cron) and set-user-ID programs.

## 1.2.0 – 2026-11-18

- New tutorial mission, **First Steps**, for players who have never used a terminal.
- The simulated computer is now called **DotOS**, and the company you work for **DotTech**.
- The start menu logo slowly shifts between "hackdot" and "hack.".

## 1.1.0 – 2026-11-18

- **Tab completion** in the terminal: a Tab button (and the Tab key) finishes commands, file and folder names,
  and shows the choices when several match.
- A ↓ button next to ↑ to step through earlier commands.
- **Install as app** on the desktop and in the start menu – with instructions on iPhone and iPad.
- A banner tells you when a new version is ready, so the installed app never gets stuck on an old one.

## 1.0.0 – 2026-11-18

- Hackdot is online at [dradwul.github.io/Hackdot](https://dradwul.github.io/Hackdot/) and can be installed as an
  app that also works offline.

## 0.2.0 – 2026-11-17

- **Files** app: browse the same computer as the terminal, with hidden files and permissions.
- **Guide** app: lessons, a command reference and your own notes.
- Login screen and log out, `su` and `exit`, and `sudo` with real sudo rules.
- Text commands: `head`, `tail`, `wc`, `sort`, `uniq`, `cut`, `echo`, `base64` and `history`.
- "Did you mean …?" when a path only has the wrong upper/lower case.
- Missions 4–9: Behind Closed Doors, Who Keeps Knocking?, Encoded, Not Encrypted, Footprints,
  A Little Too Generous and Inside Job.

## 0.1.0 – 2026-11-16

- The first version: the DotOS desktop, a virtual file system and a terminal with `ls`, `cd`, `cat`, `grep`,
  `find` and pipes.
- **Mission** app with flags, three-step hints, points and saved progress.
- Missions 1–3: The Sloppy Intern, Hidden in the Shadows and Needle in the Log Stack.
