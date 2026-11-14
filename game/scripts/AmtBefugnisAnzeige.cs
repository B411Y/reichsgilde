using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// „Euer Amt“: was das Amt des Spielers darf – Hinweise und Knöpfe je Amt (Streife verlegen, Gefangene freilassen, freier Antrag, Verweise auf Gericht, Markt und Kasse).
/// Er steht am eigenen Knoten des Ratsbaums in der Ratskammer (F7); die Kurzübersicht im Rathaus (<see cref="RathausTafel"/>) verweist nur dorthin.
/// Die Anzeige ändert nichts selbst, sondern schickt Befehle über <see cref="Befehlen"/>. Aussehen nur aus dem Thema („Rat…“).
/// </summary>
public sealed class AmtBefugnisAnzeige
{
    private readonly Konto _spieler;

    /// <summary>Schickt einen Befehl des Spielers (die Anzeige oben meldet das Ergebnis).</summary>
    public Action<Befehl> Befehlen { get; init; } = _ => { };
    /// <summary>Knöpfe zu den großen Tafeln: Ratskammer (auf einem Reiter), Gericht, Markt.</summary>
    public Action<RatAnzeige.Reiter>? RatGewuenscht { get; init; }
    public Action? GerichtGewuenscht { get; init; }
    public Action? MarktGewuenscht { get; init; }

    public AmtBefugnisAnzeige(Konto spieler) => _spieler = spieler;

    private static Label Text(Container eltern, string text, string variante = "Text") => RatStil.Text(eltern, text, "Rat" + variante, umbrechen: true);
    private Button Knopf(Container eltern, string text, Action aktion, string tipp = "") => RatStil.Amtsknopf(eltern, text, aktion, tipp);

    /// <summary>Hängt den Abschnitt in <paramref name="seite"/> (die Überschrift „Euer Amt: …“ gehört dazu).</summary>
    public void Aufbauen(Stadtwirtschaft w, Container seite)
    {
        var bf = w.Befugnisse;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var amt = w.Rat.AmtVon(_spieler);
        if (amt is null)
        {
            Text(seite, "Ihr habt kein Amt.", "Text");
            Text(seite, "Jedes Amt hat eine Befugnis: Bürgermeister – ein Antrag im Jahr ohne Aktionspunkte, Ausschlag bei Gleichstand · " +
                        "Kämmerer – sieht den Haushalt, Steuersatz ohne Aktionspunkte · Stadtrichter – Buße ±25 % · Stadthauptmann – Streife verlegen · " +
                        "Marktmeister – Preise der nächsten Stunde, halbe Steuer am Markt · Gefängniswärter – Gefangene gegen Kaution freilassen · " +
                        "Zöllner – zollfrei am eigenen Hafen.", "Hinweis");
            return;
        }
        Text(seite, $"Euer Amt: {amt.Name} ({Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung} im Jahr aus der Stadtkasse)", "Gold");
        switch (amt.Name)
        {
            case Amtsbefugnisse.Buergermeister:
                var offen = bf.FreierAntragOffen(_spieler);
                Text(seite, offen
                    ? "Euer freier Antrag ist offen: Einen Antrag in diesem Jahr stellt ihr ohne Aktionspunkte (Statut, Glaube, Pflaster, Steuer, Bau oder Fest)."
                    : "Euren freien Antrag habt ihr in diesem Jahr schon gestellt.", offen ? "Gut" : "Hinweis");
                Text(seite, "Bei Gleichstand im Rat gibt eure Stimme den Ausschlag.", "Hinweis");
                Knopf(seite, "Zu den Anträgen (R)", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Antraege));
                break;
            case Amtsbefugnisse.Kaemmerer:
                Text(seite, "Ihr seht den ganzen Haushalt (Ratskammer, Reiter Kasse) und beantragt den Steuersatz ohne Aktionspunkte.", "Text");
                Knopf(seite, "Zur Kasse der Ratskammer", () => RatGewuenscht?.Invoke(RatAnzeige.Reiter.Kasse));
                break;
            case Amtsbefugnisse.Stadtrichter:
                Text(seite, $"Im Vorsitz eines Verfahrens setzt ihr die Buße um bis zu ±{Zahlenformat.Ganz(bf.Daten.RichterSpanne * 100)} % herauf oder herab – " +
                            "im Gerichtssaal bei den laufenden Verfahren.", "Text");
                if (w.Justiz.IstAktiv)
                    Text(seite, w.Justiz.Verfahren.Count == 0 ? "Zurzeit ist kein Verfahren anhängig." : $"Anhängige Verfahren: {w.Justiz.Verfahren.Count}", "Hinweis");
                Knopf(seite, "Zum Gericht", () => GerichtGewuenscht?.Invoke());
                break;
            case Amtsbefugnisse.Stadthauptmann:
                StreifeAufbauen(w, seite);
                break;
            case Amtsbefugnisse.Marktmeister:
                Text(seite, $"Ihr seht die Preise der nächsten Stunde (im Markt) und zahlt auf eigene Verkäufe am Markt nur " +
                            $"{Zahlenformat.Ganz(w.SteuersatzMarkt(_spieler) * 100)} % statt {Zahlenformat.Ganz(w.Umsatzsteuer * 100)} % Steuer.", "Text");
                Knopf(seite, "Zum Markt (M)", () => MarktGewuenscht?.Invoke());
                break;
            case Amtsbefugnisse.Gefaengniswaerter:
                GefangeneAufbauen(w, seite);
                break;
            case Amtsbefugnisse.Zoellner:
                Text(seite, w.Schiffahrt.IstAktiv
                    ? "Eure Schiffe zahlen am eigenen Hafen keinen Zoll – die erste Zollstelle jeder Fahrt gehört der Stadt."
                    : "Am eigenen Hafen zahlt ihr keinen Zoll – diese Stadt hat aber keinen Schiffshandel.", "Text");
                break;
            default:
                Text(seite, "Dieses Amt hat keine eigene Befugnis.", "Hinweis");
                break;
        }
        if (w.Befugnisse.Streife is { } viertel && amt.Name != Amtsbefugnisse.Stadthauptmann)
            Text(seite, $"Die Streife geht vor allem im {Amtsbefugnisse.ViertelName(viertel)} (Stadthauptmann {w.Rat.AmtNamens(Amtsbefugnisse.Stadthauptmann)?.Inhaber}).", "Hinweis");
    }

    /// <summary>Stadthauptmann: die Streife in ein Viertel verlegen (dort schützt die Wache doppelt) – mit den eigenen Betrieben je Viertel.</summary>
    private void StreifeAufbauen(Stadtwirtschaft w, Container seite)
    {
        var bf = w.Befugnisse;
        Text(seite, "Ihr verlegt die Streife: In einem Viertel schützt die Wache nachts doppelt, in den anderen wie gewohnt.", "Text");
        var eigene = w.Betriebe.Where(b => b.Konto == _spieler && b.InDerStadt).GroupBy(bf.ViertelVon).ToDictionary(g => g.Key, g => g.Count());
        var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, ThemeTypeVariation = "RatAuswahl" };
        var ziele = new List<Stadtviertel?> { null };
        ziele.AddRange(Enum.GetValues<Stadtviertel>().Select(v => (Stadtviertel?)v));
        foreach (var z in ziele)
            wahl.AddItem(z is { } v ? $"{Amtsbefugnisse.ViertelName(v)} ({eigene.GetValueOrDefault(v)} eigene Betriebe)" : "gleichmäßig über die Stadt");
        wahl.Selected = ziele.IndexOf(bf.Streife);
        var grund = bf.StreifeVerboten(_spieler);
        wahl.Disabled = grund is not null;
        wahl.TooltipText = grund ?? "Die Viertel teilen sich nach der Himmelsrichtung von der Stadtmitte aus.";
        wahl.ItemSelected += i => Befehlen(new StreifeVerlegen(ziele[(int)i]));
        seite.AddChild(wahl);
    }

    /// <summary>Gefängniswärter: die Gefangenen mit Kaution und „Freilassen“ (das Geld zahlt der Gefangene, ihr bekommt es).</summary>
    private void GefangeneAufbauen(Stadtwirtschaft w, Container seite)
    {
        var bf = w.Befugnisse;
        var waehrung = w.Daten.Allgemein.Waehrung;
        Text(seite, "Ihr lasst Gefangene gegen eine Kaution vorzeitig frei – der Gefangene zahlt, das Geld bekommt ihr.", "Text");
        var gefangene = w.Justiz.IstAktiv ? w.Konten.Where(k => w.Justiz.ImKerker(k)).ToList() : new List<Konto>();
        if (gefangene.Count == 0)
        {
            Text(seite, "Zurzeit sitzt niemand im Kerker.", "Hinweis");
            return;
        }
        foreach (var g in gefangene)
        {
            Text(seite, $"{g.Inhaber}: bis Ende {w.Justiz.KerkerBis(g)} · Kaution {Zahlenformat.Ganz(bf.Kaution(g))} {waehrung}");
            var grund = bf.FreilassenVerboten(_spieler, g);
            var name = g.Inhaber;
            var knopf = Knopf(seite, "Freilassen", () => Befehlen(new GefangenenFreilassen(name)),
                grund ?? $"{name} zahlt {Zahlenformat.Ganz(bf.Kaution(g))} {waehrung} Kaution an euch und kommt sofort frei.");
            knopf.Disabled = grund is not null;
        }
    }
}
