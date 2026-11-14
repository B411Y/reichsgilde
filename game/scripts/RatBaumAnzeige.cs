using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Ratsbaum (PR „Ratsbaum“): die Ämter der Stadt als Baum nach <c>politik.aemter[].vorgesetzt</c> – oben die Wurzel (der Bürgermeister), darunter seine Untergebenen.
/// <list type="bullet">
/// <item><b>Layout</b> (einfach und lehrreich): die Blätter bekommen von links nach rechts je eine Spalte, jedes Amt steht mittig über seinen Untergebenen, die Zeile ist die Tiefe im Baum.
/// Die Reihenfolge ist die der Datei (<see cref="Stadtrat.Wurzelaemter"/>, <see cref="Stadtrat.Untergebene"/>) – keine Reihenfolge aus einem Wörterbuch.</item>
/// <item><b>Knoten</b> sind Knöpfe (Maus und Tastatur: Tab und Pfeiltasten springen von Knoten zu Knoten, Eingabe wählt): Porträt, Amt, Name mit Wappen, Einkommen und Meinung.
/// Euer Amt ist golden, ein freies Amt ein Schatten mit „?“, erreichbare Ämter (eine Stufe höher) haben einen helleren Rand.</item>
/// <item><b>Wähler</b> leuchten beim Darüberfahren auf (F4): Die Wahlregel folgt der Stufe, nicht den Linien – so lernt man sie.</item>
/// <item><b>Linien</b> zeichnet <c>_Draw</c>: erst ein dunkler Umriss, darüber der helle Strich; die Linie von eurem Amt zum Vorgesetzten ist golden.</item>
/// </list>
/// Farben, Größen und Ränder stehen nur im Thema <c>rat_thema.tres</c> (Varianten „RatKnoten…“, Maße „knoten_…“, „linie_…“).
/// Die Anzeige ändert nichts, sie meldet nur den Klick (<see cref="AmtGewaehlt"/>).
/// </summary>
public partial class RatBaumAnzeige : Control
{
    /// <summary>Ein Knoten wurde gewählt (Klick, Eingabetaste).</summary>
    public event Action<Amt>? AmtGewaehlt;

    private readonly Dictionary<string, Button> _knoten = new();
    private readonly Dictionary<string, string> _grundVariante = new();
    private readonly List<(Vector2 Von, Vector2 Nach, bool Gold)> _linien = new();
    private readonly HashSet<string> _leuchtend = new();

    /// <summary>Baut den Baum neu auf (beim Öffnen und nach jedem Befehl). <paramref name="gewaehlt"/> ist der Name des gewählten Amtes (oder null).</summary>
    public void Aufbauen(Stadtwirtschaft w, Konto spieler, string? gewaehlt)
    {
        UiHilfe.KinderEntfernen(this);
        _knoten.Clear();
        _grundVariante.Clear();
        _linien.Clear();
        _leuchtend.Clear();
        var rat = w.Rat;

        // 1. Layout: Blätter nummerieren, Eltern mittig darüber, Zeile = Tiefe.
        var stelle = new Dictionary<string, (double X, int Tiefe)>();
        var reihenfolge = new List<Amt>();
        var blatt = 0;
        double Platzieren(Amt amt, int tiefe)
        {
            reihenfolge.Add(amt);
            var kinder = rat.Untergebene(amt).Where(k => !stelle.ContainsKey(k.Name) && k != amt).ToList();
            stelle[amt.Name] = (0, tiefe); // schon vermerkt: schützt vor einem Kreis in den Daten
            double x;
            if (kinder.Count == 0) x = blatt++;
            else
            {
                var xs = kinder.Select(k => Platzieren(k, tiefe + 1)).ToList();
                x = (xs[0] + xs[^1]) / 2;
            }
            stelle[amt.Name] = (x, tiefe);
            return x;
        }
        foreach (var wurzel in rat.Wurzelaemter()) Platzieren(wurzel, 0);
        foreach (var rest in rat.Aemter.Where(a => !stelle.ContainsKey(a.Name))) Platzieren(rest, 0); // ein Amt mit unbekanntem Vorgesetzten wird eine eigene Wurzel

        var breite = RatStil.Mass("knoten_breite");
        var hoehe = RatStil.Mass("knoten_hoehe");
        var schrittX = breite + RatStil.Mass("knoten_abstand_x");
        var schrittY = hoehe + RatStil.Mass("knoten_abstand_y");
        var tiefste = stelle.Values.Max(s => s.Tiefe);
        CustomMinimumSize = new Vector2((Math.Max(blatt, 1) - 1) * schrittX + breite, tiefste * schrittY + hoehe);

        var erreichbar = rat.WaehlbareAemter(spieler).Select(a => a.Name).ToHashSet();
        var eigenes = rat.AmtVon(spieler);
        foreach (var amt in reihenfolge)
        {
            var (x, tiefe) = stelle[amt.Name];
            var ort = new Vector2((float)(x * schrittX), tiefe * schrittY);
            var knoten = KnotenBauen(w, spieler, amt, erreichbar.Contains(amt.Name), breite, hoehe);
            knoten.Position = ort;
            knoten.ButtonPressed = amt.Name == gewaehlt;
            AddChild(knoten);
            _knoten[amt.Name] = knoten;
            if (amt.Daten.Vorgesetzt is { } oben && stelle.TryGetValue(oben, out var obenStelle))
                _linien.Add((new Vector2((float)(obenStelle.X * schrittX) + breite / 2f, obenStelle.Tiefe * schrittY + hoehe),
                             new Vector2(ort.X + breite / 2f, ort.Y), eigenes == amt));
        }
        QueueRedraw();
    }

    /// <summary>Markiert das gewählte Amt (ohne Neuaufbau – die Tastaturauswahl bleibt am Knoten).</summary>
    public void Waehlen(string? amtName)
    {
        foreach (var (name, knopf) in _knoten) knopf.SetPressedNoSignal(name == amtName);
    }

    /// <summary>Setzt den Fokus der Tastatur auf den Knoten dieses Amtes (nach einem Neuaufbau).</summary>
    public void FokusAuf(string amtName)
    {
        if (_knoten.TryGetValue(amtName, out var knopf)) knopf.GrabFocus();
    }

    /// <summary>Der Name des Amtes, dessen Knoten gerade den Fokus hat (sonst null).</summary>
    public string? FokusAmt()
    {
        var fokus = GetViewport()?.GuiGetFocusOwner();
        return _knoten.FirstOrDefault(k => k.Value == fokus).Key;
    }

    public override void _Draw()
    {
        var tinte = RatStil.Farbe("linie_tinte");
        var hell = RatStil.Farbe("linie");
        var gold = RatStil.Farbe("linie_gold");
        var dick = RatStil.Mass("linie_tinte_breite");
        var duenn = RatStil.Mass("linie_breite");
        // Erst alle Umrisse, dann die hellen Striche darüber: Wo Linien sich treffen, bleibt der Umriss nicht stehen.
        foreach (var l in _linien) DrawPolyline(Weg(l), tinte, dick);
        foreach (var l in _linien.Where(l => !l.Gold)) DrawPolyline(Weg(l), hell, duenn);
        foreach (var l in _linien.Where(l => l.Gold)) DrawPolyline(Weg(l), gold, duenn);
    }

    /// <summary>Von der Unterkante des Vorgesetzten gerade hinunter, quer, dann hinunter in die Oberkante des Untergebenen.</summary>
    private static Vector2[] Weg((Vector2 Von, Vector2 Nach, bool Gold) l)
    {
        var mitte = (l.Von.Y + l.Nach.Y) / 2;
        return new[] { l.Von, new Vector2(l.Von.X, mitte), new Vector2(l.Nach.X, mitte), l.Nach };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ein Knoten
    // ═════════════════════════════════════════════════════════════════════════

    private Button KnotenBauen(Stadtwirtschaft w, Konto spieler, Amt amt, bool erreichbar, int breite, int hoehe)
    {
        var rat = w.Rat;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var variante = amt.Konto == spieler ? "RatKnotenEigen" : !amt.IstBesetzt ? "RatKnotenFrei" : erreichbar ? "RatKnotenZiel" : "RatKnoten";
        var knopf = new Button
        {
            ThemeTypeVariation = variante, ToggleMode = true, CustomMinimumSize = new Vector2(breite, hoehe), Size = new Vector2(breite, hoehe),
            FocusMode = FocusModeEnum.All, Name = "Knoten_" + amt.Name, TooltipText = Tipp(w, amt),
        };
        _grundVariante[amt.Name] = variante;
        knopf.Pressed += () => AmtGewaehlt?.Invoke(amt);
        knopf.MouseEntered += () => Hervorheben(rat, amt);
        knopf.FocusEntered += () => Hervorheben(rat, amt);
        knopf.MouseExited += Zuruecknehmen;
        knopf.FocusExited += Zuruecknehmen;

        var rand = RatStil.Container<MarginContainer>(knopf, "RatKnotenRand");
        rand.SetAnchorsPreset(LayoutPreset.FullRect);
        rand.MouseFilter = MouseFilterEnum.Ignore;
        var zeile = RatStil.Container<HBoxContainer>(rand, "RatZeile");
        zeile.MouseFilter = MouseFilterEnum.Ignore;

        var portraetGroesse = RatStil.Mass("knoten_portraet");
        var bild = PortraetAnzeige.Neu(portraetGroesse);
        if (bild.ZeigenAmt(w, amt)) zeile.AddChild(bild);
        else
        {
            // Freier Sitz: ein Schatten mit „?“
            var schatten = RatStil.Container<PanelContainer>(zeile, "RatKnotenSchatten");
            schatten.CustomMinimumSize = new Vector2(portraetGroesse, portraetGroesse);
            schatten.MouseFilter = MouseFilterEnum.Ignore;
            var frage = RatStil.Text(schatten, "?", "RatKnotenFrage", zentriert: true);
            frage.VerticalAlignment = VerticalAlignment.Center;
            frage.MouseFilter = MouseFilterEnum.Ignore;
        }

        var spalte = RatStil.Container<VBoxContainer>(zeile, "RatKnotenSpalte");
        spalte.MouseFilter = MouseFilterEnum.Ignore;
        spalte.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Kurz(spalte, amt.Name, "RatKnotenAmt");
        var namensZeile = RatStil.Container<HBoxContainer>(spalte, "RatKnotenNamenszeile");
        namensZeile.MouseFilter = MouseFilterEnum.Ignore;
        if (amt.Konto is { } haus)
        {
            var wappen = WappenBild.Neu(haus.Wappen, RatStil.Mass("knoten_wappen"));
            wappen.MouseFilter = MouseFilterEnum.Ignore;
            namensZeile.AddChild(wappen);
        }
        Kurz(namensZeile, amt.IstBesetzt ? amt.Inhaber : "unbesetzt", amt.IstBesetzt ? "RatKnotenName" : "RatKnotenInfo");
        Kurz(spalte, $"{Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung}/Jahr · Wahl am Jahresende", "RatKnotenInfo");
        if (amt.Ratsherr is { } ratsherr)
        {
            var meinung = rat.Meinung(ratsherr, spieler);
            Kurz(spalte, $"Meinung {Zahlenformat.MitVorzeichen(meinung)}", meinung >= 5 ? "RatKnotenGut" : meinung <= -5 ? "RatKnotenSchlecht" : "RatKnotenInfo");
        }
        else
        {
            var bewerber = rat.Kandidaten(amt).Count(k => !k.IstAmtsinhaber);
            if (bewerber > 0) Kurz(spalte, bewerber == 1 ? "1 Bewerber" : $"{bewerber} Bewerber", "RatKnotenGut");
        }
        return knopf;
    }

    private static Label Kurz(Container eltern, string text, string variante)
    {
        var l = RatStil.Text(eltern, text, variante);
        l.ClipText = true;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill; // sonst wäre ein abgeschnittener Text null Pixel breit
        l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        l.MouseFilter = MouseFilterEnum.Ignore;
        return l;
    }

    /// <summary>Was ein Knoten im Tooltip sagt: Haus oder Ratsherr, Stand, Volk, Glaube, Befugnis und Einkommen.</summary>
    private static string Tipp(Stadtwirtschaft w, Amt amt)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<string>();
        if (amt.Ratsherr is { } r)
        {
            zeilen.Add($"{amt.Name}: {r.Name}, Ratsherr der Stadt");
            zeilen.Add($"Volk: {r.Daten.Volk.Einzahl()}" + (w.Glaube.IstAktiv ? $" · Glaube: {w.Glaube.GlaubeVon(r).Name()}" : ""));
            zeilen.Add($"Haltung: {r.Haltung.Kuerzel()} – {r.Haltung.Name()}");
        }
        else if (amt.Konto is { } k)
        {
            zeilen.Add($"{amt.Name}: {k.Inhaber}");
            var volk = w.Familien.Von(k)?.Volk;
            var glaube = w.Glaube.IstAktiv ? w.Glaube.GlaubeVon(k) : null;
            zeilen.Add($"Stand: {w.StandVon(k).Name}" + (volk is { } v ? $" · Volk: {v.Einzahl()}" : "") + (glaube is { } g ? $" · Glaube: {g.Name()}" : ""));
        }
        else
        {
            zeilen.Add($"{amt.Name}: unbesetzt");
            zeilen.Add($"Bewerben darf, wer mindestens den Stand {amt.Daten.MindestStand} hat.");
        }
        zeilen.Add($"Einkommen: {Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung} im Jahr");
        zeilen.Add($"Befugnis: {amt.Daten.Befugnis}");
        var waehler = w.Rat.Waehler(amt).Select(a => a.Name).ToList();
        zeilen.Add(waehler.Count == 0 ? "Gewählt wird von niemandem – es ist das höchste Amt." : $"Es wählen: {string.Join(", ", waehler)} (Inhaber höherer Ämter).");
        return string.Join("\n", zeilen);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wähler hervorheben (F4)
    // ═════════════════════════════════════════════════════════════════════════

    private void Hervorheben(Stadtrat rat, Amt amt)
    {
        Zuruecknehmen();
        foreach (var waehler in rat.Waehler(amt))
        {
            if (!_knoten.TryGetValue(waehler.Name, out var knopf)) continue;
            knopf.ThemeTypeVariation = "RatKnotenWaehler";
            _leuchtend.Add(waehler.Name);
        }
    }

    private void Zuruecknehmen()
    {
        foreach (var name in _leuchtend)
            if (_knoten.TryGetValue(name, out var knopf) && IsInstanceValid(knopf)) knopf.ThemeTypeVariation = _grundVariante[name];
        _leuchtend.Clear();
    }
}
