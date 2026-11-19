# Roadmap

What's planned for Hackdot, and ideas that might come later. Done work moves to [CHANGELOG.md](CHANGELOG.md).

## Decided – will be done

### Swedish

Hackdot will be available in **Swedish** as well as English. Until then, English only is fine.

The groundwork is in place: no hard-coded player texts, `.resx` files for the UI and the terminal, and
localizable texts (`{ "en": "…", "sv": "…" }`) in missions and guide topics. What's left:

- `SharedResource.sv.resx` (UI) and `TerminalResources.sv.resx` (terminal messages and help).
- Swedish texts in every mission and guide topic, and for ranks and achievements.
- A language picker in the start menu: the browser's language by default, the choice remembered.
- Never translated: commands and flags, file and folder names, `FLAG{…}`, passwords and Linux-style output.

## Ideas – not decided yet

### Rewards

- **Themes**: terminal colours (amber, retro green, light), wallpapers – unlocked by rank.
- **Story**: a Mail or Chat app in DotOS where DotTech's people send missions and react to what you did.
- **Bonus missions**: harder challenges without hints, unlocked by rank or achievements.
- **Fun commands**: easter eggs to unlock, like a `dotsay` mascot, `fortune` or a "matrix" screensaver.
- **Employee badge**: a DotTech ID card with name, rank and achievements – to show or share as an image.
- **Stars per mission**: 1–3 ★ – solved, without hints, with few commands.
- **Certificates**: a printable certificate per chapter.

### Bigger features

- **Attack & defend**: plant "footprints" in a red-team exercise, then find and clean them up as the blue team.
  Needs goals that check the system's state (not just a flag), and `rm`, `cp`, `mv` and a text editor.
- **Text editor app**: edit files comfortably on a phone (also needed for attack & defend).
- **Network**: several machines per mission, with `ping`, `ssh` and a simplified `nmap`.
- **Browser app**: fake websites to explore – page source, `robots.txt`, cookies, and the ideas behind web
  vulnerabilities.
- **Mission editor**: create your own missions in the app and share them with a link – for teachers and friends.
- **GitHub Releases** made automatically from this changelog.
