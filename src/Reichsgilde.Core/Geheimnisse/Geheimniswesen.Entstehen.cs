using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Geheimnisse;

/// <summary>
/// Wie Geheimnisse entstehen und ans Licht kommen. Reihenfolge der Würfe immer nach Konto, Person und Nummer – nie aus einem Dictionary.
/// </summary>
public sealed partial class Geheimniswesen
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Entstehen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Zu Partiebeginn hat jeder Ratsherr mit <see cref="GeheimnisRatsherrenDaten.Start"/> ein Geheimnis (Art nach seinen Zügen gewichtet).</summary>
    private void RatsherrenBeiPartiebeginn()
    {
        if (!_w.Rat.IstAktiv) return;
        foreach (var r in _w.Rat.Ratsherren)
            if (_zufall.NextDouble() < Daten.Ratsherren.Start)
                RatsherrGeheimnis(r);
    }

    /// <summary>Ein Geheimnis für den Ratsherrn: eine Art, die er noch nicht hat, gewichtet nach Grundgewicht × Zug-Gewichten.</summary>
    private void RatsherrGeheimnis(Ratsherr r)
    {
        var d = Daten;
        var arten = d.Ratsherren.Arten.OrderBy(x => x.Key).Where(x => !Hat(r, x.Key))
            .Select(x => (Art: x.Key, Gewicht: x.Value * ZugFaktor(r.Zuege, d.Art(x.Key).RatsherrZuege ?? d.Art(x.Key).Zuege)))
            .Where(x => x.Gewicht > 0).ToList();
        if (arten.Count == 0) return;
        var wurf = _zufall.NextDouble() * arten.Sum(x => x.Gewicht);
        foreach (var (art, gewicht) in arten)
        {
            wurf -= gewicht;
            if (wurf < 0) { Neu(art, r); return; }
        }
        Neu(arten[^1].Art, r);
    }

    /// <summary>Ist die Person verheiratet – und mit wem? (Oberhaupt mit lebendem Ehepartner bzw. der Ehepartner.)</summary>
    private static Person? EhepartnerVon(Familie f, Person p) =>
        p == f.Oberhaupt ? f.Ehepartner is { Lebt: true } e ? e : null
        : p == f.Ehepartner && f.Oberhaupt.Lebt ? f.Oberhaupt : null;

    /// <summary>Chance auf einen Ehebruch in diesem Jahr (0: nicht verheiratet).</summary>
    public double EhebruchChance(Person p)
    {
        if (!IstAktiv || _w.Familien.FamilieVon(p) is not { } f || EhepartnerVon(f, p) is not { } partner) return 0;
        var a = Daten.Art(Geheimnisart.Ehebruch);
        var c = a.Chance * ZugFaktor(p.Zuege, a.Zuege);
        if (_w.Meinungen.IstAktiv && _w.Meinungen.Meinung(p, partner) <= a.MeinungEhepartnerAb + 1e-9) c *= a.MeinungFaktor;
        if (a.StressStufe > 0 && _w.Familien.StressStufe(p) >= a.StressStufe) c *= a.StressFaktor;
        return Math.Clamp(c, 0, 1);
    }

    /// <summary>Chance auf heimliche Ketzerei in diesem Jahr (nur mit einem Zug aus <see cref="GeheimnisArtDaten.NurMit"/>, z. B. Zynisch).</summary>
    public double KetzereiChance(Person p)
    {
        if (!IstAktiv || !_w.Glaube.IstAktiv || !p.Glaube.IstKirchlich()) return 0;
        var a = Daten.Art(Geheimnisart.Ketzerei);
        if (a.NurMit.Count > 0 && !p.Zuege.Any(z => a.NurMit.Contains(z.Kennung))) return 0;
        return Math.Clamp(a.Chance * ZugFaktor(p.Zuege, a.Zuege), 0, 1);
    }

    /// <summary>
    /// Ein Glaubenswechsel (Konvertieren des Hauses, Übertritt bei der Heirat): Mit <see cref="GeheimnisArtDaten.Wechsel"/> bleibt die Person
    /// heimlich beim alten Glauben – Ketzerei (Fromm × 2, Zynisch 0).
    /// </summary>
    internal void Glaubenswechsel(Person p)
    {
        if (!IstAktiv || !p.Lebt || Hat(p, Geheimnisart.Ketzerei) || _w.Familien.FamilieVon(p) is not { Konto.IstAktiv: true }) return;
        var a = Daten.Art(Geheimnisart.Ketzerei);
        var chance = a.Wechsel * ZugFaktor(p.Zuege, a.WechselZuege);
        if (chance <= 0) return; // kein Wurf ohne Chance
        if (_zufall.NextDouble() < chance) Neu(Geheimnisart.Ketzerei, p);
    }

    /// <summary>
    /// Ein Opfer ist an Krötenschleim oder einem Giftdolch gestorben (aus dem Gesundheitswesen). Das Oberhaupt, das den Anschlag in Auftrag gab,
    /// hütet nun einen Mord – solange es lebt; er verjährt nie.
    /// </summary>
    internal void Mord(int taeter, Person opfer)
    {
        if (!IstAktiv || _w.Familien.PersonNummer(taeter) is not { Lebt: true } t || _w.Familien.FamilieVon(t) is not { Konto.IstAktiv: true } f) return;
        if (_w.Familien.FamilieVon(opfer) == f) return;
        Neu(Geheimnisart.Mord, t, opfer.Nummer);
    }

    /// <summary>
    /// Am Jahresende (nach den Todesfällen der Familien): Geheimnisse Verstorbener enden, Schulden entstehen oder enden, neue Ehebrüche, Ketzerei
    /// der Zyniker und Geheimnisse der Ratsherren; dann der Ehepartner, das Gerede und die Beichte.
    /// </summary>
    internal void Jahreswechsel()
    {
        if (!IstAktiv) return;
        var d = Daten;
        // Wer tot ist oder dessen Haus nicht mehr spielt, hat keine Geheimnisse mehr.
        foreach (var g in _geheimnisse.Where(g => Inhaber(g) is not ICharakter c || c is Person p && (!p.Lebt || HausVon(g) is not { IstAktiv: true })).ToList())
            Entfernen(g);
        // Aufräumen, damit der Spielstand nicht wächst: Wissen ausgeschiedener Häuser, abgelaufene Schande und Sperren (wirken nicht mehr)
        _wissen.RemoveAll(x => !x.Haus.IstAktiv);
        _schande.RemoveAll(s => s.Bis < Jahr);
        foreach (var name in _gesperrt.Where(x => x.Value < Jahr).Select(x => x.Key).OrderBy(n => n, StringComparer.Ordinal).ToList())
            _gesperrt.Remove(name);

        var fb = _w.Familien;
        var schulden = d.Art(Geheimnisart.Schulden);
        foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
        {
            if (fb.Von(konto) is not { } f) continue;
            // Schulden: das Oberhaupt, solange Bargeld im Minus oder Kredite über dem Anteil des Reinvermögens; danach vorbei.
            var o = f.Oberhaupt;
            var steckt = konto.Vermoegen < 0 || konto.Schulden > 0 && konto.Schulden > schulden.KreditAnteil * konto.Reinvermoegen;
            var alt = _geheimnisse.FirstOrDefault(g => g.Art == Geheimnisart.Schulden && g.Inhaber == o.Kennung);
            if (steckt && alt is null) Neu(Geheimnisart.Schulden, o);
            else if (!steckt && alt is not null) Entfernen(alt);
            foreach (var p in fb.Hauptpersonen(f))
            {
                if (!Hat(p, Geheimnisart.Ehebruch) && EhebruchChance(p) is var e and > 0 && _zufall.NextDouble() < e) Neu(Geheimnisart.Ehebruch, p);
                if (!Hat(p, Geheimnisart.Ketzerei) && KetzereiChance(p) is var k and > 0 && _zufall.NextDouble() < k) Neu(Geheimnisart.Ketzerei, p);
            }
        }
        if (_w.Rat.IstAktiv && d.Ratsherren.JeJahr > 0)
            foreach (var r in _w.Rat.Ratsherren)
                if (_zufall.NextDouble() < d.Ratsherren.JeJahr) RatsherrGeheimnis(r);

        EhepartnerEntdeckt();
        Gerede();
        Beichte();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Entdecken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Ehepartner entdeckt einen Ehebruch selbst (Klug × 1,5) – das bleibt im Haus: Meinung und Stress für beide.</summary>
    private void EhepartnerEntdeckt()
    {
        var e = Daten.Entdecken;
        foreach (var g in _geheimnisse.Where(g => g.Art == Geheimnisart.Ehebruch && !g.EhepartnerWeiss && !g.Oeffentlich).ToList())
        {
            if (Inhaber(g) is not Person p || _w.Familien.FamilieVon(p) is not { } f || EhepartnerVon(f, p) is not { } partner) continue;
            if (_zufall.NextDouble() >= e.Ehepartner * ZugFaktor(partner.Zuege, e.EhepartnerZuege)) continue;
            g.EhepartnerWeiss = true;
            _w.Meinungen.TatEintragen(partner.Kennung, p.Kennung, "ehebruch", "Ehebruch entdeckt", e.EhepartnerMeinung);
            _w.Familien.StressAendern(partner, e.EhepartnerStress, $"Ehebruch von {p.Vorname} entdeckt");
            _w.Familien.StressAendern(p, e.EhepartnerStress, $"{partner.Vorname} weiß vom Ehebruch");
            _w.Melden(Meldungsart.Sonstiges, $"Unfriede im Haus {f.Nachname}: {partner.Vorname} hat den Ehebruch von {p.Vorname} entdeckt. " +
                                              "Das bleibt im Haus – aber die Ehe leidet.", haus: f.Konto, person: p, vertraulich: true, geheimnis: g.Art);
            _w.Familien.ChronikEintragen(f.Konto, Dynastie.Chronikart.Geheimnis, $"{partner.Vorname} entdeckt den Ehebruch von {p.Vorname}.", p, vertraulich: true);
        }
    }

    /// <summary>Gerede: Jedes Haus erfährt jedes fremde Geheimnis mit <see cref="GeheimnisEntdeckenDaten.Gerede"/> im Jahr (dazu Gerüchteküche).</summary>
    private void Gerede()
    {
        var e = Daten.Entdecken;
        foreach (var haus in _w.Konten.Where(k => k.IstAktiv).ToList())
        {
            var gefunden = new List<Geheimnis>();
            var chance = e.Gerede + _w.Familien.Lebenswirkung(haus).Geruechte;
            if (chance <= 0) continue;
            foreach (var g in _geheimnisse.Where(g => !g.Oeffentlich && !IstEigen(haus, g) && Wissen(haus, g) is null).ToList())
                if (_zufall.NextDouble() < chance * Schutz(g) && WissenGeben(haus, g, "Gerede"))
                    gefunden.Add(g);
            if (gefunden.Count > 0)
                _w.Melden(Meldungsart.Sonstiges, $"Gerede in der Stadt: {string.Join("; ", gefunden.Select(Text))}. Ihr wisst es nun – was Ihr daraus macht, steht im Charakterfenster.",
                    haus: haus, vertraulich: true, geheimnis: EineArt(gefunden));
        }
    }

    /// <summary>
    /// Beichte: Wer fromm ist oder gepilgert hat, beichtet – eine Kirche desselben Glaubens erfährt es mit <see cref="GeheimnisEntdeckenDaten.Beichte"/>.
    /// Nutzen kann ihr Haus es erst, wenn es das Beichtgeheimnis bricht.
    /// </summary>
    private void Beichte()
    {
        var e = Daten.Entdecken;
        if (!_w.Kirchenwesen.IstAktiv || e.Beichte <= 0) return;
        foreach (var prediger in _w.Kirchenwesen.Kirchen.Select(b => b.Konto).Distinct().ToList())
        {
            var glaube = _w.Glaube.IstAktiv ? _w.Glaube.GlaubeVon(prediger) : null;
            foreach (var g in _geheimnisse.Where(g => !g.Oeffentlich && !IstEigen(prediger, g) && Wissen(prediger, g) is null).ToList())
            {
                if (Inhaber(g) is not { } c || !c.Zuege.Any(z => e.BeichteZuege.Contains(z.Kennung))) continue;
                if (glaube is not null && GlaubeVon(c) != glaube) continue;
                if (_zufall.NextDouble() < e.Beichte * Schutz(g) && WissenGeben(prediger, g, "Beichte", ausBeichte: true))
                    _w.Melden(Meldungsart.Sonstiges, $"Im Beichtstuhl hört {prediger.Inhaber}: {Text(g)}. Nutzen könnt Ihr es erst, wenn Ihr das Beichtgeheimnis brecht.",
                        haus: prediger, vertraulich: true, geheimnis: g.Art);
            }
        }
    }

    private Konfession? GlaubeVon(ICharakter c) => c switch
    {
        Person p => p.Glaube,
        Ratsherr r => _w.Glaube.GlaubeVon(r),
        _ => null,
    };

    /// <summary>„Agnes Hammerschmid: Ehebruch“ – für Meldungen.</summary>
    internal string Text(Geheimnis g) => $"{Inhaber(g)?.Name ?? g.Inhaber}: {Beschreibung(g)}";

    /// <summary>
    /// Spitzelbericht am Gerichtstag (aus dem Stadtgericht): Jedes Geheimnis von Oberhaupt, Ehepartner und Erbe des Ziels findet der Spitzel mit
    /// 30 % ± 3 Prozentpunkten je Stern „Bei Nacht und Nebel“ (eigenes Oberhaupt plus, Inhaber minus), 10–60 %, dazu Ohr an der Wand; mal Verschwiegen.
    /// </summary>
    /// <returns>Was er fand (für den Bericht: <see cref="Text"/>, Art: <see cref="EineArt"/>).</returns>
    internal List<Geheimnis> SpitzelBericht(Konto auftraggeber, Konto ziel)
    {
        var gefunden = new List<Geheimnis>();
        if (!IstAktiv || _w.Familien.Von(ziel) is not { } f) return gefunden;
        var e = Daten.Entdecken;
        var eigen = _w.Familien.Talent(auftraggeber, Talent.NachtUndNebel);
        var bonus = _w.Familien.Lebenswirkung(auftraggeber).SpitzelBonus;
        foreach (var p in _w.Familien.Hauptpersonen(f))
            foreach (var g in Von(p).Where(g => !g.Oeffentlich && !KenntNutzbar(auftraggeber, g)).ToList())
            {
                var chance = Math.Clamp(e.Spitzel + e.SpitzelJeStern * (eigen - p.Talent(Talent.NachtUndNebel)), e.SpitzelMin, e.SpitzelMax) + bonus;
                if (_zufall.NextDouble() < Math.Min(1, chance) * Schutz(g) && WissenGeben(auftraggeber, g, "Spitzel"))
                    gefunden.Add(g);
            }
        return gefunden;
    }

    /// <summary>Am Gerichtstag: Die Spitzel auf Ratsherren berichten (nur Geheimnisse, keine Spuren), dann sind sie fort.</summary>
    /// <param name="eintrag">Text, Auftraggeber (vertraulich) und die Art der gefundenen Geheimnisse (<see cref="EineArt"/>) für die Meldung.</param>
    internal void RatsspitzelBerichten(Action<string, Konto?, Geheimnisart?> eintrag)
    {
        if (!IstAktiv) { _ratsspitzel.Clear(); return; }
        var e = Daten.Entdecken;
        foreach (var (auftraggeber, r) in _ratsspitzel)
        {
            var gefunden = new List<Geheimnis>();
            var chance = Math.Min(1, e.SpitzelRatsherr + _w.Familien.Lebenswirkung(auftraggeber).SpitzelBonus);
            foreach (var g in Von(r).Where(g => !g.Oeffentlich && !KenntNutzbar(auftraggeber, g)).ToList())
                if (_zufall.NextDouble() < chance && WissenGeben(auftraggeber, g, "Spitzel"))
                    gefunden.Add(g);
            eintrag($"Der Spitzel von {auftraggeber.Inhaber} berichtet über {r.Name}: " +
                    (gefunden.Count == 0 ? "nichts, was sich gegen ihn verwenden ließe." : $"ein Geheimnis – {string.Join(", ", gefunden.Select(Beschreibung))}."),
                auftraggeber, EineArt(gefunden));
        }
        _ratsspitzel.Clear();
    }
}
