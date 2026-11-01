namespace Reichsgilde;

/// <summary>
/// Schicht 4 der Passanten (Nutzer, 1.11.2026): Figuren weichen einander aus, damit sich keine Körper überschneiden. Nur Rechnung ohne Godot, damit ein
/// Test sie mitübersetzen kann (<c>PassantenausweichenTests</c>); die Darstellung (Stadtansicht.Passantenausweichen.cs) füllt jedes Bild die Liste der
/// Figuren und übernimmt das Ergebnis im nächsten Bild.
///
/// So geht es:
///   • Jede laufende Figur schaut <see cref="Vorschau"/> Sekunden voraus, wie nah ihr jede Nachbarin kommt, wenn beide so weitergehen (nächster Abstand
///     der beiden Bahnen). Kommt sie näher als beide Körper (<see cref="Figur.Radius"/>) plus <see cref="Luft"/>, tritt sie seitlich zur Seite, auf der die
///     andere nicht ist – um den fehlenden Abstand; laufen beide, übernimmt jede etwas mehr als die Hälfte. Wer steht, weicht nicht (die Laufende tut alles).
///   • Der seitliche Versatz bleibt innerhalb der Wegbreite (<see cref="Figur.Links"/> bis <see cref="Figur.Rechts"/>, die Darstellung rechnet sie aus der
///     Breite der Karte und dem Wandabstand). Reicht der Platz nicht und ist jemand dicht vor ihr in derselben Richtung (oder steht), wird sie langsamer
///     (<see cref="Figur.Bremse"/>).
///   • Kreuzen sich zwei Wege, lässt die mit der höheren <see cref="Figur.Nummer"/> der anderen den Vortritt und wird kurz langsamer – sonst warteten
///     an Kreuzungen alle aufeinander.
///   • Kein Zittern: Die Seite, die eine Figur einmal gewählt hat, behält sie, solange die andere auf gleicher Linie ist; ist der Abstand gerade ausreichend,
///     hält sie ihren Versatz (Spanne <see cref="Halten"/>), statt zurückzugleiten; seitlich bewegt sie sich höchstens mit einem Teil ihres Gehtempos, und
///     zurück zur Spur nur langsam.
///   • Kosten: Die Figuren kommen in ein Raster (Zellen so groß wie die Vorschau); jede prüft nur ihre Zelle und die acht daneben, nicht jede andere.
/// </summary>
public sealed class Passantenausweichen
{
    /// <summary>Eine Figur für einen Schritt: Eingaben (Lage, Geschwindigkeit, Richtung, Körper, Spielraum) und Ergebnis (Versatz, Bremse, Seite).</summary>
    public struct Figur
    {
        /// <summary>Lage auf dem Boden (Meter), schon mit dem bisherigen seitlichen Versatz.</summary>
        public float X, Z;
        /// <summary>Geschwindigkeit (Meter je echter Sekunde); null, wer steht.</summary>
        public float Vx, Vz;
        /// <summary>Rechtsrichtung des Weges (Länge 1), quer zu der ausgewichen wird.</summary>
        public float Rx, Rz;
        /// <summary>Halbe Körperbreite (Meter).</summary>
        public float Radius;
        /// <summary>Wie weit der seitliche Versatz nach links (≤ 0) und rechts (≥ 0) gehen darf, ohne dass die Figur den Rand des Weges erreicht.</summary>
        public float Links, Rechts;
        /// <summary>Der seitliche Versatz durch Ausweichen (Meter, rechts positiv): Eingabe der bisherige, Ergebnis der neue.</summary>
        public float Seitlich;
        /// <summary>Anteil des Gehtempos (1 = frei, 0 = steht), wenn sie hinter jemandem nicht vorbeikommt: Eingabe der bisherige, Ergebnis der neue.</summary>
        public float Bremse;
        /// <summary>Wie lange (Sekunden) sie ihren Versatz nach dem letzten Ausweichen noch hält, bevor sie zur Spur zurückgleitet: Eingabe und Ergebnis.</summary>
        public float Haltezeit;
        /// <summary>Zuletzt gewählte Ausweichseite (−1 links, +1 rechts, 0 keine) – damit sie bei gleicher Linie nicht hin und her wechselt.</summary>
        public int Seite;
        /// <summary>Läuft sie (und weicht aus)? Wer steht, ist nur Hindernis.</summary>
        public bool Laeuft;
        /// <summary>Feste Nummer (Einwohner): Kreuzen sich zwei Wege, lässt die mit der höheren Nummer der anderen den Vortritt.</summary>
        public int Nummer;
    }

    /// <summary>So viel Luft (Meter) zwischen zwei Körpern ist das Ziel.</summary>
    public const float Luft = 0.12f;

    /// <summary>So weit (Sekunden) schaut eine Figur voraus.</summary>
    public const float Vorschau = 1.6f;

    /// <summary>Ist der Abstand nur so viel (Meter) größer als nötig, hält die Figur ihren Versatz, statt zur Spur zurückzugleiten.</summary>
    public const float Halten = 0.5f;

    /// <summary>Seitlich bewegt sie sich höchstens mit diesem Anteil ihres Gehtempos (und mindestens <see cref="SeitMindestens"/> Meter je Sekunde).</summary>
    public const float SeitAnteil = 0.6f, SeitMindestens = 0.5f;

    /// <summary>Zurück zur eigenen Spur (Meter je Sekunde bei 1,3 m/s Gehtempo): langsam, damit es ruhig aussieht.</summary>
    public const float Zurueck = 0.3f;

    /// <summary>Unter diesem Abstand (über den nötigen hinaus, Meter) zu jemandem vor ihr wird sie langsamer, wenn sie nicht vorbeikommt.</summary>
    public const float Bremsweg = 0.6f;

    /// <summary>Liegt die andere seitlich höchstens so weit (Meter) neben ihrer Linie, behält sie die Seite, auf die sie schon ausweicht.</summary>
    public const float Gleichauf = 0.15f;

    /// <summary>Kleiner als das (Meter) ist ein Schub gegen die gerade gewählte Seite nicht der Rede wert: Sie hält ihren Versatz.</summary>
    public const float Totband = 0.08f;

    /// <summary>Nach dem Ausweichen hält sie den Versatz noch so lange (Sekunden), bevor sie zurückgleitet – sonst pendelte sie im Gedränge hin und her.</summary>
    public const float HaltenSekunden = 1.0f;

    /// <summary>Bremsen geht sofort, wieder schneller werden höchstens um diesen Anteil je Sekunde (kein Ruckeln zwischen Gehen und Stehen).</summary>
    public const float Anfahren = 1.5f;

    private readonly Dictionary<long, int> _kopf = new();
    private int[] _naechste = Array.Empty<int>();
    private float[] _neu = Array.Empty<float>();

    /// <summary>Wie viele Paare im letzten Schritt verglichen wurden (zum Prüfen der Kosten).</summary>
    public int Paare { get; private set; }

    /// <summary>
    /// Ein Schritt über <paramref name="dt"/> Sekunden für die ersten <paramref name="anzahl"/> Figuren: schreibt <see cref="Figur.Seitlich"/>,
    /// <see cref="Figur.Bremse"/> und <see cref="Figur.Seite"/>. Die Lage bleibt (die Darstellung setzt sie im nächsten Bild mit dem neuen Versatz).
    /// </summary>
    public void Schritt(Figur[] figuren, int anzahl, float dt)
    {
        Paare = 0;
        if (anzahl == 0) return;
        if (_naechste.Length < anzahl) { _naechste = new int[anzahl * 2]; _neu = new float[anzahl * 2]; }
        // Zellen so groß, dass alles, was in der Vorschau herankommen kann, in den neun Zellen liegt (bei hohem Spieltempo größer)
        var schnellste = 0f;
        for (var i = 0; i < anzahl; i++) schnellste = MathF.Max(schnellste, figuren[i].Vx * figuren[i].Vx + figuren[i].Vz * figuren[i].Vz);
        var zelle = MathF.Max(3.5f, 2 * MathF.Sqrt(schnellste) * Vorschau * 0.75f);
        _kopf.Clear();
        for (var i = 0; i < anzahl; i++)
        {
            var schluessel = Schluessel((int)MathF.Floor(figuren[i].X / zelle), (int)MathF.Floor(figuren[i].Z / zelle));
            _naechste[i] = _kopf.TryGetValue(schluessel, out var kopf) ? kopf : -1;
            _kopf[schluessel] = i;
        }

        for (var i = 0; i < anzahl; i++)
        {
            ref var a = ref figuren[i];
            _neu[i] = a.Seitlich;
            if (!a.Laeuft) { (a.Bremse, a.Haltezeit) = (1, 0); continue; }
            var tempo = MathF.Sqrt(a.Vx * a.Vx + a.Vz * a.Vz);
            var (fx, fz) = (a.Rz, -a.Rx); // vorwärts aus rechts = (−z, x)
            var (schiebRechts, schiebLinks, halten, luecke, vortritt) = (0f, 0f, false, float.MaxValue, 1f);
            var (cx, cz) = ((int)MathF.Floor(a.X / zelle), (int)MathF.Floor(a.Z / zelle));
            for (var ox = -1; ox <= 1; ox++)
                for (var oz = -1; oz <= 1; oz++)
                {
                    if (!_kopf.TryGetValue(Schluessel(cx + ox, cz + oz), out var j)) continue;
                    for (; j >= 0; j = _naechste[j])
                    {
                        if (j == i) continue;
                        Paare++;
                        ref readonly var b = ref figuren[j];
                        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
                        if (dx * dx + dz * dz > zelle * zelle) continue;
                        var (ux, uz) = (b.Vx - a.Vx, b.Vz - a.Vz);
                        var uu = ux * ux + uz * uz;
                        var t = uu > 1e-4f ? Math.Clamp(-(dx * ux + dz * uz) / uu, 0, Vorschau) : 0;
                        var (nx, nz) = (dx + ux * t, dz + uz * t); // wo die andere ihr am nächsten ist (von ihr aus)
                        var noetig = a.Radius + b.Radius + Luft;
                        var abstand2 = nx * nx + nz * nz;
                        // die Richtungen (aus dem Weg, nicht aus dem Tempo: wer gebremst steht, geht trotzdem in seine Richtung)
                        var richtung = b.Laeuft ? fx * b.Rz - fz * b.Rx : 1;
                        var gleichlaeufig = !b.Laeuft || richtung > 0.5f;
                        var gegenlaeufig = b.Laeuft && richtung < -0.5f;
                        // dicht vor ihr, in ihrer Richtung oder stehend: merken, falls sie nicht vorbeikommt
                        var laengs = dx * fx + dz * fz;
                        // (vorn heißt: auch aus ihrer Richtung gesehen vorn – laufen zwei Wege schräg zusammen, hielte sonst jede die andere für vorn, und beide warteten)
                        if (gleichlaeufig && laengs > 0 && (!b.Laeuft || dx * b.Rz - dz * b.Rx > 0) && MathF.Abs(dx * a.Rx + dz * a.Rz) < noetig) luecke = MathF.Min(luecke, laengs - noetig);
                        // schon ineinander, in derselben Richtung und keine vorn (dieselbe Tür, derselbe Punkt): die mit der höheren Nummer wartet (nach der
                        // Lage, nicht nach dem Weg: zwei Wege sind verschieden lang, sonst wartete jede auf die andere)
                        else if (gleichlaeufig && b.Laeuft && dx * dx + dz * dz < noetig * noetig && MathF.Abs(laengs) < 0.05f && a.Nummer > b.Nummer) luecke = MathF.Min(luecke, -0.01f);
                        if (abstand2 >= (noetig + Halten) * (noetig + Halten)) continue;
                        if (abstand2 >= noetig * noetig) { halten = true; continue; }
                        // Wege kreuzen sich (weder gleich- noch gegenläufig) und die andere ist vor ihr: Vortritt nach der Nummer – wer ihn lässt, wird
                        // langsamer, bis die andere vorbei ist (sonst warteten beide aufeinander)
                        // (nur, wenn die andere auch geht: Wer selbst wartet, bekommt keinen Vortritt – sonst warteten im Kreis alle aufeinander)
                        if (!gleichlaeufig && !gegenlaeufig && laengs > 0 && a.Nummer > b.Nummer && b.Bremse > 0.5f)
                            vortritt = MathF.Min(vortritt, Math.Clamp(t / Vorschau, 0, 1) * 0.6f);
                        var quer = nx * a.Rx + nz * a.Rz; // wo die andere seitlich ist (rechts positiv)
                        var laengsNah = nx * fx + nz * fz;
                        var seitlichNoetig = MathF.Sqrt(MathF.Max(noetig * noetig - laengsNah * laengsNah, 0));
                        if (seitlichNoetig - MathF.Abs(quer) <= 0) { halten = true; continue; }
                        var anteil = b.Laeuft ? 0.6f : 1f;
                        // Um wie viel nach links (die andere bleibt rechts) oder nach rechts (sie bleibt links) sie treten müsste
                        var (nachLinks, nachRechts) = (MathF.Max(seitlichNoetig - quer, 0) * anteil, MathF.Max(seitlichNoetig + quer, 0) * anteil);
                        var (passtRechts, passtLinks) = (a.Seitlich + nachRechts <= a.Rechts + 0.01f, a.Seitlich - nachLinks >= a.Links - 0.01f);
                        int seite;
                        if (a.Seite != 0 && MathF.Abs(quer) <= Gleichauf && (a.Seite > 0 ? passtRechts : passtLinks))
                            seite = a.Seite;                                      // fast auf gleicher Linie: die gewählte Seite behalten, solange sie passt
                        else
                        {
                            if (MathF.Abs(quer) > 0.05f) seite = quer > 0 ? -1 : 1;  // weg von der Seite, auf der die andere ist
                            else if (gegenlaeufig) seite = 1;                         // Gegenverkehr: beide nach rechts
                            else seite = a.Rechts - a.Seitlich >= a.Seitlich - a.Links ? 1 : -1; // sonst dorthin, wo mehr Platz ist
                            // Passt das nicht in den Weg, aber auf der anderen Seite schon (überholen links statt rechts): dann dort
                            if (seite > 0 ? !passtRechts && passtLinks : !passtLinks && passtRechts) seite = -seite;
                        }
                        if (seite > 0) schiebRechts = MathF.Max(schiebRechts, nachRechts);
                        else schiebLinks = MathF.Max(schiebLinks, nachLinks);
                    }
                }

            // ein kleiner Schub (unter dem Totband, die Luft zwischen den Körpern reicht noch) setzt niemanden in Bewegung, der gerade nicht ausweicht
            if (a.Seite == 0 && a.Seitlich == 0 && MathF.Max(schiebRechts, schiebLinks) < Totband / 2) (schiebRechts, schiebLinks, halten) = (0, 0, true);
            var schieben = schiebRechts - schiebLinks;
            // ein kleiner Schub gegen die Seite, auf die sie gerade ausgewichen ist, wird gehalten statt befolgt (kein Pendeln um wenige Zentimeter)
            if (a.Seite != 0 && MathF.Sign(schieben) == -a.Seite && MathF.Abs(schieben) < Totband) (schiebRechts, schiebLinks, schieben, halten) = (0, 0, 0, true);
            var beideSeiten = schiebRechts > 0 && schiebLinks > 0;
            var weicht = schiebRechts > 0 || schiebLinks > 0;
            a.Haltezeit = weicht ? HaltenSekunden : halten ? MathF.Max(a.Haltezeit, HaltenSekunden / 2) : MathF.Max(a.Haltezeit - dt, 0);
            var ziel = weicht ? a.Seitlich + schieben : a.Haltezeit > 0 ? a.Seitlich : 0;
            var geklemmt = Math.Clamp(ziel, MathF.Min(a.Links, 0), MathF.Max(a.Rechts, 0));
            var klemmt = MathF.Abs(geklemmt - ziel) > 0.05f || beideSeiten;
            // langsamer: jemand dicht vor ihr in ihrer Richtung, und vorbei geht es nicht (Rand erreicht oder auf beiden Seiten jemand) – oder sie ist schon zu nah
            var bremse = MathF.Min(vortritt, luecke < Bremsweg && (klemmt || luecke < 0) ? Math.Clamp(luecke / Bremsweg, 0, 1) : 1);
            a.Bremse = bremse <= a.Bremse ? bremse : MathF.Min(bremse, a.Bremse + Anfahren * dt);
            // dorthin gleiten: zum Ausweichen zügig, zurück zur Spur langsam
            var rate = weicht ? MathF.Max(SeitMindestens, SeitAnteil * tempo) : Zurueck * MathF.Max(1, tempo / 1.3f);
            _neu[i] = a.Seitlich + Math.Clamp(geklemmt - a.Seitlich, -rate * dt, rate * dt);
            if (weicht) a.Seite = MathF.Sign(schieben) is var s && s != 0 ? (int)s : a.Seite;
            else if (a.Haltezeit <= 0) a.Seite = 0;
        }
        for (var i = 0; i < anzahl; i++) figuren[i].Seitlich = _neu[i];
    }

    private static long Schluessel(int x, int z) => ((long)x << 32) ^ (uint)z;
}
