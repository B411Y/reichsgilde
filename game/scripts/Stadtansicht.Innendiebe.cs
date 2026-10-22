using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Diebe im Haus – Teil der <c>partial class Stadtansicht</c> (PR #171), nach dem Vorbild der Gläubigen (Stadtansicht.Innenglaeubige.cs), aber mit den echten Arbeitern des Betriebs.
///
/// Das Modell (<c>dieb_innen.glb</c>) stellt Plätze: <c>Dieb_&lt;n&gt;</c> mit den extras „taetigkeit“ (Ueben am Übungsbrett, Planen am Kartentisch, Zaehlen am Zähltisch, Schleifen am Schleifbock
/// und Wetztisch, Schlafen auf dem Schlafboden), „bewegung“ (Knacken, Predigen, Zaehlen, Schleifen, Feinarbeit, Schlafen), „platz“ (Füllreihenfolge je Tätigkeit) und – wer sitzt – „sitzhoehe“;
/// dazu <c>Meister_1</c>, der Thron in der Gildenhalle. Wer gerade wo ist und was tut, sagt der Kern (<see cref="Diebeswesen.AnwesenheitJetzt"/>, nach der Spieluhr, ohne Zufall): Das Spiel
/// setzt die Arbeiter, die <see cref="Diebesort.Daheim"/> sind, auf einen freien Platz ihrer Tätigkeit. Wer am Markt oder mit einem Auftrag unterwegs ist, steht nicht im Raum.
///
/// Die Figuren sind die Arbeiter des Betriebs: Sie sind anklickbar und auswählbar wie in den anderen Werkstätten (Auswahlring, Name und Tätigkeit unter der Maus, Rahmen aufziehen).
/// Wer seinen Platz behalten kann (gleiche Tätigkeit wie zuvor), bleibt sitzen oder stehen – nur wer eine neue Tätigkeit beginnt, wechselt den Platz, so rücken nicht alle um, wenn einer dazukommt.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Platz des Modells (<c>Dieb_&lt;n&gt;</c> oder <c>Meister_1</c>) und der Dieb, der darauf sitzt oder steht.</summary>
    private sealed class DiebPlatz
    {
        /// <summary>Tätigkeit des Platzes: „Ueben“, „Planen“, „Zaehlen“, „Schleifen“, „Schlafen“ oder „Thronen“ (der Meisterdieb; die Gruppen der Kern-Tätigkeiten siehe <see cref="DiebGruppe"/>).</summary>
        public required string Gruppe { get; init; }
        /// <summary>Füllreihenfolge innerhalb der Tätigkeit (extras „platz“).</summary>
        public required int Reihe { get; init; }
        public required string Bewegung { get; init; }
        public required bool Sitzend { get; init; }
        public required int AbStufe { get; init; }
        public required Node3D Anker { get; init; }
        public ModellDieb? Besetzt { get; set; }
    }

    /// <summary>Ein Arbeiter des Diebesbetriebs als Figur. Die Figur wird beim ersten Kommen gebaut und bleibt dieselbe (so lange Art und Rang gleich bleiben).</summary>
    private sealed class ModellDieb
    {
        public required int Nummer { get; init; }
        public required string Arbeitskraft { get; init; }
        public required bool Meister { get; init; }
        public required Figur Figur { get; init; }
        public required Node3D Halter { get; init; }
        public required CollisionShape3D Klick { get; init; }
        /// <summary>Goldener Ring auf dem Boden: zeigt, wer ausgewählt ist.</summary>
        public required MeshInstance3D Ring { get; init; }
        public DiebPlatz? Platz { get; set; }
        public Diebestaetigkeit Taetigkeit { get; set; }
    }

    /// <summary>So oft (Sekunden, echte Zeit) fragt die Darstellung den Kern, wer im Haus ist – die Antwort ändert sich höchstens mit der Spielminute.</summary>
    private const double DiebAbfrageAlle = 0.4;

    /// <summary>Die Gruppe der Plätze, auf die eine Tätigkeit gehört: der Vorsitz des Meisterdiebs auf den Thron, sonst der Name der Tätigkeit.</summary>
    private static string DiebGruppe(Diebestaetigkeit taetigkeit) => taetigkeit == Diebestaetigkeit.Vorsitzen ? "Thronen" : taetigkeit.ToString();

    /// <summary>Der Platz zu einem Knoten <c>Dieb_&lt;n&gt;</c> oder <c>Meister_1</c>; null bei jedem anderen Namen.</summary>
    private static DiebPlatz? DiebPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        if (teile.Length != 2 || !int.TryParse(teile[1], out var nummer) || nummer < 1) return null;
        var gruppe = ExtraText(knoten, "taetigkeit");
        var bewegung = ExtraText(knoten, "bewegung");
        if (gruppe == "" || bewegung == "") return null;
        return new DiebPlatz
        {
            Gruppe = gruppe, Reihe = ExtraZahl(knoten, "platz", nummer), Bewegung = bewegung, Anker = knoten,
            Sitzend = Extras(knoten) is { } extras && extras.ContainsKey("sitzhoehe"),
            AbStufe = ExtraZahl(knoten, "stufe", 1),
        };
    }

    /// <summary>
    /// Setzt die Diebe nach dem Kern: alle halbe Sekunde fragen, nur bei einer neuen Verteilung Figuren setzen, zeigen oder verstecken; im Bild laufen nur die Figuren im gezeigten Raum.
    /// Ohne Plätze im Modell oder ohne Abfrage tut die Funktion nichts.
    /// </summary>
    private void InnenDiebeAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, int stufe, double delta)
    {
        if (_simulation?.Wirtschaft is not { } w) return;
        md.DiebZeit += delta;
        if (md.DiebStand is null || md.DiebZeit >= DiebAbfrageAlle || delta <= 0)
        {
            md.DiebZeit = 0;
            var liste = w.Diebe.AnwesenheitJetzt(b);
            var stand = stufe + "|" + string.Join(",", liste.Select(s => $"{s.Arbeiter.Nummer}{s.Arbeiter.Arbeitskraft}{(int)s.Ort}{(int)s.Taetigkeit}{(s.Meisterdieb ? "M" : "")}"));
            if (stand != md.DiebStand)
            {
                md.DiebStand = stand;
                DiebeSetzen(a, md, liste, stufe);
            }
        }

        var bewegt = !_simulation.Takt.IstPausiert;
        foreach (var dieb in md.Diebe.Values)
        {
            var da = dieb.Platz is not null;
            if (dieb.Klick.Disabled == da) dieb.Klick.Disabled = !da; // Einsteigen setzt alle Klickflächen an: abgleichen
            var gewaehlt = _gewaehlt.Contains(dieb.Nummer);
            FigurHervorheben(dieb.Figur, da && (gewaehlt || _hoverArbeiter == dieb.Nummer));
            if (dieb.Ring.Visible != (da && gewaehlt)) dieb.Ring.Visible = da && gewaehlt;
            if (dieb.Platz is not { } platz || _raum is null || !IstImGezeigtenRaum(dieb.Halter)) continue;
            var takt = RaumTakt.GetValueOrDefault((a.Stil, platz.Bewegung));
            if (takt > 0 && md.Animation is not null)
            {
                // Am Schleifbock läuft die Figur im Takt des Schleifsteins (Animation „Arbeit“ des Raums), damit Fuß und Kurbel zum Stein passen.
                dieb.Figur.Phase = md.ArbeitPhase % takt / takt * dieb.Figur.Dauer;
                FigurBewegen(dieb.Figur, 0);
            }
            else if (bewegt || dieb.Figur.Neu) FigurImBildBewegen(dieb.Figur, null, bewegt ? delta : 0);
        }
    }

    /// <summary>Verteilt die Arbeiter, die daheim sind, auf die Plätze ihrer Tätigkeit (wer seinen Platz behalten kann, behält ihn) und räumt auf, wer gegangen ist.</summary>
    private void DiebeSetzen(BetriebsAnsicht a, ModellRaumDaten md, IReadOnlyList<Diebesstand> liste, int stufe)
    {
        var daheim = liste.Where(s => s.Ort == Diebesort.Daheim).ToDictionary(s => s.Arbeiter.Nummer);
        var gewaehltGeaendert = false;
        foreach (var dieb in md.Diebe.Values.ToList())
        {
            var stand = liste.FirstOrDefault(s => s.Arbeiter.Nummer == dieb.Nummer);
            if (stand is null || stand.Arbeiter.Arbeitskraft != dieb.Arbeitskraft || stand.Meisterdieb != dieb.Meister)
            {
                // nicht mehr im Betrieb oder befördert (neues Gewand): die Figur wird neu gebaut
                DiebEntfernen(a, md, dieb);
                gewaehltGeaendert |= stand is null && _gewaehlt.Remove(dieb.Nummer);
            }
            else if (dieb.Platz is { } platz && (!daheim.TryGetValue(dieb.Nummer, out var s) || platz.Gruppe != DiebGruppe(s.Taetigkeit)))
            {
                platz.Besetzt = null; // geht auf den Markt oder zu einem Auftrag, oder beginnt etwas anderes
                dieb.Platz = null;
            }
        }
        foreach (var stand in daheim.Values)
        {
            var dieb = md.Diebe.GetValueOrDefault(stand.Arbeiter.Nummer);
            if (dieb?.Platz is null)
            {
                var gruppe = DiebGruppe(stand.Taetigkeit);
                var platz = md.Diebesplaetze.FirstOrDefault(p => p.Gruppe == gruppe && p.Besetzt is null && p.AbStufe <= stufe);
                if (platz is not null)
                {
                    dieb ??= DiebBauen(a, md, stand, platz);
                    if (dieb is not null) DiebAufPlatz(dieb, platz);
                }
            }
            if (dieb is not null) dieb.Taetigkeit = stand.Taetigkeit;
        }
        foreach (var dieb in md.Diebe.Values) dieb.Halter.Visible = dieb.Platz is not null;
        md.DiebWach = daheim.Values.Any(s => s.Taetigkeit != Diebestaetigkeit.Schlafen);
        md.DiebBewegtRaum = md.Diebesplaetze.Any(p => p.Besetzt is not null && p.Bewegung is "Schleifen" or "Zaehlen");
        if (gewaehltGeaendert) ArbeiterAuswahlGeaendert?.Invoke();
    }

    private void DiebEntfernen(BetriebsAnsicht a, ModellRaumDaten md, ModellDieb dieb)
    {
        if (dieb.Platz is { } platz) platz.Besetzt = null;
        a.Raumklicks.Remove(dieb.Klick);
        dieb.Halter.GetParent()?.RemoveChild(dieb.Halter);
        dieb.Halter.QueueFree();
        md.Diebe.Remove(dieb.Nummer);
    }

    /// <summary>Baut die Figur eines Arbeiters (null, wenn das Figurenmodell fehlt). Sie hängt zuerst am Anker des Platzes, auf den sie kommt.</summary>
    private ModellDieb? DiebBauen(BetriebsAnsicht a, ModellRaumDaten md, Diebesstand stand, DiebPlatz platz)
    {
        var nummer = stand.Arbeiter.Nummer;
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0), Visible = false }; // der Anker blickt mit −Z auf sein Ziel, die Figur mit +Z: um 180° gedreht einsetzen
        platz.Anker.AddChild(halter);
        if (FigurEinsetzen(halter, DiebAussehen(nummer, stand.Arbeiter.Arbeitskraft, stand.Meisterdieb), nummer) is not { } figur)
        {
            halter.QueueFree();
            return null;
        }
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", "arbeiter:" + nummer);
        var form = new CollisionShape3D { Disabled = true };
        koerper.AddChild(form);
        halter.AddChild(koerper);
        a.Raumklicks.Add(form);
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.46f, OuterRadius = 0.58f, RingSegments = 6, Rings = 24 },
            MaterialOverride = AuswahlRing(), Position = new Vector3(0, 0.04f, 0), Visible = false,
        };
        halter.AddChild(ring);
        var dieb = new ModellDieb { Nummer = nummer, Arbeitskraft = stand.Arbeiter.Arbeitskraft, Meister = stand.Meisterdieb, Figur = figur, Halter = halter, Klick = form, Ring = ring };
        md.Diebe[nummer] = dieb;
        return dieb;
    }

    /// <summary>Setzt die Figur auf den Platz: Anker, Sitzhöhe, Bewegung (Dietrich beim Knacken, Münzbeutel beim Zählen), Klickfläche und Render-Ebene des Raums.</summary>
    private void DiebAufPlatz(ModellDieb dieb, DiebPlatz platz)
    {
        platz.Besetzt = dieb;
        dieb.Platz = platz;
        var halter = dieb.Halter;
        if (halter.GetParent() != platz.Anker)
        {
            halter.GetParent()?.RemoveChild(halter);
            platz.Anker.AddChild(halter);
        }
        halter.Rotation = new Vector3(0, Mathf.Pi, 0);
        // Wer sitzt, hat die Hüfte auf Sitzhöhe des Menschen (kleine Völker sitzen höher, Orks tiefer); wer liegt, hat den Fußpunkt unter der Hüfte und wird nicht angehoben.
        halter.Position = platz.Sitzend ? new Vector3(0, SitzKniehoehe - dieb.Figur.Kniehoehe, 0) : Vector3.Zero;
        var tempo = platz.Bewegung == "Schleifen" ? 1f : 0.85f + (Streuung(dieb.Nummer, 17) % 31) / 100f; // 0,85 … 1,15: nicht im Gleichschritt
        BewegungSetzen(dieb.Figur, platz.Bewegung, tempo);
        // Klickfläche: stehend und sitzend eine Kapsel, liegend ein flacher Quader über der Länge der Figur
        if (platz.Bewegung == "Schlafen")
        {
            dieb.Klick.Shape = new BoxShape3D { Size = new Vector3(0.7f, 0.4f, 1.8f) };
            dieb.Klick.Position = new Vector3(0, 0.3f, 0);
        }
        else
        {
            var hoehe = platz.Sitzend ? 1.3f : 1.75f;
            dieb.Klick.Shape = new CapsuleShape3D { Radius = platz.Sitzend ? 0.36f : 0.38f, Height = hoehe };
            dieb.Klick.Position = new Vector3(0, hoehe / 2 + (platz.Sitzend ? 0.1f : 0), 0);
        }
        dieb.Ring.Scale = platz.Bewegung == "Schlafen" ? new Vector3(1.2f, 1, 2.2f) : Vector3.One;
        RaumEbeneUebernehmen(halter);
    }

    /// <summary>
    /// Aussehen eines Diebes, fest je Arbeiter: dunkles Gewand (Lehrlinge heller und geflickt), meist mit Kapuze, manche mit Barett, Orks grün;
    /// der Meisterdieb trägt weinrotes Samtgewand und ein schwarzes Barett über grauem Haar.
    /// </summary>
    private static FigurAussehen DiebAussehen(int nummer, string arbeitskraft, bool meister)
    {
        var volk = arbeitskraft == "Ork" ? Volk.Ork : Volk.Mensch;
        if (meister)
            return new FigurAussehen(volk, false, new Color(0.34f, 0.08f, 0.11f), Beinkleidfarben[1], HautVon(volk), GrauesHaar, Schwarz, Kopfbedeckung.Barett);
        var dunkel = new[] { new Color(0.17f, 0.17f, 0.19f), new Color(0.22f, 0.20f, 0.17f), new Color(0.15f, 0.18f, 0.17f), new Color(0.26f, 0.20f, 0.16f) };
        var gewand = arbeitskraft == "Lehrling" ? new Color(0.32f, 0.29f, 0.25f) : dunkel[Streuung(nummer, 7) % (uint)dunkel.Length];
        var haare = Haarfarben(volk);
        var frau = volk == Volk.Mensch && Streuung(nummer, 53) % 4 == 0;
        var kopf = (Streuung(nummer, 29) % 6) switch { 0 => Kopfbedeckung.Barett, 1 => Kopfbedeckung.Keine, _ => Kopfbedeckung.Kapuze };
        return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk), haare[nummer * 5 % haare.Length], gewand, kopf);
    }

    /// <summary>Name und Hinweis unter der Maus bei einem Dieb: Rang und Nummer, darunter was er gerade tut und seine Laune.</summary>
    private static (string Name, string Hinweis) DiebText(ModellRaumDaten md, ArbeiterZeile zeile, bool gewaehlt)
    {
        var dieb = md.Diebe.GetValueOrDefault(zeile.Nummer);
        var was = dieb?.Taetigkeit switch
        {
            Diebestaetigkeit.Ueben => "übt am Übungsbrett Schlösser zu knacken",
            Diebestaetigkeit.Schleifen => "schleift Messer und Werkzeug",
            Diebestaetigkeit.Zaehlen => "zählt die Beute",
            Diebestaetigkeit.Planen => "plant am Kartentisch",
            Diebestaetigkeit.Schlafen => "schläft",
            Diebestaetigkeit.Vorsitzen => "sitzt der Gilde vor",
            _ => "ist im Haus",
        };
        return ($"{(dieb?.Meister == true ? "Meisterdieb" : zeile.Arbeitskraft)} Nr. {zeile.Nummer}", $"{was} · Laune {Zahlenformat.Ganz(zeile.Laune)}" + (gewaehlt ? " · ausgewählt" : ""));
    }
}
