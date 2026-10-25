using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Rathaus (Raumansicht) und Dom (Diorama) – dritter Teil der Stadtansicht.
///
/// Beide sind keine Betriebe, nutzen aber dasselbe Gerüst wie die Werkstätten (<see cref="InnenAnsicht"/>):
/// ein Knoten „Innen". Von außen stehen die Modelle <c>rathaus.glb</c> und <c>dom.glb</c> (Stadtansicht.Modelle.cs) an ihrer Lage in der Karte, auch
/// gedreht; beim Dom sind Dach, Turm und die vier Wände eigene Knoten des Modells, die beim Hineinsehen ausgeblendet werden. Die Ansicht liest nur
/// Abfragen des Kerns.
///
/// Rathaus – vier Räume, jeder mit fester Ansicht (Raumansicht, siehe Stadtansicht.Raeume.cs):
///   • Ratssaal (West): Hufeisentisch mit den sieben Ämtern. Häuser im Amt sitzen in ihrer Farbe da,
///     Ratsherren der Stadt in Ratsrot, der Bürgermeister mit Amtskette. Liegt ein Antrag vor, liegt eine Rolle auf dem Tisch.
///   • Gerichtssaal (Mitte): Richterbank auf einem Podest, Schranke, Anklagebank. Läuft ein Verfahren, sitzen die drei
///     Richter dieses Verfahrens, Ankläger und Beklagter stehen davor.
///   • Kerker: hinter Gittern die Oberhäupter der Häuser, die einsitzen; davor ein Wächter.
///   • Amtsstube (Ost): Kanzleipult des Stadtschreibers, Ständetafel mit einer Marke für den Stand des Spielers, Stadtkasse.
///   Tagsüber (8–20 Uhr) sind die Amtsinhaber da. Ein Klick in den Raum öffnet seine Tafel (Hauptszene).
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
        var (groesse, breite, bart) = Koerperbau(volk);
        var haut = volk is Volk.Ork or Volk.Oger ? new Color(0.36f, 0.50f, 0.28f) : Haut;
        return FigurBauen(eltern, a.Mitte + platz.Ort + new Vector3(0, hoehe, 0), platz.Drehung, new Vector3(groesse * breite, groesse, groesse * breite),
            gewand, haut, schuerze, kopf, platz.Werkzeug, platz.Art, takt, platz.Sitzt, bart);
    }

    /// <summary>Volk des Oberhaupts eines Hauses (für seine Figur), sonst Mensch.</summary>
    private Volk VolkVon(Konto konto) => _simulation?.Wirtschaft?.Familien.Von(konto)?.Oberhaupt.Volk ?? Volk.Mensch;

    /// <summary>Entfernt Figuren und Schilder, die bei einer Änderung neu gebaut werden (Ratsherren, Richter, Gefangene).</summary>
    private void Wegraeumen(InnenAnsicht a, List<Arbeiterfigur> figuren, List<Node3D> schilder)
    {
        foreach (var f in figuren)
        {
            a.Besucher.Remove(f);
            KnotenEntfernen(f.Koerper);
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
        // Außen das Modell rathaus.glb: Markthalle mit Ratssaal in Fachwerk (16 × 10 m, Vorderseite +Z zum Markt) und Uhrturm vorn rechts, 23,6 m hoch.
        // Innen vier Räume (Raumansicht, je 40 m auseinander); das Haus bleibt dabei in der Stadt stehen.
        var lage = ModellLage(g);
        var p = lage.Origin;
        var drehung = DrehungVon(g);
        var flaeche = GroesseVon(g);
        var a = SonderbauAnlegen("Rathaus", string.IsNullOrEmpty(g.Name) ? "Rathaus" : g.Name, p, new Vector3(flaeche.X, 10, flaeche.Y), new Vector3(0, 26, 0),
            zoom: 21, reichweite: 12, raumansicht: true, drehung: drehung);
        var huelle = ModellEinsetzen(g.Baustein?.Modell ?? "gebaeude/rathaus", this, p, new ModellZustand(), "Rathaus");
        if (huelle is not null) huelle.Wurzel.Transform = lage;
        a.Huelle = huelle?.Wurzel;
        a.ZusatzKlicks.Add(Klickflaeche("Rathaus", lage * new Vector3(6.2f, 17, 3.2f), new Vector3(4.2f, 14, 4.2f), drehung: drehung)); // der Uhrturm

        var innen = a.Innen!;
        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var gold = Metall(Gold);
        var stein = Material(Mauerstein);
        var eisen = Metall(new Color(0.30f, 0.31f, 0.34f));
        const float rat = 0, gericht = RaumAbstand, amt = 2 * RaumAbstand, kerker = 3 * RaumAbstand;
        const float tiefe = 9.4f, rueckwand = -tiefe / 2 + 0.05f;

        RaumAnlegen(a, new Raum(RaumRat, "Ratssaal", new Vector3(rat, 0, 0), new Vector3(8, 6, tiefe)), Putz, Steinboden);
        RaumAnlegen(a, new Raum(RaumGericht, "Gerichtssaal", new Vector3(gericht, 0, 0), new Vector3(8, 6, tiefe)), Putz, Steinboden);
        RaumAnlegen(a, new Raum(RaumAmt, "Amtsstube", new Vector3(amt, 0, 0), new Vector3(7, 4.5f, tiefe)), Putz, Dielen);
        RaumAnlegen(a, new Raum(RaumKerker, "Kerker", new Vector3(kerker, 0, 0), new Vector3(6.5f, 4, 7)), Mauerstein,
            new Color(0.26f, 0.24f, 0.22f), fenster: false, licht: 0.35f);
        var schildRat = Beschriftung("", a.Mitte + new Vector3(rat, 4.8f, -1.5f), 30, innen);
        var schildGericht = Beschriftung("", a.Mitte + new Vector3(gericht, 4.8f, -1.5f), 30, innen);
        var schildAmt = Beschriftung("", a.Mitte + new Vector3(amt, 3.7f, -1.5f), 30, innen);
        var schildKerker = Beschriftung("", a.Mitte + new Vector3(kerker, 3.3f, -1.0f), 30, innen);

        // ── Ratssaal: Teppich, Hufeisentisch, sieben Stühle, Stadtwappen ──
        Teil(a, new Vector3(rat, 0.11f, -0.6f), new Vector3(2.6f, 0.02f, 3.6f), Material(Ratsrot));
        Teil(a, new Vector3(rat, 0.8f, -2.4f), new Vector3(4.0f, 0.1f, 0.8f), holz);
        Teil(a, new Vector3(rat, 0.4f, -2.4f), new Vector3(3.4f, 0.7f, 0.3f), dunkel);
        foreach (var dx in new[] { -1.6f, 1.6f })
        {
            Teil(a, new Vector3(rat + dx, 0.8f, -0.4f), new Vector3(0.8f, 0.1f, 3.2f), holz);
            Teil(a, new Vector3(rat + dx, 0.4f, -0.4f), new Vector3(0.3f, 0.7f, 2.6f), dunkel);
        }
        var sitze = new[]
        {
            new Arbeitsplatz(new Vector3(rat, 0, -3.3f), Sued, Bewegung.Beten, Werkzeug.Keins, true),          // Bürgermeister
            new Arbeitsplatz(new Vector3(rat - 1.2f, 0, -3.3f), Sued, Bewegung.Schreiben, Werkzeug.Feder, true),
            new Arbeitsplatz(new Vector3(rat + 1.2f, 0, -3.3f), Sued, Bewegung.Schreiben, Werkzeug.Feder, true),
            new Arbeitsplatz(new Vector3(rat - 2.45f, 0, -1.0f), Ost, Bewegung.Beten, Werkzeug.Keins, true),
            new Arbeitsplatz(new Vector3(rat + 2.45f, 0, -1.0f), West, Bewegung.Schreiben, Werkzeug.Feder, true),
            new Arbeitsplatz(new Vector3(rat - 2.45f, 0, 0.6f), Ost, Bewegung.Schreiben, Werkzeug.Feder, true),
            new Arbeitsplatz(new Vector3(rat + 2.45f, 0, 0.6f), West, Bewegung.Beten, Werkzeug.Keins, true),
        };
        _rathaus = a;
        _ratssaalSitze = sitze; // für die sichtbare Ratssitzung (Stadtansicht.Ratssitzung.cs)
        for (var i = 0; i < sitze.Length; i++)
        {
            var s = sitze[i];
            var hinten = new Vector3(Mathf.Sin(s.Drehung), 0, Mathf.Cos(s.Drehung)) * 0.32f; // Lehne hinter der Figur
            Teil(a, s.Ort + new Vector3(0, 0.42f, 0), new Vector3(0.55f, 0.08f, 0.55f), dunkel);
            var lehne = Teil(a, s.Ort + hinten + new Vector3(0, i == 0 ? 1.2f : 0.85f, 0), new Vector3(0.55f, i == 0 ? 1.6f : 0.9f, 0.08f), i == 0 ? gold : dunkel);
            lehne.Rotation = new Vector3(0, s.Drehung, 0);
        }
        Teil(a, new Vector3(rat, 3.4f, rueckwand), new Vector3(1.2f, 1.5f, 0.06f), Material(Leinen));   // Stadtwappen
        Teil(a, new Vector3(rat, 3.6f, rueckwand + 0.02f), new Vector3(1.2f, 0.5f, 0.06f), Material(Stadtrot));
        var antragsrolle = Rund(a, new Vector3(rat, 0.92f, -2.3f), 0.07f, 0.5f, Material(Pergament), drehung: new Vector3(0, 0, 90), segmente: 8);

        // ── Gerichtssaal: Podest mit Richterbank, Richtschwert, Schranke, Anklagebank ──
        Teil(a, new Vector3(gericht, 0.2f, -3.7f), new Vector3(4.4f, 0.4f, 1.5f), stein);
        Teil(a, new Vector3(gericht, 0.75f, -3.1f), new Vector3(3.8f, 0.7f, 0.4f), dunkel); // niedrig, damit man die Richter sieht
        Teil(a, new Vector3(gericht, 3.6f, rueckwand), new Vector3(0.12f, 1.6f, 0.08f), gold);         // Richtschwert als Zeichen
        Teil(a, new Vector3(gericht, 3.9f, rueckwand), new Vector3(0.7f, 0.1f, 0.08f), gold);
        Teil(a, new Vector3(gericht, 0.85f, 0.2f), new Vector3(4.4f, 0.08f, 0.08f), holz);              // Schranke
        foreach (var dx in new[] { -2.1f, -0.7f, 0.7f, 2.1f })
            Teil(a, new Vector3(gericht + dx, 0.45f, 0.2f), new Vector3(0.08f, 0.8f, 0.08f), holz);
        Teil(a, new Vector3(gericht - 1.4f, 0.45f, 1.6f), new Vector3(1.6f, 0.08f, 0.4f), holz);        // Anklagebank
        Teil(a, new Vector3(gericht - 1.4f, 0.22f, 1.6f), new Vector3(1.4f, 0.38f, 0.12f), dunkel);

        // ── Kerker: Gitter quer durch den Raum, dahinter Stroh und die Gefangenen, davor ein Wächter ──
        var zelle = new Vector3(kerker, 0, -2.0f);
        Teil(a, new Vector3(kerker, 0.12f, -2.3f), new Vector3(5.6f, 0.03f, 2.0f), Material(Stroh));
        for (var i = 0; i < 15; i++)
            Teil(a, new Vector3(kerker - 3.1f + i * 0.44f, 1.9f, -0.6f), new Vector3(0.07f, 3.8f, 0.07f), eisen);
        foreach (var y in new[] { 0.4f, 3.5f })
            Teil(a, new Vector3(kerker, y, -0.6f), new Vector3(6.4f, 0.1f, 0.1f), eisen);
        Teil(a, new Vector3(kerker - 2.6f, 2.6f, -3.45f), new Vector3(0.15f, 0.15f, 0.1f), eisen);       // Kettenring an der Wand
        var waechter = PersonBauen(a, innen, new Arbeitsplatz(new Vector3(kerker + 1.8f, 0, 1.3f), Sued - 0.6f, Bewegung.Stehen, Werkzeug.Speer),
            Volk.Mensch, Stadtrot, Kopfputz.Helm, 0.2f);
        a.Besucher.Add(waechter);
        var fackel = Leuchtend(new Color(1f, 0.7f, 0.3f), new Color(1f, 0.55f, 0.2f));
        Teil(a, new Vector3(kerker + 3.05f, 2.4f, 0.8f), new Vector3(0.12f, 0.3f, 0.12f), fackel);
        FeuerAnlegen(a, new Vector3(kerker + 2.6f, 2.6f, 0.8f), fackel, lichtAn: 1.2f, lichtAus: 1.2f, glutAn: 2f, glutAus: 2f, an: () => true, reichweite: 7);

        // ── Amtsstube: Kanzleipult, Ständetafel, Stadtkasse, Stadtschreiber ──
        Teil(a, new Vector3(amt, 0.45f, -3.4f), new Vector3(2.0f, 0.9f, 0.8f), holz);
        Teil(a, new Vector3(amt + 0.4f, 0.93f, -3.4f), new Vector3(0.5f, 0.06f, 0.6f), Material(new Color(0.45f, 0.18f, 0.14f)));
        Teil(a, new Vector3(amt + 3.45f, 2.3f, 0.4f), new Vector3(0.06f, 3.0f, 2.4f), Material(Pergament));     // Ständetafel an der Ostwand
        var stufen = _simulation?.Wirtschaft?.Daten.Staende.Count ?? 6;
        for (var i = 0; i < stufen; i++)
            Teil(a, new Vector3(amt + 3.42f, 1.0f + i * 2.6f / stufen, 0.4f), new Vector3(0.06f, 0.05f, 2.2f), dunkel);
        var marke = Teil(a, new Vector3(amt + 3.25f, 1.15f, -0.4f), new Vector3(0.2f, 0.3f, 0.3f), Material(Spieler));
        Teil(a, new Vector3(amt - 2.2f, 0.4f, 2.4f), new Vector3(1.2f, 0.7f, 0.6f), eisen);                     // Stadtkasse
        Teil(a, new Vector3(amt - 2.2f, 0.78f, 2.4f), new Vector3(1.25f, 0.08f, 0.65f), gold);
        var schreiber = PersonBauen(a, innen, new Arbeitsplatz(new Vector3(amt, 0, -4.0f), Sued, Bewegung.Schreiben, Werkzeug.Feder), Volk.Mensch,
            new Color(0.22f, 0.26f, 0.36f), Kopfputz.Barett, 0.4f);
        a.Besucher.Add(schreiber);

        // ── Figuren, die sich mit der Simulation ändern ──
        var ratsfiguren = new List<Arbeiterfigur>();
        var ratsschilder = new List<Node3D>();
        var gerichtsfiguren = new List<Arbeiterfigur>();
        var gerichtsschilder = new List<Node3D>();
        var gefangene = new List<Arbeiterfigur>();
        var keinSchild = new List<Node3D>();
        string? ratStand = null, gerichtStand = null, kerkerStand = null; // null = noch nie gebaut

        a.Zustand = (zeit, arbeitszeit, aktiv) =>
        {
            if (_simulation?.Wirtschaft is not { } w) return;
            var gulden = w.Daten.Allgemein.Waehrung;

            // Ratssaal: Ämter nach Rang (Bürgermeister zuerst), je Sitz der Inhaber
            var aemter = w.Rat.IstAktiv ? w.Rat.Aemter.OrderByDescending(x => x.Stufe).Take(sitze.Length).ToList() : new List<Amt>();
            var stand = string.Join("|", aemter.Select(x => x.Inhaber));
            if (stand != ratStand)
            {
                Wegraeumen(a, ratsfiguren, ratsschilder);
                for (var i = 0; i < aemter.Count; i++)
                {
                    var amtX = aemter[i];
                    if (!amtX.IstBesetzt) continue;
                    var volk = amtX.Konto is { } k ? VolkVon(k) : amtX.Ratsherr!.Daten.Volk;
                    var gewand = amtX.Konto is { } k2 ? Besitzerfarbe(k2) : Ratsrot;
                    var figur = PersonBauen(a, innen, sitze[i], volk, gewand, Kopfputz.Barett, i * 0.7f);
                    if (i == 0) // Amtskette des Bürgermeisters
                        figur.Koerper.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.08f, 0.06f) }, MaterialOverride = gold, Position = new Vector3(0, 1.22f, -0.27f) });
                    ratsfiguren.Add(figur);
                    a.Besucher.Add(figur);
                    if (amtX.Konto is not null) // Häuser im Amt beschriften – so findet man den eigenen Sitz
                        ratsschilder.Add(Beschriftung($"{amtX.Name}: {amtX.Inhaber}", a.Mitte + sitze[i].Ort + new Vector3(0, 2.3f, 0), 24, innen));
                }
                ratStand = stand;
            }
            foreach (var f in ratsfiguren) f.Koerper.Visible = arbeitszeit && _ratssitzung is null; // in der Sitzung sitzen dort die Figuren der Sitzung
            foreach (var schild in ratsschilder) schild.Visible = _ratssitzung is null;
            schildRat.Visible = _ratssitzung is null;
            var statutAntrag = w.Rat.IstAktiv ? w.Rat.Antrag : null;
            var glaubeAntrag = w.Glaube.IstAktiv ? w.Glaube.Antrag : null;
            antragsrolle.Visible = statutAntrag is not null || glaubeAntrag is not null;
            schildRat.Text = "Ratssaal" + (w.Rat.IstAktiv ? $"\nBürgermeister: {w.Rat.Aemter.First().Inhaber}" : "")
                + (statutAntrag is { } sa ? $"\nAntrag: Statut {sa.Statut.Kuerzel()} ({sa.Antragsteller.Inhaber})" : "")
                + (glaubeAntrag is { } ga ? $"\nAntrag zum Stadtglauben: {KonfessionTexte.StadtglaubeText(ga.Glaube)} ({ga.Antragsteller.Inhaber})" : "");

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
                        var platz = new Arbeitsplatz(new Vector3(gericht - 1.2f + i * 1.2f, 0, -3.85f), Sued, i == 1 ? Bewegung.Schreiben : Bewegung.Beten, i == 1 ? Werkzeug.Feder : Werkzeug.Keins, true);
                        var figur = PersonBauen(a, innen, platz, r.Konto is { } k ? VolkVon(k) : r.Ratsherr?.Daten.Volk ?? Volk.Mensch,
                            r.Konto is { } k2 ? Besitzerfarbe(k2) : Robe, Kopfputz.Barett, i, hoehe: 0.4f);
                        gerichtsfiguren.Add(figur);
                    }
                    foreach (var (konto, x) in new[] { (verfahren.Beklagter, -1.4f), (verfahren.Anklaeger, 1.4f) })
                    {
                        var figur = PersonBauen(a, innen, new Arbeitsplatz(new Vector3(gericht + x, 0, 0.9f), Nord, Bewegung.Stehen, Werkzeug.Keins),
                            VolkVon(konto), Besitzerfarbe(konto), Kopfputz.Keiner, x);
                        gerichtsfiguren.Add(figur);
                        gerichtsschilder.Add(Beschriftung(konto.Inhaber, a.Mitte + new Vector3(gericht + x, 2.3f, 0.9f), 24, innen));
                    }
                }
                else if (w.Rat.IstAktiv && w.Rat.Aemter.FirstOrDefault(x => x.Name == "Stadtrichter") is { IstBesetzt: true } richterAmt)
                {
                    // Ohne Verfahren erledigt der Stadtrichter allein Schreibarbeit.
                    var platz = new Arbeitsplatz(new Vector3(gericht, 0, -3.85f), Sued, Bewegung.Schreiben, Werkzeug.Feder, true);
                    gerichtsfiguren.Add(PersonBauen(a, innen, platz, richterAmt.Konto is { } k ? VolkVon(k) : richterAmt.Ratsherr!.Daten.Volk,
                        richterAmt.Konto is { } k2 ? Besitzerfarbe(k2) : Robe, Kopfputz.Barett, 0, hoehe: 0.4f));
                }
                a.Besucher.AddRange(gerichtsfiguren);
                gerichtStand = gStand;
            }
            foreach (var f in gerichtsfiguren) f.Koerper.Visible = arbeitszeit;

            // Kerker: Häuser, deren Oberhaupt einsitzt, schauen aus der Gittergrube
            var imKerker = w.Justiz.IstAktiv ? w.Konten.Where(k => w.Justiz.KerkerBis(k) is not null).Take(3).ToList() : new List<Konto>();
            var kStand = string.Join("|", imKerker.Select(k => k.Inhaber));
            if (kStand != kerkerStand)
            {
                Wegraeumen(a, gefangene, keinSchild);
                for (var i = 0; i < imKerker.Count; i++)
                {
                    var platz = new Arbeitsplatz(zelle + new Vector3(-1.4f + i * 1.4f, 0, 0), Sued, Bewegung.Stehen, Werkzeug.Keins);
                    gefangene.Add(PersonBauen(a, innen, platz, VolkVon(imKerker[i]), Besitzerfarbe(imKerker[i]), Kopfputz.Keiner, i));
                }
                kerkerStand = kStand;
            }
            var letztes = w.Justiz.IstAktiv ? w.Justiz.LetzteUrteile.LastOrDefault() : null;
            schildGericht.Text = "Gerichtssaal"
                + (verfahren is not null
                    ? $"\n{verfahren.Anklaeger.Inhaber} klagt {verfahren.Beklagter.Inhaber} an · Prognose: {w.Justiz.Prognose(verfahren).Count(x => x.Schuldig)} von {verfahren.Richter.Count} für schuldig"
                    : letztes is not null ? $"\nLetztes Urteil: {letztes.Beklagter.Inhaber} {(letztes.Schuldig ? "schuldig" : "freigesprochen")}" : "\nKein Verfahren anhängig")
                + (imKerker.Count > 0 ? $"\nIm Kerker: {string.Join(", ", imKerker.Select(k => $"{k.Inhaber} (bis {w.Justiz.KerkerBis(k)})"))}" : "");
            schildKerker.Text = "Kerker" + (imKerker.Count > 0
                ? "\n" + string.Join("\n", imKerker.Select(k => $"{k.Inhaber} – bis {w.Justiz.KerkerBis(k)}"))
                : "\nDie Zellen sind leer");

            // Amtsstube: Stand des Spielers auf der Ständetafel
            if (_spielerKonto is { } sp)
            {
                var stufe = Math.Clamp(sp.Stand, 0, stufen - 1);
                marke.Position = a.Mitte + new Vector3(amt + 3.25f, 1.15f + stufe * 2.6f / stufen, -0.4f);
                var naechster = w.NaechsterStand(sp);
                schildAmt.Text = $"Amtsstube\nEuer Stand: {w.StandVon(sp).Name}"
                    + (naechster is not null ? $"\nNächster: {naechster.Name} ab {Zahlenformat.Ganz(naechster.Schwelle)} {gulden}" : "")
                    + $"\nStatut {w.Statut.Kuerzel()} – {w.Statut.Name()}";
            }
            else schildAmt.Text = "Amtsstube";
            schreiber.Koerper.Visible = arbeitszeit;
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Dom
    // ═════════════════════════════════════════════════════════════════════════

    private void DomBauen(Kartengebaeude g)
    {
        // Außen das Modell dom.glb, Ursprung in der Mitte des Langhauses (10 × 22 m, Wände 12 m hoch; innen x −4,5 … 4,5, z −10,5 … 10,5;
        // Chor hinten, ungedreht im Norden), 2,85 m hinter der Mitte der Fläche (versatz im Katalog). Der Westturm steht vor dem Portal (vorne).
        // Hineinsehen: Dach und Turm aus, dazu die Wände zur Kamera. Der Innenraum ist um die Mitte gebaut und dreht sich als Ganzes mit.
        var lage = ModellLage(g);
        var p = lage.Origin;
        var drehung = DrehungVon(g);
        var basis = new Basis(Vector3.Up, drehung);
        var a = SonderbauAnlegen("Dom", string.IsNullOrEmpty(g.Name) ? "Dom" : g.Name, p, new Vector3(11.5f, 13, 24.6f), basis * new Vector3(0, 46, 13),
            zoom: 27, reichweite: 15, drehung: drehung);
        a.Innen!.Transform = new Transform3D(basis, p - basis * p); // Innenraum um die Mitte gedreht (seine Teile stehen bei p + Lage im Dom)
        var huelle = ModellEinsetzen(g.Baustein?.Modell ?? "gebaeude/dom", this, p, new ModellZustand(), "Dom");
        if (huelle is not null)
        {
            huelle.Wurzel.Transform = lage;
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
        a.ZusatzKlicks.Add(Klickflaeche("Dom", p + basis * new Vector3(0, 21, 14.5f), new Vector3(7.5f, 42, 7), drehung: drehung));   // der Westturm
        // Boden und Licht im Innern: das Modell hat keinen Boden
        Quader(p + new Vector3(0, 0.05f, 0), new Vector3(9, 0.1f, 21), Material(Steinboden), a.Innen);
        foreach (var versatz in new[] { -0.25f, 0.25f })
            a.Innen!.AddChild(new OmniLight3D
            {
                LightColor = new Color(1f, 0.85f, 0.6f),
                OmniRange = 13,
                LightEnergy = 0.45f,
                Position = p + new Vector3(0, 6.6f, 22 * versatz),
            });

        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var stein = Material(Mauerstein);
        var gold = Metall(Gold);
        var weiss = Material(Leinen);
        var flamme = Leuchtend(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.7f, 0.3f));
        var messe = new bool[1];

        // Gemeinsam: Chorstufe, Bänke, Kanzel, Taufstein, Leuchter
        Teil(a, new Vector3(0, 0.25f, -8.4f), new Vector3(9.0f, 0.3f, 4.2f), stein);
        var bankreihen = 8;
        var sitzplaetze = new List<(Arbeitsplatz Platz, bool Links)>();
        for (var reihe = 0; reihe < bankreihen; reihe++)
        {
            var z = -4.6f + reihe * 1.55f;
            foreach (var x in new[] { -2.4f, 2.4f })
            {
                Teil(a, new Vector3(x, 0.45f, z), new Vector3(3.2f, 0.08f, 0.45f), holz);
                Teil(a, new Vector3(x, 0.22f, z), new Vector3(3.0f, 0.4f, 0.35f), dunkel);
                Teil(a, new Vector3(x, 0.8f, z + 0.24f), new Vector3(3.2f, 0.6f, 0.07f), holz);
                foreach (var dx in new[] { -1.0f, 0f, 1.0f })
                    sitzplaetze.Add((new Arbeitsplatz(new Vector3(x + dx, 0, z), Nord, Bewegung.Beten, Werkzeug.Keins, true), x < 0));
            }
        }
        var kanzel = new Vector3(3.6f, 0, -5.6f);
        Rund(a, kanzel + new Vector3(0, 0.8f, 0), 0.25f, 1.6f, stein, segmente: 8);
        Rund(a, kanzel + new Vector3(0, 2.0f, 0), 0.6f, 0.9f, holz, segmente: 8);
        Rund(a, kanzel + new Vector3(0, 3.9f, 0), 0.8f, 0.12f, dunkel, segmente: 8);   // Schalldeckel
        Rund(a, new Vector3(-3.2f, 0.5f, 8.6f), 0.5f, 1.0f, stein, obenRadius: 0.65f);    // Taufstein
        Rund(a, new Vector3(-3.2f, 1.01f, 8.6f), 0.55f, 0.02f, Material(Wasser));
        foreach (var x in new[] { -3.6f, 3.6f })
        {
            Rund(a, new Vector3(x, 1.1f, -7.6f), 0.06f, 1.6f, Metall(new Color(0.2f, 0.2f, 0.22f)), segmente: 6);
            Teil(a, new Vector3(x, 2.0f, -7.6f), new Vector3(0.08f, 0.15f, 0.08f), flamme);
        }

        // Katholisch: Hochaltar mit goldenem Retabel, Marienfigur, Weihrauchfass, Bischof mit Mitra
        var katholisch = new Node3D { Name = "Katholisch" };
        a.Innen!.AddChild(katholisch);
        Teil(a, new Vector3(0, 0.95f, -9.3f), new Vector3(2.6f, 1.1f, 1.0f), stein, katholisch);
        Teil(a, new Vector3(0, 1.52f, -9.3f), new Vector3(2.7f, 0.05f, 1.05f), Material(Ratsrot), katholisch);
        Teil(a, new Vector3(0, 3.4f, -9.95f), new Vector3(3.0f, 3.6f, 0.15f), gold, katholisch);
        foreach (var dx in new[] { -2.0f, 2.0f })
        {
            var fluegel = Teil(a, new Vector3(dx, 3.2f, -9.75f), new Vector3(1.2f, 3.0f, 0.1f), gold, katholisch);
            fluegel.RotationDegrees = new Vector3(0, dx < 0 ? 30 : -30, 0);
        }
        for (var i = 0; i < 6; i++)
        {
            Rund(a, new Vector3(-1.1f + i * 0.44f, 1.75f, -9.5f), 0.05f, 0.4f, weiss, katholisch, segmente: 6);
            Teil(a, new Vector3(-1.1f + i * 0.44f, 2.0f, -9.5f), new Vector3(0.06f, 0.1f, 0.06f), flamme, katholisch);
        }
        Teil(a, new Vector3(-3.4f, 0.9f, -9.0f), new Vector3(0.7f, 1.0f, 0.7f), stein, katholisch);   // Marienfigur auf Sockel
        katholisch.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.5f }, MaterialOverride = Material(new Color(0.20f, 0.30f, 0.62f)), Position = a.Mitte + new Vector3(-3.4f, 2.15f, -9.0f) });
        Kugel(a, new Vector3(-3.4f, 3.05f, -9.0f), 0.2f, Material(Haut), katholisch);
        Teil(a, new Vector3(1.6f, 3.3f, -8.0f), new Vector3(0.03f, 1.6f, 0.03f), Metall(new Color(0.3f, 0.3f, 0.32f)), katholisch);  // Kette
        var weihrauch = Kugel(a, new Vector3(1.6f, 2.4f, -8.0f), 0.18f, gold, katholisch);
        a.Bewegt.Add(new Bewegtteil(weihrauch, Vector3.Zero, 1.6f, new Vector3(0.35f, 0, 0), weihrauch.Position));
        var bischof = PersonBauen(a, katholisch, new Arbeitsplatz(new Vector3(0, 0.4f, -8.3f), Sued, Bewegung.Beten, Werkzeug.Keins),
            Volk.Mensch, new Color(0.55f, 0.10f, 0.18f), Kopfputz.Mitra, 0.3f, schuerze: Gold);
        a.Besucher.Add(bischof);

        // Evangelisch: Abendmahlstisch mit Bibel vor dem Chor, Holzkreuz, Pfarrer im Talar
        var evangelisch = new Node3D { Name = "Evangelisch" };
        a.Innen.AddChild(evangelisch);
        Teil(a, new Vector3(0, 0.85f, -7.0f), new Vector3(2.2f, 0.9f, 0.9f), holz, evangelisch);
        Teil(a, new Vector3(0, 1.32f, -7.0f), new Vector3(2.3f, 0.04f, 1.0f), weiss, evangelisch);
        Teil(a, new Vector3(0.3f, 1.4f, -7.0f), new Vector3(0.5f, 0.1f, 0.35f), Material(new Color(0.25f, 0.12f, 0.10f)), evangelisch); // Bibel
        var kreuzLang = Teil(a, new Vector3(0, 3.6f, -10.2f), new Vector3(0.18f, 3.0f, 0.12f), dunkel, evangelisch);
        Teil(a, new Vector3(0, 4.3f, -10.2f), new Vector3(1.4f, 0.18f, 0.12f), dunkel, evangelisch);
        var pfarrerPlatz = new Arbeitsplatz(new Vector3(0, 0.4f, -7.8f), Sued, Bewegung.Beten, Werkzeug.Keins);
        var pfarrer = PersonBauen(a, evangelisch, pfarrerPlatz, Volk.Mensch, Robe, Kopfputz.Barett, 0.9f);
        pfarrer.Koerper.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.18f, 0.04f) }, MaterialOverride = weiss, Position = new Vector3(0, 1.25f, -0.27f) }); // Beffchen
        a.Besucher.Add(pfarrer);

        // Simultankirche: Schranke im Mittelgang
        var simultan = new Node3D { Name = "Simultan" };
        a.Innen.AddChild(simultan);
        Teil(a, new Vector3(0, 0.8f, 1.0f), new Vector3(0.06f, 0.06f, 11.6f), Material(Stadtrot), simultan);
        for (var i = 0; i < 6; i++)
            Teil(a, new Vector3(0, 0.45f, -4.6f + i * 2.3f), new Vector3(0.1f, 0.9f, 0.1f), holz, simultan);

        FeuerAnlegen(a, new Vector3(0, 3.0f, -7.5f), flamme, lichtAn: 1.6f, lichtAus: 0.2f, glutAn: 2.5f, glutAus: 0.6f, an: () => messe[0], reichweite: 10);

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
            SitzendeBewegen(); // Betende und Gläubige sitzen und atmen (Stadtansicht.Passanten.cs)
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
