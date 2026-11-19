using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Kapitel „Die Häuser“ des Schwarzen Buchs: die Doppelseite eines Hauses. Links, was man über das Haus weiß und was man seinem Besitz antun
/// kann (Steckbrief, Betriebe, Wohnhaus und Umland), rechts, was die Person und ihren Ruf trifft (Oberhaupt, Ruf und Rat, Erpressung).
/// Dieselben Rechnungen und Befehle wie bisher – nur je Haus eine Seite statt aller Häuser untereinander.
/// </summary>
public partial class GaunerAnzeige
{
    private void HausSeiten(VBoxContainer links, VBoxContainer rechts, List<Konto> haeuser, Konto? haus, Betrieb? versteck)
    {
        if (haus is null)
        {
            HinterzimmerStil.Text(links, "Die Häuser", "HzSeitentitel");
            HinterzimmerStil.Text(links, "Kein anderes Haus ist mehr im Spiel.", "HzKlein", "tat_breite");
            return;
        }
        var w = _w;
        var unterwelt = w.Gauner;
        var g = unterwelt.Daten;
        var gericht = w.Justiz;
        var diebe = w.Diebe;
        var inhaber = haus.Inhaber;
        var bestellt = unterwelt.Auftraege.Where(a => a.Auftraggeber == _spieler && a.Ziel == haus).ToList();
        var fremd = w.Familien.Von(haus);
        var beweise = gericht.IstAktiv ? gericht.Beweise(_spieler!, haus).Count() : 0;

        // ── Links: Steckbrief ──
        HinterzimmerStil.Text(links, inhaber, "HzSeitentitel");
        var steck = HinterzimmerStil.Container<HBoxContainer>(links, "HzZeile");
        var wappen = WappenBild.Neu(haus.Wappen, HinterzimmerStil.Mass("wappen"));
        wappen.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        steck.AddChild(wappen);
        var daten = HinterzimmerStil.Container<GridContainer>(steck, "HzSteckbrief");
        daten.Columns = 2;
        void Angabe(string was, string wert, string tipp = "")
        {
            HinterzimmerStil.Text(daten, was, "HzKlein");
            HinterzimmerStil.Text(daten, wert, "HzText", tipp: tipp);
        }
        if (fremd is not null)
            Angabe("Oberhaupt", $"{fremd.Oberhaupt.OberhauptTitel} {fremd.Oberhaupt.Vorname}{(fremd.Oberhaupt.Krankheit is { } k ? $" (krank: {k.Name})" : "")}");
        Angabe("Stand", w.Rat.IstAktiv ? $"{w.StandVon(haus).Name} · Ansehen {Zahlenformat.Ganz(w.Rat.Ansehen(haus))}" : w.StandVon(haus).Name);
        Angabe("Vermögen", Gulden(haus.Reinvermoegen), "Reinvermögen des Hauses.");
        if (gericht.IstAktiv)
            Angabe("Beweise", $"{beweise} von {g.ErpressungMindestBeweise}", "Eure Beweise gegen dieses Haus – Grundlage für Erpressung und Anklage.");

        // ── Links: Betriebe ──
        foreach (var b in w.Betriebe.Where(b => b.Konto == haus))
        {
            var raster = Gruppe(links, $"Betrieb · {b.Name}{(b.Waechter ? " (Wächter)" : "")}",
                b.Waechter ? "Ein Wächter steht hier: geringere Chance, eher ein Zeuge." : "");
            var name = b.Name;
            var beute = unterwelt.MoeglicheBeute(b);
            Tat(raster, "Diebstahl", beute.Count == 0 ? "Im Lager liegt nichts zu holen."
                    : $"Beute: {string.Join(", ", beute.Select(x => $"{x.Stueck} {x.Ware}"))} (Hehler ≈ {Zahlenformat.Ganz(beute.Sum(x => x.Wert) * g.HehlerAnteil)})",
                new Beauftragen("", Gaunerstueck.Diebstahl), () => Befehlen(new Beauftragen(inhaber, Gaunerstueck.Diebstahl, name)),
                chance: unterwelt.Erfolgschance(Gaunerstueck.Diebstahl, b, _spieler), preis: Zahlenformat.Ganz(g.Diebstahl.Kosten),
                bestellt: bestellt.Any(a => a.Art == Gaunerstueck.Diebstahl && a.Betrieb == name) ? "Für heute Nacht bestellt." : null,
                tipp: "Die Bande holt die wertvollste Ware aus dem Lager, der Hehler zahlt euch seinen Anteil.");
            Tat(raster, "Sabotage", $"Schaden beim Gegner ≈ {Zahlenformat.Ganz(unterwelt.Sabotageschaden(b))}",
                new Beauftragen("", Gaunerstueck.Sabotage), () => Befehlen(new Beauftragen(inhaber, Gaunerstueck.Sabotage, name)),
                chance: unterwelt.Erfolgschance(Gaunerstueck.Sabotage, b, _spieler), preis: Zahlenformat.Ganz(g.Sabotage.Kosten),
                bestellt: bestellt.Any(a => a.Art == Gaunerstueck.Sabotage && a.Betrieb == name) ? "Für heute Nacht bestellt." : null,
                tipp: "Feuer in der Werkstatt: Stillstand und Reparaturkosten.");
            if (versteck is not null)
                Tat(raster, "Einbruch (eigene Leute)", $"Beute ≈ {Zahlenformat.Ganz(diebe.MoeglicheBeute(b, _spieler!).Sum(x => x.Wert))} ins eigene Lager",
                    new Einbrechen("", ""), () => Befehlen(new Einbrechen(inhaber, name)),
                    chance: diebe.Einbruchchance(b, _spieler!), preis: "frei",
                    bestellt: diebe.Auftraege.Any(a => a.Dieb == _spieler && a.Art == Diebesstueck.Einbruch && a.Betrieb == name) ? "Eure Leute steigen heute Nacht ein." : null,
                    grund: diebe.Verboten(_spieler!, Diebesstueck.Einbruch),
                    tipp: "Eure eigenen Diebe steigen ein: kein Hehler, keine Bande, kein Geld – nur Aktionspunkte.");
        }

        // ── Links: Wohnhaus und Umland ──
        if (unterwelt.GegenWohnhaus && w.Wohnen.Stufe(haus) is { } hausStufe)
        {
            var beschaedigt = w.Wohnen.IstBeschaedigt(haus);
            var raster = Gruppe(links, $"Wohnhaus · {hausStufe.Name}{(haus.Hauswaechter ? " (Hauswächter)" : "")}{(beschaedigt ? " – abgebrannt" : "")}",
                "Das Wohnhaus als Ziel: Einbruch und Brandstiftung sind möglich, ein Hauswächter senkt die Chance.");
            var wh = g.Wohnhaus!;
            Tat(raster, "Einbruch", $"Bargeld aus der Truhe, Beute ≈ {Zahlenformat.Ganz(unterwelt.Hausbeute(haus))}",
                new Beauftragen("", Gaunerstueck.Hauseinbruch), () => Befehlen(new Beauftragen(inhaber, Gaunerstueck.Hauseinbruch)),
                chance: unterwelt.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, haus, _spieler), preis: Zahlenformat.Ganz(wh.Einbruch.Kosten),
                bestellt: bestellt.Any(a => a.Art == Gaunerstueck.Hauseinbruch) ? "Für heute Nacht bestellt." : null,
                tipp: "Die Bande holt Bargeld aus der Truhe – kein Hehler nötig.");
            Tat(raster, "Brand", $"Reparatur ≈ {Zahlenformat.Ganz(unterwelt.Brandschaden(haus))}" +
                                 (hausStufe.Ansehen > 0 ? $", −{Zahlenformat.Ganz(hausStufe.Ansehen)} Ansehen für einen Tag" : ""),
                new Beauftragen("", Gaunerstueck.Brandstiftung), () => Befehlen(new Beauftragen(inhaber, Gaunerstueck.Brandstiftung)),
                chance: unterwelt.ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, haus, _spieler), preis: Zahlenformat.Ganz(wh.Brand.Kosten),
                bestellt: bestellt.Any(a => a.Art == Gaunerstueck.Brandstiftung) ? "Für heute Nacht bestellt." : null,
                grund: beschaedigt ? "Das Haus ist vom letzten Brand noch nicht instand gesetzt." : null,
                tipp: "Feuer am Wohnhaus: Reparatur, und bis zur Instandsetzung fehlt dem Haus das Ansehen. Die Stufe bleibt.");
        }
        {
            var raster = Gruppe(links, "Karren im Umland", "Karren im Umland können Wegelagerer überfallen.");
            var lauert = unterwelt.Hinterhalte.Any(h => h.Auftraggeber == _spieler && h.Ziel == haus);
            Tat(raster, "Überfall auf Karren", $"Wegelagerer, Stärke {Zahlenformat.Kommazahl(unterwelt.Staerke(_spieler))}, lauern einer Fahrt auf.",
                new Beauftragen("", Gaunerstueck.Ueberfall), () => Befehlen(new Beauftragen(inhaber, Gaunerstueck.Ueberfall)),
                preis: Zahlenformat.Ganz(g.Ueberfall.Kosten), schwer: true,
                bestellt: lauert ? "Eure Wegelagerer liegen auf der Lauer." : bestellt.Any(a => a.Art == Gaunerstueck.Ueberfall) ? "Für heute Nacht bestellt." : null,
                bestelltKurz: lauert ? "lauern" : "bestellt",
                grund: unterwelt.HatUmlandfahrten(haus) ? null : "Das Haus hat keine Karren im Umland.",
                tipp: "Die Wegelagerer lauern bis zur nächsten Nacht der nächsten beladenen Fahrt auf. Erfolg = Stärke × (1 − Schutz der Eskorte). Ein Überfall ist ein schweres Vergehen.");
        }
        Blaetterknopf(links, haeuser, haus, -1);

        // ── Rechts: Oberhaupt ──
        if (w.Gesundheit.IstAktiv && fremd is not null)
        {
            var o = fremd.Oberhaupt;
            var raster = Gruppe(rechts, $"Oberhaupt · {o.OberhauptTitel} {o.Vorname}{(o.Krankheit is { } k ? $" (krank: {k.Name})" : "")}");
            Tat(raster, "Krötenschleim", "Ins Essen gemischt: Das Oberhaupt erkrankt.",
                new Beauftragen("", Gaunerstueck.Kroetenschleim), () => Befehlen(new Beauftragen(inhaber, Gaunerstueck.Kroetenschleim)),
                chance: unterwelt.Erfolgschance(Gaunerstueck.Kroetenschleim, null, _spieler), preis: Zahlenformat.Ganz(unterwelt.Kosten(Gaunerstueck.Kroetenschleim, _spieler)),
                bestellt: bestellt.Any(a => a.Art == Gaunerstueck.Kroetenschleim) ? "Für heute Nacht bestellt." : null,
                tipp: "Das Oberhaupt erkrankt (Ruhr, Lungenentzündung, Haarausfall …). Wächter helfen nicht; wer schon krank ist, merkt nichts. Vergehen: Giftmischerei." +
                      (unterwelt.MischtSelbst(_spieler) ? " Ihr mischt selbst – darum der günstigere Preis." : ""));

            // Giftdolche des Parfumeurs – nur Gegengift heilt
            if (w.Duft.IstAktiv)
            {
                var duft = w.Duft;
                var geplant = duft.Auftraege.FirstOrDefault(a => a.Auftraggeber == _spieler && a.Ziel == haus);
                foreach (var (dolch, dolchDaten) in duft.Daten.Gift.Dolche.OrderBy(x => x.Value.Chance))
                {
                    if (geplant is not null && geplant.Dolch != dolch) continue; // geplant ist geplant: nur dieser Dolch steht noch da
                    var krankheit = w.Gesundheit.Daten.Krankheiten.GetValueOrDefault(dolchDaten.Krankheit);
                    Tat(raster, dolch, $"{dolchDaten.Krankheit}, {Prozent(krankheit?.Sterben ?? 0)} tödlich",
                        new Giftanschlag("", ""), () => Befehlen(new Giftanschlag(inhaber, dolch)),
                        chance: duft.Giftchance(_spieler!, dolch), preis: Preis(duft.Preis(_spieler!, dolch)), schwer: true,
                        bestellt: geplant is not null ? "Giftanschlag für heute Nacht geplant." : null, bestelltKurz: "geplant",
                        grund: geplant is null ? duft.GiftVerboten(_spieler!, haus, dolch) : null,
                        tipp: "Ein Giftdolch gegen das Oberhaupt: Die Krankheit kann am Jahresende töten, nur Gegengift heilt. Schwerste Tat. Vergehen: Giftanschlag – vor Gericht droht Kerker.");
                }
            }
        }

        // ── Rechts: Ruf und Rat ──
        var hetzen = w.Kirchenwesen is { IstAktiv: true };
        var zwietracht = w.Duft.IstAktiv && w.Rat.IstAktiv && w.Rat.Ratsherren.Count > 0;
        if (hetzen || zwietracht)
        {
            var raster = Gruppe(rechts, "Ruf und Rat");
            if (hetzen)
            {
                var kw = w.Kirchenwesen!;
                var hetze = kw.Daten.Hetzschrift;
                var preis = kw.Schriftpreis(_spieler!, hetze.Ware);
                Tat(raster, "Hetzschrift", $"{Zahlenformat.Ganz(hetze.Ansehen)} Ansehen weniger bis Ende nächsten Jahres.",
                    new HetzschriftVerbreiten(""), () => Befehlen(new HetzschriftVerbreiten(inhaber)), preis: Preis(preis),
                    bestellt: kw.GehetztBis(haus) is int bis ? $"Eine Hetzschrift geht um (bis Ende {bis})." : null, bestelltKurz: "geht um",
                    grund: _spieler!.Vermoegen < preis ? $"Die Hetzschrift kostet {Gulden(preis)}." : null,
                    tipp: "Weniger Ansehen zählt bei Wahl, Brautschau und Kirche. Vergehen: Verleumdung.");
            }
            if (zwietracht)
            {
                var duft = w.Duft;
                var ratsherren = w.Rat.Ratsherren;
                var wahl = new OptionButton { FocusMode = FocusModeEnum.None, FitToLongestItem = false, ClipText = true, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                wahl.CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("tat_breite"), 0);
                foreach (var r in ratsherren) wahl.AddItem($"{r.Name} (Meinung {Zahlenformat.MitVorzeichen(w.Rat.Meinung(r, haus))})");
                var gewaehlt = Math.Max(0, ratsherren.ToList().FindIndex(r => r.Name == _ratsherr.GetValueOrDefault(inhaber)));
                wahl.Select(gewaehlt);
                wahl.ItemSelected += i => _ratsherr[inhaber] = ratsherren[(int)i].Name;
                wahl.TooltipText = "Bei welchem Ratsherrn die Blume wirken soll – dahinter seine Meinung über dieses Haus.";
                Tat(raster, duft.Daten.Zwietracht.Ware, $"Meinung des Ratsherrn sinkt um {Zahlenformat.Ganz(duft.Daten.Zwietracht.Gunst)}.",
                    new ZwietrachtSaeen("", ""), () => Befehlen(new ZwietrachtSaeen(ratsherren[Math.Max(0, wahl.Selected)].Name, inhaber)),
                    preis: Preis(duft.Preis(_spieler!, duft.Daten.Zwietracht.Ware)), feld: wahl, knopf: "Säen",
                    grund: duft.ZwietrachtVerboten(_spieler!, ratsherren[gewaehlt].Name, haus),
                    tipp: "Der gewählte Ratsherr denkt schlechter über dieses Haus. Die Meinung zählt bei Wahl, Anträgen und Gericht; sie verblasst wie alle Taten. Vergehen: Intrige.");
            }
        }

        // ── Rechts: Erpressung ──
        if (gericht.IstAktiv)
        {
            var raster = Gruppe(rechts, $"Erpressung · {beweise} von {g.ErpressungMindestBeweise} Beweisen",
                "Mit genug Beweisen könnt ihr Schweigegeld verlangen. Beweise sammeln eure Zeugen, eure Diebe und die Briefe der anderen.");
            var busse = gericht.DrohendeBusse(_spieler!, haus);
            var vorschlag = Math.Round(busse * 0.6 / 50) * 50;
            var offen = unterwelt.ForderungAn(haus, _spieler!);
            Tat(raster, "Erpresserbrief", $"Dem Haus drohen {Gulden(busse)} Buße.",
                new Erpressen("", 0), () => Befehlen(new Erpressen(inhaber, _schweigegeld.GetValueOrDefault(inhaber, vorschlag))),
                feld: Betragsfeld(_schweigegeld, inhaber, vorschlag, 50, 50_000, 50), knopf: "Schicken",
                bestellt: offen is not null ? $"Brief über {Gulden(offen.Betrag)} zugestellt – wartet auf Antwort." : null, bestelltKurz: "zugestellt",
                grund: beweise < g.ErpressungMindestBeweise ? $"Erst ab {g.ErpressungMindestBeweise} Beweisen – ihr habt {beweise}." : null,
                tipp: "So viel Buße kostete das Haus heute ein Schuldspruch – wer weniger verlangt, wird eher bezahlt. Der Brief selbst ist ein Beweis gegen euch. Zahlt das Haus, verbrennen eure Beweise; zahlt es nicht, könnt ihr klagen.");
        }
        Blaetterknopf(rechts, haeuser, haus, +1);
    }

    /// <summary>Am Fuß der Seite: zum vorigen (links) bzw. nächsten Haus (rechts) blättern. Am Rand des Registers steht nichts.</summary>
    private void Blaetterknopf(VBoxContainer seite, List<Konto> haeuser, Konto haus, int richtung)
    {
        var nummer = haeuser.IndexOf(haus) + richtung;
        seite.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        if (nummer < 0 || nummer >= haeuser.Count) return;
        var inhaber = haeuser[nummer].Inhaber;
        var knopf = HinterzimmerStil.Knopf(seite, richtung < 0 ? $"‹ {inhaber}" : $"{inhaber} ›", () => Blaettern(inhaber), "HzBlaettern", "Zur Doppelseite dieses Hauses blättern.");
        knopf.SizeFlagsHorizontal = richtung < 0 ? SizeFlags.ShrinkBegin : SizeFlags.ShrinkEnd;
    }
}
