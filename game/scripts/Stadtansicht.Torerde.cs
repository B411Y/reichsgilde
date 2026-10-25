using System.Threading.Tasks;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Erde im Torweg – ein Teil der <c>partial class Stadtansicht</c> (Schritt 5j, Übergang Pflaster–Erde). Der Boden des Tordurchgangs gehört zum
/// Modell der Torburg (<c>stadtmauer.glb</c>, „Tor“: Steinboden 3,6 × 5,2 m, 6 cm über dem Gelände); der Boden-Shader erreicht ihn nicht, und die
/// verschmierte Erde der Wegeschicht liegt unter ihm (die Grenze Erde–Pflaster ist die Mauerlinie mitten im Tor). Darum bekommt jede Torburg, durch
/// die ein Erdweg (Trampelpfad oder Fahrweg) führt, ein <see cref="Decal"/>: ausgetretene Erde auf den Steinen, dicht an der Schwelle und nach innen
/// in Flecken ausdünnend, bis ein paar Meter aufs Stadtpflaster; auf einem Fahrweg laufen die Spurrillen hinein und verblassen. Eine gepflasterte
/// oder bekieste Zufahrt bleibt sauber. Gerechnet wird mit der Wegeschicht (<see cref="WegeschichtBild"/>): bei jeder Änderung der Wege für alle Tore,
/// ein neues Bild aber nur, wenn sich der Weg am Tor geändert hat.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Torburgen der Ansicht (Spiel und Werkstatt); eine neue meldet <see cref="TorErdeMerken"/>.</summary>
    private readonly List<MeshInstance3D> _torKnoten = new();

    private bool _torErdeVeraltet;

    /// <summary>
    /// Die Ebene (Godot <c>Layers</c>, Ebene 2), auf die das Decal wirkt: Nur die Geländekacheln und die Torburgen tragen sie zusätzlich – Figuren und
    /// Karren im Tor bekommen keine Erde ab.
    /// </summary>
    private const uint TorErdeEbene = 1u << 1;

    /// <summary>Der Torweg im Modell (lokal, Meter): halbe Breite zwischen den Wänden, Schwelle außen (+Z) und innen.</summary>
    private const float TorwegHalb = 1.8f, TorSchwelle = 2.6f;

    /// <summary>So weit reicht das Decal hinter die innere Schwelle aufs Pflaster, so breit ist es (Meter), so viele Bildpunkte je Meter.</summary>
    private const float TorErdeInnen = 6.5f, TorErdeBreite = 4.4f, TorErdeDichte = 40f;

    /// <summary>So viel grauer als die Erde-Textur ist die Erde im Tor (Anteil).</summary>
    private const float TorErdeGrau = 0.2f;

    /// <summary>Maßstab der Bodentexturen (Meter je Wiederholung, wie <c>massstab</c> in <c>boden.gdshaderinc</c>).</summary>
    private const float TorErdeMassstab = 6f;

    /// <summary>Die Erde-Textur des Bodens auf <see cref="TorErdeDichte"/> verkleinert (RGBA8, quadratisch) – einmal gelesen.</summary>
    private (byte[] Daten, int Seite)? _torErdeTextur;

    /// <summary>Eine Torburg ist neu gezeichnet (<see cref="MauerAufbauen"/>): beim nächsten Bild bekommt sie ihre Erde.</summary>
    private void TorErdeMerken(MeshInstance3D tor)
    {
        _torKnoten.Add(tor);
        _torErdeVeraltet = true;
    }

    /// <summary>Der Weg am Tor, wie das Bild ihn braucht: Fahrweg-Anteil (Rillen), Austritt, Versatz der Wegmitte und halbe Breite (Meter).</summary>
    private readonly record struct Torweg(float Fahrweg, float Austritt, float Versatz, float Halb);

    /// <summary>
    /// Gibt jeder Torburg ihr Decal (oder nimmt es weg), aus den Teilen der Wegeschicht (<see cref="SchichtTeile"/>). Ohne <paramref name="teile"/>
    /// werden sie hier gesammelt – nur wenn eine Torburg neu ist und die Wege sich nicht geändert haben.
    /// </summary>
    private void TorErdeNachfuehren(List<Schichtteil>? teile)
    {
        _torKnoten.RemoveAll(t => !IsInstanceValid(t) || t.IsQueuedForDeletion());
        _torErdeVeraltet = false;
        if (_torKnoten.Count == 0) return;
        teile ??= SchichtTeile().Teile;
        var zurueck = GlobalTransform.AffineInverse(); // Wege und Boden rechnen in den Maßen der Ansicht
        foreach (var tor in _torKnoten)
        {
            var lage = zurueck * tor.GlobalTransform;
            var weg = TorwegVon(lage, teile);
            var alt = tor.GetNodeOrNull<Decal>("Torerde");
            if (weg is not { } w)
            {
                if (alt is not null)
                {
                    tor.RemoveChild(alt);
                    alt.QueueFree();
                }
                continue;
            }
            if (alt is not null && alt.HasMeta("weg") && alt.GetMeta("weg").AsString() == w.ToString()) continue;
            if (TorErdeBild(lage, w) is not { } bild) return;
            if (alt is null)
            {
                // Lokal: X entlang der Mauer, Z zur Feldseite; projiziert wird von oben (−Y) auf Boden und Torburg (CullMask). Senkrechte Flächen
                // (Wände, Torflügel) bleiben frei (NormalFade); der Sockel der Türme (0,9 m hoch) liegt über dem Kasten.
                var laenge = TorSchwelle * 2 + TorErdeInnen + 0.2f;
                alt = new Decal
                {
                    Name = "Torerde",
                    Size = new Vector3(TorErdeBreite, 1.6f, laenge),
                    Position = new Vector3(0, 0.03f, TorSchwelle + 0.2f - laenge / 2),
                    AlbedoMix = 1,
                    NormalFade = 0.5f,
                    UpperFade = 2f, LowerFade = 0.5f,
                    CullMask = TorErdeEbene,
                };
                tor.AddChild(alt);
            }
            alt.TextureAlbedo = ImageTexture.CreateFromImage(bild);
            alt.SetMeta("weg", w.ToString());
        }
    }

    /// <summary>
    /// Führt ein Erdweg durch das Tor mit der Lage <paramref name="lage"/>? Gesucht wird an fünf Stellen quer über die äußere Schwelle (ein Weg kann
    /// schräg aufs Tor treffen) und 1,9 m davor: Dort muss ein Weg liegen, und der stärkste Belag dort (wie in der Wegeschicht) muss Trampelpfad oder
    /// Fahrweg sein. Liegt dort eine Brücke (Lücke im Belag, etwa über einen Graben), zählt der Belag gleich neben ihr. Null: kein Weg oder ein
    /// befestigter.
    /// </summary>
    private static Torweg? TorwegVon(Transform3D lage, List<Schichtteil> teile)
    {
        var o = new Vector2(lage.Origin.X, lage.Origin.Z);
        var entlang = new Vector2(lage.Basis.X.X, lage.Basis.X.Z).Normalized();
        var aussen = new Vector2(lage.Basis.Z.X, lage.Basis.Z.Z).Normalized();
        var schwelle = o + aussen * TorSchwelle;
        Schichtteil? bester = null;
        var (stufe, stelle) = (-1f, 0f);
        var proben = new[] { -1.6f, -0.8f, 0f, 0.8f, 1.6f }.Select(u => schwelle + aussen * 0.4f + entlang * u).Append(schwelle + aussen * 1.9f);
        foreach (var vor in proben)
            foreach (var teil in teile)
            {
                if (teil.Flaeche || !teil.Rahmen.HasPoint(vor)) continue;
                var (abstand, s) = LinieNaechster(teil, vor);
                if (abstand > teil.Halb) continue;
                var st = SchichtStufeBei(teil, s);
                if (float.IsNaN(st)) st = StufeNebenLuecke(teil, s);
                if (float.IsNaN(st) || st <= stufe) continue;
                (bester, stufe, stelle) = (teil, st, s);
            }
        if (bester is null || stufe >= 1.5f) return null; // kein Weg, oder Kies, Pflaster, Baustelle
        var austritt = SchichtWertBei(bester.Austritt, stelle);
        // Wo die Wegmitte die Linie der Schwelle kreuzt (quer zum Tor gemessen; sonst ihr nächster Punkt): Dort setzen die Rillen innen an
        var versatz = SchwelleGekreuzt(bester, schwelle, entlang) ?? (LinienPunkt(bester, LinieNaechster(bester, schwelle).Stelle) - schwelle).Dot(entlang);
        versatz = Mathf.Clamp(versatz, -0.9f, 0.9f);
        // gerundet: kleine Änderungen anderswo am Weg rechnen das Bild nicht neu
        return new Torweg(MathF.Round(Mathf.Clamp(stufe, 0, 1) * 10) / 10, MathF.Round(austritt * 10) / 10, MathF.Round(versatz * 20) / 20,
            MathF.Round(Mathf.Min(bester.Halb, TorwegHalb) * 10) / 10);
    }

    /// <summary>Wo die Linie eines Weges die Gerade durch <paramref name="schwelle"/> (Richtung <paramref name="entlang"/>) bis 4 m daneben kreuzt: der Abstand
    /// entlang; null, wenn sie das nicht tut.</summary>
    private static float? SchwelleGekreuzt(Schichtteil teil, Vector2 schwelle, Vector2 entlang)
    {
        var quer = new Vector2(-entlang.Y, entlang.X);
        float? bester = null;
        for (var i = 0; i + 1 < teil.Linie.Length; i++)
        {
            var (da, db) = ((teil.Linie[i] - schwelle).Dot(quer), (teil.Linie[i + 1] - schwelle).Dot(quer));
            if (da * db > 0 || MathF.Abs(da - db) < 1e-6f) continue;
            var u = (teil.Linie[i].Lerp(teil.Linie[i + 1], da / (da - db)) - schwelle).Dot(entlang);
            if (MathF.Abs(u) <= 4f && (bester is null || MathF.Abs(u) < MathF.Abs(bester.Value))) bester = u;
        }
        return bester;
    }

    /// <summary>Der Belag gleich neben einer Lücke (Brücke) an der Stelle <paramref name="s"/>: der nächste Wert ohne Lücke, bis 40 m weit.</summary>
    private static float StufeNebenLuecke(Schichtteil teil, float s)
    {
        var k = Math.Clamp((int)MathF.Round(s / SchichtBelagSchritt), 0, teil.Stufe.Length - 1);
        for (var d = 1; d <= 80; d++)
        {
            if (k - d >= 0 && !float.IsNaN(teil.Stufe[k - d])) return teil.Stufe[k - d];
            if (k + d < teil.Stufe.Length && !float.IsNaN(teil.Stufe[k + d])) return teil.Stufe[k + d];
        }
        return float.NaN;
    }

    /// <summary>
    /// Das Bild des Decals (Farbe und Deckung): Spalte = lokal X, Zeile = lokal Z von innen (−Z) nach außen. Die Erde ist die Erde-Textur des Bodens an
    /// derselben Stelle der Welt, gedeckt wie die verschmierte Erde auf dem Pflaster (Rauschen des Boden-Shaders): im Torweg dicht, hinter der inneren
    /// Schwelle auf 3–6 m (quer wechselnd) in Flecken ausdünnend, zu den Wänden und neben der Gasse etwas weniger. Auf einem Fahrweg dazu die Rillen
    /// (nasse, dunkle Erde), die auf etwa 8 m verblassen.
    /// </summary>
    private Image? TorErdeBild(Transform3D lage, Torweg weg)
    {
        if (TorErdeTextur() is not { } erde) return null;
        var laenge = TorSchwelle * 2 + TorErdeInnen + 0.2f;
        var (w, h) = ((int)(TorErdeBreite * TorErdeDichte), (int)(laenge * TorErdeDichte));
        var z0 = TorSchwelle + 0.2f - laenge; // lokal Z der ersten Zeile
        var daten = new byte[w * h * 4];
        var rb = Mathf.Lerp(0.08f, 0.17f, weg.Austritt); // halbe Breite einer Rille, wie im Boden-Shader
        var rillen = weg.Fahrweg * Mathf.Lerp(0.35f, 1f, weg.Austritt);
        Parallel.For(0, h, b =>
        {
            for (var a = 0; a < w; a++)
            {
                var (x, z) = ((a + 0.5f) / TorErdeDichte - TorErdeBreite / 2, z0 + (b + 0.5f) / TorErdeDichte);
                var welt = lage.Origin + lage.Basis.X * x + lage.Basis.Z * z;
                var p = new Vector2(welt.X, welt.Z);
                var t = TorSchwelle - z; // Meter hinter der äußeren Schwelle
                var n1 = Rausch(p.X * 0.8f, p.Y * 0.8f) - 0.5f;
                var n2 = Rausch(p.X * 3.3f, p.Y * 3.3f) - 0.5f;
                var n3 = Rausch(p.X * 11f, p.Y * 11f) - 0.5f;
                // entlang: im Torweg dicht (an der inneren Schwelle noch drei Viertel), dahinter auf 3–6 m (quer wechselnd) aus
                var torweg = TorSchwelle * 2;
                var innen = Mathf.Lerp(3f, 6f, Rausch(p.X * 0.45f + 3.7f, p.Y * 0.45f + 3.7f)) * Mathf.Lerp(0.75f, 1f, weg.Fahrweg);
                var grund = t < torweg ? 1 - 0.25f * t / torweg : 0.75f * (1 - (t - torweg) / innen);
                // quer: im Torweg zu den Wänden etwas weniger, dahinter nur so breit wie der Weg und vor dem Rand des Decals aus
                var quer = MathF.Abs(x - weg.Versatz);
                var seite = t < torweg
                    ? 0.3f * Mathf.SmoothStep(weg.Halb * 0.6f, TorwegHalb, MathF.Abs(x))
                    : Mathf.SmoothStep(weg.Halb * 0.6f, weg.Halb + 0.6f, quer + n1 * 0.6f) + Mathf.SmoothStep(TorErdeBreite / 2 - 0.5f, TorErdeBreite / 2 - 0.05f, MathF.Abs(x));
                var dicht = grund * 0.9f - seite + n1 * 0.35f + n2 * 0.5f + n3 * 0.3f - 0.15f;
                var deckung = Mathf.SmoothStep(0f, 0.25f, dicht);
                // die Rillen der Karren, nach innen verblassend
                var rille = 0f;
                if (rillen > 0)
                {
                    var dx = x - weg.Versatz + n2 * 0.06f;
                    var r = MathF.Min(MathF.Abs(dx - SpurWeite), MathF.Abs(dx + SpurWeite));
                    rille = (1 - Mathf.SmoothStep(rb * 0.4f, rb, r)) * rillen * (1 - Mathf.SmoothStep(1f, 8f + n1 * 2f, t));
                    deckung = MathF.Max(deckung, rille * 0.95f);
                }
                // außen hinter der Schwelle liegt schon der Weg: dort weich aus
                deckung *= 1 - Mathf.SmoothStep(-0.05f, 0.15f, -t);
                var (u, v) = (PosMod(p.X / TorErdeMassstab, erde.Seite), PosMod(p.Y / TorErdeMassstab, erde.Seite));
                var i = (v * erde.Seite + u) * 4;
                // festgetreten: etwas grauer und dunkler als die Textur, in den Rillen nass
                var ton = Mathf.Lerp(0.8f, 0.95f, n3 + 0.5f) * (1 - 0.32f * rille);
                var (r0, g0, b0) = (erde.Daten[i], erde.Daten[i + 1], erde.Daten[i + 2]);
                var grau = (r0 * 0.3f + g0 * 0.5f + b0 * 0.2f) * TorErdeGrau;
                var o = (b * w + a) * 4;
                daten[o] = Byte((r0 * (1 - TorErdeGrau) + grau) * ton);
                daten[o + 1] = Byte((g0 * (1 - TorErdeGrau) + grau) * ton);
                daten[o + 2] = Byte((b0 * (1 - TorErdeGrau) + grau) * ton);
                daten[o + 3] = Byte(deckung * 255f);
            }
        });
        var bild = Image.CreateFromData(w, h, false, Image.Format.Rgba8, daten);
        bild.GenerateMipmaps();
        return bild;
    }

    /// <summary>Bildpunkt einer Textur mit <paramref name="seite"/> Punkten zur Koordinate <paramref name="uv"/> (wiederholt).</summary>
    private static int PosMod(float uv, int seite) => (int)((uv - MathF.Floor(uv)) * seite) % seite;

    private (byte[] Daten, int Seite)? TorErdeTextur()
    {
        if (_torErdeTextur is { } fertig) return fertig;
        if (_bodenTexturen?.GetLayerData((int)Bodentextur.Erde) is not { } bild) return null;
        bild = (Image)bild.Duplicate();
        if (bild.IsCompressed()) bild.Decompress();
        bild.ClearMipmaps();
        bild.Convert(Image.Format.Rgba8);
        var seite = (int)(TorErdeMassstab * TorErdeDichte);
        bild.Resize(seite, seite, Image.Interpolation.Cubic);
        return _torErdeTextur = (bild.GetData(), seite);
    }
}
