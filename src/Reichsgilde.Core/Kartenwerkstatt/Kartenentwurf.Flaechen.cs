using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Flächen und Natur im Kartenentwurf: Bauzonen, Wald- und Streuflächen (Vielecke mit Aussparungen), Felder (seit Schritt 5a), einzelne
//  Pflanzen und der Pinsel
//
//  Ecken landen auf 5 cm, die Ecken einer Bauzone auf dem Raster der Karte (2 m) – so verlangt es das Format. Die Natur einer Fläche
//  rechnet NaturVerteilung: Die Darstellung fragt nach jeder Änderung NaturVon(…) und zeichnet das Ergebnis; unter Gebäuden, Wegen und
//  Bauzonen bleibt es von selbst frei (Naturfreiraum).
// ═════════════════════════════════════════════════════════════════════════════

public sealed partial class Kartenentwurf
{
    // ── Vorlagen für neue Naturflächen ───────────────────────────────────────

    /// <summary>
    /// Dichte einer neuen Waldfläche (Bäume je 1.000 m², je Streupunkt ein Baum). Die Wälder der Greybox haben 2,4 (unverändert); eine kleine
    /// neue Fläche wirkte damit licht (1.300 m² trugen 3 Bäume). Die Kronen der Baummodelle sind 3–4 m (Tanne) bis 8 m (Eiche) breit, im
    /// Mischwald der Vorlage im Mittel etwa 25 m² Kronenfläche: Bei 10 Bäumen je 1.000 m² (ein Baum je 10 × 10 m) ist von oben ein Viertel
    /// des Bodens bedeckt, im Schrägblick der Kamera schließt sich das Bild fast.
    /// </summary>
    public const double WaldDichte = 10;

    /// <summary>Dichte einer neuen Streufläche (Streupunkte je 1.000 m², wie die Streu der Greybox).</summary>
    public const double StreuDichte = 2.9;

    /// <summary>Die Gruppen einer neuen Fläche: Wald = Mischwald (Tanne, Eiche, Buche, je Punkt ein Baum); Streu = Büsche, Boden, Steine, Holz wie im Umland der Greybox.</summary>
    public static IReadOnlyList<Naturgruppe> VorlageGruppen(string art) => art == "streu"
        ? new[]
        {
            Gruppe(0.34, 3.5, (Arten("hasel", "holunder", "brombeere", "ginster", "wacholder"), 2, 4), (Arten("farn", "gras", "brennnessel", "pilze", "wildblumen"), 2, 4)),
            Gruppe(0.2, 2.5, (Arten("felsbrocken", "findling", "kiesel", "lesesteinhaufen"), 2, 4), (Arten("gras"), 1, 3)),
            Gruppe(0.14, 2.5, (Arten("stumpf", "totholz", "reisighaufen"), 1, 3), (Arten("farn"), 1, 3)),
            Gruppe(0.32, 4, (Arten("wildblumen"), 2, 5), (Arten("gras"), 3, 6)),
        }
        : new[] { Gruppe(1, 0, (new Dictionary<string, double> { ["tanne"] = 0.55, ["eiche"] = 0.3, ["buche"] = 0.15 }, 1, 1)) };

    private static Dictionary<string, double> Arten(params string[] arten) => arten.ToDictionary(a => a, _ => 1.0);

    private static Naturgruppe Gruppe(double gewicht, double radius, params (Dictionary<string, double> Arten, int Von, int Bis)[] teile) => new()
    {
        Gewicht = gewicht, Radius = radius, Teile = teile.Select(t => new Naturteil { Arten = t.Arten, Anzahl = new[] { t.Von, t.Bis } }).ToList(),
    };

    // ── Bauzonen ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt eine Bauzone an: Lage und Ecken (mindestens drei verschiedene, auf das Raster der Karte gerundet). Kennung: Anfangsbuchstabe der
    /// Lage mit der kleinsten freien Nummer („K4“, „V9“), wenn keine angegeben ist.
    /// </summary>
    /// <exception cref="ArgumentException">Mit weniger als drei verschiedenen Ecken.</exception>
    public EntwurfBauzone BauzoneAnlegen(Lage art, IReadOnlyList<Kartenort> ecken, string? kennung = null, string? nur = null)
    {
        var zone = new EntwurfBauzone { Art = art, Nur = nur, Kennung = string.IsNullOrWhiteSpace(kennung) ? NeueZonenkennung(art) : kennung };
        zone.VieleckSetzen(-1, Ecken(ecken, zone));
        if (zone.Ecken.Count < 3) throw new ArgumentException("Eine Bauzone braucht mindestens drei verschiedene Ecken.", nameof(ecken));
        Geschichte.Ausfuehren(new TeilSetzen(zone, _bauzonen.Count));
        return zone;
    }

    public void BauzoneArtSetzen(EntwurfBauzone zone, Lage art) => Aendern(zone, "Lage geändert", z => z.Art = art);

    /// <summary>Ändert die Kennung (sie muss in der Karte frei sein, sonst meldet die Prüfung sie doppelt).</summary>
    public void BauzoneKennungSetzen(EntwurfBauzone zone, string kennung) => Aendern(zone, "umbenannt", z => z.Kennung = kennung);

    private string NeueZonenkennung(Lage art)
    {
        var basis = art.ToString()[..1];
        var belegt = _bauzonen.Select(z => z.Kennung).ToHashSet();
        var n = 1;
        while (belegt.Contains(basis + n)) n++;
        return basis + n;
    }

    // ── Naturflächen ─────────────────────────────────────────────────────────

    /// <summary>
    /// Legt eine Wald- („wald“) oder Streufläche („streu“) an: Ecken (mindestens drei verschiedene), Name (Vorgabe „Wald“, „Streu“ mit Nummer),
    /// Dichte und Gruppen aus der Vorlage (<see cref="VorlageGruppen"/>), Startwert = der größte der Karte + 1 (oder 1000).
    /// </summary>
    /// <exception cref="ArgumentException">Mit weniger als drei verschiedenen Ecken oder einer anderen Art als wald/streu.</exception>
    public EntwurfNaturflaeche NaturflaecheAnlegen(string art, IReadOnlyList<Kartenort> ecken, string? name = null)
    {
        if (art is not ("wald" or "streu")) throw new ArgumentException("Eine Naturfläche ist „wald“ oder „streu“.", nameof(art));
        var f = new EntwurfNaturflaeche
        {
            Name = string.IsNullOrWhiteSpace(name) ? FreierName(art == "wald" ? "Wald" : "Streu", _naturflaechen.Select(n => n.Name)) : name,
            Art = art, Dichte = art == "wald" ? WaldDichte : StreuDichte,
            Startwert = _naturflaechen.Count == 0 ? 1000 : _naturflaechen.Max(n => n.Startwert) + 1,
        };
        f.GruppenSetzen(VorlageGruppen(art));
        f.VieleckSetzen(-1, Ecken(ecken, f));
        if (f.Ecken.Count < 3) throw new ArgumentException("Eine Fläche braucht mindestens drei verschiedene Ecken.", nameof(ecken));
        Geschichte.Ausfuehren(new TeilSetzen(f, _naturflaechen.Count));
        return f;
    }

    public void NaturflaecheUmbenennen(EntwurfNaturflaeche f, string name) => Aendern(f, "umbenannt", n => n.Name = name);

    /// <summary>„wald“ oder „streu“ (nur die Beschreibung; die Mischung bleibt).</summary>
    public void NaturArtSetzen(EntwurfNaturflaeche f, string art) => Aendern(f, "Art geändert", n => n.Art = art);

    /// <summary>Streupunkte je 1.000 m² (nicht negativ, auf 0,01).</summary>
    public void DichteSetzen(EntwurfNaturflaeche f, double dichte) => Aendern(f, "Dichte geändert", n => n.Dichte = Math.Round(Math.Max(0, dichte), 2));

    /// <summary>Neuer Startwert: dieselbe Mischung, andere Lagen („neu würfeln“).</summary>
    public void StartwertSetzen(EntwurfNaturflaeche f, int startwert) => Aendern(f, "neu gewürfelt", n => n.Startwert = startwert);

    /// <summary>Ersetzt die Gruppen (für eine eigene Mischung mit Radius und Anzahl je Teil).</summary>
    public void GruppenSetzen(EntwurfNaturflaeche f, IReadOnlyList<Naturgruppe> gruppen) => Aendern(f, "Mischung geändert", n => n.GruppenSetzen(gruppen));

    // ── Felder (Schritt 5a) ──────────────────────────────────────────────────
    //  Eine Feldfläche ist ein Vieleck, das in Ackerstreifen geteilt wird (FeldVerteilung). Ecken, Aussparungen, Verschieben und Löschen wie
    //  bei jeder Fläche (unten); hier, was nur Felder haben: Richtung, Streifenbreite und Stücklänge, Früchte, Startwert.

    /// <summary>Die Früchte einer neuen Feldfläche: Winterkorn, Sommerkorn und Brache wie in der Dreifelderwirtschaft.</summary>
    public static IReadOnlyDictionary<string, double> VorlageFruechte => new Dictionary<string, double> { ["roggen"] = 1, ["hafer"] = 1, ["brache"] = 0.5 };

    /// <summary>
    /// Legt eine Feldfläche an: Ecken (mindestens drei verschiedene), Name (Vorgabe „Feld“ mit Nummer), Richtung der Streifen in Grad (0 = Nord–Süd),
    /// Streifen 8 m, Stücke um 40 m, Früchte aus der Vorlage (<see cref="VorlageFruechte"/>), Startwert = der größte der Felder + 1 (oder 2000).
    /// </summary>
    /// <exception cref="ArgumentException">Mit weniger als drei verschiedenen Ecken.</exception>
    public EntwurfFeld FeldAnlegen(IReadOnlyList<Kartenort> ecken, string? name = null, double richtung = 0)
    {
        var f = NeuesFeld(ecken, name, richtung, _felder.Select(x => x.Name), _felder.Count == 0 ? 2000 : _felder.Max(x => x.Startwert) + 1);
        Geschichte.Ausfuehren(new TeilSetzen(f, _felder.Count));
        return f;
    }

    private EntwurfFeld NeuesFeld(IReadOnlyList<Kartenort> ecken, string? name, double richtung, IEnumerable<string> namen, int startwert)
    {
        var f = new EntwurfFeld
        {
            Name = string.IsNullOrWhiteSpace(name) ? FreierName("Feld", namen) : name, Richtung = Kartenraster.Drehung(richtung), Startwert = startwert,
        };
        f.FruechteSetzen(VorlageFruechte);
        f.VieleckSetzen(-1, Ecken(ecken, f));
        if (f.Ecken.Count < 3) throw new ArgumentException("Eine Fläche braucht mindestens drei verschiedene Ecken.", nameof(ecken));
        return f;
    }

    public void FeldUmbenennen(EntwurfFeld f, string name) => Aendern(f, "umbenannt", n => n.Name = name);

    /// <summary>Richtung der Streifen in Grad (0 = Nord–Süd, 90 = Ost–West), auf 0,1°.</summary>
    public void FeldRichtungSetzen(EntwurfFeld f, double richtung) => Aendern(f, "gedreht", n => n.Richtung = Kartenraster.Drehung(richtung));

    /// <summary>
    /// Breite der Ackerstreifen (3–30 m, auf 0,5 m) und mittlere Länge der Stücke (10–200 m, auf ganze Meter).
    /// </summary>
    public void FeldStreifenSetzen(EntwurfFeld f, double streifen, double laenge) => Aendern(f, "Streifen geändert", n =>
    {
        n.Streifen = Math.Clamp(Math.Round(streifen * 2) / 2, FeldVerteilung.StreifenMin, FeldVerteilung.StreifenMax);
        n.Laenge = Math.Clamp(Math.Round(laenge), FeldVerteilung.LaengeMin, FeldVerteilung.LaengeMax);
    });

    /// <summary>Die Früchte mit Gewicht (nur bekannte, <see cref="FeldVerteilung.Fruechte"/>; Gewicht auf 0,01, 0 nimmt eine heraus).</summary>
    /// <exception cref="ArgumentException">Bei einer unbekannten Frucht.</exception>
    public void FeldFruechteSetzen(EntwurfFeld f, IReadOnlyDictionary<string, double> fruechte)
    {
        foreach (var k in fruechte.Keys.Where(k => !FeldVerteilung.Fruechte.ContainsKey(k)))
            throw new ArgumentException($"Eine Frucht „{k}“ gibt es nicht (bekannt: {string.Join(", ", FeldVerteilung.Fruechte.Keys)}).", nameof(fruechte));
        Aendern(f, "Früchte geändert", n => n.FruechteSetzen(fruechte.Select(x => KeyValuePair.Create(x.Key, Math.Round(x.Value, 2))).Where(x => x.Value > 0)));
    }

    /// <summary>Neuer Startwert: dieselben Früchte, andere Stücke („neu würfeln“).</summary>
    public void FeldStartwertSetzen(EntwurfFeld f, int startwert) => Aendern(f, "neu gewürfelt", n => n.Startwert = startwert);

    // ── Gruppen einzeln (Streu: Büsche mit Kraut, Steine mit Gras …) ─────────
    //  Eine Gruppe ist eine Vorlage für einen Streupunkt: Gewicht (wie oft sie gewählt wird), Radius der Scheibe und ihre Teile (je Teil
    //  Arten mit Gewicht und Anzahl [von, bis]). Die Befehle ändern eine Gruppe oder einen Teil, die übrigen bleiben, wie sie sind.

    /// <summary>
    /// Fügt eine Gruppe hinten an und gibt ihren Index zurück. Ohne Angabe: Gewicht 1, Radius 2 m, ein Teil mit <paramref name="art"/> (Vorgabe
    /// Gras) und 1 bis 3 Stück.
    /// </summary>
    public int GruppeHinzufuegen(EntwurfNaturflaeche f, Naturgruppe? gruppe = null, string art = "gras")
    {
        var neu = gruppe ?? Gruppe(1, 2, (Arten(art), 1, 3));
        Aendern(f, "Gruppe hinzugefügt", n => n.GruppenSetzen(n.Gruppen.Append(neu)));
        return f.Gruppen.Count - 1;
    }

    /// <summary>Entfernt eine Gruppe; false (kein Schritt), wenn es die letzte ist (eine Fläche ohne Gruppe streut nichts – ein Fehler).</summary>
    public bool GruppeEntfernen(EntwurfNaturflaeche f, int gruppe)
    {
        Pruefen(f, gruppe, f.Gruppen.Count);
        if (f.Gruppen.Count <= 1) return false;
        Aendern(f, "Gruppe entfernt", n => n.GruppenSetzen(n.Gruppen.Where((_, i) => i != gruppe)));
        return true;
    }

    /// <summary>Gewicht (nicht negativ, auf 0,01) und Radius (Meter, nicht negativ, auf 0,1) einer Gruppe; die Teile bleiben.</summary>
    public void GruppeSetzen(EntwurfNaturflaeche f, int gruppe, double gewicht, double radius)
    {
        Pruefen(f, gruppe, f.Gruppen.Count);
        GruppeAendern(f, gruppe, "Gruppe geändert", g => new Naturgruppe { Gewicht = Math.Round(Math.Max(0, gewicht), 2), Radius = Math.Round(Math.Max(0, radius), 1), Teile = g.Teile });
    }

    /// <summary>Fügt einer Gruppe einen Teil an (Arten mit Gewicht, Anzahl [von, bis]) und gibt seinen Index zurück.</summary>
    public int GruppenteilHinzufuegen(EntwurfNaturflaeche f, int gruppe, IReadOnlyDictionary<string, double> arten, int von = 1, int bis = 1)
    {
        Pruefen(f, gruppe, f.Gruppen.Count);
        var teil = NeuerTeil(arten, von, bis);
        GruppeAendern(f, gruppe, "Teil hinzugefügt", g => new Naturgruppe { Gewicht = g.Gewicht, Radius = g.Radius, Teile = g.Teile.Append(teil).ToList() });
        return f.Gruppen[gruppe].Teile.Count - 1;
    }

    /// <summary>Ändert einen Teil einer Gruppe: Arten mit Gewicht (Gewicht 0 fällt weg) und Anzahl [von, bis] (0 ≤ von ≤ bis).</summary>
    public void GruppenteilSetzen(EntwurfNaturflaeche f, int gruppe, int teil, IReadOnlyDictionary<string, double> arten, int von, int bis)
    {
        Pruefen(f, gruppe, f.Gruppen.Count);
        Pruefen(f, teil, f.Gruppen[gruppe].Teile.Count);
        var neu = NeuerTeil(arten, von, bis);
        GruppeAendern(f, gruppe, "Teil geändert", g => new Naturgruppe { Gewicht = g.Gewicht, Radius = g.Radius, Teile = g.Teile.Select((t, i) => i == teil ? neu : t).ToList() });
    }

    /// <summary>Entfernt einen Teil; false (kein Schritt), wenn es der letzte der Gruppe ist (dann die Gruppe entfernen).</summary>
    public bool GruppenteilEntfernen(EntwurfNaturflaeche f, int gruppe, int teil)
    {
        Pruefen(f, gruppe, f.Gruppen.Count);
        Pruefen(f, teil, f.Gruppen[gruppe].Teile.Count);
        if (f.Gruppen[gruppe].Teile.Count <= 1) return false;
        GruppeAendern(f, gruppe, "Teil entfernt", g => new Naturgruppe { Gewicht = g.Gewicht, Radius = g.Radius, Teile = g.Teile.Where((_, i) => i != teil).ToList() });
        return true;
    }

    private void GruppeAendern(EntwurfNaturflaeche f, int gruppe, string was, Func<Naturgruppe, Naturgruppe> aenderung) =>
        Aendern(f, was, n => n.GruppenSetzen(n.Gruppen.Select((g, i) => i == gruppe ? aenderung(g) : g)));

    private static Naturteil NeuerTeil(IReadOnlyDictionary<string, double> arten, int von, int bis)
    {
        von = Math.Max(0, von);
        return new Naturteil { Arten = arten.Where(a => a.Value > 0).ToDictionary(a => a.Key, a => a.Value), Anzahl = new[] { von, Math.Max(von, bis) } };
    }

    /// <summary>
    /// Setzt die Mischung der Arten (Art → Gewicht). Hat die Fläche eine Gruppe mit einem Teil (ein Wald), bekommt dieser Teil die neuen Arten
    /// (Radius und Anzahl bleiben); sonst ersetzt eine Gruppe mit einer Pflanze je Streupunkt alle bisherigen.
    /// </summary>
    public void MischungSetzen(EntwurfNaturflaeche f, IReadOnlyDictionary<string, double> arten)
    {
        var neu = arten.Where(a => a.Value > 0).ToDictionary(a => a.Key, a => a.Value);
        Aendern(f, "Mischung geändert", n =>
        {
            if (n.Gruppen is [{ Teile: [var teil] } g])
                n.GruppenSetzen(new[] { new Naturgruppe { Gewicht = g.Gewicht, Radius = g.Radius, Teile = { new Naturteil { Arten = neu, Anzahl = teil.Anzahl } } } });
            else
                n.GruppenSetzen(new[] { Gruppe(1, 0, (neu, 1, 1)) });
        });
    }

    // ── Ecken und Aussparungen (Bauzonen und Naturflächen) ───────────────────

    /// <summary>Verschiebt eine Ecke des Umrisses (<paramref name="aussparung"/> = −1) oder einer Aussparung.</summary>
    public void EckeVerschieben(EntwurfFlaeche f, int ecke, double x, double z, int aussparung = -1)
    {
        Pruefen(f, ecke, VieleckVon(f, aussparung).Count);
        var ort = Ecke(new Kartenort(x, z), f);
        Aendern(f, "Ecke verschoben", n => n.VieleckSetzen(aussparung, n.Vieleck(aussparung).Select((p, i) => i == ecke ? ort : p)));
    }

    /// <summary>Fügt eine Ecke an der Stelle <paramref name="index"/> ein (0 … Anzahl).</summary>
    public void EckeEinfuegen(EntwurfFlaeche f, int index, double x, double z, int aussparung = -1)
    {
        Pruefen(f, index, VieleckVon(f, aussparung).Count + 1);
        var ort = Ecke(new Kartenort(x, z), f);
        Aendern(f, "Ecke eingefügt", n => n.VieleckSetzen(aussparung, n.Vieleck(aussparung).Take(index).Append(ort).Concat(n.Vieleck(aussparung).Skip(index))));
    }

    /// <summary>
    /// Fügt eine Ecke auf der nächsten Kante ein (Klick auf den Rand) und gibt ihren Index zurück (−1: dort ist schon eine Ecke).
    /// </summary>
    public int EckeEinfuegenBei(EntwurfFlaeche f, double x, double z, int aussparung = -1)
    {
        var poly = VieleckVon(f, aussparung);
        var p = new Kartenort(x, z);
        var (best, index) = (double.MaxValue, poly.Count);
        for (var i = 0; i < poly.Count; i++)
        {
            var d = Kartengeometrie.AbstandStrecke(p, poly[i], poly[(i + 1) % poly.Count]);
            if (d < best) (best, index) = (d, i + 1);
        }
        var ort = Ecke(p, f);
        if (poly.Any(q => q.Abstand(ort) < 1e-3)) return -1;
        EckeEinfuegen(f, index, ort.X, ort.Z, aussparung);
        return index;
    }

    /// <summary>Löscht eine Ecke; false (kein Schritt), wenn das Vieleck dann weniger als drei hätte.</summary>
    public bool EckeLoeschen(EntwurfFlaeche f, int ecke, int aussparung = -1)
    {
        var poly = VieleckVon(f, aussparung);
        Pruefen(f, ecke, poly.Count);
        if (poly.Count <= 3) return false;
        Aendern(f, "Ecke gelöscht", n => n.VieleckSetzen(aussparung, n.Vieleck(aussparung).Where((_, i) => i != ecke)));
        return true;
    }

    /// <summary>Verschiebt die ganze Fläche mit ihren Aussparungen um (dx, dz) Meter (eine Bauzone in Rasterschritten).</summary>
    public void FlaecheVerschieben(EntwurfFlaeche f, double dx, double dz) => Aendern(f, "verschoben", n =>
    {
        Kartenort Weiter(Kartenort p) => Ecke(new Kartenort(p.X + dx, p.Z + dz), n);
        n.VieleckSetzen(-1, n.Ecken.Select(Weiter).ToList());
        for (var i = 0; i < n.Aussparungen.Count; i++) n.VieleckSetzen(i, n.Aussparungen[i].Select(Weiter).ToList());
    });

    /// <summary>Legt in einer Naturfläche oder einem Feld eine Aussparung an (mindestens drei verschiedene Ecken) und gibt ihren Index zurück.</summary>
    /// <exception cref="ArgumentException">Mit weniger als drei verschiedenen Ecken oder in einer Bauzone.</exception>
    public int AussparungAnlegen(EntwurfFlaeche f, IReadOnlyList<Kartenort> ecken)
    {
        if (!f.KannAussparungen) throw new ArgumentException($"„{f.Bezeichnung}“ kann keine Aussparungen haben.", nameof(f));
        var liste = Ecken(ecken, f);
        if (liste.Count < 3) throw new ArgumentException("Eine Aussparung braucht mindestens drei verschiedene Ecken.", nameof(ecken));
        Aendern(f, "Aussparung angelegt", n => n.AussparungenSetzen(n.Aussparungen.Append(liste)));
        return f.Aussparungen.Count - 1;
    }

    public void AussparungLoeschen(EntwurfFlaeche f, int aussparung)
    {
        Pruefen(f, aussparung, f.Aussparungen.Count);
        Aendern(f, "Aussparung gelöscht", n => n.AussparungenSetzen(n.Aussparungen.Where((_, i) => i != aussparung)));
    }

    /// <summary>Löscht eine Bauzone, Naturfläche oder Feldfläche (Rückgängig setzt sie an dieselbe Stelle zurück).</summary>
    public void FlaecheLoeschen(EntwurfFlaeche f) => TeilEntfernen(f);

    private static IReadOnlyList<Kartenort> VieleckVon(EntwurfFlaeche f, int aussparung)
    {
        if (aussparung >= f.Aussparungen.Count) throw new ArgumentOutOfRangeException(nameof(aussparung), aussparung, $"die Fläche hat {f.Aussparungen.Count} Aussparungen");
        return f.Vieleck(aussparung);
    }

    /// <summary>Eine Ecke: bei Bauzonen auf dem Raster der Karte, sonst auf 5 cm.</summary>
    private Kartenort Ecke(Kartenort p, EntwurfFlaeche f) =>
        f is EntwurfBauzone ? new Kartenort(Kartenraster.Runden(p.X, Raster) + 0.0, Kartenraster.Runden(p.Z, Raster) + 0.0) : Gerundet(p);

    /// <summary>Gerundete Ecken ohne gleiche Nachbarn (auch die letzte nicht gleich der ersten).</summary>
    private List<Kartenort> Ecken(IEnumerable<Kartenort> ecken, EntwurfFlaeche f)
    {
        var liste = OhneDoppelte(ecken.Select(p => Ecke(p, f)));
        while (liste.Count > 1 && liste[^1].Abstand(liste[0]) < 1e-6) liste.RemoveAt(liste.Count - 1);
        return liste;
    }

    // ── Einzelne Pflanzen und Pinsel ─────────────────────────────────────────

    /// <summary>
    /// Setzt eine Pflanze (einen Natur-Baustein des Katalogs: Baum, Busch, Stein …) auf (x, z); Drehung in Grad, Maßstab, Variante (0 = fest
    /// aus dem Ort gewürfelt).
    /// </summary>
    /// <exception cref="ArgumentException">Wenn der Baustein keine Natur des Katalogs ist.</exception>
    public EntwurfPflanze PflanzeSetzen(string katalog, double x, double z, double drehung = 0, double massstab = 1, int variante = 0)
    {
        var p = NeuePflanze(katalog, x, z, drehung, massstab, variante);
        Geschichte.Ausfuehren(new TeilSetzen(p, _pflanzen.Count));
        return p;
    }

    public void PflanzeVerschieben(EntwurfPflanze p, double x, double z) =>
        Aendern(p, "verschoben", n => (n.X, n.Z) = (Kartenraster.Punkt(x), Kartenraster.Punkt(z)));

    public void PflanzeDrehen(EntwurfPflanze p, double drehung) => Aendern(p, "gedreht", n => n.Drehung = Kartenraster.Drehung(drehung));

    public void PflanzeMassstabSetzen(EntwurfPflanze p, double massstab) => Aendern(p, "Maßstab geändert", n => n.Massstab = Math.Round(massstab, 2));

    public void PflanzeLoeschen(EntwurfPflanze p) => TeilEntfernen(p);

    /// <summary>Löscht mehrere Pflanzen in einem Schritt (Radierer); nichts, wenn keine im Entwurf ist.</summary>
    public void PflanzenLoeschen(IEnumerable<EntwurfPflanze> pflanzen)
    {
        // von hinten nach vorn: Rückgängig setzt jede an ihre alte Stelle zurück
        var befehle = pflanzen.Distinct(ReferenceEqualityComparer.Instance).Cast<EntwurfPflanze>().Select(p => (p, i: IndexVon(p))).Where(x => x.i >= 0)
            .OrderByDescending(x => x.i).Select(x => (IKartenbefehl)new TeilLoeschen(x.p, x.i)).ToList();
        if (befehle.Count == 0) return;
        Geschichte.Ausfuehren(befehle.Count == 1 ? befehle[0] : new Sammelbefehl($"{befehle.Count} Pflanzen gelöscht", befehle));
    }

    /// <summary>
    /// Der Pinsel: streut Pflanzen locker entlang eines Strichs (ein Punkt = ein Tupfer) in einem Band mit <paramref name="radius"/> Metern,
    /// <paramref name="dichte"/> Pflanzen je 1.000 m², Arten nach Gewicht (Natur-Bausteine). Wege, Gebäude, Bauzonen, die Stadt und schon
    /// stehende Einzelpflanzen bleiben frei (<see cref="NaturVerteilung.Strich"/>). Gleicher Strich und <paramref name="startwert"/> – gleiche
    /// Pflanzen (ohne Startwert: einer aus dem ersten Punkt). Ein Schritt je Strich; leer, wenn nichts Platz fand.
    /// </summary>
    public IReadOnlyList<EntwurfPflanze> PflanzenStreuen(IReadOnlyList<Kartenort> strich, double radius, double dichte, IReadOnlyDictionary<string, double> arten,
        int? startwert = null, Func<Baustein, (double X, double Z)?>? modellmass = null)
    {
        if (strich.Count == 0) return Array.Empty<EntwurfPflanze>();
        var start = startwert ?? unchecked((int)NaturVerteilung.Streuung((int)Math.Round(strich[0].X * 100), (long)Math.Round(strich[0].Z * 100) + _pflanzen.Count));
        var frei = Freiraum(modellmass);
        var lagen = NaturVerteilung.Strich(EntwurfTeil.Roh(strich), radius, dichte, arten, start, Katalog, frei, _pflanzen.Select(p => new[] { p.X, p.Z }));
        // auf 5 cm gerundet; was dabei gerade an einen Weg oder ein Gebäude rutscht, fällt weg
        var neue = lagen.Select(l => NeuePflanze(l.Art.Kennung, l.X, l.Z, l.Drehung, Math.Round(l.Massstab, 2), l.Variante)).Where(p => frei.Frei(p.X, p.Z)).ToList();
        if (neue.Count == 0) return neue;
        var befehle = neue.Select((p, i) => (IKartenbefehl)new TeilSetzen(p, _pflanzen.Count + i)).ToList();
        Geschichte.Ausfuehren(befehle.Count == 1 ? befehle[0] : new Sammelbefehl($"{neue.Count} Pflanzen gestreut", befehle));
        return neue;
    }

    private EntwurfPflanze NeuePflanze(string katalog, double x, double z, double drehung, double massstab, int variante)
    {
        var b = Katalog.Finde(katalog);
        if (b is not { Art: Bausteinart.Natur }) throw new ArgumentException($"„{katalog}“ ist kein Natur-Baustein des Katalogs.", nameof(katalog));
        return new EntwurfPflanze
        {
            Katalog = katalog, Baustein = b, X = Kartenraster.Punkt(x), Z = Kartenraster.Punkt(z), Drehung = Kartenraster.Drehung(drehung),
            Massstab = massstab, Variante = Math.Clamp(variante, 0, Math.Max(1, b.Varianten)),
        };
    }

    // ── Natur verteilen (für die Darstellung) ────────────────────────────────

    /// <summary>
    /// Wo im Entwurf nichts wächst: Stadt und Mauer, Wege (mit ihrem Verlauf und ihrer Breite), Gebäude, Bauzonen, Gewässer. Einmal je Änderung bauen
    /// und für alle Flächen nutzen; <paramref name="modellmass"/> wie bei <see cref="Naturhindernis.Von"/> (die Darstellung kennt die Modelle).
    /// </summary>
    public Naturfreiraum Freiraum(Func<Baustein, (double X, double Z)?>? modellmass = null) => new(
        _wege.Where(w => w.Punkte.Count >= 2).Select(w => (w.VerlaufRoh, w.BreiteWirksam)).Concat(Naturfreiraum.Flussstuecke(_gelaende.Wasser)),
        _gebaeude.Select(g => Naturhindernis.Von(g.Baustein, g.X, g.Z, g.Drehung, g.Massstab, modellmass)).OfType<Naturhindernis>(),
        _bauzonen.Select(z => (IReadOnlyList<double[]>)EntwurfTeil.Roh(z.Ecken)).Concat(Naturfreiraum.Seeflaechen(_gelaende.Wasser)),
        _mauern.Select(m => (IReadOnlyList<double[]>)EntwurfTeil.Roh(m.Ecken)));

    /// <summary>Die Pflanzen einer Naturfläche, wie das Spiel sie zeigt (deterministisch aus Fläche, Startwert und Freiraum).</summary>
    public List<Pflanzenlage> NaturVon(EntwurfNaturflaeche f, Naturfreiraum frei) => NaturVerteilung.Flaeche(f.AlsNaturflaeche(), Katalog, frei);

    /// <summary>Wie die Pflanze gezeichnet wird (Variante 0 fest aus dem Ort) – null, wenn ihr Baustein keine Natur des Katalogs ist.</summary>
    public Pflanzenlage? LageVon(EntwurfPflanze p) => NaturVerteilung.Einzeln(p.AlsPflanze(), Katalog);

    /// <summary>Die Äcker einer Feldfläche, wie das Spiel sie zeigt (deterministisch aus Fläche, Startwert und Freiraum, <see cref="FeldVerteilung"/>).</summary>
    public List<Feldparzelle> FelderVon(EntwurfFeld f, Naturfreiraum frei) => FeldVerteilung.Flaeche(f.AlsFeldflaeche(), frei);

    /// <summary>Alle Äcker des Entwurfs, Feldfläche für Feldfläche.</summary>
    public List<Feldparzelle> FelderAlle(Func<Baustein, (double X, double Z)?>? modellmass = null)
    {
        var frei = Freiraum(modellmass);
        return _felder.SelectMany(f => FelderVon(f, frei)).ToList();
    }

    /// <summary>Alle Pflanzen des Entwurfs: die Flächen in ihrer Reihenfolge, dann die einzeln gesetzten.</summary>
    public List<Pflanzenlage> NaturAlle(Func<Baustein, (double X, double Z)?>? modellmass = null)
    {
        var frei = Freiraum(modellmass);
        var liste = _naturflaechen.SelectMany(f => NaturVon(f, frei)).ToList();
        liste.AddRange(_pflanzen.Select(LageVon).OfType<Pflanzenlage>());
        return liste;
    }
}
