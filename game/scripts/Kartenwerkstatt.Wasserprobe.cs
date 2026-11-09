using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Wasserprobe der Kartenwerkstatt (Startoption <c>--wasserprobe=&lt;see|muendung|kurve|alle&gt;</c>, mit <c>--kartenwerkstatt=neu</c>): eine Messkarte
/// mit genau den drei Übergängen, die im Bild schwierig sind (Nutzer 09.11.2026) – ein Fluss mit zwei Seen daneben (der eine bis an sein Ufer, der
/// andere ein Stück hinein), ein Bach, der in den Fluss mündet, und ein Bach mit einem geraden Stück und engen Kurven. Auf flachem Gelände, damit
/// nur das Wasser zählt. Die Probe füllt nur die Startoptionen <c>--werkstatt-groesse</c>, <c>--werkstatt-fluss</c>, <c>--werkstatt-see</c> und
/// <c>--blick</c> vor (was schon angegeben ist, bleibt); der Blick zeigt den genannten Fall, bei <c>alle</c> die Mündung.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Wo die Kamera je Fall hinschaut (x, z, Zoom wie <c>--blick</c>).</summary>
    private static readonly Dictionary<string, string> WasserprobeBlick = new()
    {
        ["see"] = "-240,-150,40",
        ["muendung"] = "0,-150,40",
        ["kurve"] = "-170,160,40",
        ["alle"] = "0,-150,40",
    };

    /// <summary>Setzt die Startoptionen der Wasserprobe ein, wenn <c>--wasserprobe</c> angegeben ist (siehe Kopf der Datei).</summary>
    private static void WasserprobeEinsetzen(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("wasserprobe", out var fall)) return;
        if (!WasserprobeBlick.ContainsKey(fall))
        {
            GD.PushError($"Kartenwerkstatt: --wasserprobe={fall} gibt es nicht (see, muendung, kurve, alle).");
            return;
        }
        o.TryAdd("werkstatt-groesse", "800,600");
        // Der Fluss (16 m) von West nach Ost; der Mündungsbach von Norden (3 m) mit einer sanften Biegung bis in den Fluss hinein; der Kurvenbach
        // erst 150 m gerade, dann enge Kurven (Halbmesser um 15–25 m)
        o.TryAdd("werkstatt-fluss",
            "fluss,16,-390,-150;-300,-148;-200,-150;-100,-158;0,-165;100,-160;200,-140;300,-130;390,-135" +
            "|bach,3,15,120;5,60;-8,0;-10,-80;-2,-140;0,-166" +
            "|bach,3,-370,230;-300,230;-220,230;-190,215;-175,190;-185,165;-205,145;-200,120;-175,105;-145,110;-125,90;-120,60");
        // See A bis an das nördliche Ufer des Flusses (das Ufer liegt bei z ≈ −156), See B südlich ein Stück in den Fluss hinein
        o.TryAdd("werkstatt-see", "-330,-230;-230,-235;-215,-190;-235,-158;-330,-156" + "|200,-136;300,-126;315,-80;280,-40;210,-50;190,-95");
        o.TryAdd("blick", WasserprobeBlick[fall]);
        GD.Print($"[wasserprobe] Fall {fall}: Fluss mit See A (bis ans Ufer, bei -280,-190) und See B (in den Fluss hinein, bei 250,-90), " +
                 "Mündung des Baches bei 0,-160, Kurvenbach gerade bis -220,230, Kurven bei -190,160");
    }
}
