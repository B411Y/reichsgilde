using Godot;
using Reichsgilde.Core.Gefahren;

namespace Reichsgilde;

/// <summary>
/// Die Löschkette beim Blitzschlag (Teil der <c>partial class Stadtansicht</c>): <b>nur Darstellung</b>, der Kern weiß davon nichts – ob und wie lange
/// es brennt, entscheidet er allein (<see cref="Blitzwesen"/>).
///
/// Brennt ein Haus, laufen 6–15 Leute aus der Nähe herbei (eigene Figuren, die Passanten des Kerns gehen ihren Weg weiter): Sie kommen über die
/// Gassen von Knoten 25–70 m entfernt, stellen sich in einer Reihe zwischen Wasser und Haus auf – vom Brunnen des nächsten Marktes oder vom
/// Ufer des nächsten Flusses, ist das Wasser weiter als die Kette reicht, steht sie am Haus und die Eimer kommen von hinten – und reichen Eimer von
/// Hand zu Hand; die vorderen zwei schütten. Ist das Feuer aus, gehen alle zurück und verschwinden dort, wo sie herkamen. Die Eimer
/// (Modell <c>requisiten/eimer</c>) gehen voll zum Haus und leer zurück.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Abstand der Leute in der Kette (Meter).</summary>
    private const float KettenAbstand = 1.7f;

    /// <summary>So viele Leute stehen mindestens in der Kette (zu wenige Passanten: eigene Figuren füllen auf) und so weit stehen sie höchstens auseinander – sonst wäre die Kette bei weitem Wasser nicht zu erkennen.</summary>
    private const int KettenMindestens = 8;
    private const float KettenAbstandMax = 3.4f;

    /// <summary>So schnell wandert ein Eimer die Kette entlang (Meter je echter Sekunde, bei Tempo 1).</summary>
    private const float EimerTempo = 1.6f;

    private sealed class Helfer
    {
        public required Node3D Knoten { get; init; }
        public Figur? Figur { get; init; }
        /// <summary>Der eingezogene Passant (die Figur gehört dann ihm, nicht der Kette); null bei einer eigenen Figur (nur Reserve, wenn zu wenige Passanten in der Nähe sind).</summary>
        public Passant? Passant { get; init; }
        /// <summary>Wasserspritzer beim Schütten (nur bei den Schüttern, ein Zeichenaufruf).</summary>
        public Wasserspritzer? Spritzer { get; set; }
        public required Vector3 Herkunft { get; init; }
        public required Vector3 Platz { get; init; }
        /// <summary>Blickrichtung am Platz (Winkel um die Hochachse).</summary>
        public float Blick { get; init; }
        public bool Schuettet { get; init; }
        public Vector3[] Pfad { get; set; } = Array.Empty<Vector3>();
        public float Laenge { get; set; }
        public float Strecke { get; set; }
        /// <summary>0 = kommt, 1 = steht in der Kette, 2 = geht zurück, 3 = fort.</summary>
        public int Stand { get; set; }
        /// <summary>Wann sie losläuft (echte Sekunden nach dem Beginn der Kette) – nicht alle zugleich.</summary>
        public float Losgehen { get; init; }
    }

    private sealed class Loeschkette
    {
        public required Blitzeinschlag Einschlag { get; init; }
        public required Node3D Wurzel { get; init; }
        public List<Helfer> Helfer { get; } = new();
        public List<Node3D> Eimer { get; } = new();
        /// <summary>Die Plätze der Kette vom Wasser (0) zum Haus.</summary>
        public Vector3[] Kette { get; set; } = Array.Empty<Vector3>();
        public float Zeit { get; set; }
        /// <summary>Abstand der Leute in dieser Kette (Meter): so dicht wie möglich, so weit wie nötig, damit sie das Wasser erreicht.</summary>
        public float Abstand { get; set; } = KettenAbstand;
        public bool Fertig { get; set; }
    }

    /// <summary>
    /// Ein Eimer: das Modell <c>requisiten/eimer.glb</c> (Knoten „Eimer_0“ nah, „Eimer_1“ fern und „Wasser“, Ursprung Bodenmitte). <paramref name="voll"/>: mit Wasser
    /// (Hinweg zum Haus), sonst leer (Rückweg zum Wasser). Fehlt die Datei, bleibt ein leerer Knoten (das Spiel meldet den Fehler).
    /// </summary>
    private Node3D EimerBauen(Node3D eltern, bool voll)
    {
        var eimer = Modell("requisiten/eimer")?.Instantiate<Node3D>() ?? new Node3D();
        eimer.Name = voll ? "EimerVoll" : "EimerLeer";
        if (eimer.FindChild("Wasser", true, false) is Node3D wasser) wasser.Visible = voll;
        // zwei Stufen: Eimer_0 (194 Dreiecke) bis 25 m vor der Kamera, darüber Eimer_1 (82); das Wasser nur nah
        const float Stufenwechsel = 25f, Uebergang = 2f;
        if (eimer.FindChild("Eimer_0", true, false) is GeometryInstance3D nah)
            (nah.VisibilityRangeEnd, nah.VisibilityRangeEndMargin) = (Stufenwechsel, Uebergang);
        if (eimer.FindChild("Eimer_1", true, false) is GeometryInstance3D fern)
            (fern.VisibilityRangeBegin, fern.VisibilityRangeBeginMargin) = (Stufenwechsel, Uebergang);
        if (eimer.FindChild("Wasser", true, false) is GeometryInstance3D wasserNetz)
            (wasserNetz.VisibilityRangeEnd, wasserNetz.VisibilityRangeEndMargin) = (Stufenwechsel, Uebergang);
        foreach (var teil in eimer.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        eltern.AddChild(eimer);
        return eimer;
    }

    /// <summary>Beginnt die Löschkette an einem brennenden Haus; null, wenn es kein Gassennetz gibt.</summary>
    private Loeschkette? LoeschketteBeginnen(Blitzeinschlag e, ModellInstanz? modell)
    {
        if (_netz.Knoten.Count == 0) return null;
        var haus = new Vector3((float)e.Ziel.X, 0, (float)e.Ziel.Z);
        var halb = 4f;
        if (modell is not null && IsInstanceValid(modell.Wurzel))
        {
            var (min, max) = WeltUmriss(modell.Wurzel);
            if (min.X <= max.X)
            {
                haus = new Vector3((min.X + max.X) / 2, 0, (min.Z + max.Z) / 2);
                halb = Mathf.Min(max.X - min.X, max.Z - min.Z) / 2;
            }
        }
        var wasser = NaechstesWasser(haus);
        var saat = (uint)(e.Minute * 2246822519L) ^ (uint)e.Ziel.Kennung.Sum(c => c * 31);
        var anzahl = 6 + (int)(saat % 10); // 6–15
        // Die Helfer sind echte Passanten aus der Nähe (Wunsch des Nutzers: keine eigenen Figuren); nur wenn kaum jemand unterwegs ist, ergänzen eigene Figuren auf mindestens vier
        var echte = PassantenFuerKette(haus, anzahl);
        anzahl = Math.Max(Math.Max(echte.Count, anzahl), KettenMindestens);
        // Die Kette: vom Rand des Hauses (der Seite zum Wasser) zum Wasser; reicht sie nicht, beginnt sie am Haus
        var richtung = new Vector3(wasser.X - haus.X, 0, wasser.Z - haus.Z);
        if (richtung.LengthSquared() < 1) richtung = Vector3.Back;
        var weit = richtung.Length();
        richtung /= weit;
        var vorne = haus + richtung * (halb + 1.6f);
        var platz = Mathf.Max(0, weit - halb - 1.6f - 1.2f);
        var glieder = Math.Clamp((int)(platz / KettenAbstand) + 1, 2, anzahl);
        // Reicht die Kette bei engem Stand nicht bis zum Wasser, stehen die Leute weiter auseinander (bis KettenAbstandMax)
        var abstand = glieder > 1 ? Mathf.Clamp(platz / (glieder - 1), KettenAbstand, KettenAbstandMax) : KettenAbstand;
        glieder = Math.Clamp(glieder, 2, anzahl);
        var quer = new Vector3(-richtung.Z, 0, richtung.X);
        var kette = new Vector3[glieder];
        for (var i = 0; i < glieder; i++)
        {
            // vom Wasser (0) zum Haus (glieder − 1); ein leichtes Schlängeln, die Leute stehen nicht wie mit dem Lineal
            var k = glieder - 1 - i;
            var p = vorne + richtung * (k * abstand) + quer * (0.35f * Mathf.Sin(k * 1.7f + saat % 7));
            kette[i] = AufFuss(p);
        }
        var wurzel = new Node3D { Name = "Loeschkette_" + e.Ziel.Kennung };
        AddChild(wurzel);
        var lk = new Loeschkette { Einschlag = e, Wurzel = wurzel, Kette = kette, Abstand = abstand };
        // Herkunft: Knoten des Gassennetzes 25–70 m vom Haus, gestreut
        var knoten = Enumerable.Range(0, _netz.Knoten.Count)
            .Select(i => (I: i, D: new Vector2(_netz.Knoten[i].X - haus.X, _netz.Knoten[i].Z - haus.Z).Length()))
            .Where(x => x.D is > 25f and < 70f).OrderBy(x => (x.I * 2654435761u) ^ saat).Take(anzahl).Select(x => x.I).ToList();
        if (knoten.Count == 0) knoten.Add(NaechsterKnoten(haus + richtung * 30f));
        for (var n = 0; n < anzahl; n++)
        {
            // die ersten bilden die Kette (vom Haus her), wer übrig ist, schüttet mit am Haus
            var inKette = n < glieder;
            var stelle = inKette ? kette[glieder - 1 - n] : AufFuss(vorne + quer * ((n - glieder) % 2 == 0 ? 1.4f : -1.4f) * (1 + (n - glieder) / 2) - richtung * 0.4f);
            var schuettet = !inKette || n <= 1;
            var blick = schuettet ? Mathf.Atan2(-richtung.X, -richtung.Z) : Mathf.Atan2(quer.X, quer.Z) + (n % 2 == 0 ? 0 : Mathf.Pi);
            Helfer h;
            if (n < echte.Count)
            {
                // ein echter Passant: er geht von dort los, wo er gerade ist (mit seiner Figur, Tracht und Nummer)
                var p = echte[n];
                p.Eingezogen = true;
                var hier = new Vector3(p.Knoten.Position.X, 0, p.Knoten.Position.Z);
                h = new Helfer { Knoten = p.Knoten, Figur = p.Figur, Passant = p, Herkunft = hier, Platz = stelle, Blick = blick, Schuettet = schuettet, Losgehen = 0.4f * n };
                HelferPfad(h, hier, stelle);
            }
            else
            {
                var herkunft = _netz.Knoten[knoten[n % knoten.Count]];
                var knotenFigur = new Node3D { Name = "Helfer" + n, Position = AufFuss(herkunft) };
                wurzel.AddChild(knotenFigur);
                var nummer = 90000 + (int)(saat % 5000) * 16 + n;
                var figur = FigurEinsetzen(knotenFigur, AussehenAusNummer(nummer), nummer);
                h = new Helfer { Knoten = knotenFigur, Figur = figur, Herkunft = herkunft, Platz = stelle, Blick = blick, Schuettet = schuettet, Losgehen = 0.4f * n };
                HelferPfad(h, herkunft, stelle);
            }
            if (schuettet)
            {
                h.Spritzer = new Wasserspritzer { Name = "Spritzer", Visible = false, Position = new Vector3(0, 1.05f, 0.35f), Scale = new Vector3(1f, 1f, 2.2f) };
                h.Knoten.AddChild(h.Spritzer);
            }
            lk.Helfer.Add(h);
        }
        for (var i = 0; i < 2 * Math.Max(1, glieder / 2); i++)
        {
            var eimer = EimerBauen(wurzel, voll: i % 2 == 0); // gerade voll zum Haus, ungerade leer zurück
            eimer.Visible = false;
            lk.Eimer.Add(eimer);
        }
        if (OS.GetCmdlineUserArgs().Contains("--kettenblick=1"))
        {
            // Startoption --kettenblick=1: die Kamera auf die Mitte zwischen Haus und Wasser stellen (zum Ansehen der Kette)
            var mitte = (haus + kette[0]) * 0.5f;
            KameraSetzen(mitte.X, mitte.Z, Mathf.Clamp(25f + (glieder - 1) * abstand * 0.8f, 30f, 50f));
        }
        GD.Print($"[blitz] Kette: Abstand {abstand:0.0} m, erster Platz {kette[0]}, letzter {kette[^1]}, Haus {haus}, {2 * Math.Max(1, glieder / 2)} Eimer, Schütter {lk.Helfer.Count(x => x.Schuettet)}");
        GD.Print($"[blitz] Löschkette an {e.Ziel.Kennung}: {anzahl} Leute ({echte.Count} echte Passanten), {glieder} in der Kette, Wasser {wasser} ({weit:0} m)");
        return lk;
    }

    /// <summary>Die sichtbaren Passanten 12–90 m um das Haus, die nächsten zuerst (höchstens <paramref name="anzahl"/>); wer gerade in einer anderen Löschkette steht, zählt nicht.</summary>
    private List<Passant> PassantenFuerKette(Vector3 haus, int anzahl) =>
        _passanten.Values
            .Where(p => !p.Eingezogen && p.Figur is not null && p.Knoten.Visible && IsInstanceValid(p.Knoten) && p.Knoten.IsInsideTree())
            .Select(p => (P: p, D: new Vector2(p.Knoten.Position.X - haus.X, p.Knoten.Position.Z - haus.Z).Length()))
            .Where(x => x.D is > 12f and < 90f)
            .OrderBy(x => x.D).Take(anzahl).Select(x => x.P).ToList();

    /// <summary>Der Helfer ist fertig: ein Passant geht wieder seinen Weg (die Planung der Passanten übernimmt), eine eigene Figur verschwindet.</summary>
    private static void HelferFreigeben(Helfer h)
    {
        if (h.Spritzer is not null && IsInstanceValid(h.Spritzer)) h.Spritzer.Visible = false;
        if (h.Passant is { } p) p.Eingezogen = false;
        else h.Knoten.Visible = false;
    }

    /// <summary>Der Weg über die Gassen von <paramref name="von"/> zum Platz <paramref name="nach"/>, das letzte Stück quer über den Platz.</summary>
    private void HelferPfad(Helfer h, Vector3 von, Vector3 nach)
    {
        // Nur vorhandene Knoten (NaechsterKnoten, Stadtansicht.Passantenwege.cs): KnotenBei legte neue an und ließe alle Passanten ihre Wege neu suchen (Befund der Prüfung)
        var a = NaechsterKnoten(von);
        var b = NaechsterKnoten(nach);
        var pfad = new List<Vector3> { von };
        if (a >= 0 && b >= 0 && a != b) pfad.AddRange(_netz.Pfad(a, b));
        pfad.Add(nach);
        h.Pfad = pfad.Select(p => new Vector3(p.X, 0, p.Z)).ToArray();
        h.Laenge = Gassennetz.Laenge(h.Pfad);
        h.Strecke = 0;
    }

    /// <summary>Wasser zum Löschen: der Brunnen des nächsten Marktes oder das Ufer des nächsten Flusses bzw. Bachs, was näher ist.</summary>
    private Vector3 NaechstesWasser(Vector3 haus)
    {
        var bestes = haus + new Vector3(0, 0, 30);
        var abstand = float.MaxValue;
        foreach (var m in _maerkte)
        {
            var d = new Vector2(m.Brunnen.X - haus.X, m.Brunnen.Z - haus.Z).Length() - m.BrunnenRadius;
            if (d < abstand) (bestes, abstand) = (m.Brunnen, d);
        }
        foreach (var g in _karte?.Wasser ?? new List<Reichsgilde.Core.Bauen.Gewaesser>())
        {
            if (!g.IstLinie || g.Linie is not { Count: >= 2 } linie) continue;
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var (ax, az, bx, bz) = ((float)linie[i][0], (float)linie[i][1], (float)linie[i + 1][0], (float)linie[i + 1][1]);
                var ab = new Vector2(bx - ax, bz - az);
                var t = ab.LengthSquared() > 0 ? Mathf.Clamp(new Vector2(haus.X - ax, haus.Z - az).Dot(ab) / ab.LengthSquared(), 0, 1) : 0;
                var p = new Vector2(ax, az) + ab * t;
                var breite = linie[i].Length > 2 ? (float)linie[i][2] : g.Breite > 0 ? (float)g.Breite : 8f;
                var zumHaus = new Vector2(haus.X, haus.Z) - p;
                var d = zumHaus.Length() - breite / 2;
                if (d >= abstand || zumHaus.LengthSquared() < 1e-4f) continue;
                var ufer = p + zumHaus.Normalized() * (breite / 2 + 1f);
                (bestes, abstand) = (new Vector3(ufer.X, 0, ufer.Y), d);
            }
        }
        return bestes;
    }

    /// <summary>Jedes Bild: Leute laufen herbei, reichen Eimer, schütten; nach dem Feuer zurück und fort.</summary>
    private void LoeschketteAktualisieren(Loeschkette lk, double minuteGenau, double delta)
    {
        if (lk.Fertig || !IsInstanceValid(lk.Wurzel)) return;
        var echt = _simulation is { Takt.IstPausiert: false, Ratssitzung: null } ? (float)delta : 0f; // in der Pause und während der Ratssitzung steht alles
        var tempo = (float)Math.Clamp(_simulation?.Takt.Tempo ?? 1, 0.5, 4);
        var sekunden = echt * tempo; // im schnellen Spiel laufen und reichen sie schneller, sonst wären sie erst da, wenn es aus ist
        lk.Zeit += sekunden;
        var brennt = minuteGenau < lk.Einschlag.BrandEnde;
        var alleFort = true;
        foreach (var h in lk.Helfer)
        {
            if (!brennt && h.Stand < 2)
            {
                h.Stand = 2; // das Feuer ist aus: zurück, wo sie herkam
                HelferPfad(h, h.Knoten.Position, h.Herkunft);
            }
            if (h.Stand == 3) continue;
            alleFort = false;
            if (h.Passant is not null && h.Figur is { } pf)
            {
                // Die Planung der Passanten überspringt den Helfer, also auch ihre Sichtbarkeit: die Figur zeigen (gebackene Probefiguren zeichnet das MultiMesh)
                var zeigen = !(pf.Probe && _probeVat && !pf.ProbeNah);
                if (pf.Modell.Visible != zeigen) pf.Modell.Visible = zeigen;
            }
            if (h.Spritzer is { } spritzer) spritzer.Visible = h.Stand == 1 && brennt;
            if (h.Stand == 1)
            {
                if (h.Figur is { } f)
                {
                    BewegungSetzen(f, h.Schuettet || EimerNahe(lk, h.Platz) ? "Buecken" : "Stehen", 1.4f * tempo);
                    FigurImBildBewegen(f, h.Knoten.Position, echt);
                }
                continue;
            }
            if (h.Stand == 0 && lk.Zeit < h.Losgehen) continue;
            // laufen: eilig hin, gemächlich zurück
            var eilig = h.Stand == 0;
            h.Strecke += sekunden * (eilig ? EilenTempo : GehenTempo);
            var anteil = h.Laenge > 0 ? Mathf.Min(1, h.Strecke / h.Laenge) : 1;
            var hier = Gassennetz.PunktAuf(h.Pfad, anteil, h.Laenge);
            var weiter = Gassennetz.PunktAuf(h.Pfad, Mathf.Min(1, anteil + 0.4f / Mathf.Max(h.Laenge, 0.4f)), h.Laenge);
            var r = weiter - hier;
            if (r.LengthSquared() > 1e-6f) h.Knoten.Rotation = new Vector3(0, Mathf.Atan2(r.X, r.Z), 0);
            h.Knoten.Position = AufFuss(hier);
            if (h.Figur is { } figur)
            {
                BewegungSetzen(figur, eilig ? "Eilen" : "Gehen", tempo / figur.Beinfaktor); // Schritt passend zum Tempo (wie die Streife)
                FigurImBildBewegen(figur, h.Knoten.Position, echt);
            }
            if (anteil < 1) continue;
            if (h.Stand == 0)
            {
                h.Stand = 1;
                h.Knoten.Position = h.Platz;
                h.Knoten.Rotation = new Vector3(0, h.Blick, 0);
            }
            else
            {
                h.Stand = 3;
                HelferFreigeben(h);
            }
        }
        EimerBewegen(lk, brennt);
        if (!alleFort) return;
        LoeschketteEntfernen(lk);
    }

    /// <summary>
    /// Die Eimer wandern gleichmäßig verteilt vom Wasser zum Haus, je Stück zwischen zwei Leuten in einem kleinen Bogen (von Hand zu Hand); vorn
    /// werden sie geleert; die leeren wandern auf der anderen Seite der Reihe zurück zum Wasser. Erst, wenn die Kette steht.
    /// </summary>
    private void EimerBewegen(Loeschkette lk, bool brennt)
    {
        var steht = brennt && lk.Helfer.Count(h => h.Stand == 1) >= Math.Max(2, Math.Min(lk.Kette.Length, lk.Helfer.Count) * 2 / 3); // die Eimer wandern, sobald zwei Drittel der Kette stehen (Nachzügler stören nicht)
        var n = lk.Kette.Length;
        for (var i = 0; i < lk.Eimer.Count; i++)
        {
            var eimer = lk.Eimer[i];
            if (!steht || n < 2) { eimer.Visible = false; continue; }
            var weg = (n - 1) * lk.Abstand;
            var zurueck = i % 2 == 1; // leer vom Haus zum Wasser, etwas versetzt auf der anderen Seite der Reihe
            var s = (lk.Zeit * EimerTempo + (i / 2) * weg / Math.Max(1, lk.Eimer.Count / 2)) % weg;
            if (zurueck) s = weg - s;
            var stueck = Mathf.Clamp((int)(s / lk.Abstand), 0, n - 2);
            var t = Mathf.Clamp(s / lk.Abstand - stueck, 0, 1);
            var p = lk.Kette[stueck].Lerp(lk.Kette[stueck + 1], t);
            var laengs = lk.Kette[stueck + 1] - lk.Kette[stueck];
            var quer = new Vector3(-laengs.Z, 0, laengs.X).Normalized() * (zurueck ? -0.3f : 0.3f);
            eimer.Position = p + quer + new Vector3(0, 0.85f + 0.3f * Mathf.Sin(t * Mathf.Pi), 0); // auf Brusthöhe, von Hand zu Hand im Bogen
            eimer.Scale = Vector3.One * 1.7f;      // deutlich sichtbar (aus der Spielkamera ist ein Eimer von 0,3 m ein Fleck)
            eimer.Rotation = new Vector3(0, Mathf.Atan2(laengs.X, laengs.Z), 0);
            eimer.Visible = true;
        }
    }

    /// <summary>Ist gerade ein Eimer bei diesem Platz (dann bückt sich die Figur und reicht ihn weiter)?</summary>
    private static bool EimerNahe(Loeschkette lk, Vector3 platz) =>
        lk.Eimer.Any(e => e.Visible && new Vector2(e.Position.X - platz.X, e.Position.Z - platz.Z).LengthSquared() < 0.45f * 0.45f);

    private void LoeschketteEntfernen(Loeschkette lk)
    {
        if (lk.Fertig) return;
        lk.Fertig = true;
        foreach (var h in lk.Helfer.Where(h => h.Stand != 3)) HelferFreigeben(h); // Passanten gehen wieder ihren Weg (ihre Knoten hängen nicht an der Kette)
        if (IsInstanceValid(lk.Wurzel)) lk.Wurzel.QueueFree();
        GD.Print($"[blitz] Löschkette an {lk.Einschlag.Ziel.Kennung} aufgelöst");
    }
}
