using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Ein Pfad, wie ihn das Charakterfenster zeigt.</summary>
/// <param name="Aktiv">In diesen Pfad fließt die Erfahrung gerade.</param>
/// <param name="Punkte">Alle Punkte aus der Erfahrung dieses Pfads (genommene und freie).</param>
/// <param name="Frei">Punkte, die noch auf einen Knoten warten.</param>
/// <param name="BisNaechsterPunkt">Erfahrung, die zum nächsten Punkt fehlt (null: Der Baum ist voll).</param>
/// <param name="ErfahrungNaechsterPunkt">Was der nächste Punkt insgesamt kostet (für den Balken; null: Der Baum ist voll).</param>
public sealed record PfadZeile(Talent Pfad, string Name, bool Aktiv, double Erfahrung, int Punkte, int Frei, int Knoten,
    double? BisNaechsterPunkt, double? ErfahrungNaechsterPunkt);

/// <summary>
/// Die Lebensweise (Charaktersystem Teil 3a): Erfahrung je Pfad, Punkte, Knoten, Erbe und Vermächtnis.
///
/// Wer sammelt: das Oberhaupt (voll), Erbe und Ehepartner (<see cref="LebensweiseDaten.AnteilFamilie"/>). Gebucht wird der Grundwert am
/// Jahresende vor den Todesfällen, Taten sofort beim Ausgeben der Aktionspunkte (nur das Oberhaupt). Erfahrung fließt in den aktiven Pfad;
/// ein Wechsel kostet nichts, Gesammeltes und Knoten bleiben beim alten Pfad. Die Wirkung der Knoten zählt für das Haus, solange ihr Besitzer
/// Oberhaupt ist (<see cref="Lebenswirkung(Konto)"/>), die persönlichen (Meinung über die Person, eigene Krankheit) immer.
///
/// Beim Erbfall behält der Erbe seine eigenen Knoten und bekommt als Vermächtnis einen Anteil der Erfahrung des Verstorbenen in dessen
/// aktivem Pfad. Die Knoten des Toten gehen nicht über.
/// </summary>
public sealed partial class Familienbuch
{
    /// <summary>Die Daten der Lebensweise (null = ohne Lebensweise, etwa in alten Spielständen).</summary>
    public LebensweiseDaten? LebensweiseDaten => _w.Daten.Lebensweise is { IstAktiv: true } d ? d : null;
    public bool HatLebensweise => IstAktiv && LebensweiseDaten is not null;

    /// <summary>Die Knoten haben sich geändert (Knoten genommen, Erbfall): Die Betriebe des Hauses rechnen ihre Wirkungen neu.</summary>
    internal event Action<Konto>? LebenswirkungGeaendert;

    /// <summary>Sammelt diese Person Erfahrung der Lebensweise? Oberhaupt, Erbe und Ehepartner eines Hauses, solange sie leben.</summary>
    public bool HatLebensweg(Person p) =>
        HatLebensweise && p.Lebt && FamilieVon(p) is { } f && f.Konto.IstAktiv && (p == f.Oberhaupt || p == f.Ehepartner || p == Erbe(f));

    /// <summary>Anteil am Grundwert: Oberhaupt 1, Erbe und Ehepartner <see cref="LebensweiseDaten.AnteilFamilie"/>, sonst 0.</summary>
    public double LebenswegAnteil(Person p) =>
        !HatLebensweg(p) ? 0 : FamilieVon(p)!.Oberhaupt == p ? 1 : LebensweiseDaten!.AnteilFamilie;

    /// <summary>Der Pfad, in den die Erfahrung fließt: der gewählte, sonst der des stärksten Talents (bei Gleichstand der erste).</summary>
    public Talent AktiverPfad(Person p) =>
        p.Lebensweg.Pfad ?? TalentTexte.Alle.OrderByDescending(t => p.Talent(t)).ThenBy(t => t).First();

    /// <summary>Faktor auf jede Erfahrung dieser Person im Pfad: passende und hemmende Züge, Gelehrt, Bibliothek.</summary>
    public double ErfahrungFaktor(Person p, Talent pfad)
    {
        if (LebensweiseDaten is not { } d || !d.Pfade.TryGetValue(pfad, out var pd)) return 0;
        var faktor = 1.0;
        foreach (var z in p.Zuege)
        {
            if (pd.Passend.Contains(z.Kennung)) faktor *= d.ZugPassend;
            if (pd.Hemmend.Contains(z.Kennung)) faktor *= d.ZugHemmend;
            if (d.Zugfaktoren.TryGetValue(z.Kennung, out var zf)) faktor *= zf;
        }
        if (FamilieVon(p) is { } f)
            foreach (var (kennung, wf) in d.Wohnhaus)
                if (f.Konto.WohnhausEinrichtung.Contains(kennung)) faktor *= wf;
        return faktor;
    }

    /// <summary>Was die Person am Jahresende ohne Taten bekommt (für die Anzeige „etwa n Jahre“).</summary>
    public double ErfahrungJeJahr(Person p)
    {
        if (LebensweiseDaten is not { } d || LebenswegAnteil(p) is var anteil && anteil <= 0) return 0;
        var pfad = AktiverPfad(p);
        return (d.Grund + d.JeStern * p.Talent(pfad)) * anteil * ErfahrungFaktor(p, pfad);
    }

    /// <summary>Die fünf Pfade einer Person mit Erfahrung, Punkten und freien Punkten (leer ohne Lebensweise).</summary>
    public IReadOnlyList<PfadZeile> Pfade(Person p)
    {
        if (LebensweiseDaten is not { } d) return Array.Empty<PfadZeile>();
        var aktiv = AktiverPfad(p);
        return TalentTexte.Alle.Where(d.Pfade.ContainsKey).Select(t =>
        {
            var erfahrung = p.Lebensweg.Erfahrung(t);
            var punkte = d.PunkteAus(t, erfahrung);
            var knoten = KnotenImPfad(p, t);
            var voll = punkte >= d.Pfade[t].Knoten.Count;
            return new PfadZeile(t, d.Pfade[t].Name, t == aktiv, erfahrung, punkte, Math.Max(0, punkte - knoten), knoten,
                voll ? null : d.ErfahrungFuer(punkte + 1) - erfahrung, voll ? null : d.Kosten[Math.Min(punkte, d.Kosten.Count - 1)]);
        }).ToList();
    }

    private int KnotenImPfad(Person p, Talent pfad) =>
        LebensweiseDaten is { } d && d.Pfade.TryGetValue(pfad, out var pd) ? p.Lebensweg.Knoten.Count(pd.Knoten.ContainsKey) : 0;

    /// <summary>Freie Punkte der Person in diesem Pfad.</summary>
    public int FreiePunkte(Person p, Talent pfad) =>
        LebensweiseDaten is not { } d ? 0 : Math.Max(0, d.PunkteAus(pfad, p.Lebensweg.Erfahrung(pfad)) - KnotenImPfad(p, pfad));

    /// <summary>Warum dieses Haus der Person diesen Knoten jetzt nicht geben kann (null: Es geht).</summary>
    public string? KnotenVerboten(Konto konto, Person p, string kennung)
    {
        if (LebensweiseDaten is not { } d) return "In dieser Stadt gibt es keine Lebensweise.";
        if (d.Knoten(kennung) is not { } k) return $"Einen Knoten „{kennung}“ gibt es nicht.";
        if (Von(konto) is not { } f || !f.MitgliederListe.Contains(p)) return $"{p.Vorname} gehört nicht zum Haus {konto.Inhaber}.";
        if (!HatLebensweg(p)) return $"{p.Vorname} führt kein eigenes Leben im Haus – nur Oberhaupt, Erbe und Ehepartner lernen eine Lebensweise.";
        if (p.Lebensweg.HatKnoten(kennung)) return $"{p.Vorname} hat „{k.Daten.Name}“ schon.";
        if (d.Voraussetzung(k.Pfad, k.Daten) is { } v && !p.Lebensweg.HatKnoten(v))
            return $"Erst braucht es „{d.Pfade[k.Pfad].Knoten[v].Name}“.";
        if (FreiePunkte(p, k.Pfad) <= 0) return $"Kein freier Punkt im {d.Pfade[k.Pfad].Name} – es fehlt Erfahrung.";
        return null;
    }

    /// <summary>
    /// Der Baum eines Pfads für die <c>TalentbaumAnsicht</c> (wie der Einrichtungsbaum): Spalten, drei Reihen, alle offen (<see cref="Einrichtungsbaum.Stufe"/> 3);
    /// ein Knoten kostet einen Punkt (<see cref="BaumStueck.Preis"/> 1). Zustand: genommen, nehmbar (freier Punkt und Voraussetzung),
    /// nicht jetzt (Voraussetzung da, aber kein Punkt), gesperrt (Voraussetzung fehlt). Für fremde Personen (<paramref name="betrachter"/> ist ein
    /// anderes Haus) ist nichts nehmbar.
    /// </summary>
    public Einrichtungsbaum? Lebensbaum(Person p, Talent pfad, Konto? betrachter = null)
    {
        if (LebensweiseDaten is not { } d || !d.Pfade.TryGetValue(pfad, out var pd)) return null;
        var eigenes = betrachter is null || FamilieVon(p)?.Konto == betrachter;
        var stuecke = d.AlleKnoten.Where(x => x.Pfad == pfad).Select(x =>
        {
            var vor = d.Voraussetzung(pfad, x.Daten);
            var grund = FamilieVon(p) is { } f ? KnotenVerboten(f.Konto, p, x.Kennung) : "Die Person gehört zu keinem Haus.";
            if (grund is null && !eigenes) grund = $"{p.Vorname} gehört nicht zu Eurem Haus.";
            var zustand = p.Lebensweg.HatKnoten(x.Kennung) ? Baumzustand.Gekauft
                : vor is not null && !p.Lebensweg.HatKnoten(vor) ? Baumzustand.Gesperrt
                : grund is null ? Baumzustand.Kaufbar : Baumzustand.NichtJetzt;
            return new BaumStueck(x.Kennung, x.Daten.Name, x.Daten.Spalte, x.Daten.Reihe, 1, vor, x.Daten.Beschreibung, zustand,
                zustand == Baumzustand.Gekauft ? null : grund, 0, null);
        }).ToList();
        var reihen = pd.Knoten.Count == 0 ? 3 : pd.Knoten.Values.Max(k => k.Reihe);
        return new Einrichtungsbaum(pd.Spalten, reihen, reihen, stuecke);
    }

    /// <summary>Alle Knoten des Oberhaupts zusammen – sie wirken für das ganze Haus (ohne Lebensweise: ohne Wirkung).</summary>
    public LebenswegWirkung Lebenswirkung(Konto konto) =>
        HatLebensweise && Von(konto) is { } f ? f.Oberhaupt.Lebensweg.Wirkung : LebenswegWirkung.Keine;

    /// <summary>Die persönlichen Wirkungen (Meinung über die Person, eigene Krankheit) gelten immer – nicht nur für Oberhäupter.</summary>
    public LebenswegWirkung Lebenswirkung(Person p) => HatLebensweise ? p.Lebensweg.Wirkung : LebenswegWirkung.Keine;

    /// <summary>Werkstatt-Wirkungen der Knoten des Oberhaupts für einen Betrieb dieser Gebäudestufe (wie Einrichtung).</summary>
    public IEnumerable<EinrichtungWirkung> Werkstattwirkungen(Konto konto, int stufe) =>
        HatLebensweise && Von(konto) is { } f
            ? f.Oberhaupt.Lebensweg.Werkstatt.Where(w => stufe >= w.AbStufe).Select(w => w.Wirkung)
            : Enumerable.Empty<EinrichtungWirkung>();

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle
    // ═════════════════════════════════════════════════════════════════════════

    private Person? LebenswegPerson(Konto konto, int nummer, out string? grund)
    {
        grund = null;
        if (!HatLebensweise) { grund = "In dieser Stadt gibt es keine Lebensweise."; return null; }
        if (Von(konto) is not { } f) { grund = $"{konto.Inhaber} hat keine Familie."; return null; }
        var p = f.MitgliederListe.FirstOrDefault(x => x.Nummer == nummer);
        if (p is null) { grund = "Diese Person gehört nicht zum Haus."; return null; }
        if (!HatLebensweg(p)) { grund = $"{p.Vorname} führt kein eigenes Leben im Haus – nur Oberhaupt, Erbe und Ehepartner lernen eine Lebensweise."; return null; }
        return p;
    }

    /// <summary>Befehl <see cref="LebenswegWaehlen"/>: Ab jetzt fließt die Erfahrung in diesen Pfad (0 AP, Gesammeltes bleibt).</summary>
    internal Befehlsergebnis PfadWaehlen(Konto konto, int nummer, Talent pfad)
    {
        if (LebenswegPerson(konto, nummer, out var grund) is not { } p) return Befehlsergebnis.Abgelehnt(grund!);
        var d = LebensweiseDaten!;
        if (!d.Pfade.TryGetValue(pfad, out var pd)) return Befehlsergebnis.Abgelehnt("Diesen Pfad gibt es nicht.");
        if (p.Lebensweg.Pfad == pfad) return Befehlsergebnis.Ok($"{p.Vorname} folgt schon dem {pd.Name}.");
        p.Lebensweg.Pfad = pfad;
        return Befehlsergebnis.Ok($"{p.Vorname} folgt nun dem {pd.Name}. Was {(p.Geschlecht == Geschlecht.Frau ? "sie" : "er")} sonst gelernt hat, bleibt.");
    }

    /// <summary>Befehl <see cref="Wirtschaft.KnotenWaehlen"/>: einen Knoten mit einem freien Punkt seines Pfads nehmen (0 AP, für immer).</summary>
    internal Befehlsergebnis KnotenNehmen(Konto konto, int nummer, string kennung)
    {
        if (LebenswegPerson(konto, nummer, out var grund) is not { } p) return Befehlsergebnis.Abgelehnt(grund!);
        if (KnotenVerboten(konto, p, kennung) is { } verboten) return Befehlsergebnis.Abgelehnt(verboten);
        var d = LebensweiseDaten!;
        var k = d.Knoten(kennung)!.Value;
        p.Lebensweg.KnotenHinzufuegen(kennung, d);
        LebenswirkungGeaendert?.Invoke(konto);
        return Befehlsergebnis.Ok($"{p.Vorname} lernt „{k.Daten.Name}“: {k.Daten.Beschreibung}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Erfahrung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Erfahrung in den aktiven Pfad buchen; beim ersten Mal wird der Pfad des stärksten Talents festgehalten.</summary>
    private void ErfahrungBuchen(Person p, double roh)
    {
        if (roh <= 0) return;
        var pfad = AktiverPfad(p);
        p.Lebensweg.Pfad ??= pfad;
        p.Lebensweg.ErfahrungDazu(pfad, roh * ErfahrungFaktor(p, pfad)); // ein neuer Punkt zeigt sich im Charakterfenster, ohne Meldung
    }

    /// <summary>
    /// Eine Handlung hat Aktionspunkte gekostet (aus Stadtwirtschaft.Ausfuehren): Ist sie eine Tat des aktiven Pfads des Oberhaupts,
    /// gibt es <see cref="LebensweiseDaten.JeAp"/> je AP. Üben zählt, wenn das geübte Talent das des aktiven Pfads ist.
    /// </summary>
    internal void TatGebucht(Konto konto, Befehl befehl, int ap)
    {
        if (ap <= 0 || LebensweiseDaten is not { } d || Von(konto) is not { } f || !HatLebensweg(f.Oberhaupt)) return;
        var o = f.Oberhaupt;
        var pfad = AktiverPfad(o);
        if (!d.Pfade.TryGetValue(pfad, out var pd)) return;
        var typ = befehl.GetType().Name;
        var art = befehl switch
        {
            Aktivitaet a => a.Art,
            Beauftragen b => b.Art.ToString(),
            _ => null,
        };
        var passt = befehl is TalentTrainieren t ? t.Talent == pfad
            : pd.Taten.Contains(typ) || (art is not null && pd.Taten.Contains($"{typ}:{art}"));
        if (passt) ErfahrungBuchen(o, d.JeAp * ap);
    }

    /// <summary>Am Jahresende (vor den Todesfällen): Grundwert und Sterne für Oberhaupt, Erbe und Ehepartner.</summary>
    internal void LebensweiseImJahreswechsel(Familie f)
    {
        if (LebensweiseDaten is not { } d) return;
        var personen = new List<Person> { f.Oberhaupt };
        if (Erbe(f) is { } erbe && !personen.Contains(erbe)) personen.Add(erbe);
        if (f.Ehepartner is { } e && !personen.Contains(e)) personen.Add(e);
        foreach (var p in personen.Where(HatLebensweg))
        {
            var pfad = AktiverPfad(p);
            ErfahrungBuchen(p, (d.Grund + d.JeStern * p.Talent(pfad)) * LebenswegAnteil(p));
        }
    }

    /// <summary>Vermächtnis: Der Erbe bekommt einen Anteil der Erfahrung des Verstorbenen in dessen aktivem Pfad.</summary>
    private void Vermaechtnis(Familie f, Person tot, Person erbe)
    {
        LebenswirkungGeaendert?.Invoke(f.Konto); // ab jetzt wirken die Knoten des Erben
        if (LebensweiseDaten is not { } d || d.Vermaechtnis <= 0) return;
        var pfad = tot.Lebensweg.Pfad ?? AktiverPfad(tot);
        var wert = tot.Lebensweg.Erfahrung(pfad) * d.Vermaechtnis;
        if (wert > 0)
        {
            erbe.Lebensweg.ErfahrungDazu(pfad, wert);
            Ereignis(f.Konto, $"Vermächtnis: {erbe.Vorname} erbt einen Teil dessen, was {tot.Vorname} im {d.Pfade[pfad].Name} gelernt hat " +
                              $"({Zahlenformat.Ganz(wert)} Erfahrung).", person: erbe, wichtigkeit: Meldungswichtigkeit.Gering);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    private LebenswegStand? LebenswegSichern(Person p) =>
        !HatLebensweise || p.Lebensweg.IstLeer ? null : new LebenswegStand
        {
            Pfad = p.Lebensweg.Pfad,
            Erfahrung = TalentTexte.Alle.Select(p.Lebensweg.Erfahrung).ToList(),
            Knoten = p.Lebensweg.Knoten.ToList(),
        };

    private void LebenswegWiederherstellen(Person p, LebenswegStand? s)
    {
        if (s is null) return;
        p.Lebensweg.Pfad = s.Pfad;
        foreach (var t in TalentTexte.Alle.Where(t => (int)t < s.Erfahrung.Count))
            p.Lebensweg.ErfahrungSetzen(t, s.Erfahrung[(int)t]);
        foreach (var k in s.Knoten) p.Lebensweg.KnotenHinzufuegen(k, LebensweiseDaten);
    }
}
