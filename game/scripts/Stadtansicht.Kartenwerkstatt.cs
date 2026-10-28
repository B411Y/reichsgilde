using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>Was ein Klick in die Stadt der Kartenwerkstatt gerade tut.</summary>
public enum Werkstattwerkzeug
{
    /// <summary>Wählen und Ziehen (Gebäude, Wege, Flächen, Pflanzen, Griffe).</summary>
    Auswahl,

    /// <summary>Ein Gebäude des Katalogs setzen (Umriss an der Maus).</summary>
    Baustein,

    /// <summary>Eine einzelne Pflanze des Katalogs setzen.</summary>
    Pflanze,

    /// <summary>Einen Weg zeichnen: Punkte klicken, Doppelklick oder Enter beendet.</summary>
    Weg,

    /// <summary>Eine Bauzone zeichnen: Ecken klicken, Doppelklick schließt.</summary>
    Bauzone,

    Wald,
    Streu,

    /// <summary>Eine Feldfläche zeichnen (Äcker, vor allem im Randstreifen): Ecken klicken, Doppelklick schließt.</summary>
    Feld,

    /// <summary>Eine Aussparung in der gewählten Wald-, Streu- oder Feldfläche zeichnen.</summary>
    Aussparung,

    /// <summary>Pflanzen entlang eines gezogenen Strichs streuen.</summary>
    Pinsel,

    /// <summary>Pflanzen im Kreis unter dem Strich löschen.</summary>
    Radierer,

    /// <summary>Eine Mauer zeichnen: Ecken klicken, Klick auf die erste schließt, Doppelklick oder Enter lässt sie offen.</summary>
    Mauer,

    /// <summary>Ein Tor auf eine Mauer setzen.</summary>
    Tor,

    /// <summary>Einen Platz für die Startbelegung wählen (Wohnhaus, Betrieb, Startplatz – die Tafel „Start“ sagt, wofür).</summary>
    Platz,

    /// <summary>Das Gelände mit einem Pinsel formen (Heben, Senken, Glätten, Ebnen, Rauschen – <see cref="Stadtansicht.GelaendeArt"/>): mit gedrückter Maus malen.</summary>
    Gelaende,

    /// <summary>Eine Rampe zwischen zwei Punkten: zwei Klicks.</summary>
    Rampe,

    /// <summary>Eine Bodentextur malen (Schritt 5e): mit gedrückter Maus; die Textur (oder der Radierer) steht in <see cref="Stadtansicht.BodenArt"/>.</summary>
    Boden,

    /// <summary>Einen Fluss oder Bach zeichnen (Schritt 5c): Punkte klicken in Fließrichtung, Doppelklick oder Enter beendet.</summary>
    Fluss,

    /// <summary>Einen See oder Teich zeichnen: Ecken klicken, Doppelklick schließt.</summary>
    See,

    /// <summary>Eine Fährstelle setzen: zwei Klicks, die beiden Anleger.</summary>
    Faehre,
}

/// <summary>
/// Die Kartenwerkstatt (Level-Editor) in der Stadtansicht – ein Teil der <c>partial class Stadtansicht</c>. Die Szene „Kartenwerkstatt“
/// (<see cref="Kartenwerkstatt"/>) hat Tafeln, Dateien und Startoptionen; hier liegt alles, was in der 3D-Welt passiert:
///   • diese Datei: Aufbau, Gebäude als „Stücke“, Auswahl, Werkzeuge, Eingabe;
///   • Stadtansicht.Kartenwerkstatt.Wege.cs: Wege zeichnen und bearbeiten (Stützpunkte), Gassennetz, Einrasten;
///   • Stadtansicht.Kartenwerkstatt.Flaechen.cs: Bauzonen und Naturflächen als Vielecke (Ecken), Vorschau beim Zeichnen;
///   • Stadtansicht.Kartenwerkstatt.Natur.cs: die Natur der Flächen live, einzelne Pflanzen, Pinsel und Radierer;
///   • Stadtansicht.Kartenwerkstatt.Mauern.cs: Mauern und Tore zeichnen und bearbeiten;
///   • Stadtansicht.Kartenwerkstatt.Start.cs: die Startbelegung in der Karte (Farben der Häuser) und das Wählen eines Platzes dafür;
///   • Stadtansicht.Kartenwerkstatt.Wasser.cs: Flüsse, Seen und Fährstellen zeichnen, markieren und wählen (das Wasser selbst: Stadtansicht.Wasser.cs).
///
/// <b>Zeichnen.</b> Der Boden kommt aus derselben Methode wie im Spiel (Stadtansicht.Karte.cs), einmal beim Öffnen. Jeder
/// bearbeitbare Teil des Entwurfs (Gebäude, Weg, Bauzone, Naturfläche, Pflanze, Mauer) hat einen eigenen Knoten; ändert ein Befehl einen Teil
/// (Ereignis <see cref="Kartenentwurf.Geaendert"/>), wird nur sein Knoten neu gebaut. Was davon abhängt – die Natur der Flächen in der
/// Nähe, das Gassennetz, die rote Markierung der Fehler –, erledigt <see cref="WerkstattAktualisieren"/> einmal am Ende des Bildes.
/// Werkstattplätze zeigen das Werkstattmodell des Schmieds (Stufe 1, ohne Hausfarbe): Alle Werkstätten haben dieselbe Normfläche
/// 16 × 14 m, und ein Haus zeigt Abstände und Höhe besser als ein flacher Bauplatz-Zaun. Wohnhausplätze zeigen Fachwerkhäuser (ein
/// Doppelplatz zwei), Einwohnerplätze ein Einwohnerhaus, alle anderen das Modell ihres Bausteins (ohne Modell ein grauer Block). Ist ein
/// Platz am Start belegt, trägt das Modell die Farbe des Hauses (ein Startbetrieb auch das Modell seiner Werkstatt), dazu eine Fahne
/// (Stadtansicht.Kartenwerkstatt.Start.cs).
///
/// <b>Bedienen.</b> Das Werkzeug (<see cref="Kartenwerkzeug"/>) sagt, was ein Klick tut. Mit „Auswahl“ wählt ein Klick einen Teil
/// (<see cref="AuswahlKlick"/>), Ziehen verschiebt Gebäude, Pflanzen, die gewählte Fläche und mit Umschalt eine Mauer (ein Rückgängig-Schritt je Ziehen); ein gewählter
/// Weg, eine gewählte Fläche oder Mauer zeigt Griffe an Stützpunkten, Ecken und Toren. Ein Gebäude trifft der Klick nur dort, wo sein Modell ist
/// (<see cref="StueckKlickflaeche"/>): Im Schrägblick bleiben Boden, Wege und Flächen hinter einem Haus erreichbar. Ein Baustein des Katalogs: Sein Umriss folgt
/// der Maus auf dem 1-m-Raster, Q/E dreht in 15°-Schritten, mit gehaltener Alt-Taste frei; grün = an dieser Stelle kein Fehler
/// (<see cref="Kartenpruefung.PruefeStelle"/>), rot = mit dem ersten Fehler als Text. Teile mit Fehler leuchten rot. Die Kamera dreht
/// hier mit Strg+Q/E oder Bild↑/Bild↓.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Der Entwurf, den die Kartenwerkstatt zeigt (null: im Spiel und im Hauptmenü).</summary>
    private Kartenentwurf? _entwurf;

    private Node3D? _stueckeKnoten;
    private readonly Dictionary<EntwurfGebaeude, Stueck> _stuecke = new();

    /// <summary>Ein Gebäude des Entwurfs in der Welt: Wurzel an Punkt und Drehung, darunter Modell, Klickfläche, Name und Markierung.</summary>
    private sealed class Stueck
    {
        public required Node3D Wurzel { get; init; }

        /// <summary>Alles Sichtbare des Modells (bekommt den roten Überzug, wenn das Stück einen Fehler hat).</summary>
        public required Node3D Modell { get; init; }

        /// <summary>Rahmen um die Grundfläche: gold, wenn gewählt, rot bei Fehlern.</summary>
        public required Node3D Markierung { get; init; }

        public required MeshInstance3D[] Rahmen { get; init; }
        public required MeshInstance3D Fuellung { get; init; }
        public bool Fehlerhaft { get; set; }

        /// <summary>Wer den Platz am Start nutzt, wie das Stück es zeigt (null: niemand) – ändert sich das, wird das Stück neu gebaut.</summary>
        public Belegungsanzeige? Belegung { get; init; }
    }

    /// <summary>Kennung der Klickfläche eines Stücks (Präfix „stueck:“, damit sie sich nicht mit dem Spiel mischt).</summary>
    private static string StueckKennung(EntwurfGebaeude g) => "stueck:" + g.Kennung;

    /// <summary>Gesperrt, solange die Oberfläche eine Rückfrage zeigt: Klicks und Tasten gehen dann nicht an die Karte.</summary>
    public bool WerkstattGesperrt { get; set; }

    /// <summary>Der gewählte Teil (Gebäude, Weg, Bauzone, Naturfläche, Pflanze) – null: keiner.</summary>
    public EntwurfTeil? WerkstattGewaehlt { get; private set; }

    /// <summary>Kennung des gewählten Gebäudes (null: keins oder ein anderer Teil gewählt).</summary>
    public string? WerkstattAuswahl => (WerkstattGewaehlt as EntwurfGebaeude)?.Kennung;

    /// <summary>Die Auswahl hat sich geändert (Klick, Setzen, Löschen) – die Tafel rechts zeigt den neuen Teil.</summary>
    public event Action<EntwurfTeil?>? WerkstattAuswahlGeaendert;

    /// <summary>Was ein Klick tut (<see cref="WerkstattWerkzeugWaehlen"/>).</summary>
    public Werkstattwerkzeug Kartenwerkzeug { get; private set; }

    /// <summary>Der Baustein des Katalogs, der gerade gesetzt wird (Gebäude oder Pflanze; null: keiner).</summary>
    public string? WerkstattBaustein { get; private set; }

    /// <summary>Das Werkzeug wurde in der Stadt beendet (Esc, Rechtsklick, fertige Aussparung) – der Katalog hebt die Wahl auf.</summary>
    public event Action? WerkstattWerkzeugBeendet;

    /// <summary>Eine Meldung für die Leiste unten (Text, ob Fehler) – etwa, wenn ein Befehl nicht ging.</summary>
    public event Action<string, bool>? WerkstattMeldung;

    /// <summary>Die Drehung des Umrisses beim Setzen (Grad), auch für Pflanzen.</summary>
    public double WerkstattDrehung { get; set; }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen Entwurf: den Boden wie im Spiel, dazu jeden Teil des Entwurfs mit eigenem Knoten. Einmal je Szene – ein anderer
    /// Entwurf lädt die Szene neu. Licht fest auf hellen Mittag, ohne Passanten, Karren und Vögel. Beim Verlassen der Szene löst
    /// <see cref="WerkstattLoesen"/> die Stadtansicht wieder vom Entwurf (er kann sie überleben: „Karte ausprobieren“).
    /// </summary>
    public void EntwurfZeigen(Kartenentwurf entwurf)
    {
        if (_karteGebaut) return;
        _karteGebaut = true;
        _entwurf = entwurf;
        _gelaende = entwurf.Gelaendeform; // folgt jeder Änderung des Entwurfs von selbst
        _karte = HintergrundVon(entwurf);
        if (_karte is { } karte)
        {
            LagenLesen(karte);
            // Mauern, ihr Pflaster und die Tore kommen aus dem Entwurf und folgen jeder Änderung (Stadtansicht.Kartenwerkstatt.Mauern.cs)
            _mauerFlaechen.Clear();
            _tore.Clear();
            var (x0, z0, x1, z1) = entwurf.Grenzen();
            _kartenRahmen = _kartenRahmen.Merge(new Rect2((float)x0, (float)z0, (float)(x1 - x0), (float)(z1 - z0))).Grow(30);
            BodenLegen(karte);
            KameraAusKarte(karte);
        }
        else BodenBauen(entwurf.Kartengrenzen); // Boden, Dunst und Kartenrand (Stadtansicht.Rand.cs)

        entwurf.Fangpunkte = Halteplaetze; // Karrenplätze und Gatter aus den Modellen (Stadtansicht.Kartenwerkstatt.Wege.cs)
        _stueckeKnoten = new Node3D { Name = "Stuecke" };
        AddChild(_stueckeKnoten);
        foreach (var g in entwurf.Gebaeude) StueckBauen(g);
        WegeAufbauen();      // Stadtansicht.Kartenwerkstatt.Wege.cs
        FlaechenAufbauen();  // Stadtansicht.Kartenwerkstatt.Flaechen.cs
        PflanzenAufbauen();  // Stadtansicht.Kartenwerkstatt.Natur.cs
        BodendekoAufbauen(); // Stadtansicht.Kartenwerkstatt.Boden.cs
        MauernAufbauen();    // Stadtansicht.Kartenwerkstatt.Mauern.cs
        WasserAufbauen();    // Stadtansicht.Kartenwerkstatt.Wasser.cs
        _wsFehlerNeu = true;
        entwurf.Geaendert += EntwurfGeaendert;
        LichtNachUhrzeit(12 * 60, 100); // heller Mittag, sofort ohne Überblenden
    }

    /// <summary>
    /// Die Karte für den Boden: der Entwurf, wie der Kern ihn zum Spielen liest. Hat er Fehler, die der Leser nicht annimmt (z. B. einen
    /// unbekannten Baustein), ohne die Gebäude.
    /// </summary>
    private static Stadtkarte? HintergrundVon(Kartenentwurf entwurf)
    {
        var json = entwurf.ZuJson();
        try
        {
            return Stadtkarte.AusJson(json, entwurf.Katalog);
        }
        catch (KartenFehler) { }
        try
        {
            var ohne = JsonNode.Parse(json)!.AsObject();
            ohne["gebaeude"] = new JsonArray();
            return Stadtkarte.AusJson(ohne.ToJsonString(), entwurf.Katalog);
        }
        catch (Exception ex) when (ex is KartenFehler or JsonException)
        {
            GD.PushWarning($"Kartenwerkstatt: Die Karte „{entwurf.Kennung}“ lässt sich nicht zeichnen ({ex.Message}) – nur die Teile des Entwurfs erscheinen.");
            return null;
        }
    }

    /// <summary>Der Rand einer Karte mit Größe (neue Karten): ein heller Strich am Boden.</summary>
    private void KartenrandZeichnen((double X0, double Z0, double X1, double Z1) g)
    {
        var stoff = Transparent(new Color(0.95f, 0.88f, 0.6f), 0.7f);
        var (x0, z0, x1, z1) = ((float)g.X0, (float)g.Z0, (float)g.X1, (float)g.Z1);
        var rand = new Node3D { Name = "Kartenrand" };
        AddChild(rand);
        Quader(new Vector3((x0 + x1) / 2, 0.1f, z0), new Vector3(x1 - x0, 0.1f, 0.6f), stoff, rand);
        Quader(new Vector3((x0 + x1) / 2, 0.1f, z1), new Vector3(x1 - x0, 0.1f, 0.6f), stoff, rand);
        Quader(new Vector3(x0, 0.1f, (z0 + z1) / 2), new Vector3(0.6f, 0.1f, z1 - z0), stoff, rand);
        Quader(new Vector3(x1, 0.1f, (z0 + z1) / 2), new Vector3(0.6f, 0.1f, z1 - z0), stoff, rand);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Änderungen: den Teil neu bauen, den Rest am Ende des Bildes
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Was am Ende des Bildes nachzuziehen ist: Fehlerfarben, Griffe des gewählten Teils.</summary>
    private bool _wsFehlerNeu, _wsGriffeNeu;

    /// <summary>
    /// Wo jedes Hindernis der Natur (Gebäude, Weg, Bauzone) zuletzt lag – das Rechteck mit dem Rand, in dem es die Natur verdrängt: Ändert es sich,
    /// wird die Natur am alten und am neuen Ort neu gestreut.
    /// </summary>
    private readonly Dictionary<EntwurfTeil, Rect2> _wsBereiche = new();

    private void EntwurfGeaendert(Kartenaenderung a)
    {
        var weg = a.Art == Aenderungsart.Geloescht;
        if (a.Objekt is EntwurfGebaeude or EntwurfWeg or EntwurfBauzone) WerkstattKapazitaetZeigen(null); // gälte nicht mehr (Stadtansicht.Kartenwerkstatt.Flaechen.cs)
        var hatteBereich = _wsBereiche.Remove(a.Objekt, out var alt);
        if (hatteBereich) NaturBereichVormerken(alt);
        if (!hatteBereich) alt = default;
        switch (a.Objekt)
        {
            case EntwurfFluss fluss: // Gewässer vor den Flächen: Ein See ist auch eine Fläche
                FlussMarkieren(fluss, weg);
                GewaesserGeaendert(fluss, weg);
                break;
            case EntwurfSee see:
                FlaecheBauen(see, weg);
                GewaesserGeaendert(see, weg);
                break;
            case EntwurfFaehre faehre:
                FaehreMarkieren(faehre, weg);
                _wsWasserNeu = true;
                break;
            case EntwurfGebaeude g:
                StueckEntfernen(g);
                if (!weg) StueckBauen(g);
                _wsNetzNeu = true; // Höfe und Karrenplätze hängen am Netz
                if (g.Baustein?.AmUfer == true) GewaesserGeaendert(g, weg); // ein Hafen: das Becken vor der Kaimauer (Stadtansicht.Kartenwerkstatt.Wasser.cs)
                break;
            case EntwurfWeg w:
                WegBauen(w, weg);
                _wsNetzNeu = true;
                if (!GewaesserLeer) _wsWasserNeu = true; // Brücken, Furten und Fähren hängen an den Wegen
                break;
            case EntwurfFlaeche f:
                FlaecheBauen(f, weg);
                break;
            case EntwurfPflanze p:
                PflanzeBauen(p, weg);
                break;
            case EntwurfMauer m:
                MauerBauen(m, weg);
                _wsNetzNeu = true; // die Tore hängen am Netz
                break;
            case EntwurfStart:
                _wsBelegungNeu = true; // Stadtansicht.Kartenwerkstatt.Start.cs
                break;
            case EntwurfKopf:
                _wsRandNeu = true; // Größe, Rand oder Kamera-Start geändert: Boden, Dunst und Kartenrand neu (Stadtansicht.Kartenwerkstatt.Felder.cs)
                break;
            case EntwurfGelaende:
                if (a.Bereich is { } geaendert) GelaendeVormerken(geaendert); // Stadtansicht.Kartenwerkstatt.Gelaende.cs
                break;
            case EntwurfBoden:
                if (a.Bereich is { } gemalt) BodenVormerken(gemalt); // Stadtansicht.Kartenwerkstatt.Boden.cs
                break;
        }
        if (a.Objekt is EntwurfWeg or EntwurfMauer)
        {
            // Wege und Mauern legen ihr Bett ins Gelände: Der Boden unter dem alten und dem neuen Verlauf ändert sich
            if (alt != default) GelaendeVormerken(RechteckVon(alt).Vergroessert(Gelaendeform.Glaettung + Gelaendeform.Mauerbett));
            if (!weg && BereichVon(a.Objekt) is { } nach) GelaendeVormerken(RechteckVon(nach).Vergroessert(Gelaendeform.Glaettung + Gelaendeform.Mauerbett));
        }
        if (!weg && a.Objekt is not (EntwurfPflanze or EntwurfNaturflaeche or EntwurfFeld or EntwurfKopf) && BereichVon(a.Objekt) is { } neu)
        {
            _wsBereiche[a.Objekt] = neu;
            NaturBereichVormerken(neu);
        }
        if (ReferenceEquals(a.Objekt, WerkstattGewaehlt))
        {
            if (weg) WerkstattWaehlen((EntwurfTeil?)null);
            else
            {
                TeilMarkieren(a.Objekt);
                _wsGriffeNeu = true;
            }
        }
        _wsFehlerNeu = true;
    }

    /// <summary>
    /// Nach allen Änderungen eines Bildes (aus <see cref="WerkstattAktualisieren"/>): Stücke mit geänderter Startbelegung neu, Natur der betroffenen
    /// Flächen neu streuen, Gassennetz neu, Fehlerfarben, Griffe.
    /// </summary>
    private void Nachziehen()
    {
        if (_wsBelegungNeu) BelegungNachziehen();
        if (_wsRandNeu) RandNeuBauen();
        GelaendeNachziehen(); // vor der Natur: Sie liest die neue Höhe (Stadtansicht.Kartenwerkstatt.Gelaende.cs)
        BodenNachziehen();    // Bilder der Bodentexturen und ihre Deko (Stadtansicht.Kartenwerkstatt.Boden.cs)
        if (_wsWasserNeu || _wsWegeNeu) WasserNachziehen(); // Stadtansicht.Kartenwerkstatt.Wasser.cs
        NaturNachziehen();
        if (_wsNetzNeu) NetzBauen();
        if (_wsFehlerNeu) FehlerZeigen();
        if (_wsGriffeNeu) GriffeBauen();
        WerkstattEbenenAnwenden(); // Ebenen ein- und ausgeblendet (Stadtansicht.Kartenwerkstatt.Ebenen.cs)
    }

    /// <summary>Das Rechteck, in dem ein Teil die Natur verdrängt (mit Rand; bei einer Mauer die ganze Stadt darin); null für Pflanzen und leere Teile.</summary>
    private Rect2? BereichVon(EntwurfTeil t)
    {
        var rand = (float)Naturfreiraum.Abstand + 1;
        switch (t)
        {
            case EntwurfGebaeude g when Naturhindernis.Von(g.Baustein, g.X, g.Z, g.Drehung, g.Massstab, Modellmass) is { } h:
            {
                var r = Mathf.Sqrt((float)(h.HalbX * h.HalbX + h.HalbZ * h.HalbZ)) + rand;
                return new Rect2((float)g.X - r, (float)g.Z - r, 2 * r, 2 * r);
            }
            case EntwurfWeg w when w.Verlauf.Count > 0:
                return Rahmen(w.Verlauf).Grow((float)w.BreiteWirksam / 2 + rand);
            case EntwurfFlaeche f when f.Ecken.Count > 0:
                return Rahmen(f.Ecken).Grow(rand);
            case EntwurfMauer m when m.Ecken.Count > 0:
                return Rahmen(m.Ecken).Grow((float)Naturfreiraum.AbstandMauer + 1);
        }
        return null;
    }

    private static Rect2 Rahmen(IEnumerable<Kartenort> punkte)
    {
        Rect2? r = null;
        foreach (var p in punkte)
        {
            var q = new Vector2((float)p.X, (float)p.Z);
            r = r is { } x ? x.Expand(q) : new Rect2(q, Vector2.Zero);
        }
        return r ?? new Rect2();
    }

    private static Vector2 V2(Kartenort p) => new((float)p.X, (float)p.Z);
    /// <summary>Ein Ort der Karte als Punkt der Welt, <paramref name="hoehe"/> Meter über dem Boden.</summary>
    private Vector3 V3(Kartenort p, float hoehe = 0) => new((float)p.X, hoehe + BodenHoehe((float)p.X, (float)p.Z), (float)p.Z);

    // ═════════════════════════════════════════════════════════════════════════
    //  Stücke (Gebäude)
    // ═════════════════════════════════════════════════════════════════════════

    private void StueckEntfernen(EntwurfGebaeude g)
    {
        if (_stuecke.Remove(g, out var s)) KnotenEntfernen(s.Wurzel);
    }

    /// <summary>
    /// Baut das Stück eines Gebäudes: Modell nach Art (siehe Kopf der Datei) in der Farbe seiner Startbelegung, Klickfläche auf dem Modell, Name,
    /// Markierung, die Fahne der Belegung.
    /// </summary>
    private void StueckBauen(EntwurfGebaeude g)
    {
        if (_stueckeKnoten is null) return;
        var fundament = g.Groesse is not null && _entwurf is not null ? _entwurf.Fundament(g) : default; // auf Gelände auf dem höchsten Punkt unter der Fläche
        var wurzel = new Node3D
        {
            Name = "Stueck_" + g.Kennung,
            Position = new Vector3((float)g.X, (float)fundament.Hoehe, (float)g.Z),
            Rotation = new Vector3(0, Mathf.DegToRad((float)g.Drehung), 0),
        };
        _stueckeKnoten.AddChild(wurzel);
        var modell = new Node3D { Name = "Modell" };
        wurzel.AddChild(modell);
        var groesse = g.Groesse is var (b, t) ? new Vector2((float)b, (float)t) : new Vector2(4, 4);
        if (g.Groesse is not null) SockelBauen(wurzel, Vector3.Zero, 0, groesse, (float)fundament.Tiefe); // Bruchsteinsockel bis zum Boden (Stadtansicht.Gelaende.cs)
        var belegung = BelegungsanzeigeVon(g); // Stadtansicht.Kartenwerkstatt.Start.cs
        StueckModell(g, modell, groesse, belegung);

        // Name so hoch wie das Modell (flache wie der Markt mindestens 2 m, höchstens 30 m)
        var hoehe = Mathf.Clamp(MasseImModell(modell).End.Y, 2, 30);
        var kennung = StueckKennung(g);
        StueckKlickflaeche(kennung, modell, wurzel);
        var name = Beschriftung(belegung is { } bel ? $"{g.Name}\n{bel.Text}" : g.Name, new Vector3(0, hoehe + 4, 0), 44, wurzel, kennung);
        if (belegung is { } farbig) name.Modulate = farbig.Farbe.Lightened(0.55f);

        var (markierung, rahmen, fuellung) = MarkierungBauen(groesse, wurzel);
        if (belegung is { } anzeige) BelegungsfahneBauen(groesse, anzeige, wurzel); // Stadtansicht.Kartenwerkstatt.Start.cs
        var stueck = new Stueck { Wurzel = wurzel, Modell = modell, Markierung = markierung, Rahmen = rahmen, Fuellung = fuellung, Belegung = belegung };
        _stuecke[g] = stueck;
        if (_entwurf?.FehlerhafteGebaeude.Contains(g.Kennung) == true) FehlerUeberzug(stueck, true);
        MarkierungSetzen(g, stueck);
        if (BereichVon(g) is { } bereich) _wsBereiche[g] = bereich;
    }

    /// <summary>
    /// Das Modell eines Bausteins für das Vorschaubild der Palette (Kartenwerkstatt.Palette.cs): ohne Besitzer, Stufe 1 und Variante 1, ohne Wappen, Fahne
    /// und Gerüst; ein Platz zeigt sein Haus (Werkstatt des Schmieds, Fachwerkhaus, Einwohnerhaus), eine Arbeitsstelle nur ihren Knoten. Null ohne Modell.
    /// Der Aufrufer besitzt den Knoten; er wird nirgends in der Stadt gemeldet (keine Fenster, Kronen, Fahnen).
    /// </summary>
    public Node3D? WerkstattVorschaumodell(Baustein b)
    {
        var datei = b.Art switch
        {
            Bausteinart.Werkstattplatz => WerkstattModellDatei("Schmied"),
            Bausteinart.Wohnhausplatz => Katalog?.Wohnhaus(0)?.Modell,
            Bausteinart.Einwohnerplatz => EinwohnerDatei(b.Groesse is { Length: 2 } g && g[0] >= 8 ? Hausart.Buergerhaus : Hausart.Wohnhaus),
            _ => b.Modell,
        };
        if (datei is null || Modell(datei) is not { } szene) return null;
        var wurzel = szene.Instantiate<Node3D>();
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            if (name is "Stufe2" or "Stufe3" or "Variante2" or "Variante3" or "Geruest" or "Leerstand" or "Wappen" or "Fahne") teil.Visible = false;
            else if (b.Art == Bausteinart.Umlandstelle && b.Knoten is { } knoten && teil.GetParent() == wurzel) teil.Visible = name == knoten;
        }
        // Pflanzen und Requisiten teilen sich eine Datei (props_gegenstaende, natur_baeume): nur die erste Variante des Knotens zeigen
        if (b.Art == Bausteinart.Natur && b.Knoten is { } natur && wurzel.FindChild(natur + "1", false, false) is not null)
            foreach (var kind in wurzel.GetChildren().OfType<Node3D>()) kind.Visible = kind.Name == natur + "1";
        return wurzel;
    }

    /// <summary>
    /// Das Modell eines Stücks nach der Art seines Bausteins, lokal: Mitte der Grundfläche im Ursprung, Vorderseite nach +z. Mit Startbelegung in
    /// der Farbe des Hauses; ein Werkstattplatz zeigt dann die Werkstatt seines Startbetriebs (sonst die des Schmieds).
    /// </summary>
    private void StueckModell(EntwurfGebaeude g, Node3D modell, Vector2 groesse, Belegungsanzeige? belegung)
    {
        var m = (float)g.Massstab;
        var versatz = g.Baustein?.Versatz is { Length: 2 } v ? new Vector3((float)v[0], 0, (float)v[1]) * m : Vector3.Zero;
        var stabil = g.Kennung.Sum(c => c); // eine feste Zahl je Kennung für Variante und Hausfarbe (string.GetHashCode ändert sich je Start)
        var besitzer = belegung?.Modellfarbe;
        switch (g.Art)
        {
            case Bausteinart.Werkstattplatz:
                Einsetzen(WerkstattModellDatei(belegung?.Beruf ?? "Schmied"), new ModellZustand(Stufe: 1, Besitzer: besitzer));
                break;
            case Bausteinart.Wohnhausplatz when Katalog?.Wohnhaus(0) is { Modell: { } datei } fachwerk:
            {
                // Fachwerkhäuser mit der Front an der Vorderkante; ein Doppelplatz (17 m) bekommt zwei
                var anzahl = Math.Max(1, (int)(groesse.X / 8));
                var breite = groesse.X / anzahl;
                for (var i = 0; i < anzahl; i++)
                    ModellEinsetzen(datei, modell, new Vector3(-groesse.X / 2 + breite * (i + 0.5f), 0, groesse.Y / 2 - ModellFront(fachwerk)),
                        new ModellZustand(Variante: (stabil + i) % 3 + 1, Besitzer: besitzer ?? Hausfarben[(stabil + i) % Hausfarben.Length]));
                break;
            }
            case Bausteinart.Einwohnerplatz:
            {
                var art = groesse.X >= 8 ? Hausart.Buergerhaus : Hausart.Wohnhaus;
                ModellEinsetzen(EinwohnerDatei(art), modell, new Vector3(0, 0, groesse.Y / 2 - EinwohnerFront(art)),
                    new ModellZustand(Variante: stabil % 3 + 1, Besitzer: Hausfarben[stabil % Hausfarben.Length]));
                break;
            }
            case Bausteinart.Umlandstelle when g.Baustein is { Modell: { } hafen } && hafen.StartsWith("wasser/"):
                Einsetzen(hafen, new ModellZustand()); // der Hafen: das ganze Modell
                break;
            case Bausteinart.Umlandstelle when g.Baustein is { Modell: { } datei } b:
                StelleEinsetzen(datei, b.Knoten ?? g.Name);
                break;
            case Bausteinart.Kulisse when g.Baustein?.Modell is { } datei:
                Einsetzen(datei, new ModellZustand(Variante: stabil % 3 + 1, Besitzer: Hausfarben[stabil % Hausfarben.Length]));
                break;
            default:
                if (g.Baustein?.Modell is { } eigenes) Einsetzen(eigenes, new ModellZustand(Besitzer: besitzer));
                else Block();
                break;
        }

        // Ein Modell an der Lage der Karte (versatz und Maßstab wie ModellLage)
        void Einsetzen(string datei, ModellZustand zustand)
        {
            if (ModellEinsetzen(datei, modell, Vector3.Zero, zustand) is { } instanz)
                instanz.Wurzel.Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * m), versatz);
        }

        // Eine Arbeitsstelle aus dem Baukasten (Weide, Feld, Wald) – ihr Knoten samt Kindern (Laubkrone)
        void StelleEinsetzen(string datei, string knoten)
        {
            if (Modell(datei)?.Instantiate<Node3D>() is not { } kasten) return;
            if (kasten.FindChild(knoten, true, false) is MeshInstance3D { Mesh: { } netz } teil)
            {
                LaubMelden(netz);
                var stelle = new MeshInstance3D { Name = knoten, Mesh = netz, Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * m), versatz) };
                foreach (var kind in teil.GetChildren().OfType<MeshInstance3D>())
                    stelle.AddChild(new MeshInstance3D { Name = kind.Name, Mesh = kind.Mesh, Transform = kind.Transform });
                modell.AddChild(stelle);
                KronenMelden(stelle);
            }
            else GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb hat keinen Knoten „{knoten}“.");
            kasten.Free();
        }

        // Ohne Modell (Kirche, Hafen): ein grauer Block mit Dach auf der Fläche ohne Vorplatz, bis es eins gibt
        void Block()
        {
            const float hoehe = 6;
            var haus = new Vector2(Mathf.Max(1, groesse.X - 1), Mathf.Max(1, groesse.Y - (float)Kartenpruefung.Vorplatz - 1));
            var mitte = new Vector3(0, 0, -(float)Kartenpruefung.Vorplatz / 2);
            Quader(mitte + new Vector3(0, hoehe / 2, 0), new Vector3(haus.X, hoehe, haus.Y), Material(Bruchstein), modell);
            Dach(mitte + new Vector3(0, hoehe, 0), new Vector3(haus.X + 0.4f, 3, haus.Y + 0.4f), Material(Schiefer), quer: true, modell);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Klickflächen: so genau wie das Modell
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die konvexe Hülle je Netz eines Modells (Punkte im Raum des Netzes) – einmal gerechnet, alle Stücke mit demselben Netz teilen sie.</summary>
    private readonly Dictionary<Mesh, Vector3[]> _wsHuellen = new();

    /// <summary>
    /// Die Klickfläche eines Stücks: je sichtbarem Netz des Modells seine konvexe Hülle (ein Haus: Wände und Dach, ein Baum seine Krone). So trifft
    /// ein Klick das Gebäude nur dort, wo es zu sehen ist, und im Schrägblick bleiben Boden, Wege und Flächen hinter einem Haus erreichbar – vorher
    /// war es ein Kasten über der ganzen Grundfläche bis 12 m Höhe. Ohne jedes Netz (das Modell fehlt) ein flacher Kasten in der Mitte.
    /// </summary>
    private void StueckKlickflaeche(string kennung, Node3D modell, Node3D wurzel)
    {
        var koerper = new StaticBody3D { Name = "Klickflaeche" };
        koerper.SetMeta("kennung", kennung);
        wurzel.AddChild(koerper);
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz || !teil.IsVisibleInTree()) continue;
            if (!_wsHuellen.TryGetValue(netz, out var huelle))
                _wsHuellen[netz] = huelle = netz.CreateConvexShape(clean: true, simplify: false)?.Points ?? Array.Empty<Vector3>();
            if (huelle.Length < 4) continue; // flach oder leer: keine Hülle
            var lage = LageUnter(wurzel, teil);
            koerper.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = huelle.Select(p => lage * p).ToArray() } });
        }
        if (koerper.GetChildCount() == 0)
            koerper.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, 2, 4) }, Position = new Vector3(0, 1, 0) });
    }

    /// <summary>Das Gebäude, dessen Modell an dieser Bildschirmstelle zuerst getroffen wird, und wie hoch über dem Boden (null: keins).</summary>
    private (EntwurfGebaeude Gebaeude, float Hoehe)? StueckTreffer(Vector2 maus)
    {
        var von = _kamera.ProjectRayOrigin(maus);
        var treffer = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(von, von + _kamera.ProjectRayNormal(maus) * 2000));
        if (treffer.Count == 0 || treffer["collider"].As<Node>() is not { } koerper || !koerper.HasMeta("kennung")) return null;
        var kennung = koerper.GetMeta("kennung").AsString();
        if (!kennung.StartsWith("stueck:") || _entwurf?.GebaeudeMit(kennung["stueck:".Length..]) is not { } g) return null;
        return (g, treffer["position"].AsVector3().Y);
    }

    /// <summary>Das Gebäude, auf dessen Grundfläche (mit Vorplatz) der Punkt liegt – das kleinste, wenn mehrere (null: keins).</summary>
    private EntwurfGebaeude? GebaeudeAufGrund(Vector2 p) => _entwurf?.Gebaeude
        .Where(g => g.Groesse is not null && Geometry2D.IsPointInPolygon(p, g.Ecken().Select(V2).ToArray()))
        .OrderBy(g => g.Groesse!.Value.Breite * g.Groesse.Value.Tiefe).FirstOrDefault();

    /// <summary>Wo das Modell eines Gebäudes des Entwurfs steht (wie <see cref="ModellLage"/> für die Karte).</summary>
    private static Transform3D ModellLage(EntwurfGebaeude g)
    {
        var m = (float)g.Massstab;
        var drehung = new Basis(Vector3.Up, Mathf.DegToRad((float)g.Drehung));
        var versatz = g.Baustein?.Versatz is { Length: 2 } v ? new Vector3((float)v[0], 0, (float)v[1]) * m : Vector3.Zero;
        return new Transform3D(drehung.Scaled(Vector3.One * m), new Vector3((float)g.X, 0, (float)g.Z) + drehung * versatz);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Markierung: gewählt (gold) und Fehler (rot)
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Color Auswahlgold = new(1f, 0.82f, 0.3f);
    private static readonly Color Fehlerrot = new(1f, 0.22f, 0.18f);
    private StandardMaterial3D? _rahmenGold, _rahmenRot, _flaecheGold, _flaecheRot, _fehlerUeberzug;

    /// <summary>Ein Rahmen um die Grundfläche und eine Linie an der Kante des Vorplatzes (2 m hinter der Vorderkante), dazu eine leichte Füllung.</summary>
    private (Node3D Knoten, MeshInstance3D[] Rahmen, MeshInstance3D Fuellung) MarkierungBauen(Vector2 groesse, Node3D wurzel)
    {
        // Der Rahmen scheint durch die Häuser (ohne Tiefentest) – sonst verdecken sie im Schrägblick die hintere Hälfte und Nachbarn den Rest
        _rahmenGold ??= ImmerSichtbar(Transparent(Auswahlgold, 0.95f));
        _rahmenRot ??= ImmerSichtbar(Transparent(Fehlerrot, 0.95f));
        _flaecheGold ??= Transparent(Auswahlgold, 0.18f);
        _flaecheRot ??= Transparent(Fehlerrot, 0.28f);
        var knoten = new Node3D { Name = "Markierung", Visible = false };
        wurzel.AddChild(knoten);
        var (hb, ht) = (groesse.X / 2, groesse.Y / 2);
        const float dicke = 0.45f, hoehe = 0.25f;
        var vorplatz = ht - (float)Kartenpruefung.Vorplatz;
        var rahmen = new[]
        {
            Quader(new Vector3(0, hoehe / 2, -ht), new Vector3(groesse.X + dicke, hoehe, dicke), _rahmenGold, knoten),
            Quader(new Vector3(0, hoehe / 2, ht), new Vector3(groesse.X + dicke, hoehe, dicke), _rahmenGold, knoten),
            Quader(new Vector3(-hb, hoehe / 2, 0), new Vector3(dicke, hoehe, groesse.Y), _rahmenGold, knoten),
            Quader(new Vector3(hb, hoehe / 2, 0), new Vector3(dicke, hoehe, groesse.Y), _rahmenGold, knoten),
            Quader(new Vector3(0, hoehe / 2, vorplatz), new Vector3(groesse.X, hoehe * 0.6f, dicke * 0.4f), _rahmenGold, knoten),
        };
        var fuellung = Quader(new Vector3(0, 0.12f, 0), new Vector3(groesse.X, 0.08f, groesse.Y), _flaecheGold, knoten);
        foreach (var teil in rahmen.Append(fuellung)) teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return (knoten, rahmen, fuellung);
    }

    private static StandardMaterial3D ImmerSichtbar(StandardMaterial3D stoff)
    {
        stoff.NoDepthTest = true;
        stoff.RenderPriority = 1;
        return stoff;
    }

    /// <summary>Zeigt Rahmen und roten Überzug eines Stücks nach Auswahl und Fehlern.</summary>
    private void MarkierungSetzen(EntwurfGebaeude g, Stueck s)
    {
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, g);
        s.Markierung.Visible = gewaehlt || s.Fehlerhaft;
        foreach (var teil in s.Rahmen) teil.MaterialOverride = s.Fehlerhaft ? _rahmenRot : _rahmenGold;
        s.Fuellung.MaterialOverride = s.Fehlerhaft ? _flaecheRot : _flaecheGold;
        // gewählt UND fehlerhaft: der Rahmen rot, die Füllung gold – beides ist zu sehen
        if (gewaehlt && s.Fehlerhaft) s.Fuellung.MaterialOverride = _flaecheGold;
    }

    private void FehlerUeberzug(Stueck s, bool fehlerhaft)
    {
        _fehlerUeberzug ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(Fehlerrot, 0.42f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        s.Fehlerhaft = fehlerhaft;
        foreach (var teil in s.Modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            teil.MaterialOverlay = fehlerhaft ? _fehlerUeberzug : null;
    }

    /// <summary>Die Wege, Flächen und Pflanzen mit einem Fehler (aus <see cref="Befund.Andere"/>).</summary>
    private readonly HashSet<EntwurfTeil> _wsFehlerTeile = new(ReferenceEqualityComparer.Instance);

    /// <summary>Nach jeder Änderung: Welche Teile haben einen Fehler? Nur wo sich das ändert, wird umgefärbt.</summary>
    private void FehlerZeigen()
    {
        _wsFehlerNeu = false;
        if (_entwurf is null) return;
        var fehlerhaft = _entwurf.FehlerhafteGebaeude;
        foreach (var (g, s) in _stuecke)
        {
            var neu = fehlerhaft.Contains(g.Kennung);
            if (neu == s.Fehlerhaft) continue;
            FehlerUeberzug(s, neu);
            MarkierungSetzen(g, s);
        }
        var andere = new HashSet<EntwurfTeil>(_entwurf.Befunde.Where(b => b.Schwere == Schwere.Fehler).SelectMany(b => b.Andere), ReferenceEqualityComparer.Instance);
        var wechsel = _wsFehlerTeile.Where(t => !andere.Contains(t)).Concat(andere.Where(t => !_wsFehlerTeile.Contains(t))).ToList();
        _wsFehlerTeile.Clear();
        _wsFehlerTeile.UnionWith(andere);
        foreach (var t in wechsel) TeilMarkieren(t);
    }

    /// <summary>Hat der Teil (Weg, Fläche, Pflanze) einen Fehler?</summary>
    private bool Fehlerhaft(EntwurfTeil t) => _wsFehlerTeile.Contains(t);

    /// <summary>Zeichnet die Markierung eines Teils neu (gewählt: gold, Fehler: rot).</summary>
    private void TeilMarkieren(EntwurfTeil t)
    {
        switch (t)
        {
            case EntwurfGebaeude g when _stuecke.TryGetValue(g, out var s):
                MarkierungSetzen(g, s);
                break;
            case EntwurfWeg w:
                WegMarkieren(w);
                break;
            case EntwurfFlaeche f:
                FlaecheMarkieren(f);
                break;
            case EntwurfPflanze p:
                PflanzeMarkieren(p);
                break;
            case EntwurfMauer m:
                MauerMarkieren(m);
                break;
            case EntwurfFluss fluss:
                FlussMarkieren(fluss, false);
                break;
            case EntwurfFaehre faehre:
                FaehreMarkieren(faehre, false);
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahl und Kamera
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wählt ein Gebäude über seine Kennung (null: nichts) – mit <paramref name="hinfahren"/> fährt die Kamera hin.</summary>
    public void WerkstattWaehlen(string? kennung, bool hinfahren = false) => WerkstattWaehlen(_entwurf?.GebaeudeMit(kennung), hinfahren);

    /// <summary>Wählt einen Teil (null: nichts) – mit <paramref name="hinfahren"/> fährt die Kamera hin.</summary>
    public void WerkstattWaehlen(EntwurfTeil? teil, bool hinfahren = false)
    {
        if (teil is not null && _entwurf?.Enthaelt(teil) != true) teil = null;
        var vorher = WerkstattGewaehlt;
        WerkstattGewaehlt = teil;
        _wsGriff = null;
        Auswahl = teil is EntwurfGebaeude g ? StueckKennung(g) : null; // der Name bleibt sichtbar
        if (vorher is not null && !ReferenceEquals(vorher, teil)) TeilMarkieren(vorher);
        if (teil is not null) TeilMarkieren(teil);
        GriffeBauen();
        if (hinfahren && teil is not null) WerkstattHinfahren(MitteVon(teil));
        if (!ReferenceEquals(vorher, teil)) WerkstattAuswahlGeaendert?.Invoke(teil);
    }

    /// <summary>Wo ein Teil liegt (zum Hinfahren): Punkt, Mitte des Verlaufs, Mitte der Fläche bzw. der Ecken einer Mauer.</summary>
    public static Kartenort MitteVon(EntwurfTeil t) => t switch
    {
        EntwurfGebaeude g => g.Punkt,
        EntwurfPflanze p => p.Punkt,
        EntwurfWeg { Verlauf.Count: > 0 } w => w.Verlauf[w.Verlauf.Count / 2],
        EntwurfFluss { Punkte.Count: > 0 } fl => fl.Verlauf[fl.Verlauf.Count / 2],
        EntwurfFaehre fa => new Kartenort((fa.Von.X + fa.Bis.X) / 2, (fa.Von.Z + fa.Bis.Z) / 2),
        EntwurfFlaeche { Ecken.Count: > 0 } f => new Kartenort(f.Ecken.Average(e => e.X), f.Ecken.Average(e => e.Z)),
        EntwurfMauer { Ecken.Count: > 0 } m => new Kartenort(m.Ecken.Average(e => e.X), m.Ecken.Average(e => e.Z)),
        _ => default,
    };

    /// <summary>Fährt die Kamera zu einem Ort der Karte (Zoom bleibt).</summary>
    public void WerkstattHinfahren(Kartenort ort) => Kamerafahrt(new Vector3((float)ort.X, 0, (float)ort.Z), _kamera.Size);

    /// <summary>Wohin die Kamera blickt, wie weit gezoomt und wie gedreht – damit die Werkstatt nach „Karte ausprobieren“ genauso weitermacht.</summary>
    public (Vector3 Punkt, float Zoom, float Drehung) WerkstattKamera
    {
        get => (_kameraDrehung.Position, _kamera.Size, _zielDrehung);
        set
        {
            _kameraDrehung.Position = value.Punkt;
            KameraHoeheSofort();
            _kamera.Size = Mathf.Clamp(value.Zoom, KameraMinZoom, KameraMaxZoom);
            _zielDrehung = value.Drehung;
            _kameraDrehung.RotationDegrees = new Vector3(0, _zielDrehung, 0);
        }
    }

    /// <summary>
    /// Löst die Stadtansicht vom Entwurf, wenn die Szene geht: Der Entwurf kann sie überleben („Karte ausprobieren“ hält ihn samt Rückgängig für
    /// die Rückkehr) und darf dann keine Ereignisse an eine freigegebene Stadtansicht schicken.
    /// </summary>
    public void WerkstattLoesen()
    {
        if (_entwurf is null) return;
        _entwurf.Geaendert -= EntwurfGeaendert;
        _entwurf.Fangpunkte = null;
    }

    /// <summary>Wo ein Ort der Karte auf dem Bildschirm liegt (für Klicktests, <c>--werkstatt-bildpunkte</c>), gerundet auf ganze Bildpunkte.</summary>
    public string WerkstattBildpunkt(double x, double z)
    {
        var p = _kamera.UnprojectPosition(new Vector3((float)x, BodenHoehe((float)x, (float)z), (float)z));
        return $"{Mathf.RoundToInt(p.X)},{Mathf.RoundToInt(p.Y)}";
    }

    /// <summary>Meter je Bildpunkt bei diesem Zoom (die sichtbare Höhe entspricht <c>_kamera.Size</c>).</summary>
    private float MeterJePixel => _kamera.Size / GetViewport().GetVisibleRect().Size.Y;

    /// <summary>Der Boden unter der Maus – null über einer Tafel, bei gesperrter Werkstatt oder außerhalb.</summary>
    private Vector2? MausBoden()
    {
        if (WerkstattGesperrt || GetViewport().GuiGetHoveredControl() is not null) return null;
        return BodenpunktBei(GetViewport().GetMousePosition());
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeug
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein anderes Werkzeug wurde gewählt (die Tafel rechts zeigt bei Pinsel und Radierer deren Regler).</summary>
    public event Action? WerkstattWerkzeugGeaendert;

    /// <summary>
    /// Wählt das Werkzeug: <paramref name="baustein"/> ist bei <see cref="Werkstattwerkzeug.Baustein"/> und <see cref="Werkstattwerkzeug.Pflanze"/>
    /// die Kennung im Katalog. Eine Aussparung braucht eine gewählte Naturfläche – ohne sie bleibt die Auswahl (false). Laufendes Zeichnen und Ziehen
    /// werden abgebrochen.
    /// </summary>
    public bool WerkstattWerkzeugWaehlen(Werkstattwerkzeug werkzeug, string? baustein = null)
    {
        ZiehenAbbrechen();
        TeilZiehenAbbrechen();
        ZeichnenAbbrechen();
        StrichAbbrechen();
        GelaendeWerkzeugAus(); // Stadtansicht.Kartenwerkstatt.Gelaende.cs
        BodenWerkzeugAus();    // Stadtansicht.Kartenwerkstatt.Boden.cs
        WasserWerkzeugAus();   // Stadtansicht.Kartenwerkstatt.Wasser.cs
        var b = _entwurf?.Katalog.Finde(baustein);
        if (werkzeug == Werkstattwerkzeug.Baustein && (b is null || b.Art == Bausteinart.Natur)) werkzeug = Werkstattwerkzeug.Auswahl;
        if (werkzeug == Werkstattwerkzeug.Pflanze && b is not { Art: Bausteinart.Natur }) werkzeug = Werkstattwerkzeug.Auswahl;
        var ok = true;
        if (werkzeug == Werkstattwerkzeug.Aussparung && WerkstattGewaehlt is not EntwurfFlaeche { KannAussparungen: true })
        {
            WerkstattMeldung?.Invoke("Für eine Aussparung erst eine Wald-, Streu- oder Feldfläche wählen.", true);
            werkzeug = Werkstattwerkzeug.Auswahl;
            ok = false;
        }
        Kartenwerkzeug = werkzeug;
        WerkstattBaustein = werkzeug is Werkstattwerkzeug.Baustein or Werkstattwerkzeug.Pflanze ? baustein : null;
        _wsGeprueft = null;
        if (werkzeug != Werkstattwerkzeug.Baustein && _wsUmriss is not null) _wsUmriss.Visible = false;
        if (werkzeug != Werkstattwerkzeug.Pflanze) PflanzenGeistAus();
        if (werkzeug is not (Werkstattwerkzeug.Pinsel or Werkstattwerkzeug.Radierer)) PinselAus();
        if (werkzeug != Werkstattwerkzeug.Platz) PlatzwahlAus(); // Stadtansicht.Kartenwerkstatt.Start.cs
        WerkstattWerkzeugGeaendert?.Invoke();
        return ok;
    }

    /// <summary>Wählt einen Baustein zum Setzen (null: Setzen beenden) – eine Natur-Art setzt Pflanzen. Die Auswahl bleibt.</summary>
    public void WerkstattBausteinWaehlen(string? katalog)
    {
        var b = _entwurf?.Katalog.Finde(katalog);
        WerkstattWerkzeugWaehlen(b is null ? Werkstattwerkzeug.Auswahl : b.Art == Bausteinart.Natur ? Werkstattwerkzeug.Pflanze : Werkstattwerkzeug.Baustein, katalog);
    }

    /// <summary>Beendet das Werkzeug aus der Stadt heraus (Esc, Rechtsklick) – zurück zur Auswahl, der Katalog hebt seine Wahl auf.</summary>
    private void WerkzeugBeenden()
    {
        WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Auswahl);
        WerkstattWerkzeugBeendet?.Invoke();
    }

    /// <summary>Führt einen Befehl aus; geht er nicht (der Teil ist inzwischen weg, zu wenige Punkte …), steht der Grund unten in der Leiste.</summary>
    private T? Befehl<T>(Func<T> befehl)
    {
        try
        {
            return befehl();
        }
        catch (ArgumentException fehler)
        {
            WerkstattMeldung?.Invoke(fehler.Message.Split(" (Parameter")[0], true);
            return default;
        }
    }

    private void Befehl(Action befehl) => Befehl(() => { befehl(); return true; });

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Für Startoptionen: Der Umriss des gewählten Bausteins steht an dieser Stelle (Meter), bis die Maus bewegt wird.</summary>
    public void WerkstattUmrissSetzen(double x, double z) => _wsOrt = new Vector2((float)x, (float)z);

    /// <summary>Nur für Startoptionen: Umriss, Pinsel und Vorschau stehen hier, bis die Maus bewegt wird.</summary>
    private Vector2? _wsOrt;

    /// <summary>Jedes Bild (aus der Szene): Änderungen nachziehen, dann Umriss, Vorschau, Griffe und Pinsel der Maus nachführen.</summary>
    public void WerkstattAktualisieren()
    {
        if (_entwurf is null) return;
        Nachziehen();
        GriffAenderungMelden(); // Stadtansicht.Kartenwerkstatt.Wasser.cs
        GriffeSkalieren();
        var maus = _wsOrt ?? MausBoden();
        VorschauLeeren();
        if (_wsZiehen is { Aktiv: true } z)
        {
            ZiehenNachfuehren(z);
            return;
        }
        if (_wsTeilZiehen is { } t)
        {
            TeilZiehenNachfuehren(t, maus);
            return;
        }
        switch (Kartenwerkzeug)
        {
            case Werkstattwerkzeug.Baustein when WerkstattBaustein is { } katalog:
                if (maus is { } ort)
                {
                    var (x, zz) = Einrasten(ort.X, ort.Y);
                    UmrissZeigen(katalog, x, zz, WerkstattDrehung, 1, null);
                }
                else if (_wsUmriss is not null) _wsUmriss.Visible = false;
                break;
            case Werkstattwerkzeug.Pflanze:
                PflanzenGeistZeigen(maus);
                break;
            case Werkstattwerkzeug.Weg:
                WegVorschau(maus);
                break;
            case Werkstattwerkzeug.Bauzone or Werkstattwerkzeug.Wald or Werkstattwerkzeug.Streu or Werkstattwerkzeug.Feld or Werkstattwerkzeug.Aussparung
                or Werkstattwerkzeug.See:
                FlaechenVorschau(maus);
                break;
            case Werkstattwerkzeug.Fluss:
                FlussVorschau(maus); // Stadtansicht.Kartenwerkstatt.Wasser.cs
                break;
            case Werkstattwerkzeug.Faehre:
                FaehreVorschau(maus);
                break;
            case Werkstattwerkzeug.Pinsel or Werkstattwerkzeug.Radierer:
                PinselZeigen(maus);
                break;
            case Werkstattwerkzeug.Gelaende:
                GelaendePinselZeigen(maus);
                break;
            case Werkstattwerkzeug.Boden:
                BodenPinselZeigen(maus);
                break;
            case Werkstattwerkzeug.Rampe:
                RampeZeigen(maus);
                break;
            case Werkstattwerkzeug.Mauer:
                MauerVorschau(maus);
                break;
            case Werkstattwerkzeug.Tor:
                TorVorschau(maus);
                break;
            case Werkstattwerkzeug.Platz:
                PlatzVorschau(maus);
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Setzen: der Umriss an der Maus
    // ═════════════════════════════════════════════════════════════════════════

    private Node3D? _wsUmriss;
    private MeshInstance3D? _wsFlaeche, _wsVorplatz, _wsKoerper, _wsPfeil;
    private StandardMaterial3D? _wsFarbe, _wsVorplatzFarbe, _wsKoerperFarbe, _wsPfeilFarbe;
    private Label3D? _wsText;

    /// <summary>Was zuletzt geprüft wurde (Baustein, Punkt, Drehung, Kennung) und mit welchem Ergebnis – geprüft wird nur, wenn sich das ändert.</summary>
    private (string Katalog, double X, double Z, double Drehung, double Massstab, string? Kennung)? _wsGeprueft;

    private string? _wsGrund, _wsHinweis;

    /// <summary>Auf das 1-m-Raster – mit gehaltener Alt-Taste frei (der Kern rundet dann auf 5 cm).</summary>
    private static (double X, double Z) Einrasten(double x, double z) =>
        Input.IsKeyPressed(Key.Alt) ? (x, z) : (Kartenraster.Runden(x), Kartenraster.Runden(z));

    /// <summary>Zeigt den Umriss eines Bausteins an einer Stelle: grün ohne Fehler, rot mit dem ersten Fehler als Text.</summary>
    private void UmrissZeigen(string katalog, double x, double z, double drehung, double massstab, string? kennung)
    {
        if (_entwurf is null || _entwurf.Katalog.Finde(katalog) is not { } baustein) return;
        var stand = (katalog, x, z, drehung, massstab, kennung);
        if (_wsGeprueft != stand)
        {
            _wsGeprueft = stand;
            var befunde = Kartenpruefung.PruefeStelle(_entwurf, katalog, x, z, drehung, massstab, kennung).ToList();
            _wsGrund = befunde.FirstOrDefault(b => b.Schwere == Schwere.Fehler)?.Text;
            _wsHinweis = _wsGrund is null ? befunde.FirstOrDefault(b => b.Schwere == Schwere.Hinweis)?.Text : null; // z. B. ein Hang von 8 bis 15 %: gelb
        }
        var groesse = baustein.Groesse is { Length: 2 } gr ? new Vector2((float)gr[0], (float)gr[1]) * (float)massstab : new Vector2(4, 4);
        if (_wsUmriss is null)
        {
            _wsUmriss = new Node3D { Name = "Werkstattumriss" };
            AddChild(_wsUmriss);
            _wsFarbe = Transparent(Colors.White, 0.45f);
            _wsVorplatzFarbe = Transparent(Colors.White, 0.3f);
            _wsKoerperFarbe = Transparent(Colors.White, 0.16f);
            _wsPfeilFarbe = Transparent(Colors.White, 0.85f);
            _wsFlaeche = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _wsFarbe };
            _wsVorplatz = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _wsVorplatzFarbe };
            _wsKoerper = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _wsKoerperFarbe };
            _wsPfeil = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(2.4f, 1.2f, 0.3f) }, MaterialOverride = _wsPfeilFarbe, RotationDegrees = new Vector3(90, 0, 0) };
            foreach (var teil in new[] { _wsFlaeche, _wsVorplatz, _wsKoerper, _wsPfeil })
            {
                teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _wsUmriss.AddChild(teil);
            }
            _wsText = Beschriftung("", Vector3.Zero, 40, _wsUmriss);
            _wsText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _wsText.Width = 620;
        }
        const float hoehe = 6;
        var vorplatz = (float)Kartenpruefung.Vorplatz;
        _wsUmriss.Visible = true;
        _wsUmriss.Position = new Vector3((float)x, FundamentVon(new Vector3((float)x, 0, (float)z), Mathf.DegToRad((float)drehung), groesse).Hoehe, (float)z); // auf Gelände wie das Haus stünde
        _wsUmriss.Rotation = new Vector3(0, Mathf.DegToRad((float)drehung), 0);
        var farbe = _wsGrund is not null ? new Color(0.95f, 0.28f, 0.25f) : _wsHinweis is not null ? new Color(0.96f, 0.80f, 0.25f) : new Color(0.35f, 0.9f, 0.35f);
        _wsFarbe!.AlbedoColor = new Color(farbe, 0.45f);
        _wsVorplatzFarbe!.AlbedoColor = new Color(farbe.Lightened(0.35f), 0.3f);
        _wsKoerperFarbe!.AlbedoColor = new Color(farbe, 0.16f);
        _wsPfeilFarbe!.AlbedoColor = new Color(farbe.Lightened(0.5f), 0.85f);
        ((BoxMesh)_wsFlaeche!.Mesh).Size = new Vector3(groesse.X, 0.12f, groesse.Y);
        _wsFlaeche.Position = new Vector3(0, 0.14f, 0);
        ((BoxMesh)_wsVorplatz!.Mesh).Size = new Vector3(groesse.X, 0.16f, vorplatz);
        _wsVorplatz.Position = new Vector3(0, 0.16f, groesse.Y / 2 - vorplatz / 2);
        ((BoxMesh)_wsKoerper!.Mesh).Size = new Vector3(Mathf.Max(0.4f, groesse.X - 0.4f), hoehe, Mathf.Max(0.4f, groesse.Y - vorplatz - 0.4f));
        _wsKoerper.Position = new Vector3(0, hoehe / 2, -vorplatz / 2);
        _wsPfeil!.Position = new Vector3(0, 0.3f, groesse.Y / 2 - vorplatz / 2); // die Spitze zeigt zur Vorderseite (+z)
        _wsText!.Position = new Vector3(0, hoehe + 4, 0);
        _wsText.Text = _wsGrund ?? (_wsHinweis is not null ? "Hinweis: " + _wsHinweis : kennung is null ? $"{baustein.Anzeigename} – passt" : "passt");
        _wsText.Modulate = farbe.Lightened(0.35f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ziehen eines Gebäudes
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Stück, das gerade gezogen wird: wo die Maus anfasste, der Abstand zur Mitte und die vorläufige Lage.</summary>
    private sealed class Ziehvorgang
    {
        public required EntwurfGebaeude Gebaeude { get; init; }
        public required Vector2 Start { get; init; }
        public required Vector2 Versatz { get; init; }
        public bool Aktiv { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double Drehung { get; set; }
    }

    private Ziehvorgang? _wsZiehen;

    /// <summary>So viele Pixel muss die Maus mit gedrückter Taste wandern, bis aus dem Klick ein Ziehen wird.</summary>
    private const float ZiehSchwelle = 5;

    private void ZiehenNachfuehren(Ziehvorgang z)
    {
        if (BodenpunktBei(GetViewport().GetMousePosition()) is { } boden)
            (z.X, z.Z) = Einrasten(boden.X + z.Versatz.X, boden.Y + z.Versatz.Y);
        if (_stuecke.TryGetValue(z.Gebaeude, out var s))
        {
            s.Wurzel.Position = new Vector3((float)z.X, WerkstattFussboden(z.Gebaeude, z.X, z.Z, z.Drehung), (float)z.Z);
            s.Wurzel.Rotation = new Vector3(0, Mathf.DegToRad((float)z.Drehung), 0);
        }
        UmrissZeigen(z.Gebaeude.Katalog, z.X, z.Z, z.Drehung, z.Gebaeude.Massstab, z.Gebaeude.Kennung);
    }

    /// <summary>Loslassen: Hat sich Lage oder Drehung geändert, wird daraus ein Befehl (ein Rückgängig-Schritt).</summary>
    private void ZiehenBeenden()
    {
        if (_wsZiehen is not { } z || _entwurf is null) return;
        _wsZiehen = null;
        if (_wsUmriss is not null) _wsUmriss.Visible = false;
        if (!z.Aktiv) return;
        var g = z.Gebaeude;
        if (Kartenraster.Punkt(z.X) != g.X || Kartenraster.Punkt(z.Z) != g.Z || Kartenraster.Drehung(z.Drehung) != g.Drehung)
            Befehl(() => _entwurf.Versetzen(g.Kennung, z.X, z.Z, z.Drehung)); // baut das Stück neu (EntwurfGeaendert)
        else if (_stuecke.TryGetValue(g, out var s))
        {
            s.Wurzel.Position = new Vector3((float)g.X, WerkstattFussboden(g, g.X, g.Z, g.Drehung), (float)g.Z);
            s.Wurzel.Rotation = new Vector3(0, Mathf.DegToRad((float)g.Drehung), 0);
        }
    }

    /// <summary>Esc beim Ziehen: Das Stück springt an seinen Platz zurück, es entsteht kein Befehl.</summary>
    private void ZiehenAbbrechen()
    {
        if (_wsZiehen is not { } z) return;
        _wsZiehen = null;
        if (_wsUmriss is not null) _wsUmriss.Visible = false;
        if (!_stuecke.TryGetValue(z.Gebaeude, out var s)) return;
        s.Wurzel.Position = new Vector3((float)z.Gebaeude.X, WerkstattFussboden(z.Gebaeude, z.Gebaeude.X, z.Gebaeude.Z, z.Gebaeude.Drehung), (float)z.Gebaeude.Z);
        s.Wurzel.Rotation = new Vector3(0, Mathf.DegToRad((float)z.Gebaeude.Drehung), 0);
    }

    /// <summary>Die Höhe des Fußbodens eines Gebäudes des Entwurfs an einer Lage (beim Ziehen): der höchste Punkt des Bodens unter seiner Fläche; ohne Fläche oder auf flacher Karte 0.</summary>
    private float WerkstattFussboden(EntwurfGebaeude g, double x, double z, double drehung) =>
        g.Groesse is var (b, t) ? FundamentVon(new Vector3((float)x, 0, (float)z), Mathf.DegToRad((float)drehung), new Vector2((float)b, (float)t)).Hoehe : 0f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Eingabe
    // ═════════════════════════════════════════════════════════════════════════

    private float _wsRechtsGezogen;

    /// <summary>
    /// Die Eingabe der Kartenwerkstatt – vor der Kamera (wie <see cref="BauEingabe"/>). Gibt true zurück, wenn verbraucht. Mausrad, rechte
    /// Maustaste und Strg+Q/E bleiben bei der Kamera, Strg+Z/Y/S bei der Szene.
    /// </summary>
    private bool WerkstattEingabe(InputEvent eingabe)
    {
        if (_entwurf is null) return false;
        if (WerkstattGesperrt) return eingabe is InputEventMouseButton { ButtonIndex: MouseButton.Left };
        // Das Mausrad über einer Tafel, die nicht (mehr) rollen kann, zoomt nicht die Kamera
        if (eingabe is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } && GetViewport().GuiGetHoveredControl() is not null) return true;
        var zeichnet = Kartenwerkzeug is Werkstattwerkzeug.Weg or Werkstattwerkzeug.Bauzone or Werkstattwerkzeug.Wald
            or Werkstattwerkzeug.Streu or Werkstattwerkzeug.Feld or Werkstattwerkzeug.Aussparung or Werkstattwerkzeug.Mauer
            or Werkstattwerkzeug.Fluss or Werkstattwerkzeug.See;
        switch (eingabe)
        {
            case InputEventKey { Pressed: true, Keycode: Key.Q or Key.E, CtrlPressed: false } taste:
                Drehen(taste.Keycode == Key.Q ? 1 : -1, taste.AltPressed, taste.Echo);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Pageup or Key.Pagedown } taste:
                _zielDrehung += taste.Keycode == Key.Pageup ? -90 : 90; // die Kamera (Q/E drehen hier das Stück)
                return true;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter } when zeichnet:
                ZeichnenBeenden();
                return true;
            case InputEventKey { Pressed: true, Keycode: Key.Backspace } when zeichnet && _wsPunkte.Count > 0:
                _wsPunkte.RemoveAt(_wsPunkte.Count - 1);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Delete } when _wsZiehen is null && _wsTeilZiehen is null:
                Loeschen();
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                if (_wsZiehen is not null) ZiehenAbbrechen();
                else if (_wsTeilZiehen is not null) TeilZiehenAbbrechen();
                else if (_wsStrich is not null) StrichAbbrechen();
                else if (_wsGelaendeStrich) GelaendeStrichAbbrechen();
                else if (_wsBodenStrich) BodenStrichAbbrechen();
                else if (_wsRampeAnfang is not null) _wsRampeAnfang = null;
                else if (_wsFaehreStart is not null) _wsFaehreStart = null;
                else if (_wsPunkte.Count > 0) ZeichnenAbbrechen();
                else if (Kartenwerkzeug != Werkstattwerkzeug.Auswahl) WerkzeugBeenden();
                else if (_wsGriff is not null) { _wsGriff = null; GriffeBauen(); }
                else if (WerkstattGewaehlt is not null) WerkstattWaehlen((EntwurfTeil?)null);
                else if (WerkstattKapazitaetSichtbar) WerkstattKapazitaetZeigen(null);
                else return false;
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } klick:
                LinksDruecken(klick.Position, klick.DoubleClick);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                ZiehenBeenden();
                TeilZiehenBeenden();
                StrichBeenden();
                GelaendeStrichBeenden();
                BodenStrichBeenden();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rechts:
                if (rechts.Pressed) _wsRechtsGezogen = 0;
                else if (_wsRechtsGezogen < 6 && Kartenwerkzeug != Werkstattwerkzeug.Auswahl && _wsPunkte.Count == 0 && _wsStrich is null && !_wsGelaendeStrich && !_wsBodenStrich) WerkzeugBeenden();
                return false; // das Ziehen der Kamera läuft weiter
            case InputEventMouseMotion bewegung:
                _wsOrt = null;
                if (Input.IsMouseButtonPressed(MouseButton.Right)) _wsRechtsGezogen += bewegung.Relative.Length();
                if (_wsZiehen is { Aktiv: false } z && bewegung.Position.DistanceTo(z.Start) > ZiehSchwelle) z.Aktiv = true;
                if (_wsTeilZiehen is { Aktiv: false } t && bewegung.Position.DistanceTo(t.Start) > ZiehSchwelle) t.Aktiv = true;
                StrichWeiter(bewegung.Position);
                GelaendeStrichWeiter(bewegung.Position);
                BodenStrichWeiter(bewegung.Position);
                return false;
        }
        return false;
    }

    /// <summary>Linke Maustaste: was sie tut, sagt das Werkzeug.</summary>
    private void LinksDruecken(Vector2 maus, bool doppelklick)
    {
        if (_entwurf is null) return;
        var boden = BodenpunktBei(maus);
        switch (Kartenwerkzeug)
        {
            case Werkstattwerkzeug.Baustein when WerkstattBaustein is { } katalog:
                // wo der Umriss steht (Startoption) – sonst, wo geklickt wurde (der Umriss folgt der Maus erst im nächsten Bild)
                var (x, z) = _wsOrt is null && boden is { } b ? Einrasten(b.X, b.Y) : _wsUmriss is { Visible: true } u ? (u.Position.X, u.Position.Z) : (double.NaN, 0);
                if (double.IsNaN(x)) return;
                if (Befehl(() => _entwurf.Setzen(katalog, x, z, WerkstattDrehung)) is { } g) WerkstattWaehlen(g);
                return;
            case Werkstattwerkzeug.Pflanze when WerkstattBaustein is { } art && boden is { } ort:
                if (Befehl(() => _entwurf.PflanzeSetzen(art, ort.X, ort.Y, WerkstattDrehung)) is { } p) WerkstattWaehlen(p);
                return;
            case Werkstattwerkzeug.Weg or Werkstattwerkzeug.Bauzone or Werkstattwerkzeug.Wald or Werkstattwerkzeug.Streu or Werkstattwerkzeug.Feld
                or Werkstattwerkzeug.Aussparung or Werkstattwerkzeug.Mauer or Werkstattwerkzeug.Fluss or Werkstattwerkzeug.See:
                ZeichnenKlick(maus, boden, doppelklick); // Stadtansicht.Kartenwerkstatt.Flaechen.cs
                return;
            case Werkstattwerkzeug.Faehre:
                FaehreKlick(boden); // Stadtansicht.Kartenwerkstatt.Wasser.cs
                return;
            case Werkstattwerkzeug.Pinsel or Werkstattwerkzeug.Radierer:
                StrichBeginnen(maus); // Stadtansicht.Kartenwerkstatt.Natur.cs
                return;
            case Werkstattwerkzeug.Gelaende:
                GelaendeStrichBeginnen(maus); // Stadtansicht.Kartenwerkstatt.Gelaende.cs
                return;
            case Werkstattwerkzeug.Boden:
                BodenStrichBeginnen(maus); // Stadtansicht.Kartenwerkstatt.Boden.cs
                return;
            case Werkstattwerkzeug.Rampe:
                RampeKlick(boden);
                return;
            case Werkstattwerkzeug.Tor:
                TorKlick(boden); // Stadtansicht.Kartenwerkstatt.Mauern.cs
                return;
            case Werkstattwerkzeug.Platz:
                PlatzKlick(maus, boden); // Stadtansicht.Kartenwerkstatt.Start.cs
                return;
        }
        AuswahlKlick(maus, boden);
    }

    /// <summary>
    /// Ein Klick mit dem Werkzeug „Auswahl“: mit gehaltener Umschalttaste auf eine Mauer wählt sie und zieht sie ganz (Stadtansicht.Kartenwerkstatt.Mauern.cs);
    /// sonst zuerst die Griffe des gewählten Teils (ziehen), dann die Linie des gewählten Weges, der Rand der
    /// gewählten Fläche bzw. ein Tor oder die Linie der gewählten Mauer (Stützpunkt bzw. Ecke einfügen, gleich weiterziehen). Dann, was unter der
    /// Maus liegt: ein Gebäude, dessen Modell über dem Boden getroffen wird (<see cref="StueckTreffer"/>), ein Tor, eine Pflanze, ein Weg, ein
    /// flaches Gebäude (Feld, Weide), eine Mauer, die Grundfläche eines Gebäudes, das Innere der gewählten Fläche (ganze Fläche ziehen), Bauzonen
    /// und Naturflächen. Ein Klick ins Leere hebt die Auswahl auf.
    /// </summary>
    private void AuswahlKlick(Vector2 maus, Vector2? boden)
    {
        if (_entwurf is null) return;
        var toleranz = Mathf.Max(0.6f, MeterJePixel * 8);
        if (Input.IsKeyPressed(Key.Shift) && boden is { } ganz && MauerBei(ganz, toleranz) is { } ganzeMauer)
        {
            WerkstattWaehlen(ganzeMauer);
            TeilZiehenBeginnen(ganzeMauer, Ziehart.Ganz, maus, ganz, -1, -1);
            return;
        }
        if (GriffBei(maus) is { } griff && WerkstattGewaehlt is { } gewaehlt)
        {
            _wsGriff = griff;
            GriffeBauen();
            TeilZiehenBeginnen(gewaehlt, Ziehart.Griff, maus, boden ?? Vector2.Zero, griff.Vieleck, griff.Index);
            return;
        }
        if (boden is not { } p)
        {
            WerkstattWaehlen((EntwurfTeil?)null);
            return;
        }
        if (WerkstattGewaehlt is EntwurfWeg gewaehlterWeg && AufWeg(gewaehlterWeg, p, toleranz))
        {
            var (index, _, _) = gewaehlterWeg.NaechsteStelle(new Kartenort(p.X, p.Y));
            TeilZiehenBeginnen(gewaehlterWeg, Ziehart.Einfuegen, maus, p, -1, index);
            return;
        }
        if (WerkstattGewaehlt is EntwurfFluss gewaehlterFluss && AufFluss(gewaehlterFluss, p, toleranz))
        {
            var (index, _, _) = gewaehlterFluss.NaechsteStelle(new Kartenort(p.X, p.Y));
            TeilZiehenBeginnen(gewaehlterFluss, Ziehart.Einfuegen, maus, p, -1, index);
            return;
        }
        if (WerkstattGewaehlt is EntwurfFlaeche gewaehlteFlaeche && AmRand(gewaehlteFlaeche, p, toleranz) is var (vieleck, kante))
        {
            TeilZiehenBeginnen(gewaehlteFlaeche, Ziehart.Einfuegen, maus, p, vieleck, kante + 1);
            return;
        }
        if (WerkstattGewaehlt is EntwurfMauer gewaehlteMauer)
        {
            if (_entwurf.TorBei(p.X, p.Y, VorschauTorBreite / 2) is var (torMauer, tor, _) && ReferenceEquals(torMauer, gewaehlteMauer))
            {
                _wsGriff = new Griff(TorGriffe, tor);
                GriffeBauen();
                TeilZiehenBeginnen(gewaehlteMauer, Ziehart.Griff, maus, p, TorGriffe, tor);
                return;
            }
            if (ReferenceEquals(MauerBei(p, toleranz), gewaehlteMauer))
            {
                TeilZiehenBeginnen(gewaehlteMauer, Ziehart.Einfuegen, maus, p, -1, gewaehlteMauer.NaechsteStelle(new Kartenort(p.X, p.Y)).Strecke + 1);
                return;
            }
        }
        var treffer = StueckTreffer(maus);
        if (treffer is { Hoehe: >= 0.6f } hoch)
        {
            GebaeudeZiehenBeginnen(hoch.Gebaeude, maus, p);
            return;
        }
        if (_entwurf.TorBei(p.X, p.Y, Mathf.Max((float)Kartenentwurf.Torfang, toleranz)) is var (mauer, torNummer, _))
        {
            WerkstattWaehlen(mauer);
            _wsGriff = new Griff(TorGriffe, torNummer);
            GriffeBauen();
            TeilZiehenBeginnen(mauer, Ziehart.Griff, maus, p, TorGriffe, torNummer);
            return;
        }
        if (PflanzeBei(p, toleranz) is { } pflanze)
        {
            WerkstattWaehlen(pflanze);
            TeilZiehenBeginnen(pflanze, Ziehart.Ganz, maus, p, -1, -1);
            return;
        }
        if (FaehreBei(p, toleranz) is { } faehre)
        {
            WerkstattWaehlen(faehre);
            return;
        }
        if (WegBei(p, toleranz) is { } weg)
        {
            WerkstattWaehlen(weg);
            return;
        }
        if (FlussBei(p, toleranz) is { } fluss) // Wege und Brücken gehen vor (sie liegen über dem Wasser), dann der Fluss
        {
            WerkstattWaehlen(fluss);
            return;
        }
        if ((treffer?.Gebaeude ?? (MauerBei(p, toleranz) is null ? GebaeudeAufGrund(p) : null)) is { } gebaeude)
        {
            GebaeudeZiehenBeginnen(gebaeude, maus, p);
            return;
        }
        if (MauerBei(p, toleranz) is { } mauerLinie)
        {
            WerkstattWaehlen(mauerLinie);
            return;
        }
        if (WerkstattGewaehlt is EntwurfFlaeche innen && InFlaeche(innen, p))
        {
            TeilZiehenBeginnen(innen, Ziehart.Ganz, maus, p, -1, -1);
            return;
        }
        WerkstattWaehlen(FlaecheBei(p));
    }

    /// <summary>Wählt ein Gebäude und hält es zum Ziehen fest (ein Ziehen beginnt erst nach ein paar Bildpunkten).</summary>
    private void GebaeudeZiehenBeginnen(EntwurfGebaeude gebaeude, Vector2 maus, Vector2 boden)
    {
        WerkstattWaehlen(gebaeude);
        _wsZiehen = new Ziehvorgang
        {
            Gebaeude = gebaeude, Start = maus, Versatz = new Vector2((float)gebaeude.X - boden.X, (float)gebaeude.Z - boden.Y),
            X = gebaeude.X, Z = gebaeude.Z, Drehung = gebaeude.Drehung,
        };
    }

    /// <summary>Entf: den gewählten Griff (Stützpunkt, Ecke) – ohne Griff den gewählten Teil.</summary>
    private void Loeschen()
    {
        if (_entwurf is null || WerkstattGewaehlt is not { } teil) return;
        if (_wsGriff is { } griff)
        {
            switch (teil)
            {
                case EntwurfMauer m when !MauerGriffLoeschen(m, griff): // Stadtansicht.Kartenwerkstatt.Mauern.cs
                    return;
                case EntwurfWeg w when !Befehl(() => _entwurf.StuetzpunktLoeschen(w, griff.Index)):
                    WerkstattMeldung?.Invoke("Ein Weg braucht mindestens zwei Stützpunkte – Entf ohne gewählten Griff löscht den ganzen Weg.", true);
                    return;
                case EntwurfFluss fl when !Befehl(() => _entwurf.FlussPunktLoeschen(fl, griff.Index)):
                    WerkstattMeldung?.Invoke("Ein Fluss braucht mindestens zwei Stützpunkte – Entf ohne gewählten Griff löscht den ganzen Fluss.", true);
                    return;
                case EntwurfFaehre:
                    WerkstattMeldung?.Invoke("Eine Fährstelle hat immer zwei Anleger – Entf ohne gewählten Griff löscht sie ganz.", true);
                    return;
                case EntwurfFlaeche { KannAussparungen: true } f when griff.Vieleck >= 0 && f.Aussparungen[griff.Vieleck].Count <= 3:
                    Befehl(() => _entwurf.AussparungLoeschen(f, griff.Vieleck)); // eine Aussparung mit drei Ecken: weg mit ihr
                    break;
                case EntwurfFlaeche f when !Befehl(() => _entwurf.EckeLoeschen(f, griff.Index, griff.Vieleck)):
                    WerkstattMeldung?.Invoke("Eine Fläche braucht mindestens drei Ecken – Entf ohne gewählten Griff löscht die ganze Fläche.", true);
                    return;
            }
            _wsGriff = null;
            _wsGriffeNeu = true;
            return;
        }
        switch (teil)
        {
            case EntwurfGebaeude g: Befehl(() => _entwurf.Loeschen(g.Kennung)); break;
            case EntwurfWeg w: Befehl(() => _entwurf.WegLoeschen(w)); break;
            case EntwurfFluss or EntwurfSee: Befehl(() => _entwurf.GewaesserLoeschen(teil)); break;
            case EntwurfFaehre fa: Befehl(() => _entwurf.FaehreLoeschen(fa)); break;
            case EntwurfFlaeche f: Befehl(() => _entwurf.FlaecheLoeschen(f)); break;
            case EntwurfPflanze p: Befehl(() => _entwurf.PflanzeLoeschen(p)); break;
            case EntwurfMauer m: Befehl(() => _entwurf.MauerLoeschen(m)); break;
        }
    }

    /// <summary>
    /// Q/E: dreht, was gerade bewegt wird – den Umriss beim Setzen, das gezogene Stück, die Pflanze an der Maus oder (als Befehl) das gewählte
    /// Gebäude bzw. die gewählte Pflanze. In 15°-Schritten auf das Raster, mit Alt um 1°. <paramref name="richtung"/> +1 = gegen den
    /// Uhrzeigersinn (von oben), −1 = mit ihm.
    /// </summary>
    private void Drehen(int richtung, bool frei, bool wiederholt)
    {
        double Weiter(double grad) => frei ? Kartenraster.Drehung(grad + richtung) : Kartenraster.DrehungRunden(grad + richtung * Kartenraster.Drehschritt);
        if (_wsZiehen is { } z) z.Drehung = Weiter(z.Drehung);
        else if (Kartenwerkzeug is Werkstattwerkzeug.Baustein or Werkstattwerkzeug.Pflanze) WerkstattDrehung = Weiter(WerkstattDrehung);
        else if (wiederholt || _entwurf is null) return; // gehaltene Taste: kein Befehl je Wiederholung
        else if (WerkstattGewaehlt is EntwurfGebaeude g) Befehl(() => _entwurf.Drehen(g.Kennung, Weiter(g.Drehung)));
        else if (WerkstattGewaehlt is EntwurfPflanze p) Befehl(() => _entwurf.PflanzeDrehen(p, Weiter(p.Drehung)));
    }
}
