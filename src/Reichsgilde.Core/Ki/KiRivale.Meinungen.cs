using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Meinungen (Charaktersystem Teil 2, PR #231) in den Entscheidungen der KI-Häuser:
///
///   Rat        Vor einem Geschenk an einen Ratsherrn, dessen Stimme sie braucht (Wahl, eigener Antrag, Gericht), unternimmt die KI eine
///              Aktivität mit ihm: zuerst eine, die ihr Oberhaupt selbst mag (Gesellig zecht, Fromm geht zur Messe, Klug spielt Brettspiel,
///              Mutig jagt, Hochmütig lädt zum Gastmahl), sonst die mit der besten erwarteten Wirkung – nie eine, die ihr Oberhaupt
///              nicht mag (Scheu zecht nicht, Feige jagt nicht). Was danach noch fehlt, schließt ein Geschenk. Reichen die Aktionspunkte
///              dafür nicht, plant sie wie früher nur mit Geschenken.
///   Häuser     Über wen ihr Oberhaupt gut denkt (ab <c>kiVerschontAb</c>), den verschont sie: keine Gaunerstücke, keine Diebe, kein Spitzel,
///              Klage erst mit einem Beweis mehr. Über wen es schlecht denkt (bis <c>kiVerhasstAb</c>), den nimmt sie sich vor, auch wenn er
///              hinter ihr liegt, und klagt schon mit einem Beweis weniger. Bei Wahlen wirkt ein geliebtes Haus weniger gefährlich.
/// Ohne Abschnitt "meinungen" ist alles wie vorher.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Was das eigene Oberhaupt über das Oberhaupt dieses Hauses denkt (0 ohne Meinungen).</summary>
    public double MeinungUeber(Konto anderes) => _w.Meinungen.IstAktiv && anderes != Konto ? _w.Meinungen.Meinung(Konto, anderes) : 0;

    private bool Verschont(Konto anderes) =>
        _w.Meinungen.Daten is { } d && anderes != Konto && MeinungUeber(anderes) >= d.Wirkung.KiVerschontAb;

    private bool Verhasst(Konto anderes) =>
        _w.Meinungen.Daten is { } d && anderes != Konto && MeinungUeber(anderes) <= d.Wirkung.KiVerhasstAb;

    /// <summary>Klageschwelle gegen dieses Haus: verhasst ein Beweis weniger, verschont einer mehr – nie unter dem, was ein Schuldspruch braucht.</summary>
    private double KlageschwelleGegen(Konto gegner)
    {
        var d = _w.Justiz.Daten;
        var mindestens = Math.Ceiling(d.SchuldSchwelle / d.BeweiskraftJeBeweis - 1e-9) * d.BeweiskraftJeBeweis;
        var mehr = Verhasst(gegner) ? -1 : Verschont(gegner) ? 1 : 0;
        return Math.Max(mindestens, Klageschwelle + mehr * d.BeweiskraftJeBeweis);
    }

    /// <summary>Wie die KI einen Ratsherrn umstimmen will: vielleicht eine Aktivität, dann ein Geschenk für den Rest.</summary>
    /// <param name="Fehlt">So viel Meinung (bzw. Wahlpunkte) fehlt.</param>
    /// <param name="Aktivitaet">Art der Aktivität vorher (null = nur ein Geschenk).</param>
    /// <param name="Geschenk">Gulden des Geschenks nach der erwarteten Wirkung der Aktivität (0 = keins nötig).</param>
    private sealed record Werbung(Ratsherr Ratsherr, double Fehlt, string? Aktivitaet, double AktivitaetGulden, double Erwartet, double Geschenk, int Ap)
    {
        public double Gulden => AktivitaetGulden + Geschenk;
    }

    /// <summary>Gulden für so viel Meinung bei diesem Ratsherrn – auf 10 aufgerundet, wie die KI schenkt.</summary>
    private double GeschenkFuer(Ratsherr r, double fehlt) =>
        fehlt <= 0 ? 0 : Math.Ceiling(fehlt / _w.Rat.GunstJeGulden(r, Konto) / 10) * 10;

    /// <summary>
    /// Für jeden Ratsherrn, dessen Stimme fehlt, die günstigste Werbung. <paramref name="mitAktivitaeten"/>: erst eine Aktivität, die das
    /// eigene Oberhaupt mag (sonst die beste), dann ein Geschenk für den Rest.
    /// </summary>
    private List<Werbung> Werbungen(IEnumerable<(Ratsherr Ratsherr, double Fehlt)> fehlt, bool mitAktivitaeten)
    {
        var apGeschenk = _w.ApKosten(new Schenken("", 0));
        var liste = new List<Werbung>();
        foreach (var (r, wieviel) in fehlt)
        {
            var a = mitAktivitaeten ? BesteAktivitaet(r) : null;
            if (a is null)
            {
                liste.Add(new Werbung(r, wieviel, null, 0, 0, GeschenkFuer(r, wieviel), apGeschenk));
                continue;
            }
            var geschenk = GeschenkFuer(r, wieviel - a.Erwartet);
            liste.Add(new Werbung(r, wieviel, a.Art, a.Gulden, a.Erwartet, geschenk, a.Ap + (geschenk > 0 ? apGeschenk : 0)));
        }
        return liste;
    }

    /// <summary>Die Aktivität für diesen Ratsherrn: frei, im Mittel ein Gewinn, nicht gegen die Züge des eigenen Oberhaupts; was es mag, zuerst.</summary>
    private Aktivitaetsangebot? BesteAktivitaet(Ratsherr r)
    {
        var mw = _w.Meinungen;
        if (!mw.IstAktiv || mw.Oberhaupt(Konto) is not { } ich) return null;
        bool Mag(string art, bool nicht) => mw.Daten!.Aktivitaeten[art] is var a && (nicht ? a.MagNicht : a.Mag).Any(ich.HatZug);
        return mw.Aktivitaeten(Konto, r)
            .Where(a => a.Art != mw.GeschenkArt && a.Sperrgrund is null && a.Erwartet > 0 && !Mag(a.Art, nicht: true)
                        && Konto.Vermoegen - a.Gulden >= Profil.Reserve)
            .OrderByDescending(a => Mag(a.Art, nicht: false))
            .ThenByDescending(a => a.Erwartet)
            .ThenBy(a => a.Gulden)
            .ThenBy(a => a.Art, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Eine Werbung ausführen: erst die Aktivität, dann so viel Geschenk, wie danach wirklich noch fehlt.</summary>
    private void Werben(Werbung w, string zweck)
    {
        var r = w.Ratsherr;
        var fehlt = w.Fehlt;
        if (w.Aktivitaet is { } art)
        {
            var vorher = _w.Rat.Meinung(r, Konto);
            var a = _w.Meinungen.Daten!.Aktivitaeten[art];
            if (Befehlen(new Aktivitaet(art, r.Kennung), $"{a.Name} mit {r.Name} {zweck}."))
                fehlt -= _w.Rat.Meinung(r, Konto) - vorher;
        }
        // Nie mehr als ein Geschenk allein gekostet hätte – auch nach einem Unglück bei der Aktivität.
        var gulden = w.Aktivitaet is null ? w.Geschenk : Math.Min(GeschenkFuer(r, fehlt), GeschenkFuer(r, w.Fehlt));
        if (gulden > 0)
            Befehlen(new Schenken(r.Name, gulden), $"beschenkt {r.Name} mit {Zahlenformat.Ganz(gulden)} Gulden {zweck}.");
    }
}
