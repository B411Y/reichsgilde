namespace Reichsgilde;

// ═════════════════════════════════════════════════════════════════════════════
//  Detailstufen der Passanten-Figuren mit Fingerknochen (reine Logik, ohne Godot – FigurenLodTests übersetzt die Datei mit)
//
//  Nutzerentscheidung 14.11.2026 („Finger C mit 3 Stufen“):
//    Nah     volles Skelett, die Finger greifen (Innenräume; in der Stadt bei der nächsten Zoomstufe und in der Ego-Ansicht nahe Figuren)
//    Mittel  Skelett ohne Finger: die Fingerplätze der Haut zeigen auf den Handknochen, die Hand bleibt die halb geschlossene Ruhehand
//    Fern    VAT wie bisher (die Grenze bestimmt die vorhandene Passanten-Logik; hier nur übernommen)
//  Gegen Flattern an der Grenze Nah/Mittel eine Hysterese; der Wechsel wird über Ueberblendung Sekunden weich gemacht (Fingergewicht),
//  damit kein Griff springt. Vorbild: Unreal (Skeletal-Mesh-LOD mit Knochenreduktion, Update Rate Optimizations), Simplygon
//  (Bone Reduction: Gewichte entfernter Knochen auf den Elternknochen), O3DE (Skelett-LOD je Actor-LOD).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Detailstufe einer Figur.</summary>
public enum FigurStufe { Nah, Mittel, Fern }

/// <summary>Wahl der Detailstufe nach Zoom, Innenraum und Abstand (Ego-Ansicht), mit Hysterese; Fingergewicht für den weichen Wechsel.</summary>
public static class FigurenLod
{
    /// <summary>Bis zu dieser Zoomstufe (Meter Bildhöhe, nächste der drei Pixel-Zoomstufen 100/50/25) zeigt die Stadt die Finger.</summary>
    public const float NahBisZoom = 25f;

    /// <summary>In der Ego-Ansicht (Zoom ohne Bedeutung) greifen Figuren bis zu so vielen Metern Abstand mit den Fingern.</summary>
    public const float EgoNahAbstand = 15f;

    /// <summary>Hysterese: Nah wird erst unterhalb Grenze × (1 − Hysterese) erreicht und erst oberhalb Grenze × (1 + Hysterese) verlassen.</summary>
    public const float Hysterese = 0.1f;

    /// <summary>Dauer der Überblendung zwischen Griff- und Ruhehand in Sekunden.</summary>
    public const float Ueberblendung = 0.3f;

    /// <summary>
    /// Neue Stufe aus der bisherigen. <paramref name="fernAbZoom"/>: Grenze der vorhandenen VAT-Logik (ab dort Fern, ohne Hysterese wie bisher).
    /// <paramref name="egoAbstand"/>: Abstand zur Kamera in der Ego-Ansicht, sonst null.
    /// </summary>
    public static FigurStufe Waehlen(FigurStufe bisher, bool innen, float zoom, float fernAbZoom, float? egoAbstand = null)
    {
        if (innen) return FigurStufe.Nah;
        if (egoAbstand is { } a)
            return NahMitHysterese(bisher, a, EgoNahAbstand) ? FigurStufe.Nah : FigurStufe.Mittel;
        if (zoom >= fernAbZoom) return FigurStufe.Fern;
        return NahMitHysterese(bisher, zoom, NahBisZoom) ? FigurStufe.Nah : FigurStufe.Mittel;
    }

    private static bool NahMitHysterese(FigurStufe bisher, float wert, float grenze) =>
        bisher == FigurStufe.Nah ? wert <= grenze * (1f + Hysterese) : wert <= grenze * (1f - Hysterese);

    /// <summary>Ob die Fingerknochen gerechnet werden (nur Nah oder solange noch überblendet wird).</summary>
    public static bool FingerRechnen(FigurStufe stufe, float fingerGewicht) => stufe == FigurStufe.Nah || fingerGewicht > 0f;

    /// <summary>Fingergewicht (0 = Ruhehand, 1 = Griff des Clips) nach <paramref name="dt"/> Sekunden: läuft linear zum Ziel der Stufe.</summary>
    public static float FingerGewicht(float bisher, FigurStufe stufe, float dt)
    {
        var ziel = stufe == FigurStufe.Nah ? 1f : 0f;
        var schritt = Ueberblendung <= 0f ? 1f : dt / Ueberblendung;
        return bisher < ziel ? System.Math.Min(ziel, bisher + schritt) : System.Math.Max(ziel, bisher - schritt);
    }
}
