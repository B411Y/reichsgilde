using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Kranladen (Teil der <c>partial class Stadtansicht</c>): Der Tretradkran auf dem Hafensteg hebt die Ladung des Zierschiffs (Entladen, Stufe 2).
///
/// Jedes Stück der Ladung ist ein Hub: Der Kran schwenkt das Kranhaus, bis der Ausleger über dem Stück steht (Haken oben), das Seil senkt sich, das Stück
/// hängt am Haken (die Kopie aus dem Stapel am Steg, das Schiffsstück wird ausgeblendet), der Haken hebt, schwenkt über den Stapelplatz auf dem Steg und
/// setzt ab. Die Rückfracht läuft umgekehrt: vom Stapel (Stücke, die der Träger nicht wegträgt) auf das Schiff. Das Tretrad dreht sich nur, solange sich der
/// Haken hebt oder senkt (Winkel = Hakenhöhe, ohne eigenen Zähler). Das Seil hängt an einer Laufkatze am Ausleger, deren Abstand von der Drehachse dem
/// des Hakens entspricht (das Schiff liegt für den Kran etwas längs versetzt, siehe <see cref="ZierLaengs"/>).
///
/// Alles ist eine reine Funktion des Liegeanteils: Bei hoher Spielgeschwindigkeit werden die Hübe einfach schneller durchlaufen, übersprungene Hübe zeigen
/// beim nächsten Bild das richtige Standbild (Stück auf dem Stapel bzw. an Bord), ohne Sprung am Haken.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>So weit liegt das Schiff für den Zierbesuch längs zum Steg versetzt (m, in Stegrichtung zum Kran hin): Der Ausleger reicht dann an alle Stücke.</summary>
    private const float ZierLaengs = 5.0f;

    /// <summary>Höhe des Hakens oben (Steg-Raum, m): über den Stapeln und der Ladung.</summary>
    private const float KranHochY = 4.0f;

    /// <summary>Das Stück hängt so weit (m) unter dem Haken.</summary>
    private const float KranGehaenge = 0.7f;

    /// <summary>Anteile eines Hubs (0…1): Anfahrt und Heben aus der Ruhe, Senken, Heben mit Last, Schwenken, Absetzen.</summary>
    private const double KranAnfahrtBis = 0.25, KranSenkenBis = Hafenarbeit.KranHebtBei, KranHebenBis = 0.58, KranSchwenkenBis = 0.82;

    private Node3D? _kranHaus, _kranRad, _kranHaken, _kranGehaenge, _kranKatze;
    private MeshInstance3D? _kranSeil;
    private Vector3 _kranAchse;
    private int _kranGehalten = -1;

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

    // ── Mast legen ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Der Ausleger des Krans schwenkt auf Höhe des Mastes und käme nicht an ihm vorbei. Wie bei den Oberländern unter Brücken üblich, wird der Mast im Hafen
    // gelegt: Gleich nach dem Anlegen (vor dem ersten Hub, 0,5–4,5 % der Liegezeit) kippt er um seinen Fuß nach vorn aufs Deck, mit Rah, gerefftem Segel und
    // Wimpel; die Wanten und Stagen sind dann abgenommen. Vor dem Ablegen (94–98 %) wird er wieder aufgerichtet und aufgetakelt.
    private const double MastLegenAb = 0.005, MastLegenBis = 0.045, MastAufAb = 0.94, MastAufBis = 0.98;
    private SchiffAnsicht? _mastAnsicht;
    private Node3D? _mastKnoten, _takelage;
    private Node3D[] _mastMit = System.Array.Empty<Node3D>();
    private float _mastWinkel = -1;

    private void ZierMastSetzen(SchiffAnsicht ansicht, Besuchsphase phase, double a)
    {
        if (_mastAnsicht != ansicht)
        {
            _mastAnsicht = ansicht;
            var w = ansicht.Wurzel;
            _mastKnoten = w.FindChild("Mast", true, false) as Node3D;
            _takelage = w.FindChild("Takelage", true, false) as Node3D;
            _mastMit = new[] { _mastKnoten, ansicht.SegelGerefft, w.FindChild("Fahne", true, false) as Node3D }.OfType<Node3D>().ToArray();
            _mastWinkel = -1;
        }
        if (_mastKnoten is null) return;
        var t = phase != Besuchsphase.Liegen ? 0f : Math.Min(Glatt((a - MastLegenAb) / (MastLegenBis - MastLegenAb)), 1 - Glatt((a - MastAufAb) / (MastAufBis - MastAufAb)));
        if (Math.Abs(t - _mastWinkel) < 1e-4f) return;
        _mastWinkel = t;
        var drehen = new Transform3D(new Basis(Vector3.Back, -t * Mathf.Pi / 2), Vector3.Zero);
        var fuss = _mastAnsicht!.MastFuss;
        var um = new Transform3D(Basis.Identity, fuss) * drehen * new Transform3D(Basis.Identity, -fuss);
        foreach (var n in _mastMit) n.Transform = um;
        if (_takelage is not null) _takelage.Visible = t <= 0f;
    }

    private static float Glatt(double x) { var t = (float)Math.Clamp(x, 0, 1); return t * t * (3 - 2 * t); }

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
        // Gehänge: zwei Taue vom Haken zu den oberen Ecken der Last (nur sichtbar, solange etwas hängt)
        _kranGehaenge = new Node3D { Name = "Gehaenge", Visible = false };
        _kranHaken.AddChild(_kranGehaenge);
        foreach (var s in new[] { -1f, 1f })
        {
            var tau = Kasten(_kranGehaenge, "Tau", new Vector3(0.05f, 0.75f, 0.05f), seil);
            tau.Position = new Vector3(s * 0.2f, -0.43f, 0);
            tau.Rotation = new Vector3(0, 0, s * 0.43f);
        }
        _kranGehalten = -1;
        KranHakenSetzen(KranRuheOrt(), 0);
    }

    /// <summary>Ruhestellung des Hakens: oben über dem Stapelplatz.</summary>
    private static Vector3 KranRuheOrt() => new(1.0f, KranHochY, -9.3f);

    /// <summary>Setzt Haken, Seil, Kranhausdrehung und Tretrad; <paramref name="dreh"/> = Drehung der Last (nur fürs Gehänge).</summary>
    private void KranHakenSetzen(Vector3 haken, float dreh)
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
        _kranGehaenge!.Rotation = new Vector3(0, dreh, 0);
        if (_kranRad is not null) _kranRad.Rotation = new Vector3((KranHochY - haken.Y) * 0.9f, _kranRad.Rotation.Y, _kranRad.Rotation.Z);
    }

    /// <summary>Kein Schiff in der Liegephase: Haken in Ruhe, nichts hängt; <paramref name="stapel"/> Stücke stehen am Steg.</summary>
    private void KranRuhe(int stapel = 0, Hafenplatz? hafen = null)
    {
        // Steht ein Stapel (zwischen den Besuchen), muss er gebaut sein, auch wenn gerade kein Schiff zu sehen ist
        if (stapel > 0 && hafen is not null) ZierStapelBauen(ZierSchiffHolen(true), hafen);
        if (_kranHaken is null || !IsInstanceValid(_kranHaken)) return;
        if (_kranHaken.Position != KranRuheOrt() || _kranGehaenge!.Visible)
        {
            _kranGehaenge!.Visible = false;
            KranHakenSetzen(KranRuheOrt(), 0);
        }
        _kranGehalten = -1;
        ZierStapelZeigen(stapel);
    }

    private Vector3 KranWeg(Vector3 von, Vector3 nach, double u) => von.Lerp(nach, Glatt(u));

    /// <summary>
    /// Ein Hub zur Zeit <paramref name="u"/> (0…1): Haken von <paramref name="start"/> hoch über die Quelle, runter, mit dem Stück hoch, über das Ziel, runter.
    /// <paramref name="haengt"/> sagt, ob das Stück am Haken hängt, <paramref name="dreht"/> wie weit seine Drehung von Quelle zu Ziel ist (0…1).
    /// </summary>
    private Vector3 KranHub(Vector3 start, Vector3 quelleOben, Vector3 zielOben, double u, out bool haengt, out float dreht)
    {
        var obenQ = new Vector3(quelleOben.X, KranHochY, quelleOben.Z);
        var untenQ = quelleOben + new Vector3(0, KranGehaenge, 0);
        var obenZ = new Vector3(zielOben.X, KranHochY, zielOben.Z);
        var untenZ = zielOben + new Vector3(0, KranGehaenge, 0);
        haengt = u >= KranSenkenBis && u < 1.0;
        dreht = Glatt((u - KranHebenBis) / (KranSchwenkenBis - KranHebenBis));
        if (u < KranAnfahrtBis) return KranWeg(start, obenQ, u / KranAnfahrtBis);
        if (u < KranSenkenBis) return KranWeg(obenQ, untenQ, (u - KranAnfahrtBis) / (KranSenkenBis - KranAnfahrtBis));
        if (u < KranHebenBis) return KranWeg(untenQ, obenQ, (u - KranSenkenBis) / (KranHebenBis - KranSenkenBis));
        if (u < KranSchwenkenBis) return KranWeg(obenQ, obenZ, (u - KranHebenBis) / (KranSchwenkenBis - KranHebenBis));
        return KranWeg(obenZ, untenZ, (u - KranSchwenkenBis) / (1.0 - KranSchwenkenBis));
    }

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

    /// <summary>
    /// Liegephase: setzt Kran, Last, Stapel; gibt zurück, wie viele Stücke (die niedrigsten Nummern) an Bord sichtbar sind.
    /// </summary>
    private int KranLadung(SchiffAnsicht ansicht, double a, bool nieder, int voll)
    {
        var steg = _zierStapel.Count > 0 && IsInstanceValid(_zierStapel[0].Knoten) ? _zierStapel[0].Knoten.GetParent() as Node3D : null;
        if (steg is null || _zierStapel.Count < voll || _kranHaken is null || !IsInstanceValid(_kranHaken)) return voll;
        // Niederländer: alles vom Schiff auf den Stapel (Entladen); Oberländer: alles vom Stapel aufs Schiff (Laden, Stapelrecht)
        var rueck = nieder ? 0 : voll;
        var entladen = nieder ? voll : 0;
        var w = (ZierNiederEntladeBis - ZierEntladeAb) / voll;
        var wr = (ZierOberLadeBis - ZierOberLadeAb) / voll;
        var ende1 = ZierEntladeAb + entladen * w;
        var ende2 = ZierOberLadeAb + rueck * wr;

        // Quelle und Ziel des Hubs: Entladen i = voll−1−h (Schiff → Stapel h), Rückfracht i = j (Stapel voll−1−j → Schiff)
        (Vector3 Oben, float Dreh) Schiff(int i) => KranSchiffStueck(ansicht, steg, i);
        (Vector3 Oben, float Dreh) Stapel(int k) => (_zierStapel[k].Oben, _zierStapel[k].Dreh);
        Vector3 Hoch(Vector3 o) => new(o.X, KranHochY, o.Z);
        Vector3 Tief(Vector3 o) => o + new Vector3(0, KranGehaenge, 0);

        Vector3 haken = KranRuheOrt();
        var gehalten = -1;
        var dreh0 = 0f;
        var dreh1 = 0f;
        var dreht = 0f;
        var haengt = false;
        int anBord;
        int abgesetzt = 0, gehoben = 0;          // Entladen: Stapel fertig / vom Schiff gehoben
        int rueckGehoben = 0, rueckAbgesetzt = 0; // Rückfracht: vom Stapel gehoben / aufs Schiff gesetzt
        if (a < (nieder ? ZierEntladeAb : ZierOberLadeAb))
        {
            anBord = nieder ? voll : 0;
            abgesetzt = nieder ? 0 : voll;
            gehoben = nieder ? 0 : voll;
        }
        else if (nieder && a < ende1)
        {
            var x = (a - ZierEntladeAb) / w;
            var h = Math.Min((int)x, voll - 1);
            var u = x - h;
            var q = Schiff(voll - 1 - h);
            var z = Stapel(h);
            var start = h == 0 ? KranRuheOrt() : Tief(Stapel(h - 1).Oben);
            haken = KranHub(start, q.Oben, z.Oben, u, out haengt, out dreht);
            (dreh0, dreh1) = (q.Dreh, z.Dreh);
            if (haengt) gehalten = h;
            abgesetzt = h;
            gehoben = haengt ? h + 1 : h;
            anBord = voll - gehoben;
        }
        else if (nieder)
        {
            // Der Haken steigt über dem zuletzt abgesetzten Stück und kehrt vor dem Ablegen in die Ruhe zurück
            haken = KranWeg(Tief(Stapel(voll - 1).Oben), Hoch(Stapel(voll - 1).Oben), (a - ende1) / 0.02).Lerp(KranRuheOrt(), Glatt((a - 0.93) / 0.06));
            abgesetzt = voll;
            gehoben = voll;
            anBord = 0;
        }
        else if (a < ende2)
        {
            var x = (a - ZierOberLadeAb) / wr;
            var j = Math.Min((int)x, rueck - 1);
            var u = x - j;
            var k = voll - 1 - j;
            var q = Stapel(k);
            var z = Schiff(j);
            var start = j == 0 ? KranRuheOrt() : Tief(Schiff(j - 1).Oben);
            haken = KranHub(start, q.Oben, z.Oben, u, out haengt, out dreht);
            (dreh0, dreh1) = (q.Dreh, z.Dreh);
            if (haengt) gehalten = k;
            abgesetzt = voll;
            gehoben = voll;
            rueckGehoben = haengt ? j + 1 : j;
            rueckAbgesetzt = j;
            anBord = rueckAbgesetzt;
        }
        else
        {
            // Nach der Rückfracht: Haken steigt, kehrt vor dem Ablegen in die Ruhe zurück
            var letzt = Schiff(rueck - 1).Oben;
            var rueckkehr = Glatt((a - 0.93) / 0.06);
            haken = KranWeg(Tief(letzt), Hoch(letzt), (a - ende2) / 0.02).Lerp(KranRuheOrt(), rueckkehr);
            abgesetzt = voll;
            gehoben = voll;
            rueckGehoben = rueck;
            rueckAbgesetzt = rueck;
            anBord = rueck;
        }

        // Stücke am Steg: abgesetzte und (Rückfracht) noch nicht gehobene sind da, das gehobene hängt am Haken
        var nichtRueck = voll - rueck;
        for (var k = 0; k < voll; k++)
        {
            var s = _zierStapel[k];
            bool sichtbar;
            if (nieder && a < ende1) sichtbar = k < abgesetzt;
            else if (k < nichtRueck) sichtbar = true; // Niederländer: der Stapel bleibt stehen (bis der Oberländer ihn aufnimmt)
            else sichtbar = voll - 1 - k >= rueckGehoben; // Rückfracht-Stück bleibt bis zum Heben
            if (k == gehalten) sichtbar = true;
            if (s.Knoten.Visible != sichtbar) s.Knoten.Visible = sichtbar;
        }
        // ein Stück, das eben noch am Haken hing, steht wieder (genau) auf seinem Platz
        if (_kranGehalten >= 0 && _kranGehalten != gehalten)
        {
            var alt = _zierStapel[_kranGehalten];
            StueckSetzen(alt, alt.Unten, alt.Dreh);
        }
        _kranGehalten = gehalten;
        if (gehalten >= 0)
        {
            var yaw = Mathf.LerpAngle(dreh0, dreh1, dreht);
            StueckSetzen(_zierStapel[gehalten], haken - new Vector3(0, KranGehaenge + _zierStapel[gehalten].Hoehe, 0), yaw);
            _kranGehaenge!.Rotation = new Vector3(0, yaw, 0);
        }
        _kranGehaenge!.Visible = gehalten >= 0;
        KranHakenSetzen(haken, gehalten >= 0 ? Mathf.LerpAngle(dreh0, dreh1, dreht) : 0);
        _zierStapelGezeigt = -1;
        return anBord;
    }
}
