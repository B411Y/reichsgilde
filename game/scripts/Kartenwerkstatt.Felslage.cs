using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafel rechts für gewählte Felsstücke (Fels R3) – ein Teil der <c>partial class Kartenwerkstatt</c>: oben die Knöpfe des Gizmos (Verschieben,
/// Drehen, Skalieren, Welt- oder Stückachsen, Einrasten), darunter Zahlenfelder für Lage, Drehung (Euler-Winkel) und Maßstab je Achse, dann Neigung
/// zum Hang, Einsinken und die Knöpfe „Auf den Boden“, „Am Hang ausrichten“, „Neu würfeln“ und „Kopieren“, dazu die Auswahl (P2: gleiche Form,
/// gleiches Gestein, umkehren, alle). Jede Eingabe ist ein Befehl des Kerns
/// (<see cref="Kartenentwurf.FelsTransformieren(IEnumerable{EntwurfFels}, Felswerte)"/>) und damit ein Schritt bei Rückgängig. Sind mehrere Stücke
/// gewählt, zeigen die Felder die Werte des zuletzt gewählten: Die Lage verschiebt alle um den Unterschied, die übrigen Felder setzen den Wert für alle.
/// </summary>
public partial class Kartenwerkstatt
{
    private void FelsLageTafel(EntwurfFels f, GridContainer raster)
    {
        var auswahl = _stadt.FelsAuswahl.Count > 0 ? _stadt.FelsAuswahl.ToList() : new List<EntwurfFels> { f };
        var viele = auswahl.Count > 1;
        var titel = viele ? $"{auswahl.Count} Felsstücke gewählt" : f.Bezeichnung;
        Zeile(raster, "Felsstück", Wert(titel + (viele ? $" – Werte vom letzten ({f.Bezeichnung})" : "")));

        // Gizmo: Modus, Achsen, Einrasten
        var modi = new HBoxContainer();
        modi.AddThemeConstantOverride("separation", 4);
        foreach (var (art, text, tipp) in new[]
                 {
                     (Stadtansicht.FelsGizmoArt.Verschieben, "Verschieben", "Taste 1: Pfeile ziehen, Quadrate ziehen in einer Ebene"),
                     (Stadtansicht.FelsGizmoArt.Drehen, "Drehen", "Taste 2: Ringe ziehen"),
                     (Stadtansicht.FelsGizmoArt.Skalieren, "Skalieren", "Taste 3: Würfel ziehen, der mittlere skaliert gleichmäßig"),
                 })
        {
            var knopf = new Button { Text = text, ToggleMode = true, ButtonPressed = _stadt.FelsGizmoModus == art, FocusMode = Control.FocusModeEnum.None, TooltipText = tipp };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            var a = art;
            knopf.Pressed += () => _stadt.FelsGizmoModusSetzen(a);
            modi.AddChild(knopf);
        }
        Zeile(raster, "Gizmo", modi);

        var achsen = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Taste 4: Die Achsen des Gizmos sind die der Welt oder die des Stücks (beim Skalieren immer die des Stücks)" };
        achsen.AddItem("Welt");
        achsen.AddItem("Stück");
        achsen.Select(_stadt.FelsGizmoWelt ? 0 : 1);
        achsen.ItemSelected += i => _stadt.FelsGizmoWeltSetzen(i == 0);
        Zeile(raster, "Achsen", achsen);

        var rastet = new CheckBox { Text = "Einrasten (Strg)", ButtonPressed = _stadt.FelsGizmoRastet, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Der Zug rastet ein: 0,25 m, 15° und 0,1 beim Skalieren. Gehaltene Strg-Taste tut dasselbe, ohne das Häkchen." };
        rastet.AddThemeFontSizeOverride("font_size", 13);
        rastet.Toggled += an => _stadt.FelsGizmoRastet = an;
        Zeile(raster, "", rastet);

        // Lage
        Zeile(raster, "Ost (x)", FelsFeld(-9000, 9000, 0.05, f.X, "Meter nach Osten" + (viele ? " – verschiebt alle gewählten um den Unterschied" : ""),
            v => FelsAendern(auswahl, viele ? new Felsaenderung(DX: v - f.X) : null, new Felswerte(X: v))));
        Zeile(raster, "Süd (z)", FelsFeld(-9000, 9000, 0.05, f.Z, "Meter nach Süden" + (viele ? " – verschiebt alle gewählten um den Unterschied" : ""),
            v => FelsAendern(auswahl, viele ? new Felsaenderung(DZ: v - f.Z) : null, new Felswerte(Z: v))));
        Zeile(raster, "Höhe", FelsFeld(-Felsformen.VersatzMax, Felsformen.VersatzMax, 0.05, f.Versatz,
            "Höhenversatz in Metern über der Lage aus Boden und Einsinken (±20 m)" + (viele ? " – hebt alle gewählten um den Unterschied" : ""),
            v => FelsAendern(auswahl, viele ? new Felsaenderung(DY: v - f.Versatz) : null, new Felswerte(Versatz: v))));

        // Drehung (Euler Y · X · Z)
        Zeile(raster, "Drehung (Y)", FelsFeld(0, 359.9, 0.1, f.Drehung, "Drehung um die Senkrechte in Grad (0° = Schauseite nach Süden)", v => FelsAendern(auswahl, null, new Felswerte(Drehung: v)), "°"));
        Zeile(raster, "Kippen (X)", FelsFeld(-180, 180, 0.1, f.KippenX, "Kippen um die eigene x-Achse in Grad (nach vorn oder hinten)", v => FelsAendern(auswahl, null, new Felswerte(Kippen: v)), "°"));
        Zeile(raster, "Rollen (Z)", FelsFeld(-180, 180, 0.1, f.KippenZ, "Rollen um die eigene z-Achse in Grad (zur Seite)", v => FelsAendern(auswahl, null, new Felswerte(Rollen: v)), "°"));

        // Größe
        Zeile(raster, "Maßstab", FelsFeld(Felsformen.MassstabMin, Felsformen.MassstabMax, 0.05, f.Massstab, "Gleichmäßige Größe (0,25 bis 4)", v => FelsAendern(auswahl, null, new Felswerte(Massstab: v))));
        Zeile(raster, "Breite (X)", FelsFeld(Felsformen.AchseMin, Felsformen.AchseMax, 0.05, f.AchseX, "Faktor auf den Maßstab in der Breite", v => FelsAendern(auswahl, null, new Felswerte(AchseX: v))));
        Zeile(raster, "Höhe (Y)", FelsFeld(Felsformen.AchseMin, Felsformen.AchseMax, 0.05, f.AchseY, "Faktor auf den Maßstab in der Höhe", v => FelsAendern(auswahl, null, new Felswerte(AchseY: v))));
        Zeile(raster, "Tiefe (Z)", FelsFeld(Felsformen.AchseMin, Felsformen.AchseMax, 0.05, f.AchseZ, "Faktor auf den Maßstab in der Tiefe", v => FelsAendern(auswahl, null, new Felswerte(AchseZ: v))));

        Zeile(raster, "Neigung", FelsFeld(0, 1, 0.05, f.Neigung, "Wie weit sich das Stück zum Hang legt: 0 steht senkrecht, 1 liegt ganz am Hang an", v => FelsAendern(auswahl, null, new Felswerte(Neigung: v))));
        Zeile(raster, "Einsinken", FelsFeld(0, Felsformen.EinsinkenMax, 0.05, f.Einsinken, "Anteil der Höhe unter dem Boden", v => FelsAendern(auswahl, null, new Felswerte(Einsinken: v))));

        var knoepfe = new HFlowContainer();
        knoepfe.AddThemeConstantOverride("h_separation", 6);
        knoepfe.AddThemeConstantOverride("v_separation", 4);
        _eigenschaften.AddChild(knoepfe);
        Knopf(knoepfe, "Auf den Boden (End)", () => _stadt.FelsAufsetzen(false), "Setzt die Stücke so auf den Boden, dass ihr tiefster Punkt genau so tief steckt wie das Einsinken sagt (Taste End wie in Unreal, oder B)");
        Knopf(knoepfe, "Am Hang ausrichten", () => _stadt.FelsAufsetzen(true), "Wie „Auf den Boden“, dazu an der Hangnormalen ausgerichtet, gerade (Umschalt+End oder Umschalt+B)");
        Knopf(knoepfe, "Neu würfeln", () =>
        {
            _stadt.WerkstattBefehl(() => _entwurf.FelsNeuWuerfeln(auswahl, _stadt.FelsPinsel with { Radierer = false }));
        }, "Würfelt Größe und Drehung (und was der Pinsel als Bereich hat) für die gewählten Stücke neu – wie „Reapply“ in Unreal");
        Knopf(knoepfe, "Kopieren (Strg+D)", () => _stadt.FelsKopieren(), "Kopiert die gewählten Stücke an Ort und Stelle (ein Schritt für Strg+Z); danach sind die Kopien gewählt – mit dem Gizmo wegziehen");

        // Auswahl (P2): alle einer Art, umkehren, alle
        var wahl = new HFlowContainer();
        wahl.AddThemeConstantOverride("h_separation", 6);
        wahl.AddThemeConstantOverride("v_separation", 4);
        _eigenschaften.AddChild(wahl);
        Knopf(wahl, "Gleiche Form", () => _stadt.FelsGleicheWaehlen(nachGestein: false), "Wählt alle Felsstücke mit derselben Form wie die gewählten");
        Knopf(wahl, "Gleiches Gestein", () => _stadt.FelsGleicheWaehlen(nachGestein: true), "Wählt alle Felsstücke aus demselben Gestein wie die gewählten (auch das Grundgestein der Karte)");
        Knopf(wahl, "Umkehren (Strg+I)", () => _stadt.FelsAuswahlUmkehren(), "Die gewählten Stücke fallen heraus, alle anderen Felsstücke werden gewählt");
        Knopf(wahl, "Alle (Strg+A)", () => _stadt.FelsAlleWaehlen(), "Wählt alle Felsstücke der Karte");
        Hauptmenue.Text(_eigenschaften, "Gizmo: 1 Verschieben, 2 Drehen, 3 Skalieren, 4 Welt/Stück (die Gruppen 1–4 links erst nach Esc) · Pfeiltasten schieben 0,25 m (Umschalt 1 m, Strg aufs Raster, Alt+oben/unten hebt), eine Tastenfolge ist ein Schritt für Strg+Z · "
                                        + "End oder B auf den Boden · Q/E drehen um 15° · Strg+D kopiert · "
                                        + "Umschalt+Klick wählt mehrere, Ziehen ins Leere zieht einen Rahmen (mit Umschalt: dazu). Der Zug ist erst beim Loslassen ein Schritt für Strg+Z, Esc nimmt ihn zurück. "
                                        + "WASD schiebt die Kamera.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    /// <summary>Ein Zahlenfeld für einen Wert; die Änderung ruft <paramref name="setzen"/> (Befehl). Der Wert wird nicht noch einmal gesetzt, wenn er gleich ist.</summary>
    private static Zahlfeld FelsFeld(double min, double max, double schritt, double wert, string tipp, Action<double> setzen, string suffix = "")
    {
        var feld = new Zahlfeld { MinValue = min, MaxValue = max, Step = schritt, Suffix = suffix, Value = wert, TooltipText = tipp };
        feld.ValueChanged += v => setzen(v);
        return feld;
    }

    /// <summary>Wendet einen Wert auf die gewählten Stücke an: bei mehreren relativ (<paramref name="relativ"/>), sonst absolut.</summary>
    private void FelsAendern(List<EntwurfFels> auswahl, Felsaenderung? relativ, Felswerte absolut)
    {
        // Befehl der Stadtansicht: ein Fehler (ArgumentException) steht unten in der Leiste
        _stadt.WerkstattBefehl(() =>
        {
            if (relativ is not null) _entwurf.FelsTransformieren(auswahl, relativ);
            else _entwurf.FelsTransformieren(auswahl, absolut);
        });
    }
}
