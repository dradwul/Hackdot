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
Spelaren använder ett simulerat skrivbords-OS ("DotOS") med terminal, filutforskare, uppdragsapp och guide.
Allt körs i webbläsaren – inget riktigt nätverk eller filsystem används.

- **Stack:** .NET 10 (LTS), Blazor WebAssembly Standalone, PWA. Inga tunga UI-bibliotek – egen, lättviktig CSS.
- **Språk:** Appen är på **engelska** (UI, terminalutskrifter, uppdrag, filinnehåll). Kod, identifierare och
  kodkommentarer skrivs också på engelska. Svenska ska kunna läggas till senare – se *Lokalisering* nedan.

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

## Versioner och changelog

- Versionen står i `<Version>` i `src/Hackdot/Hackdot.csproj` och visas längst ner på skrivbordet, under "Install as app" (med länk till
  `CHANGELOG.md` på GitHub). Bygget lägger till git-commiten efter `+`, som visas i länkens tooltip.
- Varje release (push till `main` som spelaren märker) får en ny version och en rad i `CHANGELOG.md`, skriven för
  spelaren på engelska: **minor** (1.4.0 → 1.5.0) för nya uppdrag/funktioner, **patch** (1.4.0 → 1.4.1) för rättningar.
- `ROADMAP.md` listar det som är bestämt – t.ex. att **svenska ska göras** (men inte förrän användaren säger till) –
  och idéer som inte är bestämda. Flytta det som blir klart därifrån till changelogen.

## Lokalisering

Engelska (`en`) är standardspråk och fallback. Bygg allt så att fler språk – först svenska (`sv`) – kan läggas
till utan kodändringar, i första hand för uppdragsbeskrivningar, ledtrådar och UI.

- **Inga hårdkodade användartexter** i komponenter eller tjänster. Använd `IStringLocalizer<T>` med `.resx`:
  - UI: `src/Hackdot/Resources/SharedResource.resx` via `IStringLocalizer<SharedResource>`.
  - Core (terminalens meddelanden, hjälptexter, felmeddelanden): `.resx` under `src/Hackdot.Core/Resources/`.
  - Ett nytt språk = en ny `*.sv.resx` bredvid den engelska. Nycklar namnges hierarkiskt, t.ex. `App.Terminal.Title`.
- **Uppdrag i JSON:** textfält (titel, berättelse, ledtrådar, m.m.) är *lokaliserbar text* – antingen en vanlig
  sträng (= engelska) eller ett objekt per språk: `{ "en": "…", "sv": "…" }`. Saknas språket används engelska.
- **Översätts aldrig:** kommandonamn och flaggor (`ls -la`), fil- och mappnamn, flaggor (`FLAG{…}`), lösenord och
  Linux-formaterad utdata (t.ex. kolumnerna i `ls -l`). Förklarande meddelanden och hjälptexter översätts.
- Formatera datum/tal kulturmedvetet där de visas för spelaren; i simulerad Linux-utdata används invariant format.

## Banor (uppdrag)

En bana = en JSON-fil i `src/Hackdot.Core/Data/Missions/` (`NN-namn.json`), modell: `MissionDefinition`.

- `id` är stabilt (sparade framsteg använder det) – ändra aldrig ett släppt id. `order` styr ordning och upplåsning.
- Exakt **3 ledtrådar**: 1) begreppet/idén, 2) vilket kommando/verktyg, 3) exakt var flaggan finns.
- `fileSystem` läggs ovanpå basen (`Data/FileSystems/base.json`). Långa filer genereras med
  `"generate": { "kind": "authLog", "lines": 1500, "seed": 1, "inserts": [...] }`.
- Texter får innehålla `` `backticks` `` för kommandon/sökvägar, `**fetstil**` och tomrad för nytt stycke.
- Uppdragets `skills` länkar till Guide-appen: ett kommandonamn (`ls -a` → `ls`) eller ett ämnes-`id` i
  `Data/Guide/topics.json` (t.ex. `pipes`). `GuideTests` kontrollerar att varje skill har en guide-post.
- Uppdragets `techniques` är id:n på teknikkort i `Data/Guide/cards.json` (minst ett per uppdrag) – kortet samlas när
  uppdraget klaras. `chapter` (standard 1) styr kapitelprestationerna. `TechniqueCardTests` kontrollerar kopplingarna.

## Guide-appen

- **Learn:** ämnen i `src/Hackdot.Core/Data/Guide/topics.json` (lokaliserbar text som uppdragen). Exempelkommandona
  ska fungera på basfilsystemet utan fel – det testas.
- **Commands:** byggs från terminalens egna kommandon och hjälptexter (`TerminalResources.resx`) – lägg aldrig in
  kommandodokumentation på två ställen.
- **Notes:** spelarens anteckningar sparas i `localStorage`.
- `MissionCatalog.Validate` + testerna i `MissionTests` kontrollerar att flaggan finns i en fil som spelaren kan läsa.
  Lägg till banans lösning i `EmbeddedMissions_CanBeSolvedInTheTerminal`.
- Allt som spelet gör är fiktivt (DotTech, dokumentations-IP:n enligt RFC 5737). Framställ alltid hackning som
  något man gör med tillstånd.

## Riktlinjer

- **Mobile-first:** designa för ~360–430 px bredd först, förbättra sedan för ≥768 px.
  Tryckytor minst 44×44 px, respektera `env(safe-area-inset-*)`, använd `100dvh`.
  På mobil visas max ett fönster åt gången; på desktop ett centrerat, flytande fönster.
- **Färger och mått** finns som CSS-variabler i `wwwroot/css/app.css` – använd dem i stället för hårdkodade värden.
  Komponentstilar läggs i `*.razor.css` (scoped CSS).
- **Spellogik hör hemma i `Hackdot.Core`** och ska ha tester. Blazor-komponenterna ska vara tunna.
- **Nya banor** läggs till som JSON i `src/Hackdot.Core/Data/Missions/` – ingen kodändring ska behövas för en ny bana.
- Terminalen och filutforskaren ska alltid visa **samma** virtuella filsystem, men har var sin `ShellSession`
  för den inloggade användaren (`GameService.CreateSession`) – `su` i terminalen påverkar inte filutforskaren.
- Verifiera att `dotnet build` och `dotnet test` går igenom innan du committar.
