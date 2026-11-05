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
        // LiegeBeginn (Minute der Ankunft) kennzeichnet den Besuch: bisher nur von der Anzeige der Phase gebraucht
        var besuch = (double)(fenster * ZierFenster + ankunft);
        if (im < ankunft) return (ZierPhase.Anfahrt, (im - anfang) / fahrt, vonAuf, nachAuf, besuch);
        if (im < ablegen) return (ZierPhase.Liegen, (im - ankunft) / liegen, vonAuf, nachAuf, besuch);
        return (ZierPhase.Abfahrt, (im - ablegen) / fahrt, vonAuf, nachAuf, besuch);
    }

    private SchiffAnsicht? _zierschiff;
    private SchiffAnsicht? _zierOber, _zierNieder;
    private double? _zierAbbruch;
    private bool? _zierTypFest;

    /// <summary>Startoption <c>--zierschiff=ober|nieder</c>: Das Zierschiff ist immer dieser Typ (nur für Bildschirmfotos); sonst wechselt es je Besuch.</summary>
    public void ZierTypVorgeben(bool nieder) => _zierTypFest = nieder;

    /// <summary>
    /// Welcher Typ kommt im Fenster <paramref name="fenster"/> (Stapelrecht, Köln 1259): gerade Fenster der Niederländer (er wird entladen, die Ware kommt auf den Stapel),
    /// ungerade der Oberländer (er lädt vom Stapel auf). Fest aus der Fensternummer, ohne Zufall.
    /// </summary>
    private bool ZierIstNieder(long fenster) => _zierTypFest ?? (fenster % 2 == 0);

    /// <summary>Liegt nach dem Besuch im Fenster (oder vor dem Besuch im nächsten) ein Stapel am Steg? Nur wenn der letzte Besuch ein Niederländer war.</summary>
    private bool ZierStapelFern(double minute)
    {
        var fenster = (long)Math.Floor(minute / ZierFenster);
        var im = minute - fenster * ZierFenster;
        // Der Besuch endet frühestens bei Minute 150 und beginnt spätestens bei 110 des Fensters
        return ZierIstNieder(im >= 130 ? fenster : fenster - 1);
    }

    private SchiffAnsicht ZierSchiffHolen(bool nieder)
    {
        SchiffAnsicht Neu(string name, int nummer, string modell)
        {
            var a = SchiffErzeugen(name, new Color(0.62f, 0.2f, 0.16f), nummer, false, modell);
            a.Wurzel.Visible = false; // erst ZierschiffSetzen zeigt es
            return a;
        }
        return nieder ? _zierNieder ??= Neu("Zierschiff Niederlaender", 8, NiederlaenderModell) : _zierOber ??= Neu("Zierschiff", 7, SchiffModell);
    }

    private const int ZierVoll = SchiffLadungsknoten;

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
                var nieder = ZierIstNieder((long)Math.Floor(minute / ZierFenster));
                // Wird der Hafen zu, während das Schiff schon liegt oder kommt: es legt sofort ab und fährt seine Route zu Ende. War es vorher nicht
                // zu sehen (Partie oder Spielstand beginnt im Winter, Hafen zu), erscheint es gar nicht erst.
                if (!offen && phase is ZierPhase.Anfahrt or ZierPhase.Liegen)
                {
                    if (_zierAbbruch is null && _zierschiff is { Wurzel.Visible: true }) _zierAbbruch = minute;
                    if (_zierAbbruch is { } beginn)
                    {
                        phase = ZierPhase.Abfahrt;
                        plan = (phase, Math.Clamp((minute - beginn) / fahrt, 0, 1), plan.VonFlussauf, plan.NachFlussauf, plan.LiegeBeginn);
                    }
                    else phase = ZierPhase.Fern;
                }
                else if (offen || phase == ZierPhase.Fern) _zierAbbruch = null;
                else if (_zierAbbruch is null) phase = ZierPhase.Fern; // geschlossen, es fuhr schon: nicht neu erscheinen
                if (phase != ZierPhase.Fern) fern = !ZierschiffSetzen(w, hafen, phase, plan.Anteil, plan.VonFlussauf, plan.NachFlussauf, nieder, delta);
            }
        }
        if (fern)
            foreach (var s in new[] { _zierOber, _zierNieder })
                if (s is { Wurzel.Visible: true })
                {
                    s.Wurzel.Visible = false;
                    s.Sichtbar = false;
                }
        // Zwischen den Besuchen bleibt der Stapel des Niederländers am Steg stehen, bis der Oberländer ihn aufnimmt
        if (fern) KranRuhe(hafen is not null && ZierStapelFern(minute) ? ZierVoll : 0, hafen);
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
    private bool ZierschiffSetzen(Stadtwirtschaft w, Hafenplatz hafen, ZierPhase phase, double anteil, bool vonAuf, bool nachAuf, bool nieder, double delta)
    {
        var ansicht = _zierschiff = ZierSchiffHolen(nieder);
        var anderes = nieder ? _zierOber : _zierNieder;
        if (anderes is { Wurzel.Visible: true }) { anderes.Wurzel.Visible = false; anderes.Sichtbar = false; }
        Vector3 ziel;
        float drehung;
        if (phase == ZierPhase.Liegen)
        {
            // Längsseits am Steg, wie die Schiffe des Handels (SchiffBewegen)
            (ziel, drehung) = StegLiegeplatz(hafen, ansicht, StegAbstand, ZierLaengs);
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
                KranRuhe(ZierStapelZahl(nieder, phase), hafen);
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
                var (liege, liegeDrehung) = StegLiegeplatz(hafen, ansicht, StegAbstand, ZierLaengs);
                ziel = ziel.Lerp(liege, gleich);
                drehung = Mathf.LerpAngle(drehung, liegeDrehung, gleich);
            }
        }
        ansicht.Wurzel.Visible = true;
        SchiffPoseSetzen(ansicht, ziel, drehung, delta);
        SchiffSegelUndPlanke(ansicht, phase != ZierPhase.Liegen, phase == ZierPhase.Liegen);
        ZierLadungSetzen(ansicht, hafen, phase, anteil, nieder);
        return true;
    }

    // ── Stapelrecht (Köln 1259: Waren aus Nord und Süd mussten in Köln umgeladen werden) ────────────────────────────────────────────────────────────────────────────────────────
    // Gerade Besuche (Fenster) kommt ein Niederländer voll beladen: Der Kran hebt Stück für Stück aufs Steg-Deck, die Ware bleibt dort als Stapel stehen, das Schiff
    // fährt leer ab. Ungerade Besuche kommt ein leerer Oberländer: Der Kran hebt den Stapel Stück für Stück an Bord, er fährt voll ab. Alles reine Funktion von
    // Spielzeit und Fensternummer; Zufall gibt es nicht (und der Kern weiß nichts davon).

    private const double ZierEntladeAb = 0.05, ZierNiederEntladeBis = 0.88, ZierOberLadeAb = 0.08, ZierOberLadeBis = 0.90;

    /// <summary>Wie viele Stücke des Stapels stehen am Steg, wenn das Schiff dieser Art in dieser Phase fährt (Anfahrt, Abfahrt)?</summary>
    private static int ZierStapelZahl(bool nieder, ZierPhase phase) => phase switch
    {
        ZierPhase.Anfahrt => nieder ? 0 : ZierVoll,
        ZierPhase.Abfahrt => nieder ? ZierVoll : 0,
        _ => 0,
    };

    /// <summary>
    /// Wie viele Stücke liegen an Bord, und wie viele stehen am Steg? <paramref name="voll"/> = Stücke des Schiffs.
    /// Reine Funktion von Art, Phase und Liegeanteil (die Liegephase nur als Rückfall, wenn der Kran fehlt).
    /// </summary>
    private static (int AnBord, int Stapel) ZierLadungsstand(bool nieder, ZierPhase phase, double anteil, int voll)
    {
        if (phase == ZierPhase.Anfahrt) return nieder ? (voll, 0) : (0, voll);
        if (phase == ZierPhase.Abfahrt) return nieder ? (0, voll) : (voll, 0);
        var t = nieder
            ? Math.Clamp((anteil - ZierEntladeAb) / (ZierNiederEntladeBis - ZierEntladeAb), 0, 1)
            : Math.Clamp((anteil - ZierOberLadeAb) / (ZierOberLadeBis - ZierOberLadeAb), 0, 1);
        var n = (int)Math.Floor(t * voll + 1e-9);
        return nieder ? (voll - n, n) : (n, voll - n);
    }

    private void ZierLadungSetzen(SchiffAnsicht ansicht, Hafenplatz hafen, ZierPhase phase, double anteil, bool nieder)
    {
        var voll = ansicht.Ladung.Length;
        if (voll == 0) return;
        ZierStapelBauen(ZierSchiffHolen(true), hafen); // Der Stapel besteht aus Stücken des Niederländers (Oberländer: gleiche Arten je Nummer)
        ZierMastSetzen(ansicht, phase, anteil); // Stadtansicht.Kranladen.cs: im Hafen ist der Mast gelegt
        int anBord;
        if (phase == ZierPhase.Liegen) anBord = KranLadung(ansicht, anteil, nieder, voll); // Stadtansicht.Kranladen.cs: der Kran hebt jedes Stück
        else
        {
            var stand = ZierLadungsstand(nieder, phase, anteil, voll);
            anBord = stand.AnBord;
            KranRuhe(stand.Stapel, hafen);
        }
        // An Bord zeigt man die niedrigsten Nummern (die höchsten gehen zuerst)
        if (anBord != ansicht.GezeigteLadung)
        {
            ansicht.GezeigteLadung = anBord;
            for (var i = 0; i < voll; i++) ansicht.Ladung[i].Visible = i < anBord;
        }
    }

    private readonly List<ZierStueck> _zierStapel = new();
    private int _zierStapelGezeigt = -1;

    /// <summary>Plätze der abgeladenen Stücke auf dem Kopf des Stegs (Steg-Koordinaten, Land bei +Z): 4 Spalten × 2 Reihen westlich der Laufplanke, links und rechts vom Fuß der Rampe (aus der Standardansicht frei zu sehen, nicht hinter dem Kranhaus), dann zwei Lagen darüber.</summary>
    private static (float X, float Z, int Lage, int Grund) ZierStapelPlatz(int k)
    {
        var nummer = k < 8 ? k : (k < 16 ? k - 8 : k - 16);
        var lage = k < 8 ? 0 : (k < 16 ? 1 : 2);
        var grund = new[] { 0, 5, 2, 7, 1, 4, 3, 6 }[nummer]; // gemischt, damit gleiche Stücke nicht nebeneinander stehen
        var spalte = grund % 4;
        var reihe = grund / 4;
        return (-3.0f + 1.9f * spalte + 0.12f * MathF.Sin(k * 2.3f), -(8.65f + 1.1f * reihe) + 0.08f * MathF.Cos(k * 1.7f), lage, nummer);
    }

    /// <summary>Höhe des Stegdecks im Steg-Modell (Kai 0,25 m, Deck 0,45 m darunter).</summary>
    private const float StegDeck = -0.2f;

    /// <summary>Legt einmal je Zierschiff die Stapel an: Kopien der Ladungsstücke des Schiffs als Kinder des Stegs (mit Seil und Haken des Krans).</summary>
    private void ZierStapelBauen(SchiffAnsicht ansicht, Hafenplatz hafen)
    {
        if (_zierStapel.Count > 0 && IsInstanceValid(_zierStapel[0].Knoten)) return;
        if (!_stege.TryGetValue(hafen.Kennung, out var steg) || !IsInstanceValid(steg)) return;
        _zierStapel.Clear();
        KranHakenBauen(steg);
        var voll = ansicht.Ladung.Length;
        var hoehe = new float[8];
        for (var k = 0; k < voll; k++)
        {
            var quelle = ansicht.Ladung[voll - 1 - k]; // das k-te abgeladene Stück ist Ladung_(voll-k)
            if (quelle.Duplicate() is not MeshInstance3D kopie || quelle.Mesh is null) continue;
            var box = quelle.Mesh.GetAabb();
            var (x, z, lage, grund) = ZierStapelPlatz(k);
            kopie.Name = "Stapel_" + (k + 1);
            kopie.Visible = false;
            steg.AddChild(kopie);
            var oben = lage == 0 ? 0 : hoehe[grund];
            // leicht verdreht, wie hingestellt: um die Mitte des Stücks drehen
            var dreh = 0.22f * MathF.Sin(k * 1.9f + 0.5f);
            var stueck = new ZierStueck
            {
                Knoten = kopie, Unten = new Vector3(x, StegDeck + oben, z), Dreh = dreh, Hoehe = box.Size.Y, NetzUnten = box.Position.Y,
                Mitte = new Vector3(box.Position.X + box.Size.X / 2, 0, box.Position.Z + box.Size.Z / 2),
            };
            StueckSetzen(stueck, stueck.Unten, dreh);
            hoehe[grund] = oben + box.Size.Y;
            _zierStapel.Add(stueck);
        }
        _zierStapelGezeigt = -1;
    }

    private void ZierStapelZeigen(int anzahl)
    {
        if (anzahl == _zierStapelGezeigt || _zierStapel.Count == 0) return;
        _zierStapelGezeigt = anzahl;
        for (var i = 0; i < _zierStapel.Count; i++)
            if (IsInstanceValid(_zierStapel[i].Knoten)) _zierStapel[i].Knoten.Visible = i < anzahl;
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
