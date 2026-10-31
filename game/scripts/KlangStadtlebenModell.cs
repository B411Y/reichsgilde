namespace Reichsgilde;

/// <summary>
/// Die <b>Menschen hinter den Umgebungsklängen</b> (Ton Schritt 4, Nachbesserung 31.10.2026) ohne Godot: Gemurmel, Passantenschritte,
/// Markttreiben, Schenken und Hämmern hängen nicht mehr nur an Uhrzeit und Zoom, sondern an dem, was in der Stadt wirklich geschieht.
/// Leere Straßen sind still, volle Straßen murmeln.
///
/// <para><b>Ablauf.</b> Die Stadtansicht füllt je Bild (höchstens zehnmal je Sekunde) ein <see cref="Momentbild"/>: wer draußen geht oder
/// steht, wer auf dem Markt ist, wie viele Gäste in einer geöffneten Schänke sitzen, welche Werkstätten gerade arbeiten – jeweils mit Ort
/// in Metern und der Seite im Bild (−1 links … +1 rechts). Der <see cref="Zustand"/> rechnet daraus mit dem Zoom die <see cref="Lage"/>:
/// Pegel von 0 bis 1 je Klang, weich geglättet (Anstieg mit 3 s, Abfall mit 6 s Zeitkonstante). <see cref="KlangUmgebungModell"/> mischt
/// die Schichten damit und wählt die Orte der Einzelrufe.</para>
///
/// <para><b>Umkreis.</b> Gezählt wird, wer im Umkreis des Kamerablickpunkts am Boden ist – auch hinter Häusern, nicht nur im Bild. Der
/// Umkreis wächst mit dem Zoom S (sichtbare Bildhöhe in Metern): <c>25 + 0,7·S</c> m (Zoom 25: 42 m, Startzoom 95: 91 m, Höchstzoom
/// 140: 123 m), für Schenken <c>15 + 0,5·S</c> (Zoom 40: 35 m), für Hämmern <c>60 + 1,2·S</c> (der Schlag trägt weit; Zoom 40: 108 m), für Schritte <c>15 + 0,45·S</c>. Wer näher als 30 % des
/// Umkreises ist, zählt ganz, danach fällt das Gewicht mit Smoothstep auf 0 am Rand. Die Menge wird gesättigt:
/// <c>1 − exp(−(g − frei)/k)</c> – wenige Leute murmeln nicht, viele nicht lauter als eine volle Straße.</para>
/// </summary>
public static class KlangStadtlebenModell
{
    /// <summary>
    /// Ein Mensch oder Ort in der Welt. <see cref="Seite"/>: waagerechte Lage im Bild, −1 links … +1 rechts (außerhalb des Bildes auch
    /// darüber hinaus – der Aufrufer rechnet sie aus der Kamera). <see cref="Anzahl"/>: bei Schänken die Gäste, bei Werkstätten die
    /// Arbeiter. <see cref="Geht"/>: Der Mensch ist unterwegs (nur dann hört man Schritte).
    /// </summary>
    public readonly record struct Quelle(float X, float Z, float Seite = 0f, int Anzahl = 1, bool Geht = false);

    /// <summary>Was die Stadtansicht in diesem Bild weiß. Wird weiterverwendet (<see cref="Leeren"/>), damit keine Listen entstehen.</summary>
    public sealed class Momentbild
    {
        /// <summary>Blickpunkt der Kamera am Boden (Meter).</summary>
        public float FokusX, FokusZ;
        /// <summary>Wer draußen auf Gasse und Tor geht oder steht (nicht im Umland, nicht auf dem Markt, nicht in Gebäuden).</summary>
        public readonly List<Quelle> Leute = new();
        /// <summary>Wer auf dem Markt ist: Kunden, Plaudernde, Schlendernde und – nur bei offenem Markt – die Verkäufer an den Ständen.</summary>
        public readonly List<Quelle> Marktleute = new();
        /// <summary>Ist der Markt offen (Arbeitszeit)? Sonst zählt auch ein Mensch auf dem Platz nur als Passant.</summary>
        public bool MarktOffen;
        /// <summary>Geöffnete Schänken mit mindestens einem Gast (Anzahl = Gäste).</summary>
        public readonly List<Quelle> Schenken = new();
        /// <summary>Betriebe, die gerade laut arbeiten (Hämmern; Anzahl = Arbeiter).</summary>
        public readonly List<Quelle> Werkstaetten = new();
        /// <summary>Wachleute auf Streife, die gerade zu sehen sind (Nachtwächter: Horn und Glöckchen kommen von hier).</summary>
        public readonly List<Quelle> Wachen = new();
        /// <summary>Ort des Doms (oder der Hauptkirche), von dem die Glocken läuten; null, wenn die Karte keinen hat.</summary>
        public Quelle? Dom;
        /// <summary>Gäste in der Schankstube, in der man gerade ist (Innenansicht des Wirtshauses); −1 = nicht in einer Schankstube.</summary>
        public int InnenGaeste = -1;
        /// <summary>Die Umgebung um den Blickpunkt (Stadt gegen Natur, <see cref="KlangUmweltModell"/>), roh für den Umkreis dieses Bildes; null ohne Karte.</summary>
        public KlangUmweltModell.Lage? Umwelt;

        public void Leeren()
        {
            Leute.Clear();
            Marktleute.Clear();
            Schenken.Clear();
            Werkstaetten.Clear();
            Wachen.Clear();
            Dom = null;
            InnenGaeste = -1;
            Umwelt = null;
            MarktOffen = false;
        }
    }

    /// <summary>
    /// Ergebnis: Pegel 0–1 (geglättet) und die Orte für die Rufe. <see cref="Gemessen"/> ist false, wenn niemand etwas gemessen hat
    /// (Selbsttests, Dienst ohne Partie): Dann gilt <see cref="Voll"/>, also das Verhalten vor dieser Nachbesserung (nur Uhr und Zoom).
    /// </summary>
    public sealed class Lage
    {
        public bool Gemessen;
        /// <summary>Menschen auf Straßen und Platz im Umkreis → Stadtleben und Gemurmel.</summary>
        public float Menge;
        /// <summary>Markttreiben (nur bei offenem Markt) → Gemurmel.</summary>
        public float Markt;
        /// <summary>Gäste in geöffneten Schänken im Umkreis (der lauteste Ort zählt) → Schenkengemurmel.</summary>
        public float Schenke;
        /// <summary>Seite der lautesten Schänke, −1…+1 (geglättet) → Panner des Schenkenbusses.</summary>
        public float SchenkeSeite;
        /// <summary>Arbeitende Werkstätten in Hörweite → Hämmern in der Ferne.</summary>
        public float Werkstatt;
        /// <summary>Wer im Umkreis der Schritte geht → Passantenschritte (mehr Gänger, häufiger).</summary>
        public float Geher;
        /// <summary>Zahlen fürs Log und den Entwicklerreiter (ungeglättet, im Umkreis).</summary>
        public int LeuteImUmkreis, MarktleuteImUmkreis, GaesteImUmkreis, ArbeiterImUmkreis, GeherImUmkreis;
        /// <summary>Kandidaten für den Ort eines Rufs (neu in jedem Bild): Gänger im Umkreis der Schritte, arbeitende Werkstätten in Hörweite.</summary>
        public readonly List<(Quelle Q, float Abstand)> GeherOrte = new();
        public readonly List<(Quelle Q, float Abstand)> WerkOrte = new();
        /// <summary>Umkreise dieses Bildes (Meter), für die Abstufung der Rufe nach Entfernung.</summary>
        public float GeherUmkreis = 60f, WerkUmkreis = 130f;

        /// <summary>Wie viele Gesprächsgruppen in Hörweite stehen (0–1, geglättet) → Häufigkeit der Gesprächsfetzen.</summary>
        public float Gespraech;
        /// <summary>Gesprächsgruppen im Umkreis (neu in jedem Bild): Ort, Abstand zum Blickpunkt, ob am Markt, Köpfe.</summary>
        public readonly List<(Quelle Q, float Abstand, bool Markt, int Koepfe)> GruppenOrte = new();
        public float GruppenUmkreis = 40f;
        public int GruppenImUmkreis;
        /// <summary>Wachleute auf Streife in Hörweite des Horns (0–1, geglättet) und ihre Orte (neu in jedem Bild).</summary>
        public float Wache;
        public readonly List<(Quelle Q, float Abstand)> WachOrte = new();
        public float WachUmkreis = 120f;
        /// <summary>Der Dom: Seite im Bild (−1…+1) und Abstand zum Blickpunkt in Metern; <see cref="HatDom"/> false = unbekannt.</summary>
        public bool HatDom;
        public float DomSeite, DomAbstand = 60f;
        /// <summary>Die Umgebung (Stadt gegen Natur, Baum, Feld, Wasser), geglättet; ohne Karte <see cref="KlangUmweltModell.Lage.Stadtmitte"/>.</summary>
        public KlangUmweltModell.Lage Umwelt = KlangUmweltModell.Lage.Stadtmitte;
        /// <summary>Nähe zur lautesten Schänke (0–1): dicht davor und herangezoomt klingt sie hell und laut, wie durch die offene Tür.</summary>
        public float SchenkeNah;
        /// <summary>Gäste in der lautesten geöffneten Schänke, ohne Rücksicht auf die Entfernung (0–1): So voll klingt es in der Schankstube selbst.</summary>
        public float SchenkeInnen;

        /// <summary>Keine Messung: alles voll, wie vor der Nachbesserung. Gemeinsam benutzt, nicht verändern.</summary>
        public static readonly Lage Voll = new() { Gemessen = false, Menge = 1f, Markt = 1f, Schenke = 1f, Werkstatt = 1f, Geher = 1f };
    }

    // ── Umkreise und Sättigung ──────────────────────────────────────────────────────────────────────────────────

    public static float Umkreis(float zoom) => 25f + 0.7f * zoom;
    public static float SchenkenUmkreis(float zoom) => 15f + 0.5f * zoom;
    public static float WerkUmkreis(float zoom) => 60f + 1.2f * zoom;
    public static float GeherUmkreis(float zoom) => 15f + 0.45f * zoom;
    /// <summary>Gesprächsfetzen tragen nicht weit: Zoom 40 → 26 m, Startzoom 95 → 45 m.</summary>
    public static float GespraechUmkreis(float zoom) => 12f + 0.35f * zoom;
    /// <summary>Das Horn des Nachtwächters trägt weit: Zoom 95 → 135 m.</summary>
    public static float WachUmkreis(float zoom) => 40f + 1.0f * zoom;
    /// <summary>Wie nah zwei Menschen beieinander stehen, damit sie als Gruppe gelten (Meter).</summary>
    public const float GruppenAbstand = 3.5f;

    /// <summary>Gewicht eines Menschen im Abstand d (Meter) bei Umkreis r: 1 bis 30 % des Umkreises, dann Smoothstep auf 0 am Rand.</summary>
    public static float Nahe(float d, float r) => 1f - KlangUmgebungModell.Glatt(0.3f * r, r, d);

    /// <summary>Sättigung: 0 bis zur Menge <paramref name="frei"/>, dann <c>1 − exp(−(g − frei)/k)</c>.</summary>
    public static float Saettigung(float g, float frei, float k) => g <= frei ? 0f : 1f - MathF.Exp(-(g - frei) / k);

    // Mengen, bei denen es hörbar wird (frei) und wie schnell es voll wird (k): gewichtete Köpfe
    public const float MengeFrei = 1.0f, MengeK = 6.0f;      // ab 2 Leuten ein Hauch, 8 → 0,63, 14 → 0,83
    public const float MarktFrei = 0.5f, MarktK = 4.0f;      // 4 Leute am Stand → 0,58, 8 → 0,83
    public const float SchenkeFrei = 0.5f, SchenkeK = 3.0f;  // 2 Gäste → 0,39, 5 → 0,78
    public const float WerkK = 2.0f;                         // 1 Arbeiter 0,39, 2 → 0,63, 4 → 0,86
    public const float GeherK = 3.0f;                        // 1 Gänger 0,28, 3 → 0,63
    public const float GespraechK = 2.0f;                    // 1 Gruppe in Hörweite 0,39, 2 → 0,63
    public const float WachK = 1.0f;                         // 1 Streife 0,63

    /// <summary>Anstieg und Abfall der Pegel: Zeitkonstanten in Sekunden Echtzeit.</summary>
    public const float AnstiegSekunden = 3f, AbfallSekunden = 6f, SeiteSekunden = 1.5f, UmweltSekunden = 3f;

    /// <summary>Die ungeglätteten Zielwerte eines Bildes (Pegel 0–1, Seite −1…+1).</summary>
    public readonly record struct Ziele(float Menge, float Markt, float Schenke, float Seite, float Werkstatt, float Geher, float Gespraech = 0f,
                                        float Wache = 0f, float SchenkeNah = 0f, float SchenkeInnen = 0f);

    /// <summary>Die Zielwerte (ungeglättet) aus einem Momentbild und dem Zoom; füllt auch die Zahlen und Orte in <paramref name="l"/>.</summary>
    public static Ziele Berechnen(Momentbild m, float zoom, Lage l)
    {
        float r = Umkreis(zoom), rs = SchenkenUmkreis(zoom), rw = WerkUmkreis(zoom), rg = GeherUmkreis(zoom);
        l.GeherUmkreis = rg;
        l.WerkUmkreis = rw;
        l.GeherOrte.Clear();
        l.WerkOrte.Clear();
        l.GruppenOrte.Clear();
        l.WachOrte.Clear();
        float Abstand(in Quelle q) => MathF.Sqrt((q.X - m.FokusX) * (q.X - m.FokusX) + (q.Z - m.FokusZ) * (q.Z - m.FokusZ));

        float g = 0, gm = 0, gg = 0;
        int leute = 0, marktleute = 0, geher = 0;
        foreach (var q in m.Leute)
        {
            var d = Abstand(q);
            g += Nahe(d, r);
            if (d < r) leute++;
            if (q.Geht && d < rg)
            {
                gg += Nahe(d, rg);
                geher++;
                l.GeherOrte.Add((q, d));
            }
        }
        foreach (var q in m.Marktleute)
        {
            var d = Abstand(q);
            g += Nahe(d, r);
            if (d < r) marktleute++;
            if (m.MarktOffen) gm += Nahe(d, r * 1.3f); // ein Markt trägt weiter als eine einzelne Stimme
            if (q.Geht && d < rg)
            {
                gg += Nahe(d, rg);
                geher++;
                l.GeherOrte.Add((q, d));
            }
        }
        l.LeuteImUmkreis = leute + marktleute;
        l.MarktleuteImUmkreis = m.MarktOffen ? marktleute : 0;
        l.GeherImUmkreis = geher;
        var menge = Saettigung(g, MengeFrei, MengeK);
        var markt = m.MarktOffen ? Saettigung(gm, MarktFrei, MarktK) : 0f;
        var gehGewicht = Saettigung(gg, 0f, GeherK);

        // Schänken: der lauteste Ort zählt (zwei Schänken nebeneinander werden nicht doppelt so laut), seine Seite bestimmt die Lage
        float best = 0, bestSeite = 0;
        int gaeste = 0;
        foreach (var q in m.Schenken)
        {
            var d = Abstand(q);
            var w = Saettigung(q.Anzahl, SchenkeFrei, SchenkeK) * Nahe(d, rs);
            if (d < rs) gaeste += q.Anzahl;
            if (w > best)
            {
                best = w;
                bestSeite = q.Seite;
            }
        }
        l.GaesteImUmkreis = gaeste;
        var seite = Math.Clamp(bestSeite, -1f, 1f);

        // Werkstätten: hörbar, wenn eine in Hörweite arbeitet; mehrere Arbeiter machen es sicherer, nicht lauter
        float werkSumme = 0;
        int arbeiter = 0;
        foreach (var q in m.Werkstaetten)
        {
            var d = Abstand(q);
            if (d >= rw) continue;
            werkSumme += q.Anzahl * Nahe(d, rw);
            arbeiter += q.Anzahl;
            l.WerkOrte.Add((q, d));
        }
        l.ArbeiterImUmkreis = arbeiter;

        // Wie nah ist die lauteste Schänke (nur wenn man sie hört): dicht davor und herangezoomt → hell und laut
        float schenkeNah = 0, schenkeInnen = 0;
        foreach (var q in m.Schenken)
        {
            schenkeInnen = Math.Max(schenkeInnen, Saettigung(q.Anzahl, SchenkeFrei, SchenkeK));
            var wq = Saettigung(q.Anzahl, SchenkeFrei, SchenkeK) * (1f - KlangUmgebungModell.Glatt(8f, 30f, Abstand(q))) * (1f - KlangUmgebungModell.Glatt(35f, 70f, zoom));
            schenkeNah = Math.Max(schenkeNah, wq);
        }

        if (m.InnenGaeste >= 0) schenkeInnen = Saettigung(m.InnenGaeste, SchenkeFrei, SchenkeK); // in der Schankstube selbst zählen nur ihre Gäste

        // Gesprächsgruppen: zwei oder mehr Menschen dicht beieinander, von denen mindestens einer nicht geht (Kunde am Stand, Plauder an der Ecke)
        var gruppen = Gruppen(m, zoom, l);

        // Wachleute auf Streife in Hörweite des Horns
        var rwa = WachUmkreis(zoom);
        l.WachUmkreis = rwa;
        float wachSumme = 0;
        foreach (var q in m.Wachen)
        {
            var d = Abstand(q);
            if (d >= rwa) continue;
            wachSumme += Nahe(d, rwa);
            l.WachOrte.Add((q, d));
        }

        // Der Dom: Seite im Bild und Abstand zum Blickpunkt (für die Glocken)
        if (m.Dom is { } dom)
        {
            l.HatDom = true;
            l.DomSeite = dom.Seite;
            l.DomAbstand = Abstand(dom);
        }
        else l.HatDom = false;
        return new Ziele(menge, markt, best, seite, Saettigung(werkSumme, 0f, WerkK), gehGewicht, gruppen, Saettigung(wachSumme, 0f, WachK), schenkeNah, schenkeInnen);
    }

    /// <summary>
    /// Findet die Gesprächsgruppen im Umkreis (<see cref="GespraechUmkreis"/>) und füllt <see cref="Lage.GruppenOrte"/>. Gruppe = ein Mensch mit
    /// mindestens einem Nachbarn im Abstand von <see cref="GruppenAbstand"/>; wenn alle gehen, ist es kein Gespräch, sondern ein Spaziergang.
    /// Jeder Mensch gehört höchstens einer Gruppe an (Nachbarn des ersten, der noch frei ist). Gibt das Gewicht 0–1 zurück.
    /// </summary>
    private static float Gruppen(Momentbild m, float zoom, Lage l)
    {
        var r = GespraechUmkreis(zoom);
        l.GruppenUmkreis = r;
        l.GruppenImUmkreis = 0;
        var alle = new List<(Quelle Q, bool Markt, float Abstand)>();
        foreach (var q in m.Leute)
        {
            var d = MathF.Sqrt((q.X - m.FokusX) * (q.X - m.FokusX) + (q.Z - m.FokusZ) * (q.Z - m.FokusZ));
            if (d < r + 4f) alle.Add((q, false, d));
        }
        foreach (var q in m.Marktleute)
        {
            var d = MathF.Sqrt((q.X - m.FokusX) * (q.X - m.FokusX) + (q.Z - m.FokusZ) * (q.Z - m.FokusZ));
            if (d < r + 4f) alle.Add((q, true, d));
        }
        var vergeben = new bool[alle.Count];
        float summe = 0;
        for (var i = 0; i < alle.Count; i++)
        {
            if (vergeben[i]) continue;
            float sx = alle[i].Q.X, sz = alle[i].Q.Z;
            var koepfe = 1;
            var markt = alle[i].Markt ? 1 : 0;
            var steht = !alle[i].Q.Geht;
            for (var j = i + 1; j < alle.Count; j++)
            {
                if (vergeben[j]) continue;
                float dx = alle[j].Q.X - alle[i].Q.X, dz = alle[j].Q.Z - alle[i].Q.Z;
                if (dx * dx + dz * dz > GruppenAbstand * GruppenAbstand) continue;
                vergeben[j] = true;
                sx += alle[j].Q.X;
                sz += alle[j].Q.Z;
                koepfe++;
                if (alle[j].Markt) markt++;
                if (!alle[j].Q.Geht) steht = true;
            }
            vergeben[i] = true;
            if (koepfe < 2 || !steht) continue;
            var mitte = new Quelle(sx / koepfe, sz / koepfe, alle[i].Q.Seite, koepfe, false);
            var d = MathF.Sqrt((mitte.X - m.FokusX) * (mitte.X - m.FokusX) + (mitte.Z - m.FokusZ) * (mitte.Z - m.FokusZ));
            if (d >= r) continue;
            l.GruppenImUmkreis++;
            summe += Nahe(d, r);
            l.GruppenOrte.Add((mitte, d, markt * 2 > koepfe, koepfe));
        }
        return Saettigung(summe, 0f, GespraechK);
    }

    /// <summary>
    /// Glättet die Zielwerte weich in Echtzeit (Anstieg 3 s, Abfall 6 s) und liefert die <see cref="Lage"/>. Das erste Bild setzt die Werte
    /// ohne Übergang (das erste Einblenden der Umgebung blendet ohnehin über 3 s).
    /// </summary>
    public sealed class Zustand
    {
        public readonly Lage Lage = new() { Gemessen = true };
        private bool _gestartet;

        public Lage Schritt(Momentbild m, float zoom, double delta)
        {
            var dt = (float)Math.Max(delta, 0);
            var l = Lage;
            var z = Berechnen(m, zoom, l);
            if (!_gestartet)
            {
                _gestartet = true;
                (l.Menge, l.Markt, l.Schenke, l.SchenkeSeite, l.Werkstatt, l.Geher) = (z.Menge, z.Markt, z.Schenke, z.Seite, z.Werkstatt, z.Geher);
                (l.Gespraech, l.Wache, l.SchenkeNah, l.SchenkeInnen) = (z.Gespraech, z.Wache, z.SchenkeNah, z.SchenkeInnen);
                l.Umwelt = m.Umwelt ?? KlangUmweltModell.Lage.Stadtmitte;
                return l;
            }
            l.Menge = Folge(l.Menge, z.Menge, dt);
            l.Markt = Folge(l.Markt, z.Markt, dt);
            l.Schenke = Folge(l.Schenke, z.Schenke, dt);
            l.Werkstatt = Folge(l.Werkstatt, z.Werkstatt, dt);
            l.Geher = Folge(l.Geher, z.Geher, dt);
            l.Gespraech = Folge(l.Gespraech, z.Gespraech, dt);
            l.Wache = Folge(l.Wache, z.Wache, dt);
            l.SchenkeNah = Folge(l.SchenkeNah, z.SchenkeNah, dt);
            l.SchenkeInnen = Folge(l.SchenkeInnen, z.SchenkeInnen, dt);
            // Die Umgebung wechselt langsam (Zeitkonstante 3 s in beide Richtungen): Vom Markt aufs Feld wird es weich anders
            if (m.Umwelt is { } u)
            {
                var k = 1f - MathF.Exp(-dt / UmweltSekunden);
                var a = l.Umwelt;
                l.Umwelt = new KlangUmweltModell.Lage(a.Stadt + (u.Stadt - a.Stadt) * k, a.Baum + (u.Baum - a.Baum) * k, a.Feld + (u.Feld - a.Feld) * k, a.Wasser + (u.Wasser - a.Wasser) * k);
            }
            else l.Umwelt = KlangUmweltModell.Lage.Stadtmitte;
            // Seite nur nachführen, solange man die Schänke hört (sonst wandert der Klang beim Ausblenden)
            if (z.Schenke > 0.01f) l.SchenkeSeite += (z.Seite - l.SchenkeSeite) * (1f - MathF.Exp(-dt / SeiteSekunden));
            return l;
        }

        /// <summary>Alle Pegel sofort auf die Zielwerte (Sprung der Uhr, neue Partie).</summary>
        public void Zuruecksetzen() => _gestartet = false;

        private static float Folge(float ist, float ziel, float dt) =>
            ist + (ziel - ist) * (1f - MathF.Exp(-dt / (ziel > ist ? AnstiegSekunden : AbfallSekunden)));
    }

    // ── Ein festes Szenario für Hörprobe und Selbsttest ────────────────────────────────────────────────────────

    /// <summary>
    /// Ein erfundener, aber gleichmäßiger Tagesablauf (die Zahlen rechnet <see cref="Zaehlen"/> aus der Uhr): leere Straßen bis 6:30, die Straßen
    /// füllen sich bis 9 Uhr, der Markt öffnet um 8 Uhr, mittags am vollsten, abends leeren sich die Straßen, die Schänke füllt sich und schließt
    /// um 22 Uhr, die Werkstatt hämmert 8–20 Uhr. Die Menschen stehen deterministisch im Kreis um den Blickpunkt (Markt links, Schänke links davor,
    /// Werkstatt rechts hinten). Ersetzt in der Hörprobe <c>--umgebungfahrt=1</c> die Stadtansicht, damit leere und volle Straßen vergleichbar sind.
    /// </summary>
    public static class Szenario
    {
        private static float Trapez(float x, float a, float b, float c, float d) => KlangUmgebungModell.Trapez(x, a, b, c, d);

        public static (int Leute, int Markt, int Gaeste, int Arbeiter, bool MarktOffen) Zaehlen(float stunde)
        {
            var offen = stunde is >= 8f and < 20f;
            var leute = 16f * Trapez(stunde, 6.5f, 9.0f, 17.5f, 20.5f) + 5f * Trapez(stunde, 19.5f, 20.5f, 21.5f, 22.5f);
            var markt = offen ? 14f * Trapez(stunde, 8.0f, 9.5f, 17.0f, 20.0f) : 0f;
            var gaeste = stunde < 22f ? 8f * Trapez(stunde, 12.0f, 20.0f, 21.7f, 22.0f) : 0f;
            return ((int)MathF.Round(leute), (int)MathF.Round(markt), (int)MathF.Round(gaeste), offen ? 3 : 0, offen);
        }

        /// <summary>Wo der Dom im Szenario steht (wie in der Karte <c>greybox</c>), von dort läuten die Glocken der Hörprobe.</summary>
        public static readonly (float X, float Z) DomOrt = (-20f, -11f);

        /// <summary>
        /// Füllt das Momentbild für diese Uhr; Blickpunkt (<paramref name="fokusX"/>, <paramref name="fokusZ"/>, ohne Angabe der Ursprung = Markt), die Seite folgt
        /// aus dem Abstand zum Blickpunkt und dem Zoom (16:9). Die Menschen stehen fest um den Ursprung: Wer den Blickpunkt aus der Stadt schiebt,
        /// lässt sie hinter sich (der Abstand wächst). Von den Stehenden hat jeder einen Nachbarn dicht daneben (ein Gespräch); nachts geht eine Streife.
        /// </summary>
        public static void Fuellen(Momentbild m, float stunde, float zoom, float fokusX = 0f, float fokusZ = 0f)
        {
            m.Leeren();
            m.FokusX = fokusX;
            m.FokusZ = fokusZ;
            var (leute, markt, gaeste, arbeiter, offen) = Zaehlen(stunde);
            m.MarktOffen = offen;
            var halbe = Math.Max(zoom * 0.5f * 16f / 9f, 1f);
            float S(float x) => (x - fokusX) / halbe;
            for (var i = 0; i < leute; i++)
            {
                var winkel = i * 2.399963f;
                var d = 8f + i * 37 % 70;
                float x = MathF.Cos(winkel) * d, z = MathF.Sin(winkel) * d;
                var geht = i % 3 != 0;
                m.Leute.Add(new Quelle(x, z, S(x), 1, geht));
                if (!geht) m.Leute.Add(new Quelle(x + 1.4f, z + 0.8f, S(x + 1.4f), 1, false)); // der Gesprächspartner
            }
            for (var i = 0; i < markt; i++)
            {
                var winkel = i * 2.399963f;
                var d = 3f + i * 5 % 14;
                float x = -30f + MathF.Cos(winkel) * d, z = -12f + MathF.Sin(winkel) * d;
                var geht = i % 4 == 0;
                m.Marktleute.Add(new Quelle(x, z, S(x), 1, geht));
                if (i % 4 == 2) m.Marktleute.Add(new Quelle(x + 1.2f, z + 1f, S(x + 1.2f), 1, false)); // Kunde und Verkäufer am Stand
            }
            if (gaeste > 0) m.Schenken.Add(new Quelle(-24f, 10f, S(-24f), gaeste));
            if (arbeiter > 0) m.Werkstaetten.Add(new Quelle(75f, -45f, S(75f), arbeiter));
            m.Dom = new Quelle(DomOrt.X, DomOrt.Z, S(DomOrt.X));
            if (stunde >= 21.3f || stunde < 5.5f)
            {
                // Streife: pendelt auf der Gasse zwischen Markt und Tor (rund 160 m Weg, rund 2 Minuten Spielzeit je Strecke)
                var f = (stunde * 60f / 7f) % 2f;
                var t = f < 1f ? f : 2f - f;
                float x = -10f + 80f * t, z = 14f - 6f * t;
                m.Wachen.Add(new Quelle(x, z, S(x), 1, true));
            }
        }
    }
}
