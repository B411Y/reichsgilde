using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Rathaus-Tafel unter dem Text der Auswahl (PR #200), wenn das Rathaus angeklickt ist und die Stadt eine Stadtkasse hat – seit dem PR „Ratskammer mit Reitern“
/// nur noch die <b>Kurzübersicht</b>; alles Ausführliche steht in der Ratskammer. Drei kleine Felder:
/// <list type="bullet">
/// <item><b>Stadtkasse</b>: der Stand und der Steuersatz in einer Zeile, Knopf zum Reiter „Kasse“ der Ratskammer.</item>
/// <item><b>Nächste Sitzung</b>: wie viele Anträge auf der Tagesordnung stehen, Knopf zum Reiter „Anträge“; dazu „Letzte Ratssitzung ansehen“.</item>
/// <item><b>Euer Amt</b>: die Befugnis des eigenen Amtes – Streife verlegen (Hauptmann), Gefangene freilassen (Gefängniswärter), freier Antrag
/// (Bürgermeister), Hinweise und Knöpfe zu Gericht (Richter), Markt (Marktmeister) und Hafen (Zöllner). Ohne Amt nur, was die Ämter dürfen.
/// Mit dem Amtsbaum der Ratskammer zieht die Befugnis an den eigenen Knoten um.</item>
/// </list>
/// Die Tafel wird nur neu aufgebaut, wenn sich ihr Inhalt ändert (zweimal je Sekunde geprüft) – so gehen Klicks nicht verloren.
/// Aussehen (Farben, Ränder, Abstände, Knopf) kommt aus dem Thema <c>res://scenes/rat_thema.tres</c> (<see cref="RatStil"/>).
/// Wie alle Bedienfelder ändert sie nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class RathausTafel : VBoxContainer
{
    private static Color Gold => RatStil.Farbe("gold");
    private static Color Grau => RatStil.Farbe("grau");
    private static Color Gruen => RatStil.Farbe("gruen");
    private static Color Rot => RatStil.Farbe("rot");
    private const int Breite = 300;

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _seite = null!;
    private string _kennzeichen = "";
    private double _seitPruefung = 1;

    /// <summary>Ergebnis eines Befehls – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Knöpfe zu den großen Tafeln: Ratskammer (auf einem Reiter), Gericht, Markt.</summary>
    public event Action<RatAnzeige.Reiter>? RatGewuenscht;
    public event Action? GerichtGewuenscht, MarktGewuenscht;

    /// <summary>Knopf „Letzte Ratssitzung ansehen“: die abgehaltene Sitzung noch einmal im Ratssaal (Hauptszene.Ratssitzung.cs).</summary>
    public event Action? LetzteSitzungGewuenscht;

    public override void _Ready()
    {
        RatStil.Anwenden(this);
        _seite = RatStil.Container<VBoxContainer>(this, "RatSpalte");
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
        if (sichtbar) { _kennzeichen = ""; _seitPruefung = 1; }
    }

    public override void _Process(double delta)
    {
        if (!Visible || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        _seitPruefung += delta;
        if (_seitPruefung < 0.5) return;
        _seitPruefung = 0;
        var kennzeichen = Kennzeichen(w);
        if (_kennzeichen == kennzeichen) return;
        _kennzeichen = kennzeichen;
        UiHilfe.KinderEntfernen(_seite);
        UebersichtAufbauen(w);
        FeldAufbauen(_seite, "Euer Amt", seite =>
        {
            AmtAufbauen(w, seite);
            if (w.Befugnisse.Streife is { } viertel && w.Rat.AmtVon(_spieler!)?.Name != Amtsbefugnisse.Stadthauptmann)
                Text(seite, $"Die Streife geht vor allem im {Amtsbefugnisse.ViertelName(viertel)} (Stadthauptmann {w.Rat.AmtNamens(Amtsbefugnisse.Stadthauptmann)?.Inhaber}).", 12, Grau);
        });
    }

    /// <summary>Alles, was die Tafel zeigt, in einer Zeile: Ändert sie sich, wird sie neu aufgebaut.</summary>
    private string Kennzeichen(Stadtwirtschaft w)
    {
        var bf = w.Befugnisse;
        return $"{w.Jahr}|{Math.Round(w.Kasse.Stand)}|{w.Kasse.SteuersatzProzent}|{string.Join(";", w.Ratsantraege.Tagesordnung.Select(p => p.Text + p.Antragsteller.Inhaber))}|" +
               $"{w.Rat.LetzteRatssitzung?.Jahr}|{w.Rat.AmtVon(_spieler!)?.Name}|{bf.Streife}|{bf.FreierAntragOffen(_spieler!)}|{Math.Round(_spieler!.Vermoegen / 100)}|" +
               string.Join(",", w.Konten.Where(k => w.Justiz.IstAktiv && w.Justiz.ImKerker(k)).Select(k => k.Inhaber + Math.Round(k.Vermoegen / 100)));
    }

    private void Befehlen(Befehl befehl)
    {
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, ergebnis =>
        {
            Gemeldet?.Invoke(UiHilfe.MeldungText(ergebnis));
            _kennzeichen = "";
            _seitPruefung = 1;
        });
    }

    private static Label Text(Container eltern, string text, int groesse = 13, Color? farbe = null) =>
        Hauptmenue.Text(eltern, text, groesse, farbe, umbruchBreite: Breite);

    private static Button Knopf(Container eltern, string text, Action aktion, string tipp = "") =>
        RatStil.Amtsknopf(eltern, text, aktion, tipp);

    /// <summary>Ein Feld der Kurzübersicht: Rahmen aus dem Thema („RatKurzTafel“), darin Überschrift und Inhalt.</summary>
    private static void FeldAufbauen(Container eltern, string titel, Action<VBoxContainer> fuellen)
    {
        var feld = RatStil.Container<PanelContainer>(eltern, "RatKurzTafel");
        var spalte = RatStil.Container<VBoxContainer>(feld, "RatSpalte");
        RatStil.Text(spalte, titel, "RatAbschnitt");
        fuellen(spalte);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kurzübersicht: Stadtkasse und nächste Sitzung
    // ═════════════════════════════════════════════════════════════════════════

    private void UebersichtAufbauen(Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        FeldAufbauen(_seite, "Stadtkasse", seite =>
        {
            var kasse = w.Kasse;
            RatStil.Text(seite, $"Stand {Zahlenformat.Ganz(kasse.Stand)} {waehrung} · Umsatzsteuer {kasse.SteuersatzProzent} %", kasse.Stand < 0 ? "RatSchlecht" : "RatText")
                .TooltipText = "Die Stadtkasse: Einnahmen minus Ausgaben. Den Steuersatz bestimmt der Rat auf Antrag.";
            Knopf(seite, "Kasse ansehen", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Kasse), "Der Haushalt, die Bauten der Stadt und die Anträge zu Steuer, Bau und Fest – in der Ratskammer.");
        });
        FeldAufbauen(_seite, "Nächste Ratssitzung", seite =>
        {
            var n = w.Ratsantraege.Tagesordnung.Count;
            RatStil.Text(seite, n == 0 ? "Bisher liegt kein Antrag vor." : n == 1 ? "Ein Antrag auf der Tagesordnung." : $"{n} Anträge auf der Tagesordnung.", n == 0 ? "RatHinweis" : "RatText");
            Knopf(seite, "Anträge und Stimmen (R)", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Antraege), "Dort stellt ihr Anträge (als Mitglied des Rates) und legt eure Stimme fest.");
            if (w.Rat.LetzteRatssitzung is { Abgehalten.Count: > 0 } letzte)
                Knopf(seite, $"Letzte Ratssitzung ({letzte.Jahr}) ansehen", () => LetzteSitzungGewuenscht?.Invoke(),
                    "Die Sitzung noch einmal im Ratssaal: Anträge, Begründungen, alle Stimmen und die Ergebnisse. Die Partie steht so lange.");
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Euer Amt
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
                Knopf(seite, "Zur Ratskammer (R)", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Antraege));
                break;
            case Amtsbefugnisse.Kaemmerer:
                Text(seite, "Ihr seht den ganzen Haushalt (Ratskammer, Reiter Kasse) und beantragt den Steuersatz ohne Aktionspunkte.", 13);
                Knopf(seite, "Zur Kasse der Ratskammer", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Kasse));
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
