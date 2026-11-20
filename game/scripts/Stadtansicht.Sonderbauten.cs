using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Rathaus (Raumansicht) und Dom (Diorama) – dritter Teil der Stadtansicht; dazu die Kirchen mit eigenem Modell (nur von außen, <see cref="KircheBauen"/>).
///
/// Beide sind keine Betriebe, nutzen aber dasselbe Gerüst wie die Werkstätten (<see cref="InnenAnsicht"/>):
/// ein Knoten „Innen". Von außen stehen die Modelle des Rathauses (<c>gebaeude/neubau/rathaus.glb</c>, in Köln <c>koelner_rathaus.glb</c>) und
/// <c>dom.glb</c> (Stadtansicht.Modelle.cs) an ihrer Lage in der Karte, auch gedreht; beim Dom sind Dach, Turm und die vier Wände eigene Knoten des
/// Modells, die beim Hineinsehen ausgeblendet werden. Die Ansicht liest nur Abfragen des Kerns.
///
/// Rathaus – sieben Räume im Haus selbst (Stadtansicht.Rathausneubau.cs; Modelle und Anker aus tools/modelle/neubau/rathaus_*.py):
///   • Ratshalle: die sieben Ämter an den Sitzen der Ratssitzung. Häuser im Amt sitzen in ihrer Farbe da,
///     Ratsherren der Stadt in Ratsrot, der Bürgermeister mit Amtskette.
///   • Gerichtssaal: Läuft ein Verfahren, sitzen die drei Richter dieses Verfahrens, Ankläger und Beklagter stehen davor.
///   • Kerker: hinter Gittern die Oberhäupter der Häuser, die einsitzen; davor ein Wächter.
///   • Amtsstube: der Stadtschreiber am Kanzleipult, die Ständetafel mit einer Marke für den Stand des Spielers.
///   Tagsüber (8–20 Uhr) sind die Amtsinhaber da. Gegenstände im Raum öffnen ihre Tafel (Hauptszene, RathausGegenstaende.cs).
///
/// Dom – Hauptkirche des Stadtglaubens:
///   • katholisch: Hochaltar mit goldenem Retabel, Marienfigur, Weihrauchfass, ein Bischof mit Mitra;
///   • evangelisch: schlichter Abendmahlstisch mit Bibel, Holzkreuz, die Kanzel steht im Mittelpunkt, ein Pfarrer im Talar;
///   • Glaubensfreiheit: Simultankirche – Hochaltar im Chor, Abendmahlstisch davor, eine Schranke teilt die Bänke
///     (Katholiken links, Evangelische rechts).
///   Zur Messe (10 Uhr) sitzen die Gläubigen da, die keine eigene Kirche eines Hauses besucht haben – eine Figur je fünf.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kennungen der Räume im Rathaus (Klick in der Innenansicht).</summary>
    public const string RaumRat = "rathaus:rat", RaumGericht = "rathaus:gericht", RaumAmt = "rathaus:amt", RaumKerker = "rathaus:kerker";

    private static readonly Color Ratsrot = new(0.42f, 0.10f, 0.12f);
    private static readonly Color Robe = new(0.12f, 0.12f, 0.14f);
    private static readonly Color Gold = new(0.90f, 0.74f, 0.30f);

    /// <summary>Abstand der Stühle an den Seitentischen des Ratssaals von der Mitte (Meter): weit genug, dass Rocksaum und Füße vor dem Tischfuß bleiben.</summary>
    private const float RatssaalSeite = 2.6f;

    /// <summary>Stühle an der Stirnseite des Ratssaals (z, Meter): 10 cm hinter dem früheren Platz, damit Knie und Rock großer Figuren vor dem Tisch bleiben.</summary>
    private const float RatssaalStirn = -3.4f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Gemeinsames Gerüst
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Gerüst eines Sonderbaus ohne Hülle (die Hülle ist ein Modell, <see cref="ModellEinsetzen"/>): Klickfläche, Beschriftung
    /// und der leere Knoten „Innen“ für die Einrichtung. Beim Rathaus liegt er unter der Stadt (Raumansicht).
    /// </summary>
    /// <param name="klick">Größe der Klickfläche des Gebäudes (Mitte auf halber Höhe über <paramref name="p"/>).</param>
    /// <param name="drehung">Drehung des Gebäudes um die Hochachse (Bogenmaß) – die Klickfläche dreht sich mit.</param>
    private SonderbauAnsicht SonderbauAnlegen(string kennung, string titel, Vector3 p, Vector3 klick, Vector3 schildOrt, float zoom, float reichweite,
        bool raumansicht = false, float drehung = 0)
    {
        var ansicht = new SonderbauAnsicht
        {
            Mitte = p,
            Schluessel = kennung,
            Titel = titel,
            Schild = Beschriftung(titel, p + schildOrt, 56, kennung: kennung),
            Zoom = zoom,
            Reichweite = reichweite,
        };
        ansicht.Aussenklick = Klickflaeche(kennung, p + new Vector3(0, klick.Y / 2, 0), klick, drehung: drehung);
        var innen = new Node3D { Name = "Innen_" + kennung, Visible = false };
        if (raumansicht) innen.Position = new Vector3(0, RaumTiefe, 0); // Räume unter der Stadt, jeder mit eigener Bühne (RaumAnlegen)
        AddChild(innen);
        ansicht.Innen = innen;
        _sonderbauten[kennung] = ansicht;
        return ansicht;
    }

    /// <summary>Körperbau nach Volk (wie bei den Gästen der Werkstätten).</summary>
    private static (float Groesse, float Breite, bool Bart) Koerperbau(Volk volk) => volk switch
    {
        Volk.Zwerg => Voelker[1],
        Volk.Elb => Voelker[2],
        Volk.Halbling => Voelker[3],
        Volk.Ork or Volk.Oger => (1.2f, 1.15f, false),
        _ => Voelker[0],
    };

    /// <summary>Eine Person mit Volk, Gewand und Kopfbedeckung – für Ratsherren, Richter, Parteien, Geistliche.</summary>
    private Arbeiterfigur PersonBauen(InnenAnsicht a, Node3D eltern, Arbeitsplatz platz, Volk volk, Color gewand, Kopfputz kopf, float takt,
        float hoehe = 0, Color? schuerze = null)
    {
        if (RathausNeuFigur(a, eltern, platz, volk, gewand, kopf, takt) is { } echt) return echt; // neues Rathaus: echte Figur statt Kapsel (Stadtansicht.Rathausneubau.cs)
        var (groesse, breite, bart) = Koerperbau(volk);
        var haut = volk is Volk.Ork or Volk.Oger ? new Color(0.36f, 0.50f, 0.28f) : Haut;
        return FigurBauen(eltern, a.Mitte + platz.Ort + new Vector3(0, hoehe, 0), platz.Drehung, new Vector3(groesse * breite, groesse, groesse * breite),
            gewand, haut, schuerze, kopf, platz.Werkzeug, platz.Art, takt, platz.Sitzt, bart, volk, 700 + (int)(takt * 100));
    }

    /// <summary>Volk des Oberhaupts eines Hauses (für seine Figur), sonst Mensch.</summary>
    private Volk VolkVon(Konto konto) => _simulation?.Wirtschaft?.Familien.Von(konto)?.Oberhaupt.Volk ?? Volk.Mensch;

    /// <summary>Entfernt Figuren und Schilder, die bei einer Änderung neu gebaut werden (Ratsherren, Richter, Gefangene).</summary>
    private void Wegraeumen(InnenAnsicht a, List<Arbeiterfigur> figuren, List<Node3D> schilder)
    {
        foreach (var f in figuren)
        {
            a.Besucher.Remove(f);
            SitzendeEntfernen(f); // auch aus den bewegten Figuren des Baukastens
        }
        foreach (var s in schilder) KnotenEntfernen(s);
        figuren.Clear();
        schilder.Clear();
    }

    /// <summary>Jedes Bild in der Innenansicht eines Sonderbaus: tagsüber laufen die Amtsgeschäfte.</summary>
    private void SonderbauAktualisieren(SonderbauAnsicht a, Spielzeit zeit, bool arbeitszeit, double delta)
    {
        GemeinsamAktualisieren(a, zeit, arbeitszeit, arbeitszeit, delta, 1);
        BesucherBewegen(a);
        if (a == _rathaus) RatssitzungAktualisieren(delta);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rathaus
    // ═════════════════════════════════════════════════════════════════════════

    private void RathausBauen(Kartengebaeude g)
    {
        // Außen der Neubau gebaeude/neubau/rathaus.glb (16 × 10 m, Vorderseite +Z zum Markt, Turm rechts, 22 m hoch), in Köln koelner_rathaus.glb
        // (22 × 13 m: Saalbau mit dem Hansasaal und links davon der Ratsturm als Baustelle). Innen in beiden die sieben Räume des Neubaus im Haus
        // selbst (Stadtansicht.Rathausneubau.cs), in Köln mittig in der größeren Hülle; das Haus bleibt dabei in der Stadt stehen.
        var koeln = g.Katalog == "koelner_rathaus";
        var lage = ModellLage(g);
        var p = lage.Origin;
        var drehung = DrehungVon(g);
        var flaeche = GroesseVon(g);
        var a = SonderbauAnlegen("Rathaus", string.IsNullOrEmpty(g.Name) ? "Rathaus" : g.Name, p, new Vector3(flaeche.X, koeln ? 16 : 10, flaeche.Y), new Vector3(0, 26, 0),
            zoom: 21, reichweite: 12, raumansicht: true, drehung: drehung);
        var huelle = ModellEinsetzen(g.Baustein?.Modell ?? NeubauOrdner + "rathaus", this, p, new ModellZustand(), "Rathaus");
        if (huelle is not null) huelle.Wurzel.Transform = lage;
        a.Huelle = huelle?.Wurzel;
        if (koeln) a.ZusatzKlicks.Add(Klickflaeche("Rathaus", lage * new Vector3(-7.8f, 4.6f, 2.2f), new Vector3(6.2f, 9.2f, 6.2f), drehung: drehung)); // der Ratsturm im Bau
        else a.ZusatzKlicks.Add(Klickflaeche("Rathaus", lage * new Vector3(5.7f, 11, 2.5f), new Vector3(4.2f, 22, 4.2f), drehung: drehung)); // der Turm

        _rathaus = a;
        RathausNeuVorbereiten(a, huelle, fremdeHuelle: koeln); // die sieben Räume im Haus, Raumleiste mit der Eingangshalle zuerst
        RatssaalModellBauen(a);                                // die Sitzung tagt in der Ratshalle
        var schildGericht = RathausNeuSchild(a, RaumGericht, "Schild_Gericht");
        var schildAmt = RathausNeuSchild(a, RaumAmt, "Schild_Amt");
        var schildKerker = RathausNeuSchild(a, RaumKerker, "Schild_Kerker");
        var stufen = _simulation?.Wirtschaft?.Daten.Staende.Count ?? 6;

        // Kerker: der Wächter vor dem Gitter
        var waechter = RathausNeuPlatz(a, RaumKerker, "Platz_Waechter", Bewegung.Stehen, Werkzeug.Speer, false) is (var wEltern, var wPlatz)
            ? PersonBauen(a, wEltern, wPlatz, Volk.Mensch, Stadtrot, Kopfputz.Helm, 0.2f) : null;
        if (waechter is not null) a.Besucher.Add(waechter);

        // Amtsstube: der Stadtschreiber am Kanzleipult, die Marke auf der Ständetafel
        var schreiber = RathausNeuPlatz(a, RaumAmt, "Platz_Schreiber", Bewegung.Schreiben, Werkzeug.Feder, true) is (var sEltern, var sPlatz)
            ? PersonBauen(a, sEltern, sPlatz, Volk.Mensch, new Color(0.22f, 0.26f, 0.36f), Kopfputz.Barett, 0.4f) : null;
        if (schreiber is not null) a.Besucher.Add(schreiber);
        var marke = RathausNeuStandmarke();
        var leiter = RathausNeuStaendeleiterAnlegen(stufen);   // die Stände mit Schwelle auf der Ständetafel, der eigene hervorgehoben
        if (leiter.Count > 0 && schildAmt is not null) schildAmt.VerticalAlignment = VerticalAlignment.Bottom;   // das Raumschild steht über der Leiter, nicht auf ihr

        // ── Figuren, die sich mit der Simulation ändern ──
        var ratsfiguren = new List<Arbeiterfigur>();
        var ratsschilder = new List<Node3D>();
        var gerichtsfiguren = new List<Arbeiterfigur>();
        var gerichtsschilder = new List<Node3D>();
        var gefangene = new List<Arbeiterfigur>();
        var keinSchild = new List<Node3D>();
        string? ratStand = null, gerichtStand = null, kerkerStand = null; // null = noch nie gebaut
        // Die Ratsherren sitzen in der Ratshalle an den Ankern der Sitzung (Stadtansicht.Rathausneubau.cs)
        var ratSitze = RathausNeuSitzplaetze(a) ?? Array.Empty<Arbeitsplatz>();

        a.Zustand = (zeit, arbeitszeit, aktiv) =>
        {
            if (_simulation?.Wirtschaft is not { } w || _rathausNeuInnen is not { } innen) return;
            var gulden = w.Daten.Allgemein.Waehrung;

            // Ratshalle: Ämter nach Rang (Bürgermeister zuerst), je Sitz der Inhaber
            var aemter = w.Rat.IstAktiv ? w.Rat.Aemter.OrderByDescending(x => x.Stufe).Take(ratSitze.Length).ToList() : new List<Amt>();
            var stand = string.Join("|", aemter.Select(x => x.Inhaber));
            if (stand != ratStand)
            {
                Wegraeumen(a, ratsfiguren, ratsschilder);
                for (var i = 0; i < aemter.Count; i++)
                {
                    var amtX = aemter[i];
                    if (!amtX.IstBesetzt) continue;
                    // Baukasten: ein Haus zeigt sein Oberhaupt (dieselbe Person wie in Kontor und Wohnstube), ein Ratsherr der Stadt sich selbst;
                    // der Bürgermeister mit Amtskette (wie in der Ratssitzung)
                    var rezept = RatsRezept(w, amtX.Konto is { } k ? k.Inhaber : amtX.Ratsherr!.Name, amtX.Konto, i == 0);
                    var figur = InnenFigurBauen(a, ratSitze[i], rezept, "Sitzen", "Sitzen", FigurenBaukasten.Sitzflaeche, vorderkante: 0.275f, eltern: innen, mitte: a.Mitte); // Stuhl 0,55 m tief
                    ratsfiguren.Add(figur);
                    a.Besucher.Add(figur);
                    if (amtX.Konto is not null) // Häuser im Amt beschriften – so findet man den eigenen Sitz
                        ratsschilder.Add(RathausBeschriftung($"{amtX.Name}: {amtX.Inhaber}", a.Mitte + ratSitze[i].Ort + new Vector3(0, 1.75f, 0)));
                }
                BaukastenMelden("Ratssaal", ratsfiguren.Count);
                ratStand = stand;
            }
            foreach (var f in ratsfiguren) f.Koerper.Visible = arbeitszeit && _ratssitzung is null; // in der Sitzung sitzen dort die Figuren der Sitzung
            foreach (var schild in ratsschilder) schild.Visible = _ratssitzung is null;

            // Gerichtssaal: das Verfahren, das den Spieler betrifft, sonst das erste
            var verfahren = w.Justiz.IstAktiv
                ? w.Justiz.Verfahren.OrderByDescending(v => v.Anklaeger == _spielerKonto || v.Beklagter == _spielerKonto).FirstOrDefault()
                : null;
            // Ohne Verfahren zählt, wer Stadtrichter ist – sonst stimmte der leere Text mit dem Anfang überein und der Richter fehlte.
            var gStand = verfahren is null ? "richter:" + w.Rat.Aemter.FirstOrDefault(x => x.Name == "Stadtrichter")?.Inhaber : $"{verfahren.Anklaeger.Inhaber}>{verfahren.Beklagter.Inhaber}:{string.Join(",", verfahren.Richter.Select(r => r.Konto?.Inhaber ?? r.Ratsherr?.Name))}";
            if (gStand != gerichtStand)
            {
                Wegraeumen(a, gerichtsfiguren, gerichtsschilder);
                if (verfahren is not null)
                {
                    for (var i = 0; i < Math.Min(3, verfahren.Richter.Count); i++)
                    {
                        var r = verfahren.Richter[i];
                        var (bewegung, werkzeug) = i == 1 ? (Bewegung.Schreiben, Werkzeug.Feder) : (Bewegung.Beten, Werkzeug.Keins);
                        if (RathausNeuPlatz(a, RaumGericht, $"Platz_Richter_{i + 1}", bewegung, werkzeug, true) is not (var eltern, var platz)) continue;
                        gerichtsfiguren.Add(PersonBauen(a, eltern, platz, r.Konto is { } k ? VolkVon(k) : r.Ratsherr?.Daten.Volk ?? Volk.Mensch,
                            r.Konto is { } k2 ? Besitzerfarbe(k2) : Robe, Kopfputz.Barett, i));
                    }
                    foreach (var (konto, anker) in new[] { (verfahren.Beklagter, "Platz_Beklagter"), (verfahren.Anklaeger, "Platz_Klaeger") })
                    {
                        if (RathausNeuPlatz(a, RaumGericht, anker, Bewegung.Stehen, Werkzeug.Keins, false) is not (var eltern, var platz)) continue;
                        gerichtsfiguren.Add(PersonBauen(a, eltern, platz, VolkVon(konto), Besitzerfarbe(konto), Kopfputz.Keiner, anker.Length));
                        gerichtsschilder.Add(RathausBeschriftung(konto.Inhaber, a.Mitte + platz.Ort + new Vector3(0, 2.05f, 0)));
                    }
                }
                else if (w.Rat.IstAktiv && w.Rat.Aemter.FirstOrDefault(x => x.Name == "Stadtrichter") is { IstBesetzt: true } richterAmt
                         && RathausNeuPlatz(a, RaumGericht, "Platz_Richter_2", Bewegung.Schreiben, Werkzeug.Feder, true) is (var eltern, var platz))
                {
                    // Ohne Verfahren erledigt der Stadtrichter allein Schreibarbeit.
                    gerichtsfiguren.Add(PersonBauen(a, eltern, platz, richterAmt.Konto is { } k ? VolkVon(k) : richterAmt.Ratsherr!.Daten.Volk,
                        richterAmt.Konto is { } k2 ? Besitzerfarbe(k2) : Robe, Kopfputz.Barett, 0));
                }
                a.Besucher.AddRange(gerichtsfiguren);
                gerichtStand = gStand;
            }
            foreach (var f in gerichtsfiguren) f.Koerper.Visible = arbeitszeit;

            // Kerker: Häuser, deren Oberhaupt einsitzt, stehen hinter dem Gitter
            var imKerker = w.Justiz.IstAktiv ? w.Konten.Where(k => w.Justiz.KerkerBis(k) is not null).Take(3).ToList() : new List<Konto>();
            var kStand = string.Join("|", imKerker.Select(k => k.Inhaber));
            if (kStand != kerkerStand)
            {
                Wegraeumen(a, gefangene, keinSchild);
                for (var i = 0; i < imKerker.Count; i++)
                    if (RathausNeuPlatz(a, RaumKerker, $"Platz_Gefangener_{i + 1}", Bewegung.Stehen, Werkzeug.Keins, false) is (var eltern, var platz))
                        gefangene.Add(PersonBauen(a, eltern, platz, VolkVon(imKerker[i]), Besitzerfarbe(imKerker[i]), Kopfputz.Keiner, i));
                kerkerStand = kStand;
            }
            var letztes = w.Justiz.IstAktiv ? w.Justiz.LetzteUrteile.LastOrDefault() : null;
            if (schildGericht is not null)
                schildGericht.Text = "Gerichtssaal"
                    + (verfahren is not null
                        ? $"\n{verfahren.Anklaeger.Inhaber} klagt {verfahren.Beklagter.Inhaber} an · Prognose: {w.Justiz.Prognose(verfahren).Count(x => x.Schuldig)} von {verfahren.Richter.Count} für schuldig"
                        : letztes is not null ? $"\nLetztes Urteil: {letztes.Beklagter.Inhaber} {(letztes.Schuldig ? "schuldig" : "freigesprochen")}" : "\nKein Verfahren anhängig")
                    + (imKerker.Count > 0 ? $"\nIm Kerker: {string.Join(", ", imKerker.Select(k => $"{k.Inhaber} (bis {w.Justiz.KerkerBis(k)})"))}" : "");
            if (schildKerker is not null)
                schildKerker.Text = "Kerker" + (imKerker.Count > 0
                    ? "\n" + string.Join("\n", imKerker.Select(k => $"{k.Inhaber} – bis {w.Justiz.KerkerBis(k)}"))
                    : "\nDie Zellen sind leer");

            // Amtsstube: Stand des Spielers auf der Ständetafel
            if (_spielerKonto is { } sp)
            {
                var stufe = Math.Clamp(sp.Stand, 0, stufen - 1);
                if (marke is var (standmarke, unten, oben)) standmarke.Position = new Vector3(0, Mathf.Lerp(unten, oben, stufen > 1 ? stufe / (float)(stufen - 1) : 0), 0);
                RathausNeuStaendeleiterSetzen(leiter, w.Daten.Staende, sp.Stand, gulden);
                var naechster = w.NaechsterStand(sp);
                if (schildAmt is not null)
                    schildAmt.Text = $"Amtsstube\nEuer Stand: {w.StandVon(sp).Name}"
                        + (naechster is not null ? $"\nNächster: {naechster.Name} ab {Zahlenformat.Ganz(naechster.Schwelle)} {gulden}" : "")
                        + $"\nStatut {w.Statut.Kuerzel()} – {w.Statut.Name()}";
            }
            else if (schildAmt is not null) schildAmt.Text = "Amtsstube";
            if (schreiber is not null) schreiber.Koerper.Visible = arbeitszeit;
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kirchen mit Modell (die romanischen Kirchen Kölns)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Eine Kirche mit eigenem Modell (Katalog <c>kirche_…</c>, z. B. <c>koeln_st_gereon.glb</c>): nur von außen, ohne Innenraum. Die Fenster sind dunkel
    /// und leuchten zur Messe wie die des Doms (<see cref="_kirchenFenster"/>), das Ewige Licht brennt im Westportal, bei St. Severin dreht sich das
    /// Tretrad des Krans (beides <see cref="ModellEinsetzen"/>). Klickfläche und Name wie beim Block einer Kirche ohne Modell (<see cref="Platzhalter"/>),
    /// Höhen aus dem Katalog (<c>klickHoehe</c>, <c>schildHoehe</c>).
    /// </summary>
    private void KircheBauen(Kartengebaeude g)
    {
        if (ModellEinsetzen(g.Baustein!.Modell!, this, Vector3.Zero, new ModellZustand()) is not { } modell)
        {
            Platzhalter(g); // die Datei fehlt
            return;
        }
        modell.Wurzel.Name = "Kirche_" + g.Kennung;
        modell.Wurzel.Transform = ModellLage(g);
        FenstergruppeSetzen(modell, Fenstergruppe.Kirche);
        _kirchenFenster.Add(modell);
        var groesse = GroesseVon(g);
        var hoehe = (float)(g.Baustein.KlickHoehe ?? 12);
        var kennung = "karte:" + g.Kennung;
        Klickflaeche(kennung, new Vector3(0, hoehe / 2, 0), new Vector3(groesse.X, hoehe, groesse.Y), modell.Wurzel);
        Beschriftung(g.Name, new Vector3(0, (float)(g.Baustein.SchildHoehe ?? hoehe + 6), 0), 44, modell.Wurzel, kennung);
    }

    /// <summary>
    /// Text für die Auswahl eines Gebäudes der Karte ohne eigene Tafel (Kennung <c>karte:&lt;Kennung&gt;</c>: die Kirchen mit Modell und die Blöcke
    /// ohne Modell): Name, bei den Kölner Kirchen eine Zeile Geschichte um 1400. Null, wenn die Kennung keine solche ist.
    /// </summary>
    public string? KartenText(string kennung)
    {
        if (!kennung.StartsWith("karte:") || _karte is null) return null;
        var k = kennung["karte:".Length..];
        var g = _karte.Gebaeude.FirstOrDefault(x => x.Kennung == k);
        if (g is null) return null;
        var name = string.IsNullOrEmpty(g.Name) ? g.Baustein?.Name ?? k : g.Name;
        if (KirchenGeschichte.TryGetValue(g.Katalog, out var geschichte))
            return $"{name}\n\n{geschichte}\n\nEin Innenraum folgt später; zur Messe leuchten die Fenster.";
        return $"{name}\n\nEin Gebäude der Stadt.";
    }

    /// <summary>Eine Zeile zu jeder der fünf Kölner Kirchen, Stand um 1400 (Tafeltext bei Klick, <see cref="KartenText"/>).</summary>
    private static readonly Dictionary<string, string> KirchenGeschichte = new()
    {
        ["kirche_gross_st_martin"] = "Benediktinerabtei am Rhein; nach dem Brand von 1378 trägt der Vierungsturm ein Notdach.",
        ["kirche_st_gereon"] = "Stiftskirche über römischen Gräbern, berühmt für ihr zehneckiges Langhaus.",
        ["kirche_st_aposteln"] = "Stiftskirche am Neumarkt mit Kleeblattchor und Laternenturm.",
        ["kirche_st_severin"] = "Stiftskirche im Süden der Stadt; der Westturm wird seit 1393 neu gebaut, der Tretradkran steht noch daneben.",
        ["kirche_st_kunibert"] = "Spätromanische Stiftskirche am Rhein im Norden der Stadt.",
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  Dom
    // ═════════════════════════════════════════════════════════════════════════

    private void DomBauen(Kartengebaeude g)
    {
        // Außen das Modell dom.glb, Ursprung in der Mitte des Langhauses (10 × 22 m, Wände 12 m hoch; innen x −4,5 … 4,5, z −10,5 … 10,5;
        // Chor hinten, ungedreht im Norden), 2,85 m hinter der Mitte der Fläche (versatz im Katalog). Der Westturm steht vor dem Portal (vorne).
        // Hineinsehen: Dach und Turm aus, dazu die Wände zur Kamera. Der Innenraum ist um die Mitte gebaut und dreht sich als Ganzes mit.
        // Kölner Dom (koelner_dom.glb, fertig gebaut – Alternativgeschichte: durch Zwerge und Elben schneller vollendet): Ursprung in der Mitte der
        // Fläche 14 × 34 m, die Türme (58 m) im Westen (vorne), der Chor im Osten. Der Innenraum ist EIN Raum aus Mittel- und Seitenschiffen mit offener
        // Arkade (x ±5,4, y −10,55 … 3 vom Ursprung, Mitte 3,775 m vor dem Ursprung), kürzer als der des Doms der übrigen Karten: der Chor mit Altar
        // rückt um 3,7 m vor. Beim Hineinsehen gehen Chor und Langhausdach („Dach“), Türme und Portalwand („Turm“) und die Wände zur Kamera weg.
        var koeln = g.Katalog == "koelner_dom";
        var lage = ModellLage(g);
        var p = lage.Origin;
        var drehung = DrehungVon(g);
        var basis = new Basis(Vector3.Up, drehung);
        var pm = koeln ? p + basis * new Vector3(0, 0, 3.775f) : p; // Mitte des Innenraums
        var klick = koeln ? new Vector3(12.4f, 32, 13.6f) : new Vector3(11.5f, 13, 24.6f);
        var a = SonderbauAnlegen("Dom", string.IsNullOrEmpty(g.Name) ? "Dom" : g.Name, pm, klick, basis * (koeln ? new Vector3(0, 46, -8.2f) : new Vector3(0, 46, 13)),
            zoom: koeln ? 24 : 27, reichweite: koeln ? 13 : 15, drehung: drehung);
        a.Innen!.Transform = new Transform3D(basis, pm - basis * pm); // Innenraum um die Mitte gedreht (seine Teile stehen bei pm + Lage im Dom)
        var v = koeln ? 3.7f : 0f; // Köln: Chor, Kanzel und Taufstein rücken zur Mitte, damit sie in den kürzeren Raum passen
        var huelle = ModellEinsetzen(g.Baustein?.Modell ?? "gebaeude/dom", this, p, new ModellZustand(), "Dom");
        if (huelle is not null)
        {
            huelle.Wurzel.Transform = lage;
            _kirchenFenster.Add(huelle);
            FenstergruppeSetzen(huelle, Fenstergruppe.Kirche);
            var dach = new Node3D { Name = "Dach_Dom", Transform = lage };
            AddChild(dach);
            foreach (var teil in new[] { "Dach", "Turm" })
                if (huelle.Wurzel.FindChild(teil, false, false) is { } knoten)
                {
                    huelle.Wurzel.RemoveChild(knoten);
                    dach.AddChild(knoten);
                }
            a.Dach = dach;
            foreach (var (name, aussen) in new[] { ("Wand_vorne", Vector3.Back), ("Wand_hinten", Vector3.Forward), ("Wand_links", Vector3.Left), ("Wand_rechts", Vector3.Right) })
                if (huelle.Wurzel.FindChild(name, false, false) is Node3D wand) a.Waende.Add((wand, basis * aussen));
            a.Huelle = huelle.Wurzel;
        }
        if (koeln)
        {
            a.ZusatzKlicks.Add(Klickflaeche("Dom", p + basis * new Vector3(0, 17, -9.5f), new Vector3(14, 34, 13.5f), drehung: drehung));    // der Chor
            a.ZusatzKlicks.Add(Klickflaeche("Dom", p + basis * new Vector3(0, 29, 13.7f), new Vector3(13.8f, 58, 5.6f), drehung: drehung)); // die Türme
        }
        else a.ZusatzKlicks.Add(Klickflaeche("Dom", p + basis * new Vector3(0, 21, 14.5f), new Vector3(7.5f, 42, 7), drehung: drehung));   // der Westturm
        // Boden und Licht im Innern: das Modell hat keinen Boden
        Quader(pm + new Vector3(0, 0.05f, 0), new Vector3(koeln ? 10.8f : 9, 0.1f, koeln ? 13.55f : 21), Material(Steinboden), a.Innen);
        foreach (var versatz in new[] { -0.25f, 0.25f })
            a.Innen!.AddChild(new OmniLight3D
            {
                LightColor = new Color(1f, 0.85f, 0.6f),
                OmniRange = 13,
                LightEnergy = 0.45f,
                Position = pm + new Vector3(0, 6.6f, (koeln ? 14 : 22) * versatz),
            });

        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var stein = Material(Mauerstein);
        var gold = Metall(Gold);
        var weiss = Material(Leinen);
        var flamme = Leuchtend(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.7f, 0.3f));
        var messe = new bool[1];

        // Gemeinsam: Chorstufe, Bänke, Kanzel, Taufstein, Leuchter
        Teil(a, new Vector3(0, 0.25f, -8.4f + v), new Vector3(9.0f, 0.3f, 4.2f), stein);
        var bankreihen = koeln ? 5 : 8;
        var bank0 = koeln ? -1.6f : -4.6f;
        var sitzplaetze = new List<(Arbeitsplatz Platz, bool Links)>();
        for (var reihe = 0; reihe < bankreihen; reihe++)
        {
            var z = bank0 + reihe * 1.55f;
            foreach (var x in new[] { -2.4f, 2.4f })
            {
                // Sitzfläche auf der Höhe des Baukastens (vorher 0,49 m: die Füße hingen 4 cm), der Unterbau darunter
                Teil(a, new Vector3(x, FigurenBaukasten.Sitzflaeche - 0.04f, z), new Vector3(3.2f, 0.08f, 0.45f), holz);
                // Unterbau schmal und nach hinten gesetzt (Platz für Saum und Füße), Lehne 3 cm weiter hinten (Rücken großer Figuren)
                Teil(a, new Vector3(x, (FigurenBaukasten.Sitzflaeche - 0.08f) / 2, z + 0.05f), new Vector3(3.0f, FigurenBaukasten.Sitzflaeche - 0.08f, 0.2f), dunkel);
                Teil(a, new Vector3(x, 0.8f, z + 0.27f), new Vector3(3.2f, 0.6f, 0.07f), holz);
                foreach (var dx in new[] { -1.0f, 0f, 1.0f })
                    sitzplaetze.Add((new Arbeitsplatz(new Vector3(x + dx, 0, z), Nord, Bewegung.Beten, Werkzeug.Keins, true), x < 0));
            }
        }
        var kanzel = new Vector3(3.6f, 0, -5.6f + v);
        Rund(a, kanzel + new Vector3(0, 0.8f, 0), 0.25f, 1.6f, stein, segmente: 8);
        Rund(a, kanzel + new Vector3(0, 2.0f, 0), 0.6f, 0.9f, holz, segmente: 8);
        Rund(a, kanzel + new Vector3(0, 3.9f, 0), 0.8f, 0.12f, dunkel, segmente: 8);   // Schalldeckel
        Rund(a, new Vector3(-3.2f, 0.5f, koeln ? 5.8f : 8.6f), 0.5f, 1.0f, stein, obenRadius: 0.65f);    // Taufstein
        Rund(a, new Vector3(-3.2f, 1.01f, koeln ? 5.8f : 8.6f), 0.55f, 0.02f, Material(Wasser));
        foreach (var x in new[] { -3.6f, 3.6f })
        {
            Rund(a, new Vector3(x, 1.1f, -7.6f + v), 0.06f, 1.6f, Metall(new Color(0.2f, 0.2f, 0.22f)), segmente: 6);
            Kerzenflamme(a, new Vector3(x, 1.925f, -7.6f + v), () => messe[0]);
        }

        // Katholisch: Hochaltar mit goldenem Retabel, Marienfigur, Weihrauchfass, Bischof mit Mitra
        var katholisch = new Node3D { Name = "Katholisch" };
        a.Innen!.AddChild(katholisch);
        Teil(a, new Vector3(0, 0.95f, -9.3f + v), new Vector3(2.6f, 1.1f, 1.0f), stein, katholisch);
        Teil(a, new Vector3(0, 1.52f, -9.3f + v), new Vector3(2.7f, 0.05f, 1.05f), Material(Ratsrot), katholisch);
        Teil(a, new Vector3(0, 3.4f, -9.95f + v), new Vector3(3.0f, 3.6f, 0.15f), gold, katholisch);
        foreach (var dx in new[] { -2.0f, 2.0f })
        {
            var fluegel = Teil(a, new Vector3(dx, 3.2f, -9.75f + v), new Vector3(1.2f, 3.0f, 0.1f), gold, katholisch);
            fluegel.RotationDegrees = new Vector3(0, dx < 0 ? 30 : -30, 0);
        }
        for (var i = 0; i < 6; i++)
        {
            Rund(a, new Vector3(-1.1f + i * 0.44f, 1.75f, -9.5f + v), 0.05f, 0.4f, weiss, katholisch, segmente: 6);
            Kerzenflamme(a, new Vector3(-1.1f + i * 0.44f, 1.95f, -9.5f + v), () => messe[0], katholisch);
        }
        Teil(a, new Vector3(-3.4f, 0.9f, -9.0f + v), new Vector3(0.7f, 1.0f, 0.7f), stein, katholisch);   // Marienfigur auf Sockel
        katholisch.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.5f }, MaterialOverride = Material(new Color(0.20f, 0.30f, 0.62f)), Position = a.Mitte + new Vector3(-3.4f, 2.15f, -9.0f + v) });
        Kugel(a, new Vector3(-3.4f, 3.05f, -9.0f + v), 0.2f, Material(Haut), katholisch);
        Teil(a, new Vector3(1.6f, 3.3f, -8.0f + v), new Vector3(0.03f, 1.6f, 0.03f), Metall(new Color(0.3f, 0.3f, 0.32f)), katholisch);  // Kette
        var weihrauch = Kugel(a, new Vector3(1.6f, 2.4f, -8.0f + v), 0.18f, gold, katholisch);
        a.Bewegt.Add(new Bewegtteil(weihrauch, Vector3.Zero, 1.6f, new Vector3(0.35f, 0, 0), weihrauch.Position));
        var bischof = PersonBauen(a, katholisch, new Arbeitsplatz(new Vector3(0, 0.4f, -8.3f + v), Sued, Bewegung.Beten, Werkzeug.Keins),
            Volk.Mensch, new Color(0.55f, 0.10f, 0.18f), Kopfputz.Mitra, 0.3f, schuerze: Gold);
        a.Besucher.Add(bischof);

        // Evangelisch: Abendmahlstisch mit Bibel vor dem Chor, Holzkreuz, Pfarrer im Talar
        var evangelisch = new Node3D { Name = "Evangelisch" };
        a.Innen.AddChild(evangelisch);
        Teil(a, new Vector3(0, 0.85f, -7.0f + v), new Vector3(2.2f, 0.9f, 0.9f), holz, evangelisch);
        Teil(a, new Vector3(0, 1.32f, -7.0f + v), new Vector3(2.3f, 0.04f, 1.0f), weiss, evangelisch);
        Teil(a, new Vector3(0.3f, 1.4f, -7.0f + v), new Vector3(0.5f, 0.1f, 0.35f), Material(new Color(0.25f, 0.12f, 0.10f)), evangelisch); // Bibel
        var kreuzLang = Teil(a, new Vector3(0, 3.6f, -10.2f + v), new Vector3(0.18f, 3.0f, 0.12f), dunkel, evangelisch);
        Teil(a, new Vector3(0, 4.3f, -10.2f + v), new Vector3(1.4f, 0.18f, 0.12f), dunkel, evangelisch);
        var pfarrerPlatz = new Arbeitsplatz(new Vector3(0, 0.4f, -7.8f + v), Sued, Bewegung.Beten, Werkzeug.Keins);
        var pfarrer = PersonBauen(a, evangelisch, pfarrerPlatz, Volk.Mensch, Robe, Kopfputz.Barett, 0.9f);
        pfarrer.Koerper.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.18f, 0.04f) }, MaterialOverride = weiss, Position = new Vector3(0, 1.25f, -0.27f) }); // Beffchen
        a.Besucher.Add(pfarrer);

        // Simultankirche: Schranke im Mittelgang
        var simultan = new Node3D { Name = "Simultan" };
        a.Innen.AddChild(simultan);
        Teil(a, new Vector3(0, 0.8f, koeln ? 2.05f : 1.0f), new Vector3(0.06f, 0.06f, koeln ? 8.9f : 11.6f), Material(Stadtrot), simultan);
        for (var i = 0; i < (koeln ? 4 : 6); i++)
            Teil(a, new Vector3(0, 0.45f, bank0 + i * 2.3f), new Vector3(0.1f, 0.9f, 0.1f), holz, simultan);

        FeuerAnlegen(a, new Vector3(0, 3.0f, -7.5f + v), flamme, lichtAn: 1.6f, lichtAus: 0.2f, glutAn: 2.5f, glutAus: 0.6f, an: () => messe[0], reichweite: 10);

        // Gläubige zur Messe: eine Figur je fünf, vorne zuerst
        var reihenfolge = sitzplaetze.Select((x, i) => (x, i)).OrderBy(x => x.x.Platz.Ort.Z).ThenBy(x => Math.Abs(x.x.Platz.Ort.X)).Select(x => x.i).ToList();
        var glaeubige = sitzplaetze.Select((x, i) => GlaeubigerBauen(a, x.Platz, i)).ToList(); // Figurmodelle wie die Passanten
        // Einwohner, die gerade im Dom sind (Passanten), sitzen einzeln: Nummer → Figur und Sitzplatz
        var betende = new Dictionary<int, (Arbeiterfigur Figur, int Platz)>();
        Infotafel(a, new Vector3(0, 5.5f, 0));

        a.Zustand = (zeit, arbeitszeit, aktiv) =>
        {
            if (_simulation?.Wirtschaft is not { } w) return;
            var glaubeAktiv = w.Glaube.IstAktiv;
            Konfession? stadtglaube = glaubeAktiv ? w.Glaube.Stadtglaube : Konfession.Katholisch;
            var istSimultan = glaubeAktiv && stadtglaube is null;
            katholisch.Visible = stadtglaube is Konfession.Katholisch || istSimultan;
            evangelisch.Visible = stadtglaube is Konfession.Evangelisch || istSimultan;
            simultan.Visible = istSimultan;
            kreuzLang.Visible = stadtglaube is Konfession.Evangelisch; // in der Simultankirche hängt das Retabel dahinter
            bischof.Koerper.Visible = arbeitszeit && katholisch.Visible;
            pfarrer.Koerper.Visible = arbeitszeit && evangelisch.Visible;

            // Messe: Wer keine Kirche eines Hauses besucht hat, kommt in den Dom.
            var k = w.Kirchenwesen;
            var stunde = zeit.UhrzeitInMinuten / 60;
            messe[0] = k.IstAktiv && stunde == k.Daten.MesseUm;
            var imDom = k.IstAktiv ? Math.Max(0, k.Daten.GlaeubigeJeJahr - k.Kirchen.Sum(b => k.DiesesJahr(b).Glaeubige)) : 0;
            var zahl = messe[0] ? Math.Min(glaeubige.Count, (int)Math.Ceiling(imDom / 5)) : 0;
            // In der Simultankirche sitzen Katholiken links, Evangelische rechts – nach ihren Anteilen in der Stadt.
            var anteilKatholisch = glaubeAktiv ? w.Glaube.Anteile.GetValueOrDefault(Konfession.Katholisch) : 1;
            var links = istSimultan ? (int)Math.Round(zahl * anteilKatholisch) : -1;
            int linksGesetzt = 0, rechtsGesetzt = 0;
            var besetzt = BetendeSetzen(a, betende, sitzplaetze, reihenfolge, istSimultan);
            foreach (var g in glaeubige) g.Koerper.Visible = false;
            foreach (var i in reihenfolge)
            {
                if (besetzt.Contains(i)) continue; // dort sitzt schon ein Einwohner
                var istLinks = sitzplaetze[i].Links;
                if (links < 0) { if (linksGesetzt + rechtsGesetzt < zahl) { glaeubige[i].Koerper.Visible = true; linksGesetzt++; } continue; }
                if (istLinks && linksGesetzt < links) { glaeubige[i].Koerper.Visible = true; linksGesetzt++; }
                else if (!istLinks && rechtsGesetzt < zahl - links) { glaeubige[i].Koerper.Visible = true; rechtsGesetzt++; }
            }
            // Zur Messe predigt der Pfarrer von der Kanzel.
            pfarrer.Koerper.Position = a.Mitte + (messe[0] && !istSimultan ? kanzel + new Vector3(-0.1f, 1.7f, 0) : pfarrerPlatz.Ort);
            pfarrer.Koerper.Rotation = new Vector3(0, messe[0] && !istSimultan ? West : Sued, 0);

            var titel = !glaubeAktiv ? "Dom"
                : istSimultan ? "Dom – Simultankirche (Glaubensfreiheit): Katholiken links, Evangelische rechts"
                : $"Dom – Hauptkirche des Stadtglaubens: {stadtglaube!.Value.Name()}";
            var messeText = !k.IstAktiv ? ""
                : messe[0] ? $"\nMesse: {Zahlenformat.Ganz(imDom)} Gläubige ohne Kirche eines Hauses"
                : stunde < k.Daten.MesseUm ? $"\nMesse um {k.Daten.MesseUm} Uhr"
                : $"\nMesse gehalten: {Zahlenformat.Ganz(imDom)} Gläubige ohne Kirche eines Hauses";
            var betenText = betende.Count == 0 ? "" : $"\nZum Gebet im Dom: {PersonenText(betende.Count)}";
            a.Infotafel!.Text = titel + messeText + betenText;
            // Betende und Gläubige sitzen und atmen: SitzendeBewegen läuft jetzt in jedem Raum einmal je Bild (Stadtansicht.cs)
        };
    }

    /// <summary>
    /// Setzt die Einwohner, die gerade im Dom sind, auf die Bänke – jeder behält seinen Platz, bis er geht. Neue nehmen den
    /// vordersten freien Platz; in der Simultankirche Katholiken links, Evangelische rechts. Gibt die besetzten Plätze zurück,
    /// damit die Gläubigen der Messe sich daneben setzen. Gibt es mehr Betende als Plätze, bleiben die übrigen ungezeigt.
    /// </summary>
    private HashSet<int> BetendeSetzen(InnenAnsicht a, Dictionary<int, (Arbeiterfigur Figur, int Platz)> betende,
        List<(Arbeitsplatz Platz, bool Links)> sitzplaetze, List<int> reihenfolge, bool simultan)
    {
        // Wer gegangen ist, steht auf.
        foreach (var nummer in betende.Keys.Where(n => !_imDom.Contains(n)).ToList())
        {
            a.Besucher.Remove(betende[nummer].Figur);
            SitzendeEntfernen(betende[nummer].Figur);
            betende.Remove(nummer);
        }
        var besetzt = betende.Values.Select(b => b.Platz).ToHashSet();
        if (_imDom.Count == betende.Count || _simulation?.Wirtschaft?.Einwohnerschaft is not { } ew) return besetzt;
        // Wer neu da ist, setzt sich.
        foreach (var e in ew.Einwohner)
        {
            if (!_imDom.Contains(e.Nummer) || betende.ContainsKey(e.Nummer)) continue;
            var links = e.Glaube == Konfession.Katholisch;
            var frei = reihenfolge.FirstOrDefault(i => !besetzt.Contains(i) && (!simultan || sitzplaetze[i].Links == links), -1);
            if (frei < 0) continue;
            betende[e.Nummer] = (SitzendeFigur(a, e, sitzplaetze[frei].Platz), frei);
            besetzt.Add(frei);
        }
        return besetzt;
    }
}
