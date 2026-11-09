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
        // Der Fluss (16 m) von West nach Ost; der Mündungsbach von Norden (3 m) mit einer sanften Biegung bis in den Fluss hinein; der Kurvenbach
        // erst 150 m gerade, dann enge Kurven; der zweite Fluss (12 m, die Verengung setzt WasserprobeNacharbeit) mit drei Bächen von Süden
        o.TryAdd("werkstatt-fluss",
            "fluss,16,-390,-150;-300,-148;-200,-150;-100,-158;0,-165;100,-160;200,-140;300,-130;390,-135" +
            "|bach,3,15,120;5,60;-8,0;-10,-80;-2,-140;0,-166" +
            "|bach,3,-370,230;-300,230;-220,230;-190,215;-175,190;-185,165;-205,145;-200,120;-175,105;-145,110;-125,90;-120,60" +
            "|fluss,12,-550,330;-300,330;-50,335;110,332;150,330;300,325;550,330" +
            "|bach,1.5,-400,445;-405,400;-400,331" +
            "|bach,6,-200,445;-195,390;-200,336" +
            "|bach,3,80,445;85,390;80,333");
        // See A bis an das nördliche Ufer des Flusses (das Ufer liegt bei z ≈ −156), See B südlich ein Stück in den Fluss hinein
        o.TryAdd("werkstatt-see", "-330,-230;-230,-235;-215,-190;-235,-158;-330,-156" + "|200,-136;300,-126;315,-80;280,-40;210,-50;190,-95");
        o.TryAdd("blick", WasserprobeBlick[fall]);
        GD.Print($"[wasserprobe] Fall {fall}: Fluss mit See A (bis ans Ufer, bei -280,-190) und See B (in den Fluss hinein, bei 250,-90), " +
                 "Mündung des Baches bei 0,-160, Kurvenbach gerade bis -220,230, Kurven bei -190,160; zweiter Fluss bei z 330 (ab x 150 nur 6 m) " +
                 "mit Bächen 1,5 m bei -400, 6 m bei -200, 3 m bei 80");
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
    }
}
