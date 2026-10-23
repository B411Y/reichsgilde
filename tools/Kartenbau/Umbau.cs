using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau;

// ═════════════════════════════════════════════════════════════════════════════
//  Umbau: eine vorhandene Karte per Skript ändern (je Umbau eine Datei unter Umbauten/)
//
//  Ein Stadtskript legt eine Karte neu an; ein Umbau öffnet eine vorhandene (game/karten/<karte>.json), ändert sie mit denselben Befehlen
//  des Kartenentwurfs und schreibt sie nur ohne Fehler zurück. Gedacht für Änderungen, die mehrere Schritte zugleich brauchen, weil die Karte
//  dazwischen Fehler hätte (z. B. Köln beim Umstieg auf das Wasser von Schritt 5c). So bleibt nachlesbar, was mit einer Karte geschah.
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen <umbau>
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Umbau einer vorhandenen Karte. Unterklassen nennen Kennung, Karte und Beschreibung und ändern in <see cref="Anwenden"/>.</summary>
public abstract class Umbau
{
    /// <summary>Kennung des Umbaus (für den Aufruf), klein und ohne Umlaute.</summary>
    public abstract string Kennung { get; }

    /// <summary>Die Kennung der Karte, die er ändert (<c>game/karten/&lt;karte&gt;.json</c>).</summary>
    public abstract string Karte { get; }

    /// <summary>Eine Zeile für die Liste.</summary>
    public virtual string Beschreibung => "";

    /// <summary>Ändert den Entwurf (mit seinen Befehlen) und schreibt auf <paramref name="aus"/>, was geschah.</summary>
    public abstract void Anwenden(Kartenentwurf e, TextWriter aus);

    /// <summary>Alle Umbauten dieses Programms, nach Kennung.</summary>
    public static IReadOnlyList<Umbau> Alle() => typeof(Umbau).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(Umbau)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
        .Select(t => (Umbau)Activator.CreateInstance(t)!).OrderBy(u => u.Kennung, StringComparer.Ordinal).ToList();

    /// <summary>Der Umbau mit dieser Kennung (oder diesem Klassennamen) – oder null.</summary>
    public static Umbau? Finde(string name) =>
        Alle().FirstOrDefault(u => string.Equals(u.Kennung, name, StringComparison.OrdinalIgnoreCase) || string.Equals(u.GetType().Name, name, StringComparison.OrdinalIgnoreCase));
}
