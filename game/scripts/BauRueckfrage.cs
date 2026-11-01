using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Rückfrage vor dem Bauen: Neubau auf dem gewählten Platz, Kauf eines Leerstands (mit Sanierung und Umzug)
/// oder Abriss eines Leerstands. Das Fenster rechnet selbst nichts: Alle Zahlen kommen aus
/// <see cref="Bauwesen.Vorschau"/> bzw. <see cref="Bauwesen.LeerstandVorschau"/> – dieselbe Rechnung, nach der der Kern bucht.
/// Erst „Bauen", „Kaufen" oder „Abreißen" schickt den Befehl (<see cref="WohnhausBauen"/>, <see cref="WerkstattBauen"/>,
/// <see cref="LeerstandKaufen"/>, <see cref="LeerstandAbreissen"/>).
///
/// Solange das Fenster offen ist, läuft die Zeit weiter (Vermögen, Uhrzeit des Fertigstellens) – darum wird die Vorschau
/// zweimal je Sekunde neu geholt und der Inhalt nur bei einer sichtbaren Änderung neu gebaut (wie bei <see cref="VerkaufAnzeige"/>).
/// </summary>
public partial class BauRueckfrage : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);
    private const int Breite = 480;

    private enum Art { Neubau, LeerstandKaufen, LeerstandAbreissen }

    private Simulation? _simulation;
    private Konto? _spieler;
    private Art _art;
    private BauBefehl? _befehl;
    private Konto? _leer;
    private VBoxContainer _inhalt = null!;
    private Button _bauKnopf = null!;
    private Button _zurueckKnopf = null!;
    private string _bauplan = "";
    private double _seitAktualisierung;

    /// <summary>Der Fahrweg der Karren vom gewählten Bauort zum nächsten Markt und die Zahl der eingezeichneten Plätze (Stadtansicht, nur bei einer Werkstatt).</summary>
    public Func<(Stadtansicht.FahrwegSchild Schild, int Plaetze)?>? Fahrweg { get; set; }

    /// <summary>Ergebnis des Befehls (Erfolg oder Ablehnung) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Der Befehl ging durch: Der Bau hat begonnen.</summary>
    public event Action<bool>? Beauftragt;

    /// <summary>Der Spieler hat „Zurück" gewählt oder Esc gedrückt – ohne etwas zu bauen.</summary>
    public event Action? Abgebrochen;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.10f, 0.08f, 1f),
            BorderColor = new Color(0.62f, 0.50f, 0.32f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);
        var spalte = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        spalte.AddThemeConstantOverride("separation", 12);
        rand.AddChild(spalte);
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_inhalt);

        // Die Knöpfe stehen außerhalb des Inhalts: Der wird neu gebaut, die Knöpfe bleiben unter der Maus.
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 16);
        spalte.AddChild(knoepfe);
        _bauKnopf = Hauptmenue.Knopf(knoepfe, "Bauen", Bestaetigen);
        _zurueckKnopf = Hauptmenue.Knopf(knoepfe, "Zurück", Zurueck);
        Rollbereich.Einhuellen(this);
        Fensterrahmen.Anmelden(this, "BauRueckfrage", schliessbar: false);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    /// <summary>Rückfrage zum Neubau an der gewählten Stelle.</summary>
    public void OeffnenNeubau(BauBefehl befehl)
    {
        _art = Art.Neubau;
        _befehl = befehl;
        _leer = null;
        Oeffnen();
    }

    /// <summary>Rückfrage zum Kauf des Leerstands (mit Sanierung und Umzug) oder zu seinem Abriss.</summary>
    public void OeffnenLeerstand(Konto leer, bool abreissen)
    {
        _art = abreissen ? Art.LeerstandAbreissen : Art.LeerstandKaufen;
        _befehl = null;
        _leer = leer;
        Oeffnen();
    }

    private void Oeffnen()
    {
        _bauplan = ""; // erzwingt den Neubau
        Aufbauen();
        Show();
    }

    /// <summary>Für welchen Leerstand das Fenster gerade offen ist (sonst null).</summary>
    public Konto? Leerstand => Visible ? _leer : null;

    public void Zurueck()
    {
        Hide();
        Abgebrochen?.Invoke();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _seitAktualisierung += delta;
        if (_seitAktualisierung < 0.5) return;
        _seitAktualisierung = 0;
        Aufbauen();
    }

    private void Bestaetigen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        Befehl? befehl = _art switch
        {
            Art.Neubau => _befehl,
            Art.LeerstandKaufen when _leer is not null => new LeerstandKaufen(_leer.Inhaber),
            Art.LeerstandAbreissen when _leer is not null => new LeerstandAbreissen(_leer.Inhaber),
            _ => null,
        };
        if (befehl is null) return;
        var neubau = _art == Art.Neubau;
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, ergebnis =>
        {
            Gemeldet?.Invoke(UiHilfe.MeldungText(ergebnis));
            if (!ergebnis.Erfolg) { Aufbauen(); return; }
            Hide();
            Beauftragt?.Invoke(neubau);
        });
    }

    /// <summary>Holt die Vorschau aus dem Kern und baut den Inhalt neu – aber nur, wenn sich etwas Sichtbares geändert hat.</summary>
    private void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        string plan;
        Action zeigen;
        string? verboten;
        string knopf;
        if (_art == Art.Neubau && _befehl is { } befehl)
        {
            var v = w.Bauen.Vorschau(_spieler, befehl);
            verboten = v.Verboten;
            knopf = "Bauen";
            plan = string.Join("|", "neu", v.Gebaeude, v.Betrieb, Zahlenformat.Ganz(v.Kosten), Zahlenformat.Ganz(v.Anrechnung), verboten, Zahlenformat.Ganz(_spieler.Vermoegen), FertigText(v.BauzeitMinuten),
                befehl is WerkstattBauen && Fahrweg?.Invoke() is var (f, _) ? $"{f.Hin}/{f.Zurueck}/{f.Markt}" : "");
            zeigen = () => NeubauZeigen(w, befehl, v, waehrung);
        }
        else if (_leer is { } leer)
        {
            var v = w.Bauen.LeerstandVorschau(_spieler, leer);
            var abriss = _art == Art.LeerstandAbreissen;
            verboten = abriss ? v.AbrissVerboten : v.KaufVerboten;
            knopf = abriss ? "Abreißen lassen" : "Kaufen";
            plan = string.Join("|", abriss ? "abriss" : "kauf", v.Haus, Zahlenformat.Ganz(v.KaufSumme), Zahlenformat.Ganz(v.AbrissSumme), Zahlenformat.Ganz(v.Anrechnung), verboten, FertigText(abriss ? v.AbrissMinuten : v.SanierungMinuten));
            zeigen = () => LeerstandZeigen(w, v, abriss, waehrung);
        }
        else { Hide(); return; }

        if (_bauKnopf.Text != knopf) _bauKnopf.Text = knopf;
        _bauKnopf.Disabled = verboten is not null || _spieler.IstAktiv != true;
        _bauKnopf.TooltipText = verboten ?? "";
        if (plan == _bauplan) return;
        _bauplan = plan;
        UiHilfe.KinderEntfernen(_inhalt);
        zeigen();
    }

    /// <summary>Wann der Bau fertig wäre, wenn er jetzt begänne: „gegen 14:30 Uhr" am selben Spieltag, sonst das Datum.</summary>
    private string FertigText(int dauerMinuten)
    {
        var zeit = new Spielzeit(_simulation!.Zeit.VergangeneMinuten + dauerMinuten);
        return zeit.Jahr == _simulation.Zeit.Jahr ? "gegen " + zeit.UhrzeitText + " Uhr" : zeit.DatumText;
    }

    // ── Inhalt ──────────────────────────────────────────────────────────────

    private void NeubauZeigen(Stadtwirtschaft w, BauBefehl befehl, Bauvorschau v, string waehrung)
    {
        var werkstatt = befehl is WerkstattBauen;
        Hauptmenue.Text(_inhalt, $"Neubau: {v.Gebaeude}", 22, Gold, zentriert: true);
        if (v.Betrieb is not null) Hauptmenue.Text(_inhalt, $"„{v.Betrieb}“", 16, Hell, zentriert: true);
        var f = w.Bauen.Flaeche(befehl); // ein schräger Wohnhausplatz: Maße und Eingang wie der Platz
        Hauptmenue.Text(_inhalt,
            $"{Zahlenformat.Ganz(f.Breite)} × {Zahlenformat.Ganz(f.Tiefe)} m{(f.Tiefe > 9 ? " (mit 2 m Vorplatz)" : " (der Wohnhausplatz)")}, Eingang nach {Himmelsrichtung(Grundriss.DrehungZu(f.Vorne.X, f.Vorne.Z))}.",
            13, Grau, umbruchBreite: Breite);

        var rechnung = Raster(_inhalt);
        Zeile(rechnung, werkstatt ? $"Preis (eine freie {v.Gebaeude} der Stufe 1)" : $"Hauswert ({v.Gebaeude})", Zahlenformat.Ganz(v.Preis));
        if (!werkstatt && v.Anrechnung > 0.5)
            Zeile(rechnung, "Euer altes Haus wird angerechnet", "−" + Zahlenformat.Ganz(v.Anrechnung));
        Zeile(rechnung, "= Kosten, fällig beim Baubeginn", $"{Zahlenformat.Ganz(v.Kosten)} {waehrung}", Gold, 17);
        if (werkstatt)
            Zeile(rechnung, "Dazu ein Jahr Unterhalt, bar vorhanden", Zahlenformat.Ganz(v.Unterhalt));
        Zeile(rechnung, "Euer Vermögen", $"{Zahlenformat.Ganz(_spieler!.Vermoegen)} {waehrung}", Hell, 14);
        Zeile(rechnung, "Bauzeit", $"{BauAnzeige.BauzeitText(v.BauzeitMinuten)} – fertig {FertigText(v.BauzeitMinuten)}", Hell, 14);
        if (werkstatt && Fahrweg?.Invoke() is var (fahrweg, plaetze))
        {
            var (links, rechts) = Zeile(rechnung, "Fahrt zum Markt", FahrwegAnzeige.Kurz(fahrweg), Schriften.LesbareFarbe(Fahrzeitstufen.Stufe(fahrweg.Hin)), 14);
            links.TooltipText = rechts.TooltipText = FahrwegAnzeige.Tipp(fahrweg, plaetze);
            links.MouseFilter = rechts.MouseFilter = MouseFilterEnum.Stop;
        }

        Hauptmenue.Text(_inhalt, werkstatt
            ? "Während des Baus fallen keine Löhne und kein Unterhalt an. Danach öffnet die Werkstatt; Personal stellt ihr dann ein wie überall."
            : $"Bis zur Fertigstellung wohnt ihr im alten Haus. Danach zieht ihr um, das alte Haus wird abgerissen und zum Bauplatz. Unterhalt des neuen Hauses: {Zahlenformat.Ganz(v.Unterhalt)} {waehrung} im Jahr.",
            12, Grau, umbruchBreite: Breite);
        if (v.Verboten is { } grund)
            Hauptmenue.Text(_inhalt, $"✗ {grund}", 14, Rot, umbruchBreite: Breite);
    }

    private void LeerstandZeigen(Stadtwirtschaft w, Leerstandsvorschau v, bool abriss, string waehrung)
    {
        var d = w.Bauen.Daten?.Leerstand;
        Hauptmenue.Text(_inhalt, abriss ? $"Leerstand abreißen: {v.Gebaeude}" : $"Leerstand kaufen: {v.Gebaeude}", 22, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt,
            $"Das {v.Gebaeude} von {v.Haus} steht leer. Voller Hauswert {Zahlenformat.Ganz(v.Hauswert)} {waehrung} (Summe der Ausbaupreise bis zu dieser Stufe).",
            13, Grau, umbruchBreite: Breite);

        var rechnung = Raster(_inhalt);
        Zeile(rechnung, $"Kauf ({Zahlenformat.Ganz((d?.Kauf ?? 0.5) * 100)} % des Hauswerts)", Zahlenformat.Ganz(v.Kaufpreis));
        if (abriss)
        {
            Zeile(rechnung, "Abrisskosten", Zahlenformat.Ganz(v.Abriss));
            Zeile(rechnung, "= Summe, fällig jetzt", $"{Zahlenformat.Ganz(v.AbrissSumme)} {waehrung}", Gold, 17);
        }
        else
        {
            Zeile(rechnung, $"Sanierung ({Zahlenformat.Ganz((d?.Sanierung ?? 0.2) * 100)} %)", Zahlenformat.Ganz(v.Sanierung));
            if (v.Anrechnung > 0.5) Zeile(rechnung, "Euer altes Haus wird angerechnet", "−" + Zahlenformat.Ganz(v.Anrechnung));
            Zeile(rechnung, "= Summe, fällig jetzt", $"{Zahlenformat.Ganz(v.KaufSumme)} {waehrung}", Gold, 17);
        }
        Zeile(rechnung, "Euer Vermögen", $"{Zahlenformat.Ganz(_spieler!.Vermoegen)} {waehrung}", Hell, 14);
        var minuten = abriss ? v.AbrissMinuten : v.SanierungMinuten;
        Zeile(rechnung, abriss ? "Abriss dauert" : "Sanierung dauert", $"{BauAnzeige.BauzeitText(minuten)} – fertig {FertigText(minuten)}", Hell, 14);

        Hauptmenue.Text(_inhalt, abriss
            ? "Danach ist hier ein freier Bauplatz, auf dem jeder bauen darf."
            : "Nach der Sanierung zieht ihr ein. Euer altes Haus wird abgerissen und zum Bauplatz; die Anrechnung zahlt keine Auszahlung aus.",
            12, Grau, umbruchBreite: Breite);
        if ((abriss ? v.AbrissVerboten : v.KaufVerboten) is { } grund)
            Hauptmenue.Text(_inhalt, $"✗ {grund}", 14, Rot, umbruchBreite: Breite);
    }

    private static string Himmelsrichtung(int drehung) => drehung switch { 0 => "Süden", 1 => "Osten", 2 => "Norden", _ => "Westen" };

    // ── Bausteine ───────────────────────────────────────────────────────────

    private static GridContainer Raster(Container eltern)
    {
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 24);
        raster.AddThemeConstantOverride("v_separation", 4);
        eltern.AddChild(raster);
        return raster;
    }

    /// <summary>Eine Zeile der Rechnung: Text links, Betrag rechtsbündig.</summary>
    private static (Label Links, Label Rechts) Zeile(GridContainer raster, string text, string betrag, Color? farbe = null, int groesse = 14)
    {
        var links = Hauptmenue.Text(raster, text, groesse, Hell);
        links.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var rechts = Hauptmenue.Text(raster, betrag, groesse, farbe ?? Hell);
        rechts.HorizontalAlignment = HorizontalAlignment.Right;
        return (links, rechts);
    }
}
