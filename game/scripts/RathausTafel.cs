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
/// <item><b>Euer Amt</b>: der Name des eigenen Amtes mit Einkommen und ein Knopf zum eigenen Knoten im Ratsbaum. Die Befugnis mit ihren Knöpfen
/// (Streife, Gefangene, freier Antrag, Gericht, Markt) steht dort (<see cref="AmtBefugnisAnzeige"/>); die Kurzübersicht bleibt so klein, dass sie ganz in die Tafel passt.</item>
/// </list>
/// Die Tafel wird nur neu aufgebaut, wenn sich ihr Inhalt ändert (zweimal je Sekunde geprüft) – so gehen Klicks nicht verloren.
/// Aussehen (Farben, Ränder, Abstände, Knopf) kommt aus dem Thema <c>res://scenes/rat_thema.tres</c> (<see cref="RatStil"/>).
/// Sie liest den Kern nur und ändert nichts.
/// </summary>
public partial class RathausTafel : VBoxContainer
{
    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _seite = null!;
    private string _kennzeichen = "";
    private double _seitPruefung = 1;

    /// <summary>Knöpfe zur Ratskammer (auf einem Reiter).</summary>
    public event Action<RatAnzeige.Reiter>? RatGewuenscht;

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
            var amt = w.Rat.AmtVon(_spieler!);
            RatStil.Text(seite, amt is null ? "Ihr habt kein Amt." : $"{amt.Name} · {Zahlenformat.Ganz(amt.Daten.Einkommen)} {w.Daten.Allgemein.Waehrung} im Jahr",
                amt is null ? "RatKurzHinweis" : "RatKurzGold", umbrechen: true, breite: "kurz_breite");
            Knopf(seite, amt is null ? "Ämter ansehen" : "Euer Amt ansehen", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Aemter),
                "Der Ratsbaum der Ratskammer: bei eurem Amt seine Befugnis und die Knöpfe dazu, sonst die Voraussetzungen und das Bewerben.");
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
}
