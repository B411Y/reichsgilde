using System.Collections.Generic;
using System.Linq;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Texte zu den Lagen aller Rohstoffquellen (Kern: <see cref="Lagenwesen"/>, Datenversion 92) für Markt, Tooltip und Jahresbericht – ohne Godot, damit der
/// Test sie mitübersetzen kann. Die Ernte hat ihre eigenen Zeilen (<c>MarktAnzeige</c>) und kommt hier nicht vor; gewöhnliche Lagen auch nicht (nur gute,
/// schlechte und schwere haben ein Ereignis). Gemeldet wird im Kern (<c>Lagenwesen.Minute</c>) erst zum Beginn, über den vorhandenen Meldungsweg.
/// </summary>
public static class LagenTexte
{
    private static bool Eigen(Quellenlage l) => l.Quelle.Kennung != "Ernte" && l.Ereignis is not null;

    /// <summary>Die Lagen mit Ereignis, die jetzt gelten (ohne Ernte).</summary>
    public static IEnumerable<Quellenlage> Aktuelle(Stadtwirtschaft w) => w.Lagen.Jetzt.Where(Eigen);

    /// <summary>Kurz für Markt und Jahresbericht: „Grube 1404: Wassereinbruch – Eisenerz und Silber, bis 1407“.</summary>
    public static string Aktuell(Quellenlage l) =>
        $"{l.Quelle.Name} {l.Stichjahr}: {l.Ereignis!.Titel} – {Lagenwesen.WarenText(l)}, bis {l.LetztesJahr}";

    /// <summary>Alle Zeilen für Markt und Jahresbericht: nur die Lagen, die jetzt gelten (nie ein Blick in die Zukunft, Nutzer 15.11.2026); leer, wenn keine Lage mit Ereignis gilt.</summary>
    public static List<string> Uebersicht(Stadtwirtschaft w) => Aktuelle(w).Select(Aktuell).ToList();

    /// <summary>
    /// Tooltip-Zeilen einer Ware: je Lage, die sie jetzt verschiebt, „Grube 1404: Wassereinbruch – Eisenerz ×0,7“. Leer, wenn keine Lage die Ware betrifft.
    /// </summary>
    public static List<string> Tipp(Stadtwirtschaft w, string ware)
    {
        var zeilen = new List<string>();
        foreach (var (lage, faktor) in w.Lagen.Gruende(ware))
            if (Eigen(lage))
                zeilen.Add($"{lage.Quelle.Name} {lage.Stichjahr}: {lage.Ereignis!.Titel} – {ware} ×{Zahlenformat.Kommazahl(faktor)}");
        return zeilen;
    }
}
