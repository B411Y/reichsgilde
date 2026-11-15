using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die sichtbare Ratssitzung im Ratssaal (PR #202, Szene Teil 1 14.11.2026) – Teil der <c>partial class Stadtansicht</c>. Die Oberfläche (<see cref="RatssitzungAnzeige"/>) führt
/// durch die Punkte; die Hauptszene reicht jeden Schritt hierher weiter. Die Darstellung liest nur, was ihr gezeigt wird.
///
/// Die Auszählung sieht man im Saal: Alle, die dafür sind (oder einen Kandidaten wählen), heben <i>gleichzeitig</i> die Hand, die Neinstimmen schütteln den Kopf;
/// dann zählt der Stadtschreiber am Pult die Hände einzeln, über jeder erscheint eine Zahl (<see cref="RatssitzungGezaehlt"/>), sein Zwischenstand steht über ihm,
/// und die Oberfläche führt das Ratsbuch. Die Plätze sind nach Ast geordnet (<see cref="SitzOrdnung"/>: je Zweig des Amtsbaums eine Tischseite), Bewerber ohne Ratssitz
/// sitzen auf der Zuhörerbank (<see cref="ZuhoererSetzen"/>, gebaut in <see cref="RatssaalModellBauen"/>). Alle Figuren der Sitzung entstehen über
/// <see cref="SitzungsFigur"/>: Das ist die eine Stelle, an der der Figuren-Baukasten sie später austauscht.
///
/// Am Ratstisch sitzt je besetztem Amt eine Figur (die Figuren aus Blender, Stadtansicht.Figuren.cs): Häuser in ihrer Farbe, Ratsherren der Stadt in Ratsrot,
/// der Bürgermeister mit goldenem Barett auf dem erhöhten Stuhl. Über jedem Platz steht klein der Name (beim Bürgermeister mit Amt; die Ämter nennt die Tafel),
/// der eigene in Gold; wer gerade abstimmt, dessen Name leuchtet hell. Der Antragsteller tritt zum Reden vor (Bewegung „Reden“ am Rednerplatz); sitzt er selbst
/// im Rat, ist sein Stuhl so lange leer. Stimmt ein Sitz, steht seine Stimme groß vor ihm auf dem Tisch
/// (Ja grün, Nein rot, Enthaltung grau, bei Wahlen der Name des Kandidaten in Gold) und die Figur zeigt sie: Wer dafür ist oder einen Kandidaten wählt,
/// hebt die rechte Hand, wer dagegen ist, schüttelt den Kopf. Die Pose kommt nicht aus dem Modell: Das Spiel dreht nach der Sitzbewegung Oberarm,
/// Unterarm bzw. Kopf des Skeletts selbst (<see cref="GesteAnwenden"/>).
///
/// Die Plätze kommen aus der Ratshalle des Rathauses (Anker im Modell, Stadtansicht.Rathausneubau.cs, auch in Köln). Während der Sitzung
/// sind die Figuren der Arbeitszeit aus, die Eingabe im Raum ruht (kein Raumwechsel, kein Hinausgehen), und die Kerzen des Leuchters brennen –
/// die Sitzung ist am Jahresende, vor Sonnenaufgang, und danach kommt der Jahresbericht.
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
        /// <summary>Die Zahl des Schreibers über der Hand, wenn sie gezählt ist.</summary>
        public required Label3D Zahl { get; init; }
        /// <summary>Sitzhöhe des Stuhls (Meter): Die Hüfte der Figur kommt darauf.</summary>
        public float Sitzhoehe { get; init; } = 0.46f;
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
        /// <summary>Knoten der Sitzung an den Ankern des Modells (Halter der Figuren) – sie gehen mit der Sitzung.</summary>
        public List<Node3D> AnAnkern { get; } = new();
        public List<SitzungsPlatz> Plaetze { get; } = new();
        public Node3D? Redner { get; set; }
        public Figur? RednerFigur { get; set; }
        /// <summary>Der Platz des Antragstellers, solange er vorn redet (sein Stuhl ist dann leer).</summary>
        public SitzungsPlatz? RednerPlatz { get; set; }
        public SitzungsPlatz? AmZug { get; set; }
        public Figur? SchreiberFigur { get; set; }
        /// <summary>Der Zwischenstand der Zählung über dem Schreiber.</summary>
        public Label3D? SchreiberZahl { get; set; }
        /// <summary>Die Bewerber auf der Zuhörerbank (ohne Sitz am Tisch).</summary>
        public List<Zuhoerer> Zuhoerer { get; } = new();
    }

    /// <summary>Ein Bewerber auf der Zuhörerbank: Halter an der Bank, Figur, Namensschild.</summary>
    private sealed class Zuhoerer
    {
        public required Node3D Halter { get; init; }
        public required string Name { get; init; }
        public required Label3D Schild { get; init; }
        public Figur? Figur { get; set; }
    }

    /// <summary>Die Rathaus-Ansicht – gesetzt in RathausBauen.</summary>
    private SonderbauAnsicht? _rathaus;
    private Sitzungsbild? _ratssitzung;

    /// <summary>Ein Stuhl der Sitzung: Elternknoten und Lage darin (die Figur blickt mit +Z zum Tisch), Sitzhöhe in Metern.</summary>
    private sealed record Stuhl(Node3D Eltern, Transform3D Lage, float Sitzhoehe);

    /// <summary>
    /// Der Saal der Sitzung: die Ratshalle des Rathauses (<see cref="RathausNeuRatssaal"/>). Stühle: Bürgermeister auf dem Podest, dann
    /// <c>Anker_Sitz_&lt;n&gt;</c> nach „platz“ (Rang); dazu Redner, Schreiber, Zuhörerbank, die Rolle <c>Antrag</c> und die Blickpunkte.
    /// </summary>
    private sealed class Ratssaalmodell
    {
        public required Node3D Wurzel { get; init; }
        public List<Stuhl> Stuehle { get; } = new();
        public Stuhl? Redner { get; set; }
        public Stuhl? Schreiber { get; set; }
        /// <summary>Die Plätze der Zuhörerbank (Anker, Blick zum Tisch): Bewerber ohne Ratssitz warten dort auf ihre Wahl.</summary>
        public List<Stuhl> Bank { get; } = new();
        public Node3D? Antrag { get; set; }
        /// <summary>Blickpunkt vom Podest zurück (<c>Blick_Rat_R2</c>): der Redner von vorn.</summary>
        public Node3D? RednerBlick { get; set; }
        /// <summary>Kamera der Sitzung aus den Ankern <c>Blick_Sitzung_Auge</c>/<c>_Ziel</c>.</summary>
        public Node3D? Auge { get; set; }
        public Node3D? Ziel { get; set; }
    }

    private Ratssaalmodell? _ratssaal;

    /// <summary>Die Anker blicken mit −Z zum Tisch (wie die Plätze der Kirche), die Figuren mit +Z: um 180° gedreht einsetzen.</summary>
    private static readonly Transform3D AmAnker = new(new Basis(Vector3.Up, Mathf.Pi), Vector3.Zero);

    /// <summary>Im Ratssaal: der Blick über den Tisch – oder, solange der Antragsteller redet, vom Podest zurück auf ihn.</summary>
    private void SaalKamera(bool redner)
    {
        if (_ratssaal is not { } saal) return;
        redner &= saal.RednerBlick is not null;
        if (redner)
            _raumKamera.GlobalTransform = new Transform3D(saal.RednerBlick!.GlobalTransform.Basis.Orthonormalized(), saal.RednerBlick.GlobalPosition);
        else if (saal.Auge is not null && saal.Ziel is not null)
            _raumKamera.LookAtFromPosition(saal.Auge.GlobalPosition, saal.Ziel.GlobalPosition, Vector3.Up); // Ratshalle (Stadtansicht.Rathausneubau.cs)
        // Vom Podest aus stünden die Schilder der nahen Plätze riesig im Bild: Solange der Redner spricht, sind sie aus
        if (_ratssitzung is { } s)
            foreach (var platz in s.Plaetze) platz.Schild.Visible = platz.Stimme.Visible = platz.Zahl.Visible = !redner;
    }


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
    /// Der Saal der Sitzung (aus RathausBauen): die Ratshalle im Rathaus mit ihren Ankern (<see cref="RathausNeuRatssaal"/>). Fehlt sie, gibt es
    /// keine Sitzung im Bild (die Oberfläche zeigt sie dann ohne Bild).
    /// </summary>
    private void RatssaalModellBauen(SonderbauAnsicht a)
    {
        if (!RathausNeuRatssaal(a)) GD.PushWarning("Rathaus: die Ratshalle oder ihre Anker fehlen – die Ratssitzung läuft ohne Bild.");
        else GD.Print($"[rathausneu] Saal der Sitzung: Ratshalle, {_ratssaal!.Stuehle.Count} Stühle, Bank {_ratssaal.Bank.Count}, Redner {(_ratssaal.Redner is null ? "fehlt" : "da")}, Schreiber {(_ratssaal.Schreiber is null ? "fehlt" : "da")}, Kamera {(_ratssaal.Auge is null ? "fehlt" : "da")}");
    }

    /// <summary>Eine Kommazahl aus den „extras“ eines Knotens, sonst der Vorgabewert.</summary>
    private static float ExtraZahlKomma(Node knoten, string schluessel, float vorgabe)
    {
        var extras = Extras(knoten);
        return extras is not null && extras.ContainsKey(schluessel) ? (float)extras[schluessel].AsDouble() : vorgabe;
    }

    /// <summary>Die Stühle der Sitzung, Bürgermeister zuerst (Anker der Ratshalle).</summary>
    private List<Stuhl> SitzungsStuehle() => _ratssaal?.Stuehle ?? new List<Stuhl>();

    /// <summary>
    /// Geht in den Ratssaal und setzt die Ratsherren an den Tisch. Gibt false zurück, wenn die Stadt kein Rathaus mit Ratshalle hat (dann zeigt die Oberfläche die Sitzung ohne Bild).
    /// </summary>
    public bool RatssitzungBetreten()
    {
        if (_rathaus?.Innen is not { } innen || _simulation?.Wirtschaft is not { } w || _ratssaal is null) return false;
        if (_ratssitzung is null)
        {
            var wurzel = new Node3D { Name = "Ratssitzung" };
            innen.AddChild(wurzel);
            _ratssitzung = new Sitzungsbild { Wurzel = wurzel };
            var aemter = SitzOrdnung(w);
            var stuehle = SitzungsStuehle();
            for (var i = 0; i < aemter.Count && i < stuehle.Count; i++)
            {
                var halter = new Node3D { Transform = stuehle[i].Lage };
                stuehle[i].Eltern.AddChild(halter);
                if (stuehle[i].Eltern != wurzel) _ratssitzung.AnAnkern.Add(halter);
                var platz = new SitzungsPlatz
                {
                    Amt = aemter[i], Halter = halter, Sitzhoehe = stuehle[i].Sitzhoehe,
                    Schild = SitzungsSchild(halter, new Vector3(0, i == 0 ? 2.1f : 1.75f, 0), 28),
                    // Die Stimme liegt vor dem Platz auf dem Tisch: Die Stühle an den Seiten stehen im Bild dicht hintereinander, ihre Köpfe fast übereinander
                    Stimme = SitzungsSchild(halter, new Vector3(0, 1.05f, 0.74f), 44),
                    Zahl = SitzungsSchild(halter, new Vector3(0, 1.42f, 0), 40),
                };
                platz.Zahl.Modulate = StimmeKandidat;
                if (_ratssaal is { } saal && i > 0)
                {
                    // Am langen Tisch des Modells liegt die Stimme bis an die Mitte und läuft zur eigenen Seite hin aus – die gegenüber überdeckt sie so nie
                    var links = saal.Wurzel.ToLocal(stuehle[i].Eltern.GlobalPosition).X < 0;
                    platz.Stimme.HorizontalAlignment = links ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                }
                _ratssitzung.Plaetze.Add(platz);
            }
            if (_ratssaal?.Schreiber is { } pult) SchreiberSetzen(_ratssitzung, pult);
            PlaetzeBesetzen(w, null);
        }
        if (!InnenansichtBetreten(_rathaus.Kennung)) return false;
        RaumWaehlen(RaumRat);
        SaalKamera(false);
        return true;
    }

    /// <summary>Ende der Sitzung: Figuren und Lichter weg, zurück in die Stadt.</summary>
    public void RatssitzungVerlassen()
    {
        if (_ratssitzung is null) return;
        foreach (var knoten in _ratssitzung.AnAnkern) KnotenEntfernen(knoten);
        KnotenEntfernen(_ratssitzung.Wurzel);
        if (_ratssaal?.Antrag is { } rolle) rolle.Visible = false;
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
            platz.Zahl.Text = "";
            // Bei einer Wahl sitzen die Kandidaten mit am Tisch, stimmen aber nicht mit
            var kandidat = punkt.Thema is Sitzungsthema.Wahl or Sitzungsthema.Domprediger
                && punkt.Kandidaten.Any(k => (k.Haus is not null && k.Haus == platz.Haus) || k.Name == platz.Inhaber);
            StimmeSchreiben(platz, kandidat ? "kandidiert" : "", StimmeEnthalten);
        }
        RatssitzungAmZug(null);
        if (s.SchreiberZahl is { } stand) stand.Text = "";
        ZuhoererSetzen(punkt);
        RednerSetzen(punkt.Antragsteller);
        if (_ratssaal?.Antrag is { } rolle) rolle.Visible = punkt.Thema is not (Sitzungsthema.Wahl or Sitzungsthema.Domprediger); // die versiegelte Rolle liegt vor, solange über einen Antrag gesprochen wird
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
        if (_ratssitzung is not { } s || _simulation?.Wirtschaft is not { } w || punkt.Thema is not (Sitzungsthema.Wahl or Sitzungsthema.Domprediger)) return;
        PlaetzeBesetzen(w, punkt);
        ZuhoererSetzen(punkt); // der Abgewählte geht zur Bank, der Gewählte hat sich an seinen Stuhl gesetzt
        if (punkt.Ergebnis?.Gewaehlt is not { } gewaehlt) return;
        if (punkt.Amt is not null && s.Plaetze.FirstOrDefault(p => p.Amt == punkt.Amt) is { } platz)
        {
            GesteSetzen(platz, Geste.Keine);
            StimmeSchreiben(platz, "gewählt: " + Kurzname(gewaehlt), StimmeKandidat);
        }
        else if (s.Zuhoerer.FirstOrDefault(z => z.Name == gewaehlt) is { } dom) // der Domprediger ist kein Ratsamt: Er bleibt auf der Bank, gekennzeichnet
            dom.Schild.Text = "gewählt: " + Kurzname(gewaehlt);
    }

    /// <summary>
    /// Der Schreiber hat eine Hand gezählt: Die Zahl steht über dem Sitz (laufende Nummer in der Zeile des Ratsbuchs), der Zwischenstand über dem Schreiber.
    /// </summary>
    public void RatssitzungGezaehlt(Sitzungsstimme stimme, int nummer, string stand)
    {
        if (_ratssitzung is not { } s) return;
        if (s.Plaetze.FirstOrDefault(p => p.Amt == stimme.Sitz) is { } platz) platz.Zahl.Text = nummer.ToString();
        if (s.SchreiberZahl is { } zahl) zahl.Text = stand;
    }

    /// <summary>
    /// Die Zuhörerbank bei einer Wahl: Wer sich bewirbt und keinen Sitz am Tisch hat, sitzt dort (mit seinem Namen); sitzt er nach der Wahl am Tisch, ist er von der Bank weg,
    /// der Abgewählte ist dafür dort. Ohne Bank im Modell (alter Ratssaal) bleibt sie leer.
    /// </summary>
    private void ZuhoererSetzen(Sitzungspunkt punkt)
    {
        if (_ratssitzung is not { } s || _simulation?.Wirtschaft is not { } w) return;
        foreach (var alt in s.Zuhoerer)
        {
            s.AnAnkern.Remove(alt.Halter);
            KnotenEntfernen(alt.Halter);
        }
        s.Zuhoerer.Clear();
        if (_ratssaal is not { } saal || punkt.Thema is not (Sitzungsthema.Wahl or Sitzungsthema.Domprediger)) return;
        var warten = punkt.Kandidaten.Where(k => !s.Plaetze.Any(p => p.Figur is not null && ((k.Haus is not null && k.Haus == p.Haus) || k.Name == p.Inhaber))).ToList();
        for (var i = 0; i < warten.Count && i < saal.Bank.Count; i++)
        {
            var bank = saal.Bank[i];
            var halter = new Node3D { Transform = bank.Lage };
            bank.Eltern.AddChild(halter);
            s.AnAnkern.Add(halter);
            var schild = SitzungsSchild(halter, new Vector3(0, 1.6f, 0), 28);
            schild.Text = Kurzname(warten[i].Name);
            schild.Modulate = warten[i].Haus is not null && warten[i].Haus == _spielerKonto ? StimmeKandidat : new Color(0.78f, 0.74f, 0.66f);
            s.Zuhoerer.Add(new Zuhoerer
            {
                Halter = halter, Name = warten[i].Name, Schild = schild,
                Figur = SitzungsFigur(halter, RatsherrAussehen(w, warten[i].Name, warten[i].Haus, false), 820 + i, "Sitzen", bank.Sitzhoehe, 0.9f + i * 0.05f),
            });
        }
    }

    /// <summary>
    /// Die Ämter in der Reihenfolge der Stühle: der Bürgermeister (die Wurzel des Amtsbaums) zuerst, dann je Zweig eine Tischseite – die Stühle im Modell stehen
    /// abwechselnd links und rechts, vom Podest zur Tür. Der erste Zweig (in der Reihenfolge der Daten) sitzt also links, der zweite rechts, in jedem steht das
    /// höchste Amt vorn (Stadtrichter, Stadthauptmann, Gefängniswärter links; Kämmerer, Marktmeister, Zöllner rechts). Die Ordnung folgt allein
    /// <see cref="Stadtrat.Untergebene"/>: Ändern die Daten den Baum, ändert sich die Saalordnung. Ämter ohne Anschluss an den Baum kommen zuletzt (nach Stufe).
    /// </summary>
    private static List<Amt> SitzOrdnung(Stadtwirtschaft w)
    {
        var liste = new List<Amt>();
        var aeste = new List<List<Amt>>();
        foreach (var wurzel in w.Rat.Wurzelaemter())
        {
            liste.Add(wurzel);
            foreach (var kind in w.Rat.Untergebene(wurzel)) aeste.Add(Ast(w, kind));
        }
        for (var tiefe = 0; aeste.Any(a => tiefe < a.Count); tiefe++)
            foreach (var ast in aeste)
                if (tiefe < ast.Count) liste.Add(ast[tiefe]);
        foreach (var amt in w.Rat.Aemter.OrderByDescending(a => a.Stufe))
            if (!liste.Contains(amt)) liste.Add(amt);
        return liste;
    }

    /// <summary>Ein Zweig des Amtsbaums von oben nach unten: das Amt, dann seine Untergebenen (in der Reihenfolge der Daten, tiefer zuerst, wo es sich verzweigt).</summary>
    private static List<Amt> Ast(Stadtwirtschaft w, Amt amt)
    {
        var liste = new List<Amt> { amt };
        foreach (var kind in w.Rat.Untergebene(amt)) liste.AddRange(Ast(w, kind));
        return liste;
    }

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
            if (SitzungsFigur(platz.Halter, RatsherrAussehen(w, name, haus, i == 0), 700 + i, "Sitzen", platz.Sitzhoehe, 0.9f + i * 0.04f) is not { } figur) continue;
            platz.Figur = figur;
            platz.Skelett = SitzungsSkelett(figur);
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
        SaalKamera(antragsteller is not null);
        if (antragsteller is null) return;
        if (_ratssaal?.Redner is not { } pult) return;
        var halter = new Node3D { Transform = pult.Lage };
        pult.Eltern.AddChild(halter);
        var oberhaupt = w.Familien.Von(antragsteller)?.Oberhaupt;
        var volk = oberhaupt?.Volk ?? Volk.Mensch;
        var haare = Haarfarben(volk);
        var aussehen = new FigurAussehen(volk, oberhaupt?.Geschlecht == Geschlecht.Frau, Besitzerfarbe(antragsteller), Beinkleidfarben[2], HautVon(volk),
            haare[0], new Color(0.12f, 0.10f, 0.10f), Kopfbedeckung.Barett);
        s.RednerFigur = SitzungsFigur(halter, aussehen, 790, "Reden");
        s.Redner = halter;
        // Sitzt er selbst im Rat, ist sein Stuhl leer, solange er vorn steht
        s.RednerPlatz = s.Plaetze.FirstOrDefault(p => p.Haus == antragsteller);
        if (s.RednerPlatz?.Figur is { } sitzend) sitzend.Modell.Visible = false;
    }

    /// <summary>Der Stadtschreiber am Pult des Modells: ein Mensch in dunklem Blau mit Barett, schreibt das Protokoll mit.</summary>
    private void SchreiberSetzen(Sitzungsbild s, Stuhl pult)
    {
        var halter = new Node3D { Transform = pult.Lage };
        pult.Eltern.AddChild(halter);
        s.AnAnkern.Add(halter);
        var aussehen = new FigurAussehen(Volk.Mensch, false, new Color(0.22f, 0.26f, 0.36f), Beinkleidfarben[1], HautVon(Volk.Mensch), GrauesHaar,
            new Color(0.12f, 0.10f, 0.10f), Kopfbedeckung.Barett);
        s.SchreiberFigur = SitzungsFigur(halter, aussehen, 795, "Schreiben", pult.Sitzhoehe);
        s.SchreiberZahl = SitzungsSchild(halter, new Vector3(0, 1.75f, 0), 36); // der Zwischenstand der Zählung über ihm
        s.SchreiberZahl.Modulate = StimmeKandidat;
    }

    /// <summary>
    /// <b>Die eine Stelle für die Figuren der Sitzung</b> (Ratsherren, Redner, Schreiber, Zuhörer): setzt eine Figur an den Halter, bei <paramref name="sitzhoehe"/> sitzend
    /// (die Hüfte auf dem Stuhl, Kniehöhe des Menschen 0,50 m auf 0,46 m), sonst stehend, und startet die Bewegung. Der Figuren-Baukasten (Szene Teil 2) tauscht nur diese
    /// Methode und <see cref="SitzungsSkelett"/> aus.
    /// </summary>
    private Figur? SitzungsFigur(Node3D halter, FigurAussehen aussehen, int nummer, string bewegung, float? sitzhoehe = null, float takt = 1)
    {
        if (FigurEinsetzen(halter, aussehen, nummer) is not { } figur) return null;
        if (sitzhoehe is { } hoehe) figur.Modell.Position = new Vector3(0, hoehe + 0.04f - figur.Kniehoehe, 0);
        BewegungSetzen(figur, bewegung, takt);
        return figur;
    }

    /// <summary>Das Skelett einer Sitzungsfigur, an dem das Spiel die Gesten dreht (Hand heben, Kopf schütteln); siehe <see cref="SitzungsFigur"/>.</summary>
    private static Skeleton3D? SitzungsSkelett(Figur figur) => figur.Modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();

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
    private Label3D SitzungsSchild(Node3D eltern, Vector3 ort, int groesse) // ort im Halter des Platzes
    {
        // Klicktest 4: Mit fester Größe in der Welt (0,0042 m je Bildpunkt) stand der Name des Platzes nah an der Kamera riesig am Bildrand.
        // Jetzt feste Größe im Bild wie die Schilder im Haus (FixedSize), im Pixel-Look scharf über dem Pixelbild (RathausNeuTexteScharf).
        var schild = new Label3D
        {
            Position = ort, FontSize = groesse, PixelSize = 0.0009f, FixedSize = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            OutlineSize = 12, OutlineModulate = new Color(0.08f, 0.07f, 0.06f),
            Font = Schriften.Ueberschrift,
        };
        eltern.AddChild(schild);
        _rathausNeuTexte.Add(schild);
        return schild;
    }

    /// <summary>
    /// Jedes Bild während der Sitzung: Die Figuren atmen im Sitzen (die Sitzung läuft auch, wenn die Partie steht), danach setzt das Spiel die Geste
    /// über die Pose der Bewegung.
    /// </summary>
    private void RatssitzungAktualisieren(double delta)
    {
        if (_ratssitzung is not { } s || _raum?.Kennung is not RaumRat) return;
        foreach (var platz in s.Plaetze)
        {
            if (platz.Figur is not { } figur) continue;
            FigurBewegen(figur, delta);
            platz.GestenZeit += delta;
            if (platz.Skelett is { } skelett && platz.Geste != Geste.Keine) GesteAnwenden(skelett, platz.Geste, platz.GestenZeit);
        }
        if (s.RednerFigur is { } redner) FigurBewegen(redner, delta);
        if (s.SchreiberFigur is { } schreiber) FigurBewegen(schreiber, delta);
        foreach (var zuhoerer in s.Zuhoerer)
            if (zuhoerer.Figur is { } figur) FigurBewegen(figur, delta);
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
