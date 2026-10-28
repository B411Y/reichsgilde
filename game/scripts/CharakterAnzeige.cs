using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Charakterfenster (PR #227, Vorbild: das Charakterfenster in Crusader Kings 3, nur die Mechanik): großes Porträt, Name, Volk, Alter, Stand und Glaube,
/// die Talente als Sterne (Anlage golden, Zugwirkung grün dazu oder rot abgezogen), die Charakterzüge als runde Medaillons in drei Gruppen und
/// die Familie als kleine Porträts. Ein Klick auf ein Porträt irgendwo im Spiel öffnet es (<see cref="PortraetAnzeige.PersonOeffnen"/>); ein Klick auf ein
/// Familienmitglied zeigt dessen Fenster, „Zurück“ geht zur vorigen Person. Das Fenster liest den Kern nur.
///
/// Ratsherren sind keine Personen des Familienbuchs: Ihr Fenster zeigt Volk, Amt, Glaube und Züge, dazu die Bestechlichkeit – ohne Talente und Familie.
/// Vorarbeiter haben keine Personen-Züge, ihre Porträts öffnen nichts.
/// </summary>
public partial class CharakterAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private const int Breite = 780;

    private Simulation? _simulation;
    private VBoxContainer _inhalt = null!;
    private Fensterrahmen? _rahmen;
    private readonly List<object> _verlauf = new(); // Person oder Ratsherr, die letzte ist die gezeigte

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Breite, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.10f, 1f),
            BorderColor = Gold * new Color(1, 1, 1, 0.8f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 8);
        rand.AddChild(_inhalt);
        Rollbereich.Einhuellen(this);
        _rahmen = Fensterrahmen.Anmelden(this, "Charakter", festhaltbar: true);
        _rahmen.PlatzSetzen((groesse, sichtbar) => new Vector2(sichtbar.X - groesse.X - 16, Rollbereich.KopfHoehe));
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
        _aktivMit = null;
        _verlauf.Clear();
    }

    /// <summary>Zeigt das Fenster dieser Person.</summary>
    public void Oeffnen(Person person) => Zeigen(person);

    /// <summary>Zeigt das Fenster dieses Ratsherrn.</summary>
    public void Oeffnen(Ratsherr ratsherr) => Zeigen(ratsherr);

    private void Zeigen(object ziel)
    {
        if (_simulation?.Wirtschaft is null) return;
        if (_verlauf.Count == 0 || !_verlauf[^1].Equals(ziel)) { _verlauf.Add(ziel); _aktivMit = null; _meldung = ""; }
        if (_verlauf.Count > 30) _verlauf.RemoveAt(0);
        Aufbauen();
        Show();
    }

    /// <summary>Baut den Inhalt neu auf – beim Öffnen und zum Jahreswechsel (Alter, neue Züge).</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _verlauf.Count == 0) return;
        UiHilfe.KinderEntfernen(_inhalt);
        if (_aktivMit is not null && !(_verlauf[^1] is ICharakter aktuell && aktuell.Kennung == _aktivMit.Kennung)) _aktivMit = null;
        if (_aktivMit is not null) AktivitaetenBauen(w, _aktivMit);
        else
            switch (_verlauf[^1])
            {
                case Person p: PersonBauen(w, p); break;
                case Ratsherr r: RatsherrBauen(w, r); break;
            }
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        _inhalt.AddChild(knoepfe);
        if (_aktivMit is not null)
            Hauptmenue.Knopf(knoepfe, "‹ Zurück", () => { _aktivMit = null; Aufbauen(); });
        else
        {
            if (_verlauf[^1] is ICharakter ziel) AktivitaetenKnopf(knoepfe, w, ziel);
            if (_verlauf.Count > 1)
                Hauptmenue.Knopf(knoepfe, "‹ Zurück", () => { _verlauf.RemoveAt(_verlauf.Count - 1); Aufbauen(); });
        }
        Hauptmenue.Knopf(knoepfe, "Schließen", () => { _verlauf.Clear(); _aktivMit = null; Hide(); });
    }

    // ── Person ──────────────────────────────────────────────────────────────

    private void PersonBauen(Stadtwirtschaft w, Person p)
    {
        var buch = w.Familien;
        var familie = buch.FamilieVon(p);
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 16);
        _inhalt.AddChild(kopf);
        var bild = PortraetAnzeige.Neu(PortraetAnzeige.Gross);
        bild.Zeigen(buch, p);
        kopf.AddChild(bild);

        var daten = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        daten.AddThemeConstantOverride("separation", 4);
        kopf.AddChild(daten);
        Hauptmenue.Text(daten, p.NameMitHerkunft, 24, Gold, umbruchBreite: Breite - PortraetAnzeige.Gross - 70);
        var alter = p.Alter(buch.Jahr);
        Hauptmenue.Text(daten, p.Lebt ? $"{p.Volk.Einzahl()} · {alter} Jahre" : $"{p.Volk.Einzahl()} · † mit {alter} Jahren", 15, Grau);

        var rolle = familie is null ? "Heiratskandidat" : familie.Oberhaupt == p ? p.OberhauptTitel
            : familie.Ehepartner == p ? p.Ehepartnertitel : p.SohnOderTochter;
        if (familie is not null) rolle += $" im Haus {familie.Nachname}";
        Hauptmenue.Text(daten, rolle, 15, null, umbruchBreite: Breite - PortraetAnzeige.Gross - 70);
        var stand = familie is null ? null : w.StandVon(familie.Konto).Name;
        if (stand is not null) Hauptmenue.Text(daten, $"Stand: {stand}", 14, Grau);
        if (familie is not null && familie.Oberhaupt == p && w.Rat.AmtVon(familie.Konto) is { } amt)
            Hauptmenue.Text(daten, $"Amt: {amt.Name}", 14, Grau);
        if (w.Glaube.IstAktiv) Hauptmenue.Text(daten, $"Glaube: {p.Glaube.Name()}", 14, Grau);
        if (p.Krankheit is { } k) Hauptmenue.Text(daten, $"Krank: {k.Name}", 14, Rot);
        MeinungBauen(daten, w, p);

        daten.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        Hauptmenue.Text(daten, "Talente", 17, Gold);
        foreach (var t in TalentTexte.Alle) TalentZeile(daten, p, t);

        _inhalt.AddChild(new HSeparator());
        ZuegeBauen(w, p.Zuege, buch.IstVolljaehrig(p) || !p.Lebt, p.Lebt && !buch.IstVolljaehrig(p));

        var v = buch.Verwandte(p);
        if (v.Vater is null && v.Mutter is null && v.Ehepartner is null && v.Kinder.Count == 0) return;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Familie", 17, Gold);
        var fluss = new HFlowContainer();
        fluss.AddThemeConstantOverride("h_separation", 12);
        fluss.AddThemeConstantOverride("v_separation", 8);
        _inhalt.AddChild(fluss);
        if (v.Vater is { } vater) Verwandter(fluss, buch, vater, vater.Geschlecht == Geschlecht.Frau ? "Mutter" : "Vater");
        if (v.Mutter is { } mutter) Verwandter(fluss, buch, mutter, "Mutter");
        if (v.Ehepartner is { } partner) Verwandter(fluss, buch, partner, partner.Ehepartnertitel);
        foreach (var kind in v.Kinder) Verwandter(fluss, buch, kind, kind.SohnOderTochter);
    }

    private static void Verwandter(Container eltern, Familienbuch buch, Person p, string titel)
    {
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 2);
        var bild = PortraetAnzeige.Neu(PortraetAnzeige.Klein);
        bild.Zeigen(buch, p);
        bild.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        spalte.AddChild(bild);
        var name = Hauptmenue.Text(spalte, (p.Lebt ? "" : "† ") + p.Vorname, 13, p.Lebt ? Gold : Grau, zentriert: true);
        name.CustomMinimumSize = new Vector2(PortraetAnzeige.Klein + 16, 0);
        var rolle = Hauptmenue.Text(spalte, titel, 11, Grau, zentriert: true);
        rolle.CustomMinimumSize = new Vector2(PortraetAnzeige.Klein + 16, 0);
        eltern.AddChild(spalte);
    }

    private void TalentZeile(Container eltern, Person p, Talent t)
    {
        var anlage = p.Anlage(t);
        var jetzt = p.Talent(t);
        var zeile = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        zeile.AddThemeConstantOverride("separation", 10);
        eltern.AddChild(zeile);
        var name = Hauptmenue.Text(zeile, t.Name(), 14);
        name.CustomMinimumSize = new Vector2(150, 0);
        name.MouseFilter = MouseFilterEnum.Ignore;
        zeile.AddChild(new SterneFeld(anlage, jetzt));
        var diff = jetzt - anlage;
        var zahl = Hauptmenue.Text(zeile, TalentTexte.Sterne(jetzt) + (Math.Abs(diff) < 0.01 ? "" : $" ({(diff > 0 ? "+" : "−")}{TalentTexte.Sterne(Math.Abs(diff))})"), 14,
            diff > 0.01 ? Gruen : diff < -0.01 ? Rot : null);
        zahl.MouseFilter = MouseFilterEnum.Ignore;

        var tipp = $"{t.Name()}: {TalentTexte.Sterne(jetzt)} von 6 Sternen\nAnlage {TalentTexte.Sterne(anlage)}";
        foreach (var z in p.Zuege.Where(z => Math.Abs(z.Talent(t)) > 1e-9))
            tipp += $"\n{z.Name} {(z.Talent(t) > 0 ? "+" : "−")}{TalentTexte.Sterne(Math.Abs(z.Talent(t)))}";
        if (p.Krankheit is { } k && k.Abzug(t) > 0) tipp += $"\n{k.Name} −{TalentTexte.Sterne(k.Abzug(t))}";
        tipp += "\nMehr zu [[talente|Talenten]] und [[charakterzuege|Charakterzügen]].";
        zeile.TooltipText = tipp;
    }

    // ── Ratsherr ────────────────────────────────────────────────────────────

    private void RatsherrBauen(Stadtwirtschaft w, Ratsherr r)
    {
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 16);
        _inhalt.AddChild(kopf);
        var bild = PortraetAnzeige.Neu(PortraetAnzeige.Gross);
        bild.ZeigenRatsherr(r);
        kopf.AddChild(bild);
        var daten = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        daten.AddThemeConstantOverride("separation", 4);
        kopf.AddChild(daten);
        Hauptmenue.Text(daten, r.Name, 24, Gold, umbruchBreite: Breite - PortraetAnzeige.Gross - 70);
        Hauptmenue.Text(daten, r.Daten.Volk.Einzahl(), 15, Grau);
        var amt = w.Rat.Aemter.FirstOrDefault(a => a.Ratsherr == r);
        Hauptmenue.Text(daten, amt is null ? "Ratsherr der Stadt" : $"Amt: {amt.Name}", 15);
        if (w.Glaube.IstAktiv) Hauptmenue.Text(daten, $"Glaube: {w.Glaube.GlaubeVon(r).Name()}", 14, Grau);
        MeinungBauen(daten, w, r);
        if (w.Familien.HatZuege)
        {
            var faktor = r.Bestechlichkeit;
            var text = Math.Abs(faktor - 1) < 0.01 ? "Bestechlichkeit: durchschnittlich"
                : $"Bestechlichkeit: Geschenke wirken {(faktor > 1 ? "+" : "−")}{Zahlenformat.Ganz(Math.Abs(faktor - 1) * 100)} %";
            var zeile = Hauptmenue.Text(daten, text, 14, faktor > 1.01 ? Rot : faktor < 0.99 ? Gruen : Grau, umbruchBreite: Breite - PortraetAnzeige.Gross - 70);
            zeile.MouseFilter = MouseFilterEnum.Stop;
            zeile.TooltipText = "Je geschenktem Gulden gewinnt Ihr bei diesem Ratsherrn mehr oder weniger Gunst, je nach seinen [[charakterzuege|Charakterzügen]].";
        }
        _inhalt.AddChild(new HSeparator());
        ZuegeBauen(w, r.Zuege, true, false);
    }

    // ── Züge ────────────────────────────────────────────────────────────────

    private void ZuegeBauen(Stadtwirtschaft w, IReadOnlyList<Charakterzug> zuege, bool erwachsen, bool kind)
    {
        Hauptmenue.Text(_inhalt, "Charakterzüge", 17, Gold);
        if (!w.Familien.HatZuege)
        {
            Hauptmenue.Text(_inhalt, "In diesem Spielstand gibt es keine Charakterzüge.", 14, Grau);
            return;
        }
        if (kind)
            Hauptmenue.Text(_inhalt, "Die Persönlichkeit entwickelt sich erst mit der Volljährigkeit.", 13, Grau, umbruchBreite: Breite - 50);
        foreach (var art in new[] { Zugart.Persoenlichkeit, Zugart.Angeboren, Zugart.Erworben })
        {
            var gruppe = zuege.Where(z => z.Art == art).ToList();
            if (gruppe.Count == 0)
            {
                if (art == Zugart.Persoenlichkeit && erwachsen && !kind)
                    Hauptmenue.Text(_inhalt, "Keine Züge der Persönlichkeit.", 13, Grau);
                continue;
            }
            Hauptmenue.Text(_inhalt, art switch { Zugart.Persoenlichkeit => "Persönlichkeit", Zugart.Angeboren => "Angeboren", _ => "Erworben" }, 13, Grau);
            var reihe = new HFlowContainer();
            reihe.AddThemeConstantOverride("h_separation", 10);
            _inhalt.AddChild(reihe);
            foreach (var z in gruppe) reihe.AddChild(new ZugIcon(z, ZugTipp(w, z)));
        }
        if (zuege.Count == 0 && !kind) Hauptmenue.Text(_inhalt, "Keine besonderen Züge.", 13, Grau);
    }

    private static string ZugTipp(Stadtwirtschaft w, Charakterzug z)
    {
        var tipp = $"{z.Name} ({z.Artname})";
        if (z.Beschreibung != "") tipp += "\n" + z.Beschreibung;
        if (z.Wirkungstext != "") tipp += "\nWirkung: " + z.Wirkungstext;
        if (z.Gegenteil is { } g) tipp += $"\nGegenteil: {w.Familien.Zug(g)?.Name ?? g}";
        return tipp + "\nMehr zu [[charakterzuege|Charakterzügen]].";
    }

    /// <summary>
    /// Ein rundes Medaillon: das gemalte Bild mit geschmiedetem Metallring nach Art des Zugs (Persönlichkeit Kupfer, angeboren Gold,
    /// erworben Eisen; Hammerschlag, Kerben je Zug an anderer Stelle – schon im Bild). Fehlt das Bild, zeichnet das Fenster einen
    /// schlichten, abgeschrägten Ring in derselben Farbe mit dem Anfangsbuchstaben.
    /// </summary>
    private sealed partial class ZugIcon : Control
    {
        private const int Seite = 64;
        private const float Ring = 4f;
        private const int Stuecke = 72;
        private static readonly Dictionary<string, Texture2D?> Bilder = new();
        private static readonly Vector2 Licht = new Vector2(-1, -1).Normalized();
        private readonly Charakterzug _zug;
        private readonly Texture2D? _bild;

        public static Color Rahmen(Zugart art) => art switch
        {
            Zugart.Persoenlichkeit => new Color("a0583a"),
            Zugart.Angeboren => new Color("e3b33c"),
            _ => new Color("6e7378"),
        };

        public ZugIcon(Charakterzug zug, string tipp)
        {
            _zug = zug;
            CustomMinimumSize = new Vector2(Seite, Seite);
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.Help;
            TooltipText = tipp;
            if (!Bilder.TryGetValue(zug.Kennung, out _bild))
            {
                var pfad = $"res://assets/oberflaeche/zuege/{zug.Kennung.ToLowerInvariant()}.png";
                Bilder[zug.Kennung] = _bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
            }
        }

        public override void _Draw()
        {
            // Die Bilder bringen ihren geschmiedeten Ring schon mit
            if (_bild is not null)
            {
                DrawTextureRect(_bild, new Rect2(0, 0, Seite, Seite), false);
                return;
            }
            var mitte = new Vector2(Seite, Seite) / 2;
            var aussen = Seite / 2f;
            DrawCircle(mitte, aussen - Ring / 2, new Color(0.16f, 0.14f, 0.12f));
            var font = ThemeDB.FallbackFont;
            var buchstabe = _zug.Name.Length > 0 ? _zug.Name[..1].ToUpperInvariant() : "?";
            var groesse = font.GetStringSize(buchstabe, HorizontalAlignment.Left, -1, 24);
            DrawString(font, new Vector2(mitte.X - groesse.X / 2, mitte.Y + 8), buchstabe, HorizontalAlignment.Left, -1, 24, new Color(0.92f, 0.88f, 0.80f));
            // Ring in Stücken (Warcraft-3-Abschrägung): je nach Richtung zum Licht heller oder dunkler
            var farbe = Rahmen(_zug.Art);
            for (var i = 0; i < Stuecke; i++)
            {
                var von = Mathf.Tau * i / Stuecke;
                var bis = Mathf.Tau * (i + 1) / Stuecke + 0.01f;
                var richtung = Vector2.FromAngle((von + bis) / 2);
                var hell = richtung.Dot(Licht);
                var c = hell > 0 ? farbe.Lerp(Colors.White, 0.5f * hell) : farbe.Lerp(Colors.Black, -0.45f * hell);
                DrawArc(mitte, aussen - Ring / 2, von, bis, 4, c, Ring, true);
            }
            // feine dunkle Kanten außen und innen
            DrawArc(mitte, aussen - 0.5f, 0, Mathf.Tau, Stuecke, new Color(0, 0, 0, 0.75f), 1f, true);
            DrawArc(mitte, aussen - Ring - 0.5f, 0, Mathf.Tau, Stuecke, new Color(0, 0, 0, 0.6f), 1f, true);
        }
    }

    /// <summary>Sechs Sterne: Anlage golden, Zugwirkung grün dazu, abgezogene Sterne rot; leere Sterne nur als Umriss.</summary>
    private sealed partial class SterneFeld : Control
    {
        private const int Stern = 22, Anzahl = 6;
        private readonly double _anlage, _jetzt;

        public SterneFeld(double anlage, double jetzt)
        {
            _anlage = anlage;
            _jetzt = jetzt;
            CustomMinimumSize = new Vector2(Stern * Anzahl, Stern);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        private static Vector2[] Umriss(Vector2 mitte, float r)
        {
            var punkte = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var winkel = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
                var radius = i % 2 == 0 ? r : r * 0.42f;
                punkte[i] = mitte + new Vector2(Mathf.Cos(winkel), Mathf.Sin(winkel)) * radius;
            }
            return punkte;
        }

        public override void _Draw()
        {
            var behalten = Math.Min(_anlage, _jetzt);
            (double Von, double Bis, Color Farbe)[] streifen =
            {
                (0, behalten, new Color(0.95f, 0.78f, 0.30f)),
                (_anlage, _jetzt, new Color(0.45f, 0.85f, 0.40f)),
                (_jetzt, _anlage, new Color(0.90f, 0.35f, 0.30f)),
            };
            for (var i = 0; i < Anzahl; i++)
            {
                var mitte = new Vector2(i * Stern + Stern / 2f, Stern / 2f);
                var form = Umriss(mitte, Stern / 2f - 1);
                DrawColoredPolygon(form, new Color(0.22f, 0.20f, 0.18f));
                foreach (var (von, bis, farbe) in streifen)
                {
                    var lo = (float)Math.Clamp(von - i, 0, 1);
                    var hi = (float)Math.Clamp(bis - i, 0, 1);
                    if (hi <= lo) continue;
                    var ausschnitt = new[]
                    {
                        new Vector2(i * Stern + lo * Stern, 0), new Vector2(i * Stern + hi * Stern, 0),
                        new Vector2(i * Stern + hi * Stern, Stern), new Vector2(i * Stern + lo * Stern, Stern),
                    };
                    foreach (var teil in Geometry2D.IntersectPolygons(form, ausschnitt))
                        if (teil.Length >= 3) DrawColoredPolygon(teil, farbe);
                }
                var linie = new Vector2[11];
                Array.Copy(form, linie, 10);
                linie[10] = form[0];
                DrawPolyline(linie, new Color(0.05f, 0.04f, 0.03f), 1.5f, true);
            }
        }
    }
}
