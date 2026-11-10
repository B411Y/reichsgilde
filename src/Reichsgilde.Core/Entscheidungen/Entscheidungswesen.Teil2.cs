using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Gesundheit;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Entscheidungen;

/// <summary>
/// Teil 2 der Entscheidungen: Krankheit, Erbe, Geheimnis und Mahnung. Anders als die Forderungen der Unterwelt haben diese Fragen keine eigene
/// Liste im Kern – ihr Anlass ist ein Zustand (jemand ist krank, das Oberhaupt ist alt, ein Geheimnis ist entdeckt, der Kämmerer hat gemahnt).
/// Jeder Anlass hat einen Schlüssel; ist er entschieden, wird er nicht wieder gefragt, solange er besteht.
/// </summary>
public sealed partial class Entscheidungswesen
{
    /// <summary>Krankheit: den Medicus rufen (<see cref="MedicusRufen"/>).</summary>
    public const int Medicus = 0;

    /// <summary>Krankheit: Kräuterschnaps aus der Hausapotheke geben (<see cref="SchnapsGeben"/>).</summary>
    public const int Schnaps = 1;

    /// <summary>Krankheit: nichts tun.</summary>
    public const int NichtsTun = 2;

    /// <summary>Erbe: bei der Erbfolge bleiben (Wahl 1 … n: das Kind aus <see cref="Entscheidung.Auswahl"/> ins Testament).</summary>
    public const int ErbfolgeBelassen = 0;

    /// <summary>Geheimnis: vor Gericht anzeigen.</summary>
    public const int Anzeigen = 0;

    /// <summary>Geheimnis: erpressen – um Gulden, bei einem Ratsherrn um die Ratsstimme.</summary>
    public const int Erpressen = 1;

    /// <summary>Geheimnis: in der Stadt bloßstellen.</summary>
    public const int Blossstellen = 2;

    /// <summary>Geheimnis: für später merken (nichts geschieht).</summary>
    public const int Merken = 3;

    /// <summary>Mahnung: abwarten.</summary>
    public const int Abwarten = 0;

    /// <summary>Mahnung: Wegweiser zur Bank (die Oberfläche öffnet die Tafel; der Kern schließt nur die Frage).</summary>
    public const int ZumKredit = 1;

    /// <summary>Mahnung: Wegweiser zu den Betrieben (die Oberfläche öffnet die Tafel; der Kern schließt nur die Frage).</summary>
    public const int ZumVerkauf = 2;

    // ═════════════════════════════════════════════════════════════════════════
    //  Anlässe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Alle Anlässe von Teil 2 als Vorlage der Frage (Nummer, Beginn und Frist setzt das Öffnen) – je Haus eines Menschen in Kontoreihenfolge:
    /// Krankheiten (in der Reihenfolge der Familie), Erbe, Geheimnisse (Reihenfolge des Wissens), Mahnung.
    /// </summary>
    private List<Entscheidung> AnlaesseTeil2()
    {
        var liste = new List<Entscheidung>();
        for (var h = 0; h < _w.Konten.Count; h++)
        {
            var haus = _w.Konten[h];
            if (!BekommtEntscheidungen(haus)) continue;
            if (_w.Familien.Von(haus) is { } f)
            {
                if (_w.Gesundheit.IstAktiv)
                    foreach (var p in f.Lebende)
                        if (p.Krankheit is { } k && !k.NurGegengift && !(k.IstPest && k.Medicus && k.Schnaps) && !_w.Diebe.IstGeisel(p))
                            liste.Add(new Entscheidung(0, Entscheidungsart.Krankheit, haus, null, p.Nummer, _w.Gesundheit.MedicusKosten(k), 0, 0, Medicus,
                                Anlass: $"Krankheit:{h}:{p.Nummer}:{k.Name}:{k.Bis}"));
                if (ErbeAnlass(f) is { } grund && Erbkandidaten(f) is { Count: >= 2 } kinder)
                    liste.Add(new Entscheidung(0, Entscheidungsart.Erbe, haus, null, f.Oberhaupt.Nummer, 0, 0, 0, ErbfolgeBelassen, kinder,
                        Anlass: $"Erbe:{h}:{f.Oberhaupt.Nummer}:{grund}"));
            }
            if (_w.Geheimnisse.IstAktiv)
                foreach (var wissen in _w.Geheimnisse.AllesWissen)
                    if (wissen.Haus == haus && GeheimnisAnlass(haus, wissen) is { } g)
                        liste.Add(new Entscheidung(0, Entscheidungsart.Geheimnis, haus, _w.Geheimnisse.HausVon(g), _w.Geheimnisse.Inhaber(g) is Person ip ? ip.Nummer : null,
                            0, 0, 0, Merken, Geheimnis: g.Nummer, Anlass: $"Geheimnis:{h}:{g.Nummer}"));
            if (haus.Schuldenjahre > 0)
                liste.Add(new Entscheidung(0, Entscheidungsart.Mahnung, haus, null, null, Math.Max(0, -haus.Reinvermoegen), 0, 0, Abwarten,
                    Anlass: $"Mahnung:{h}:{_w.Jahr}:{haus.Schuldenjahre}"));
        }
        return liste;
    }

    /// <summary>Warum nach dem Erben gefragt wird: „alt“ (Sterberisiko vor Alter) oder „krank:Name:Bis“ (an der Krankheit) – null: kein Anlass.</summary>
    private string? ErbeAnlass(Familie f)
    {
        var o = f.Oberhaupt;
        if (!o.Lebt) return null;
        if (_w.Familien.Sterberisiko(o) >= Daten.ErbeAbSterberisiko) return "alt";
        if (o.Krankheit is { } k && _w.Gesundheit.IstAktiv && _w.Gesundheit.Sterberisiko(k) >= Daten.ErbeAbSterberisiko) return $"krank:{k.Name}:{k.Bis}";
        return null;
    }

    /// <summary>Die lebenden Kinder des Oberhaupts (höchstens 4, die ältesten zuerst), die ins Testament können.</summary>
    private static List<int> Erbkandidaten(Familie f) =>
        f.Kinder.Where(k => k.Lebt).OrderBy(k => k.Geburtsjahr).ThenBy(k => k.Nummer).Take(4).Select(k => k.Nummer).ToList();

    /// <summary>Das Geheimnis, wenn dieses Wissen eine Frage wert ist: frisch (dieses oder letztes Jahr), nicht aus der Beichte, noch nicht genutzt.</summary>
    private Geheimnisse.Geheimnis? GeheimnisAnlass(Konto haus, GeheimnisWissen wissen)
    {
        if (wissen.AusBeichte || wissen.GenutztIm is not null || wissen.BriefIm is not null || wissen.Jahr < _w.Jahr - 1) return null;
        if (_w.Geheimnisse.Nummer(wissen.Geheimnis) is not { } g || g.Oeffentlich || _w.Geheimnisse.IstEigen(haus, g)) return null;
        return _w.Geheimnisse.Inhaber(g) is { } inhaber && inhaber is not Person { Lebt: false } ? g : null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wahlen, Voreinstellung, Befehle
    // ═════════════════════════════════════════════════════════════════════════

    private IReadOnlyList<string> WahlenTeil2(Entscheidung e)
    {
        switch (e.Art)
        {
            case Entscheidungsart.Krankheit:
                return new[] { $"Medicus rufen ({Gulden(e.Betrag)})", $"{SchnapsName} geben ({_w.Gesundheit.Apotheke(e.Haus)} im Haus)", "Nichts tun" };
            case Entscheidungsart.Erbe:
                var f = _w.Familien.Von(e.Haus);
                var erbe = f is null ? null : _w.Familien.Erbe(f);
                return new[] { $"Bei der Erbfolge bleiben ({erbe?.Vorname ?? "niemand"})" }
                    .Concat((e.Auswahl ?? Array.Empty<int>()).Select(n => $"{PersonName(n)} als Erben einsetzen")).ToList();
            case Entscheidungsart.Geheimnis:
                return new[] { "Vor Gericht anzeigen", ErpressenUmStimme(e) ? "Um die Ratsstimme erpressen" : $"Erpressen ({Gulden(Schweigegeld(e))})",
                    "Bloßstellen", "Für später merken" };
            default:
                return new[] { "Abwarten", "Kredit aufnehmen …", "Einen Betrieb verkaufen …" };
        }
    }

    private string SchnapsName => _w.Gesundheit.IstAktiv ? _w.Gesundheit.Daten.Schnaps : "Kräuterschnaps";

    private Person? Kranker(Entscheidung e) =>
        e.Person is int n && _w.Familien.Von(e.Haus)?.Lebende.FirstOrDefault(p => p.Nummer == n) is { Krankheit: not null } p ? p : null;

    private bool MedicusMoeglich(Entscheidung e) =>
        Kranker(e)?.Krankheit is { } k && !k.NurGegengift && !(k.IstPest && k.Medicus) && e.Haus.Vermoegen >= _w.Gesundheit.MedicusKosten(k);

    private bool SchnapsMoeglich(Entscheidung e) =>
        Kranker(e)?.Krankheit is { } k && _w.Gesundheit.Apotheke(e.Haus) > 0 && (k.IstPest ? !k.Schnaps : k.Schnapsheilbar);

    /// <summary>Die Kette des Nutzers (10.11.2026): Medicus, wenn bezahlbar, sonst Kräuterschnaps, wenn Vorrat da ist, sonst nichts – für alle Kranken.</summary>
    private int KrankheitVoreinstellung(Entscheidung e) => MedicusMoeglich(e) ? Medicus : SchnapsMoeglich(e) ? Schnaps : NichtsTun;

    /// <summary>Die Wahl ohne Befehl, die immer geht – der letzte Rückfall der Voreinstellung.</summary>
    private static int Rueckfall(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Krankheit => NichtsTun,
        Entscheidungsart.Geheimnis => Merken,
        _ => 0, // Erbfolge belassen, abwarten
    };

    private bool ErpressenUmStimme(Entscheidung e) =>
        e.Geheimnis is int nr && _w.Geheimnisse.Nummer(nr) is { } g && _w.Geheimnisse.HausVon(g) is null;

    /// <summary>Das Schweigegeld, das ein Brief jetzt verlangen würde – wie die KI: ein Zehntel vom Bargeld des Opfers, auf 50 gerundet, höchstens 1.500, mindestens 50.</summary>
    public double Schweigegeld(Entscheidung e)
    {
        if (e.Absender is not { } opfer || !_w.Geheimnisse.IstAktiv) return 0;
        var ki = _w.Geheimnisse.Daten.Ki;
        return Math.Max(50, Math.Min(ki.Hoechstens, Math.Round(Math.Max(0, opfer.Vermoegen) * ki.Anteil / 50) * 50));
    }

    private Befehl? BefehlTeil2(Entscheidung e, int wahl) => e.Art switch
    {
        Entscheidungsart.Krankheit when e.Person is int p => wahl switch { Medicus => new MedicusRufen(p), Schnaps => new SchnapsGeben(p), _ => null },
        Entscheidungsart.Erbe when wahl > 0 && e.Auswahl is { } kinder && wahl <= kinder.Count => new ErbenBestimmen(kinder[wahl - 1]),
        Entscheidungsart.Geheimnis when e.Geheimnis is int g => wahl switch
        {
            Anzeigen => new GeheimnisNutzen(g, Geheimnisnutzung.Anzeige),
            Erpressen => ErpressenUmStimme(e) ? new GeheimnisNutzen(g, Geheimnisnutzung.Ratsstimme) : new GeheimnisNutzen(g, Geheimnisnutzung.Gulden, Schweigegeld(e)),
            Blossstellen => new GeheimnisNutzen(g, Geheimnisnutzung.Blossstellen),
            _ => null,
        },
        _ => null, // nichts tun, merken, Erbfolge belassen, Mahnung (Wegweiser)
    };

    /// <summary>Welche Wahl dieser Befehl für diese Frage ist (null: er gehört nicht zu ihr) – für den Doppelweg.</summary>
    private static int? WahlAusBefehl(Entscheidung e, Befehl befehl) => (e.Art, befehl) switch
    {
        (Entscheidungsart.Krankheit, MedicusRufen m) when m.Person == e.Person => Medicus,
        (Entscheidungsart.Krankheit, SchnapsGeben s) when s.Person == e.Person => Schnaps,
        (Entscheidungsart.Erbe, ErbenBestimmen b) => e.Auswahl is { } kinder && kinder.Contains(b.Person) ? kinder.ToList().IndexOf(b.Person) + 1 : -1,
        (Entscheidungsart.Geheimnis, GeheimnisNutzen n) when n.Geheimnis == e.Geheimnis => n.Nutzung switch
        {
            Geheimnisnutzung.Anzeige => Anzeigen,
            Geheimnisnutzung.Blossstellen => Blossstellen,
            _ => Erpressen,
        },
        (Entscheidungsart.Mahnung, KreditAufnehmen) => ZumKredit,
        (Entscheidungsart.Mahnung, BetriebVerkaufen) => ZumVerkauf,
        _ => null,
    };

    /// <summary>Was der Befehl ohne Befehl zurückmeldet (nichts tun, merken, Erbfolge, Wegweiser).</summary>
    private string OhneBefehlText(Entscheidung e, int wahl) => (e.Art, wahl) switch
    {
        (Entscheidungsart.Mahnung, ZumKredit) => "Gelesen – die Geldleiher geben Kredit.",
        (Entscheidungsart.Mahnung, ZumVerkauf) => "Gelesen – ein verkaufter Betrieb hebt das Reinvermögen.",
        _ => "Gelesen – es geschieht nichts.",
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  Texte
    // ═════════════════════════════════════════════════════════════════════════

    private string PersonName(int nummer) => _w.Familien.PersonNummer(nummer)?.Vorname ?? "Unbekannt";

    private string GeheimnisText(Entscheidung e) =>
        e.Geheimnis is int nr && _w.Geheimnisse.Nummer(nr) is { } g
            ? $"{_w.Geheimnisse.Inhaber(g)?.Name ?? "Unbekannt"}: {_w.Geheimnisse.Beschreibung(g)}" : "ein Geheimnis";

    private string TitelTeil2(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Krankheit => $"{(e.Person is int p ? PersonName(p) : "Jemand")} ist krank",
        Entscheidungsart.Erbe => $"Wer soll {(e.Person is int o ? PersonName(o) : "das Oberhaupt")} beerben?",
        Entscheidungsart.Geheimnis => "Ein Geheimnis entdeckt",
        _ => "Mahnung des Kämmerers",
    };

    private string BeschreibungTeil2(Entscheidung e)
    {
        switch (e.Art)
        {
            case Entscheidungsart.Krankheit:
                if (Kranker(e) is not { Krankheit: { } k } p) return "Die Krankheit ist vorbei.";
                return $"{p.Vorname} hat {(k.IstPest ? "die Pest" : k.Name)} – bis Ende {k.Bis}, am Jahresende tödlich mit {Prozent(_w.Gesundheit.Sterberisiko(k))}.";
            case Entscheidungsart.Erbe:
                var o = e.Person is int n ? _w.Familien.PersonNummer(n) : null;
                // Das Wort wie im Familienbuch (Familienbuch.Verfassung: „betagt“ bis 15 %, dann „gebrechlich“)
                var grund = o?.Krankheit is { } kr && _w.Familien.Sterberisiko(o) < Daten.ErbeAbSterberisiko ? $"schwer krank ({kr.Name})"
                    : o is null ? "betagt" : _w.Familien.Verfassung(o);
                return $"{o?.Vorname ?? "Das Oberhaupt"} ist {grund}. Wer soll das Haus erben? Ohne Testament erbt das älteste volljährige Kind.";
            case Entscheidungsart.Geheimnis:
                return $"Ihr habt erfahren: {GeheimnisText(e)}. Was macht ihr daraus?";
            default:
                var rest = _w.Daten.Schulden.JahreBisBankrott - e.Haus.Schuldenjahre;
                return $"Euer Haus hat {Gulden(e.Betrag)} Schulden. Noch {rest} {(rest == 1 ? "Jahresabschluss" : "Jahresabschlüsse")} im Minus, dann droht der Bankrott.";
        }
    }

    private static string Prozent(double x) => $"{Zahlenformat.Ganz(x * 100)} %";

    private string FolgenTeil2(Entscheidung e, int wahl)
    {
        switch (e.Art)
        {
            case Entscheidungsart.Krankheit:
                if (Kranker(e) is not { Krankheit: { } k } p) return "Die Krankheit ist vorbei.";
                var risiko = _w.Gesundheit.Sterberisiko(k);
                if (wahl == Medicus)
                {
                    if (k.IstPest && k.Medicus) return $"Der Medicus pflegt {p.Vorname} schon.";
                    var kosten = _w.Gesundheit.MedicusKosten(k);
                    var geld = e.Haus.Vermoegen < kosten ? $" Dafür reicht euer Geld nicht – es fehlen {Gulden(kosten - e.Haus.Vermoegen)}." : "";
                    return $"Kostet {Gulden(kosten)} (ihr habt {Gulden(e.Haus.Vermoegen)}).{geld} " + (k.IstPest
                        ? $"Das Sterberisiko sinkt von {Prozent(risiko)} auf {Prozent(_w.Gesundheit.Sterberisiko(k with { Medicus = true }))}."
                        : $"{k.Name} ist sofort geheilt.");
                }
                if (wahl == Schnaps)
                {
                    var vorrat = _w.Gesundheit.Apotheke(e.Haus);
                    var leer = vorrat > 0 ? $"Aus der Hausapotheke (noch {vorrat})." : $"Die Hausapotheke ist leer – {SchnapsName} kostet am Markt etwa {Gulden(_w.Gesundheit.SchnapsPreis)}.";
                    if (k.IstPest)
                        return k.Schnaps ? $"{p.Vorname} hat schon {SchnapsName} bekommen – mehr hilft nicht."
                            : $"{leer} Das Sterberisiko sinkt von {Prozent(risiko)} auf {Prozent(_w.Gesundheit.Sterberisiko(k with { Schnaps = true }))}.";
                    return k.Schnapsheilbar ? $"{leer} {k.Name} ist sofort geheilt." : $"Gegen {k.Name} hilft {SchnapsName} nicht – nur der Medicus.";
                }
                return $"Kostet nichts. {p.Vorname} bleibt krank bis Ende {k.Bis}; am Jahresende tödlich mit {Prozent(risiko)}." +
                       (risiko > 0 ? " Ein Tod lässt sich nicht zurücknehmen." : "");
            case Entscheidungsart.Erbe:
                var f = _w.Familien.Von(e.Haus);
                if (wahl == ErbfolgeBelassen)
                    return f is not null && _w.Familien.Erbe(f) is { } erbe
                        ? $"Es bleibt bei der Erbfolge: Erben würde jetzt {erbe.Vorname}."
                        : "Es bleibt bei der Erbfolge – erben würde jetzt niemand, das Haus erlöschte.";
                var kind = e.Auswahl is { } kinder && wahl <= kinder.Count ? _w.Familien.PersonNummer(kinder[wahl - 1]) : null;
                if (kind is null) return "Dieses Kind gibt es nicht mehr.";
                return $"{kind.Vorname} ({kind.Alter(_w.Familien.Jahr)} Jahre) kommt ins Testament" +
                       (_w.Familien.IstVolljaehrig(kind) ? "." : " – bis zur Volljährigkeit erbt noch das älteste volljährige Kind.") +
                       " Kostet nichts und lässt sich jederzeit ändern.";
            case Entscheidungsart.Geheimnis:
                if (wahl == Merken) return "Nichts geschieht. Ihr könnt das Geheimnis später im Charakterfenster nutzen, solange es keiner verrät.";
                if (e.Geheimnis is not int nr || _w.Geheimnisse.Nummer(nr) is not { } g) return "Dieses Geheimnis gibt es nicht mehr.";
                var nutzung = wahl == Anzeigen ? Geheimnisnutzung.Anzeige : wahl == Blossstellen ? Geheimnisnutzung.Blossstellen
                    : ErpressenUmStimme(e) ? Geheimnisnutzung.Ratsstimme : Geheimnisnutzung.Gulden;
                if (_w.Geheimnisse.NutzenVerboten(e.Haus, g, nutzung, Schweigegeld(e)) is { } verboten) return $"Geht nicht: {verboten}";
                var ap = _w.HatAktionspunkte ? $"Kostet {_w.Geheimnisse.ApKosten(nutzung)} AP (ihr habt {e.Haus.Aktionspunkte.Vorrat}). " : "";
                var inhaber = _w.Geheimnisse.Inhaber(g)?.Name ?? "Unbekannt";
                return ap + nutzung switch
                {
                    Geheimnisnutzung.Anzeige => $"Dazu die Gerichtsgebühr von {Gulden(_w.Justiz.Gerichtsgebuehr(e.Haus))}. Am Gerichtstag zählt das Geheimnis als Beweis gegen {inhaber}.",
                    Geheimnisnutzung.Ratsstimme => $"In der nächsten Ratssitzung stimmt {inhaber} wie ihr. Ihr hinterlasst eine Spur, {inhaber} denkt schlechter von euch.",
                    Geheimnisnutzung.Gulden => $"{Name(e.Absender)} bekommt einen Erpresserbrief über {Gulden(Schweigegeld(e))} Schweigegeld. Der Brief kann ein Beweis gegen euch werden.",
                    _ => $"Die ganze Stadt erfährt es, {inhaber} verliert Ansehen – danach taugt das Geheimnis zu nichts mehr. Das lässt sich nicht zurücknehmen.",
                };
            default:
                var rest = _w.Daten.Schulden.JahreBisBankrott - e.Haus.Schuldenjahre;
                return wahl switch
                {
                    ZumKredit => "Öffnet die Bank: Ein Kredit bringt Bargeld, kostet aber Zinsen.",
                    ZumVerkauf => "Öffnet eure Betriebe: Ein verkaufter Betrieb bringt Geld und hebt das Reinvermögen sofort.",
                    _ => $"Nichts geschieht. Bleibt das Reinvermögen im Minus, ist das Haus nach {rest} weiteren {(rest == 1 ? "Jahresabschluss" : "Jahresabschlüssen")} bankrott.",
                };
        }
    }

    private string AbschlussTeil2(Entscheidung e, Entscheidungsabschluss grund, int? wahl)
    {
        var person = e.Person is int n ? PersonName(n) : "";
        if (wahl is null)
            return e.Art switch
            {
                Entscheidungsart.Krankheit => e.Person is int tot && _w.Familien.PersonNummer(tot) is { Lebt: false }
                    ? $"{person} ist gestorben – die Frage nach der Krankheit hat sich erledigt."
                    : $"Die Krankheit von {person} ist vorbei – nichts mehr zu entscheiden.",
                Entscheidungsart.Erbe => $"Die Frage nach dem Erben von {person} hat sich erledigt.",
                Entscheidungsart.Geheimnis => $"Das Geheimnis ({GeheimnisText(e)}) taugt nicht mehr – nichts mehr zu entscheiden.",
                _ => "Die Mahnung des Kämmerers hat sich erledigt.",
            };
        return (e.Art, wahl) switch
        {
            (Entscheidungsart.Krankheit, Medicus) => $"Ihr habt den Medicus zu {person} gerufen.",
            (Entscheidungsart.Krankheit, Schnaps) => $"{person} hat {SchnapsName} bekommen.",
            (Entscheidungsart.Krankheit, _) => $"Für {person} habt ihr nichts unternommen.",
            (Entscheidungsart.Erbe, 0) => $"Für das Erbe von {person} bleibt es bei der Erbfolge.",
            (Entscheidungsart.Erbe, -1) => $"Ihr habt das Testament von {person} geändert.",
            (Entscheidungsart.Erbe, int i) => e.Auswahl is { } kinder && i <= kinder.Count
                ? $"Testament: {PersonName(kinder[i - 1])} soll {person} beerben." : $"Ihr habt das Testament von {person} geändert.",
            (Entscheidungsart.Geheimnis, Anzeigen) => $"Ihr habt das Geheimnis angezeigt ({GeheimnisText(e)}).",
            (Entscheidungsart.Geheimnis, Erpressen) => $"Ihr erpresst mit dem Geheimnis ({GeheimnisText(e)}).",
            (Entscheidungsart.Geheimnis, Blossstellen) => $"Ihr habt das Geheimnis bloßgestellt ({GeheimnisText(e)}).",
            (Entscheidungsart.Geheimnis, _) => $"Ihr behaltet das Geheimnis für später ({GeheimnisText(e)}).",
            (_, ZumKredit) => "Mahnung des Kämmerers: Ihr seht nach einem Kredit.",
            (_, ZumVerkauf) => "Mahnung des Kämmerers: Ihr seht nach euren Betrieben.",
            _ => "Mahnung des Kämmerers: Ihr wartet ab.",
        };
    }
}
