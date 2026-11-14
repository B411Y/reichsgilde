namespace Reichsgilde;

/// <summary>Die Stufen der Klinken-Folge der Hand (Namen der Clips stehen in <see cref="KlinkeFolge"/>).</summary>
public enum KlinkePhase { Aus, Greifen, Druecken, Gehalten, Loslassen }

/// <summary>
/// Die reine Zustandslogik der Klinken-Folge des 3D-Handschuhs beim Betreten eines Gebäudes, ohne Godot (<c>KlinkeFolgeTests.cs</c>):
/// Hover über der Tür = Haltung Bereit (offene Hand, Schloss darunter), beim Betreten <c>Klinke_greifen</c>, dann <c>Klinke_druecken</c>
/// (Hand und Klinke gemeinsam, das Schloss spielt <c>Druecken</c>), dann bleibt die Hand am Ende gedrückt (<see cref="KlinkePhase.Gehalten"/>),
/// bis der Raum offen ist oder abgebrochen wird: <c>Klinke_loslassen</c> (endet in der Haltung Bereit, das Schloss spielt <c>Loslassen</c>).
/// Die Längen der Clips kommen aus der Datei (Spielcode), nicht von hier. Die Schwarzblende beginnt, wenn <see cref="VorlaufAnteil"/> des Drückens vorbei ist
/// (das Ende des Drückens ist ohnehin nur Halten).
/// </summary>
public sealed class KlinkeFolge
{
    public const string ClipGreifen = "Klinke_greifen", ClipDruecken = "Klinke_druecken", ClipLoslassen = "Klinke_loslassen", ClipBereit = "Klinke_bereit";
    public const string SchlossDruecken = "Druecken", SchlossLoslassen = "Loslassen";

    /// <summary>Ab diesem Anteil des Drückens (0,8 = die ersten 16 von 20 Bildern, dann Halten) darf die Blende ins Schwarz laufen.</summary>
    public const double VorlaufAnteil = 0.8;

    public KlinkePhase Phase { get; private set; } = KlinkePhase.Aus;

    /// <summary>Die Klinke am Schloss steht oben (Ruhelage): in Bereit, im Übergang dorthin und beim Greifen; sie geht nur beim Drücken nach unten und kommt beim Loslassen wieder hoch.</summary>
    public bool KlinkeInRuhelage => Phase is KlinkePhase.Aus or KlinkePhase.Greifen;

    /// <summary>Die Folge läuft (die Hand folgt ihr, nicht dem Haltungsautomaten).</summary>
    public bool Laeuft => Phase != KlinkePhase.Aus;

    private double _t, _greifen, _druecken, _loslassen;

    /// <summary>Beginnt die Folge, wenn die Hand in der Haltung Bereit steht (<paramref name="bereit"/>). Liefert die Zeit, nach der die Blende beginnen soll, sonst null (keine Klinke, die Blende läuft allein).</summary>
    public double? Starten(bool bereit, double greifenDauer, double drueckenDauer, double loslassenDauer)
    {
        if (Phase != KlinkePhase.Aus || !bereit || greifenDauer <= 0 || drueckenDauer <= 0) return null;
        (_greifen, _druecken, _loslassen) = (greifenDauer, drueckenDauer, loslassenDauer);
        Phase = KlinkePhase.Greifen;
        _t = 0;
        return greifenDauer + drueckenDauer * VorlaufAnteil;
    }

    /// <summary>Der Raum ist offen oder das Betreten abgebrochen: die Hand lässt die Klinke los (nur aus Greifen, Drücken oder Halten).</summary>
    public bool Loslassen()
    {
        if (Phase is not (KlinkePhase.Greifen or KlinkePhase.Druecken or KlinkePhase.Gehalten)) return false;
        Phase = KlinkePhase.Loslassen;
        _t = 0;
        return true;
    }

    /// <summary>Ein Bild weiter; gibt true zurück, wenn die Stufe wechselte (dann ist <see cref="Clip"/> neu zu spielen).</summary>
    public bool Schritt(double dt)
    {
        _t += Math.Max(dt, 0);
        switch (Phase)
        {
            case KlinkePhase.Greifen when _t >= _greifen:
                Phase = KlinkePhase.Druecken; _t -= _greifen; return true;
            case KlinkePhase.Druecken when _t >= _druecken:
                Phase = KlinkePhase.Gehalten; _t = 0; return true;
            case KlinkePhase.Loslassen when _t >= _loslassen:
                Phase = KlinkePhase.Aus; _t = 0; return true;
        }
        return false;
    }

    /// <summary>Der Clip der Hand zur Stufe (null: Aus und Gehalten, dort bleibt die Hand im letzten Bild des Drückens).</summary>
    public string? Clip => Phase switch
    {
        KlinkePhase.Greifen => ClipGreifen,
        KlinkePhase.Druecken => ClipDruecken,
        KlinkePhase.Loslassen => ClipLoslassen,
        _ => null,
    };

    /// <summary>Die Aktion am Schloss zur Stufe (null: keine).</summary>
    public string? SchlossAktion => Phase switch
    {
        KlinkePhase.Druecken => SchlossDruecken,
        KlinkePhase.Loslassen => SchlossLoslassen,
        _ => null,
    };
}

/// <summary>Was ein Klick auf ein Gebäude auslöst (Nutzer, 11.11.2026): der erste Klick öffnet sofort die Kurzübersicht; erkennt das Spiel den Doppelklick auf ein betretbares Gebäude, schließt es die Tafel wieder und betritt das Haus (mit der Klinke).</summary>
public enum KlickFolge { Nichts, TafelZeigen, TafelSchliessenUndBetreten }

public static class KlickRegel
{
    /// <summary>Entscheidet für einen Klick. Läuft schon ein Übergang, zählt der Klick nicht; ein Doppelklick auf ein Gebäude ohne Innenraum zeigt wie ein Einzelklick die Tafel.</summary>
    public static KlickFolge Entscheide(bool doppelklick, bool betretbar, bool uebergangLaeuft) =>
        uebergangLaeuft ? KlickFolge.Nichts : doppelklick && betretbar ? KlickFolge.TafelSchliessenUndBetreten : KlickFolge.TafelZeigen;
}

/// <summary>
/// Das Einwachsen des Türschlosses und das Gleiten des Zeigerpunkts zur Schlossmitte, ohne Godot (Tests in <c>KlinkeFolgeTests.cs</c>): ein Fortschritt t von 0 bis 1,
/// der sich mit der Dauer der Bereit-Übergänge der Hand (7 Bilder bei 24/s = 0,2917 s, <see cref="UebergangSekunden"/>) auf sein Ziel zubewegt, und die Kurve
/// Smoothstep <c>s = t²(3 − 2t)</c> wie bei Knochen und Handlage der Clips. Das Ziel wird gleichzeitig mit dem Übergangsclip der Hand gesetzt (Hover-Eintritt: 1, Austritt: 0);
/// dreht der Wunsch mitten im Wachsen um, läuft t von dort rückwärts (gleich schnell, kein Sprung).
/// </summary>
public sealed class SchlossEinwuchs
{
    /// <summary>Dauer der Übergänge Ruhe/Zeigen nach Bereit und zurück (7 Bilder bei 24/s); gelesen wird die Länge aus der Datei, das hier gilt als Vorgabe.</summary>
    public const double UebergangSekunden = 7.0 / 24.0;

    /// <summary>Fortschritt 0..1 (linear in der Zeit).</summary>
    public double T { get; private set; }

    /// <summary>Wohin es geht: 1 = Schloss voll da, 0 = weg.</summary>
    public double Ziel { get; private set; }

    /// <summary>Der Wert der Kurve (Smoothstep) – Größe des Schlosses (0..1) und Gewicht des Zeigerpunkts.</summary>
    public double S => Sanft(T);

    public static double Sanft(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    /// <summary>Setzt das Ziel (der Übergangsclip der Hand beginnt jetzt).</summary>
    public void ZielSetzen(double ziel) => Ziel = ziel > 0.5 ? 1 : 0;

    /// <summary>Ein Bild weiter; <paramref name="dauer"/> = Länge des Übergangsclips (Sekunden).</summary>
    public void Schritt(double dt, double dauer = UebergangSekunden)
    {
        var schritt = Math.Max(dt, 0) / Math.Max(dauer, 1e-6);
        T = Ziel > T ? Math.Min(Ziel, T + schritt) : Math.Max(Ziel, T - schritt);
    }

    /// <summary>Beginnt ein Übergangsclip der Hand mit diesem Namen? Dann das neue Ziel (nach Bereit hin 1, von Bereit weg 0), sonst null (keine Änderung).</summary>
    public static double? ZielFuerClip(string clip) =>
        clip.EndsWith("_Bereit") && clip.StartsWith("Uebergang_") ? 1.0 : clip.StartsWith("Uebergang_Bereit_") ? 0.0 : null;
}
