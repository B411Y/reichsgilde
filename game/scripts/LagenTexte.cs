using System.Collections.Generic;
using System.Linq;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Texte zu den Lagen aller Rohstoffquellen (Kern: <see cref="Lagenwesen"/>, Datenversion 92) für Markt, Tooltip und Jahresbericht – ohne Godot, damit der
/// Test sie mitübersetzen kann. Die Ernte hat ihre eigenen Zeilen (<c>MarktAnzeige</c>) und kommt hier nicht vor; gewöhnliche Lagen auch nicht (nur gute,
/// schlechte und schwere haben ein Ereignis). Gemeldet wird im Kern (<c>Lagenwesen.Minute</c>) über den vorhandenen Meldungsweg.
/// </summary>
public static class LagenTexte
{
    private static bool Eigen(Quellenlage l) => l.Quelle.Kennung != "Ernte" && l.Ereignis is not null;

    /// <summary>Die Lagen mit Ereignis, die jetzt gelten (ohne Ernte).</summary>
    public static IEnumerable<Quellenlage> Aktuelle(Stadtwirtschaft w) => w.Lagen.Jetzt.Where(Eigen);

    /// <summary>Die Lagen mit Ereignis, die jetzt angesagt sind (ohne Ernte).</summary>
    public static IEnumerable<Quellenlage> Angesagte(Stadtwirtschaft w) => w.Lagen.Angesagt.Where(Eigen);

    private static string Jahreszeitwort(Jahreszeit j) => j switch
    {
        Jahreszeit.Fruehling => "Frühling",
        Jahreszeit.Sommer => "Sommer",
        Jahreszeit.Herbst => "Herbst",
        _ => "Winter",
    };

    /// <summary>Kurz für Markt und Jahresbericht: „Grube 1404: Wassereinbruch – Eisenerz und Silber, bis 1407“.</summary>
    public static string Aktuell(Quellenlage l) =>
        $"{l.Quelle.Name} {l.Stichjahr}: {l.Ereignis!.Titel} – {Lagenwesen.WarenText(l)}, bis {l.LetztesJahr}";

    /// <summary>Kurz: „angesagt für Frühling 1408: Wassereinbruch in der Grube – Eisenerz und Silber“.</summary>
    public static string Angesagt(Quellenlage l) =>
        $"angesagt für {Jahreszeitwort(l.Quelle.Stichjahreszeit)} {l.Stichjahr}: {l.Ereignis!.Titel} ({l.Quelle.Name}) – {Lagenwesen.WarenText(l)}";

    /// <summary>Alle Zeilen für Markt und Jahresbericht (aktuelle, dann angesagte); leer, wenn keine Lage mit Ereignis besteht.</summary>
    public static List<string> Uebersicht(Stadtwirtschaft w)
    {
        var zeilen = Aktuelle(w).Select(Aktuell).ToList();
        zeilen.AddRange(Angesagte(w).Select(l => Angesagt(l)));
        return zeilen;
    }

    /// <summary>
    /// Tooltip-Zeilen einer Ware: je Lage, die sie jetzt verschiebt, „Grube 1404: Wassereinbruch – Eisenerz ×0,7“, und je angesagter Lage, die sie träfe,
    /// „angesagt für Frühling 1408: Wassereinbruch (Grube)“. Leer, wenn keine Lage die Ware betrifft.
    /// </summary>
    public static List<string> Tipp(Stadtwirtschaft w, string ware)
    {
        var zeilen = new List<string>();
        foreach (var (lage, faktor) in w.Lagen.Gruende(ware))
            if (Eigen(lage))
                zeilen.Add($"{lage.Quelle.Name} {lage.Stichjahr}: {lage.Ereignis!.Titel} – {ware} ×{Zahlenformat.Kommazahl(faktor)}");
        foreach (var a in Angesagte(w))
            if (a.Quelle.Rohwaren(a.Guete).Contains(ware))
                zeilen.Add($"angesagt für {Jahreszeitwort(a.Quelle.Stichjahreszeit)} {a.Stichjahr}: {a.Ereignis!.Titel} ({a.Quelle.Name})");
        return zeilen;
    }
}
