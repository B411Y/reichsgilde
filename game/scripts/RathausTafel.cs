using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Rathaus-Tafel unter dem Text der Auswahl (PR #200), wenn das Rathaus angeklickt ist und die Stadt eine Stadtkasse hat. Drei Reiter:
/// <list type="bullet">
/// <item><b>Stadtkasse</b>: Stand, Steuersatz, Einnahmen und Ausgaben dieses Jahres und des Vorjahres; die Posten einzeln nur für den Kämmerer
/// (<see cref="Stadtkasse.HaushaltVerboten"/>), sonst die Summen mit Hinweis.</item>
/// <item><b>Rat</b>: die Tagesordnung der nächsten Sitzung, Baustellen und Bauten der Stadt, das Fest, die Streife; Knopf zur Ratskammer
/// (dort stellt man Anträge und legt die Stimme fest).</item>
/// <item><b>Amt</b>: die Befugnis des eigenen Amtes – Streife verlegen (Hauptmann), Gefangene freilassen (Gefängniswärter), freier Antrag
/// (Bürgermeister), Hinweise und Knöpfe zu Gericht (Richter), Markt (Marktmeister) und Hafen (Zöllner). Ohne Amt nur, was die Ämter dürfen.</item>
/// </list>
/// Jeder Reiter wird nur neu aufgebaut, wenn sich sein Inhalt ändert (zweimal je Sekunde geprüft) – so gehen Klicks nicht verloren.
/// Wie alle Bedienfelder ändert sie nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class RathausTafel : VBoxContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private const int Breite = 300;

    /// <summary>Die Reiter in ihrer Reihenfolge (auch für die Startoption <c>--rathaus=&lt;reiter&gt;</c>).</summary>
    public enum Reiter { Stadtkasse, Rat, Amt }

    private Simulation? _simulation;
    private Konto? _spieler;
    private TabContainer _reiter = null!;
    private readonly Dictionary<Reiter, VBoxContainer> _seiten = new();
    private readonly Dictionary<Reiter, string> _kennzeichen = new();
    private double _seitPruefung = 1;

    /// <summary>Ergebnis eines Befehls – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Knöpfe zu den großen Tafeln: Ratskammer, Gericht, Markt.</summary>
    public event Action? RatGewuenscht, GerichtGewuenscht, MarktGewuenscht;

    /// <summary>Knopf „Letzte Ratssitzung ansehen“ (Reiter Rat): die abgehaltene Sitzung noch einmal im Ratssaal (Hauptszene.Ratssitzung.cs).</summary>
    public event Action? LetzteSitzungGewuenscht;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        _reiter = new TabContainer { FocusMode = FocusModeEnum.None, ClipTabs = false, CustomMinimumSize = new Vector2(Breite, 0) };
        _reiter.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_reiter);
        foreach (var r in Enum.GetValues<Reiter>())
        {
            var seite = new VBoxContainer { Name = r.ToString() };
            seite.AddThemeConstantOverride("separation", 5);
            _reiter.AddChild(seite);
            _seiten[r] = seite;
        }
        _reiter.TabChanged += _ => _seitPruefung = 1; // gleich aufbauen
        Visible = false;
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    /// <summary>Ein- oder ausblenden (nur bei Änderung; die Hauptszene ruft es jedes Bild).</summary>
    public void Zeigen(bool sichtbar)
    {
        sichtbar &= _simulation?.Wirtschaft?.Kasse.IstAktiv == true;
        if (Visible == sichtbar) return;
        Visible = sichtbar;
        if (sichtbar) { _kennzeichen.Clear(); _seitPruefung = 1; }
    }

    public void ReiterWaehlen(Reiter reiter) => _reiter.CurrentTab = (int)reiter;

    public override void _Process(double delta)
    {
        if (!Visible || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        _seitPruefung += delta;
        if (_seitPruefung < 0.5) return;
        _seitPruefung = 0;
        var reiter = (Reiter)_reiter.CurrentTab;
        var kennzeichen = Kennzeichen(w, reiter);
        if (_kennzeichen.GetValueOrDefault(reiter) == kennzeichen) return;
        _kennzeichen[reiter] = kennzeichen;
        var seite = _seiten[reiter];
        UiHilfe.KinderEntfernen(seite);
        switch (reiter)
        {
            case Reiter.Stadtkasse: KasseAufbauen(w, seite); break;
            case Reiter.Rat: RatAufbauen(w, seite); break;
            default: AmtAufbauen(w, seite); break;
        }
    }

    /// <summary>Alles, was der Reiter zeigt, in einer Zeile: Ändert sie sich, wird er neu aufgebaut.</summary>
    private string Kennzeichen(Stadtwirtschaft w, Reiter reiter)
    {
        var bf = w.Befugnisse;
        return reiter switch
        {
            Reiter.Stadtkasse => $"{w.Jahr}|{Math.Round(w.Kasse.Stand)}|{w.Kasse.SteuersatzProzent}|{w.Kasse.HaushaltVerboten(_spieler!)}|" +
                                 string.Join(",", w.Kasse.DiesesJahr.Posten.Select(p => Math.Round(p.Betrag))),
            Reiter.Rat => $"{w.Jahr}|{string.Join(";", w.Ratsantraege.Tagesordnung.Select(p => p.Text + p.Antragsteller.Inhaber))}|" +
                          $"{string.Join(",", w.Ratsantraege.Baustellen.Select(b => b.Kennung))}|{string.Join(",", w.Ratsantraege.Gebaut)}|" +
                          $"{w.Ratsantraege.FestJahr}|{bf.Streife}|{w.Rat.AmtVon(_spieler!)?.Name}|{w.Rat.LetzteRatssitzung?.Jahr}",
            _ => $"{w.Jahr}|{w.Rat.AmtVon(_spieler!)?.Name}|{bf.Streife}|{bf.FreierAntragOffen(_spieler!)}|{Math.Round(_spieler!.Vermoegen / 100)}|" +
                 string.Join(",", w.Konten.Where(k => w.Justiz.IstAktiv && w.Justiz.ImKerker(k)).Select(k => k.Inhaber + Math.Round(k.Vermoegen / 100))),
        };
    }

    private void Befehlen(Befehl befehl)
    {
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, ergebnis =>
        {
            Gemeldet?.Invoke(UiHilfe.MeldungText(ergebnis));
            _kennzeichen.Clear();
            _seitPruefung = 1;
        });
    }

    private static Label Text(Container eltern, string text, int groesse = 13, Color? farbe = null) =>
        Hauptmenue.Text(eltern, text, groesse, farbe, umbruchBreite: Breite);

    private static Button Knopf(Container eltern, string text, Action aktion, string tipp = "")
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, TooltipText = tipp, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        knopf.AddThemeFontSizeOverride("font_size", 13);
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter „Stadtkasse“
    // ═════════════════════════════════════════════════════════════════════════

    private void KasseAufbauen(Stadtwirtschaft w, VBoxContainer seite)
    {
        var kasse = w.Kasse;
        var waehrung = w.Daten.Allgemein.Waehrung;
        Text(seite, $"Stand: {Zahlenformat.Ganz(kasse.Stand)} {waehrung}", 18, kasse.Stand < 0 ? Rot : Gold).TooltipText = "Die Stadtkasse: Einnahmen minus Ausgaben. Ist sie im Minus, kann die Stadt keine Vorhaben bezahlen.";
        Text(seite, $"Umsatzsteuer: {kasse.SteuersatzProzent} % auf alle Verkäufe", 14).TooltipText = "Den Satz bestimmt der Rat auf Antrag. Er trifft alle Häuser gleich und füllt die Stadtkasse.";
        var verboten = kasse.HaushaltVerboten(_spieler!);
        var raster = new GridContainer { Columns = 3 };
        raster.AddThemeConstantOverride("h_separation", 14);
        raster.AddThemeConstantOverride("v_separation", 2);
        seite.AddChild(raster);
        void Zeile(string name, string jetzt, string vorher, int groesse = 13, Color? farbe = null)
        {
            var a1 = Hauptmenue.Text(raster, name, groesse, farbe);
            var a2 = Hauptmenue.Text(raster, jetzt, groesse, farbe);
            var a3 = Hauptmenue.Text(raster, vorher, groesse, farbe);
            a2.HorizontalAlignment = a3.HorizontalAlignment = HorizontalAlignment.Right;
            a1.TooltipText = a2.TooltipText = a3.TooltipText = $"{name}: dieses Jahr bisher {jetzt}, im Vorjahr {vorher}.";
        }
        string Betrag(double? x) => x is { } b ? Zahlenformat.Ganz(b) : "–";
        var dj = kasse.DiesesJahr;
        var vj = kasse.Vorjahr;
        Zeile("", $"{dj.Jahr} bisher", vj is null ? "Vorjahr" : vj.Jahr.ToString(), 12, Grau);
        if (verboten is null)
        {
            // Der Kämmerer sieht jeden Posten – in der Reihenfolge der Aufzählung, erst Einnahmen, dann Ausgaben
            foreach (var einnahme in new[] { true, false })
            {
                var posten = Enum.GetValues<Kassenposten>().Where(p => p.IstEinnahme() == einnahme && (Math.Abs(dj[p]) > 0.5 || Math.Abs(vj?[p] ?? 0) > 0.5));
                foreach (var p in posten)
                    Zeile((einnahme ? "+ " : "− ") + p.Name(), Betrag(dj[p]), Betrag(vj?[p]));
            }
        }
        Zeile("Einnahmen", Betrag(dj.Einnahmen), Betrag(vj?.Einnahmen), 13, Gruen);
        Zeile("Ausgaben", Betrag(dj.Ausgaben), Betrag(vj?.Ausgaben), 13, Rot);
        Zeile("Saldo", Betrag(dj.Saldo), Betrag(vj?.Saldo), 14);
        if (verboten is not null)
            Text(seite, verboten + " Die übrigen sehen nur die Summen.", 12, Grau);
        else
            Text(seite, "Als Kämmerer seht ihr den ganzen Haushalt und beantragt den Steuersatz ohne Aktionspunkte.", 12, Grau);
        Text(seite, "Einnahmen: Umsatzsteuer, Glaubenszins, Bußen, Gebühren und der Hafenzoll. Ausgaben: Amtsgehälter, Bauvorhaben und Feste. " +
                    "Eine leere Kasse hat keine Folgen – nur Bau und Fest brauchen Deckung.", 12, Grau);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter „Rat“
    // ═════════════════════════════════════════════════════════════════════════

    private void RatAufbauen(Stadtwirtschaft w, VBoxContainer seite)
    {
        var ra = w.Ratsantraege;
        Text(seite, "Tagesordnung der Ratssitzung am Jahresende", 14, Gold);
        var punkte = ra.Tagesordnung;
        if (punkte.Count == 0) Text(seite, "Bisher liegt kein Antrag vor.", 13, Grau);
        var nummer = 1;
        foreach (var p in punkte)
        {
            var zeile = UiHilfe.MitWappen(seite, p.Antragsteller.Wappen, $"{nummer++}. {p.Text} ({p.Antragsteller.Inhaber})", 13, p.Antragsteller == _spieler ? Gold : null);
            zeile.AutowrapMode = TextServer.AutowrapMode.WordSmart; // lange Punkte brechen um, statt die Tafel zu verbreitern
            zeile.CustomMinimumSize = new Vector2(Breite - 30, 0);
        }
        var knopf = Knopf(seite, "Ratskammer: Anträge und Stimmen (R)", () => RatGewuenscht?.Invoke());
        knopf.TooltipText = "Dort stellt ihr Anträge (als Mitglied des Rates) und legt eure Stimme fest.";
        if (w.Rat.LetzteRatssitzung is { Abgehalten.Count: > 0 } letzte)
            Knopf(seite, $"Letzte Ratssitzung ({letzte.Jahr}) ansehen", () => LetzteSitzungGewuenscht?.Invoke(),
                "Die Sitzung noch einmal im Ratssaal: Anträge, Begründungen, alle Stimmen und die Ergebnisse. Die Partie steht so lange.");
        else if (w.Rat.LetzteSitzung.Count > 0)
            Text(seite, "Das Protokoll der letzten Ratssitzung steht im Bericht (Reiter Protokoll).", 12, Grau);

        seite.AddChild(new HSeparator());
        Text(seite, "Bauten der Stadt", 14, Gold);
        var minute = w.Uhr.VergangeneMinuten;
        foreach (var b in ra.Baustellen)
        {
            var rest = Math.Max(0, b.FertigAb - minute);
            Text(seite, $"Im Bau: {b.Name} – noch etwa {Math.Ceiling(rest / 60.0)} Stunden{(b.Antragsteller is { } a ? $" (beantragt von {a.Inhaber})" : "")}");
        }
        foreach (var k in ra.Gebaut)
            Text(seite, $"Fertig: {ra.BauvorhabenNamens(k)?.Name ?? k}", 13, Gruen);
        if (ra.Baustellen.Count == 0 && ra.Gebaut.Count == 0)
            Text(seite, "Die Stadt hat noch nichts gebaut.", 13, Grau);
        if (ra.FestLaeuft)
            Text(seite, "In diesem Jahr feiert die Stadt: mehr Marktbesucher und Wirtshausgäste.", 13, Gruen);
        else if (ra.FestJahr is { } jahr && jahr > w.Jahr)
            Text(seite, $"Im Jahr {jahr} feiert die Stadt.", 13);

        if (w.Befugnisse.Streife is { } viertel)
            Text(seite, $"Die Streife geht vor allem im {Amtsbefugnisse.ViertelName(viertel)} (Stadthauptmann {w.Rat.AmtNamens(Amtsbefugnisse.Stadthauptmann)?.Inhaber}).", 12, Grau);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter „Amt“
    // ═════════════════════════════════════════════════════════════════════════

    private void AmtAufbauen(Stadtwirtschaft w, VBoxContainer seite)
    {
        var bf = w.Befugnisse;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var amt = w.Rat.AmtVon(_spieler!);
        if (amt is null)
        {
            Text(seite, "Ihr habt kein Amt.", 14);
            Text(seite, "Jedes Amt hat eine Befugnis: Bürgermeister – ein Antrag im Jahr ohne Aktionspunkte, Ausschlag bei Gleichstand · " +
                        "Kämmerer – sieht den Haushalt, Steuersatz ohne Aktionspunkte · Stadtrichter – Buße ±25 % · Stadthauptmann – Streife verlegen · " +
                        "Marktmeister – Preise der nächsten Stunde, halbe Steuer am Markt · Gefängniswärter – Gefangene gegen Kaution freilassen · " +
                        "Zöllner – zollfrei am eigenen Hafen.", 12, Grau);
            return;
        }
        Text(seite, $"Euer Amt: {amt.Name} ({Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung} im Jahr aus der Stadtkasse)", 14, Gold);
        switch (amt.Name)
        {
            case Amtsbefugnisse.Buergermeister:
                Text(seite, bf.FreierAntragOffen(_spieler!)
                    ? "Euer freier Antrag ist offen: Einen Antrag in diesem Jahr stellt ihr ohne Aktionspunkte (Statut, Glaube, Pflaster, Steuer, Bau oder Fest)."
                    : "Euren freien Antrag habt ihr in diesem Jahr schon gestellt.", 13, bf.FreierAntragOffen(_spieler!) ? Gruen : Grau);
                Text(seite, "Bei Gleichstand im Rat gibt eure Stimme den Ausschlag.", 12, Grau);
                Knopf(seite, "Zur Ratskammer (R)", () => RatGewuenscht?.Invoke());
                break;
            case Amtsbefugnisse.Kaemmerer:
                Text(seite, "Ihr seht den ganzen Haushalt (Reiter Stadtkasse) und beantragt den Steuersatz ohne Aktionspunkte.", 13);
                Knopf(seite, "Zur Ratskammer (R)", () => RatGewuenscht?.Invoke());
                break;
            case Amtsbefugnisse.Stadtrichter:
                Text(seite, $"Im Vorsitz eines Verfahrens setzt ihr die Buße um bis zu ±{Zahlenformat.Ganz(bf.Daten.RichterSpanne * 100)} % herauf oder herab – " +
                            "im Gerichtssaal bei den laufenden Verfahren.", 13);
                if (w.Justiz.IstAktiv)
                    Text(seite, w.Justiz.Verfahren.Count == 0 ? "Zurzeit ist kein Verfahren anhängig." : $"Anhängige Verfahren: {w.Justiz.Verfahren.Count}", 12, Grau);
                Knopf(seite, "Zum Gericht", () => GerichtGewuenscht?.Invoke());
                break;
            case Amtsbefugnisse.Stadthauptmann:
                StreifeAufbauen(w, seite);
                break;
            case Amtsbefugnisse.Marktmeister:
                Text(seite, $"Ihr seht die Preise der nächsten Stunde (im Markt) und zahlt auf eigene Verkäufe am Markt nur " +
                            $"{Zahlenformat.Ganz(w.SteuersatzMarkt(_spieler!) * 100)} % statt {Zahlenformat.Ganz(w.Umsatzsteuer * 100)} % Steuer.", 13);
                Knopf(seite, "Zum Markt (M)", () => MarktGewuenscht?.Invoke());
                break;
            case Amtsbefugnisse.Gefaengniswaerter:
                GefangeneAufbauen(w, seite);
                break;
            case Amtsbefugnisse.Zoellner:
                Text(seite, w.Schiffahrt.IstAktiv
                    ? "Eure Schiffe zahlen am eigenen Hafen keinen Zoll – die erste Zollstelle jeder Fahrt gehört der Stadt."
                    : "Am eigenen Hafen zahlt ihr keinen Zoll – diese Stadt hat aber keinen Schiffshandel.", 13);
                break;
            default:
                Text(seite, "Dieses Amt hat keine eigene Befugnis.", 13, Grau);
                break;
        }
    }

    /// <summary>Stadthauptmann: die Streife in ein Viertel verlegen (dort schützt die Wache doppelt) – mit den eigenen Betrieben je Viertel.</summary>
    private void StreifeAufbauen(Stadtwirtschaft w, VBoxContainer seite)
    {
        var bf = w.Befugnisse;
        Text(seite, "Ihr verlegt die Streife: In einem Viertel schützt die Wache nachts doppelt, in den anderen wie gewohnt.", 13);
        var eigene = w.Betriebe.Where(b => b.Konto == _spieler && b.InDerStadt).GroupBy(bf.ViertelVon).ToDictionary(g => g.Key, g => g.Count());
        var wahl = new OptionButton { FocusMode = FocusModeEnum.None };
        wahl.AddThemeFontSizeOverride("font_size", 13);
        var ziele = new List<Stadtviertel?> { null };
        ziele.AddRange(Enum.GetValues<Stadtviertel>().Select(v => (Stadtviertel?)v));
        foreach (var z in ziele)
            wahl.AddItem(z is { } v ? $"{Amtsbefugnisse.ViertelName(v)} ({eigene.GetValueOrDefault(v)} eigene Betriebe)" : "gleichmäßig über die Stadt");
        wahl.Selected = ziele.IndexOf(bf.Streife);
        var grund = bf.StreifeVerboten(_spieler!);
        wahl.Disabled = grund is not null;
        wahl.TooltipText = grund ?? "Die Viertel teilen sich nach der Himmelsrichtung von der Stadtmitte aus.";
        wahl.ItemSelected += i => Befehlen(new StreifeVerlegen(ziele[(int)i]));
        seite.AddChild(wahl);
    }

    /// <summary>Gefängniswärter: die Gefangenen mit Kaution und „Freilassen“ (das Geld zahlt der Gefangene, ihr bekommt es).</summary>
    private void GefangeneAufbauen(Stadtwirtschaft w, VBoxContainer seite)
    {
        var bf = w.Befugnisse;
        var waehrung = w.Daten.Allgemein.Waehrung;
        Text(seite, "Ihr lasst Gefangene gegen eine Kaution vorzeitig frei – der Gefangene zahlt, das Geld bekommt ihr.", 13);
        var gefangene = w.Justiz.IstAktiv ? w.Konten.Where(k => w.Justiz.ImKerker(k)).ToList() : new List<Konto>();
        if (gefangene.Count == 0)
        {
            Text(seite, "Zurzeit sitzt niemand im Kerker.", 13, Grau);
            return;
        }
        foreach (var g in gefangene)
        {
            Text(seite, $"{g.Inhaber}: bis Ende {w.Justiz.KerkerBis(g)} · Kaution {Zahlenformat.Ganz(bf.Kaution(g))} {waehrung}");
            var grund = bf.FreilassenVerboten(_spieler!, g);
            var name = g.Inhaber;
            var knopf = Knopf(seite, "Freilassen", () => Befehlen(new GefangenenFreilassen(name)),
                grund ?? $"{name} zahlt {Zahlenformat.Ganz(bf.Kaution(g))} {waehrung} Kaution an euch und kommt sofort frei.");
            knopf.Disabled = grund is not null;
        }
    }
}
