using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafeln rechts für Wasser (Kartenwerkstatt, Schritt 5c) – ein Teil der <c>partial class Kartenwerkstatt</c> (Katalog: Kartenwerkstatt.Tafeln.cs, Zeichnen und Wählen
/// in der Stadt: Stadtansicht.Kartenwerkstatt.Wasser.cs):
/// <list type="bullet">
/// <item><b>Fluss und Bach</b> (<see cref="FlussTafel"/>): Name, Art, Breite und Tiefe für den ganzen Lauf, Strömung, „Richtung umkehren“; dazu Breite und Tiefe an einem gewählten Stützpunkt
/// (ein Griff in der Stadt oder die Nummer hier) – so wird ein Fluss zur Mündung breiter oder an einer Furt flacher.</item>
/// <item><b>See und Teich</b> (<see cref="SeeTafel"/>): Name, Art, Wasserstand (automatisch 0,4 m unter dem tiefsten Ufer oder fest) und Tiefe.</item>
/// <item><b>Fährstelle</b> (<see cref="FaehreTafel"/>): Name, Länge, welches Gewässer sie quert, wie lange Karren warten.</item>
/// <item><b>Werkzeuge</b> (<see cref="WasserWerkzeugTafel"/>): die Einstellungen neuer Flüsse (Breite) und ein Satz, wie es geht.</item>
/// </list>
/// Jede Änderung ist ein Befehl des Entwurfs; Brücken, Furten und das Bett folgen von selbst (die Prüfung meldet, was nicht geht).
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Steht in <see cref="_gezeigt"/>, solange die Tafel die Einstellungen der Wasserwerkzeuge zeigt.</summary>
    private static readonly object WasserMarke = new();

    private static string Meter(double wert) => $"{Komma(wert, "0.0#")} m";

    /// <summary>Die Nummer eines Gewässers in der Liste <c>wasser</c> (wie <see cref="Wasserlauf.Nummer"/>) – −1, wenn es nicht (mehr) im Entwurf ist.</summary>
    private int GewaesserNummer(EntwurfTeil g)
    {
        for (var i = 0; i < _entwurf.Gewaesser.Count; i++)
            if (ReferenceEquals(_entwurf.Gewaesser[i], g)) return i;
        return -1;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fluss und Bach
    // ═════════════════════════════════════════════════════════════════════════

    private void FlussTafel(EntwurfFluss f, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(f.Name, text => _entwurf.GewaesserUmbenennen(f, text)));
        var art = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Nur die Beschreibung und die Vorgabe für Breite und Strömung – Fluss 12 m, Bach 3 m" };
        art.AddItem("Fluss");
        art.AddItem("Bach");
        art.Select(f.Art == "bach" ? 1 : 0);
        art.ItemSelected += i => _entwurf.GewaesserArtSetzen(f, i == 1 ? "bach" : "fluss");
        Zeile(raster, "Art", art);
        Zeile(raster, "Breite", Schieber(Wasserform.MindestBreite, Math.Max(60, f.BreiteWirksam), 0.5, f.BreiteWirksam, Meter,
            wert => _entwurf.FlussBreiteSetzen(f, -1, wert), "Die Breite aller Stützpunkte ohne eigene Breite"));
        var tiefe = f.Tiefe ?? Wasserform.Vorgabetiefe(f.BreiteWirksam);
        Zeile(raster, "Tiefe", Schieber(Wasserform.MindestTiefe, Math.Max(8, tiefe), 0.05, tiefe, Meter, wert => _entwurf.FlussTiefeSetzen(f, -1, wert),
            "Die Tiefe in der Mitte. Unter 0,5 m ist eine Furt möglich – dort fahren Karren langsamer durch; ohne Angabe wächst sie mit der Breite"));
        Zeile(raster, "Strömung", Schieber(0, 5, 0.05, f.StroemungWirksam, v => $"{Komma(v, "0.00")} m/s", wert => _entwurf.FlussStroemungSetzen(f, wert),
            "Wie schnell das Wasser in der Mitte fließt (zum Ufer hin halb so schnell) – man sieht es an den Wellen"));
        var lauf = _entwurf.Wasserform.Laeufe.FirstOrDefault(l => l.Nummer == GewaesserNummer(f));
        Zeile(raster, "Länge", Wert(lauf is null ? $"{f.Punkte.Count} Stützpunkte" : $"{Math.Round(lauf.Laenge)} m · {f.Punkte.Count} Stützpunkte"));

        var umkehren = new HBoxContainer();
        umkehren.AddThemeConstantOverride("separation", 8);
        Knopf(umkehren, "Richtung umkehren", () => _entwurf.FlussUmkehren(f), "Die Pfeile zeigen die Fließrichtung: vom ersten zum letzten Stützpunkt. Der Fluss fließt nie bergauf – ein Fluss gegen das Gefälle gräbt sich ein.");
        _eigenschaften.AddChild(umkehren);

        // ein Stützpunkt: Breite und Tiefe dort
        Hauptmenue.Text(_eigenschaften, "Stützpunkt", 15, Gold);
        var punkt = _stadt.WerkstattGriffIndex;
        var punktRaster = new GridContainer { Columns = 2 };
        punktRaster.AddThemeConstantOverride("h_separation", 10);
        punktRaster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(punktRaster);
        var nummer = new Zahlfeld { MinValue = 0, MaxValue = f.Punkte.Count, Step = 1, Value = punkt + 1, TooltipText = $"Nummer des Stützpunkts (1 bis {f.Punkte.Count}, in Fließrichtung); 0: keiner – oder in der Stadt einen Griff anklicken" };
        nummer.ValueChanged += wert =>
        {
            _stadt.WerkstattGriffWaehlen((int)wert - 1);
            Callable.From(() => TeilTafel(f)).CallDeferred();
        };
        Zeile(punktRaster, "Nummer", nummer);
        if (punkt >= 0 && punkt < f.Punkte.Count)
        {
            var (eigeneBreite, eigeneTiefe) = (f.Breiten[punkt] is not null, f.Tiefen[punkt] is not null);
            Zeile(punktRaster, "Breite", Schieber(Wasserform.MindestBreite, Math.Max(60, f.BreiteAn(punkt)), 0.5, f.BreiteAn(punkt), Meter,
                wert => _entwurf.FlussBreiteSetzen(f, punkt, wert), eigeneBreite ? "Eigene Breite an diesem Punkt" : "Noch ohne eigene Breite – die des Flusses; ziehen setzt eine"));
            Zeile(punktRaster, "Tiefe", Schieber(Wasserform.MindestTiefe, Math.Max(8, f.TiefeAn(punkt)), 0.05, f.TiefeAn(punkt), Meter,
                wert => _entwurf.FlussTiefeSetzen(f, punkt, wert), eigeneTiefe ? "Eigene Tiefe an diesem Punkt" : "Noch ohne eigene Tiefe – die des Flusses; ziehen setzt eine"));
            if (eigeneBreite || eigeneTiefe)
            {
                var zurueck = new HBoxContainer();
                zurueck.AddThemeConstantOverride("separation", 8);
                Knopf(zurueck, "Wie der Fluss", () =>
                {
                    if (eigeneBreite) _entwurf.FlussBreiteSetzen(f, punkt, null);
                    if (eigeneTiefe) _entwurf.FlussTiefeSetzen(f, punkt, null);
                }, "Die eigene Breite und Tiefe an diesem Punkt wieder wegnehmen");
                _eigenschaften.AddChild(zurueck);
            }
        }
        Hauptmenue.Text(_eigenschaften, "Griffe ziehen verschiebt die Stützpunkte, ein Klick auf die Linie fügt einen ein, Entf löscht den gewählten. Ein Weg über den Fluss bekommt von selbst eine Brücke (Holz bis 20 m, darüber Stein) oder, wo er flach ist, eine Furt.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  See und Teich
    // ═════════════════════════════════════════════════════════════════════════

    private void SeeTafel(EntwurfSee s, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(s.Name, text => _entwurf.GewaesserUmbenennen(s, text)));
        var art = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Nur die Beschreibung – ein Teich ist klein" };
        art.AddItem("See");
        art.AddItem("Teich");
        art.Select(s.Art == "teich" ? 1 : 0);
        art.ItemSelected += i => _entwurf.GewaesserArtSetzen(s, i == 1 ? "teich" : "see");
        Zeile(raster, "Art", art);
        var see = _entwurf.Wasserform.Seen.FirstOrDefault(x => x.Nummer == GewaesserNummer(s));
        var stand = s.Wasserstand ?? see?.Wasserstand ?? 0;
        var automatisch = new CheckBox { Text = "automatisch", ButtonPressed = s.Wasserstand is null, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "An: 0,4 m unter dem tiefsten Punkt des Ufers – folgt dem Gelände. Aus: eine feste Höhe." };
        var hoehe = new Zahlfeld { MinValue = Hoehenraster.Tiefste, MaxValue = Hoehenraster.Hoechste, Step = 0.05, Suffix = "m", Value = stand, Editable = s.Wasserstand is not null,
            TooltipText = "Höhe des Wasserspiegels über dem Nullpunkt der Karte" };
        hoehe.ValueChanged += wert => _entwurf.SeeWasserstandSetzen(s, wert);
        automatisch.Toggled += an => _entwurf.SeeWasserstandSetzen(s, an ? null : stand);
        var standZeile = new HBoxContainer();
        standZeile.AddThemeConstantOverride("separation", 8);
        standZeile.AddChild(automatisch);
        standZeile.AddChild(hoehe);
        Zeile(raster, "Wasserstand", standZeile);
        var tiefe = s.Tiefe ?? Wasserform.SeeTiefeVorgabe;
        Zeile(raster, "Tiefe", Schieber(Wasserform.MindestTiefe, Math.Max(8, tiefe), 0.05, tiefe, Meter, wert => _entwurf.SeeTiefeSetzen(s, wert),
            "Die größte Tiefe; der Boden fällt vom Ufer mit 30 % ab, bis sie erreicht ist"));
        Zeile(raster, "Fläche", Wert($"{Math.Round(Inhalt(s.Ecken))} m² · {s.Ecken.Count} Ecken"));
        Hauptmenue.Text(_eigenschaften, "Ecken ziehen, ein Klick auf den Rand fügt eine ein, Ziehen innen verschiebt den See. Das Wasser liegt still; nichts darf im Wasser stehen, und kein Weg darf hindurchführen.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fährstelle
    // ═════════════════════════════════════════════════════════════════════════

    private void FaehreTafel(EntwurfFaehre fa, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(fa.Name, text => _entwurf.FaehreUmbenennen(fa, text)));
        var nummer = 0;
        foreach (var anderer in _entwurf.Faehren)
            if (ReferenceEquals(anderer, fa)) break;
            else nummer++;
        var verbindung = _entwurf.Wasserplan.Faehren.FirstOrDefault(v => v.Nummer == nummer);
        Zeile(raster, "Länge", Wert($"{Math.Round(fa.Von.Abstand(fa.Bis))} m"));
        Zeile(raster, "Gewässer", Wert(verbindung?.Gewaesser ?? "keins – die Anleger müssen an gegenüberliegenden Ufern stehen"));
        var minuten = _daten?.Logistik.Wasser?.FaehreMinuten;
        Zeile(raster, "Karren", Wert(minuten is { } m ? $"warten {m} Minuten (Fährzeit)" : "keine Fährzeit in den Wirtschaftsdaten"));
        Hauptmenue.Text(_eigenschaften, "Beide Anleger rasten an einem Weg ein; ein Anleger am Rand des Wassers (höchstens 15 m) genügt. Griffe ziehen verschiebt einen Anleger. Die Fähre pendelt und holt Karren ab; Passanten nutzen sie nicht.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeuge
    // ═════════════════════════════════════════════════════════════════════════

    private void WasserWerkzeugTafel()
    {
        UiHilfe.KinderEntfernen(_eigenschaften);
        _gezeigt = WasserMarke;
        var werkzeug = _stadt.Kartenwerkzeug;
        var titel = werkzeug switch
        {
            Werkstattwerkzeug.Fluss => _stadt.WerkstattFlussart == "bach" ? "Bach" : "Fluss",
            Werkstattwerkzeug.See => _stadt.WerkstattSeeart == "teich" ? "Teich" : "See",
            _ => "Fährstelle",
        };
        Hauptmenue.Text(_eigenschaften, titel, 17, Gold);
        if (werkzeug == Werkstattwerkzeug.Fluss)
        {
            var raster = new GridContainer { Columns = 2 };
            raster.AddThemeConstantOverride("h_separation", 10);
            raster.AddThemeConstantOverride("v_separation", 6);
            _eigenschaften.AddChild(raster);
            var vorgabe = Wasserform.Vorgabebreite(_stadt.WerkstattFlussart);
            Zeile(raster, "Breite", Schieber(Wasserform.MindestBreite, 60, 0.5, _stadt.WerkstattFlussbreite > 0 ? _stadt.WerkstattFlussbreite : vorgabe, Meter,
                wert => _stadt.WerkstattFlussbreite = wert, $"Breite des neuen Flusses (Vorgabe {Meter(vorgabe)}); später je Stützpunkt änderbar"));
            Hauptmenue.Text(_eigenschaften, "Punkte klicken – in Fließrichtung: der erste Punkt ist die Quelle, der letzte die Mündung (die Pfeile zeigen es). Doppelklick oder Enter legt den Fluss an. Das Bett wird ins Gelände gegraben; das Wasser fließt nie bergauf.",
                12, Grau, umbruchBreite: Rechtsbreite - 30);
        }
        else if (werkzeug == Werkstattwerkzeug.See)
            Hauptmenue.Text(_eigenschaften, "Ecken klicken, Doppelklick oder ein Klick auf die erste Ecke schließt. Der Wasserstand liegt 0,4 m unter dem tiefsten Punkt des Ufers; danach lassen sich Wasserstand und Tiefe rechts einstellen.",
                12, Grau, umbruchBreite: Rechtsbreite - 30);
        else
            Hauptmenue.Text(_eigenschaften, "Zwei Klicks: die Anleger an den beiden Ufern. Jeder rastet an einem Weg ein (der Ring zeigt wo). Karren fahren mit der Fähre über – sie warten dort etwas länger.",
                12, Grau, umbruchBreite: Rechtsbreite - 30);
    }
}
