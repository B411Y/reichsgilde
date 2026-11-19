namespace Reichsgilde;

/// <summary>Die 3D-Gegenstände der Innenansichten mit eigener Schau: Kartentisch und Geldtruhe.</summary>
public enum GegenstandArt { Kartentisch, Geldtruhe }

/// <summary>Wo die Schau eines Gegenstands gerade steht: Ruhe → Hin (Schwenk, der Gegenstand öffnet sich) → Offen → Zurueck (schließen, Schwenk zurück) → Ruhe.</summary>
public enum GegenstandZustand { Ruhe, Hin, Offen, Zurueck }

/// <summary>
/// Die Maße eines Gegenstands aus dem Modell (<c>docs/bereiche/darstellung.md</c>, „3D-Gegenstände“): Namen der vier Clips, Längen der beiden
/// bewegten Clips und die Stelle, ab der der Gegenstand ruhig offen ist (Sekunden); dazu, was die Kamera rahmt: Mitte (x, y, z im Gegenstand),
/// Breite und Tiefe der gerahmten Fläche in Metern und die Neigung des Blicks gegen die Senkrechte in Grad (0 = von oben).
/// </summary>
public readonly record struct GegenstandMasse(
    string ClipZu, string ClipOffen, string ClipOeffnen, string ClipSchliessen, double Oeffnen, double Schliessen, double OffenAb,
    double MitteX, double MitteY, double MitteZ, double Breite, double Tiefe, double Neigung)
{
    /// <summary>Die Werte der Modelle <c>kartentisch.glb</c> (Karte 0,70 × 0,50 m auf der Platte in 0,78 m Höhe) und <c>geldtruhe.glb</c> (Kasten 0,60 × 0,38 m, Deckel offen bis 0,68 m; der Blick zielt 0,30 m neben die Truhe, damit sie links im Bild steht und der Bericht rechts daneben Platz hat).</summary>
    public static GegenstandMasse Von(GegenstandArt art) => art switch
    {
        GegenstandArt.Kartentisch => new GegenstandMasse("zu", "offen", "entrollen", "zusammenrollen", 1.233, 1.000, 1.000, -0.01, 0.78, 0, 0.84, 0.60, 12),
        _ => new GegenstandMasse("zu", "offen", "aufspringen", "zuklappen", 1.033, 0.700, 0.467, 0.30, 0.32, -0.08, 0.96, 0.96, 42),
    };
}

/// <summary>
/// Die reine Ablauflogik der Gegenstand-Schau ohne Godot, damit sie sich testen lässt (<c>GegenstandAblaufTests.cs</c>); dieselbe Regel wie bei
/// den 3D-Büchern (<see cref="BuchAblauf"/>): Die Kamera schwenkt in <see cref="BuchAblauf.SchwenkSekunden"/> abgebremst zum Gegenstand, er öffnet
/// sich dabei (Karte entrollt sich, Deckel springt auf); beim Schließen läuft der Clip zurück und die Kamera fährt zum alten Blick. Während des
/// Schwenks (<see cref="Gesperrt"/>) ist nichts anklickbar. Ohne Bewegung (Einstellung „Siegel und Briefe bewegen“ aus) steht er sofort offen
/// und ist sofort wieder zu.
/// </summary>
public sealed class GegenstandAblauf
{
    private readonly GegenstandMasse _m;
    private double _zeit;

    public GegenstandAblauf(GegenstandMasse masse) => _m = masse;

    public GegenstandZustand Zustand { get; private set; } = GegenstandZustand.Ruhe;

    /// <summary>Läuft die Schau (alles außer Ruhe)?</summary>
    public bool Aktiv => Zustand != GegenstandZustand.Ruhe;

    /// <summary>Während des Schwenks hin und zurück ist nichts anklickbar.</summary>
    public bool Gesperrt => Zustand is GegenstandZustand.Hin or GegenstandZustand.Zurueck;

    /// <summary>Der Clip des Modells, der gerade gilt.</summary>
    public string Clip => Zustand switch
    {
        GegenstandZustand.Hin => _m.ClipOeffnen,
        GegenstandZustand.Offen => _m.ClipOffen,
        GegenstandZustand.Zurueck => _m.ClipSchliessen,
        _ => _m.ClipZu,
    };

    /// <summary>Die Stelle im Clip in Sekunden.</summary>
    public double Clipzeit => Zustand switch
    {
        GegenstandZustand.Hin => Math.Clamp(_zeit - BuchAblauf.AufklappenAb, 0, _m.Oeffnen),
        GegenstandZustand.Zurueck => Math.Min(_zeit, _m.Schliessen),
        _ => 0,
    };

    /// <summary>Wie weit die Kamera beim Gegenstand ist: 0 = alter Blick, 1 = davor. Hin abgebremst, zurück weich an- und abgefahren.</summary>
    public double Kamera
    {
        get
        {
            switch (Zustand)
            {
                case GegenstandZustand.Ruhe: return 0;
                case GegenstandZustand.Hin:
                {
                    var t = Math.Clamp(_zeit / BuchAblauf.SchwenkSekunden, 0, 1);
                    return 1 - (1 - t) * (1 - t) * (1 - t);
                }
                case GegenstandZustand.Zurueck:
                {
                    var t = Math.Clamp((_zeit - BuchAblauf.ZurueckAb) / BuchAblauf.SchwenkSekunden, 0, 1);
                    return 1 - t * t * (3 - 2 * t);
                }
                default: return 1;
            }
        }
    }

    /// <summary>Liegt der Gegenstand ruhig offen (Karte glatt, Deckel am ersten Anschlag)? Ab dann darf eine Tafel darüber liegen.</summary>
    public bool OffenSichtbar => Zustand switch
    {
        GegenstandZustand.Hin => _zeit - BuchAblauf.AufklappenAb >= _m.OffenAb,
        GegenstandZustand.Offen => true,
        _ => false,
    };

    private double HinDauer => Math.Max(BuchAblauf.SchwenkSekunden, BuchAblauf.AufklappenAb + _m.Oeffnen);
    private double ZurueckDauer => Math.Max(_m.Schliessen, BuchAblauf.ZurueckAb + BuchAblauf.SchwenkSekunden);

    /// <summary>Der Gegenstand wurde angeklickt. Gibt false zurück, wenn schon eine Schau läuft.</summary>
    public bool Oeffnen(bool bewegt)
    {
        if (Aktiv) return false;
        _zeit = 0;
        Zustand = bewegt ? GegenstandZustand.Hin : GegenstandZustand.Offen;
        return true;
    }

    /// <summary>Esc oder Klick daneben: schließen, Kamera zurück. Während des Schwenks geschieht nichts.</summary>
    public bool Schliessen(bool bewegt)
    {
        if (Zustand != GegenstandZustand.Offen) return false;
        _zeit = 0;
        Zustand = bewegt ? GegenstandZustand.Zurueck : GegenstandZustand.Ruhe;
        return true;
    }

    /// <summary>Sofort zurück in die Ruhe (Raumwechsel, Haus verlassen): kein Clip, kein Schwenk.</summary>
    public void Abbrechen()
    {
        _zeit = 0;
        Zustand = GegenstandZustand.Ruhe;
    }

    public void Weiter(double delta)
    {
        if (Zustand is GegenstandZustand.Ruhe or GegenstandZustand.Offen) return;
        _zeit += Math.Max(0, delta);
        if (Zustand == GegenstandZustand.Hin && _zeit >= HinDauer)
        {
            Zustand = GegenstandZustand.Offen;
            _zeit = 0;
        }
        else if (Zustand == GegenstandZustand.Zurueck && _zeit >= ZurueckDauer)
        {
            Zustand = GegenstandZustand.Ruhe;
            _zeit = 0;
        }
    }
}

/// <summary>
/// Füllstufe der Geldtruhe nach dem Bargeld des Hauses in Gulden: unter <see cref="AbViertel"/> leer (null), dann „viertel“, „halb“, „voll“ –
/// die Namen der Knoten <c>Fuellstufe_&lt;stufe&gt;</c> in <c>geldtruhe.glb</c>. Reine Anzeige, der Kern weiß davon nichts.
/// </summary>
public static class Truhenfuellung
{
    /// <summary>Schwellen in Gulden (Startvermögen 4.000: die Truhe beginnt zu einem Viertel gefüllt).</summary>
    public const double AbViertel = 500, AbHalb = 5000, AbVoll = 20000;

    /// <summary>Die Stufen mit Münzen, von wenig nach viel.</summary>
    public static readonly string[] Stufen = { "viertel", "halb", "voll" };

    public static string? Stufe(double gulden) => gulden >= AbVoll ? "voll" : gulden >= AbHalb ? "halb" : gulden >= AbViertel ? "viertel" : null;
}
