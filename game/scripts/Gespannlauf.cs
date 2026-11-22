namespace Reichsgilde;

/// <summary>
/// Schleppkurve eines Gespanns (Darstellung, 22.11.2026), nur Rechnung ohne Godot (<c>GespannlaufTests</c>): Das Zugtier (bzw. der Zieher) läuft den Weg, der Wagen
/// hängt am Zugpunkt und folgt nach wie ein Anhänger. Ebene X/Z in Metern, Kurs als Einheitsvektor.
///
/// <b>Einachser</b> (<see cref="Radstand"/> = 0): Der Drehpunkt <see cref="VorneX"/>/<see cref="VorneZ"/> (Achsmitte) hängt im festen Abstand <see cref="Deichsel"/> am Zugpunkt;
/// jeder Schritt zieht ihn zum Zugpunkt hin (Traktrix): Q' = Z' − L · normiert(Z' − Q). Der Wagen schneidet die Kurve innen.
/// <b>Vierradwagen</b> (<see cref="Radstand"/> &gt; 0, Drehschemel): Die Vorderachse hängt an der Deichsel und dreht mit ihr, die Hinterachse folgt der Vorderachse im Radstand.
/// Gerechnet wird in Schritten von höchstens <see cref="Schritt"/> Metern <b>zurückgelegten Weges</b>, nie nach Bildzeit: gleiches Ergebnis bei 30 und 144 Bildern je Sekunde.
/// <b>Knickwinkel</b> (<see cref="KnickMax"/>) zwischen Blickrichtung des Tiers und Deichsel sind begrenzt (kein Durchdringen), das Tier hat einen kleinsten Wendebogen
/// (<see cref="KleinsterBogen"/>): Wendet das Gespann (Ziel springt hinter das Gespann), läuft das Tier einen Bogen und holt auf.
/// </summary>
public sealed class Gespannlauf
{
    /// <summary>Größter Weg (Meter) eines Rechenschritts.</summary>
    public const double Schritt = 0.1;
    /// <summary>Liegt das Ziel näher als so viele Meter am Zugpunkt, steht der Zugpunkt gleich dort (Folgen); darüber läuft das Tier hin (Aufholen, Wenden).</summary>
    public const double Schnapp = 0.3;

    /// <summary>Abstand Zugpunkt bis Drehpunkt (Einachser: Achsmitte; Vierradwagen: Vorderachse).</summary>
    public double Deichsel { get; }
    /// <summary>Abstand Vorderachse bis Hinterachse; 0 = Einachser bzw. starres Fahrzeug.</summary>
    public double Radstand { get; }
    /// <summary>Größter Winkel (Bogenmaß) zwischen Tierkurs und Deichsel.</summary>
    public double KnickMax { get; }
    /// <summary>Kleinster Bogenhalbmesser (Meter) des Zugtiers.</summary>
    public double KleinsterBogen { get; }

    public double ZugX { get; private set; }
    public double ZugZ { get; private set; }
    /// <summary>Blickrichtung des Zugtiers (Einheitsvektor).</summary>
    public double KursX { get; private set; }
    public double KursZ { get; private set; } = 1;
    /// <summary>Drehpunkt: Achsmitte des Einachsers bzw. Mitte der Vorderachse.</summary>
    public double VorneX { get; private set; }
    public double VorneZ { get; private set; }
    /// <summary>Mitte der Hinterachse (beim Einachser gleich <see cref="VorneX"/>).</summary>
    public double HintenX { get; private set; }
    public double HintenZ { get; private set; }
    /// <summary>Richtung der Deichsel vom Drehpunkt zum Zugpunkt.</summary>
    public double DeichselX { get; private set; }
    public double DeichselZ { get; private set; } = 1;
    /// <summary>Richtung des Wagenkastens (Hinterachse zur Vorderachse; beim Einachser die Deichselrichtung).</summary>
    public double WagenX { get; private set; }
    public double WagenZ { get; private set; } = 1;
    public bool Bereit { get; private set; }

    public Gespannlauf(double deichsel, double radstand, double knickMaxGrad, double kleinsterBogen)
    {
        Deichsel = Math.Max(deichsel, 0.01);
        Radstand = Math.Max(radstand, 0);
        KnickMax = knickMaxGrad * Math.PI / 180.0;
        KleinsterBogen = Math.Max(kleinsterBogen, 0.1);
    }

    /// <summary>Knickwinkel (Bogenmaß, mit Vorzeichen) zwischen Tierkurs und Deichsel.</summary>
    public double Knick => Math.Atan2(KursX * DeichselZ - KursZ * DeichselX, KursX * DeichselX + KursZ * DeichselZ);
    /// <summary>Abstand Zugpunkt zum Drehpunkt (soll immer <see cref="Deichsel"/> sein).</summary>
    public double DeichselIst => Math.Sqrt((ZugX - VorneX) * (ZugX - VorneX) + (ZugZ - VorneZ) * (ZugZ - VorneZ));

    /// <summary>Hart setzen: das Gespann steht gestreckt hinter dem Zugpunkt in Richtung des Kurses.</summary>
    public void Setzen(double zugX, double zugZ, double kursX, double kursZ)
    {
        Norm(ref kursX, ref kursZ, 0, 1);
        (ZugX, ZugZ, KursX, KursZ) = (zugX, zugZ, kursX, kursZ);
        (DeichselX, DeichselZ) = (kursX, kursZ);
        (VorneX, VorneZ) = (zugX - kursX * Deichsel, zugZ - kursZ * Deichsel);
        (WagenX, WagenZ) = (kursX, kursZ);
        (HintenX, HintenZ) = (VorneX - kursX * Radstand, VorneZ - kursZ * Radstand);
        Bereit = true;
    }

    /// <summary>
    /// Ein Bild: Das Zugtier soll zum Ziel (Ort und Blickrichtung des Weges). <paramref name="aufholMeter"/>: so weit darf das Tier außerhalb des Folgens laufen (Tempo mal Bildzeit mal Eile).
    /// Nahe am Ziel (<see cref="Schnapp"/>) folgt der Zugpunkt dem Weg Schritt für Schritt (höchstens <see cref="Schritt"/> je Rechenschritt); sonst läuft das Tier im Bogen hin.
    /// </summary>
    public void Weiter(double zielX, double zielZ, double zielKursX, double zielKursZ, double aufholMeter)
    {
        Norm(ref zielKursX, ref zielKursZ, KursX, KursZ);
        if (!Bereit) { Setzen(zielX, zielZ, zielKursX, zielKursZ); return; }
        var dx = zielX - ZugX; var dz = zielZ - ZugZ;
        var abstand = Math.Sqrt(dx * dx + dz * dz);
        if (abstand <= Schnapp)
        {
            var n = Math.Max(1, (int)Math.Ceiling(abstand / Schritt));
            var (x0, z0) = (ZugX, ZugZ);
            for (var i = 1; i <= n; i++)
            {
                var t = (double)i / n;
                var ds = abstand / n;
                ZugX = x0 + dx * t; ZugZ = z0 + dz * t;
                Drehen(zielKursX, zielKursZ, ds / (0.5 * KleinsterBogen));
                Nachziehen();
            }
            return;
        }
        var weg = Math.Max(aufholMeter, 0);
        var m = Math.Max(1, (int)Math.Ceiling(weg / Schritt));
        var s = weg / m;
        for (var i = 0; i < m; i++)
        {
            dx = zielX - ZugX; dz = zielZ - ZugZ;
            abstand = Math.Sqrt(dx * dx + dz * dz);
            if (abstand <= Schnapp) { ZugX = zielX; ZugZ = zielZ; Drehen(zielKursX, zielKursZ, 1e9); Nachziehen(); break; }
            Drehen(dx / abstand, dz / abstand, s / KleinsterBogen);
            ZugX += KursX * s; ZugZ += KursZ * s;
            Nachziehen();
        }
    }

    /// <summary>Dreht den Tierkurs um höchstens <paramref name="maxWinkel"/> auf die Richtung zu.</summary>
    private void Drehen(double zx, double zz, double maxWinkel)
    {
        var w = Math.Atan2(KursX * zz - KursZ * zx, KursX * zx + KursZ * zz);
        w = Math.Clamp(w, -maxWinkel, maxWinkel);
        var (c, s) = (Math.Cos(w), Math.Sin(w));
        (KursX, KursZ) = (KursX * c - KursZ * s, KursX * s + KursZ * c);
    }

    /// <summary>Traktrix-Schritt: Drehpunkt zum Zugpunkt hin, Knick begrenzt; beim Vierradwagen folgt die Hinterachse der Vorderachse.</summary>
    private void Nachziehen()
    {
        var (dx, dz) = (ZugX - VorneX, ZugZ - VorneZ);
        Norm(ref dx, ref dz, DeichselX, DeichselZ);
        var knick = Math.Atan2(KursX * dz - KursZ * dx, KursX * dx + KursZ * dz);
        if (Math.Abs(knick) > KnickMax)
        {
            var w = Math.Sign(knick) * KnickMax - knick;
            var (c, s) = (Math.Cos(w), Math.Sin(w));
            (dx, dz) = (dx * c - dz * s, dx * s + dz * c);
        }
        (DeichselX, DeichselZ) = (dx, dz);
        (VorneX, VorneZ) = (ZugX - dx * Deichsel, ZugZ - dz * Deichsel);
        if (Radstand <= 0) { (HintenX, HintenZ) = (VorneX, VorneZ); (WagenX, WagenZ) = (dx, dz); return; }
        var (wx, wz) = (VorneX - HintenX, VorneZ - HintenZ);
        Norm(ref wx, ref wz, WagenX, WagenZ);
        (WagenX, WagenZ) = (wx, wz);
        (HintenX, HintenZ) = (VorneX - wx * Radstand, VorneZ - wz * Radstand);
    }

    private static void Norm(ref double x, ref double z, double ersatzX, double ersatzZ)
    {
        var l = Math.Sqrt(x * x + z * z);
        if (l < 1e-9) { x = ersatzX; z = ersatzZ; return; }
        x /= l; z /= l;
    }
}
