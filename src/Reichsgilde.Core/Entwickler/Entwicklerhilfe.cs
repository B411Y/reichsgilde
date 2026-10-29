using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gesundheit;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Entwickler;

/// <summary>
/// Das Entwicklerfenster im Kern: führt die <see cref="EntwicklerBefehl"/>e aus, hält die Sperre und merkt sich, ob je einer gewirkt hat.
///
/// Die Sperre (<see cref="Erlaubt"/>) ist aus, bis die Oberfläche sie einschaltet – im Einzelspiel nach dem Haken „Entwickler-Optionen“,
/// im Mehrspieler nie. Sie steht im Spielstand, denn sie ist eine Regel der Partie: Ein <see cref="Mehrspieler.Befehlsprotokoll"/> spielt nur
/// dann gleich ab, wenn sie beim Abspielen genauso steht wie beim Aufzeichnen.
///
/// Das Kennzeichen „mit Entwicklerhilfe“ (<see cref="Benutzt"/>) bleibt für immer, auch über Speichern und Laden – für die Laden-Liste
/// (<see cref="Speichern.Speicherstand.MitEntwicklerhilfe"/>) und den Jahresbericht.
/// </summary>
public sealed class Entwicklerhilfe
{
    private readonly Stadtwirtschaft _w;

    internal Entwicklerhilfe(Stadtwirtschaft w) => _w = w;

    /// <summary>Dürfen Entwicklerbefehle wirken? (Standard: nein – dann lehnt der Kern sie ab.)</summary>
    public bool Erlaubt { get; set; }

    /// <summary>Hat in dieser Partie je ein Entwicklerbefehl gewirkt? Lässt sich nicht zurücknehmen.</summary>
    public bool Benutzt { get; internal set; }

    /// <summary>Aus <see cref="Stadtwirtschaft.Ausfuehren"/>: prüft die Sperre, führt aus und setzt bei Erfolg das Kennzeichen.</summary>
    internal Befehlsergebnis Ausfuehren(Konto absender, EntwicklerBefehl befehl)
    {
        if (!Erlaubt)
            return Befehlsergebnis.Abgelehnt("Entwicklerbefehle sind gesperrt (Entwickler-Optionen im Hauptmenü).");
        if (_w.Rat.LaufendeSitzung is not null)
            return Befehlsergebnis.Abgelehnt("Während der Ratssitzung gehen keine Entwicklerbefehle – danach wieder.");
        var ergebnis = Bearbeiten(absender, befehl);
        if (ergebnis.Erfolg)
        {
            Benutzt = true;
            _w.Melden("Entwicklerhilfe: " + ergebnis.Meldung);
        }
        return ergebnis;
    }

    private Befehlsergebnis Bearbeiten(Konto absender, EntwicklerBefehl befehl)
    {
        switch (befehl)
        {
            case EntwicklerGulden g: return MitHaus(absender, g.Haus, k => Gulden(k, g.Betrag));
            case EntwicklerAktionspunkte ap: return MitHaus(absender, ap.Haus, k => Aktionspunkte(k, ap.Anzahl));
            case EntwicklerStand s: return MitHaus(absender, s.Haus, k => Stand(k, s.Stand));
            case EntwicklerAnsehen a: return MitHaus(absender, a.Haus, k => Ansehen(k, a.Wert));
            case EntwicklerMeinung m: return MitHaus(absender, m.Haus, k => Meinung(k, m.Von, m.Wert));
            case EntwicklerGeburt geb: return MitHaus(absender, geb.Haus, k => Geburt(k, geb.Geschlecht));
            case EntwicklerHeirat h: return MitHaus(absender, h.Haus, k => Heirat(k, h.Kandidat));
            case EntwicklerKrankheit kr: return Krankheit(kr.Person, kr.Krankheit);
            case EntwicklerTod t: return Tod(t.Person);
            case EntwicklerZug z: return Zug(z.Person, z.Zug, z.Hinzufuegen);
            case EntwicklerDiebstahl d: return MitBetrieb(d.Betrieb, Diebstahl);
            case EntwicklerFeuer f: return MitBetrieb(f.Betrieb, Feuer);
            case EntwicklerHausbrand hb: return MitHaus(absender, hb.Haus, Hausbrand);
            case EntwicklerWare ware: return MitBetrieb(ware.Betrieb, b => Ware(b, ware.Ware, ware.Menge));
            case EntwicklerZutaten z: return MitBetrieb(z.Betrieb, Zutaten);
            default: return Befehlsergebnis.Abgelehnt($"Den Entwicklerbefehl {befehl.GetType().Name} kennt der Kern nicht.");
        }
    }

    private Befehlsergebnis MitHaus(Konto absender, string? haus, Func<Konto, Befehlsergebnis> aktion) =>
        haus is null ? aktion(absender)
        : _w.Konten.FirstOrDefault(k => k.Inhaber == haus) is { } konto ? aktion(konto)
        : Befehlsergebnis.Abgelehnt($"Ein Haus „{haus}“ gibt es nicht.");

    private Befehlsergebnis MitBetrieb(string name, Func<Betrieb, Befehlsergebnis> aktion) =>
        _w.Betriebe.FirstOrDefault(b => b.Name == name) is { } betrieb ? aktion(betrieb)
        : Befehlsergebnis.Abgelehnt($"Einen Betrieb „{name}“ gibt es nicht.");

    private string Waehrung => _w.Daten.Allgemein.Waehrung;

    // ── Haus ────────────────────────────────────────────────────────────────

    private Befehlsergebnis Gulden(Konto k, double betrag)
    {
        if (!double.IsFinite(betrag) || betrag == 0) return Befehlsergebnis.Abgelehnt("Der Betrag muss eine Zahl ungleich 0 sein.");
        k.Entwicklerhilfe(betrag);
        return Befehlsergebnis.Ok($"{k.Inhaber} {(betrag > 0 ? "bekommt" : "verliert")} {Zahlenformat.Ganz(Math.Abs(betrag))} {Waehrung} " +
                                  $"(jetzt {Zahlenformat.Ganz(k.Vermoegen)}).");
    }

    private Befehlsergebnis Aktionspunkte(Konto k, int? anzahl)
    {
        var ziel = anzahl ?? _w.Daten.Aktionspunkte?.Hoechstens ?? _w.AktionspunkteJeRunde;
        if (ziel < 0) return Befehlsergebnis.Abgelehnt("Aktionspunkte können nicht negativ sein.");
        k.Aktionspunkte.Setzen(ziel);
        return Befehlsergebnis.Ok($"{k.Inhaber} hat jetzt {k.Aktionspunkte.Vorrat} Aktionspunkte.");
    }

    private Befehlsergebnis Stand(Konto k, int stand)
    {
        var staende = _w.Daten.Staende;
        if (stand < 0 || stand >= staende.Count)
            return Befehlsergebnis.Abgelehnt($"Einen Stand Nr. {stand} gibt es nicht (0 bis {staende.Count - 1}).");
        k.Stand = stand;
        return Befehlsergebnis.Ok($"{k.Inhaber} steht jetzt im Stand {staende[stand].Name}.");
    }

    private Befehlsergebnis Ansehen(Konto k, double wert)
    {
        if (!double.IsFinite(wert)) return Befehlsergebnis.Abgelehnt("Das Ansehen muss eine Zahl sein.");
        k.EntwicklerAnsehen = wert - (_w.Rat.Ansehen(k) - k.EntwicklerAnsehen);
        return Befehlsergebnis.Ok($"{k.Inhaber} hat jetzt {Zahlenformat.Kommazahl(_w.Rat.Ansehen(k))} Ansehen.");
    }

    private Befehlsergebnis Meinung(Konto k, string von, double wert)
    {
        if (_w.Meinungen.Charakter(von) is not { } person)
            return Befehlsergebnis.Abgelehnt($"Eine Person „{von}“ gibt es nicht.");
        if (!double.IsFinite(wert)) return Befehlsergebnis.Abgelehnt("Die Meinung muss eine Zahl sein.");
        if (_w.Meinungen.HausKennung(k) == person.Kennung)
            return Befehlsergebnis.Abgelehnt($"{person.Name} ist das Oberhaupt von {k.Inhaber} selbst.");
        _w.Meinungen.EntwicklerGrundSetzen(person, k, wert);
        return Befehlsergebnis.Ok($"{person.Name} denkt jetzt {Zahlenformat.Ganz(_w.Meinungen.Meinung(person, k))} über {k.Inhaber}.");
    }

    // ── Familie ─────────────────────────────────────────────────────────────

    private Befehlsergebnis Geburt(Konto k, Geschlecht geschlecht)
    {
        if (_w.Familien.Von(k) is not { } f) return Befehlsergebnis.Abgelehnt($"{k.Inhaber} hat keine Familie.");
        if (!k.IstAktiv) return Befehlsergebnis.Abgelehnt($"{k.Inhaber} ist nicht mehr im Spiel.");
        var kind = _w.Familien.EntwicklerGeburt(f, geschlecht);
        return Befehlsergebnis.Ok($"Im Haus {f.Nachname} kommt {kind.Vorname} zur Welt.");
    }

    private Befehlsergebnis Heirat(Konto k, int? nummer)
    {
        var fb = _w.Familien;
        if (fb.Von(k) is not { } f) return Befehlsergebnis.Abgelehnt($"{k.Inhaber} hat keine Familie.");
        if (!k.IstAktiv) return Befehlsergebnis.Abgelehnt($"{k.Inhaber} ist nicht mehr im Spiel.");
        if (f.Ehepartner is { } e) return Befehlsergebnis.Abgelehnt($"{f.Oberhaupt.Name} ist verheiratet mit {e.Name}.");
        var kandidat = nummer is int n ? fb.KandidatNummer(n) : fb.Kandidaten.FirstOrDefault(c => c.Person.Geschlecht != f.Oberhaupt.Geschlecht);
        if (kandidat is null)
            return Befehlsergebnis.Abgelehnt(nummer is null ? "In der Brautschau ist niemand Passendes." : "Diesen Heiratskandidaten gibt es nicht (mehr).");
        if (kandidat.Person.Geschlecht == f.Oberhaupt.Geschlecht)
            return Befehlsergebnis.Abgelehnt($"{kandidat.Person.Name} sucht {(kandidat.Person.Geschlecht == Geschlecht.Frau ? "einen Ehemann" : "eine Ehefrau")}.");
        fb.Vermaehlen(k, kandidat, kosten: 0);
        return Befehlsergebnis.Ok($"{f.Oberhaupt.Vorname} und {kandidat.Person.Vorname} sind verheiratet.");
    }

    /// <summary>Eine lebende Person eines Hauses (nicht der Brautschau) und ihr Haus.</summary>
    private (Person? Person, Familie? Familie, Befehlsergebnis? Fehler) Angehoeriger(int nummer)
    {
        if (_w.Familien.PersonNummer(nummer) is not { Lebt: true } p) return (null, null, Befehlsergebnis.Abgelehnt($"Eine lebende Person Nr. {nummer} gibt es nicht."));
        if (_w.Familien.FamilieVon(p) is not { } f) return (null, null, Befehlsergebnis.Abgelehnt($"{p.Name} gehört zu keinem Haus."));
        return (p, f, null);
    }

    private Befehlsergebnis Krankheit(int nummer, string? name)
    {
        if (!_w.Gesundheit.IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt wird niemand krank.");
        var (p, f, fehler) = Angehoeriger(nummer);
        if (fehler is not null) return fehler;
        if (name is null)
        {
            if (p!.Krankheit is not { } alt) return Befehlsergebnis.Abgelehnt($"{p.Vorname} ist gesund.");
            p.Krankheit = null;
            return Befehlsergebnis.Ok($"{p.Vorname} {p.Familienname} ist von {alt.Name} geheilt.");
        }
        if (name != Erkrankung.PestName && !_w.Gesundheit.Daten.Krankheiten.ContainsKey(name))
            return Befehlsergebnis.Abgelehnt($"Eine Krankheit „{name}“ gibt es nicht.");
        p!.Krankheit = null; // eine andere Krankheit weicht der neuen
        _w.Gesundheit.Erkranken(f!.Konto, p, name);
        return Befehlsergebnis.Ok($"{p.Vorname} {p.Familienname} ist an {name} erkrankt.");
    }

    private Befehlsergebnis Tod(int nummer)
    {
        if (_w.Familien.PersonNummer(nummer) is not { Lebt: true } p) return Befehlsergebnis.Abgelehnt($"Eine lebende Person Nr. {nummer} gibt es nicht.");
        var name = p.Name;
        _w.Familien.AnKrankheitSterben(p, "plötzlich");
        return p.Lebt ? Befehlsergebnis.Abgelehnt($"{name} kann so nicht sterben (das Haus ist nicht mehr im Spiel).")
            : Befehlsergebnis.Ok($"{name} ist gestorben.");
    }

    private Befehlsergebnis Zug(int nummer, string kennung, bool hinzufuegen)
    {
        if (!_w.Familien.HatZuege) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Charakterzüge.");
        if (_w.Familien.Zug(kennung) is not { } zug) return Befehlsergebnis.Abgelehnt($"Einen Charakterzug „{kennung}“ gibt es nicht.");
        if (_w.Familien.PersonNummer(nummer) is not { Lebt: true } p) return Befehlsergebnis.Abgelehnt($"Eine lebende Person Nr. {nummer} gibt es nicht.");
        if (hinzufuegen == p.HatZug(kennung))
            return Befehlsergebnis.Abgelehnt($"{p.Vorname} hat den Zug {zug.Name} {(hinzufuegen ? "schon" : "nicht")}.");
        if (hinzufuegen) _w.Familien.EntwicklerZugGeben(p, zug);
        else p.ZugEntfernen(zug);
        return Befehlsergebnis.Ok($"{p.Vorname} {p.Familienname} {(hinzufuegen ? "hat jetzt" : "verliert")} den Zug {zug.Name}.");
    }

    // ── Ereignisse ──────────────────────────────────────────────────────────

    private Befehlsergebnis Diebstahl(Betrieb b)
    {
        if (!_w.Gauner.IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Unterwelt.");
        var beute = _w.Gauner.MoeglicheBeute(b);
        foreach (var (ware, stueck, _) in beute)
            b.Auslagern(ware, stueck);
        var kasse = _w.Bank.IstAktiv ? _w.Bank.KasseBestehlen(b) : 0;
        var wert = beute.Sum(x => x.Wert) + kasse;
        if (wert <= 0) return Befehlsergebnis.Ok($"Einbruch in {b.Name} – doch das Lager war leer.");
        b.RaubBuchen(wert);
        var text = $"Einbruch in {b.Name}: {_w.Gauner.Diebesgut(beute, kasse)} gestohlen.";
        _w.Gauner.Melden(null, text);
        return Befehlsergebnis.Ok(text);
    }

    private Befehlsergebnis Feuer(Betrieb b)
    {
        if (!_w.Gauner.IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Unterwelt (Brandschaden und Stillstand stehen in „gauner“).");
        var sabotage = _w.Gauner.Daten.Sabotage;
        var schaden = _w.Gauner.Sabotageschaden(b);
        b.BrandBuchen(schaden);
        b.Sabotieren(sabotage.StillstandMinuten);
        b.SchadenBuchen(sabotage.Reparatur);
        var text = $"Feuer in {b.Name}! {sabotage.StillstandMinuten / 60} Arbeitsstunden Stillstand, Reparatur {Zahlenformat.Ganz(sabotage.Reparatur)}.";
        _w.Gauner.Melden(null, text);
        return Befehlsergebnis.Ok(text);
    }

    private Befehlsergebnis Hausbrand(Konto k)
    {
        if (!_w.Gauner.GegenWohnhaus) return Befehlsergebnis.Abgelehnt("In dieser Stadt brennen keine Wohnhäuser (gauner.wohnhaus fehlt).");
        if (!k.IstAktiv) return Befehlsergebnis.Abgelehnt($"{k.Inhaber} ist nicht mehr im Spiel.");
        if (_w.Wohnen.IstBeschaedigt(k)) return Befehlsergebnis.Abgelehnt($"Das Wohnhaus von {k.Inhaber} ist vom letzten Brand noch nicht instand gesetzt.");
        var schaden = _w.Gauner.Brandschaden(k);
        k.Schaden(schaden);
        _w.Wohnen.Brand(k, _w.Uhr.VergangeneMinuten + _w.Gauner.Daten.Wohnhaus!.Brand.InstandMinuten);
        var text = $"Feuer am {_w.Wohnen.Stufe(k)!.Name} von {k.Inhaber}! Reparatur {Zahlenformat.Ganz(schaden)} {Waehrung}.";
        _w.Gauner.Melden(null, text);
        return Befehlsergebnis.Ok(text);
    }

    // ── Wirtschaft ──────────────────────────────────────────────────────────

    private Befehlsergebnis Ware(Betrieb b, string ware, int menge)
    {
        if (!_w.Daten.Waren.ContainsKey(ware)) return Befehlsergebnis.Abgelehnt($"Eine Ware „{ware}“ gibt es nicht.");
        if (menge == 0) return Befehlsergebnis.Abgelehnt("Die Menge muss ungleich 0 sein.");
        if (menge > 0) b.Einlagern(ware, menge);
        else b.Auslagern(ware, -menge);
        return Befehlsergebnis.Ok($"Im Lager von {b.Name} liegen jetzt {b.ImLager(ware)} {ware}.");
    }

    private Befehlsergebnis Zutaten(Betrieb b)
    {
        if (b.Zutaten.Count == 0) return Befehlsergebnis.Abgelehnt($"{b.Name} braucht keine Zutaten.");
        var grenze = b.LagerKapazitaetJeWare;
        foreach (var ware in b.Zutaten.Keys)
            if (b.ImLager(ware) < grenze)
                b.Einlagern(ware, grenze - b.ImLager(ware));
        return Befehlsergebnis.Ok($"Die Zutaten von {b.Name} sind aufgefüllt ({string.Join(", ", b.Zutaten.Keys.Select(z => $"{b.ImLager(z)} {z}"))}).");
    }
}
