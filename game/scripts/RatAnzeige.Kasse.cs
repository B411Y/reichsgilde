using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer, Reiter „Kasse“: Stand, Steuersatz und Haushalt der Stadt (früher im Reiter „Stadtkasse“ der Rathaus-Tafel), die Bauten und das Fest
/// der Stadt und die Anträge zu Steuer, Bau und Fest (<see cref="KassenAntraegeAbschnitt"/>). Posten einzeln sieht nur der Kämmerer
/// (<see cref="Stadtkasse.HaushaltVerboten"/>), alle anderen die Summen.
/// </summary>
public partial class RatAnzeige
{
    private void KasseSeite(Stadtwirtschaft w)
    {
        var kasse = w.Kasse;
        var waehrung = w.Daten.Allgemein.Waehrung;
        RatStil.Text(_inhalt, $"Stand: {Zahlenformat.Ganz(kasse.Stand)} {waehrung}", "RatAbschnitt").TooltipText =
            "Die Stadtkasse: Einnahmen minus Ausgaben. Ist sie im Minus, kann die Stadt keine Vorhaben bezahlen.";
        RatStil.Text(_inhalt, $"Umsatzsteuer: {kasse.SteuersatzProzent} % auf alle Verkäufe").TooltipText =
            "Den Satz bestimmt der Rat auf Antrag. Er trifft alle Häuser gleich und füllt die Stadtkasse.";
        var verboten = kasse.HaushaltVerboten(_spieler!);
        var raster = RatStil.Container<GridContainer>(_inhalt, "RatRaster");
        raster.Columns = 3;
        void Zeile(string name, string jetzt, string vorher, string variante = "RatText")
        {
            var a1 = RatStil.Text(raster, name, variante);
            var a2 = RatStil.Text(raster, jetzt, variante);
            var a3 = RatStil.Text(raster, vorher, variante);
            a1.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            a2.HorizontalAlignment = a3.HorizontalAlignment = HorizontalAlignment.Right;
            a1.TooltipText = a2.TooltipText = a3.TooltipText = $"{name}: dieses Jahr bisher {jetzt}, im Vorjahr {vorher}.";
        }
        string Betrag(double? x) => x is { } b ? Zahlenformat.Ganz(b) : "–";
        var dj = kasse.DiesesJahr;
        var vj = kasse.Vorjahr;
        Zeile("", $"{dj.Jahr} bisher", vj is null ? "Vorjahr" : vj.Jahr.ToString(), "RatHinweis");
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
        Zeile("Einnahmen", Betrag(dj.Einnahmen), Betrag(vj?.Einnahmen), "RatGut");
        Zeile("Ausgaben", Betrag(dj.Ausgaben), Betrag(vj?.Ausgaben), "RatSchlecht");
        Zeile("Saldo", Betrag(dj.Saldo), Betrag(vj?.Saldo));
        RatStil.Text(_inhalt, verboten is not null ? verboten + " Die übrigen sehen nur die Summen."
            : "Als Kämmerer seht ihr den ganzen Haushalt und beantragt den Steuersatz ohne Aktionspunkte.", "RatHinweis", umbrechen: true);
        RatStil.Text(_inhalt, "Einnahmen: Umsatzsteuer, Glaubenszins, Bußen, Gebühren und der Hafenzoll. Ausgaben: Amtsgehälter, Bauvorhaben und Feste. " +
                              "Eine leere Kasse hat keine Folgen – nur Bau und Fest brauchen Deckung.", "RatHinweis", umbrechen: true);

        // ── Bauten und Fest der Stadt ──
        var ra = w.Ratsantraege;
        if (ra.IstAktiv)
        {
            _inhalt.AddChild(new HSeparator());
            RatStil.Text(_inhalt, "Bauten der Stadt", "RatAbschnitt");
            var minute = w.Uhr.VergangeneMinuten;
            foreach (var b in ra.Baustellen)
            {
                var rest = Math.Max(0, b.FertigAb - minute);
                RatStil.Text(_inhalt, $"Im Bau: {b.Name} – noch etwa {Math.Ceiling(rest / 60.0)} Stunden{(b.Antragsteller is { } a ? $" (beantragt von {a.Inhaber})" : "")}");
            }
            foreach (var k in ra.Gebaut)
                RatStil.Text(_inhalt, $"Fertig: {ra.BauvorhabenNamens(k)?.Name ?? k}", "RatGut");
            if (ra.Baustellen.Count == 0 && ra.Gebaut.Count == 0)
                RatStil.Text(_inhalt, "Die Stadt hat noch nichts gebaut.", "RatHinweis");
            if (ra.FestLaeuft)
                RatStil.Text(_inhalt, "In diesem Jahr feiert die Stadt: mehr Marktbesucher und Wirtshausgäste.", "RatGut");
            else if (ra.FestJahr is { } jahr && jahr > w.Jahr)
                RatStil.Text(_inhalt, $"Im Jahr {jahr} feiert die Stadt.");
        }

        KassenAntraegeAbschnitt(w, w.Rat.AmtVon(_spieler!)); // Steuer, Bau, Fest (RatAnzeige.Antraege.cs)
    }
}
