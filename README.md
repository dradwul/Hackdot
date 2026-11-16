# hack.

**Hackdot** är ett pedagogiskt hackerspel i fickformat. Du får ett simulerat skrivbords-OS – *HackOS* – i mobilen
och löser uppdrag i stil med Capture The Flag: leta efter flaggor (`FLAG{...}`) genom att klicka runt i filutforskaren
eller genom att använda riktiga kommandon som `ls -la`, `cat` och `grep` i terminalen.

Byggt för alla åldrar och nivåer. Fastnar du finns ett ledtrådssystem i tre steg.

## Kom igång

Kräver [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet watch --project src/Hackdot
```

Öppna <http://localhost:5282>. För mobilvy: öppna utvecklarverktygen (F12) och slå på enhetsläget
(Ctrl+Shift+M i Chrome/Edge).

```bash
dotnet test
```

## Teknik

- Blazor WebAssembly Standalone (.NET 10) som PWA – kan installeras och fungerar offline
- `Hackdot.Core` – spelmotor i ren C# (virtuellt filsystem, kommandotolk, uppdrag) med xUnit-tester
- Lättviktig, egen CSS – inga externa UI-bibliotek

## Status

- [x] Grund: projektstruktur, PWA, HackOS-skal (skrivbord, fönster, docka, startmeny)
- [ ] Virtuellt filsystem och banor i JSON
- [ ] Terminal med kommandotolk och snabbknappar
- [ ] Filutforskare med "visa dolda filer"
- [ ] Uppdragsapp med flagginlämning och 3-stegs ledtrådar
- [ ] Tre startbanor
