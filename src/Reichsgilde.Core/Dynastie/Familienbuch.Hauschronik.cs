using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Ein gespeicherter Eintrag der dauerhaften Hauschronik: Art, Spielminute, Person und ein kurzer Satz (höchstens
/// <see cref="Familienbuch.ChronikTextLaenge"/> Zeichen). <paramref name="Vertraulich"/>: nur das Haus selbst sieht ihn (etwa eine Entscheidung
/// gegen die Unterwelt oder ein Ehebruch, der im Haus bleibt).
/// </summary>
public sealed record Hauschronikeintrag(Chronikart Art, long Minute, int? Person, string Text, bool Vertraulich)
{
    public int Jahr => new Spielzeit(Minute).Jahr;
}

public sealed partial class Familienbuch
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Dauerhafte Hauschronik (nur das Wichtigste, mit dem Spielstand gespeichert)
    // ═════════════════════════════════════════════════════════════════════════
    //
    // Geburt, Tod, Erbfolge und Gründung liest die Chronik aus den Personen (die bleiben für immer im Familienbuch). Gespeichert wird nur,
    // was sich daraus nicht ablesen lässt: Heirat, Amt erhalten oder verloren, Standesaufstieg, Geheimnis entdeckt oder bloßgestellt,
    // Brand, Entscheidungen mit Folgen. Unwichtiges (Lager, Kasse, Ratsprotokoll, Gerede) kommt nie hinein.

    /// <summary>Höchstens so viele Zeichen hat ein Satz der Chronik; Längeres wird mit „…“ gekürzt.</summary>
    public const int ChronikTextLaenge = 160;

    /// <summary>Höchstens so viele gespeicherte Einträge je Haus (<c>dynastie.chronikObergrenze</c>).</summary>
    public int ChronikObergrenze => _d.ChronikObergrenze;

    /// <summary>Wie wichtig ein Eintrag ist, wenn die Chronik voll ist: Zuerst fällt der älteste Eintrag der niedrigsten Stufe heraus.</summary>
    public static int ChronikWichtigkeit(Chronikart art) => art switch
    {
        Chronikart.Gruendung or Chronikart.Geburt or Chronikart.Tod or Chronikart.Erbe or Chronikart.Heirat or Chronikart.Amt => 3,
        Chronikart.Geheimnis => 2,
        Chronikart.Brand or Chronikart.Entscheidung => 1,
        _ => 0,
    };

    /// <summary>Trägt ein Ereignis in die Chronik des Hauses ein (nichts, wenn das Haus keine Familie hat). Die Minute ist die der Uhr.</summary>
    internal void ChronikEintragen(Konto konto, Chronikart art, string text, Person? person = null, bool vertraulich = false)
    {
        if (!IstAktiv || Von(konto) is not { } f) return;
        f.ChronikListe.Add(new Hauschronikeintrag(art, _w.Uhr.VergangeneMinuten, person?.Nummer, Kuerzen(text), vertraulich));
        ChronikBegrenzen(f);
    }

    /// <summary>Ein Amt im Rat geht an das Haus.</summary>
    internal void AmtErhalten(Konto konto, string amt)
    {
        if (Von(konto) is { } f) ChronikEintragen(konto, Chronikart.Amt, $"{f.Oberhaupt.Name} wird {amt}.", f.Oberhaupt);
    }

    /// <summary>Das Haus verliert ein Amt im Rat (abgewählt, Bankrott, Tod des Oberhaupts).</summary>
    internal void AmtVerloren(Konto konto, string amt, string wie)
    {
        if (Von(konto) is { } f) ChronikEintragen(konto, Chronikart.Amt, $"Das Haus {f.Nachname} verliert das Amt {amt} ({wie}).");
    }

    /// <summary>
    /// Aus jeder Meldung: ein Feuer, das das Haus wirklich trifft (Wichtigkeit ab „Hoch“ – ein vereitelter Anschlag meldet „Normal“).
    /// Alles andere tragen die Stellen selbst ein, an denen es geschieht.
    /// </summary>
    internal void ChronikNachMeldung(Meldung m)
    {
        if (m.Art != Meldungsart.Feuer || m.Wichtigkeit < Meldungswichtigkeit.Hoch || _w.HausVon(m) is not { } konto || Von(konto) is not { } f) return;
        var wo = m.Ort == Meldungsort.Betrieb && m.Ortsname is { } name ? $"Feuer in {name}." : $"Feuer im Haus {f.Nachname}.";
        ChronikEintragen(konto, Chronikart.Brand, wo, _w.PersonVon(m), m.Vertraulich);
    }

    /// <summary>Bis auf die Obergrenze kürzen: der älteste Eintrag der niedrigsten Wichtigkeit zuerst (feste Reihenfolge, auf jedem Rechner gleich).</summary>
    private void ChronikBegrenzen(Familie f)
    {
        var liste = f.ChronikListe;
        while (liste.Count > Math.Max(1, ChronikObergrenze))
        {
            var niedrigste = liste.Min(e => ChronikWichtigkeit(e.Art));
            liste.RemoveAt(liste.FindIndex(e => ChronikWichtigkeit(e.Art) == niedrigste));
        }
    }

    private static string Kuerzen(string text)
    {
        text = text.Trim();
        return text.Length <= ChronikTextLaenge ? text : text[..(ChronikTextLaenge - 1)].TrimEnd() + "…";
    }

    /// <summary>Nur der erste Satz einer alten Meldung (beim Ergänzen aus dem Verlauf): bis zum ersten „. “ oder „! “.</summary>
    private static string ErsterSatz(string text)
    {
        var ende = -1;
        foreach (var zeichen in new[] { ". ", "! " })
        {
            var i = text.IndexOf(zeichen, StringComparison.Ordinal);
            if (i >= 0 && (ende < 0 || i < ende)) ende = i;
        }
        return ende < 0 ? text : text[..(ende + 1)];
    }

    // ── Speichern ────────────────────────────────────────────────────────────

    /// <summary>Format der Hauschronik im Spielstand (fehlt in Spielständen von vor der Chronik – dann wird aus dem Verlauf ergänzt).</summary>
    internal const int HauschronikFormat = 1;

    /// <summary>Familien, deren Chronik beim Laden fehlte – sie wird nach dem Meldungsverlauf aus ihm ergänzt.</summary>
    private readonly List<Familie> _chronikErgaenzen = new();

    internal static List<ChronikStand>? ChronikSichern(Familie f) => f.ChronikListe.Count == 0 ? null : f.ChronikListe.Select(e => new ChronikStand
    {
        Art = e.Art, Minute = e.Minute, Person = e.Person, Text = e.Text, Vertraulich = e.Vertraulich,
    }).ToList();

    private void ChronikWiederherstellen(Familie f, List<ChronikStand>? stand, bool mitChronik)
    {
        f.ChronikListe.Clear();
        if (!mitChronik) { _chronikErgaenzen.Add(f); return; }
        foreach (var e in stand ?? new List<ChronikStand>())
            f.ChronikListe.Add(new Hauschronikeintrag(e.Art, e.Minute, e.Person, Kuerzen(e.Text ?? ""), e.Vertraulich));
        ChronikBegrenzen(f);
    }

    /// <summary>
    /// Alte Spielstände (ohne Hauschronik): nach dem Laden des Meldungsverlaufs aus ihm ergänzen, soweit er reicht – Heirat, Standesaufstieg,
    /// Entscheidung, Brand und bloßgestellte Geheimnisse des Hauses, je nur der erste Satz. Ämter stehen im Verlauf ohne Haus und bleiben leer.
    /// </summary>
    internal void ChronikAusVerlaufErgaenzen()
    {
        if (_chronikErgaenzen.Count == 0) return;
        foreach (var m in _w.Meldungsverlauf)
        {
            if (_w.HausVon(m) is not { } konto || Von(konto) is not { } f || !_chronikErgaenzen.Contains(f)) continue;
            Chronikart? art = m.Art switch
            {
                Meldungsart.Heirat => Chronikart.Heirat,
                Meldungsart.StandAufstieg => Chronikart.Amt,
                Meldungsart.Entscheidung => Chronikart.Entscheidung,
                Meldungsart.Feuer when m.Wichtigkeit >= Meldungswichtigkeit.Hoch => Chronikart.Brand,
                Meldungsart.Sonstiges when m.GeheimnisArt is not null && !m.Vertraulich && m.Wichtigkeit >= Meldungswichtigkeit.Hoch => Chronikart.Geheimnis,
                _ => null,
            };
            if (art is { } a)
                f.ChronikListe.Add(new Hauschronikeintrag(a, m.Minute, m.Person, Kuerzen(ErsterSatz(m.Text)), m.Vertraulich));
        }
        foreach (var f in _chronikErgaenzen) ChronikBegrenzen(f);
        _chronikErgaenzen.Clear();
    }
}
