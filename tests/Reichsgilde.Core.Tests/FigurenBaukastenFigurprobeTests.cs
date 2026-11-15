using Reichsgilde;
using Reichsgilde.Core;

/// <summary>Prüfhilfe --figurprobe=&lt;volk&gt;:&lt;m|w&gt;:&lt;clip&gt; (FigurenBaukasten.FigurProbeLesen): was gilt, was nicht.</summary>
public static class FigurenBaukastenFigurprobeTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Figuren-Baukasten: Prüfhilfe --figurprobe liest Volk, Geschlecht und Clip (Groß- und Kleinschreibung egal)", () =>
        {
            Pruefe.Gleich(new FigurenBaukasten.FigurProbe(Volk.Zwerg, false, "Hobeln"), FigurenBaukasten.FigurProbeLesen("Zwerg:m:Hobeln"));
            Pruefe.Gleich(new FigurenBaukasten.FigurProbe(Volk.Mensch, true, "Schreiben"), FigurenBaukasten.FigurProbeLesen("mensch:W:schreiben"));
            Pruefe.Gleich(new FigurenBaukasten.FigurProbe(Volk.Halbling, true, "Rechnen"), FigurenBaukasten.FigurProbeLesen("Halbling:w:Rechnen"));
            Pruefe.Gleich(new FigurenBaukasten.FigurProbe(Volk.Oger, false, "Hueten"), FigurenBaukasten.FigurProbeLesen("Oger:m:Hueten"));
            Pruefe.Gleich(47, FigurenBaukasten.FigurProbeClips.Count);
        }),
        ("Figuren-Baukasten: Prüfhilfe --figurprobe – Gegenprobe: falsches Volk, Geschlecht, Clip, Ogerin, Zahl und Form ergeben nichts", () =>
        {
            foreach (var text in new[] { null, "", "Zwerg:m", "Zwerg:m:Hobeln:x", "Riese:m:Hobeln", "1:m:Hobeln", "Zwerg:x:Hobeln", "Zwerg:m:Hammern",
                                         "Oger:w:Hueten", "Zwerg::Hobeln" })
                Pruefe.Wahr(FigurenBaukasten.FigurProbeLesen(text) is null, text ?? "null");
        }),
    };
}
