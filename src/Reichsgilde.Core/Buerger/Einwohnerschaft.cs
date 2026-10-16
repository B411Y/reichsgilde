using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Buerger;

/// <summary>
/// Die Bürger-KI: alle Einwohner der Stadt, die keinem Haus angehören.
///
/// Wer dazugehört:
///   • gewöhnliche Einwohner – <see cref="BuergerDaten.JeHaus"/> je Haus, festgelegt beim Spielstart; stirbt einer, zieht ein neuer zu,
///   • die Ratsherren des Stadtrats (Wahl, Gunst und Ämter regelt weiter der Rat),
///   • die ledigen Heiratskandidaten (Heirat und Weggang regelt weiter das Familienbuch – hier wird nur abgeglichen).
///
/// Tagesablauf (Vorbild Utility-KI): Jede volle Stunde wachsen die Bedürfnisse jedes Einwohners. Dann bewertet er jede
/// mögliche Handlung danach, wie viel Dringlichkeit sie stillt (gewichtet nach seinem Volk), und wählt die nützlichste.
/// Ein leichtes Rauschen aus dem eigenen Zufallsgenerator und ein Bonus fürs Weitermachen machen das Verhalten lebendig,
/// aber nicht sprunghaft. Am Jahresende (= Ende des Spieltags) schlafen alle aus: Die Bedürfnisse beginnen neu.
///
/// Schritt 1: Die Einwohner haben noch KEINE Wirkung auf Markt, Schänke, Kirche oder Diebe. Sie lesen den Kern nur
/// (Schänken, Kirchen, Kandidaten, Ratsherren) und würfeln ausschließlich mit ihrem eigenen Generator –
/// deshalb bleiben alle Messungen gleich.
/// </summary>
public sealed class Einwohnerschaft
{
    private readonly Stadtwirtschaft _w;
    private readonly BuergerDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Einwohner> _einwohner = new();
    private readonly List<Stadthaus> _haeuser = new();
    private bool _gegruendet;
    private int _naechsteNummer = 1;
    private int _verstorben;
    private int _zugezogen;

    public Einwohnerschaft(Stadtwirtschaft w, BuergerDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 71 + 53));
    }

    public bool IstAktiv => _d is not null;
    public BuergerDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Einwohner (buerger fehlt in den Daten).");

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft (für Darstellung, Oberfläche und Tests)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Alle Einwohner (gewöhnliche zuerst, dann Ratsherren, dann Kandidaten). Leer, bis die Partie das erste Mal tickt.</summary>
    public IReadOnlyList<Einwohner> Einwohner => _einwohner;

    /// <summary>Die aktuelle Spielminute (<see cref="Spielzeit.VergangeneMinuten"/>) – nach ihr richten sich Wege und Aufenthalte.</summary>
    private long Jetzt => _w.Uhr.VergangeneMinuten;

    /// <summary>Wer sich gerade an diesem Ort aufhält (wer unterwegs ist, zählt nirgends).</summary>
    public IEnumerable<Einwohner> Am(Stadtort ort) => _einwohner.Where(e => e.Wo(Jetzt)?.Ort == ort);

    /// <summary>Wer gerade in dieser Schänke oder Kirche ist (Name des Betriebs).</summary>
    public IEnumerable<Einwohner> Im(string betrieb) => _einwohner.Where(e => e.Wo(Jetzt)?.Betrieb == betrieb);

    /// <summary>Wer gerade in diesem Haus ist (Bewohner, Gesinde im Dienst).</summary>
    public IEnumerable<Einwohner> ImHaus(int haus) => _einwohner.Where(e => e.Wo(Jetzt) is { Ort: Stadtort.Heim } a && a.Haus == haus);

    /// <summary>Wer gerade unterwegs ist – für die Passanten in den Gassen und auf der Landstraße.</summary>
    public IEnumerable<Einwohner> Unterwegs => _einwohner.Where(e => e.IstUnterwegs(Jetzt));

    /// <summary>Alle Häuser, in denen Einwohner wohnen (einfache Wohnhäuser, Bürgerhäuser, Ratsherrenhäuser).</summary>
    public IReadOnlyList<Stadthaus> Wohnhaeuser => _haeuser;

    /// <summary>Wer in diesem Haus wohnt.</summary>
    public IEnumerable<Einwohner> Bewohner(int haus) => _einwohner.Where(e => e.Wohnhaus == haus);

    public Einwohner? Nummer(int nummer) => _einwohner.FirstOrDefault(e => e.Nummer == nummer);

    /// <summary>Durchschnittliche Stimmung der Stadt (100 = alle zufrieden).</summary>
    public double Stimmung => _einwohner.Count == 0 ? 100 : _einwohner.Average(e => e.Stimmung);

    /// <summary>Gewöhnliche Einwohner, die im letzten Jahr gestorben bzw. zugezogen sind.</summary>
    public int VerstorbenImVorjahr => _verstorben;
    public int ZugezogenImVorjahr => _zugezogen;

    /// <summary>Bezeichnung der Rolle für diesen Einwohner, z. B. „Magd" statt „Knecht" für eine Frau.</summary>
    public string RollenName(Einwohner e) =>
        e.Geschlecht == Geschlecht.Frau && Daten.Rollen.TryGetValue(e.Rolle, out var r) && r.Frau.Length > 0 ? r.Frau : e.Rolle;

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Jede Spielminute (aus <see cref="Stadtwirtschaft"/>): Beim ersten Mal ziehen die Einwohner ein,
    /// danach planen alle zu jeder vollen Stunde, was sie in dieser Stunde tun.
    /// </summary>
    internal void Minute(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        if (!_gegruendet)
        {
            if (_w.Konten.Count == 0) return; // ohne Häuser (z. B. Tests) wartet die Stadt noch
            Gruenden(zeit.Jahr);
            Planen(zeit);
            return;
        }
        if (zeit.MinuteImTag % 60 == 0)
            Planen(zeit);
    }

    /// <summary>Jahresende = Ende des Spieltags: Gewöhnliche Einwohner altern und sterben; neue ziehen zu; alle schlafen aus.</summary>
    internal void Jahresende(int neuesJahr)
    {
        if (!IstAktiv || !_gegruendet) return;
        _verstorben = 0;
        _zugezogen = 0;
        if (_w.Familien.IstAktiv)
            for (var i = 0; i < _einwohner.Count; i++)
            {
                var e = _einwohner[i];
                if (e.Herkunft != Herkunft.Stadt) continue; // Ratsherren und Kandidaten leben nach den Regeln von Rat und Familienbuch
                if (_zufall.NextDouble() >= _w.Familien.Sterberisiko(e.Volk, e.Alter(neuesJahr - 1))) continue;
                var neu = NeuerStadtbewohner(neuesJahr, zuzug: true); // an derselben Stelle – die Reihenfolge bleibt
                neu.Wohnhaus = e.Wohnhaus; // der Zuzügler bezieht die Kammer des Verstorbenen
                _einwohner[i] = neu;
                _verstorben++;
                _zugezogen++;
            }
        foreach (var e in _einwohner)
            Ausschlafen(e);
    }

    private void Gruenden(int jahr)
    {
        var d = Daten;
        var anzahl = d.JeHaus * _w.Konten.Count;
        for (var i = 0; i < anzahl; i++)
            _einwohner.Add(NeuerStadtbewohner(jahr, zuzug: false));
        HaushalteBilden();

        if (d.MitRatsherren && _w.Rat.IstAktiv)
            foreach (var r in _w.Rat.Ratsherren)
            {
                var teile = r.Name.Split(' ', 2);
                var vorname = teile[0];
                var familienname = teile.Length > 1 ? teile[1] : "";
                var geschlecht = _w.Familien.IstAktiv && _w.Familien.VolkDaten(r.Daten.Volk).VornamenFrau.Contains(vorname)
                    ? Geschlecht.Frau
                    : Geschlecht.Mann;
                // Ratsherren altern im Rat nicht – das Alter dient nur der Anzeige.
                var alter = 35 + _zufall.Next(26);
                var e = new Einwohner(_naechsteNummer++, Herkunft.Ratsherr, r.Name, vorname, familienname, r.Daten.Volk,
                                      geschlecht, jahr - alter, _w.Glaube.GlaubeVon(r), d.RolleRatsherr);
                e.Wohnhaus = NeuesHaus(Hausart.Ratsherrenhaus); // jeder Ratsherr hat sein eigenes Haus
                Ausschlafen(e);
                _einwohner.Add(e);
            }

        KandidatenAbgleichen();
        _gegruendet = true;
    }

    /// <summary>Die Brautschau ändert sich (Heirat, Weggang, Nachrücker) – die Einwohnerliste folgt ihr.</summary>
    private void KandidatenAbgleichen()
    {
        var d = Daten;
        if (!d.MitKandidaten || !_w.Familien.IstAktiv) return;
        var kandidaten = _w.Familien.Kandidaten;
        var nummern = kandidaten.Select(k => k.Person.Nummer.ToString()).ToHashSet();
        _einwohner.RemoveAll(e => e.Herkunft == Herkunft.Kandidat && !nummern.Contains(e.Quelle));
        var vorhanden = _einwohner.Where(e => e.Herkunft == Herkunft.Kandidat).Select(e => e.Quelle).ToHashSet();
        foreach (var k in kandidaten)
        {
            var quelle = k.Person.Nummer.ToString();
            if (vorhanden.Contains(quelle)) continue;
            var p = k.Person;
            var rolle = k.Stand > 0
                ? d.RolleKandidatAbBuerger
                : Gewichtet(d.Rollen.Where(r => r.Value.Ledig && r.Value.Gewicht > 0).Select(r => (r.Key, r.Value.Gewicht)));
            var e = new Einwohner(_naechsteNummer++, Herkunft.Kandidat, quelle, p.Vorname, p.Familienname, p.Volk,
                                  p.Geschlecht, p.Geburtsjahr, p.Glaube, rolle);
            // Kandidaten wohnen bei ihren Eltern: aus Bürger- und Patrizierfamilien im Bürgerhaus, sonst in einem einfachen Haus.
            e.Wohnhaus = HausFuer(k.Stand > 0 ? Hausart.Buergerhaus : Hausart.Wohnhaus, p.Nummer);
            Ausschlafen(e);
            _einwohner.Add(e);
        }
    }

    private Einwohner NeuerStadtbewohner(int jahr, bool zuzug)
    {
        var d = Daten;
        var volk = Gewichtet(d.Voelker.Where(v => v.Value > 0).Select(v => (v.Key, v.Value)));
        var geschlecht = _zufall.NextDouble() < 0.5 ? Geschlecht.Mann : Geschlecht.Frau;

        var vd = _w.Familien.IstAktiv ? _w.Familien.VolkDaten(volk) : null;
        var von = vd?.Volljaehrig ?? 16;
        var sterbealter = vd?.Sterbealter ?? 64;
        // Zuzügler sind eher jung; bei Spielstart ist die Stadt durchmischt.
        var bis = von + (int)((sterbealter - von) * (zuzug ? 0.5 : 0.85));
        var alter = von + _zufall.Next(Math.Max(1, bis - von + 1));

        var nummer = _naechsteNummer++;
        var (vorname, familienname) = Namen(vd, geschlecht, nummer);
        var glaube = Glaubenswurf(volk);
        var rolle = Gewichtet(d.Rollen.Where(r => r.Value.Gewicht > 0).Select(r => (r.Key, r.Value.Gewicht)));
        var e = new Einwohner(nummer, Herkunft.Stadt, "", vorname, familienname, volk, geschlecht, jahr - alter, glaube, rolle);
        Ausschlafen(e);
        return e;
    }

    /// <summary>Teilt die gewöhnlichen Einwohner in Haushalte zu je 4–6 Personen (einfache Wohnhäuser) und legt die Bürgerhäuser an.</summary>
    private void HaushalteBilden()
    {
        var d = Daten;
        var offen = 0;
        foreach (var e in _einwohner.Where(e => e.Herkunft == Herkunft.Stadt))
        {
            if (offen == 0)
            {
                NeuesHaus(Hausart.Wohnhaus);
                offen = d.HaushaltVon + _zufall.Next(d.HaushaltBis - d.HaushaltVon + 1);
            }
            e.Wohnhaus = _haeuser[^1].Nummer;
            e.Haus = e.Wohnhaus;
            offen--;
        }
        for (var i = 0; i < d.Buergerhaeuser; i++)
            NeuesHaus(Hausart.Buergerhaus);
    }

    private int NeuesHaus(Hausart art)
    {
        var haus = new Stadthaus(_haeuser.Count + 1, art);
        _haeuser.Add(haus);
        return haus.Nummer;
    }

    /// <summary>Ein Haus dieser Art, fest nach einer Nummer gewählt (ohne Würfel). 0, wenn es keines gibt.</summary>
    private int HausFuer(Hausart art, int nummer)
    {
        var passend = _haeuser.Where(h => h.Art == art).ToList();
        return passend.Count == 0 ? 0 : passend[nummer % passend.Count].Nummer;
    }

    /// <summary>Wo Knechte und Mägde dienen: in einem Bürger- oder Ratsherrenhaus, fest nach ihrer Nummer.</summary>
    private int Dienstherr(Einwohner e)
    {
        var herrschaft = _haeuser.Where(h => h.Art != Hausart.Wohnhaus).ToList();
        return herrschaft.Count == 0 ? e.Wohnhaus : herrschaft[e.Nummer % herrschaft.Count].Nummer;
    }

    /// <summary>Name aus den Namenslisten des Volkes – kein ganzer Name doppelt, kein Familienname eines Hauses.</summary>
    private (string Vorname, string Familienname) Namen(VolkDaten? vd, Geschlecht geschlecht, int nummer)
    {
        if (vd is null) return ("Einwohner", nummer.ToString());
        var vornamen = geschlecht == Geschlecht.Frau ? vd.VornamenFrau : vd.VornamenMann;
        var haeuser = _w.Konten.Select(k => _w.Familien.Von(k)?.Nachname).Where(n => n is not null).ToHashSet();
        var namen = _einwohner.Select(e => e.Name).ToHashSet();
        (string, string) wahl = default;
        for (var versuch = 0; versuch < 10; versuch++)
        {
            wahl = (vornamen[_zufall.Next(vornamen.Count)], vd.Familiennamen[_zufall.Next(vd.Familiennamen.Count)]);
            if (!haeuser.Contains(wahl.Item2) && !namen.Contains($"{wahl.Item1} {wahl.Item2}")) break;
        }
        return wahl;
    }

    /// <summary>Konfession nach den Gewichten des Volkes (Abschnitt „glaube") – mit dem eigenen Generator.</summary>
    private Konfession Glaubenswurf(Volk volk)
    {
        if (_w.Daten.Glaube is not { } g || !g.Voelker.TryGetValue(volk, out var gewichte) || gewichte.Count == 0)
            return _w.Glaube.Startglaube(volk);
        return Gewichtet(gewichte.Select(x => (x.Key, x.Value)));
    }

    /// <summary>Wählt nach Gewichten. Die Liste wird sortiert, damit das Ergebnis nicht von der Reihenfolge im Wörterbuch abhängt.</summary>
    private T Gewichtet<T>(IEnumerable<(T Wert, double Gewicht)> eintraege) where T : notnull
    {
        var liste = eintraege.OrderBy(x => x.Wert.ToString(), StringComparer.Ordinal).ToList();
        var wurf = _zufall.NextDouble() * liste.Sum(x => x.Gewicht);
        foreach (var (wert, gewicht) in liste)
        {
            wurf -= gewicht;
            if (wurf < 0) return wert;
        }
        return liste[^1].Wert;
    }

    private void Ausschlafen(Einwohner e)
    {
        var streuung = Daten.StartStreuung;
        foreach (var b in Enum.GetValues<Beduerfnis>())
            e.Setzen(b, Daten.Beduerfnisse[b].Start + (streuung > 0 ? streuung * (2 * _zufall.NextDouble() - 1) : 0));
        e.Handlung = Handlung.Ruhen;
        e.Ort = Stadtort.Heim;
        e.Betrieb = null;
        e.Stelle = null;
        e.Haus = e.Wohnhaus;
        e.Weg = null; // über Nacht zu Hause
    }

    // ── Die stündliche Entscheidung ─────────────────────────────────────────

    private void Planen(Spielzeit zeit)
    {
        var d = Daten;
        var stunde = zeit.UhrzeitInMinuten / 60;
        KandidatenAbgleichen();

        var schaenken = _w.Gastwirtschaft.IstAktiv
            ? _w.Gastwirtschaft.Schaenken.OrderBy(b => b.Name, StringComparer.Ordinal).ToList()
            : new List<Betrieb>();
        var kirchen = _w.Kirchenwesen.IstAktiv
            ? _w.Kirchenwesen.Kirchen.OrderBy(b => b.Name, StringComparer.Ordinal).ToList()
            : new List<Betrieb>();
        var messe = _w.Kirchenwesen.IstAktiv && stunde == _w.Kirchenwesen.Daten.MesseUm ? d.Messe : 1;

        foreach (var e in _einwohner)
        {
            // Der Tag schreitet voran: Alle Bedürfnisse werden dringender (nach dem Volk gewichtet).
            foreach (var (b, bd) in d.Beduerfnisse)
                e.Setzen(b, e.Wert(b) + bd.Anstieg * d.Gewicht(e.Volk, b));

            var beste = Handlung.Ruhen;
            var besterNutzen = double.MinValue;
            foreach (var h in Enum.GetValues<Handlung>())
            {
                if (!Moeglich(h, stunde, schaenken)) continue;
                var nutzen = Nutzen(e, h, h == Handlung.Beten ? messe : 1);
                if (h == e.Handlung) nutzen += d.Beharrung;
                if (d.Rauschen > 0) nutzen *= 1 + d.Rauschen * (2 * _zufall.NextDouble() - 1);
                if (nutzen > besterNutzen)
                {
                    beste = h;
                    besterNutzen = nutzen;
                }
            }

            foreach (var (b, x) in d.Handlungen[beste].Stillt)
                e.Setzen(b, e.Wert(b) - x * (beste == Handlung.Beten ? messe : 1));
            var vorher = e.Aufenthalt;
            e.Handlung = beste;
            (e.Ort, e.Betrieb) = Ort(e, beste, schaenken, kirchen);
            e.Haus = e.Ort == Stadtort.Heim ? (beste == Handlung.Arbeiten && IstDienst(e) ? Dienstherr(e) : e.Wohnhaus) : 0;
            e.Stelle = e.Ort == Stadtort.Umland && d.Rollen.TryGetValue(e.Rolle, out var rolle) ? rolle.Stelle : null;
            if (e.Aufenthalt != vorher)
                e.Weg = NeuerWeg(vorher, e.Aufenthalt, zeit.VergangeneMinuten);
        }
    }

    private bool IstDienst(Einwohner e) => Daten.Rollen.TryGetValue(e.Rolle, out var r) && r.Dienst;

    /// <summary>Ein Weg beginnt kurz nach der vollen Stunde und dauert je nach Ziel: in der Stadt ~10 Minuten, zum Tor 15, ins Umland 30.</summary>
    private Weg NeuerWeg(Aufenthalt von, Aufenthalt nach, long stunde)
    {
        var w = Daten.Wege;
        var abmarsch = stunde + (w.Verzoegerung > 0 ? _zufall.Next(w.Verzoegerung + 1) : 0);
        int dauer;
        if (von.Ort == Stadtort.Umland || nach.Ort == Stadtort.Umland) dauer = w.Umland;
        else if (von.Ort == Stadtort.Tor || nach.Ort == Stadtort.Tor) dauer = w.Tor;
        else dauer = w.Stadt - w.Streuung + _zufall.Next(2 * w.Streuung + 1);
        return new Weg(von, nach, abmarsch, abmarsch + dauer);
    }

    private bool Moeglich(Handlung h, int stunde, List<Betrieb> schaenken)
    {
        var hd = Daten.Handlungen[h];
        if (stunde < hd.Von || stunde >= hd.Bis) return false;
        return h != Handlung.Einkehren || schaenken.Count > 0;
    }

    /// <summary>Nutzen = gestillte Dringlichkeit, gewichtet nach dem Volk (mehr stillen als nötig bringt nichts).</summary>
    private double Nutzen(Einwohner e, Handlung h, double faktor)
    {
        var d = Daten;
        var summe = 0.0;
        foreach (var (b, x) in d.Handlungen[h].Stillt)
            summe += Math.Min(e.Wert(b), x * faktor) * d.Gewicht(e.Volk, b);
        return summe;
    }

    private (Stadtort, string?) Ort(Einwohner e, Handlung h, List<Betrieb> schaenken, List<Betrieb> kirchen) => h switch
    {
        Handlung.Arbeiten => Arbeitsort(e, schaenken, kirchen),
        Handlung.Einkaufen => (Stadtort.Markt, null),
        Handlung.Einkehren => (Stadtort.Schaenke, Stammhaus(e, schaenken)),
        Handlung.Beten => (Stadtort.Kirche, Stammkirche(e, kirchen)),
        Handlung.Flanieren => (Stadtort.Gasse, null),
        _ => (Stadtort.Heim, null),
    };

    private (Stadtort, string?) Arbeitsort(Einwohner e, List<Betrieb> schaenken, List<Betrieb> kirchen)
    {
        var ort = Daten.Rollen.TryGetValue(e.Rolle, out var r) ? r.Arbeitsort : Stadtort.Markt;
        return ort switch
        {
            Stadtort.Kirche => (ort, Stammkirche(e, kirchen)),
            Stadtort.Schaenke when schaenken.Count > 0 => (ort, Stammhaus(e, schaenken)),
            Stadtort.Schaenke => (Stadtort.Gasse, null),
            _ => (ort, null),
        };
    }

    /// <summary>Jeder hat seine Stammschänke – fest nach seiner Nummer, ohne Würfel (die Kopplung an die Anziehung folgt später).</summary>
    private static string? Stammhaus(Einwohner e, List<Betrieb> liste) => liste.Count == 0 ? null : liste[e.Nummer % liste.Count].Name;

    /// <summary>Kirche der eigenen Konfession, sonst eine fremde, sonst die Stadtkirche (null).</summary>
    private string? Stammkirche(Einwohner e, List<Betrieb> kirchen)
    {
        var eigene = kirchen.Where(k => _w.Glaube.GlaubeVon(k.Konto) == e.Glaube).ToList();
        return Stammhaus(e, eigene.Count > 0 ? eigene : kirchen);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal EinwohnerschaftStand Sichern() => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        Gegruendet = _gegruendet,
        NaechsteNummer = _naechsteNummer,
        Verstorben = _verstorben,
        Zugezogen = _zugezogen,
        Haeuser = _haeuser.ToList(),
        Einwohner = _einwohner.Select(e => new EinwohnerStand
        {
            Nummer = e.Nummer,
            Herkunft = e.Herkunft,
            Quelle = e.Quelle,
            Vorname = e.Vorname,
            Familienname = e.Familienname,
            Volk = e.Volk,
            Geschlecht = e.Geschlecht,
            Geburtsjahr = e.Geburtsjahr,
            Glaube = e.Glaube,
            Rolle = e.Rolle,
            Handlung = e.Handlung,
            Ort = e.Ort,
            Betrieb = e.Betrieb,
            Haus = e.Haus,
            Stelle = e.Stelle,
            Wohnhaus = e.Wohnhaus,
            Weg = e.Weg,
            Beduerfnisse = e.Beduerfnisse.ToList(),
        }).ToList(),
    };

    internal void Wiederherstellen(EinwohnerschaftStand s)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _gegruendet = s.Gegruendet;
        _naechsteNummer = s.NaechsteNummer;
        _verstorben = s.Verstorben;
        _zugezogen = s.Zugezogen;
        _einwohner.Clear();
        foreach (var x in s.Einwohner)
        {
            var e = new Einwohner(x.Nummer, x.Herkunft, x.Quelle, x.Vorname, x.Familienname, x.Volk, x.Geschlecht, x.Geburtsjahr, x.Glaube, x.Rolle)
            {
                Handlung = x.Handlung,
                Ort = x.Ort,
                Betrieb = x.Betrieb,
                Haus = x.Haus,
                Stelle = x.Stelle,
                Wohnhaus = x.Wohnhaus,
                Weg = x.Weg,
            };
            for (var i = 0; i < x.Beduerfnisse.Count && i < e.Beduerfnisse.Count; i++)
                e.Setzen((Beduerfnis)i, x.Beduerfnisse[i]);
            _einwohner.Add(e);
        }
        _haeuser.Clear();
        _haeuser.AddRange(s.Haeuser);
        if (_gegruendet && _haeuser.Count == 0)
            HaeuserNachruesten();
    }

    /// <summary>Spielstand aus der Zeit vor den Wohnhäusern: Häuser jetzt verteilen (einmalig, danach wie immer).</summary>
    private void HaeuserNachruesten()
    {
        HaushalteBilden();
        foreach (var e in _einwohner.Where(e => e.Herkunft == Herkunft.Ratsherr))
            e.Wohnhaus = NeuesHaus(Hausart.Ratsherrenhaus);
        foreach (var e in _einwohner.Where(e => e.Herkunft == Herkunft.Kandidat))
        {
            var buerger = e.Rolle == Daten.RolleKandidatAbBuerger;
            e.Wohnhaus = HausFuer(buerger ? Hausart.Buergerhaus : Hausart.Wohnhaus, int.TryParse(e.Quelle, out var n) ? n : e.Nummer);
        }
        foreach (var e in _einwohner.Where(e => e.Ort == Stadtort.Heim))
            e.Haus = e.Wohnhaus;
    }
}

/// <summary>Die Einwohnerschaft im Spielstand.</summary>
public sealed record EinwohnerschaftStand
{
    public long ZufallWuerfe { get; init; }
    public bool Gegruendet { get; init; }
    public int NaechsteNummer { get; init; } = 1;
    public int Verstorben { get; init; }
    public int Zugezogen { get; init; }
    /// <summary>Wohnhäuser (leer in Spielständen von vor den Häusern – dann werden sie beim Laden verteilt).</summary>
    public List<Stadthaus> Haeuser { get; init; } = new();
    public List<EinwohnerStand> Einwohner { get; init; } = new();
}

public sealed record EinwohnerStand
{
    public int Nummer { get; init; }
    public Herkunft Herkunft { get; init; }
    public string Quelle { get; init; } = "";
    public string Vorname { get; init; } = "";
    public string Familienname { get; init; } = "";
    public Volk Volk { get; init; }
    public Geschlecht Geschlecht { get; init; }
    public int Geburtsjahr { get; init; }
    public Konfession Glaube { get; init; }
    public string Rolle { get; init; } = "";
    public Handlung Handlung { get; init; }
    public Stadtort Ort { get; init; }
    public string? Betrieb { get; init; }
    public int Haus { get; init; }
    public string? Stelle { get; init; }
    public int Wohnhaus { get; init; }
    public Weg? Weg { get; init; }
    /// <summary>Dringlichkeit je Bedürfnis in der Reihenfolge von <see cref="Beduerfnis"/>.</summary>
    public List<double> Beduerfnisse { get; init; } = new();
}
