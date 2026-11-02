using Godot;
using Reichsgilde.Core.Gefahren;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Blitzschlag (Teil der <c>partial class Stadtansicht</c>; Wunsch des Nutzers vom 2.11.2026). Der Kern entscheidet, wann und wo der Blitz einschlägt
/// und wie lange es brennt (<see cref="Blitzwesen"/>); die Darstellung liest nur <see cref="Blitzwesen.Einschlaege"/>:
/// <list type="bullet">
/// <item><b>Einschlag</b>: ein sichtbarer, gezackter Strahl aus den Wolken auf das Dach, ein heller Lichtblitz am Ort und der direkte Einschlag
/// (<see cref="BlitzEinschlag"/>: hellster Blitz, die laute Einschlag-Aufnahme fast ohne Verzögerung). Nur, wenn es gerade geschieht – nach dem Laden
/// brennt ein laufendes Feuer ohne neuen Blitz weiter.</item>
/// <item><b>Feuer</b>: Werkstätten und Wohnhäuser zünden über ihren Weg in Stadtansicht.Feuer.cs (der Kern meldet Reparatur bzw. Instandsetzung);
/// Kirchen, Rathaus, Einwohner- und Kulissenhäuser hier: das Modell an der Stelle des Einschlags brennt so lange wie im Kern, danach schwelt es, und
/// das Dach bleibt bis zum Jahresende rußig (der Kern vergisst die Einschläge zum Jahreswechsel).</item>
/// <item><b>Feuerglocke</b>: Ein paar Sekunden nach dem Einschlag läutet die nächste Kirche Sturm (<see cref="KlangSturmlaeuten"/>).</item>
/// <item><b>Löschkette</b>: Passanten laufen herbei und reichen Eimer (Stadtansicht.Loeschkette.cs).</item>
/// </list>
/// Welches Modell brennt, sucht <see cref="ModellBei"/> an der Stelle des Kerns unter allen eingesetzten Modellen (<see cref="ModellMerken"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Alle eingesetzten Modelle der Stadt (auch die Blöcke der Bauten ohne Modell), um das Gebäude an einer Stelle zu finden.</summary>
    private readonly List<ModellInstanz> _alleModelle = new();

    private void ModellMerken(ModellInstanz modell)
    {
        if (_alleModelle.Count > 4000) _alleModelle.RemoveAll(m => !IsInstanceValid(m.Wurzel));
        _alleModelle.Add(modell);
    }

    /// <summary>Was die Darstellung zu einem Einschlag des Kerns weiß.</summary>
    private sealed class Blitzansicht
    {
        public required Blitzeinschlag Einschlag { get; init; }
        public ModellInstanz? Modell { get; set; }
        /// <summary>Brennt im Weg dieser Datei (nicht Werkstatt, nicht Wohnhaus eines Hauses)?</summary>
        public bool Hier { get; init; }
        public bool Russ { get; set; }
        public Loeschkette? Kette { get; set; }
    }

    private readonly Dictionary<Blitzeinschlag, Blitzansicht> _blitze = new(ReferenceEqualityComparer.Instance);

    /// <summary>Der Einschlag des Kerns, der genau zu dieser Spielminute ein Feuer entfachte – oder null.</summary>
    private Blitzeinschlag? BlitzUm(long start) =>
        _simulation?.Wirtschaft?.Blitz is { IstAktiv: true } b ? b.Einschlaege.LastOrDefault(e => e.Minute == start) : null;

    /// <summary>Der jüngste Einschlag dieses Jahres in diese Werkstatt – oder null.</summary>
    private Blitzeinschlag? BlitzBrandIn(Betrieb betrieb) =>
        _simulation?.Wirtschaft?.Blitz is { IstAktiv: true } b ? b.Einschlaege.LastOrDefault(e => e.Ziel.Betrieb == betrieb) : null;

    /// <summary>Jedes Bild (aus <see cref="Aktualisieren"/>, vor dem Feuer): neue Einschläge zeigen, Feuer, Ruß und Löschkette nachführen.</summary>
    private void BlitzbrandAktualisieren(Stadtwirtschaft w, long jetzt, double bruchteil, double delta)
    {
        StrahlNachfuehren(delta);
        if (!w.Blitz.IstAktiv) return;
        var einschlaege = w.Blitz.Einschlaege;
        foreach (var e in einschlaege)
        {
            if (!_blitze.TryGetValue(e, out var a))
            {
                var hier = e.Ziel.Betrieb is null && e.Ziel.Bewohner is null;
                _blitze[e] = a = new Blitzansicht { Einschlag = e, Hier = hier, Modell = ModellBei((float)e.Ziel.X, (float)e.Ziel.Z) };
                var frisch = jetzt - e.Minute <= 2;
                GD.Print($"[blitz] Einschlag {e.Ziel.Kennung} ({e.Ziel.Name}) um Spielminute {e.Minute}, brennt bis {e.BrandEnde}, Modell {(a.Modell is null ? "nicht gefunden" : a.Modell.Wurzel.Name)}{(frisch ? "" : " (nach dem Laden)")}");
                if (frisch) EinschlagZeigen(e, a.Modell);
                if (e.BrenntUm(jetzt)) a.Kette = LoeschketteBeginnen(e, a.Modell);
            }
            if (a.Hier && a.Modell is { } modell && IsInstanceValid(modell.Wurzel))
            {
                var kennung = "blitz:" + e.Ziel.Kennung;
                if (!_braende.ContainsKey(kennung) && BrandLaeuft(e.Minute, jetzt) && jetzt < e.BrandEnde)
                    BrandAnbringen(kennung, modell, e.Minute);
                if (!a.Russ && jetzt >= e.Minute)
                {
                    a.Russ = true;
                    DachRussSetzen(modell, true);
                }
            }
            if (a.Kette is { } kette) LoeschketteAktualisieren(kette, jetzt + bruchteil, delta);
        }
        // Zum Jahreswechsel vergisst der Kern die alten Einschläge: Ruß ab, übrige Helfer weg
        if (_blitze.Count == einschlaege.Count) return;
        foreach (var (e, a) in _blitze.Where(x => !einschlaege.Contains(x.Key)).ToList())
        {
            if (a.Russ && a.Modell is { } m && IsInstanceValid(m.Wurzel)) DachRussSetzen(m, false);
            if (a.Kette is not null) LoeschketteEntfernen(a.Kette);
            _blitze.Remove(e);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Das Gebäude an einer Stelle
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Modell, das an der Stelle (x, z) der Karte steht: unter den Modellen, deren Grundfläche den Punkt enthält und mindestens 20 m² groß ist (kein
    /// Baum, keine Kiste), das kleinste – sonst das nächste in 12 m. Null, wenn dort nichts steht.
    /// </summary>
    private ModellInstanz? ModellBei(float x, float z)
    {
        ModellInstanz? bestes = null;
        var besteFlaeche = float.MaxValue;
        ModellInstanz? naechstes = null;
        var naechsterAbstand = 12f * 12f;
        foreach (var m in _alleModelle)
        {
            if (!IsInstanceValid(m.Wurzel) || !m.Wurzel.IsInsideTree() || !m.Wurzel.IsVisibleInTree()) continue;
            var lage = m.Wurzel.GlobalPosition;
            if ((lage.X - x) * (lage.X - x) + (lage.Z - z) * (lage.Z - z) > 60f * 60f) continue;
            var (min, max) = WeltUmriss(m.Wurzel);
            if (min.X > max.X) continue;
            var flaeche = (max.X - min.X) * (max.Z - min.Z);
            if (flaeche < 20f) continue;
            if (x >= min.X && x <= max.X && z >= min.Z && z <= max.Z)
            {
                if (flaeche < besteFlaeche) (bestes, besteFlaeche) = (m, flaeche);
                continue;
            }
            var (cx, cz) = ((min.X + max.X) / 2, (min.Z + max.Z) / 2);
            var d = (cx - x) * (cx - x) + (cz - z) * (cz - z);
            if (d < naechsterAbstand) (naechstes, naechsterAbstand) = (m, d);
        }
        return bestes ?? naechstes;
    }

    /// <summary>Umriss eines Modells in Koordinaten der Stadt (achsparallel, aus den acht Ecken des lokalen Umrisses).</summary>
    private (Vector3 Min, Vector3 Max) WeltUmriss(Node3D wurzel, float? abHoehe = null)
    {
        var (min, max) = ModellUmriss(wurzel, abHoehe);
        if (min.X > max.X) return (min, max);
        var lage = GlobalTransform.AffineInverse() * wurzel.GlobalTransform;
        var wmin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var wmax = -wmin;
        for (var i = 0; i < 8; i++)
        {
            var p = lage * new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            wmin = wmin.Min(p);
            wmax = wmax.Max(p);
        }
        return (wmin, wmax);
    }

    /// <summary>Die höchste Stelle des Dachs (Koordinaten der Stadt): Mitte des obersten Zehntels, ganz oben. Ohne Modell der Boden plus Höhe aus dem Kern.</summary>
    private Vector3 Dachspitze(Blitzeinschlag e, ModellInstanz? modell)
    {
        var (x, z) = ((float)e.Ziel.X, (float)e.Ziel.Z);
        if (modell is null || !IsInstanceValid(modell.Wurzel)) return new Vector3(x, BodenHoehe(x, z) + (float)e.Ziel.Hoehe, z);
        var (min, max) = WeltUmriss(modell.Wurzel);
        var (lokalMin, lokalMax) = ModellUmriss(modell.Wurzel);
        var (oMin, oMax) = WeltUmriss(modell.Wurzel, lokalMin.Y + (lokalMax.Y - lokalMin.Y) * 0.9f);
        if (oMin.X > oMax.X) (oMin, oMax) = (min, max);
        return new Vector3((oMin.X + oMax.X) / 2, max.Y, (oMin.Z + oMax.Z) / 2);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einschlag: Strahl, Lichtblitz, Donner, Glocke
    // ═════════════════════════════════════════════════════════════════════════

    private MeshInstance3D? _strahl;
    private OmniLight3D? _strahlLicht;
    private float _strahlZeit = -1;

    /// <summary>So lange (echte Sekunden) ist der Strahl zu sehen: drei Pulse, dazwischen dunkel, dann ein Nachglühen.</summary>
    private const float StrahlDauer = 0.55f;

    private void EinschlagZeigen(Blitzeinschlag e, ModellInstanz? modell)
    {
        var spitze = Dachspitze(e, modell);
        StrahlBauen(spitze, (uint)(e.Minute * 2654435761L ^ e.Ziel.Kennung.Length * 40503));
        BlitzEinschlag(spitze); // hellster Blitz, direkter Donner (Stadtansicht.Entwickler.cs, KlangUmgebung)
        // Feuerglocke: die nächste Kirche, ein paar Sekunden nach dem Einschlag (5–9 s)
        var kirche = NaechsteKirche(spitze);
        var warten = 5f + (e.Minute % 5);
        GetTree().CreateTimer(warten).Timeout += () => KlangSturmlaeuten.Laeuten(kirche, e.Ziel.Name);
        GD.Print($"[blitz] Strahl auf {spitze}, Feuerglocke in {warten:0} s von {kirche}");
    }

    /// <summary>Die Mitte der nächsten Kirche oder des Doms (sonst des Marktplatzes), in Höhe des Turms.</summary>
    private Vector3 NaechsteKirche(Vector3 ort)
    {
        var bestes = _markt;
        var abstand = float.MaxValue;
        foreach (var g in _karte?.Gebaeude ?? new List<Reichsgilde.Core.Bauen.Kartengebaeude>())
        {
            if (g.Katalog is not ("kirche" or "dom" or "koelner_dom")) continue;
            var p = new Vector3((float)g.Punkt[0], 0, (float)g.Punkt[1]);
            var d = (p - new Vector3(ort.X, 0, ort.Z)).LengthSquared();
            if (d < abstand) (bestes, abstand) = (p, d);
        }
        return new Vector3(bestes.X, BodenHoehe(bestes.X, bestes.Z) + 25f, bestes.Z);
    }

    /// <summary>
    /// Ein gezackter Strahl aus den Wolken (etwa 160 m über dem Dach, seitlich versetzt) bis auf die Spitze, mit einem oder zwei Ästen: je Stück zwei
    /// gekreuzte, senkrechte Bänder (von schräg oben gut zu sehen), weiß-bläulich, unbeleuchtet und additiv; dazu ein Licht am Dach.
    /// </summary>
    private void StrahlBauen(Vector3 spitze, uint saat)
    {
        _strahl?.QueueFree();
        _strahlLicht?.QueueFree();
        var zufall = new RandomNumberGenerator { Seed = saat };
        var oben = spitze + new Vector3(zufall.RandfRange(-40, 40), 160f, zufall.RandfRange(-40, 40));
        var netz = new ImmediateMesh();
        netz.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        void Band(Vector3 a, Vector3 b, float breite)
        {
            var richtung = (b - a).Normalized();
            foreach (var quer in new[] { Vector3.Right, Vector3.Back })
            {
                var seite = richtung.Cross(quer).Normalized() * breite * 0.5f;
                if (seite.LengthSquared() < 1e-6f) seite = quer * breite * 0.5f;
                netz.SurfaceAddVertex(a - seite); netz.SurfaceAddVertex(a + seite); netz.SurfaceAddVertex(b + seite);
                netz.SurfaceAddVertex(a - seite); netz.SurfaceAddVertex(b + seite); netz.SurfaceAddVertex(b - seite);
            }
        }
        List<Vector3> Zacken(Vector3 von, Vector3 bis, int stuecke, float ausschlag)
        {
            var punkte = new List<Vector3> { von };
            for (var i = 1; i < stuecke; i++)
            {
                var t = (float)i / stuecke;
                punkte.Add(von.Lerp(bis, t) + new Vector3(zufall.RandfRange(-1, 1), zufall.RandfRange(-0.3f, 0.3f), zufall.RandfRange(-1, 1)) * ausschlag * (1 - t * 0.7f));
            }
            punkte.Add(bis);
            return punkte;
        }
        var haupt = Zacken(oben, spitze, 18, 9f);
        for (var i = 0; i + 1 < haupt.Count; i++) Band(haupt[i], haupt[i + 1], 1.1f);
        for (var ast = 0; ast < 2; ast++)
        {
            var ab = haupt[3 + zufall.RandiRange(0, 8)];
            var ende = ab + new Vector3(zufall.RandfRange(-30, 30), -zufall.RandfRange(25, 50), zufall.RandfRange(-30, 30));
            var zweig = Zacken(ab, ende, 7, 5f);
            for (var i = 0; i + 1 < zweig.Count; i++) Band(zweig[i], zweig[i + 1], 0.5f);
        }
        netz.SurfaceEnd();
        _strahl = new MeshInstance3D
        {
            Name = "Blitzstrahl", Mesh = netz, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = new Color(0.85f, 0.9f, 1f, 1f), DisableFog = true,
            },
        };
        AddChild(_strahl);
        _strahlLicht = new OmniLight3D
        {
            Name = "Blitzlicht", Position = spitze + new Vector3(0, 6, 0), OmniRange = 90f, LightEnergy = 0f,
            LightColor = new Color(0.82f, 0.88f, 1f), ShadowEnabled = false,
        };
        AddChild(_strahlLicht);
        _strahlZeit = 0;
    }

    /// <summary>Jedes Bild: der Strahl flackert in drei Pulsen und erlischt, das Licht am Dach mit ihm.</summary>
    private void StrahlNachfuehren(double delta)
    {
        if (_strahlZeit < 0 || _strahl is null) return;
        _strahlZeit += (float)delta;
        var t = _strahlZeit;
        var hell = t < 0.07f ? 1f : t < 0.12f ? 0.1f : t < 0.2f ? 0.95f : t < 0.26f ? 0.15f : t < 0.33f ? 0.8f : Mathf.Max(0, 0.5f * (1 - (t - 0.33f) / (StrahlDauer - 0.33f)));
        if (_strahl.MaterialOverride is StandardMaterial3D stoff) stoff.AlbedoColor = new Color(0.85f, 0.9f, 1f, hell);
        _strahl.Visible = hell > 0.01f;
        if (_strahlLicht is not null) _strahlLicht.LightEnergy = 14f * hell;
        if (t < StrahlDauer) return;
        _strahl.QueueFree();
        _strahlLicht?.QueueFree();
        (_strahl, _strahlLicht, _strahlZeit) = (null, null, -1);
    }
}
