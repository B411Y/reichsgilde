using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Was der Kern über das Leben der Stadt weiß und die Umgebungsklänge brauchen – ohne Godot, damit es sich mit einer echten Partie prüfen lässt
/// (<c>KlangUmgebungTests</c>). Die Stadtansicht (Stadtansicht.Klang.cs) setzt die Orte hinzu. Es wird nur gelesen:
/// <list type="bullet">
/// <item><b>Schänken</b>: Betriebe mit <see cref="Betrieb.IstSchaenke"/>, die zu dieser Uhrzeit geöffnet sind (<see cref="Betrieb.IstArbeitszeit"/>, Wirtshaus 8–22 Uhr),
/// nicht im Umbau, mit der Zahl der Einwohner, die gerade drinnen sitzen (<see cref="Einwohnerschaft.Am"/>).</item>
/// <item><b>Laute Werkstätten</b>: Schmied und Steinmetz in der Stadt, die gerade arbeiten – Arbeitszeit, mindestens ein Arbeiter, kein Umbau,
/// keine Sabotage, kein Stillstand (fehlende Zutat, volles Lager). Tischler, Alchimist und die anderen sind leise; für sie fehlt auch die Aufnahme.</item>
/// </list>
/// </summary>
public static class KlangStadtlebenKern
{
    /// <summary>Berufe, deren Werkstatt beim Arbeiten weit hörbar hämmert (die Aufnahme ist eine Schmiede; Steinmetze meißeln ähnlich).</summary>
    public static readonly HashSet<string> LaermBerufe = new() { "Schmied", "Steinmetz" };

    /// <summary>Geöffnete Schänken mit mindestens einem Gast: (Betrieb, Gäste).</summary>
    public static List<(Betrieb Betrieb, int Gaeste)> OffeneSchaenken(Stadtwirtschaft w, int uhrzeitInMinuten)
    {
        var gaeste = new Dictionary<string, int>();
        if (w.Einwohnerschaft is { IstAktiv: true } ew)
            foreach (var e in ew.Am(Stadtort.Schaenke))
                if (e.Betrieb is { } name) gaeste[name] = gaeste.GetValueOrDefault(name) + 1;
        var liste = new List<(Betrieb, int)>();
        foreach (var b in w.Betriebe)
            if (b.IstSchaenke && b.InDerStadt && b.IstArbeitszeit(uhrzeitInMinuten) && b.UmbauMinuten == 0 && gaeste.TryGetValue(b.Name, out var n) && n > 0)
                liste.Add((b, n));
        return liste;
    }

    /// <summary>Betriebe, die gerade laut arbeiten: (Betrieb, Arbeiter).</summary>
    public static List<(Betrieb Betrieb, int Arbeiter)> ArbeitendeWerkstaetten(Stadtwirtschaft w, int uhrzeitInMinuten)
    {
        var liste = new List<(Betrieb, int)>();
        foreach (var b in w.Betriebe)
            if (b.InDerStadt && !b.OhneProduktion && LaermBerufe.Contains(b.Artdaten.Beruf) && b.Belegt > 0 && b.UmbauMinuten == 0
                && b.SabotageMinuten == 0 && b.Stillstand is null && b.IstArbeitszeit(uhrzeitInMinuten))
                liste.Add((b, b.Belegt));
        return liste;
    }
}
