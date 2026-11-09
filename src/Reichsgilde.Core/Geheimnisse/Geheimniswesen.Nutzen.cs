using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Geheimnisse;

/// <summary>
/// Was man mit einem Geheimnis tun kann (Befehl <see cref="GeheimnisNutzen"/>), der Spitzel auf einen Ratsherrn, das Beichtwissen und die
/// Verfahren gegen Ratsherren am Gerichtstag.
/// </summary>
public sealed partial class Geheimniswesen
{
    /// <summary>Aktionspunkte für diese Nutzung (aus "geheimnisse.ap").</summary>
    public int ApKosten(Geheimnisnutzung n) => _d?.Ap.GetValueOrDefault(n) ?? 0;

    /// <summary>Warum dieses Haus das Geheimnis so nicht nutzen kann (null = es kann). Aktionspunkte prüft die Stadtwirtschaft.</summary>
    /// <param name="betrag">Nur beim Erpressen um Gulden: das verlangte Schweigegeld.</param>
    public string? NutzenVerboten(Konto haus, Geheimnis g, Geheimnisnutzung n, double betrag = 1)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Geheimnisse.";
        if (IstEigen(haus, g)) return "Das ist ein Geheimnis eures eigenen Hauses.";
        if (g.Oeffentlich) return "Das weiß schon die ganze Stadt – es taugt zu nichts mehr.";
        if (Wissen(haus, g) is not { } w) return "Davon wisst ihr nichts.";
        if (w.AusBeichte) return "Ihr wisst es nur aus der Beichte – erst müsst ihr das Beichtgeheimnis brechen.";
        var inhaber = Inhaber(g);
        if (inhaber is null or Person { Lebt: false }) return "Wer es hütete, lebt nicht mehr.";
        var hausDes = HausVon(g);
        if (inhaber is Person && hausDes is not { IstAktiv: true }) return "Sein Haus ist nicht mehr im Spiel.";
        var waehrung = _w.Daten.Allgemein.Waehrung;
        switch (n)
        {
            case Geheimnisnutzung.Gulden:
                if (!_w.Gauner.IstAktiv) return "In dieser Stadt gibt es keine Unterwelt, die Erpresserbriefe überbringt.";
                if (hausDes is null) return $"{inhaber.Name} hat kein Geld, das ihm gehörte – bei einem Ratsherrn erpresst man die Stimme.";
                if (betrag <= 0 || !double.IsFinite(betrag)) return "Wie viel Schweigegeld soll es denn sein?";
                if (_w.Gauner.ForderungAn(hausDes, haus) is not null) return $"{hausDes.Inhaber} hat schon einen Brief von euch – wartet die Antwort ab.";
                if (_w.Justiz.IstAktiv && _w.Justiz.VerfahrenGegen(haus, hausDes) is not null) // wie beim Erpresserbrief mit Beweisen
                    return $"Eure Anklage gegen {hausDes.Inhaber} läuft schon – zum Erpressen ist es zu spät.";
                return null;
            case Geheimnisnutzung.Ratsstimme:
                if (!_w.Rat.IstAktiv) return "In dieser Stadt gibt es keinen Stadtrat.";
                if (SitzVon(g) is not { } sitz)
                    return inhaber is Ratsherr ? $"{inhaber.Name} hat zur Zeit kein Amt und keine Stimme im Rat." : $"{hausDes!.Inhaber} hat keinen Sitz im Rat.";
                if (w.GenutztIm == Jahr) return "Mit diesem Geheimnis habt ihr dieses Jahr schon eine Stimme erpresst.";
                if (ErpresserVon(sitz) is { } e)
                    return e == haus ? "Diese Stimme gehört in der nächsten Sitzung schon euch." : "Diese Stimme hat schon ein anderes Haus in der Hand.";
                if (_w.Rat.AmtVon(haus) == sitz) return "Das ist euer eigener Sitz.";
                return null;
            case Geheimnisnutzung.Anzeige:
                if (!_w.Justiz.IstAktiv) return "In dieser Stadt gibt es kein Gericht.";
                if (KeinVergehen(g) is { } keines) return keines;
                var gebuehr = _w.Justiz.Gerichtsgebuehr(haus);
                if (haus.Vermoegen < gebuehr) return $"Die Gerichtsgebühr beträgt {Zahlenformat.Ganz(gebuehr)} {waehrung}.";
                if (hausDes is not null)
                {
                    if (_w.Justiz.VerfahrenGegen(haus, hausDes) is not null) return $"Gegen {hausDes.Inhaber} läuft schon eine Anklage von euch. Verhandelt wird am Jahresende.";
                    if (_w.Justiz.VorsitzFuer(haus, hausDes) is null) return "Es gibt niemanden, der den Vorsitz führen könnte.";
                }
                else if (inhaber is Ratsherr r)
                {
                    if (_verfahren.Any(v => v.Anklaeger == haus && v.Beklagter == r)) return $"Eure Anzeige gegen {r.Name} läuft schon. Verhandelt wird am Jahresende.";
                    if (VorsitzGegen(haus, r) is null) return "Es gibt niemanden, der den Vorsitz führen könnte.";
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Warum dieses Geheimnis vor Gericht kein Vergehen ist (null = es ist eines): Schulden nie. Ketzerei nur, wenn ein Stadtglaube gilt und
    /// der Inhaber sich nach außen zu ihm bekennt, heimlich aber anderes glaubt – wer offen einen anderen Glauben lebt, ist kein Ketzer
    /// (Entscheidung des Nutzers 09.11.2026).
    /// </summary>
    public string? KeinVergehen(Geheimnis g)
    {
        var a = Daten.Art(g.Art);
        if (a.Beweiskraft <= 0) return $"{a.Name} ist kein Vergehen – anzeigen kann man es nicht. Ihr könnt erpressen oder bloßstellen.";
        if (!a.NurMitStadtglaube) return null;
        if (!_w.Glaube.IstAktiv || _w.Glaube.Stadtglaube is not { } stadt) return $"{a.Name} ist nur strafbar, wenn ein Stadtglaube gilt.";
        if (Inhaber(g) is { } c && GlaubeVon(c) != stadt) return $"{c.Name} bekennt sich nicht zum Stadtglauben – kein Ketzer.";
        return null;
    }

    /// <summary>Befehl <see cref="GeheimnisNutzen"/> (aus Stadtwirtschaft.Ausfuehren).</summary>
    internal Befehlsergebnis Nutzen(Konto haus, int nummer, Geheimnisnutzung n, double betrag)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Geheimnisse.");
        if (Nummer(nummer) is not { } g) return Befehlsergebnis.Abgelehnt("Dieses Geheimnis gibt es nicht (mehr).");
        if (NutzenVerboten(haus, g, n, betrag) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        return n switch
        {
            Geheimnisnutzung.Gulden => Erpressen(haus, g, betrag),
            Geheimnisnutzung.Ratsstimme => StimmeErpressen(haus, g),
            Geheimnisnutzung.Anzeige => Anzeigen(haus, g),
            _ => Blossstellen(haus, g),
        };
    }

    /// <summary>Erpressen um Gulden: der vorhandene Erpresserbrief (<see cref="Gauner.Unterwelt"/>), mit dem Geheimnis statt der Beweise.</summary>
    private Befehlsergebnis Erpressen(Konto haus, Geheimnis g, double betrag)
    {
        var opfer = HausVon(g)!;
        var inhaber = Inhaber(g)!;
        var ergebnis = _w.Gauner.GeheimnisErpressen(haus, opfer, betrag, g.Nummer, $"{inhaber.Name}: {Beschreibung(g)}", _zufall);
        if (!ergebnis.Erfolg) return ergebnis;
        Wissen(haus, g)!.BriefIm = Jahr;
        InhaberStress(g, Daten.Stress.Erpresst, $"Erpresst: {Beschreibung(g)}");
        return ergebnis;
    }

    /// <summary>
    /// Erpressen um die Ratsstimme: In der nächsten Sitzung stimmt der Sitz des Inhabers bei allen Punkten wie der Erpresser (siehe
    /// <see cref="Stadtrat.StimmSitz"/>). Einmal je Geheimnis und Jahr. Es bleibt eine Spur „Erpressung“, der Inhaber denkt schlechter über den Erpresser.
    /// </summary>
    private Befehlsergebnis StimmeErpressen(Konto haus, Geheimnis g)
    {
        var sitz = SitzVon(g)!;
        var inhaber = Inhaber(g)!;
        var opferHaus = sitz.Ratsherr is null ? sitz.Konto : null;
        _stimmen.Add(new ErpressteStimme(haus, sitz.Ratsherr, opferHaus, g.Nummer, Jahr));
        Wissen(haus, g)!.GenutztIm = Jahr;
        if (_w.Justiz.TatMelden(haus, Vergehen.Erpressung, inhaber.Name, 0) is { } spur && opferHaus is not null)
            _w.Justiz.BeweisGeben(opferHaus, spur); // der Erpresste weiß, wer ihn zwingt
        var ueber = _w.Meinungen.HausKennung(haus);
        _w.Meinungen.TatEintragen(inhaber.Kennung, ueber, "erpressung", "Hat ihn erpresst", Daten.MeinungErpresser);
        if (opferHaus is not null && _w.Meinungen.Oberhaupt(opferHaus) is { } o && o.Kennung != inhaber.Kennung)
            _w.Meinungen.TatEintragen(o.Kennung, ueber, "erpressung", "Hat das Haus erpresst", Daten.MeinungErpresser);
        InhaberStress(g, Daten.Stress.Erpresst, "Um die Ratsstimme erpresst");
        if (opferHaus is not null)
            _w.Melden(Meldungsart.Sonstiges, $"{haus.Inhaber} weiß von „{Beschreibung(g)}“ ({inhaber.Name}) und verlangt eure Stimme: In der nächsten Ratssitzung " +
                                              $"stimmt {opferHaus.Inhaber} bei allen Punkten wie {haus.Inhaber}.", haus: opferHaus, vertraulich: true);
        return Befehlsergebnis.Ok($"{inhaber.Name} gibt nach: In der nächsten Ratssitzung stimmt {sitz.Inhaber} ({sitz.Name}) bei allen Punkten wie ihr. " +
                                  "Die Erpressung ist eine Spur gegen euch.");
    }

    /// <summary>
    /// Anzeige vor Gericht: gegen ein Haus ein gewöhnliches Verfahren, in dem das Geheimnis als Beweis zählt; gegen einen Ratsherrn ein
    /// eigenes. Das Geheimnis ist danach öffentlich.
    /// </summary>
    private Befehlsergebnis Anzeigen(Konto haus, Geheimnis g)
    {
        var inhaber = Inhaber(g)!;
        var art = Daten.Art(g.Art);
        Befehlsergebnis ergebnis;
        if (HausVon(g) is { } beklagter)
            ergebnis = _w.Justiz.AnzeigeErheben(haus, beklagter, g.Nummer, g.Art, $"{art.Name} ({inhaber.Name})", _zufall);
        else
        {
            var r = (Ratsherr)inhaber;
            var gebuehr = _w.Justiz.Gerichtsgebuehr(haus);
            var vorsitz = VorsitzGegen(haus, r)!;
            haus.Gerichtskosten(gebuehr);
            _w.Kasse.Buchen(Kassenposten.Gebuehren, gebuehr);
            var richter = new List<Richter> { vorsitz };
            richter.AddRange(SchoeffenGegen(vorsitz, r).Select(x => new Richter("Schöffe", x, null)));
            _verfahren.Add(new RatsherrVerfahren(haus, r, g.Nummer, g.Art, Jahr, richter));
            _w.Melden(Meldungsart.Anklage, $"{haus.Inhaber} zeigt den Ratsherrn {r.Name} an: {art.Name}. Es richten {string.Join(", ", richter.Select(x => x.Name))}. " +
                                            "Gerichtstag ist am Jahresende.");
            ergebnis = Befehlsergebnis.Ok($"Anzeige gegen {r.Name} erhoben. Schöffen: {string.Join(" und ", richter.Skip(1).Select(x => x.Name))}.");
        }
        if (!ergebnis.Erfolg) return ergebnis;
        g.Oeffentlich = true;
        InhaberStress(g, Daten.Stress.Blossgestellt, $"Angezeigt: {art.Name}");
        return ergebnis;
    }

    /// <summary>
    /// Bloßstellen: Das Geheimnis wird öffentlich, ohne Gericht und ohne Spur. Ansehen des Hauses (beim Ratsherrn: seine Wahlpunkte) für
    /// <see cref="GeheimnisDaten.AnsehenJahre"/>, dazu je Art Meinungen (Ehepartner, Ratsherren des Glaubens, Angehörige des Opfers) und bei
    /// Schulden der Kreditrahmen. Der Inhaber denkt schlechter über den, der ihn bloßstellt.
    /// </summary>
    private Befehlsergebnis Blossstellen(Konto haus, Geheimnis g)
    {
        var d = Daten;
        var a = d.Art(g.Art);
        var inhaber = Inhaber(g)!;
        var hausDes = HausVon(g);
        g.Oeffentlich = true;
        if (a.Ansehen > 0 || g.Art == Geheimnisart.Schulden && a.KreditFaktor < 1)
            _schande.Add(new Schande(hausDes, inhaber as Ratsherr, g.Art, a.Ansehen, Jahr + d.AnsehenJahre - 1));
        var mw = _w.Meinungen;
        switch (g.Art)
        {
            case Geheimnisart.Ehebruch when inhaber is Person p && _w.Familien.FamilieVon(p) is { } f && EhepartnerVon(f, p) is { } partner && a.MeinungEhepartner < 0:
                mw.TatEintragen(partner.Kennung, p.Kennung, "ehebruch", "Ehebruch", a.MeinungEhepartner);
                break;
            case Geheimnisart.Ketzerei when _w.Glaube.IstAktiv && GlaubeVon(inhaber) is { } glaube && a.MeinungGlaube < 0:
                foreach (var r in _w.Rat.Ratsherren.Where(r => r != inhaber && _w.Glaube.GlaubeVon(r) == glaube))
                    mw.TatEintragen(r.Kennung, inhaber.Kennung, "ketzerei", "Ein heimlicher Ketzer", a.MeinungGlaube);
                break;
            case Geheimnisart.Mord when g.Opfer is int opfer && _w.Familien.PersonNummer(opfer) is { } tot && _w.Familien.FamilieVon(tot) is { } familie && a.MeinungAngehoerige < 0:
                foreach (var x in familie.Lebende.ToList())
                    mw.TatEintragen(x.Kennung, inhaber.Kennung, "mord", $"Mörder von {tot.Vorname}", a.MeinungAngehoerige);
                break;
        }
        mw.TatEintragen(inhaber.Kennung, mw.HausKennung(haus), "blossgestellt", "Hat ihn bloßgestellt", d.MeinungBlosssteller);
        InhaberStress(g, d.Stress.Blossgestellt, $"Bloßgestellt: {a.Name}");
        var folgen = new List<string>();
        if (a.Ansehen > 0) folgen.Add($"{(hausDes is null ? "Wahlpunkte" : "Ansehen")} −{Zahlenformat.Ganz(a.Ansehen)} bis Ende {Jahr + d.AnsehenJahre - 1}");
        if (g.Art == Geheimnisart.Schulden && a.KreditFaktor < 1) folgen.Add($"die Geldleiher leihen {Zahlenformat.Ganz((1 - a.KreditFaktor) * 100)} % weniger");
        _w.Melden(Meldungsart.Sonstiges, $"{haus.Inhaber} stellt {inhaber.Name} bloß: {Beschreibung(g)}! Die ganze Stadt redet davon" +
                                          (folgen.Count > 0 ? $" – {string.Join(", ", folgen)}." : "."),
            haus: hausDes, person: inhaber as Person, wichtigkeit: Meldungswichtigkeit.Hoch);
        return Befehlsergebnis.Ok($"Ihr stellt {inhaber.Name} bloß: {Beschreibung(g)}. Ohne Gericht und ohne Spur – aber {inhaber.Name} wird es euch nicht vergessen.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Spitzel auf Ratsherren, Beichte, Briefe, Sitzung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Warum dieses Haus keinen Spitzel auf den Ratsherrn ansetzen kann (null = es kann).</summary>
    public string? RatsspitzelVerboten(Konto haus, Ratsherr r)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Geheimnisse, die ein Spitzel finden könnte.";
        if (!_w.Justiz.IstAktiv) return "In dieser Stadt gibt es kein Gericht.";
        if (HatRatsspitzel(haus, r)) return $"Auf {r.Name} ist dieses Jahr schon ein Spitzel von euch angesetzt.";
        var kosten = _w.Justiz.Daten.SpitzelKosten;
        if (haus.Vermoegen < kosten) return $"Ein Spitzel kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung} – so viel habt ihr nicht.";
        return null;
    }

    /// <summary>Befehl <see cref="SpitzelAnsetzen"/> mit „R:Name“: bis Jahresende, Bericht am Gerichtstag, nur Geheimnisse.</summary>
    internal Befehlsergebnis RatsspitzelAnsetzen(Konto haus, string name)
    {
        if (_w.Rat.RatsherrNamens(name) is not { } r) return Befehlsergebnis.Abgelehnt($"Einen Ratsherrn „{name}“ gibt es nicht.");
        if (RatsspitzelVerboten(haus, r) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var kosten = _w.Justiz.Daten.SpitzelKosten;
        haus.Gerichtskosten(kosten);
        _ratsspitzel.Add((haus, r));
        return Befehlsergebnis.Ok($"Ein Spitzel heftet sich an die Fersen von {r.Name}. Bericht am Jahresende – über Geheimnisse, nicht über Taten.");
    }

    /// <summary>Geheimnisse dieses Hauses (aller seiner Personen), die der Prediger nur aus der Beichte kennt.</summary>
    public IEnumerable<Geheimnis> Beichtwissen(Konto prediger, Konto gegen) =>
        _geheimnisse.Where(g => HausVon(g) == gegen && Wissen(prediger, g) is { AusBeichte: true });

    /// <summary>Geheimnisse dieses Ratsherrn, die der Prediger nur aus der Beichte kennt.</summary>
    public IEnumerable<Geheimnis> Beichtwissen(Konto prediger, Ratsherr r) => Von(r).Where(g => Wissen(prediger, g) is { AusBeichte: true });

    /// <summary>Das Beichtgeheimnis ist gebrochen: Diese Geheimnisse kann der Prediger nun nutzen.</summary>
    internal void BeichteOffenbaren(Konto prediger, IEnumerable<Geheimnis> liste)
    {
        foreach (var g in liste.ToList())
            if (Wissen(prediger, g) is { } w) w.AusBeichte = false;
    }

    /// <summary>Das Opfer hat Schweigegeld gezahlt: Der Erpresser hält Wort und vergisst, was er wusste.</summary>
    internal void Vergessen(Konto erpresser, int nummer) => _wissen.RemoveAll(w => w.Haus == erpresser && w.Geheimnis == nummer);

    /// <summary>Am Ende der Ratssitzung: Die erpressten Stimmen sind abgegeben.</summary>
    internal void SitzungVorbei() => _stimmen.Clear();

    // ═════════════════════════════════════════════════════════════════════════
    //  Gericht gegen Ratsherren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wer den Vorsitz hätte: der Stadtrichter – ist er Ankläger oder selbst angezeigt, der Bürgermeister.</summary>
    private Richter? VorsitzGegen(Konto anklaeger, Ratsherr beklagter)
    {
        foreach (var (amtName, rolle) in new[] { ("Stadtrichter", "Vorsitz (Stadtrichter)"), ("Bürgermeister", "Vorsitz (Bürgermeister)") })
        {
            if (_w.Rat.AmtNamens(amtName) is not { IstBesetzt: true } amt) continue;
            if (amt.Konto == anklaeger || amt.Ratsherr == beklagter) continue;
            return new Richter(rolle, amt.Ratsherr, amt.Konto);
        }
        return null;
    }

    /// <summary>Zwei Schöffen (eigener Würfel): zuerst Ratsherren ohne Amt, sonst die übrigen – nie der Angezeigte.</summary>
    private List<Ratsherr> SchoeffenGegen(Richter vorsitz, Ratsherr beklagter)
    {
        var rat = _w.Rat;
        var ohneAmt = rat.Ratsherren.Where(r => rat.Aemter.All(a => a.Ratsherr != r) && r != vorsitz.Ratsherr && r != beklagter).ToList();
        var mitAmt = rat.Ratsherren.Where(r => !ohneAmt.Contains(r) && r != vorsitz.Ratsherr && r != beklagter).ToList();
        var gewaehlt = new List<Ratsherr>();
        foreach (var topf in new[] { ohneAmt, mitAmt })
            while (gewaehlt.Count < 2 && topf.Count > 0)
            {
                var r = topf[_zufall.Next(topf.Count)];
                topf.Remove(r);
                gewaehlt.Add(r);
            }
        return gewaehlt;
    }

    /// <summary>Wie ein Richter über den angezeigten Ratsherrn urteilt: Beweiskraft des Geheimnisses + Gunst (Meinung über beide, Plädoyer), gedeckelt.</summary>
    public double Ueberzeugung(Richter richter, RatsherrVerfahren v)
    {
        var kraft = Daten.Art(v.Art).Beweiskraft;
        if (richter.Ratsherr is not { } r || r == v.Beklagter) return kraft;
        var j = _w.Justiz.Daten;
        var einfluss = j.GunstFaktor * (_w.Rat.Meinung(r, v.Anklaeger) - _w.Meinungen.Meinung(r, v.Beklagter)) + _w.Familien.PlaedoyerAllein(v.Anklaeger);
        return kraft + Math.Clamp(einfluss, -j.GunstHoechstwirkung, j.GunstHoechstwirkung);
    }

    /// <summary>
    /// Am Gerichtstag (aus dem Stadtgericht, nach den Verfahren gegen Häuser): Schuldig verliert der Ratsherr seinen Sitz und rückt
    /// <see cref="GeheimnisRatsherrenDaten.SperreJahre"/> Jahre in kein Amt nach; Freispruch: Der Ankläger zahlt die Entschädigung in die Stadtkasse.
    /// </summary>
    internal void Gerichtstag(Action<string, Konto?> eintrag)
    {
        if (_verfahren.Count == 0) return;
        var j = _w.Justiz.Daten;
        var jahr = Jahr;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        foreach (var v in _verfahren)
        {
            if (!v.Anklaeger.IstAktiv)
            {
                eintrag($"Gerichtstag {jahr}: Die Anzeige von {v.Anklaeger.Inhaber} gegen {v.Beklagter.Name} wird eingestellt.", null);
                continue;
            }
            var schuldstimmen = v.Richter.Count(r => Ueberzeugung(r, v) >= j.SchuldSchwelle - 1e-9);
            var zaehlung = $"Anzeige: {v.Anklaeger.Inhaber}; {schuldstimmen} von {v.Richter.Count} Richtern: schuldig";
            var name = Daten.Art(v.Art).Name;
            if (schuldstimmen * 2 > v.Richter.Count)
            {
                var amt = _w.Rat.Aemter.FirstOrDefault(a => a.Ratsherr == v.Beklagter);
                _gesperrt[v.Beklagter.Name] = jahr + Daten.Ratsherren.SperreJahre;
                if (amt is not null) _w.Rat.RatsherrAbsetzen(v.Beklagter);
                eintrag($"Gerichtstag {jahr}: Der Ratsherr {v.Beklagter.Name} ist SCHULDIG – {name} ({zaehlung}): " +
                        (amt is not null ? $"Verlust des Amtes {amt.Name}, " : "") + $"{Daten.Ratsherren.SperreJahre} Jahre ohne Amt.", null);
            }
            else
            {
                var entschaedigung = j.Entschaedigung; // Rechtskundig gilt nur für den Beklagten – ein Ratsherr hat keine Lebensweise
                v.Anklaeger.Gerichtskosten(entschaedigung);
                _w.Kasse.Buchen(Kassenposten.Strafen, entschaedigung);
                _w.Meinungen.TatEintragen(v.Beklagter.Kennung, _w.Meinungen.HausKennung(v.Anklaeger), "klage", "Hat ihn verklagt",
                    _w.Meinungen.Daten?.Taten.Klage ?? 0);
                eintrag($"Gerichtstag {jahr}: Der Ratsherr {v.Beklagter.Name} wird freigesprochen ({zaehlung}). " +
                        $"{v.Anklaeger.Inhaber} zahlt {Zahlenformat.Ganz(entschaedigung)} {waehrung} in die Stadtkasse.", null);
            }
        }
        _verfahren.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Entwicklerfenster
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Entwicklerbefehl: Die Person (Kennung) bekommt ein Geheimnis dieser Art; mit <paramref name="wissen"/> weiß das Haus davon.</summary>
    internal Befehlsergebnis Entwickler(Konto absender, string kennung, Geheimnisart art, bool wissen)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Geheimnisse.");
        if (_w.Meinungen.Charakter(kennung) is not { } c || c is Person { Lebt: false }) return Befehlsergebnis.Abgelehnt($"„{kennung}“ gibt es nicht (mehr).");
        if (c is Person p && (_w.Familien.FamilieVon(p) is not { } f || !_w.Familien.Hauptpersonen(f).Contains(p)))
            return Befehlsergebnis.Abgelehnt($"{p.Vorname} hat keine Geheimnisse – nur Oberhaupt, Erbe, Ehepartner und Ratsherren.");
        var g = _geheimnisse.FirstOrDefault(x => x.Inhaber == c.Kennung && x.Art == art);
        if (g is null && art == Geheimnisart.Mord) // einen vorhandenen Mord kann man erfahren, einen neuen nicht schaffen
            return Befehlsergebnis.Abgelehnt("Einen Mord gibt es nur mit einem Opfer – über Krötenschleim oder Giftdolch.");
        g ??= Neu(art, c);
        if (wissen) WissenGeben(absender, g, "Entwicklerhilfe");
        return Befehlsergebnis.Ok($"{c.Name}: Geheimnis „{Beschreibung(g)}“" + (wissen && !IstEigen(absender, g) ? $" – {absender.Inhaber} weiß davon." : "."));
    }
}
