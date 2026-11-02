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
/// Hand zu Hand; die vorderen zwei schütten. Ist das Feuer aus, gehen alle zurück und verschwinden dort, wo sie herkamen. Die Eimer sind ein
/// Platzhalter (ein Holzzylinder mit zwei dunklen Reifen), bis es ein Modell gibt.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Abstand der Leute in der Kette (Meter).</summary>
    private const float KettenAbstand = 1.7f;

    /// <summary>So schnell wandert ein Eimer die Kette entlang (Meter je echter Sekunde, bei Tempo 1).</summary>
    private const float EimerTempo = 1.6f;

    private sealed class Helfer
    {
        public required Node3D Knoten { get; init; }
        public Figur? Figur { get; init; }
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
        public List<MeshInstance3D> Eimer { get; } = new();
        /// <summary>Die Plätze der Kette vom Wasser (0) zum Haus.</summary>
        public Vector3[] Kette { get; set; } = Array.Empty<Vector3>();
        public float Zeit { get; set; }
        public bool Fertig { get; set; }
    }

    private static Mesh? _eimerNetz;
    private static Material? _eimerStoff, _reifStoff;

    /// <summary>Ein Eimer (Platzhalter): Daube aus Holz, zwei dunkle Reifen.</summary>
    private MeshInstance3D EimerBauen(Node3D eltern)
    {
        _eimerNetz ??= new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.12f, Height = 0.26f, RadialSegments = 10, Rings = 1 };
        _eimerStoff ??= new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.31f, 0.18f), Roughness = 0.9f };
        _reifStoff ??= new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.11f, 0.10f), Roughness = 0.6f, Metallic = 0.4f };
        var eimer = new MeshInstance3D { Name = "Eimer", Mesh = _eimerNetz, MaterialOverride = _eimerStoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        foreach (var h in new[] { -0.07f, 0.07f })
            eimer.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.142f + h * 0.1f, BottomRadius = 0.142f + h * 0.1f, Height = 0.025f, RadialSegments = 10, Rings = 1 },
                MaterialOverride = _reifStoff, Position = new Vector3(0, h, 0) });
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
        var saat = (uint)(e.Minute * 2246822519L) ^ (uint)e.Ziel.Kennung.GetHashCode(StringComparison.Ordinal);
        var anzahl = 6 + (int)(saat % 10); // 6–15
        // Die Kette: vom Rand des Hauses (der Seite zum Wasser) zum Wasser; reicht sie nicht, beginnt sie am Haus
        var richtung = new Vector3(wasser.X - haus.X, 0, wasser.Z - haus.Z);
        if (richtung.LengthSquared() < 1) richtung = Vector3.Back;
        var weit = richtung.Length();
        richtung /= weit;
        var vorne = haus + richtung * (halb + 1.6f);
        var platz = Mathf.Max(0, weit - halb - 1.6f - 1.2f);
        var glieder = Math.Clamp((int)(platz / KettenAbstand) + 1, 2, anzahl);
        var quer = new Vector3(-richtung.Z, 0, richtung.X);
        var kette = new Vector3[glieder];
        for (var i = 0; i < glieder; i++)
        {
            // vom Wasser (0) zum Haus (glieder − 1); ein leichtes Schlängeln, die Leute stehen nicht wie mit dem Lineal
            var k = glieder - 1 - i;
            var p = vorne + richtung * (k * KettenAbstand) + quer * (0.35f * Mathf.Sin(k * 1.7f + saat % 7));
            kette[i] = AufFuss(p);
        }
        var wurzel = new Node3D { Name = "Loeschkette_" + e.Ziel.Kennung };
        AddChild(wurzel);
        var lk = new Loeschkette { Einschlag = e, Wurzel = wurzel, Kette = kette };
        // Herkunft: Knoten des Gassennetzes 25–70 m vom Haus, gestreut
        var knoten = Enumerable.Range(0, _netz.Knoten.Count)
            .Select(i => (I: i, D: new Vector2(_netz.Knoten[i].X - haus.X, _netz.Knoten[i].Z - haus.Z).Length()))
            .Where(x => x.D is > 25f and < 70f).OrderBy(x => (x.I * 2654435761u) ^ saat).Take(anzahl).Select(x => x.I).ToList();
        if (knoten.Count == 0) knoten.Add(_netz.KnotenBei(haus + richtung * 30f));
        for (var n = 0; n < anzahl; n++)
        {
            // die ersten bilden die Kette (vom Haus her), wer übrig ist, schüttet mit am Haus
            var inKette = n < glieder;
            var stelle = inKette ? kette[glieder - 1 - n] : AufFuss(vorne + quer * ((n - glieder) % 2 == 0 ? 1.4f : -1.4f) * (1 + (n - glieder) / 2) - richtung * 0.4f);
            var schuettet = !inKette || n == 0;
            var blick = schuettet ? Mathf.Atan2(-richtung.X, -richtung.Z) : Mathf.Atan2(quer.X, quer.Z) + (n % 2 == 0 ? 0 : Mathf.Pi);
            var herkunft = _netz.Knoten[knoten[n % knoten.Count]];
            var knotenFigur = new Node3D { Name = "Helfer" + n, Position = AufFuss(herkunft) };
            wurzel.AddChild(knotenFigur);
            var nummer = 90000 + (int)(saat % 5000) * 16 + n;
            var figur = FigurEinsetzen(knotenFigur, AussehenAusNummer(nummer), nummer);
            var h = new Helfer { Knoten = knotenFigur, Figur = figur, Herkunft = herkunft, Platz = stelle, Blick = blick, Schuettet = schuettet, Losgehen = 0.4f * n };
            HelferPfad(h, herkunft, stelle);
            lk.Helfer.Add(h);
        }
        for (var i = 0; i < Math.Max(2, glieder / 2); i++)
        {
            var eimer = EimerBauen(wurzel);
            eimer.Visible = false;
            lk.Eimer.Add(eimer);
        }
        GD.Print($"[blitz] Löschkette an {e.Ziel.Kennung}: {anzahl} Leute, {glieder} in der Kette, Wasser {wasser} ({weit:0} m)");
        return lk;
    }

    /// <summary>Der Weg über die Gassen von <paramref name="von"/> zum Platz <paramref name="nach"/>, das letzte Stück quer über den Platz.</summary>
    private void HelferPfad(Helfer h, Vector3 von, Vector3 nach)
    {
        var a = _netz.KnotenBei(von);
        var b = _netz.KnotenBei(nach);
        var pfad = new List<Vector3> { von };
        if (a >= 0 && b >= 0) pfad.AddRange(_netz.Pfad(a, b));
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
        var echt = _simulation is { Takt.IstPausiert: false } ? (float)delta : 0f; // in der Pause steht alles
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
                h.Knoten.Visible = false;
            }
        }
        EimerBewegen(lk, brennt);
        if (!alleFort) return;
        LoeschketteEntfernen(lk);
    }

    /// <summary>
    /// Die Eimer wandern gleichmäßig verteilt vom Wasser zum Haus, je Stück zwischen zwei Leuten in einem kleinen Bogen (von Hand zu Hand); vorn
    /// werden sie geleert und tauchen am Wasser wieder auf. Erst, wenn die Kette steht.
    /// </summary>
    private void EimerBewegen(Loeschkette lk, bool brennt)
    {
        var steht = brennt && lk.Helfer.Count(h => h.Stand == 1) >= Math.Min(lk.Kette.Length, lk.Helfer.Count);
        var n = lk.Kette.Length;
        for (var i = 0; i < lk.Eimer.Count; i++)
        {
            var eimer = lk.Eimer[i];
            if (!steht || n < 2) { eimer.Visible = false; continue; }
            var weg = (n - 1) * KettenAbstand;
            var s = (lk.Zeit * EimerTempo + i * weg / lk.Eimer.Count) % weg;
            var stueck = Mathf.Min(n - 2, (int)(s / KettenAbstand));
            var t = s / KettenAbstand - stueck;
            var p = lk.Kette[stueck].Lerp(lk.Kette[stueck + 1], t);
            eimer.Position = p + new Vector3(0, 0.75f + 0.18f * Mathf.Sin(t * Mathf.Pi), 0);
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
        if (IsInstanceValid(lk.Wurzel)) lk.Wurzel.QueueFree();
        GD.Print($"[blitz] Löschkette an {lk.Einschlag.Ziel.Kennung} aufgelöst");
    }
}
