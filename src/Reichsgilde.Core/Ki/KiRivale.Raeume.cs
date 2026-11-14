using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

public sealed partial class KiRivale
{
    /// <summary>
    /// Räume im Wohnhaus (Datenversion 87, Andockstelle des Kerns – einfache Regel, Nutzer 14.11.2026, später änderbar): Zum Jahresende belegt die KI
    /// einen freien Platz, wenn das Geld über der Reserve reicht. Vorrang: Dieb oder Neigung zur Gaunerei → Hinterzimmer; wer Ratsstimmen wirbt
    /// (Politikanteil über 0) → Festsaal; sonst Bibliothek. Danach die übrigen wählbaren Räume in der Reihenfolge der Daten.
    /// </summary>
    private void RaumAnbauen()
    {
        var wohnen = _w.Wohnen;
        if (!wohnen.HatRaumregeln || InNot || wohnen.FreiePlaetze(Konto) == 0) return;
        var waehlbar = wohnen.WaehlbareRaeume(Konto);
        if (waehlbar.Count == 0) return;
        var dieb = _w.Diebe.IstAktiv && _w.Betriebe.Any(b => b.Konto == Konto && _w.Diebe.IstDiebesbetrieb(b));
        var vorrang = new List<string>();
        if (dieb || Profil.Gaunerei >= 1) vorrang.Add("Hinterzimmer");
        if (Profil.PolitikAnteil > 0) vorrang.Add("Festsaal");
        vorrang.Add("Bibliothek");
        var raum = vorrang.Concat(waehlbar).FirstOrDefault(r => waehlbar.Contains(r) && wohnen.RaumAnbauenVerboten(Konto, r) is null);
        if (raum is null) return;
        var preis = _w.Baupreis(Konto, wohnen.Daten!.Raeume!.Art(raum)!.Preis);
        if (Konto.Vermoegen - preis < Profil.Reserve) return;
        Befehlen(new Wirtschaft.RaumAnbauen(raum),
            $"baut am Wohnhaus an: {wohnen.Raumname(raum)} ({Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung}).");
    }
}
