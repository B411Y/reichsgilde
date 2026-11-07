using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Kranladen (Teil der <c>partial class Stadtansicht</c>): Der Tretradkran auf dem Hafensteg hebt die Ladung des Zierschiffs (Entladen, Stufe 2).
///
/// Ein Hub hebt ein <b>Bündel</b> von <see cref="Zierfahrplan.HubGroesse"/> Stücken (drei Ladungsnummern des Schiffsmodells: zwei unten, eins obenauf): Der Kran
/// schwenkt das Kranhaus, bis der Ausleger über dem Bündel steht, das Seil senkt sich, das Bündel hängt am Haken (Kopien aus dem Stapel, die Schiffsstücke sind
/// aus), der Haken hebt, schwenkt über den Stapelplatz auf dem Steg und setzt ab. Die Rückfracht läuft umgekehrt. Weniger, dafür größere Hübe, damit das
/// Kranhaus langsam und weich schwenken kann (ein Drittel der früheren Geschwindigkeit, Anfahren und Abbremsen) und die Hübe trotzdem in die Liegezeit passen.
/// Das Kranhaus schwenkt mit gleichmäßig gedehntem Winkel (sanft an und aus), nicht auf der Geraden des Hakens.
///
/// Das Tretrad dreht sich <b>nur</b>, solange sich der Haken hebt oder senkt: sein Winkel ist die Seillänge (Hakenhöhe) geteilt durch den Trommelradius; Schwenken
/// und Stillstand lassen es stehen, Heben und Senken drehen es entgegengesetzt, in dem Tempo, in dem das Seil läuft. Das Seil hängt an einer Laufkatze am Ausleger,
/// deren Abstand von der Drehachse dem des Hakens entspricht (das Schiff liegt für den Kran etwas längs versetzt, siehe <see cref="ZierLaengs"/>).
///
/// Der Mast der Schiffe steht immer (Treidelmast vorn im Schiff, die Ladung liegt dahinter): Der Ausleger kommt ihm und der Takelage nie nahe, der Kran
/// arbeitet nur am Laderaum.
///
/// Alles ist eine reine Funktion des Liegeanteils: Bei hoher Spielgeschwindigkeit werden die Hübe einfach schneller durchlaufen, übersprungene Hübe zeigen
/// beim nächsten Bild das richtige Standbild (Stück auf dem Stapel bzw. an Bord), ohne Sprung am Haken.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>So weit liegt das Schiff für den Zierbesuch längs zum Steg versetzt (m, in Stegrichtung zum Kran hin): Der Ausleger reicht dann an alle Stücke.</summary>
    private const float ZierLaengs = 5.0f;

    /// <summary>Höhe des Hakens oben (Steg-Raum, m): über den Stapeln und der Ladung, unter der Laufkatze am Ausleger.</summary>
    private const float KranHochY = 4.4f;

    /// <summary>Das Bündel hängt so weit (m) unter dem Haken (Oberkante des höchsten Stücks).</summary>
    private const float KranGehaenge = 0.5f;

    /// <summary>Radius der Seiltrommel (m): Das Tretrad dreht sich um Seillänge geteilt durch diesen Wert (Bogenmaß je Meter Seil = 1 / Radius).</summary>
    private const float KranTrommelRadius = 1.0f;

    /// <summary>
    /// Anteile eines Hubs (0…1): Haken steigt über den Stapel, Schwenken zum Bündel, Senken, Heben, Schwenken zum Ziel, Absetzen. Das Heben des Bündels
    /// (<see cref="Hafenarbeit.KranHebtBei"/>) und das Absetzen (Ende des Hubs) kennt der Kern für die Träger; die Schwenkphasen sind je gut ein Drittel des Hubs lang.
    /// </summary>
    private const double KranAufBis = 0.06, KranAnfahrtBis = 0.33, KranSenkenBis = Hafenarbeit.KranHebtBei, KranHebenBis = 0.50, KranSchwenkenBis = 0.84;

    private Node3D? _kranHaus, _kranRad, _kranHaken, _kranGehaenge, _kranKatze;
    private MeshInstance3D? _kranSeil;
    private readonly List<MeshInstance3D> _kranTaue = new();
    private Vector3 _kranAchse;

    private sealed class ZierStueck
    {
        public Node3D Knoten = null!;
        public Vector3 Unten;      // Mitte der Unterseite im Ruhezustand auf dem Stapel (Steg-Raum)
        public float Dreh;
        public Vector3 Mitte;      // Mitte des Stücks im Netz (x, 0, z)
        public float NetzUnten;    // Unterkante des Netzes
        public float Hoehe;
        public Vector3 Oben => Unten + new Vector3(0, Hoehe, 0);
    }

    private (double Start, double Rate, ulong Seit)? _zierMinuteFest;

    /// <summary>Startoption <c>--zierminute=<i>Minute</i>[,<i>Rate</i>]</c>: Das Zierschiff zeigt diese Spielminute (seit Spielbeginn, 04:00 Uhr = 0), danach <i>Rate</i> Minuten je Sekunde; nur für Bildschirmfotos.</summary>
    public void ZierMinuteVorgeben(double start, double rate) => _zierMinuteFest = (start, rate, 0);

    private double ZierMinuteGezeigt(double minute)
    {
        if (_zierMinuteFest is not { } f) return minute;
        if (f.Seit == 0) _zierMinuteFest = f = (f.Start, f.Rate, Time.GetTicksMsec()); // die Zeit läuft ab dem ersten gezeigten Bild
        return f.Start + f.Rate * (Time.GetTicksMsec() - f.Seit) / 1000.0;
    }

    private static float Glatt(double x) { var t = (float)Math.Clamp(x, 0, 1); return t * t * (3 - 2 * t); }

    /// <summary>Noch weicher als <see cref="Glatt"/>: Anfahren und Abbremsen ohne Ruck (die Beschleunigung ist an den Enden null).</summary>
    private static float Glatter(double x) { var t = (float)Math.Clamp(x, 0, 1); return t * t * t * (t * (t * 6 - 15) + 10); }

    private static void StueckSetzen(ZierStueck s, Vector3 unten, float dreh)
    {
        s.Knoten.Rotation = new Vector3(0, dreh, 0);
        s.Knoten.Position = new Vector3(unten.X, unten.Y - s.NetzUnten, unten.Z) - new Basis(Vector3.Up, dreh) * s.Mitte;
    }

    /// <summary>Hängt Seil, Haken und Laufkatze in den Steg (einmal je Steg) und merkt sich Kranhaus und Tretrad.</summary>
    private void KranHakenBauen(Node3D steg)
    {
        if (_kranHaken is not null && IsInstanceValid(_kranHaken) && _kranHaken.GetParent() == steg) return;
        _kranHaus = steg.FindChild("Kranhaus", true, false) as Node3D;
        _kranRad = steg.FindChild("Tretrad", true, false) as Node3D;
        if (_kranHaus is null) return;
        _kranAchse = steg.ToLocal(_kranHaus.GlobalPosition);
        Material Stoff(Color c, float rau) => new StandardMaterial3D { AlbedoColor = c, Roughness = rau };
        var seil = Stoff(new Color(0.55f, 0.44f, 0.28f), 0.95f);
        var eisen = Stoff(new Color(0.16f, 0.16f, 0.18f), 0.6f);
        MeshInstance3D Kasten(Node3D eltern, string name, Vector3 groesse, Material m)
        {
            var mi = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = groesse }, MaterialOverride = m };
            eltern.AddChild(mi);
            return mi;
        }
        _kranHaken = new Node3D { Name = "Kranhaken" };
        steg.AddChild(_kranHaken);
        Kasten(_kranHaken, "Block", new Vector3(0.26f, 0.4f, 0.2f), eisen).Position = new Vector3(0, -0.2f, 0);
        _kranSeil = new MeshInstance3D { Name = "Seil", Mesh = new CylinderMesh { Height = 1f, TopRadius = 0.05f, BottomRadius = 0.05f, RadialSegments = 6, Rings = 1 }, MaterialOverride = seil };
        _kranHaken.AddChild(_kranSeil);
        _kranKatze = new Node3D { Name = "Laufkatze" };
        _kranHaken.AddChild(_kranKatze);
        Kasten(_kranKatze, "Rolle", new Vector3(0.34f, 0.16f, 0.34f), eisen);
        // Gehänge: je Stück am Haken zwei Taue vom Haken zu den oberen Ecken (Länge und Lage setzt KranTaueSetzen; nur sichtbar, solange etwas hängt)
        _kranGehaenge = new Node3D { Name = "Gehaenge", Visible = false };
        _kranHaken.AddChild(_kranGehaenge);
        _kranTaue.Clear();
        for (var i = 0; i < 2 * Zierfahrplan.HubGroesse; i++)
            _kranTaue.Add(Kasten(_kranGehaenge, "Tau", new Vector3(0.05f, 1f, 0.05f), seil));
        KranHakenSetzen(KranRuheOrt(), null);
    }

    /// <summary>Ruhestellung des Hakens: oben über dem Stapelplatz.</summary>
    private static Vector3 KranRuheOrt() => new(1.0f, KranHochY, -9.3f);

    /// <summary>
    /// Setzt Haken, Seil, Kranhausdrehung und Tretrad; <paramref name="lasten"/> sind die Stücke am Haken (Oberkante in der Mitte und Drehung, Steg-Raum): Das
    /// Gehänge spannt zwei Taue je Stück vom Block zu den oberen Ecken.
    /// </summary>
    private void KranHakenSetzen(Vector3 haken, IReadOnlyList<(Vector3 Oben, float Dreh)>? lasten)
    {
        if (_kranHaken is null || !IsInstanceValid(_kranHaken) || _kranHaus is null) return;
        _kranHaken.Position = haken;
        var dx = haken.X - _kranAchse.X;
        var dz = haken.Z - _kranAchse.Z;
        var r = Mathf.Max(Mathf.Sqrt(dx * dx + dz * dz), 0.5f);
        _kranHaus.Rotation = new Vector3(0, Mathf.Atan2(-dx, -dz), 0);
        // Ausleger (Modell): Fuß bei −1,4 m, Spitze bei 11,2 m von der Achse, Unterkante 4,15…5,5 m über dem Sockel; die Laufkatze sitzt knapp darunter
        var rb = Mathf.Clamp(r, -1.2f, 11.0f);
        var katzeY = _kranAchse.Y + 3.95f + (rb + 1.4f) / 12.6f * 1.35f;
        var laenge = Mathf.Max(katzeY - haken.Y, 0.05f);
        _kranKatze!.Position = new Vector3(0, katzeY - haken.Y, 0);
        _kranSeil!.Scale = new Vector3(1, laenge, 1);
        _kranSeil.Position = new Vector3(0, laenge / 2, 0);
        // Das Tretrad dreht sich nur mit dem Seil: Winkel = Seillänge unter dem obersten Haken / Trommelradius (Heben und Senken entgegengesetzt, Schwenken: Stillstand)
        if (_kranRad is not null) _kranRad.Rotation = new Vector3((KranHochY - haken.Y) / KranTrommelRadius, _kranRad.Rotation.Y, _kranRad.Rotation.Z);
        KranTaueSetzen(haken, lasten);
    }

    /// <summary>Zwei Taue je Stück vom Block unter dem Haken zu den oberen Ecken des Stücks (in der Längsrichtung des Stücks, 0,3 m von der Mitte).</summary>
    private void KranTaueSetzen(Vector3 haken, IReadOnlyList<(Vector3 Oben, float Dreh)>? lasten)
    {
        if (_kranGehaenge is null) return;
        var n = lasten?.Count ?? 0;
        if (_kranGehaenge.Visible != n > 0) _kranGehaenge.Visible = n > 0;
        for (var i = 0; i < _kranTaue.Count; i++)
        {
            var tau = _kranTaue[i];
            var stueck = i / 2;
            if (stueck >= n) { if (tau.Visible) tau.Visible = false; continue; }
            tau.Visible = true;
            var (oben, dreh) = lasten![stueck];
            var ecke = oben + new Basis(Vector3.Up, dreh) * new Vector3(i % 2 == 0 ? -0.3f : 0.3f, 0, 0);
            var von = new Vector3(0, -0.4f, 0);
            var d = ecke - haken - von;
            var l = Mathf.Max(d.Length(), 0.05f);
            tau.Quaternion = new Quaternion(Vector3.Up, d / l);
            tau.Scale = new Vector3(1, l, 1);
            tau.Position = von + d / 2;
        }
    }

    /// <summary>Kein Schiff in der Liegephase: Haken in Ruhe, nichts hängt; <paramref name="stapel"/> Stücke stehen am Steg.</summary>
    private void KranRuhe(int stapel = 0, Hafenplatz? hafen = null)
    {
        // Steht ein Stapel (zwischen den Besuchen), muss er gebaut sein, auch wenn gerade kein Schiff zu sehen ist
        if (stapel > 0 && hafen is not null) ZierStapelBauen(ZierSchiffHolen(true), hafen);
        if (_kranHaken is null || !IsInstanceValid(_kranHaken)) return;
        if (_kranHaken.Position != KranRuheOrt() || _kranGehaenge!.Visible) KranHakenSetzen(KranRuheOrt(), null);
        KranLastenAus(); // Stadtansicht.Hafenarbeit.cs
        ZierStapelZeigen(stapel);
    }

    private static Vector3 KranWeg(Vector3 von, Vector3 nach, double u) => von.Lerp(nach, Glatt(u));

    /// <summary>
    /// Schwenken: Der Winkel um die Drehachse des Krans wird weich von <paramref name="von"/> nach <paramref name="nach"/> gedehnt (Anfahren und Abbremsen), der
    /// Abstand von der Achse und die Höhe gleiten mit. So schwenkt das Kranhaus gleichmäßig; auf der Geraden zwischen zwei Punkten wäre es nahe der Achse viel schneller.
    /// </summary>
    private Vector3 KranSchwenk(Vector3 von, Vector3 nach, double u)
    {
        var t = Glatter(u);
        var (a0, r0) = KranPolar(von);
        var (a1, r1) = KranPolar(nach);
        var a = a0 + Mathf.Wrap(a1 - a0, -Mathf.Pi, Mathf.Pi) * t;
        var r = Mathf.Lerp(r0, r1, t);
        return new Vector3(_kranAchse.X - r * Mathf.Sin(a), Mathf.Lerp(von.Y, nach.Y, t), _kranAchse.Z - r * Mathf.Cos(a));
    }

    /// <summary>Drehwinkel des Kranhauses (wie in <see cref="KranHakenSetzen"/>) und Abstand von der Drehachse eines Punktes.</summary>
    private (float Winkel, float Abstand) KranPolar(Vector3 p)
    {
        var dx = p.X - _kranAchse.X;
        var dz = p.Z - _kranAchse.Z;
        return (Mathf.Atan2(-dx, -dz), Mathf.Sqrt(dx * dx + dz * dz));
    }

    private static Vector3 KranHoch(Vector3 o) => new(o.X, KranHochY, o.Z);

    private static Vector3 KranTief(Vector3 o) => o + new Vector3(0, KranGehaenge, 0);

    /// <summary>Oberkante (Mitte) des Schiffsstücks <paramref name="i"/> im Steg-Raum und seine Drehung gegen den Steg.</summary>
    private (Vector3 Oben, float Dreh) KranSchiffStueck(SchiffAnsicht ansicht, Node3D steg, int i)
    {
        var q = ansicht.Ladung[i];
        var box = q.Mesh?.GetAabb() ?? new Aabb();
        var mitteOben = new Vector3(box.Position.X + box.Size.X / 2, box.End.Y, box.Position.Z + box.Size.Z / 2);
        var oben = steg.ToLocal(q.GlobalTransform * mitteOben);
        var b = steg.GlobalTransform.Basis.Inverse() * q.GlobalTransform.Basis;
        return (oben, b.GetEuler().Y);
    }

    /// <summary>Ein Bündel von bis zu <see cref="Zierfahrplan.HubGroesse"/> Stücken, die ein Hub zusammen hebt: Plätze am Stapel, Lage an der Quelle und am Ziel.</summary>
    private sealed class KranBuendel
    {
        public int[] Platz = Array.Empty<int>();          // Stapelplatz je Stück (die Kopie am Haken zeigt ein Stück dieses Platzes)
        public Vector3[] QUnten = Array.Empty<Vector3>(), ZUnten = Array.Empty<Vector3>(); // Mitte der Unterseite an Quelle und Ziel
        public float[] QDreh = Array.Empty<float>(), ZDreh = Array.Empty<float>(), Hoehe = Array.Empty<float>();
        public Vector3 QBezug, ZBezug;                    // Mitte (x, z) der Stücke und Oberkante des höchsten: Der Haken hängt KranGehaenge darüber
    }

    /// <summary>Der Stapelplatz, auf dem der Hub das Stück <paramref name="h"/> absetzt (Entladen) bzw. von dem er es hebt (Rückfracht); mit Träger aus dem Plan des Kerns.</summary>
    private static int KranPlatzVon(int h, bool nieder, int voll, HafenPlan? plan)
    {
        var p = plan is null ? (nieder ? h : voll - 1 - h) : plan.KranPlatz[Math.Min(h, plan.KranPlatz.Length - 1)];
        return Math.Clamp(p, 0, voll - 1);
    }

    private KranBuendel KranBuendelBauen(SchiffAnsicht ansicht, Node3D steg, int hub, bool nieder, int voll, HafenPlan? plan)
    {
        var g = Zierfahrplan.HubGroesse;
        var n = Math.Min(g, voll - hub * g);
        var b = new KranBuendel
        {
            Platz = new int[n], QUnten = new Vector3[n], ZUnten = new Vector3[n], QDreh = new float[n], ZDreh = new float[n], Hoehe = new float[n],
        };
        for (var i = 0; i < n; i++)
        {
            var h = hub * g + i;
            var schiffNr = nieder ? voll - 1 - h : h;
            b.Platz[i] = KranPlatzVon(h, nieder, voll, plan);
            var stapel = _zierStapel[b.Platz[i]];
            var (oben, dreh) = KranSchiffStueck(ansicht, steg, schiffNr);
            var schiffHoehe = ansicht.Ladung[schiffNr].Mesh?.GetAabb().Size.Y ?? stapel.Hoehe;
            var schiffUnten = oben - new Vector3(0, schiffHoehe, 0);
            b.Hoehe[i] = stapel.Hoehe;
            (b.QUnten[i], b.QDreh[i], b.ZUnten[i], b.ZDreh[i]) = nieder ? (schiffUnten, dreh, stapel.Unten, stapel.Dreh) : (stapel.Unten, stapel.Dreh, schiffUnten, dreh);
        }
        b.QBezug = KranBezug(b.QUnten, b.Hoehe);
        b.ZBezug = KranBezug(b.ZUnten, b.Hoehe);
        return b;
    }

    private static Vector3 KranBezug(Vector3[] unten, float[] hoehe)
    {
        var x = 0f; var z = 0f; var y = float.MinValue;
        for (var i = 0; i < unten.Length; i++)
        {
            x += unten[i].X / unten.Length;
            z += unten[i].Z / unten.Length;
            y = Mathf.Max(y, unten[i].Y + hoehe[i]);
        }
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Haken im Hub zur Zeit <paramref name="u"/> (0…1): von <paramref name="start"/> hoch, zum Bündel schwenken, senken, mit dem Bündel hoch, zum Ziel schwenken,
    /// absetzen. <paramref name="haengt"/> sagt, ob das Bündel am Haken hängt, <paramref name="schwenk"/> wie weit sich seine Anordnung von der Quelle zum Ziel
    /// gelöst hat (0…1, im Takt des Schwenkens).
    /// </summary>
    private Vector3 KranHubHaken(KranBuendel b, Vector3 start, double u, out bool haengt, out float schwenk)
    {
        haengt = u >= KranSenkenBis && u < 1.0;
        schwenk = Glatter((u - KranHebenBis) / (KranSchwenkenBis - KranHebenBis));
        var qHoch = KranHoch(b.QBezug);
        if (u < KranAufBis) return KranWeg(start, KranHoch(start), u / KranAufBis);
        if (u < KranAnfahrtBis) return KranSchwenk(KranHoch(start), qHoch, (u - KranAufBis) / (KranAnfahrtBis - KranAufBis));
        if (u < KranSenkenBis) return KranWeg(qHoch, KranTief(b.QBezug), (u - KranAnfahrtBis) / (KranSenkenBis - KranAnfahrtBis));
        if (u < KranHebenBis) return KranWeg(KranTief(b.QBezug), qHoch, (u - KranSenkenBis) / (KranHebenBis - KranSenkenBis));
        if (u < KranSchwenkenBis) return KranSchwenk(qHoch, KranHoch(b.ZBezug), (u - KranHebenBis) / (KranSchwenkenBis - KranHebenBis));
        return KranWeg(KranHoch(b.ZBezug), KranTief(b.ZBezug), (u - KranSchwenkenBis) / (1.0 - KranSchwenkenBis));
    }

    /// <summary>
    /// Liegephase (mit <paramref name="plan"/> der Hafenarbeit ist der Stapel Puffer, sonst nicht): setzt Kran, Last und Stapel; gibt zurück, wie viele Stücke
    /// (die niedrigsten Nummern) an Bord sichtbar sind. Niederländer: alles vom Schiff auf den Stapel (Entladen); Oberländer: alles vom Stapel aufs Schiff.
    /// </summary>
    private int KranLadung(SchiffAnsicht ansicht, double a, bool nieder, int voll, HafenPlan? plan, double minute)
    {
        var steg = _zierStapel.Count > 0 && IsInstanceValid(_zierStapel[0].Knoten) ? _zierStapel[0].Knoten.GetParent() as Node3D : null;
        if (steg is null || _zierStapel.Count < voll || _kranHaken is null || !IsInstanceValid(_kranHaken)) return voll;
        var g = Zierfahrplan.HubGroesse;
        var huebe = (voll + g - 1) / g;
        var von = nieder ? ZierEntladeAb : ZierOberLadeAb;
        var bis = nieder ? ZierNiederEntladeBis : ZierOberLadeBis;
        var breite = (bis - von) / huebe;

        var haken = KranRuheOrt();
        KranBuendel? hub = null;
        var haengt = false;
        var schwenk = 0f;
        var fertig = 0; // Stücke aller früheren Hübe
        if (a >= von && a < bis)
        {
            var x = (a - von) / breite;
            var nr = Math.Min((int)x, huebe - 1);
            hub = KranBuendelBauen(ansicht, steg, nr, nieder, voll, plan);
            var start = nr == 0 ? KranRuheOrt() : KranTief(KranBuendelBauen(ansicht, steg, nr - 1, nieder, voll, plan).ZBezug);
            haken = KranHubHaken(hub, start, x - nr, out haengt, out schwenk);
            fertig = nr * g;
        }
        else if (a >= bis)
        {
            // Nach dem letzten Hub: Der Haken steigt über dem zuletzt abgesetzten Bündel und kehrt vor dem Ablegen in die Ruhe zurück
            var letzt = KranBuendelBauen(ansicht, steg, huebe - 1, nieder, voll, plan).ZBezug;
            haken = a < 0.93 ? KranWeg(KranTief(letzt), KranHoch(letzt), (a - bis) / 0.03) : KranSchwenk(KranHoch(letzt), KranRuheOrt(), (a - 0.93) / 0.06);
            fertig = voll;
        }
        var dabei = haengt && hub is not null ? hub.Platz.Length : 0; // Stücke am Haken

        // Stapel: mit Träger nach dem Plan des Kerns, sonst nach dem Stand der Hübe (Niederländer: abgesetzt; Oberländer: noch nicht gehoben)
        for (var k = 0; k < _zierStapel.Count; k++)
        {
            var sichtbar = plan is not null ? k < plan.Schicht.StapelUm(minute) : nieder ? k < fertig : voll - 1 - k >= fertig + dabei;
            if (_zierStapel[k].Knoten.Visible != sichtbar) _zierStapel[k].Knoten.Visible = sichtbar;
        }
        _zierStapelGezeigt = -1;

        // Das Bündel am Haken: Kopien der Stapelstücke, angeordnet wie an der Quelle und beim Schwenken ins Gefüge des Ziels übergehend
        var lasten = new List<(Vector3 Oben, float Dreh)>(g);
        var haelt = new HashSet<int>();
        if (haengt && hub is not null)
        {
            var q = hub.QBezug;
            var z = hub.ZBezug;
            for (var i = 0; i < hub.Platz.Length; i++)
            {
                if (KranLast(hub.Platz[i]) is not { } last) continue;
                var lage = (hub.QUnten[i] - q).Lerp(hub.ZUnten[i] - z, schwenk);
                var yaw = Mathf.LerpAngle(hub.QDreh[i], hub.ZDreh[i], schwenk);
                var unten = haken - new Vector3(0, KranGehaenge, 0) + lage;
                last.Knoten.Visible = true;
                StueckSetzen(last, unten, yaw);
                haelt.Add(hub.Platz[i]);
                lasten.Add((unten + new Vector3(0, last.Hoehe, 0), yaw));
            }
        }
        for (var k = 0; k < _kranLasten.Count; k++)
            if (!haelt.Contains(k) && IsInstanceValid(_kranLasten[k].Knoten) && _kranLasten[k].Knoten.Visible) _kranLasten[k].Knoten.Visible = false;
        KranHakenSetzen(haken, lasten);
        return nieder ? voll - fertig - dabei : fertig;
    }
}
