using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer, Teil 2 (PR #200): die Tagesordnung der nächsten Sitzung und die Anträge zu Steuer, Bau und Fest.
/// Je Antragsart steht entweder der vorliegende Antrag mit Prognose und eurer Stimme da oder – für Mitglieder des Rates – die Auswahl,
/// um selbst einen zu stellen (gesperrt mit dem Grund des Kerns, mit AP-Kosten bzw. „ohne AP“ für Bürgermeister und Kämmerer).
/// </summary>
public partial class RatAnzeige
{
    /// <summary>Die Tagesordnung der nächsten Ratssitzung: alle Anträge in der Reihenfolge der Abstimmung, mit Antragsteller.</summary>
    private void TagesordnungAbschnitt(Stadtwirtschaft w)
    {
        if (!w.Ratsantraege.IstAktiv) return;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Tagesordnung der Ratssitzung am Jahresende", 16);
        var punkte = w.Ratsantraege.Tagesordnung;
        if (punkte.Count == 0)
        {
            Hauptmenue.Text(_inhalt, "Bisher liegt kein Antrag vor.", 13, Grau);
            return;
        }
        var nummer = 1;
        foreach (var p in punkte)
            UiHilfe.MitWappen(_inhalt, p.Antragsteller.Wappen, $"{nummer++}. {p.Thema}: {p.Text} – beantragt von {p.Antragsteller.Inhaber}", 14,
                p.Antragsteller == _spieler ? Gold : null);
    }

    /// <summary>Anträge zu Steuersatz, Bauvorhaben und Fest (nur mit Stadtkasse).</summary>
    private void KassenAntraegeAbschnitt(Stadtwirtschaft w, Amt? eigenesAmt)
    {
        var ra = w.Ratsantraege;
        if (!ra.IstAktiv) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Anträge zu Steuer, Bau und Fest", 16);
        Hauptmenue.Text(_inhalt, $"Stadtkasse: {Zahlenformat.Ganz(w.Kasse.Stand)} {waehrung} · Umsatzsteuer {w.Kasse.SteuersatzProzent} % · " +
                                 "Bau und Fest werden bei der Abstimmung aus der Kasse bezahlt.", 13, null, umbruchBreite: 670);
        if (w.Befugnisse.FreierAntragOffen(_spieler!))
            Hauptmenue.Text(_inhalt, "Als Bürgermeister stellt ihr einen Antrag in diesem Jahr ohne Aktionspunkte – er ist noch offen.", 13, Gruen, umbruchBreite: 670);
        else if (w.Befugnisse.HatAmt(_spieler!, Amtsbefugnisse.Buergermeister))
            Hauptmenue.Text(_inhalt, "Euren freien Antrag als Bürgermeister habt ihr in diesem Jahr schon gestellt.", 13, Grau, umbruchBreite: 670);
        if (w.Befugnisse.HatAmt(_spieler!, Amtsbefugnisse.Kaemmerer))
            Hauptmenue.Text(_inhalt, "Als Kämmerer beantragt ihr den Steuersatz ohne Aktionspunkte.", 13, Gruen, umbruchBreite: 670);

        AntragsArt(w, eigenesAmt, Antragsart.Steuersatz, "Steuersatz", () => SteuerWahl(w));
        AntragsArt(w, eigenesAmt, Antragsart.Bauvorhaben, "Bauvorhaben der Stadt", () => BauWahl(w));
        AntragsArt(w, eigenesAmt, Antragsart.Fest, "Fest ausrichten", () => FestWahl(w));

        var k = w.Kasse.Daten;
        Hauptmenue.Text(_inhalt, $"Ratsherren stimmen nach der Kasse: höhere Steuer, solange weniger als ihre Reserve (um {Zahlenformat.Ganz(k.Reserve)} {waehrung}, " +
                                 "je nach Volk) darin liegt, niedrigere, sobald es mehr als das Doppelte ist; Bau und Fest, wenn danach noch die Reserve bleibt. " +
                                 $"Ab {Zahlenformat.Ganz(k.AntragGunst)} Gunst stimmen sie euren Anträgen immer zu.", 12, Grau, umbruchBreite: 670);
    }

    /// <summary>Eine Antragsart: der vorliegende Antrag mit Prognose und Stimme, sonst die Auswahl für einen eigenen Antrag.</summary>
    private void AntragsArt(Stadtwirtschaft w, Amt? eigenesAmt, Antragsart art, string titel, Action auswahl)
    {
        var ra = w.Ratsantraege;
        Hauptmenue.Text(_inhalt, titel, 14, Gold);
        if (ra.Antrag(art) is { } antrag)
        {
            var (ja, nein, enthaltung, bm) = ra.Prognose(antrag);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            Hauptmenue.Text(_inhalt, $"{antrag.Antragsteller.Inhaber} beantragt: {ra.Beschreibung(antrag)}. " +
                                     $"Stand heute: {ja} dafür, {nein} dagegen{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                14, durch ? Gruen : Rot, umbruchBreite: 670);
            AntragStimme(ra.StimmeVerboten(_spieler!, art), ra.StimmeVon(_spieler!, art), ja2 => Befehlen(new RatsantragStimme(art, ja2)));
        }
        else if (eigenesAmt is null)
            Hauptmenue.Text(_inhalt, "Es liegt kein Antrag vor. Anträge stellen nur Mitglieder des Rates.", 13, Grau, umbruchBreite: 670);
        else
            auswahl();
    }

    /// <summary>Prognose eines möglichen Antrags in Kurzform: „heute 4:3, angenommen“.</summary>
    private static string Kurzprognose(Ratsantraege ra, Ratsantrag a)
    {
        var (ja, nein, _, bm) = ra.Prognose(a);
        return $"heute {ja}:{nein}{(Stadtrat.IstAngenommen(ja, nein, bm) ? ", angenommen" : ", abgelehnt")}";
    }

    /// <summary>
    /// Der Knopf „Antrag stellen“ eines möglichen Antrags: gesperrt mit dem Grund des Kerns, sonst mit den AP-Kosten bzw. „ohne AP“
    /// (Bürgermeister: ein Antrag im Jahr, Kämmerer: Steuersatz). Wird bei jeder Auswahl neu eingestellt.
    /// </summary>
    private void AntragKnopfEinstellen(Button knopf, Stadtwirtschaft w, Ratsantrag a, Befehl befehl, string tipp)
    {
        var grund = w.Ratsantraege.BeantragenVerboten(a);
        knopf.Text = "Antrag stellen";
        knopf.Disabled = grund is not null;
        knopf.TooltipText = grund ?? tipp;
        if (w.ApKosten(_spieler!, befehl) == 0 && w.ApKosten(befehl) > 0)
            knopf.Text += " · ohne AP";
        else
            UiHilfe.ApKosten(knopf, w, _spieler!, befehl);
    }

    private void SteuerWahl(Stadtwirtschaft w)
    {
        var ra = w.Ratsantraege;
        var k = w.Kasse.Daten;
        var saetze = Enumerable.Range(k.SteuerMin, k.SteuerMax - k.SteuerMin + 1).Where(p => p != w.Kasse.SteuersatzProzent).ToList();
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(zeile);
        var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
        foreach (var p in saetze)
            auswahl.AddItem($"{p} % ({Kurzprognose(ra, new Ratsantrag(Antragsart.Steuersatz, _spieler!, Prozent: p))})");
        zeile.AddChild(auswahl);
        var stellen = new Button { FocusMode = FocusModeEnum.None };
        zeile.AddChild(stellen);
        void Einstellen() => AntragKnopfEinstellen(stellen, w, new Ratsantrag(Antragsart.Steuersatz, _spieler!, Prozent: saetze[auswahl.Selected]),
            new SteuersatzBeantragen(saetze[auswahl.Selected]), $"Die Umsatzsteuer gilt für alle Verkäufe in der Stadt (bisher {w.Kasse.SteuersatzProzent} %). " +
                                                                 "Der Rat stimmt am Jahresende ab.");
        auswahl.ItemSelected += _ => Einstellen();
        stellen.Pressed += () => Befehlen(new SteuersatzBeantragen(saetze[auswahl.Selected]));
        Einstellen();
    }

    private void BauWahl(Stadtwirtschaft w)
    {
        var ra = w.Ratsantraege;
        var waehrung = w.Daten.Allgemein.Waehrung;
        // Was die Stadt nicht bauen kann (Hafen ohne Schiffshandel), steht gar nicht erst da; Gebautes und Baustellen mit Vermerk
        var liste = ra.Bauvorhaben.Where(b => b.Wirkung != Bauwirkung.Hafen || w.Schiffahrt.IstAktiv).ToList();
        if (liste.Count == 0) return;
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(zeile);
        var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
        foreach (var b in liste)
        {
            var zusatz = ra.IstGebaut(b.Kennung) ? "schon gebaut" : ra.ImBau(b.Kennung) ? "im Bau"
                : Kurzprognose(ra, new Ratsantrag(Antragsart.Bauvorhaben, _spieler!, Bauvorhaben: b.Kennung));
            auswahl.AddItem($"{b.Name} – {Zahlenformat.Ganz(b.Kosten)} {waehrung} ({zusatz})");
        }
        zeile.AddChild(auswahl);
        var stellen = new Button { FocusMode = FocusModeEnum.None };
        zeile.AddChild(stellen);
        var wirkung = Hauptmenue.Text(_inhalt, "", 12, Grau, umbruchBreite: 670);
        void Einstellen()
        {
            var b = liste[auswahl.Selected];
            var text = $"{b.Name}: {b.Beschreibung} Bauzeit {b.BauzeitStunden} Stunden; dem Antragsteller +{Zahlenformat.Ganz(b.Ansehen)} Ansehen für {b.AnsehenJahre} Jahre.";
            wirkung.Text = text;
            AntragKnopfEinstellen(stellen, w, new Ratsantrag(Antragsart.Bauvorhaben, _spieler!, Bauvorhaben: b.Kennung), new BauvorhabenBeantragen(b.Kennung), text);
        }
        auswahl.ItemSelected += _ => Einstellen();
        stellen.Pressed += () => Befehlen(new BauvorhabenBeantragen(liste[auswahl.Selected].Kennung));
        // Vorwahl: das erste Vorhaben, das sich beantragen ließe
        var erstes = liste.FindIndex(b => ra.BeantragenVerboten(new Ratsantrag(Antragsart.Bauvorhaben, _spieler!, Bauvorhaben: b.Kennung)) is null);
        auswahl.Selected = Math.Max(0, erstes);
        Einstellen();
    }

    private void FestWahl(Stadtwirtschaft w)
    {
        var ra = w.Ratsantraege;
        var fest = w.Daten.Politik.Fest;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var antrag = new Ratsantrag(Antragsart.Fest, _spieler!);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(zeile);
        Hauptmenue.Text(zeile, $"Fest im Jahr {w.Jahr + 1} für {Zahlenformat.Ganz(ra.Kosten(antrag))} {waehrung} ({Kurzprognose(ra, antrag)})", 14);
        var stellen = new Button { FocusMode = FocusModeEnum.None };
        zeile.AddChild(stellen);
        AntragKnopfEinstellen(stellen, w, antrag, new FestBeantragen(),
            $"Ein Jahr lang {Zahlenformat.Ganz(fest.Nachfrage * 100)} % mehr Marktbesucher und {Zahlenformat.Ganz(fest.Gaeste * 100)} % mehr Wirtshausgäste; " +
            $"dem Antragsteller +{Zahlenformat.Ganz(fest.Ansehen)} Ansehen.");
        stellen.Pressed += () => Befehlen(new FestBeantragen());
    }
}
