<!-- Der einzige PR der Funktion: das Integrations-Mitglied öffnet ihn im Auftrag des Teamleiters als Entwurf (siehe .claude/teamleiter.md).
     Titel mit dem Bereich beginnen: „Kern: …“, „Spiel-KI: …“, „Darstellung: …“, „Oberfläche: …“, „Leveldesign: …“;
     arbeiten mehrere Bereiche an der Funktion, alle nennen („Kern, Oberfläche: …“). Ein PR pro Funktion. -->

## Übersicht für den Nutzer
<!-- Was kann das Spiel jetzt Neues? Wie probiere ich es aus (Startoptionen)? Was hat der Klicktest gezeigt? -->

**Aufgabe:** 
**Bereich:** Kern / Spiel-KI / Darstellung / Oberfläche / Leveldesign
**Dateien, die dieser PR anfasst:** 
**Stand:** in Arbeit

## Was neu ist

## Geprüft
- [ ] Tests grün (`dotnet run -c Release --project tests/Reichsgilde.Core.Tests`)
- [ ] Klicktest im Spiel
- [ ] `origin/main` eingemergt
- [ ] Spielbuch (`docs/spielbuch/<thema>.md`) / ARCHITEKTUR ergänzt (README nur bei großem neuen Thema), Bereichsdatei `docs/bereiche/<bereich>.md` (Offen, Entscheidungen), Messung (Kern: WIRTSCHAFTSMODELL, Spiel-KI: spiel-ki.md)
