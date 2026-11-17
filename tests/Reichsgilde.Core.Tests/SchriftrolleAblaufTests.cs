using Reichsgilde;

/// <summary>
/// Ablauf der 3D-Schriftrollen der Meldungsleiste ohne Godot (<c>game/scripts/SchriftrolleAblauf.cs</c>): Reihenfolge Aufrollen → offen → Zurollen → weg,
/// Symbolzeiten des Modells, Sprung ohne Nachschwingen, Neigung nie nach unten, Pendeln höchstens ±5° und je Rolle versetzt, Stillstand ohne Bewegung.
/// </summary>
static class SchriftrolleAblaufTests
{
    static readonly RollenMasse Masse = new(ClipSekunden: 0.9, SymbolAb: 0.233, NeigungGrad: 14, PendelGrad: 5, HuepfHoehe: 0.07, HuepfSekunden: 0.26,
                                            SchlussHuepfenAb: 0.5, AusblendSekunden: 0.16);

    static SchriftrolleAblauf Neu(double x = 0.1, double y = 0.4, double z = 0.8, double tempo = 0.5) => new(Masse, x, y, z, tempo);

    static void Laufen(SchriftrolleAblauf a, double sekunden, Action<SchriftrolleAblauf>? jeBild = null)
    {
        for (var i = 0; i < (int)Math.Round(sekunden * 60); i++)
        {
            a.Weiter(1 / 60.0, true);
            jeBild?.Invoke(a);
        }
    }

    static void Wahr(bool b, string was) { if (!b) throw new Exception("Fehlgeschlagen: " + was); }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Schriftrolle: erscheint geschlossen, rollt auf, bleibt offen", () =>
        {
            var a = Neu();
            Pruefe.Gleich(RollenZustand.Zu, a.Zustand);
            a.Erscheinen(true);
            Pruefe.Gleich(RollenZustand.Aufrollen, a.Zustand);
            Pruefe.Gleich("aufrollen", a.Clip);
            Pruefe.Ungefaehr(0, a.Clipzeit, 1e-9);
            Wahr(!a.SymbolSichtbar, "Symbol am Anfang aus");
            Laufen(a, 0.2);
            Wahr(!a.SymbolSichtbar, "Symbol vor 0,233 s aus");
            Laufen(a, 0.1);
            Wahr(a.SymbolSichtbar, "Symbol ab 0,233 s an");
            Laufen(a, 1.0);
            Pruefe.Gleich(RollenZustand.Offen, a.Zustand);
            Pruefe.Ungefaehr(0.9, a.Clipzeit, 1e-9);
            Laufen(a, 30);
            Pruefe.Gleich(RollenZustand.Offen, a.Zustand, "offen, bis die Meldung erledigt ist");
        }),
        ("Schriftrolle: Schließen rollt zu, Symbol bis 0,233 s, dann weg", () =>
        {
            var a = Neu();
            a.Erscheinen(true);
            Laufen(a, 2);
            a.Schliessen(true);
            Pruefe.Gleich(RollenZustand.Zurollen, a.Zustand);
            Pruefe.Gleich("zurollen", a.Clip);
            Wahr(a.SymbolSichtbar, "Symbol am Anfang des Zurollens an");
            Laufen(a, 0.3);
            Wahr(!a.SymbolSichtbar, "Symbol nach 0,233 s aus");
            Pruefe.Ungefaehr(1, a.Deckkraft, 1e-9);
            Laufen(a, 0.7);
            Pruefe.Gleich(RollenZustand.Weg, a.Zustand);
            Pruefe.Ungefaehr(0, a.Deckkraft, 1e-9);
            a.Schliessen(true); // ein zweites Mal ändert nichts
            Pruefe.Gleich(RollenZustand.Weg, a.Zustand);
        }),
        ("Schriftrolle: Sprung beim Erscheinen und vor dem Verschwinden, einmal hoch und zurück", () =>
        {
            var a = Neu();
            a.Erscheinen(true);
            double hoechste = 0, vorher = 0;
            var wechsel = 0; // Richtungswechsel der Höhe: genau einer (Scheitel), sonst wäre es gummiartig
            var steigt = true;
            Laufen(a, 0.9, x =>
            {
                Wahr(x.Hoehe >= 0, "nie unter die Ruhelage");
                hoechste = Math.Max(hoechste, x.Hoehe);
                if (steigt && x.Hoehe < vorher - 1e-12) { steigt = false; wechsel++; }
                else if (!steigt && x.Hoehe > vorher + 1e-12) { steigt = true; wechsel++; }
                vorher = x.Hoehe;
            });
            Wahr(hoechste > 0.06 && hoechste <= 0.07 + 1e-9, $"Scheitel nahe der Sprunghöhe, war {hoechste}");
            Pruefe.Gleich(1, wechsel);
            Pruefe.Ungefaehr(0, a.Hoehe, 1e-12);
            Laufen(a, 1);
            a.Schliessen(true);
            Laufen(a, 0.45);
            Pruefe.Ungefaehr(0, a.Hoehe, 1e-12);
            Laufen(a, 0.15);
            Wahr(a.Hoehe > 0, "Sprung am Ende des Zurollens");
        }),
        ("Schriftrolle: Neigung nach oben, Pendeln höchstens fünf Grad je Achse, nie nach unten", () =>
        {
            var a = Neu();
            a.Erscheinen(true);
            Pruefe.Ungefaehr(14, a.Drehung.X, 1e-9);
            double bewegt = 0;
            Laufen(a, 120, x =>
            {
                var (dx, dy, dz) = x.Drehung;
                Wahr(dx > 0, "kippt nie nach unten");
                Wahr(Math.Abs(dx - 14) <= 5 + 1e-9 && Math.Abs(dy) <= 5 + 1e-9 && Math.Abs(dz) <= 5 + 1e-9, $"höchstens ±5°, war {dx - 14}/{dy}/{dz}");
                bewegt = Math.Max(bewegt, Math.Abs(dy));
            });
            Wahr(bewegt > 1, "offene Rollen pendeln sichtbar");
            // Eine Neigung, die kleiner ist als das Pendeln, kippt trotzdem nie nach unten
            var flach = new SchriftrolleAblauf(Masse with { NeigungGrad = 1 }, 0.3, 0.6, 0.9, 0.2);
            flach.Erscheinen(true);
            Laufen(flach, 60, x => Wahr(x.Drehung.X >= 0, "auch flach nie nach unten"));
        }),
        ("Schriftrolle: jede Rolle pendelt versetzt", () =>
        {
            var a = Neu(0.1, 0.4, 0.8, 0.2);
            var b = Neu(0.6, 0.9, 0.3, 0.9);
            a.Erscheinen(true);
            b.Erscheinen(true);
            double abstand = 0;
            for (var i = 0; i < 600; i++)
            {
                a.Weiter(1 / 60.0, true);
                b.Weiter(1 / 60.0, true);
                abstand = Math.Max(abstand, Math.Abs(a.Drehung.Y - b.Drehung.Y));
            }
            Wahr(abstand > 1, $"zwei Rollen laufen nicht gleich, größter Abstand {abstand}");
        }),
        ("Schriftrolle: ohne Bewegung sofort offen und still, Schließen sofort weg", () =>
        {
            var a = Neu();
            a.Erscheinen(false);
            Pruefe.Gleich(RollenZustand.Offen, a.Zustand);
            Wahr(a.SymbolSichtbar, "Symbol sofort an");
            for (var i = 0; i < 300; i++) a.Weiter(1 / 60.0, false);
            Pruefe.Ungefaehr(14, a.Drehung.X, 1e-9);
            Pruefe.Ungefaehr(0, a.Drehung.Y, 1e-9);
            Pruefe.Ungefaehr(0, a.Hoehe, 1e-12);
            a.Schliessen(false);
            Pruefe.Gleich(RollenZustand.Weg, a.Zustand);
            // Bewegung mitten im Aufrollen abgeschaltet: die Rolle steht beim nächsten Bild offen
            var b = Neu();
            b.Erscheinen(true);
            b.Weiter(0.1, true);
            b.Weiter(0.016, false);
            Pruefe.Gleich(RollenZustand.Offen, b.Zustand);
        }),
        ("Schriftrolle: mitten im Aufrollen geschlossen rollt von dort zurück; Hüpfen nur offen", () =>
        {
            var a = Neu();
            a.Erscheinen(true);
            Laufen(a, 0.3);
            a.Schliessen(true);
            Pruefe.Ungefaehr(0.6, a.Clipzeit, 0.02);
            var b = Neu();
            b.Erscheinen(true);
            Laufen(b, 2);
            Pruefe.Ungefaehr(0, b.Hoehe, 1e-12);
            b.Huepfen();
            Laufen(b, 0.13);
            Wahr(b.Hoehe > 0.05, "eine weitere Meldung lässt die offene Rolle kurz springen");
            Laufen(b, 0.2);
            Pruefe.Ungefaehr(0, b.Hoehe, 1e-12);
        }),
    };
}
