using Godot;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Zierschiff (Teil der <c>partial class Stadtansicht</c>): Ruht der Schiffshandel (<see cref="Schiffahrt.Ruht"/>), legt am Hafen ab und zu ein Schiff an,
/// liegt eine Weile und fährt wieder fort – nur zur Zierde, ohne Wirkung im Kern und ohne Eintrag im Spielstand.
///
/// Der Fahrplan ist eine reine Funktion der Spielminute (<see cref="ZierFahrplan"/>): Jedes Spieljahr hat zwei Besuche (je ein Zeitfenster von 570 Minuten),
/// Ankunft, Liegezeit (60–150 Minuten, also 1–2,5 Spielstunden) und die Seite, von der es kommt und zu der es fährt, ergeben sich aus einer Streuzahl
/// von Jahr und Fenster. Laden und Speichern zeigen darum dasselbe Bild. Beide Besuche enden spätestens gegen 19:30 Uhr, das Schiff liegt also nie über Nacht
/// und fährt nie im Dunkeln. Die Wege sind <see cref="Schiffahrt.Route"/> des Hafens, das Schiff ist das Modell der Handelsschiffe (Stadtansicht.Schiffe.cs).
///
/// Nur bei <see cref="Schiffahrt.HafenOffen"/> (also nie im Winter): Wird der Hafen geschlossen, während das Schiff liegt, fährt es sofort fort; fährt es
/// gerade, verschwindet es am Kartenrand wie sonst. Ohne Hafen oder ohne Wasserweg (Route) gibt es kein Schiff.
/// </summary>
public partial class Stadtansicht
{
    private enum ZierPhase { Fern, Anfahrt, Liegen, Abfahrt }

    /// <summary>Ein Fenster des Fahrplans in Spielminuten: zwei je Spieljahr.</summary>
    private const int ZierFenster = Spielzeit.MinutenProTag / 2;

    /// <summary>Wie lange die Fahrt zwischen Kartenrand und Kai dauert (Minuten): Routenlänge / 10 m, nicht unter 20 und nicht über 70.</summary>
    private static int ZierFahrtMinuten(double routenLaenge) => (int)Math.Clamp(routenLaenge / 10, 20, 70);

    /// <summary>
    /// Der Fahrplan: Wo steht das Zierschiff zur Minute <paramref name="minute"/> (seit Spielbeginn, mit Bruchteil)? Rein aus den Argumenten berechnet.
    /// <c>Anteil</c> ist der Fortschritt 0…1 in der Phase; <c>VonFlussauf</c> sagt, auf welcher Route es kommt, <c>NachFlussauf</c>, auf welcher es geht.
    /// </summary>
    private static (ZierPhase Phase, double Anteil, bool VonFlussauf, bool NachFlussauf, double LiegeBeginn) ZierFahrplan(double minute, int fahrt)
    {
        var fenster = (long)Math.Floor(minute / ZierFenster);
        var im = minute - fenster * ZierFenster;
        // Streuzahl aus der Nummer des Fensters (Jahr * 2 + Hälfte): gleiche Minute, gleiche Zahlen
        var z = (uint)(fenster * 2654435761L + 40503) ^ 0x9E3779B9u;
        z ^= z >> 15; z *= 0x2C1B3C6Du; z ^= z >> 12; z *= 0x297A2D39u; z ^= z >> 15;
        var ankunft = 70 + (int)(z % 61);            // Minute im Fenster, zu der es anlegt (70–130); die Anfahrt beginnt vor 20 min nach dem Fensteranfang
        var liegen = 60 + (int)((z >> 8) % 91);       // 60–150 min
        var vonAuf = (z & 0x10000) != 0;
        var nachAuf = (z & 0x20000) != 0;
        var anfang = ankunft - fahrt;
        var ablegen = ankunft + liegen;
        if (im < anfang || im >= ablegen + fahrt) return (ZierPhase.Fern, 0, vonAuf, nachAuf, 0);
        if (im < ankunft) return (ZierPhase.Anfahrt, (im - anfang) / fahrt, vonAuf, nachAuf, 0);
        if (im < ablegen) return (ZierPhase.Liegen, (im - ankunft) / liegen, vonAuf, nachAuf, fenster * ZierFenster + ankunft);
        return (ZierPhase.Abfahrt, (im - ablegen) / fahrt, vonAuf, nachAuf, 0);
    }

    private SchiffAnsicht? _zierschiff;
    private double? _zierAbbruch;

    /// <summary>Jedes Bild (aus <see cref="SchiffeAktualisieren"/>, nur wenn der Schiffshandel ruht): das Zierschiff an seinen Ort setzen.</summary>
    private void ZierschiffAktualisieren(Stadtwirtschaft w, double minute, double delta)
    {
        var sf = w.Schiffahrt;
        var hafen = sf.Haefen.Count > 0 ? sf.Haefen[0] : null;
        var offen = hafen is not null && sf.HafenOffen;
        var fern = true;
        if (hafen is not null)
        {
            var laengeAuf = sf.RoutenLaenge(hafen.Kennung, true);
            var laengeAb = sf.RoutenLaenge(hafen.Kennung, false);
            if (laengeAuf > 0 || laengeAb > 0)
            {
                var fahrt = ZierFahrtMinuten(Math.Max(laengeAuf, laengeAb));
                var plan = ZierFahrplan(minute, fahrt);
                var phase = plan.Phase;
                // Wird der Hafen zu, während das Schiff schon liegt oder kommt: es legt sofort ab und fährt seine Route zu Ende. War es vorher nicht
                // zu sehen (Partie oder Spielstand beginnt im Winter, Hafen zu), erscheint es gar nicht erst.
                if (!offen && phase is ZierPhase.Anfahrt or ZierPhase.Liegen)
                {
                    if (_zierAbbruch is null && _zierschiff is { Wurzel.Visible: true }) _zierAbbruch = minute;
                    if (_zierAbbruch is { } beginn)
                    {
                        phase = ZierPhase.Abfahrt;
                        plan = (phase, Math.Clamp((minute - beginn) / fahrt, 0, 1), plan.VonFlussauf, plan.NachFlussauf, 0);
                    }
                    else phase = ZierPhase.Fern;
                }
                else if (offen || phase == ZierPhase.Fern) _zierAbbruch = null;
                else if (_zierAbbruch is null) phase = ZierPhase.Fern; // geschlossen, es fuhr schon: nicht neu erscheinen
                if (phase != ZierPhase.Fern) fern = !ZierschiffSetzen(w, hafen, phase, plan.Anteil, plan.VonFlussauf, plan.NachFlussauf, delta);
            }
        }
        if (fern && _zierschiff is { } s && s.Wurzel.Visible)
        {
            s.Wurzel.Visible = false;
            s.Sichtbar = false;
        }
        // Die Kähne des Hafenmodells weichen, solange das Zierschiff am Kai liegt (oder fährt)
        if (hafen is not null)
        {
            var frei = fern;
            if (_kaehneFrei.GetValueOrDefault(hafen.Kennung, true) != frei)
            {
                _kaehneFrei[hafen.Kennung] = frei;
                HafenKaehneZeigen(hafen.Kennung, frei);
            }
        }
    }

    /// <summary>Setzt das Zierschiff für die Phase; gibt zurück, ob es sichtbar ist (am Ende der Route hinter dem Kartenrand nicht).</summary>
    private bool ZierschiffSetzen(Stadtwirtschaft w, Hafenplatz hafen, ZierPhase phase, double anteil, bool vonAuf, bool nachAuf, double delta)
    {
        _zierschiff ??= SchiffErzeugen("Zierschiff", new Color(0.62f, 0.2f, 0.16f), 7, false);
        var ansicht = _zierschiff;
        Vector3 ziel;
        float drehung;
        if (phase == ZierPhase.Liegen)
        {
            // Längsseits am Steg, wie die Schiffe des Handels (SchiffBewegen)
            (ziel, drehung) = StegLiegeplatz(hafen, ansicht, StegAbstand);
        }
        else
        {
            var anfahrt = phase == ZierPhase.Anfahrt;
            var route = w.Schiffahrt.Route(hafen.Kennung, anfahrt ? vonAuf : nachAuf);
            var laenge = w.Schiffahrt.RoutenLaenge(hafen.Kennung, anfahrt ? vonAuf : nachAuf);
            if (route.Count < 2 || laenge <= 0) { route = w.Schiffahrt.Route(hafen.Kennung, !vonAuf); laenge = w.Schiffahrt.RoutenLaenge(hafen.Kennung, !vonAuf); }
            // Abstand vom Liegeplatz: bei der Anfahrt schrumpft er von der Routenlänge auf 0, bei der Abfahrt wächst er; am Ende der Route liegt der Kartenrand
            var stelle = (anfahrt ? 1 - anteil : anteil) * laenge;
            if (route.Count < 2 || stelle >= laenge - 0.5)
            {
                ansicht.Wurzel.Visible = false;
                ansicht.Sichtbar = false;
                return false;
            }
            var (ort, rx, rz) = ZierAufRoute(route, stelle);
            ziel = new Vector3((float)ort.X, 0, (float)ort.Z);
            if (anfahrt) (rx, rz) = (-rx, -rz); // zum Kai hin gegen die Richtung der Route
            drehung = Mathf.Atan2(-(float)rz, (float)rx);
            // Die letzten Meter vor dem Liegeplatz biegt die Fahrt zum Steg hin ab: Das Schiff dreht längsseits und hält vor dem Kopf des Stegs, nicht durch ihn
            if (stelle < StegAnlauf)
            {
                var gleich = Mathf.SmoothStep(1f, 0f, (float)stelle / StegAnlauf);
                var (liege, liegeDrehung) = StegLiegeplatz(hafen, ansicht, StegAbstand);
                ziel = ziel.Lerp(liege, gleich);
                drehung = Mathf.LerpAngle(drehung, liegeDrehung, gleich);
            }
        }
        ansicht.Wurzel.Visible = true;
        SchiffPoseSetzen(ansicht, ziel, drehung, delta);
        SchiffSegelUndPlanke(ansicht, phase != ZierPhase.Liegen, phase == ZierPhase.Liegen);
        return true;
    }

    /// <summary>Der Punkt in diesem Abstand vom Anfang der Route und die Richtung dort (vom Anfang weg); wie im Kern, der es nicht öffentlich macht.</summary>
    private static (Reichsgilde.Core.Kartenwerkstatt.Kartenort Ort, double X, double Z) ZierAufRoute(IReadOnlyList<Reichsgilde.Core.Kartenwerkstatt.Kartenort> route, double stelle)
    {
        for (var i = 1; i < route.Count; i++)
        {
            var (a, b) = (route[i - 1], route[i]);
            var l = a.Abstand(b);
            if (l <= 1e-9) continue;
            if (stelle <= l || i == route.Count - 1)
            {
                var t = Math.Clamp(stelle / l, 0, 1);
                return (new Reichsgilde.Core.Kartenwerkstatt.Kartenort(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t), (b.X - a.X) / l, (b.Z - a.Z) / l);
            }
            stelle -= l;
        }
        return (route[^1], 1, 0);
    }
}
