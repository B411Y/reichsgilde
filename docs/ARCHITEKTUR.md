# Architektur – wie Reichsgilde aufgebaut ist

Dieses Dokument erklärt die Grundidee hinter dem Code. Es wächst mit dem Projekt.

## Die wichtigste Regel: Simulation und Darstellung sind getrennt

```
┌──────────────────────────────┐        ┌──────────────────────────────┐
│  game/  (Godot)              │        │  src/Reichsgilde.Core        │
│                              │ ruft   │                              │
│  • zeigt an                  │ ─────▶ │  • entscheidet, was passiert │
│  • nimmt Eingaben entgegen   │        │  • kennt Godot NICHT         │
│  • Szenen, Grafik, Sound     │ ◀───── │  • Zeit, Markt, Bürger, ...  │
│                              │ liest  │                              │
└──────────────────────────────┘        └──────────────────────────────┘
                                                      ▲
                                                      │ prüft
                                        ┌──────────────────────────────┐
                                        │  tests/ (automatische Tests) │
                                        └──────────────────────────────┘
```

**Warum?**

1. **Multiplayer:** Später läuft der Simulationskern auf einem Server, die Spieler sehen nur die Darstellung. Weil der Kern Godot nicht kennt, können wir ihn dort unverändert einsetzen.
2. **Testbarkeit:** Spiellogik lässt sich in Sekunden prüfen, ohne das Spiel zu starten (z. B. „Ergeben 15 Minuten wirklich genau ein Jahr?“).
3. **Übersicht:** Wer eine Spielregel sucht, schaut in `src/`. Wer wissen will, wie etwas aussieht, schaut in `game/`.

## Das Zeitsystem

| Begriff | Bedeutung |
|---|---|
| **Tick** | Eine Spielminute. Kleinste Zeiteinheit der Simulation. |
| **Spieltag = Runde = Spieljahr** | 1440 Ticks, beginnt um 06:00 Uhr (wie im Original „Die Gilde“). |
| **Jahreszeit** | Wechselt von Jahr zu Jahr: Frühling 1400 → Sommer 1401 → Herbst 1402 → Winter 1403 → … |
| **Tagesphase** | Zunftglocke (06–11), Hoher Mittag (11–16), Vesper (16–21), Dunkle Nacht (21–06) |
| **Taktgeber** | Rechnet Echtzeit in Ticks um. Hier sitzen Rundendauer, Tempo und Pause. |

Die Klasse `Spielzeit` weiß **nichts** über Echtzeit. Ob eine Runde 5 oder 30 Minuten dauert, ändert nur, *wie schnell* Ticks kommen – nicht, *was* in einem Tick passiert. Dadurch läuft die Simulation bei jedem Tempo gleich ab.

### Ablauf pro Bild (ca. 60× pro Sekunde)

```
Godot: _Process(delta)
  └─▶ Simulation.Aktualisieren(delta)
        └─▶ Taktgeber: „delta Sekunden = n Ticks“
              └─▶ n × Simulation.Tick()
                    ├─ Zeit eine Minute weiter
                    ├─ (später: Produktion, Bürger-KI, Markt ...)
                    └─ Ereignisse: PhaseBegonnen, JahrBeendet
  └─▶ Anzeige aktualisieren
```

## Namenskonventionen

- Fachbegriffe im Code sind **deutsch** (`Spielzeit`, `Jahreszeit`, `Zunftglocke`), passend zum Setting.
- Umlaute werden in Bezeichnern umschrieben (`Fruehling`), in Texten für Spieler bleiben sie erhalten.
- Kommentare sind deutsch und erklären das **Warum**, nicht nur das Was.
