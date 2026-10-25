using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die sichtbare Ratssitzung im Ratssaal (PR #202) – Teil der <c>partial class Stadtansicht</c>. Die Oberfläche (<see cref="RatssitzungAnzeige"/>) führt
/// durch die Punkte; die Hauptszene reicht jeden Schritt hierher weiter. Die Darstellung liest nur, was ihr gezeigt wird.
///
/// Am Ratstisch sitzt je besetztem Amt eine Figur (die Figuren aus Blender, Stadtansicht.Figuren.cs): Häuser in ihrer Farbe, Ratsherren der Stadt in Ratsrot,
/// der Bürgermeister mit goldenem Barett auf dem erhöhten Stuhl. Über jedem Platz steht klein der Name (beim Bürgermeister mit Amt; die Ämter nennt die Tafel),
/// der eigene in Gold; wer gerade abstimmt, dessen Name leuchtet hell. Der Antragsteller tritt zum Reden vor (Bewegung „Reden“ am Rednerplatz); sitzt er selbst
/// im Rat, ist sein Stuhl so lange leer. Stimmt ein Sitz, steht seine Stimme groß vor ihm auf dem Tisch
/// (Ja grün, Nein rot, Enthaltung grau, bei Wahlen der Name des Kandidaten in Gold) und die Figur zeigt sie: Wer dafür ist oder einen Kandidaten wählt,
/// hebt die rechte Hand, wer dagegen ist, schüttelt den Kopf. Die Pose kommt nicht aus dem Modell: Das Spiel dreht nach der Sitzbewegung Oberarm,
/// Unterarm bzw. Kopf des Skeletts selbst (<see cref="GesteAnwenden"/>).
///
/// Die Plätze kommen vorerst aus dem Ratssaal aus Grundkörpern (Stadtansicht.Sonderbauten.cs, Hufeisentisch mit sieben Stühlen). Während der Sitzung
/// sind dessen Figuren aus, die Eingabe im Raum ruht (kein Raumwechsel, kein Hinausgehen), und zwei Kerzenlichter erhellen den Tisch –
/// die Sitzung ist um fünf Uhr früh, nach dem Jahreswechsel.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Was eine Figur am Ratstisch gerade zeigt.</summary>
    private enum Geste { Keine, HandHoch, Kopfschuetteln }

    /// <summary>Ein Platz am Ratstisch während der Sitzung: Figur, Schild mit Amt und Name, die Stimme darüber.</summary>
    private sealed class SitzungsPlatz
    {
        public required Amt Amt { get; init; }
        public required Node3D Halter { get; init; }
        public required Label3D Schild { get; init; }
        public required Label3D Stimme { get; init; }
        /// <summary>Wer hier sitzt (Name des Inhabers) – ändert er sich durch eine Wahl, wird die Figur neu gebaut.</summary>
        public string Inhaber { get; set; } = "";
        /// <summary>Das Haus des Inhabers (null: ein Ratsherr der Stadt oder niemand).</summary>
        public Konto? Haus { get; set; }
        public Figur? Figur { get; set; }
        public Skeleton3D? Skelett { get; set; }
        public Geste Geste { get; set; }
        /// <summary>Sekunden seit Beginn der Geste (die Hand geht in einer halben Sekunde hoch, der Kopf schüttelt zweimal).</summary>
        public double GestenZeit { get; set; }
    }

    /// <summary>Die laufende Sitzung im Bild (null: keine).</summary>
    private sealed class Sitzungsbild
    {
        public required Node3D Wurzel { get; init; }
        public List<SitzungsPlatz> Plaetze { get; } = new();
        public Node3D? Redner { get; set; }
        public Figur? RednerFigur { get; set; }
        /// <summary>Der Platz des Antragstellers, solange er vorn redet (sein Stuhl ist dann leer).</summary>
        public SitzungsPlatz? RednerPlatz { get; set; }
        public SitzungsPlatz? AmZug { get; set; }
    }

    /// <summary>Die Rathaus-Ansicht und die sieben Sitze des Ratssaals (Lage relativ zur Gebäudemitte, Bürgermeister zuerst) – gesetzt in RathausBauen.</summary>
    private SonderbauAnsicht? _rathaus;
    private Arbeitsplatz[] _ratssaalSitze = Array.Empty<Arbeitsplatz>();
    private Sitzungsbild? _ratssitzung;

    /// <summary>Wo der Antragsteller redet (relativ zur Gebäudemitte): vorn rechts zwischen den Tischreihen, Blick schräg zum Tisch, im Profil zur Kamera.</summary>
    private static readonly Vector3 RednerOrt = new(1.0f, 0, 1.7f);
    private static readonly float RednerDrehung = -2.1f;

    /// <summary>Kamera während der Sitzung (relativ zur Mitte des Ratssaals): vorn erhöht, Blick schräg hinab auf den Tisch.</summary>
    private static readonly Vector3 SitzungsAuge = new(0, 5.4f, 9.4f), SitzungsZiel = new(0, -0.2f, 0.2f);

    private static readonly Color StimmeJa = new(0.55f, 0.92f, 0.5f);
    private static readonly Color StimmeNein = new(1f, 0.45f, 0.4f);
    private static readonly Color StimmeEnthalten = new(0.78f, 0.78f, 0.78f);
    private static readonly Color StimmeKandidat = new(0.98f, 0.84f, 0.42f);

    private static readonly Color AmZugFarbe = new(1f, 1f, 0.7f);

    /// <summary>Farbe des Namens über einem Platz: der eigene in Gold, die übrigen gedämpft.</summary>
    private Color SchildFarbe(SitzungsPlatz platz) => platz.Haus is not null && platz.Haus == _spielerKonto ? StimmeKandidat : new Color(0.78f, 0.74f, 0.66f);

    /// <summary>Läuft gerade eine Sitzung im Ratssaal? (Dann ruht die Eingabe im Raum.)</summary>
    public bool RatssitzungImBild => _ratssitzung is not null;

    /// <summary>
    /// Geht in den Ratssaal und setzt die Ratsherren an den Tisch. Gibt false zurück, wenn die Stadt kein Rathaus hat (dann zeigt die Oberfläche die Sitzung ohne Bild).
    /// </summary>
    public bool RatssitzungBetreten()
    {
        if (_rathaus?.Innen is not { } innen || _simulation?.Wirtschaft is not { } w) return false;
        if (_ratssitzung is null)
        {
            var wurzel = new Node3D { Name = "Ratssitzung" };
            innen.AddChild(wurzel);
            _ratssitzung = new Sitzungsbild { Wurzel = wurzel };
            // Kerzenlicht über dem Tisch: links und rechts des Bürgermeisters, warm
            foreach (var x in new[] { -1.4f, 1.4f })
                wurzel.AddChild(new OmniLight3D
                {
                    LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 1.6f, OmniRange = 7.5f,
                    Position = _rathaus.Mitte + new Vector3(x, 2.4f, -1.2f),
                });
            var aemter = SitzOrdnung(w);
            for (var i = 0; i < aemter.Count && i < _ratssaalSitze.Length; i++)
            {
                var sitz = _ratssaalSitze[i];
                var halter = new Node3D { Position = _rathaus.Mitte + sitz.Ort, Rotation = new Vector3(0, sitz.Drehung + Mathf.Pi, 0) };
                wurzel.AddChild(halter);
                var vorn = new Vector3(Mathf.Sin(sitz.Drehung + Mathf.Pi), 0, Mathf.Cos(sitz.Drehung + Mathf.Pi)); // Blickrichtung der Figur, zum Tisch
                var platz = new SitzungsPlatz
                {
                    Amt = aemter[i], Halter = halter,
                    Schild = SitzungsSchild(wurzel, _rathaus.Mitte + sitz.Ort + new Vector3(0, i == 0 ? 2.1f : 1.75f, 0), 28),
                    // Die Stimme liegt vor dem Platz auf dem Tisch: Die Stühle an den Seiten stehen im Bild dicht hintereinander, ihre Köpfe fast übereinander
                    Stimme = SitzungsSchild(wurzel, _rathaus.Mitte + sitz.Ort + vorn * 0.8f + new Vector3(0, 1.05f, 0), 44),
                };
                _ratssitzung.Plaetze.Add(platz);
            }
            PlaetzeBesetzen(w, null);
        }
        if (!InnenansichtBetreten(_rathaus.Kennung)) return false;
        RaumWaehlen(RaumRat);
        // Steiler von oben als sonst im Ratssaal: Der Tisch rückt in die obere Bildhälfte, unten steht die Tafel der Sitzung
        var mitte = innen.ToGlobal(_rathaus.Mitte);
        _raumKamera.LookAtFromPosition(mitte + SitzungsAuge, mitte + SitzungsZiel, Vector3.Up);
        return true;
    }

    /// <summary>Ende der Sitzung: Figuren und Lichter weg, zurück in die Stadt.</summary>
    public void RatssitzungVerlassen()
    {
        if (_ratssitzung is null) return;
        KnotenEntfernen(_ratssitzung.Wurzel);
        _ratssitzung = null;
        if (InnenKennung == _rathaus?.Kennung) InnenansichtVerlassen();
    }

    /// <summary>Ein neuer Punkt: alle Stimmen und Gesten zurück, wer neu gewählt ist, sitzt jetzt am Tisch; der Antragsteller tritt vor.</summary>
    public void RatssitzungPunkt(Sitzungspunkt punkt)
    {
        if (_ratssitzung is not { } s || _simulation?.Wirtschaft is not { } w) return;
        PlaetzeBesetzen(w, punkt);
        foreach (var platz in s.Plaetze)
        {
            GesteSetzen(platz, Geste.Keine);
            // Bei einer Wahl sitzen die Kandidaten mit am Tisch, stimmen aber nicht mit
            var kandidat = punkt.Thema == Sitzungsthema.Wahl
                && punkt.Kandidaten.Any(k => (k.Haus is not null && k.Haus == platz.Haus) || k.Name == platz.Inhaber);
            StimmeSchreiben(platz, kandidat ? "kandidiert" : "", StimmeEnthalten);
        }
        RatssitzungAmZug(null);
        RednerSetzen(punkt.Antragsteller);
    }

    /// <summary>Der Redner geht an seinen Platz zurück (die Abstimmung beginnt).</summary>
    public void RatssitzungRednerFort() => RednerSetzen(null);

    /// <summary>Hebt den Sitz hervor, der gerade abstimmt (null: keiner): sein Name leuchtet hell auf.</summary>
    public void RatssitzungAmZug(Amt? amt)
    {
        if (_ratssitzung is not { } s) return;
        var neu = amt is null ? null : s.Plaetze.FirstOrDefault(p => p.Amt == amt);
        if (s.AmZug is { } alt) alt.Schild.Modulate = SchildFarbe(alt);
        s.AmZug = neu;
        if (neu is not null) neu.Schild.Modulate = AmZugFarbe;
    }

    /// <summary>Zeigt eine Stimme über ihrem Sitz und lässt die Figur sie zeigen (Hand heben, Kopf schütteln, bei Enthaltung nichts).</summary>
    public void RatssitzungStimme(Sitzungsstimme stimme)
    {
        if (_ratssitzung?.Plaetze.FirstOrDefault(p => p.Amt == stimme.Sitz) is not { } platz) return;
        if (stimme.Kandidat is { } kandidat)
        {
            StimmeSchreiben(platz, Kurzname(kandidat) + (stimme.Los ? " (Los)" : ""), StimmeKandidat);
            GesteSetzen(platz, Geste.HandHoch);
            return;
        }
        var (text, farbe, geste) = stimme.Ja switch
        {
            true => ("Ja", StimmeJa, Geste.HandHoch),
            false => ("Nein", StimmeNein, Geste.Kopfschuetteln),
            _ => ("Enthaltung", StimmeEnthalten, Geste.Keine),
        };
        StimmeSchreiben(platz, text, farbe);
        GesteSetzen(platz, geste);
    }

    /// <summary>
    /// Der Punkt ist abgehalten: Nach einer Wahl sitzt der Gewählte gleich auf seinem Stuhl, vor ihm steht „gewählt“.
    /// </summary>
    public void RatssitzungErgebnis(Sitzungspunkt punkt)
    {
        if (_ratssitzung is not { } s || _simulation?.Wirtschaft is not { } w || punkt.Thema != Sitzungsthema.Wahl || punkt.Amt is null) return;
        PlaetzeBesetzen(w, punkt);
        if (s.Plaetze.FirstOrDefault(p => p.Amt == punkt.Amt) is { } platz && punkt.Ergebnis?.Gewaehlt is { } gewaehlt)
        {
            GesteSetzen(platz, Geste.Keine);
            StimmeSchreiben(platz, "gewählt: " + Kurzname(gewaehlt), StimmeKandidat);
        }
    }

    /// <summary>Die Ämter in der Reihenfolge der Stühle (Bürgermeister zuerst) – wie die Figuren außerhalb der Sitzung.</summary>
    private static List<Amt> SitzOrdnung(Stadtwirtschaft w) => w.Rat.Aemter.OrderByDescending(a => a.Stufe).ToList();

    /// <summary>
    /// Wer zu diesem Punkt auf dem Stuhl eines Amtes sitzt (Name null: niemand): nach einer Wahl der Gewählte, sonst wer dort abstimmt, bei einer Wahl der bisherige
    /// Inhaber unter den Kandidaten, sonst der heutige Inhaber. So stimmt auch die Wiederholung einer älteren Sitzung, in der andere auf den Stühlen saßen.
    /// </summary>
    private static (string? Name, Konto? Haus) Besetzung(Amt amt, Sitzungspunkt? punkt)
    {
        if (punkt?.Amt == amt && punkt.Ergebnis?.Gewaehlt is { } gewaehlt)
            return (gewaehlt, punkt.Ergebnis.Zaehlung.FirstOrDefault(z => z.Kandidat == gewaehlt)?.Haus);
        if (punkt?.Stimmen.FirstOrDefault(x => x.Sitz == amt) is { } stimme) return (stimme.Name, stimme.Haus);
        if (punkt?.Amt == amt && punkt.Kandidaten.FirstOrDefault(k => k.IstAmtsinhaber) is { } bisher) return (bisher.Name, bisher.Haus);
        return amt.IstBesetzt ? (amt.Inhaber, amt.Konto) : (null, null);
    }

    /// <summary>Baut die Figur eines Platzes neu, wenn dort jemand anderes sitzt (nach einer Wahl, in der Wiederholung), und beschriftet ihn.</summary>
    private void PlaetzeBesetzen(Stadtwirtschaft w, Sitzungspunkt? punkt)
    {
        if (_ratssitzung is not { } s) return;
        for (var i = 0; i < s.Plaetze.Count; i++)
        {
            var platz = s.Plaetze[i];
            var amt = platz.Amt;
            var (name, haus) = Besetzung(amt, punkt);
            if (platz.Inhaber == (name ?? "") && (platz.Figur is not null || name is null)) continue;
            platz.Inhaber = name ?? "";
            platz.Haus = haus;
            StimmeSchreiben(platz, "", StimmeEnthalten); // ein neuer Inhaber hat noch nichts gesagt
            platz.Geste = Geste.Keine;
            if (platz.Figur is { } alt)
            {
                alt.Modell.QueueFree();
                platz.Figur = null;
                platz.Skelett = null;
            }
            // eine Zeile: die Stühle an den Seiten stehen im Bild dicht hintereinander
            platz.Schild.Text = name is null ? $"{amt.Name} (unbesetzt)" : i == 0 ? $"{amt.Name} {name}" : name;
            platz.Schild.Modulate = s.AmZug == platz ? AmZugFarbe : SchildFarbe(platz);
            if (name is null) continue;
            if (FigurEinsetzen(platz.Halter, RatsherrAussehen(w, name, haus, i == 0), 700 + i) is not { } figur) continue;
            figur.Modell.Position = new Vector3(0, SitzKniehoehe - figur.Kniehoehe, 0); // die Hüfte auf dem Stuhl
            BewegungSetzen(figur, "Sitzen", 0.9f + i * 0.04f);
            platz.Figur = figur;
            platz.Skelett = figur.Modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
        }
    }

    /// <summary>
    /// Aussehen eines Ratsmitglieds: ein Haus in seiner Farbe (Volk und Geschlecht des Oberhaupts), ein Ratsherr der Stadt in Ratsrot (Volk aus den Daten).
    /// Alle tragen ein dunkles Barett, der Bürgermeister ein goldenes. Haar und Beinkleid folgen fest aus dem Namen.
    /// </summary>
    private FigurAussehen RatsherrAussehen(Stadtwirtschaft w, string name, Konto? haus, bool buergermeister)
    {
        var oberhaupt = haus is not null ? w.Familien.Von(haus)?.Oberhaupt : null;
        var volk = oberhaupt?.Volk ?? (haus is not null ? Volk.Mensch : w.Rat.Ratsherren.FirstOrDefault(r => r.Name == name)?.Daten.Volk ?? Volk.Mensch);
        var frau = oberhaupt?.Geschlecht == Geschlecht.Frau;
        var gewand = haus is not null ? Besitzerfarbe(haus) : Ratsrot;
        var nummer = name.Sum(c => c) % 97; // fest je Name (string.GetHashCode wechselt mit jedem Start)
        var haare = Haarfarben(volk);
        return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[nummer % Beinkleidfarben.Length], HautVon(volk),
            nummer % 3 == 0 ? GrauesHaar : haare[nummer % haare.Length], buergermeister ? Gold : new Color(0.12f, 0.10f, 0.10f), Kopfbedeckung.Barett);
    }

    /// <summary>Stellt den Antragsteller an den Rednerplatz (null: niemand redet, sein Stuhl ist wieder besetzt).</summary>
    private void RednerSetzen(Konto? antragsteller)
    {
        if (_ratssitzung is not { } s || _rathaus is null || _simulation?.Wirtschaft is not { } w) return;
        if (s.RednerPlatz?.Figur is { } zurueck) zurueck.Modell.Visible = true;
        s.RednerPlatz = null;
        if (s.Redner is not null)
        {
            KnotenEntfernen(s.Redner);
            s.Redner = null;
            s.RednerFigur = null;
        }
        if (antragsteller is null) return;
        var halter = new Node3D { Position = _rathaus.Mitte + RednerOrt, Rotation = new Vector3(0, RednerDrehung, 0) };
        s.Wurzel.AddChild(halter);
        var oberhaupt = w.Familien.Von(antragsteller)?.Oberhaupt;
        var volk = oberhaupt?.Volk ?? Volk.Mensch;
        var haare = Haarfarben(volk);
        var aussehen = new FigurAussehen(volk, oberhaupt?.Geschlecht == Geschlecht.Frau, Besitzerfarbe(antragsteller), Beinkleidfarben[2], HautVon(volk),
            haare[0], new Color(0.12f, 0.10f, 0.10f), Kopfbedeckung.Barett);
        if (FigurEinsetzen(halter, aussehen, 790) is { } figur)
        {
            BewegungSetzen(figur, "Reden");
            s.RednerFigur = figur;
        }
        s.Redner = halter;
        // Sitzt er selbst im Rat, ist sein Stuhl leer, solange er vorn steht
        s.RednerPlatz = s.Plaetze.FirstOrDefault(p => p.Haus == antragsteller);
        if (s.RednerPlatz?.Figur is { } sitzend) sitzend.Modell.Visible = false;
    }

    /// <summary>Schreibt die Stimme vor den Platz; Namen (Wahlen) etwas kleiner als Ja und Nein – die Stühle hinten stehen nur 1,2 m auseinander.</summary>
    private static void StimmeSchreiben(SitzungsPlatz platz, string text, Color farbe)
    {
        platz.Stimme.Text = text;
        platz.Stimme.Modulate = farbe;
        platz.Stimme.FontSize = farbe == StimmeKandidat ? 32 : 44;
    }

    /// <summary>Der Kandidat kurz: „Haus Hammerschmid“ → „Hammerschmid“, „Bertram Kesselflick“ → „Kesselflick“ (die Tafel nennt den ganzen Namen).</summary>
    private static string Kurzname(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? name;

    private void GesteSetzen(SitzungsPlatz platz, Geste geste)
    {
        if (platz.Geste == geste) return;
        platz.Geste = geste;
        platz.GestenZeit = 0;
    }

    /// <summary>Ein Schild im Ratssaal: immer zur Kamera gedreht, vor allem anderen (wie die Schilder der Räume, aber ohne Zoom der Stadt).</summary>
    private static Label3D SitzungsSchild(Node3D eltern, Vector3 ort, int groesse)
    {
        var schild = new Label3D
        {
            Position = ort, FontSize = groesse, PixelSize = 0.0042f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            OutlineSize = 12, OutlineModulate = new Color(0.08f, 0.07f, 0.06f),
            Font = Schriften.Ueberschrift,
        };
        eltern.AddChild(schild);
        return schild;
    }

    /// <summary>
    /// Jedes Bild während der Sitzung: Die Figuren atmen im Sitzen (die Sitzung läuft auch, wenn die Partie steht), danach setzt das Spiel die Geste
    /// über die Pose der Bewegung.
    /// </summary>
    private void RatssitzungAktualisieren(double delta)
    {
        if (_ratssitzung is not { } s || _raum?.Kennung != RaumRat) return;
        foreach (var platz in s.Plaetze)
        {
            if (platz.Figur is not { } figur) continue;
            FigurBewegen(figur, delta);
            platz.GestenZeit += delta;
            if (platz.Skelett is { } skelett && platz.Geste != Geste.Keine) GesteAnwenden(skelett, platz.Geste, platz.GestenZeit);
        }
        if (s.RednerFigur is { } redner) FigurBewegen(redner, delta);
    }

    /// <summary>
    /// Setzt die Geste über die Pose der Sitzbewegung (im Raum des Skeletts, vorn ist +Z, oben +Y). Hand heben: Oberarm und Unterarm rechts zeigen in einer
    /// halben Sekunde nach oben und etwas nach vorn. Kopfschütteln: der Kopf dreht zweimal je Sekunde um die Hochachse hin und her, nach zwei Sekunden ruhig.
    /// Welche Achse eines Knochens entlang des Knochens zeigt, liest das Spiel aus der Ruhelage (Arme hängen dort nach unten) – so hängt es nicht davon ab,
    /// wie der Export die Knochenachsen legt.
    /// </summary>
    private static void GesteAnwenden(Skeleton3D skelett, Geste geste, double zeit)
    {
        if (geste == Geste.HandHoch)
        {
            var anteil = Mathf.SmoothStep(0f, 1f, (float)Math.Min(1, zeit / 0.5));
            var ziel = new Vector3(-0.12f, 1f, 0.3f).Normalized();
            foreach (var name in new[] { "Oberarm_R", "Unterarm_R" })
            {
                var knochen = skelett.FindBone(name);
                if (knochen < 0) continue;
                var ruhe = skelett.GetBoneGlobalRest(knochen).Basis.Orthonormalized();
                var achse = (ruhe.Inverse() * Vector3.Down).Normalized(); // in der Ruhelage hängt der Arm nach unten
                var lage = skelett.GetBoneGlobalPose(knochen);
                var jetzt = (lage.Basis.Orthonormalized() * achse).Normalized();
                var richtung = jetzt.Slerp(ziel, anteil).Normalized();
                if (jetzt.Cross(richtung).LengthSquared() < 1e-8f) continue;
                lage.Basis = new Basis(new Quaternion(jetzt, richtung)) * lage.Basis;
                skelett.SetBoneGlobalPose(knochen, lage);
            }
        }
        else if (geste == Geste.Kopfschuetteln)
        {
            var knochen = skelett.FindBone("Kopf");
            if (knochen < 0) return;
            var ausschlag = zeit < 2.0 ? Mathf.Sin((float)zeit * Mathf.Tau * 2) * 0.45f * (1 - (float)zeit / 2.4f) : 0;
            var lage = skelett.GetBoneGlobalPose(knochen);
            lage.Basis = new Basis(Vector3.Up, ausschlag) * lage.Basis;
            skelett.SetBoneGlobalPose(knochen, lage);
        }
    }
}
