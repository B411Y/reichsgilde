namespace Reichsgilde;

/// <summary>
/// Der Ablauf eines Taschendiebstahls und einer Festnahme in der Darstellung (Wachen und Diebe, Paket P3). Nur Rechnung ohne Godot, damit ein Test sie
/// mitübersetzen kann (<c>TaschendiebgangTests</c>); die Stadtansicht (Stadtansicht.Taschendiebe.cs) setzt Figuren und Wege danach.
///
/// Alles hängt allein an der Spielzeit in Minuten (<c>VergangeneMinuten</c> plus angebrochener Bruchteil): Bei Pause, Sprung oder Laden steht die
/// Figur immer dort, wo sie zu dieser Zeit sein muss. Der Kern sagt, <i>wann</i> ein Dieb zugreift (<c>Diebeswesen.GriffeDieserStunde</c>, feste Formel
/// ohne Zufall); alles Übrige (wer das Opfer ist, woher der Dieb kommt) wählt die Darstellung aus festen Hashwerten – ohne Wirkung auf das Spiel.
///
/// Ein Griff (Zeit in Spielminuten, <c>griff</c> = Minute des Zugriffs):
///   • <see cref="Abschnitt.Anschleichen"/> von <c>griff − Vorlauf</c> bis <c>griff</c>: der Dieb geht auf ein Opfer zu;
///   • <see cref="Abschnitt.Griff"/> von <c>griff</c> bis <c>griff + </c><see cref="GriffDauer"/>: er rempelt es an und greift in die Gürteltasche;
///   • <see cref="Abschnitt.Abgang"/> danach <c>Abgang</c> Minuten lang: er geht mit der Beute davon und ist dann weg.
/// Eigene Diebe zeigt das Spiel länger (sie kommen vom Rand des Marktes und gehen dorthin zurück); fremde nur im Moment des Griffs, aus dem Gedränge.
/// </summary>
public static class Taschendiebgang
{
    /// <summary>So viele Spielminuten vor dem Griff erscheint ein eigener Dieb (er geht vom Rand des Marktes zum Opfer) …</summary>
    public const double VorlaufEigen = 20;
    /// <summary>… und ein fremder (er löst sich dicht beim Opfer aus dem Gedränge).</summary>
    public const double VorlaufFremd = 6;

    /// <summary>So lange dauern Anrempeln und Zugriff (Spielminuten).</summary>
    public const double GriffDauer = 2;

    /// <summary>So lange geht ein eigener Dieb nach dem Griff davon (Spielminuten) – zum Rand des Marktes …</summary>
    public const double AbgangEigen = 14;
    /// <summary>… ein fremder nur ein kurzes Stück ins Gedränge.</summary>
    public const double AbgangFremd = 6;

    /// <summary>So weit (Meter) vom Opfer entfernt beginnt der Weg eines eigenen bzw. fremden Diebes: Rand des Marktes, mindestens und höchstens …</summary>
    public const float EntfernungEigenMin = 8, EntfernungEigenMax = 18;
    /// <summary>… und die Entfernung, aus der sich ein fremder Dieb löst.</summary>
    public const float EntfernungFremd = 6.5f;

    /// <summary>Wie weit hinter dem Opfer (Meter) der Dieb steht, wenn er zugreift; beim Anrempeln rückt er ein Stück näher.</summary>
    public const float Abstand = 0.55f, Anrempeln = 0.25f;

    /// <summary>Wo im Ablauf eines Griffs die Zeit <c>jetzt</c> liegt.</summary>
    public enum Abschnitt { Vorher, Anschleichen, Griff, Abgang, Nachher }

    public static double Vorlauf(bool eigen) => eigen ? VorlaufEigen : VorlaufFremd;
    public static double Abgang(bool eigen) => eigen ? AbgangEigen : AbgangFremd;

    /// <summary>
    /// Der Abschnitt zur Zeit <paramref name="jetzt"/> und wie weit er ist (<paramref name="anteil"/> 0 bis 1). Vor dem Vorlauf ist es
    /// <see cref="Abschnitt.Vorher"/> (Dieb nicht zu sehen), nach dem Abgang <see cref="Abschnitt.Nachher"/> (er ist weg).
    /// </summary>
    public static Abschnitt AbschnittBei(double jetzt, double griff, bool eigen, out double anteil) =>
        AbschnittBei(jetzt, griff, eigen, Vorlauf(eigen), out anteil);

    /// <summary>Wie <see cref="AbschnittBei(double,double,bool,out double)"/>, aber mit eigenem Vorlauf (kürzer, wenn der Griff erst spät bekannt wurde).</summary>
    public static Abschnitt AbschnittBei(double jetzt, double griff, bool eigen, double vorlauf, out double anteil)
    {
        var anfang = griff - vorlauf;
        if (jetzt < anfang) { anteil = 0; return Abschnitt.Vorher; }
        if (jetzt < griff) { anteil = (jetzt - anfang) / (griff - anfang); return Abschnitt.Anschleichen; }
        if (jetzt < griff + GriffDauer) { anteil = (jetzt - griff) / GriffDauer; return Abschnitt.Griff; }
        var ende = griff + GriffDauer + Abgang(eigen);
        if (jetzt < ende) { anteil = (jetzt - griff - GriffDauer) / Abgang(eigen); return Abschnitt.Abgang; }
        anteil = 1;
        return Abschnitt.Nachher;
    }

    /// <summary>Kürzester Vorlauf (Spielminuten): Auch ein Griff, den der Kern erst zum Stundenwechsel meldet, beginnt so weit vorher.</summary>
    public const double VorlaufKuerzest = 2;

    /// <summary>Ist es Zeit, den Griff zu zeigen? Ab seinem Vorlauf bis zum Ende des Zugriffs (später gemeldete Griffe beginnen sofort).</summary>
    public static bool Zeigen(double jetzt, double griff, bool eigen) => jetzt >= griff - Vorlauf(eigen) && jetzt < griff + GriffDauer;

    /// <summary>
    /// Der Vorlauf, mit dem die Szene beginnt: der volle, solange <paramref name="jetzt"/> nicht später als er beginnt; sonst nur die Zeit bis zum Griff
    /// (mindestens <see cref="VorlaufKuerzest"/>). Dazu <paramref name="entfernung"/>: Der Dieb beginnt dann entsprechend näher am Opfer (kein Sprung ins Bild).
    /// </summary>
    public static double StartVorlauf(double jetzt, double griff, bool eigen, out float entfernung)
    {
        var voll = Vorlauf(eigen);
        var v = System.Math.Clamp(griff - jetzt, VorlaufKuerzest, voll);
        entfernung = System.Math.Clamp((float)v * 0.7f, 2.5f, EntfernungFremd); // etwa 0,7 m je Spielminute, höchstens so weit wie ein fremder Dieb (er taucht aus dem Gedränge auf)
        return v;
    }

    /// <summary>Wie weit sich der Dieb beim Griff nach vorn neigt (0 bis 1 bis 0, Gipfel in der Mitte des Griffs) – das Anrempeln.</summary>
    public static double Neigung(double anteil) => anteil is <= 0 or >= 1 ? 0 : System.Math.Sin(System.Math.PI * anteil);

    /// <summary>Der Hash, aus dem die Darstellung Opfer, Rand und Richtung wählt: fest aus drei Zahlen (Nummer des Diebes, Minute des Griffs, Verwendungszweck).</summary>
    public static uint Streu(int a, long b, int c)
    {
        var x = (uint)a * 2654435761u ^ (uint)b * 40503u ^ (uint)c * 374761393u;
        x ^= x >> 15;
        x *= 2246822519u;
        x ^= x >> 13;
        x *= 3266489917u;
        x ^= x >> 16;
        return x;
    }

    // ── Festnahme ───────────────────────────────────────────────────────────

    /// <summary>Wie weit (Meter) vor dem Dieb der Gardist beim Alarm auf der Gasse erscheint (er läuft die letzten Meter über den Markt).</summary>
    public const float WacheVorlauf = 20;

    /// <summary>Wie lange der Zugriff dauert (Spielminuten): Der Gardist packt zu, der Dieb wehrt sich kurz.</summary>
    public const double ZugriffDauer = 2;

    public enum Fest { Lauf, Zugriff, Abfuehren, Fertig }

    /// <summary>
    /// Der Abschnitt der Festnahme <paramref name="seit"/> Spielminuten nach dem Alarm: der Gardist läuft <paramref name="laufDauer"/> Minuten, packt zu
    /// (<see cref="ZugriffDauer"/>) und führt den Dieb in <paramref name="fuehrDauer"/> Minuten ab. <paramref name="anteil"/>: wie weit der Abschnitt ist.
    /// </summary>
    public static Fest FestnahmeBei(double seit, double laufDauer, double fuehrDauer, out double anteil)
    {
        if (seit < laufDauer) { anteil = laufDauer <= 0 ? 1 : System.Math.Max(0, seit) / laufDauer; return Fest.Lauf; }
        seit -= laufDauer;
        if (seit < ZugriffDauer) { anteil = seit / ZugriffDauer; return Fest.Zugriff; }
        seit -= ZugriffDauer;
        if (seit < fuehrDauer) { anteil = seit / fuehrDauer; return Fest.Abfuehren; }
        anteil = 1;
        return Fest.Fertig;
    }
}
