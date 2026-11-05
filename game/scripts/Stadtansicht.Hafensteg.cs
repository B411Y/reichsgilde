using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Hafensteg (Teil der <c>partial class Stadtansicht</c>): Jeder Hafen mit Schiffsanleger bekommt eine hölzerne Landungsbrücke (<c>wasser/wasser_hafensteg</c>,
/// T-Steg: 8 m Zugang vom Kai, dann ein 16 m langer Kopf quer dazu, insgesamt 10,4 m im Wasser). Die Schiffe (Handel und Zierschiff) liegen längsseits am
/// Kopf des Stegs, nicht an der Kaimauer: Ihre Kaiseite liegt <see cref="StegAbstand"/> vor der Uferkante, die Laufplanke reicht auf den Kopf. Der Steg steht auf
/// dem Wasser an der Uferkante des Hafens (<see cref="Hafenplatz.Liegeplatz"/> minus <see cref="Schiffahrt.LiegeplatzAbstand"/>), Wasser bei −Z wie beim Hafenmodell;
/// Schnee im Winter bekommt er wie jedes Modell. Die Kähne des Hafenmodells entfallen (der Steg nimmt ihren Platz), die Klickfläche des Hafens bleibt.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Abstand der Kaiseite der Schiffe von der Uferkante (m): Seekante des Stegkopfes (10,4 m) mit seinen Reibepfählen (bis 10,7 m) plus 0,3 m Luft.</summary>
    private const float StegAbstand = 11.0f;

    /// <summary>Auf so vielen Metern vor dem Liegeplatz biegt die Route der Anfahrt zum Steg hin ab (und dreht das Schiff längsseits).</summary>
    private const float StegAnlauf = 45f;

    private readonly Dictionary<string, Node3D> _stege = new();

    /// <summary>Jedes Bild (aus <see cref="SchiffeAktualisieren"/>): Fehlt einem Hafen der Steg, wird er gesetzt (einmal, die Ansicht bleibt bis zum Neuaufbau).</summary>
    private void HafenstegeSichern(Stadtwirtschaft w)
    {
        foreach (var hafen in w.Schiffahrt.Haefen)
        {
            if (_stege.TryGetValue(hafen.Kennung, out var da) && IsInstanceValid(da)) continue;
            var kante = HafenKante(hafen);
            var nach = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ);
            if (ModellEinsetzen("wasser/wasser_hafensteg", this, Vector3.Zero, new ModellZustand()) is not { } steg) continue;
            steg.Wurzel.Name = "Hafensteg_" + hafen.Kennung;
            // Wasser des Modells bei −Z: Drehung wie beim Schiff, Höhe = Boden an der Uferkante (der Kai des Hafenmodells)
            steg.Wurzel.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(-nach.X, -nach.Z)), new Vector3(kante.X, StegHoehe(hafen, kante), kante.Z));
            _stege[hafen.Kennung] = steg.Wurzel;
            if (hafen == w.Schiffahrt.Haefen[0]) KranHakenBauen(steg.Wurzel); // Seil und Haken des Krans (Stadtansicht.Kranladen.cs)
            HafenKaehneZeigen(hafen.Kennung, false); // die Kähne weichen dem Steg
        }
    }

    /// <summary>
    /// Höhe des Modellnullpunkts (Uferkante, Kai 0,25 m darüber, Wasser 1 m darunter): die des Hafenmodells, damit Kai und Steg zusammenpassen; fehlt es, der Wasserspiegel
    /// am Kopf plus 1 m, sonst der Boden an der Kante. (Der Boden an der Kante selbst fällt zum Fluss ab und läge im Wasser.)
    /// </summary>
    private float StegHoehe(Hafenplatz hafen, Vector3 kante)
    {
        foreach (var h in _haefen)
            if (IsInstanceValid(h.Wurzel) && h.Wurzel.Name == "Hafen_" + hafen.Kennung) return h.Wurzel.GlobalPosition.Y;
        var nach = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ);
        if (SpiegelMitBecken(kante.X + nach.X * 5f, kante.Z + nach.Z * 5f) is { } spiegel) return (float)spiegel + 1.0f;
        return BodenHoehe(kante.X, kante.Z);
    }

    /// <summary>Die Uferkante des Hafens (Kaikante), von der aus die Schiffe liegen.</summary>
    private static Vector3 HafenKante(Hafenplatz hafen) =>
        new Vector3((float)hafen.Liegeplatz.X, 0, (float)hafen.Liegeplatz.Z) - new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ) * (float)Schiffahrt.LiegeplatzAbstand;

    /// <summary>Wo und wie ein Schiff am Steg liegt: Mitte (Anker_Kai auf Kante plus <paramref name="aussen"/> m nach draußen) und Drehung um die Hochachse; <paramref name="laengs"/> versetzt das Schiff in Stegrichtung (in Stegkoordinaten +X, zum Kran hin).</summary>
    private static (Vector3 Ort, float Drehung) StegLiegeplatz(Hafenplatz hafen, SchiffAnsicht ansicht, float aussen, float laengs = 0f)
    {
        var nach = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ);
        var drehung = Mathf.Atan2(-nach.X, -nach.Z);
        var anker = new Basis(Vector3.Up, drehung) * new Vector3(ansicht.AnkerKai.X, 0, ansicht.AnkerKai.Z);
        return (HafenKante(hafen) - anker + nach * aussen + new Basis(Vector3.Up, drehung) * new Vector3(laengs, 0, 0), drehung);
    }
}
