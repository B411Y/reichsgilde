using Godot;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Wasserprobe der Kartenwerkstatt (Startoption <c>--wasserprobe=&lt;fall&gt;</c>, mit <c>--kartenwerkstatt=neu</c>): eine Messkarte mit den
/// Übergängen, die im Bild schwierig sind (Nutzer 09.11.2026), auf flachem Gelände, damit nur das Wasser zählt. Gezeichnet wird mit demselben Code wie
/// jede Karte (Stadtansicht.Wasser.cs), anders als in der Testszene <c>--wassertest</c>. Die Probe füllt nur die Startoptionen
/// <c>--werkstatt-groesse</c>, <c>--werkstatt-fluss</c>, <c>--werkstatt-see</c> und <c>--blick</c> vor (was schon angegeben ist, bleibt) und setzt danach
/// die Breiten einzelner Stützpunkte (<see cref="WasserprobeNacharbeit"/>). Der Fall wählt nur den Blick:
/// <list type="bullet">
/// <item><c>see</c>: ein Fluss (16 m) mit See A bis an sein nördliches Ufer und See B, der ein Stück in den Fluss hineinreicht.</item>
/// <item><c>muendung</c> (auch <c>alle</c>): ein Bach (3 m) mündet von Norden in diesen Fluss.</item>
/// <item><c>muendung_schmal</c>: ein zweiter Fluss (12 m, ab x = 150 nur 6 m) mit Bächen von 1,5 m (bei x −400), 6 m (x −200) und 3 m (x 80, kurz vor der
/// Verengung) von Süden.</item>
/// <item><c>kurve</c>: ein Bach mit 150 m geradem Stück und engen Kurven (Halbmesser um 15–25 m).</item>
/// <item><c>kurven</c>: ein S-förmiger Bach (3 m) mit Halbkreisen von 12, 6, 3 und 1,5 m Halbmesser (bei x 200–235, z 100–145), ein Bach mit einem bewussten
/// Knick von 90° (bei 400, 190) und ein Fluss (12 m) mit einem Halbkreis von 20 m (bei 500, 60).</item>
/// <item><c>verbund</c>: ein Fluss (10 m), der in einem See endet (bei −390, −330), ein Fluss (12 m) mit einem See 8 m daneben – bleibt getrennt (bei −150,
/// −340) –, ein Bach (3 m), der in einen Teich mündet (bei 470, −330), und ein Teich 2 m neben dem zweiten Fluss (bei 400, 300).</item>
/// <item><c>verbund_hang</c>: ein Fluss (12 m) an einem Hang mit 1 % Gefälle, daneben ein See mit festem Wasserstand (Spiegel des Flusses bei x 200):
/// Er bildet nur auf einem Stück mit dem Fluss eine Fläche (bei 200, −340).</item>
/// <item><c>verbund_ecke</c>, <c>verbund_streifen</c>, <c>verbund_teich</c> (Nachbesserung nach dem NAS-Klicktest 09.11.2026): näher an die Ecke, wo der Fluss in den
/// See tritt (früher eine Landinsel), an das alte Ufer des Flusses vor See A (früher ein dunkler Streifen) und an den Teich am zweiten Fluss (im Winter
/// mit <c>--jahreszeit=winter --winter=0.6</c>: ein Teich friert ganz zu, ein See nur am Rand; das Band vereist am alten Ufer nicht mehr).</item>
/// </list>
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Wo die Kamera je Fall hinschaut (x, z, Zoom wie <c>--blick</c>).</summary>
    private static readonly Dictionary<string, string> WasserprobeBlick = new()
    {
        ["see"] = "-240,-150,40",
        ["muendung"] = "0,-150,40",
        ["muendung_schmal"] = "80,330,40",
        ["kurve"] = "-170,160,40",
        ["kurven"] = "215,125,30",
        ["verbund"] = "-390,-330,40",
        ["verbund_hang"] = "200,-345,40",
        ["verbund_ecke"] = "-440,-330,25",
        ["verbund_streifen"] = "-280,-160,25",
        ["verbund_teich"] = "400,310,25",
        ["alle"] = "0,-150,40",
    };

    /// <summary>Der verengte Fluss: Stützpunkte ab diesem bekommen 6 m statt 12 m.</summary>
    private const int WasserprobeEngAb = 4;

    private bool _wasserprobe;

    /// <summary>Setzt die Startoptionen der Wasserprobe ein, wenn <c>--wasserprobe</c> angegeben ist (siehe Kopf der Datei).</summary>
    private void WasserprobeEinsetzen(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("wasserprobe", out var fall)) return;
        if (!WasserprobeBlick.ContainsKey(fall))
        {
            GD.PushError($"Kartenwerkstatt: --wasserprobe={fall} gibt es nicht ({string.Join(", ", WasserprobeBlick.Keys)}).");
            return;
        }
        _wasserprobe = true;
        o.TryAdd("werkstatt-groesse", "1200,900");
        var radien = Pfad(new Vector2(200, 100), new Vector2(1, 0), 20, (12, 1), 10, (6, -1), 10, (3, 1), 10, (1.5f, -1), 20);
        var r20 = Pfad(new Vector2(430, 40), new Vector2(1, 0), 70, (20, 1), 70);
        // Der Fluss (16 m) von West nach Ost; der Mündungsbach von Norden (3 m) mit einer sanften Biegung bis in den Fluss hinein; der Kurvenbach
        // erst 150 m gerade, dann enge Kurven; der zweite Fluss (12 m, die Verengung setzt WasserprobeNacharbeit) mit drei Bächen von Süden
        o.TryAdd("werkstatt-fluss",
            "fluss,16,-390,-150;-300,-148;-200,-150;-100,-158;0,-165;100,-160;200,-140;300,-130;390,-135" +
            "|bach,3,15,120;5,60;-8,0;-10,-80;-2,-140;0,-166" +
            "|bach,3,-370,230;-300,230;-220,230;-190,215;-175,190;-185,165;-205,145;-200,120;-175,105;-145,110;-125,90;-120,60" +
            "|fluss,12,-550,330;-300,330;-50,335;110,332;150,330;300,325;550,330" +
            "|bach,1.5,-400,445;-405,400;-400,331" +
            "|bach,6,-200,445;-195,390;-200,336" +
            "|bach,3,80,445;85,390;80,333" +
            "|bach,3," + radien +
            "|bach,3,300,190;400,190;400,250" +
            "|fluss,12," + r20 +
            "|fluss,10,-550,-330;-470,-330;-400,-330" +
            "|fluss,12,-250,-300;-150,-300;-50,-300" +
            "|fluss,12,300,-330;200,-330;50,-330" +
            "|bach,3,470,-430;468,-380;470,-315");
        // See A bis an das nördliche Ufer des Flusses (das Ufer liegt bei z ≈ −156), See B südlich ein Stück in den Fluss hinein
        o.TryAdd("werkstatt-see", "-330,-230;-230,-235;-215,-190;-235,-158;-330,-156" + "|200,-136;300,-126;315,-80;280,-40;210,-50;190,-95" +
            "|-440,-380;-340,-380;-340,-280;-440,-280" +                // der Fluss endet darin
            "|-200,-400;-100,-400;-100,-314;-200,-314" +                // 8 m neben dem Fluss: getrennt
            "|130,-400;270,-400;270,-338;130,-338,0.1" +                // fester Wasserstand am Hang
            "|teich:450,-320;490,-320;490,-282;450,-282" +              // der Bach mündet darin
            "|teich:380,284;420,284;420,322;380,322");                  // 2 m neben dem zweiten Fluss (Ufer bei z 324)
        // der Hang für verbund_hang: von x 100 (0 m) bis 300 (2 m) steigend, 1 %
        o.TryAdd("werkstatt-gelaende", "heben:0,1,2,300,-330");
        o.TryAdd("werkstatt-rampe", "100,-330;300,-330,200");
        o.TryAdd("blick", WasserprobeBlick[fall]);
        GD.Print($"[wasserprobe] Fall {fall}: Fluss mit See A (bis ans Ufer, bei -280,-190) und See B (in den Fluss hinein, bei 250,-90), " +
                 "Mündung des Baches bei 0,-160, Kurvenbach gerade bis -220,230, Kurven bei -190,160; zweiter Fluss bei z 330 (ab x 150 nur 6 m) " +
                 "mit Bächen 1,5 m bei -400, 6 m bei -200, 3 m bei 80");
    }

    /// <summary>
    /// Ein Pfad als Punktliste für <c>--werkstatt-fluss</c>: abwechselnd gerade Stücke (Meter) und Halbkreise (Halbmesser, Seite: +1 links, −1 rechts der
    /// Richtung), Punkte etwa alle 1 m, auf einem Halbkreis mindestens acht.
    /// </summary>
    private static string Pfad(Vector2 start, Vector2 richtung, params object[] stuecke)
    {
        var punkte = new List<Vector2> { start };
        var (ort, dir) = (start, richtung.Normalized());
        foreach (var stueck in stuecke)
            if (stueck is int gerade)
            {
                ort += dir * gerade;
                punkte.Add(ort);
            }
            else if (stueck is ValueTuple<int, int> or ValueTuple<float, int>)
            {
                var (halbmesser, seite) = stueck is ValueTuple<int, int> g ? (g.Item1, g.Item2) : ((float, int))stueck;
                var links = new Vector2(dir.Y, -dir.X) * -seite; // zur Seite des Mittelpunkts
                var mitte = ort + links * halbmesser;
                var teile = Math.Max(8, Mathf.CeilToInt(Mathf.Pi * halbmesser));
                for (var t = 1; t <= teile; t++)
                {
                    var w = Mathf.Pi * t / teile * seite;
                    punkte.Add(mitte + (ort - mitte).Rotated(w));
                }
                (ort, dir) = (punkte[^1], -dir);
            }
        return string.Join(";", punkte.Select(q => $"{q.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},{q.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
    }

    /// <summary>Nach den Startoptionen: der zweite Fluss wird stromab von x = 150 schmaler (6 m), ein Schritt der Geschichte je Punkt.</summary>
    private void WasserprobeNacharbeit()
    {
        if (!_wasserprobe) return;
        var eng = _entwurf.Fluesse.FirstOrDefault(f => f.Punkte.Count > WasserprobeEngAb && f.Punkte[0].X == -550 && f.Punkte[0].Z == 330);
        if (eng is null)
        {
            GD.PushWarning("[wasserprobe] Der zweite Fluss fehlt – keine Verengung.");
            return;
        }
        for (var i = WasserprobeEngAb; i < eng.Punkte.Count; i++) _entwurf.FlussBreiteSetzen(eng, i, 6);
        GD.Print($"[wasserprobe] {eng.Bezeichnung}: ab Stützpunkt {WasserprobeEngAb} 6 m breit (Breite dort {eng.BreiteAn(WasserprobeEngAb):0.0} m)");
        // Belege für den Grund im Verbund (NAS 09.11.2026, „Schleier“): Tiefe (Spiegel − Boden des Kerns) quer über das alte Ufer
        TiefeLoggen("verbund_streifen (x −280, quer über Fluss, altes Ufer und See A)", -280, Enumerable.Range(0, 15).Select(i => -136.0 - 3 * i));
        TiefeLoggen("verbund_ecke (z −320, entlang des Westufers des Sees, wo der Fluss eintritt)", null, Enumerable.Range(0, 13).Select(i => -452.0 + 2 * i), -320);
        TiefeLoggen("verbund_ecke (z −326, am Nordufer des Flusses in den See hinein)", null, Enumerable.Range(0, 13).Select(i => -452.0 + 2 * i), -326);
    }

    /// <summary>Schreibt je Ort die Tiefe (Spiegel − Boden), den gemeinsamen Abstand und den Abstand zum geschlossenen Seeufer ins Protokoll.</summary>
    private void TiefeLoggen(string titel, double? x, IEnumerable<double> werte, double z = 0)
    {
        var form = _entwurf.Gelaendeform;
        var zeilen = werte.Select(w =>
        {
            var (px, pz) = x is { } fest ? (fest, w) : (w, z);
            var spiegel = form.Wasser.Spiegel(px, pz);
            var abstand = form.Wasser.Verbundabstand(px, pz);
            var ufer = form.Wasser.Verbuende.Select(v => v.SeeuferAbstand(px, pz)).DefaultIfEmpty(double.MaxValue).Min();
            return $"{px:0},{pz:0}: " + (spiegel is { } sp ? $"Tiefe {sp - form.Hoehe(px, pz):0.00} m" : "Land") +
                   (abstand is { } s ? $", s {s:0.0}" : "") + (ufer < 1e6 ? $", Seeufer {ufer:0.0}" : "");
        });
        GD.Print($"[wasserprobe] Tiefe {titel}: {string.Join("; ", zeilen)}");
    }
}
