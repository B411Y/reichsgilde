using System.Collections.Generic;
using System.Text.Json;
using Reichsgilde.Core;

namespace Reichsgilde;

// ═════════════════════════════════════════════════════════════════════════════
//  Tritt und Standort der kleinen Völker am Arbeitsplatz (reine Logik, ohne Godot – ArbeitsTrittTests übersetzt die Datei mit)
//
//  Nutzer 14.11.2026: Zwerge und Halblinge stehen bei der Arbeit am Möbel auf einem Hocker und so nah am Möbel, wie nötig; die Faust
//  trifft dann die alte Handmitte wie bei den Menschen. Der Hocker ist ein fester Gegenstand: je Figur (Volk und Geschlecht) und Möbel
//  EINE Höhe (Werkbank, Destille, Steinblock, Blasebalg), gleich bei allen Clips an diesem Möbel und während der ganzen Bewegung; die
//  Unterschiede zwischen den Clips gleicht der Standort aus (vor, seitlich je Clip). Die Werte rechnet tools/modelle/passanten_arbeit.py
//  je Figur (Volk und Geschlecht) und Arbeitsclip und schreibt sie nach game/daten/arbeitstritt.json (Meter im Spiel). Die Godot-Seite
//  rückt die Figur um „Vor“ zum Möbel und um „Seitlich“ zur Seite, hebt sie um „Hoch“ und zeigt das Netz „Tritt_<Möbel>“ (in dieser
//  Höhe gebaut), wenn Hoch > 0.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Versatz der Figur am Arbeitsplatz: <see cref="Vor"/> Meter näher zum Möbel, <see cref="Hoch"/> Meter höher (auf dem Tritt),
/// <see cref="Seitlich"/> Meter zur Seite (+ links der Figur); <see cref="Moebel"/>: an welchem Möbel (Name des Hockers).</summary>
public readonly record struct TrittLage(float Vor, float Hoch, float Seitlich = 0f, string? Moebel = null)
{
    /// <summary>Ob die Figur auf einem Tritt steht.</summary>
    public bool MitTritt => Hoch > 0f;

    /// <summary>Name des Hocker-Netzes in der Figurdatei („Tritt_Werkbank“ …), null ohne Hocker.</summary>
    public string? Netz => MitTritt ? $"Tritt_{Moebel}" : null;
}

/// <summary>Tritt und Standort je Figur und Arbeitsclip aus <c>game/daten/arbeitstritt.json</c>.</summary>
public sealed class ArbeitsTritt
{
    private readonly Dictionary<string, Dictionary<string, TrittLage>> _figuren = new();

    /// <summary>Höhe der Hocker je Figur und Möbel (Meter; fest, in dieser Höhe ist das Netz „Tritt_&lt;Möbel&gt;“ gebaut).</summary>
    public IReadOnlyDictionary<(string Figur, string Moebel), float> Hocker => _hocker;
    private readonly Dictionary<(string, string), float> _hocker = new();

    /// <summary>Liest die Tabelle (JSON: { "zwerg": { "hocker_m": { "Werkbank": … }, "clips": { "Haemmern": { "moebel": "Werkbank", "vor_m": …, "seitlich_m": … } } } }).</summary>
    public static ArbeitsTritt Laden(string json)
    {
        var t = new ArbeitsTritt();
        using var dok = JsonDocument.Parse(json);
        foreach (var figur in dok.RootElement.EnumerateObject())
        {
            if (figur.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var h in figur.Value.GetProperty("hocker_m").EnumerateObject())
                t._hocker[(figur.Name, h.Name)] = h.Value.GetSingle();
            var clips = new Dictionary<string, TrittLage>();
            foreach (var c in figur.Value.GetProperty("clips").EnumerateObject())
            {
                var moebel = c.Value.GetProperty("moebel").GetString() ?? "Werkbank";
                clips[c.Name] = new TrittLage(c.Value.GetProperty("vor_m").GetSingle(), t._hocker.GetValueOrDefault((figur.Name, moebel)),
                    c.Value.TryGetProperty("seitlich_m", out var seit) ? seit.GetSingle() : 0f, moebel);
            }
            t._figuren[figur.Name] = clips;
        }
        return t;
    }

    /// <summary>Schlüssel der Figur wie in den Passanten-Dateien (zwerg, zwergin, halbling, halblingin); andere Völker: null.</summary>
    public static string? Figur(Volk volk, bool frau) => volk switch
    {
        Volk.Zwerg => frau ? "zwergin" : "zwerg",
        Volk.Halbling => frau ? "halblingin" : "halbling",
        _ => null,
    };

    /// <summary>Versatz für eine Figur bei einem Clip: kleine Völker bei Arbeit am Möbel nach der Tabelle, sonst kein Versatz.</summary>
    public TrittLage Lage(Volk volk, bool frau, string clip) =>
        Figur(volk, frau) is { } f && _figuren.TryGetValue(f, out var clips) && clips.TryGetValue(clip, out var lage) ? lage : default;
}
