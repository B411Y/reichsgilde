using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Treibende Eisschollen auf den Flüssen der Karte – ein Teil der <c>partial class Stadtansicht</c> (realistisches Wasser, Phase 3c,
/// <c>docs/wasser/phase3.md</c>). Je Lauf einer Art mit Schollen (<see cref="Wasserart.Schollen"/>, der Fluss) ein <see cref="Schollenfeld"/>: Körper
/// mit Dicke statt eines Musters auf dem Wasser. Gebaut wird es ein Bild nach den Wasserflächen (dann stehen auch die Brücken, deren Pfeiler die
/// Schollen umfahren); jedes Bild treibt es mit der Strömung, folgt dem Eisfortschritt der Art und weicht dem Prahm der Fähren und den Kähnen aus.
/// Die Netze hängen unter den Wasserflächen: Sie verschwinden mit ihnen (Messung „ohne Wasser“) und entstehen neu, wenn die Grafikstufe wechselt.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Läufe, deren Schollen noch gebaut werden (aus <see cref="WasserflaechenBauen"/>).</summary>
    private readonly List<(Wasserlauf Lauf, Wasserart Art, ShaderMaterial Stoff)> _schollenWarten = new();

    private readonly List<(Schollenfeld Feld, Wasserart Art)> _schollenFelder = new();

    /// <summary>Brückenpfeiler und Joche in der Welt (Ort, halbe Ausdehnung quer zum Fluss und längs): Die Schollen weichen ihnen aus.</summary>
    private readonly List<(Vector2 Ort, float HalbQuer, float HalbLaengs)> _wasserPfeiler = new();

    private readonly List<Schollenfeld.Hindernis> _schollenBeweglich = new();

    /// <summary>Halbe Ausdehnung des Prahms der Fähre (quer zum Fluss, also entlang der Fährlinie, und längs) und eines Kahns am Kai.</summary>
    private const float PrahmQuer = 6.2f, PrahmLaengs = 2.6f, KahnQuer = 2.2f, KahnLaengs = 7f;

    private void SchollenVergessen()
    {
        _schollenWarten.Clear();
        _schollenFelder.Clear(); // die Knoten hängen unter den alten Wasserflächen und gehen mit ihnen
    }

    /// <summary>Jedes Bild (aus <see cref="WasserBild"/>): wartende Schollenfelder bauen, alle weitertreiben.</summary>
    private void SchollenBild(double delta)
    {
        if (_schollenWarten.Count > 0 && _wasserFlaechen is not null && IsInstanceValid(_wasserFlaechen))
        {
            foreach (var (lauf, art, _) in _schollenWarten) SchollenBauen(lauf, art);
            _schollenWarten.Clear();
        }
        if (_schollenFelder.Count == 0) return;
        var kamera = GetViewport()?.GetCamera3D();
        foreach (var (feld, art) in _schollenFelder)
        {
            _schollenBeweglich.Clear();
            var tipp = -1;
            foreach (var f in _faehren)
            {
                if (!IsInstanceValid(f.Wurzel)) continue;
                var p = f.Wurzel.GlobalPosition;
                var (quer, stelle, abstand) = feld.InLaufachsen(new Vector2(p.X, p.Z), ref tipp);
                if (abstand < 200f) _schollenBeweglich.Add(new(quer, stelle, PrahmQuer, PrahmLaengs));
            }
            foreach (var h in _haefen)
                foreach (var kahn in h.Kaehne)
                {
                    if (!IsInstanceValid(kahn) || !kahn.IsVisibleInTree()) continue;
                    var p = kahn.GlobalPosition;
                    tipp = -1;
                    var (quer, stelle, abstand) = feld.InLaufachsen(new Vector2(p.X, p.Z), ref tipp);
                    if (abstand < 200f) _schollenBeweglich.Add(new(quer, stelle, KahnQuer, KahnLaengs));
                }
            feld.Bild(delta, kamera, Wasserarten.EisFortschritt(art, _wasserWinter), art.Schollen, _schollenBeweglich);
        }
    }

    private void SchollenBauen(Wasserlauf lauf, Wasserart art)
    {
        var punkte = LaufJeMeter(lauf);
        if (punkte.Length < 30 || _wasserFlaechen is null) return;
        var saat = Wasserarten.Saat(lauf.Name);
        var hindernisse = new List<Schollenfeld.Hindernis>();
        // vorläufiges Feld nur für die Laufachsen der Pfeiler wäre doppelt: die Pfeiler direkt gegen die Mittellinie rechnen
        foreach (var (ort, halbQuer, halbLaengs) in _wasserPfeiler)
        {
            var (quer, stelle, abstand) = InLaufachsen(punkte, ort);
            if (abstand < punkte[(int)Mathf.Clamp(stelle, 0, punkte.Length - 1)].Halb + 4f)
                hindernisse.Add(new(quer, stelle, halbQuer, halbLaengs));
        }
        var feld = new Schollenfeld(punkte, (float)lauf.Stroemung, art.EisReichweite, (int)(saat * 100), _wasserStufe, hindernisse, lauf.Name);
        Wasserarten.WellenSetzen(feld.Stoff, art, Vector2.Up, lauf: true, fliesst: true, Wasserarten.NetzAbstand(_wasserStufe), saat);
        _wasserFlaechen.AddChild(feld.Knoten);
        _schollenFelder.Add((feld, art));
        GD.Print($"[wasser] Eisschollen {lauf.Name}: {feld.Anzahl} Schollen auf {punkte.Length} m, {hindernisse.Count} Pfeiler");
    }

    /// <summary>Die Mittellinie eines Laufs je Meter (der Kern gibt etwa einen Punkt je Meter): Ort, Spiegel, halbe Breite, Fließrichtung wie im Netz.</summary>
    private static Schollenfeld.Laufpunkt[] LaufJeMeter(Wasserlauf lauf)
    {
        var p = lauf.Punkte;
        var laenge = (int)Math.Floor(lauf.Laenge);
        var orte = new Schollenfeld.Laufpunkt[Math.Max(laenge + 1, 0)];
        var k = 0;
        for (var s = 0; s <= laenge; s++)
        {
            while (k + 2 < p.Count && p[k + 1].Stelle < s) k++;
            var (a, b) = (p[k], p[Math.Min(k + 1, p.Count - 1)]);
            var t = b.Stelle > a.Stelle ? Mathf.Clamp((float)((s - a.Stelle) / (b.Stelle - a.Stelle)), 0f, 1f) : 0f;
            var ort = new Vector2((float)(a.Ort.X + (b.Ort.X - a.Ort.X) * t), (float)(a.Ort.Z + (b.Ort.Z - a.Ort.Z) * t));
            orte[s] = new Schollenfeld.Laufpunkt(ort, (float)(a.Spiegel + (b.Spiegel - a.Spiegel) * t), (float)(a.Breite + (b.Breite - a.Breite) * t) / 2,
                Vector2.Zero);
        }
        for (var s = 0; s < orte.Length; s++)
        {
            var (vor, nach) = (orte[Math.Max(0, s - 1)].Ort, orte[Math.Min(orte.Length - 1, s + 1)].Ort);
            var r = (nach - vor).Normalized();
            orte[s] = orte[s] with { Richtung = r == Vector2.Zero ? Vector2.Up : r };
        }
        return orte;
    }

    private static (float Quer, float Stelle, float Abstand) InLaufachsen(Schollenfeld.Laufpunkt[] punkte, Vector2 ort)
    {
        var beste = 0;
        var besteD = float.MaxValue;
        for (var i = 0; i < punkte.Length; i++)
        {
            var d = punkte[i].Ort.DistanceSquaredTo(ort);
            if (d < besteD) (besteD, beste) = (d, i);
        }
        var p = punkte[beste];
        var rel = ort - p.Ort;
        return (rel.Dot(new Vector2(-p.Richtung.Y, p.Richtung.X)), beste + rel.Dot(p.Richtung), MathF.Sqrt(besteD));
    }
}
