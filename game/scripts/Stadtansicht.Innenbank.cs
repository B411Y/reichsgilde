using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bank;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Schreiber und die Kunden in der Wechselstube – Teil der <c>partial class Stadtansicht</c> (PR #175), nach dem Vorbild der Wache (Stadtansicht.Innenwache.cs)
/// und der Gäste (Stadtansicht.Innengaeste.cs).
///
/// Das Modell (<c>geldleiher_innen.glb</c>) stellt Plätze: <c>Schreiber_&lt;n&gt;</c> mit den extras „taetigkeit“ (wie <see cref="Schreibertaetigkeit"/> im Kern: Bedienen, Wiegen, Rechnen,
/// Zaehlen, Hauptbuch), „bewegung“ (Predigen, Wiegen, Rechnen, Zaehlen, Schreiben), „platz“ (Füllreihenfolge je Tätigkeit) und – wer sitzt – „sitzhoehe“, dazu
/// <c>Kunde_&lt;Art&gt;_&lt;n&gt;</c> (Kaufmann am Wechseltisch, Bürger am Schalter, Vornehm im Empfang ab Stufe 3; n = Platz + 1). Wer gerade was tut und wer gerade da ist,
/// sagt der Kern (<see cref="Bankwesen.SchreiberJetzt"/>, <see cref="Bankwesen.KundenJetzt"/>, nach der Spieluhr, ohne Zufall): Das Spiel setzt die Arbeiter auf einen freien Platz ihrer Tätigkeit
/// und die Kunden auf den Platz ihrer Art. Außerhalb der Arbeitszeit sind die Schreiber daheim, nicht im Raum.
///
/// Die Schreiber sind die Arbeiter des Betriebs: anklickbar und auswählbar wie in den anderen Werkstätten. Die Kunden sind wie die Gäste nur Figuren mit einem Namen unter der Maus.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Platz des Modells (<c>Schreiber_&lt;n&gt;</c>) und der Schreiber, der darauf sitzt oder steht.</summary>
    private sealed class SchreiberPlatz
    {
        /// <summary>Name der <see cref="Schreibertaetigkeit"/> des Platzes („Bedienen“, „Wiegen“, „Rechnen“, „Zaehlen“, „Hauptbuch“).</summary>
        public required string Gruppe { get; init; }
        /// <summary>Füllreihenfolge innerhalb der Tätigkeit (extras „platz“).</summary>
        public required int Reihe { get; init; }
        public required string Bewegung { get; init; }
        public required bool Sitzend { get; init; }
        public required int AbStufe { get; init; }
        public required Node3D Anker { get; init; }
        public ModellSchreiber? Besetzt { get; set; }
    }

    /// <summary>Ein Arbeiter der Wechselstube als Figur. Die Figur wird beim ersten Kommen gebaut und bleibt dieselbe (so lange die Art der Arbeitskraft gleich bleibt).</summary>
    private sealed class ModellSchreiber
    {
        public required int Nummer { get; init; }
        public required string Arbeitskraft { get; init; }
        public required Figur Figur { get; init; }
        public required Node3D Halter { get; init; }
        public required CollisionShape3D Klick { get; init; }
        /// <summary>Goldener Ring auf dem Boden: zeigt, wer ausgewählt ist.</summary>
        public required MeshInstance3D Ring { get; init; }
        public SchreiberPlatz? Platz { get; set; }
        public Schreibertaetigkeit Taetigkeit { get; set; }
        /// <summary>Wen er gerade bedient (nur bei <see cref="Schreibertaetigkeit.Bedienen"/>).</summary>
        public Kundenart? Bedient { get; set; }
    }

    /// <summary>Ein Platz für Kunden (<c>Kunde_&lt;Art&gt;_&lt;n&gt;</c>) und die Figur, die gerade darauf steht oder sitzt. Die Figur wird beim ersten Gebrauch gebaut und bleibt dieselbe (bei Vornehmen: solange dasselbe Haus kommt).</summary>
    private sealed class KundenPlatz
    {
        public required Kundenart Art { get; init; }
        /// <summary>Nummer im Knotennamen (Platz + 1, extras „platz“).</summary>
        public required int Reihe { get; init; }
        public required string Bewegung { get; init; }
        public required bool Sitzend { get; init; }
        public required int AbStufe { get; init; }
        public required Node3D Anker { get; init; }
        /// <summary>Wer jetzt hier ist (null: der Platz ist leer).</summary>
        public Bankkunde? Kunde { get; set; }
        public Node3D? Halter { get; set; }
        public Figur? Figur { get; set; }
        public CollisionShape3D? Klick { get; set; }
        /// <summary>Das Haus, für das die Figur gekleidet wurde (Vornehme tragen die Farbe ihres Hauses).</summary>
        public Konto? Haus { get; set; }
    }

    /// <summary>So oft (Sekunden, echte Zeit) fragt die Darstellung den Kern, wer im Haus ist und wer Kunde ist – die Antwort ändert sich höchstens mit der Spielminute.</summary>
    private const double BankAbfrageAlle = 0.4;

    /// <summary>Der Platz zu einem Knoten <c>Schreiber_&lt;n&gt;</c>; null bei jedem anderen Namen.</summary>
    private static SchreiberPlatz? SchreiberPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        if (teile.Length != 2 || !int.TryParse(teile[1], out var nummer) || nummer < 1) return null;
        var gruppe = ExtraText(knoten, "taetigkeit");
        var bewegung = ExtraText(knoten, "bewegung");
        if (gruppe == "" || bewegung == "") return null;
        return new SchreiberPlatz
        {
            Gruppe = gruppe, Reihe = ExtraZahl(knoten, "platz", nummer), Bewegung = bewegung, Anker = knoten,
            Sitzend = Extras(knoten) is { } extras && extras.ContainsKey("sitzhoehe"),
            AbStufe = ExtraZahl(knoten, "stufe", 1),
        };
    }

    /// <summary>Der Platz zu einem Knoten <c>Kunde_&lt;Art&gt;_&lt;n&gt;</c> (Kaufmann, Buerger, Vornehm); null bei jedem anderen Namen.</summary>
    private static KundenPlatz? KundenPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        if (teile.Length != 3 || !int.TryParse(teile[2], out var nummer) || nummer < 1) return null;
        if (!Enum.TryParse<Kundenart>(teile[1], out var art)) return null;
        var bewegung = ExtraText(knoten, "bewegung");
        if (bewegung == "") return null;
        return new KundenPlatz
        {
            Art = art, Reihe = ExtraZahl(knoten, "platz", nummer), Bewegung = bewegung, Anker = knoten,
            Sitzend = Extras(knoten) is { } extras && extras.ContainsKey("sitzhoehe"),
            AbStufe = ExtraZahl(knoten, "stufe", 1),
        };
    }

    /// <summary>
    /// Setzt Schreiber und Kunden nach dem Kern: alle halbe Sekunde fragen, nur bei einer neuen Verteilung Figuren setzen, zeigen oder verstecken; im Bild laufen nur die Figuren im gezeigten Raum.
    /// Ohne Plätze im Modell oder ohne Abfrage tut die Funktion nichts.
    /// </summary>
    private void InnenBankAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, int stufe, double delta)
    {
        if (_simulation?.Wirtschaft is not { } w) return;
        md.BankZeit += delta;
        if (md.BankStand is null || md.BankZeit >= BankAbfrageAlle || delta <= 0)
        {
            md.BankZeit = 0;
            var schreiber = w.Bank.SchreiberJetzt(b);
            var kunden = w.Bank.KundenJetzt(b);
            var stand = stufe + "|" + string.Join(",", schreiber.Select(s => $"{s.Arbeiter.Nummer}{s.Arbeiter.Arbeitskraft}{(int)s.Taetigkeit}{(s.Bedient is { } art ? (int)art : -1)}"))
                        + "|" + string.Join(",", kunden.Select(k => $"{(int)k.Art}.{k.Platz}.{(int)k.Anliegen}.{k.Haus?.Inhaber}"));
            if (stand != md.BankStand)
            {
                md.BankStand = stand;
                SchreiberSetzen(a, md, b, schreiber, stufe);
                KundenSetzen(a, md, kunden, stufe);
            }
        }

        var bewegt = !_simulation.Takt.IstPausiert;
        foreach (var schreiber in md.Schreiber.Values)
        {
            var da = schreiber.Platz is not null;
            if (schreiber.Klick.Disabled == da) schreiber.Klick.Disabled = !da; // Einsteigen setzt alle Klickflächen an: abgleichen
            var gewaehlt = _gewaehlt.Contains(schreiber.Nummer);
            FigurHervorheben(schreiber.Figur, da && (gewaehlt || _hoverArbeiter == schreiber.Nummer));
            if (schreiber.Ring.Visible != (da && gewaehlt)) schreiber.Ring.Visible = da && gewaehlt;
            if (schreiber.Platz is not { } platz || _raum is null || !IstImGezeigtenRaum(schreiber.Halter)) continue;
            var takt = platz.Reihe == 1 ? RaumTakt.GetValueOrDefault((a.Stil, platz.Bewegung)) : 0;
            if (takt > 0 && md.Animation is not null)
            {
                // Am Wiegetisch (Platz 1) läuft die Figur im Takt der Waage (Animation „Arbeit“ des Raums), damit Hand und Waagschale zusammenpassen.
                schreiber.Figur.Phase = md.ArbeitPhase % takt / takt * schreiber.Figur.Dauer;
                FigurBewegen(schreiber.Figur, 0);
            }
            else if (bewegt || schreiber.Figur.Neu) FigurImBildBewegen(schreiber.Figur, null, bewegt ? delta : 0);
        }
        foreach (var platz in md.Kundenplaetze)
        {
            var da = platz.Kunde is not null;
            if (platz.Klick is { } klick && klick.Disabled == da) klick.Disabled = !da; // wie die Gäste: abgleichen
            if (platz.Figur is not { } figur || !da || _raum is null || !IstImGezeigtenRaum(platz.Anker)) continue;
            if (bewegt || figur.Neu) FigurImBildBewegen(figur, null, bewegt ? delta : 0);
        }
    }

    /// <summary>Verteilt die Schreiber auf die Plätze ihrer Tätigkeit (wer seinen Platz behalten kann, behält ihn) und räumt auf, wer gegangen ist. Wer nicht in der Liste steht, ist daheim.</summary>
    private void SchreiberSetzen(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, IReadOnlyList<Schreiberstand> liste, int stufe)
    {
        var jetzt = liste.ToDictionary(s => s.Arbeiter.Nummer);
        var gewaehltGeaendert = false;
        foreach (var schreiber in md.Schreiber.Values.ToList())
        {
            var arbeiter = b.ArbeiterNr(schreiber.Nummer);
            if (arbeiter is null || arbeiter.Arbeitskraft != schreiber.Arbeitskraft)
            {
                // nicht mehr im Betrieb oder befördert (neues Gewand): die Figur wird neu gebaut
                SchreiberEntfernen(a, md, schreiber);
                gewaehltGeaendert |= arbeiter is null && _gewaehlt.Remove(schreiber.Nummer);
            }
            else if (schreiber.Platz is { } platz && (!jetzt.TryGetValue(schreiber.Nummer, out var s) || platz.Gruppe != s.Taetigkeit.ToString()))
            {
                platz.Besetzt = null; // Feierabend, oder er beginnt etwas anderes
                schreiber.Platz = null;
            }
        }
        foreach (var stand in liste.OrderBy(s => s.Arbeiter.Nummer))
        {
            var schreiber = md.Schreiber.GetValueOrDefault(stand.Arbeiter.Nummer);
            if (schreiber?.Platz is null)
            {
                var gruppe = stand.Taetigkeit.ToString();
                var platz = md.Schreiberplaetze.FirstOrDefault(p => p.Gruppe == gruppe && p.Besetzt is null && p.AbStufe <= stufe);
                if (platz is not null)
                {
                    schreiber ??= SchreiberBauen(a, md, stand, platz);
                    if (schreiber is not null) SchreiberAufPlatz(schreiber, platz);
                }
            }
            if (schreiber is not null)
            {
                schreiber.Taetigkeit = stand.Taetigkeit;
                schreiber.Bedient = stand.Bedient;
            }
        }
        foreach (var schreiber in md.Schreiber.Values) schreiber.Halter.Visible = schreiber.Platz is not null;
        md.BankBewegtRaum = md.Schreiberplaetze.Any(p => p.Besetzt is not null && p.Reihe == 1 && p.Bewegung == "Wiegen"); // die Waage schwingt nur, wenn am Wiegetisch jemand wiegt
        if (gewaehltGeaendert) ArbeiterAuswahlGeaendert?.Invoke();
    }

    private void SchreiberEntfernen(BetriebsAnsicht a, ModellRaumDaten md, ModellSchreiber schreiber)
    {
        if (schreiber.Platz is { } platz) platz.Besetzt = null;
        a.Raumklicks.Remove(schreiber.Klick);
        schreiber.Halter.GetParent()?.RemoveChild(schreiber.Halter);
        schreiber.Halter.QueueFree();
        md.Schreiber.Remove(schreiber.Nummer);
    }

    /// <summary>Baut die Figur eines Arbeiters (null, wenn das Figurenmodell fehlt). Sie hängt zuerst am Anker des Platzes, auf den sie kommt.</summary>
    private ModellSchreiber? SchreiberBauen(BetriebsAnsicht a, ModellRaumDaten md, Schreiberstand stand, SchreiberPlatz platz)
    {
        var nummer = stand.Arbeiter.Nummer;
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0), Visible = false }; // der Anker blickt mit −Z auf sein Ziel, die Figur mit +Z: um 180° gedreht einsetzen
        platz.Anker.AddChild(halter);
        if (FigurEinsetzen(halter, SchreiberAussehen(nummer, stand.Arbeiter.Arbeitskraft), nummer) is not { } figur)
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
        var schreiber = new ModellSchreiber { Nummer = nummer, Arbeitskraft = stand.Arbeiter.Arbeitskraft, Figur = figur, Halter = halter, Klick = form, Ring = ring };
        md.Schreiber[nummer] = schreiber;
        return schreiber;
    }

    /// <summary>Setzt die Figur auf den Platz: Anker, Sitzhöhe, Bewegung (Rechenpfennig beim Rechnen, Münze beim Wiegen …), Klickfläche und Render-Ebene des Raums.</summary>
    private void SchreiberAufPlatz(ModellSchreiber schreiber, SchreiberPlatz platz)
    {
        platz.Besetzt = schreiber;
        schreiber.Platz = platz;
        var halter = schreiber.Halter;
        if (halter.GetParent() != platz.Anker)
        {
            halter.GetParent()?.RemoveChild(halter);
            platz.Anker.AddChild(halter);
        }
        halter.Rotation = new Vector3(0, Mathf.Pi, 0);
        // Wer sitzt, hat die Hüfte auf Sitzhöhe des Menschen (kleine Völker sitzen höher, Orks tiefer).
        halter.Position = platz.Sitzend ? new Vector3(0, SitzKniehoehe - schreiber.Figur.Kniehoehe, 0) : Vector3.Zero;
        // Am Wiegetisch mit der bewegten Waage (Platz 1) im Takt des Raums, sonst 0,85 … 1,15: nicht im Gleichschritt.
        var tempo = platz.Reihe == 1 && RaumTakt.ContainsKey((Werkstil.Bank, platz.Bewegung)) ? 1f : 0.85f + (Streuung(schreiber.Nummer, 17) % 31) / 100f;
        BewegungSetzen(schreiber.Figur, platz.Bewegung, tempo);
        var hoehe = platz.Sitzend ? 1.3f : 1.75f;
        schreiber.Klick.Shape = new CapsuleShape3D { Radius = platz.Sitzend ? 0.36f : 0.38f, Height = hoehe };
        schreiber.Klick.Position = new Vector3(0, hoehe / 2 + (platz.Sitzend ? 0.1f : 0), 0);
        RaumEbeneUebernehmen(halter);
    }

    /// <summary>
    /// Setzt die Kunden auf ihre Plätze: Wer schon dasselbe Haus ist, bleibt, wer nicht mehr in der Liste steht, geht (die Figur wird nur versteckt).
    /// Kommt auf einen Platz der Vornehmen ein anderes Haus, bekommt er eine neue Figur in dessen Farbe.
    /// </summary>
    private void KundenSetzen(BetriebsAnsicht a, ModellRaumDaten md, IReadOnlyList<Bankkunde> kunden, int stufe)
    {
        foreach (var platz in md.Kundenplaetze)
        {
            var kunde = platz.AbStufe <= stufe ? kunden.FirstOrDefault(k => k.Art == platz.Art && k.Platz + 1 == platz.Reihe) : null;
            platz.Kunde = kunde;
            if (kunde is null)
            {
                if (platz.Halter is { } weg) weg.Visible = false;
                if (platz.Klick is { } zu) zu.Disabled = true;
                continue;
            }
            if (platz.Halter is not null && platz.Haus != kunde.Haus) KundeAbbauen(a, platz); // ein anderes Haus sitzt jetzt hier
            if (platz.Halter is null) KundeBauen(a, platz, kunde);
            if (platz.Halter is not { } halter) continue;
            halter.Visible = true;
            if (platz.Klick is { } klick) klick.Disabled = false;
        }
    }

    private void KundeAbbauen(BetriebsAnsicht a, KundenPlatz platz)
    {
        if (platz.Klick is { } klick) a.Raumklicks.Remove(klick);
        if (platz.Halter is { } halter)
        {
            halter.GetParent()?.RemoveChild(halter);
            halter.QueueFree();
        }
        platz.Halter = null;
        platz.Figur = null;
        platz.Klick = null;
        platz.Haus = null;
    }

    private void KundeBauen(BetriebsAnsicht a, KundenPlatz platz, Bankkunde kunde)
    {
        var nummer = 300 + (int)platz.Art * 10 + platz.Reihe; // Aussehen und Tempo bleiben je Platz gleich
        // Der Anker blickt mit −Z auf den Tisch bzw. das Pult, die Figur mit +Z: um 180° gedreht einsetzen.
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0), Visible = false };
        platz.Anker.AddChild(halter);
        var farbe = kunde.Haus is { } haus ? Besitzerfarbe(haus) : (Color?)null;
        if (FigurEinsetzen(halter, KundeAussehen(platz.Art, nummer, farbe), nummer) is not { } figur)
        {
            halter.QueueFree();
            return;
        }
        if (platz.Sitzend) halter.Position = new Vector3(0, SitzKniehoehe - figur.Kniehoehe, 0);
        BewegungSetzen(figur, platz.Bewegung, 0.85f + (Streuung(nummer, 17) % 31) / 100f);
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", "kunde:" + a.Modellraum!.Kundenplaetze.IndexOf(platz));
        var hoehe = platz.Sitzend ? 1.3f : 1.75f;
        var klick = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.36f, Height = hoehe }, Position = new Vector3(0, hoehe / 2 + (platz.Sitzend ? 0.1f : 0), 0), Disabled = true };
        koerper.AddChild(klick);
        halter.AddChild(koerper);
        a.Raumklicks.Add(klick);
        RaumEbeneUebernehmen(halter);
        platz.Halter = halter;
        platz.Figur = figur;
        platz.Klick = klick;
        platz.Haus = kunde.Haus;
    }

    /// <summary>
    /// Aussehen eines Schreibers, fest je Arbeiter: das dunkelblaue Gewand der Geldleiher (Lehrlinge in blassem, ungefärbtem Blau), meist mit Barett, Orks grün.
    /// </summary>
    private static FigurAussehen SchreiberAussehen(int nummer, string arbeitskraft)
    {
        var volk = arbeitskraft == "Ork" ? Volk.Ork : Volk.Mensch;
        var gewand = arbeitskraft == "Lehrling" ? new Color(0.36f, 0.40f, 0.52f) : new Color(0.14f, 0.19f, 0.36f).Lerp(new Color(0.20f, 0.26f, 0.44f), (Streuung(nummer, 7) % 5) / 8f);
        var haare = Haarfarben(volk);
        var frau = volk == Volk.Mensch && Streuung(nummer, 53) % 4 == 0;
        var kopf = frau ? Kopfbedeckung.Haube : Streuung(nummer, 29) % 3 == 0 ? Kopfbedeckung.Keine : Kopfbedeckung.Barett;
        return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk), haare[nummer * 5 % haare.Length],
            frau ? Leinenfarbe : new Color(0.10f, 0.12f, 0.22f), kopf);
    }

    /// <summary>
    /// Aussehen eines Kunden, fest je Platz: Kaufleute in Reisekleidung (erdfarbenes Gewand, Kapuze, Strohhut oder Barett, oft mit Tasche, Bündel oder Kiepe, bunt gemischte Völker),
    /// Bürger schlicht (Leinen und Grau, Frauen mit Haube), Vornehme in reicher Kleidung: das Gewand in der Farbe ihres Hauses, Barett oder Schleier; ohne Haus tiefes Weinrot.
    /// </summary>
    private static FigurAussehen KundeAussehen(Kundenart art, int nummer, Color? hausfarbe)
    {
        var wahl = Streuung(nummer, 71) % 12;
        if (art == Kundenart.Vornehm)
        {
            var volk = wahl switch { 6 => Volk.Elb, 7 => Volk.Zwerg, _ => Volk.Mensch };
            var frau = volk == Volk.Mensch && Streuung(nummer, 53) % 3 == 0;
            var gewand = (hausfarbe ?? new Color(0.40f, 0.10f, 0.16f)).Lerp(Colors.Black, 0.12f);
            var haare = Haarfarben(volk);
            return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[1], HautVon(volk), haare[nummer * 5 % haare.Length],
                frau ? Leinenfarbe : Schwarz, frau ? Kopfbedeckung.Schleier : Kopfbedeckung.Barett);
        }
        if (art == Kundenart.Buerger)
        {
            var frau = Streuung(nummer, 53) % 2 == 0;
            var schlicht = new[] { new Color(0.62f, 0.58f, 0.48f), new Color(0.46f, 0.44f, 0.42f), new Color(0.55f, 0.50f, 0.40f), new Color(0.40f, 0.42f, 0.38f) };
            var gewand = schlicht[Streuung(nummer, 7) % (uint)schlicht.Length];
            var haare = Haarfarben(Volk.Mensch);
            return new FigurAussehen(Volk.Mensch, frau, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], Hautfarbe, haare[nummer * 5 % haare.Length],
                frau ? Leinenfarbe : gewand, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine);
        }
        else
        {
            var volk = wahl switch { 6 or 7 => Volk.Zwerg, 8 => Volk.Halbling, 9 => Volk.Elb, 10 => Volk.Ork, _ => Volk.Mensch };
            var frau = volk == Volk.Mensch && Streuung(nummer, 53) % 5 == 0;
            var reise = new[] { new Color(0.42f, 0.34f, 0.22f), new Color(0.30f, 0.36f, 0.28f), new Color(0.50f, 0.38f, 0.24f), new Color(0.34f, 0.32f, 0.30f), new Color(0.26f, 0.30f, 0.38f) };
            var gewand = reise[Streuung(nummer, 7) % (uint)reise.Length];
            var haare = Haarfarben(volk);
            var kopf = frau ? Kopfbedeckung.Haube : (Streuung(nummer, 29) % 4) switch { 0 => Kopfbedeckung.Kapuze, 1 => Kopfbedeckung.Strohhut, 2 => Kopfbedeckung.Barett, _ => Kopfbedeckung.Keine };
            var last = (Streuung(nummer, 43) % 3) switch { 0 => Last.Tasche, 1 => Last.Buendel, _ => Last.Kiepe };
            return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk), haare[nummer * 5 % haare.Length],
                frau ? Leinenfarbe : gewand, kopf, last);
        }
    }

    /// <summary>Name und Hinweis unter der Maus bei einem Schreiber: Rang und Nummer, darunter was er gerade tut und seine Laune.</summary>
    private static (string Name, string Hinweis) SchreiberText(ModellRaumDaten md, ArbeiterZeile zeile, bool gewaehlt)
    {
        var schreiber = md.Schreiber.GetValueOrDefault(zeile.Nummer);
        var was = schreiber?.Taetigkeit switch
        {
            Schreibertaetigkeit.Bedienen => schreiber.Bedient switch
            {
                Kundenart.Kaufmann => "bedient einen Kaufmann am Wechseltisch",
                Kundenart.Buerger => "bedient einen Bürger am Schalter",
                Kundenart.Vornehm => "bedient die vornehmen Gäste",
                _ => "bedient die Kunden",
            },
            Schreibertaetigkeit.Rechnen => "rechnet am Rechenbrett",
            Schreibertaetigkeit.Wiegen => "wiegt Münzen auf der Waage",
            Schreibertaetigkeit.Hauptbuch => "führt das Hauptbuch",
            Schreibertaetigkeit.Zaehlen => "zählt Münzen",
            _ => "ist im Haus",
        };
        return ($"{zeile.Arbeitskraft} Nr. {zeile.Nummer}", $"{was} · Laune {Zahlenformat.Ganz(zeile.Laune)}" + (gewaehlt ? " · ausgewählt" : ""));
    }

    /// <summary>Name und Hinweis unter der Maus bei einem Kunden: Art und Anliegen, bei Vornehmen das Haus.</summary>
    private (string Name, string Hinweis) KundeText(BetriebsAnsicht a, int platz)
    {
        if (a.Modellraum is not { } md || platz < 0 || platz >= md.Kundenplaetze.Count || md.Kundenplaetze[platz].Kunde is not { } kunde) return ("Kunde", "");
        var w = _simulation?.Wirtschaft;
        var waehrung = w?.Daten.Allgemein.Waehrung ?? "";
        switch (kunde.Art)
        {
            case Kundenart.Kaufmann:
                return ("Kaufmann – wechselt Münzen", "ein auswärtiger Händler vom Markt");
            case Kundenart.Buerger:
                return (kunde.Anliegen == Kundenanliegen.Leihen ? "Bürger – leiht Geld" : "Bürger – zahlt zurück",
                        w is { Bank.IstAktiv: true } ? $"zum Stadtzins von {Zahlenformat.Ganz(w.Bank.Daten.Stadtzins * 100)} % im Jahr" : "");
            default:
                var haus = kunde.Haus?.Inhaber ?? "Ein vornehmer Gast";
                if (kunde.Anliegen == Kundenanliegen.Kredit)
                {
                    var kredit = w?.Bank.KrediteVon(a.Betrieb.Konto).FirstOrDefault(k => k.Nehmer == kunde.Haus);
                    return ($"{haus} – verhandelt einen Kredit", kredit is null ? "" : $"schuldet Euch noch {Zahlenformat.Ganz(kredit.Rest)} {waehrung} zu {Zahlenformat.Ganz(kredit.Satz * 100)} %");
                }
                return ($"{haus} – lässt sich beraten", "ein Patrizier der Stadt");
        }
    }
}
