# Hackdot – instruktioner för Claude

## ⚠️ Git: endast kontot `dradwul` får committa

Det här är ett personligt projekt. **Alla commits ska göras av GitHub-kontot `dradwul` – ingen annan identitet.**

- Repot har en lokal git-identitet (`git config --local user.name` / `user.email`) som pekar på `dradwul`.
  Kontrollera den innan **varje** commit:
  ```bash
  git config --local user.name
  git config --local user.email
  ```
  Saknas den, eller pekar den på något annat än `dradwul`: **committa inte** – fråga användaren.
- Committa aldrig med jobbidentiteten (t.ex. en `@iosoft.se`-adress) eller med den globala git-configen.
  Använd inte heller `--author`, `-c user.name=…` eller miljövariabler för att byta identitet.
- Lägg **inte** till `Co-Authored-By:`-rader eller andra medförfattare i commit-meddelanden. `dradwul` är enda författare.
- Pusha bara när användaren ber om det.

## Projektet

Hackdot (logga: `hack.` / `hackdot`) är ett mobile-first, pedagogiskt CTF-/etisk hackning-spel.
Spelaren använder ett simulerat skrivbords-OS ("HackOS") med terminal, filutforskare, uppdragsapp och guide.
Allt körs i webbläsaren – inget riktigt nätverk eller filsystem används.

- **Stack:** .NET 10 (LTS), Blazor WebAssembly Standalone, PWA. Inga tunga UI-bibliotek – egen, lättviktig CSS.
- **Språk:** UI-texter på svenska, kod/identifierare på engelska. Kommentarer får vara på svenska.

```
Hackdot.slnx
src/
  Hackdot/              Blazor WASM PWA (UI)
    Components/Shell/   Skrivbord, fönster, docka, logga, ikoner
    Components/Apps/    Apparna: Mission, Terminal, Files, Guide
    Services/           UI-tjänster (t.ex. ShellService = fönsterhantering)
    wwwroot/            index.html, app.css (globala tokens), manifest, service worker, ikoner
  Hackdot.Core/         Ren C#-logik utan Blazor-beroenden – ska vara enhetstestbar
    Models/             Domänmodeller (virtuellt filsystem, uppdrag, …)
    Services/           Logik (VirtualPath, filsystem, kommandotolk, spelstatus, …)
    Data/Missions/      Banor som JSON – bäddas in som EmbeddedResource
tests/
  Hackdot.Core.Tests/   xUnit-tester för Hackdot.Core
```

## Kommandon

```bash
dotnet build Hackdot.slnx
dotnet test Hackdot.slnx
dotnet watch --project src/Hackdot      # http://localhost:5282
```

## Riktlinjer

- **Mobile-first:** designa för ~360–430 px bredd först, förbättra sedan för ≥768 px.
  Tryckytor minst 44×44 px, respektera `env(safe-area-inset-*)`, använd `100dvh`.
  På mobil visas max ett fönster åt gången; på desktop ett centrerat, flytande fönster.
- **Färger och mått** finns som CSS-variabler i `wwwroot/css/app.css` – använd dem i stället för hårdkodade värden.
  Komponentstilar läggs i `*.razor.css` (scoped CSS).
- **Spellogik hör hemma i `Hackdot.Core`** och ska ha tester. Blazor-komponenterna ska vara tunna.
- **Nya banor** läggs till som JSON i `src/Hackdot.Core/Data/Missions/` – ingen kodändring ska behövas för en ny bana.
- Terminalen och filutforskaren ska alltid visa **samma** virtuella filsystem.
- Verifiera att `dotnet build` och `dotnet test` går igenom innan du committar.
