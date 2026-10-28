using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ordnung;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Wachleute und die Gefangenen im Wachhaus – Teil der <c>partial class Stadtansicht</c> (PR #173), nach dem Vorbild der Diebe (Stadtansicht.Innendiebe.cs).
///
/// Das Modell (<c>gardist_innen.glb</c>) stellt Plätze: <c>Wache_&lt;n&gt;</c> mit den extras „taetigkeit“ (wie <see cref="Wachtaetigkeit"/> im Kern: Wuerfeln, Putzen, Schaerfen, Wachbuch, Schlafen, Fechten),
/// „bewegung“ (Zaehlen, Putzen, Schleifen, Feinarbeit, Schreiben, Schlafen, Fechten), „platz“ (Füllreihenfolge je Tätigkeit), „stufe“ (die Übungshalle gibt es ab Stufe 2) und – wer sitzt – „sitzhoehe“;
/// dazu <c>Hauptmann_1</c> (Schreibtisch der Hauptmannsstube, ab Stufe 3) und <c>Gefangen_&lt;n&gt;</c> in der Arrestzelle. Wer gerade wo ist und was er tut, sagt der Kern
/// (<see cref="Wachwesen.AnwesenheitJetzt"/>, nach der Spieluhr, ohne Zufall): Das Spiel setzt die Arbeiter, die <see cref="Wachort.Daheim"/> sind, auf einen freien Platz ihrer Tätigkeit. Wer auf Streife
/// oder beim Geleit ist, steht nicht im Raum.
///
/// Die Wachleute sind die Arbeiter des Betriebs: anklickbar und auswählbar wie in den anderen Werkstätten. Die Gefangenen (<see cref="Wachwesen.Gefangene"/>: die Taschendiebe, für die das Haus
/// einen Beweis hat) sind dagegen wie die Gläubigen nur Figuren mit einem Namen unter der Maus; sie bleiben, solange sie in der Liste stehen, auf ihrem Platz.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Platz des Modells (<c>Wache_&lt;n&gt;</c> oder <c>Hauptmann_1</c>) und der Wachmann, der darauf sitzt, steht oder liegt.</summary>
    private sealed class WachPlatz
    {
        /// <summary>Name der <see cref="Wachtaetigkeit"/> des Platzes („Wuerfeln“, „Putzen“, „Schaerfen“, „Wachbuch“, „Schlafen“, „Fechten“, „Befehligen“).</summary>
        public required string Gruppe { get; init; }
        /// <summary>Füllreihenfolge innerhalb der Tätigkeit (extras „platz“).</summary>
        public required int Reihe { get; init; }
        public required string Bewegung { get; init; }
        public required bool Sitzend { get; init; }
        public required int AbStufe { get; init; }
        public required Node3D Anker { get; init; }
        public ModellWache? Besetzt { get; set; }
    }

    /// <summary>Ein Arbeiter des Wachhauses als Figur. Die Figur wird beim ersten Kommen gebaut und bleibt dieselbe (so lange Art und Rang gleich bleiben).</summary>
    private sealed class ModellWache
    {
        public required int Nummer { get; init; }
        public required string Arbeitskraft { get; init; }
        public required bool Hauptmann { get; init; }
        public required Figur Figur { get; init; }
        public required Node3D Halter { get; init; }
        public required CollisionShape3D Klick { get; init; }
        /// <summary>Goldener Ring auf dem Boden: zeigt, wer ausgewählt ist.</summary>
        public required MeshInstance3D Ring { get; init; }
        public WachPlatz? Platz { get; set; }
        public Wachtaetigkeit Taetigkeit { get; set; }
    }

    /// <summary>Ein Platz in der Arrestzelle (<c>Gefangen_&lt;n&gt;</c>) und der Gauner, der darauf sitzt (null: leer).</summary>
    private sealed class GefangenPlatz
    {
        public required int Reihe { get; init; }
        public required bool Sitzend { get; init; }
        public required Node3D Anker { get; init; }
        public Gefangener? Gefangener { get; set; }
        public Node3D? Halter { get; set; }
        public Figur? Figur { get; set; }
        public CollisionShape3D? Klick { get; set; }
    }

    /// <summary>So oft (Sekunden, echte Zeit) fragt die Darstellung den Kern, wer im Haus ist und wer in der Zelle sitzt – die Antwort ändert sich höchstens mit der Spielminute.</summary>
    private const double WachAbfrageAlle = 0.4;

    /// <summary>Der Platz zu einem Knoten <c>Wache_&lt;n&gt;</c> oder <c>Hauptmann_1</c>; null bei jedem anderen Namen.</summary>
    private static WachPlatz? WachPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        if (teile.Length != 2 || !int.TryParse(teile[1], out var nummer) || nummer < 1) return null;
        var gruppe = ExtraText(knoten, "taetigkeit");
        var bewegung = ExtraText(knoten, "bewegung");
        if (gruppe == "" || bewegung == "") return null;
        return new WachPlatz
        {
            Gruppe = gruppe, Reihe = ExtraZahl(knoten, "platz", nummer), Bewegung = bewegung, Anker = knoten,
            Sitzend = Extras(knoten) is { } extras && extras.ContainsKey("sitzhoehe"),
            AbStufe = ExtraZahl(knoten, "stufe", 1),
        };
    }

    /// <summary>Der Platz zu einem Knoten <c>Gefangen_&lt;n&gt;</c>; null bei jedem anderen Namen.</summary>
    private static GefangenPlatz? GefangenPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        if (teile.Length != 2 || !int.TryParse(teile[1], out var nummer) || nummer < 1) return null;
        return new GefangenPlatz { Reihe = ExtraZahl(knoten, "platz", nummer), Sitzend = Extras(knoten) is { } extras && extras.ContainsKey("sitzhoehe"), Anker = knoten };
    }

    /// <summary>
    /// Setzt Wachleute und Gefangene nach dem Kern: alle halbe Sekunde fragen, nur bei einer neuen Verteilung Figuren setzen, zeigen oder verstecken; im Bild laufen nur die Figuren im gezeigten Raum.
    /// Ohne Plätze im Modell oder ohne Abfrage tut die Funktion nichts.
    /// </summary>
    private void InnenWacheAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, int stufe, double delta)
    {
        if (_simulation?.Wirtschaft is not { } w) return;
        md.WachZeit += delta;
        if (md.WachStand is null || md.WachZeit >= WachAbfrageAlle || delta <= 0)
        {
            md.WachZeit = 0;
            var liste = w.Wache.AnwesenheitJetzt(b);
            var gefangene = w.Wache.Gefangene(b);
            var stand = stufe + "|" + string.Join(",", liste.Select(s => $"{s.Arbeiter.Nummer}{s.Arbeiter.Arbeitskraft}{(int)s.Ort}{(int)s.Taetigkeit}{(s.Hauptmann ? "H" : "")}"))
                        + "|" + string.Join(",", gefangene.Select(g => g.Spur));
            if (stand != md.WachStand)
            {
                md.WachStand = stand;
                WachleuteSetzen(a, md, liste, stufe);
                GefangeneSetzen(a, md, gefangene);
            }
        }

        var bewegt = !_simulation.Takt.IstPausiert;
        foreach (var wache in md.Wachen.Values)
        {
            var da = wache.Platz is not null;
            if (wache.Klick.Disabled == da) wache.Klick.Disabled = !da; // Einsteigen setzt alle Klickflächen an: abgleichen
            var gewaehlt = _gewaehlt.Contains(wache.Nummer);
            FigurHervorheben(wache.Figur, da && (gewaehlt || _hoverArbeiter == wache.Nummer));
            if (wache.Ring.Visible != (da && gewaehlt)) wache.Ring.Visible = da && gewaehlt;
            if (wache.Platz is not { } platz || _raum is null || !IstImGezeigtenRaum(wache.Halter)) continue;
            var takt = RaumTakt.GetValueOrDefault((a.Stil, platz.Bewegung));
            if (takt > 0 && md.Animation is not null)
            {
                // Am Tretschleifbock läuft die Figur im Takt des Schleifsteins (Animation „Arbeit“ des Raums), damit Fuß und Kurbel zum Stein passen.
                wache.Figur.Phase = md.ArbeitPhase % takt / takt * wache.Figur.Dauer;
                FigurBewegen(wache.Figur, 0);
            }
            else if (bewegt || wache.Figur.Neu) FigurImBildBewegen(wache.Figur, null, bewegt ? delta : 0);
        }
        foreach (var platz in md.Zellenplaetze)
        {
            if (platz.Klick is { } klick && klick.Disabled == (platz.Gefangener is not null)) klick.Disabled = platz.Gefangener is null; // wie die Gläubigen: abgleichen
            if (platz.Figur is not { } figur || platz.Gefangener is null || _raum is null || !IstImGezeigtenRaum(platz.Anker)) continue;
            if (bewegt || figur.Neu) FigurImBildBewegen(figur, null, bewegt ? delta : 0);
        }
    }

    /// <summary>Verteilt die Arbeiter, die daheim sind, auf die Plätze ihrer Tätigkeit (wer seinen Platz behalten kann, behält ihn) und räumt auf, wer gegangen ist.</summary>
    private void WachleuteSetzen(BetriebsAnsicht a, ModellRaumDaten md, IReadOnlyList<Wachstand> liste, int stufe)
    {
        var daheim = liste.Where(s => s.Ort == Wachort.Daheim).ToDictionary(s => s.Arbeiter.Nummer);
        var gewaehltGeaendert = false;
        foreach (var wache in md.Wachen.Values.ToList())
        {
            var stand = liste.FirstOrDefault(s => s.Arbeiter.Nummer == wache.Nummer);
            if (stand is null || stand.Arbeiter.Arbeitskraft != wache.Arbeitskraft || stand.Hauptmann != wache.Hauptmann)
            {
                // nicht mehr im Betrieb oder befördert (neues Gewand): die Figur wird neu gebaut
                WacheEntfernen(a, md, wache);
                gewaehltGeaendert |= stand is null && _gewaehlt.Remove(wache.Nummer);
            }
            else if (wache.Platz is { } platz && (!daheim.TryGetValue(wache.Nummer, out var s) || platz.Gruppe != s.Taetigkeit.ToString()))
            {
                platz.Besetzt = null; // geht auf Streife oder ins Geleit, oder beginnt etwas anderes
                wache.Platz = null;
            }
        }
        foreach (var stand in daheim.Values)
        {
            var wache = md.Wachen.GetValueOrDefault(stand.Arbeiter.Nummer);
            if (wache?.Platz is null)
            {
                var gruppe = stand.Taetigkeit.ToString();
                var platz = md.Wachplaetze.FirstOrDefault(p => p.Gruppe == gruppe && p.Besetzt is null && p.AbStufe <= stufe);
                if (platz is not null)
                {
                    wache ??= WacheBauen(a, md, stand, platz);
                    if (wache is not null) WacheAufPlatz(wache, platz);
                }
            }
            if (wache is not null) wache.Taetigkeit = stand.Taetigkeit;
        }
        foreach (var wache in md.Wachen.Values) wache.Halter.Visible = wache.Platz is not null;
        md.WachWach = daheim.Values.Any(s => s.Taetigkeit != Wachtaetigkeit.Schlafen);
        md.WachBewegtRaum = md.Wachplaetze.Any(p => p.Besetzt is not null && p.Bewegung == "Schleifen");
        if (gewaehltGeaendert) ArbeiterAuswahlGeaendert?.Invoke();
    }

    private void WacheEntfernen(BetriebsAnsicht a, ModellRaumDaten md, ModellWache wache)
    {
        if (wache.Platz is { } platz) platz.Besetzt = null;
        a.Raumklicks.Remove(wache.Klick);
        wache.Halter.GetParent()?.RemoveChild(wache.Halter);
        wache.Halter.QueueFree();
        md.Wachen.Remove(wache.Nummer);
    }

    /// <summary>Baut die Figur eines Arbeiters (null, wenn das Figurenmodell fehlt). Sie hängt zuerst am Anker des Platzes, auf den sie kommt.</summary>
    private ModellWache? WacheBauen(BetriebsAnsicht a, ModellRaumDaten md, Wachstand stand, WachPlatz platz)
    {
        var nummer = stand.Arbeiter.Nummer;
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0), Visible = false }; // der Anker blickt mit −Z auf sein Ziel, die Figur mit +Z: um 180° gedreht einsetzen
        platz.Anker.AddChild(halter);
        if (FigurEinsetzen(halter, WacheAussehen(nummer, stand.Arbeiter.Arbeitskraft, stand.Hauptmann), nummer) is not { } figur)
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
        var wache = new ModellWache { Nummer = nummer, Arbeitskraft = stand.Arbeiter.Arbeitskraft, Hauptmann = stand.Hauptmann, Figur = figur, Halter = halter, Klick = form, Ring = ring };
        md.Wachen[nummer] = wache;
        return wache;
    }

    /// <summary>Setzt die Figur auf den Platz: Anker, Sitzhöhe, Bewegung (Lappen beim Putzen, Übungsschwert beim Fechten), Klickfläche und Render-Ebene des Raums.</summary>
    private void WacheAufPlatz(ModellWache wache, WachPlatz platz)
    {
        platz.Besetzt = wache;
        wache.Platz = platz;
        var halter = wache.Halter;
        if (halter.GetParent() != platz.Anker)
        {
            halter.GetParent()?.RemoveChild(halter);
            platz.Anker.AddChild(halter);
        }
        halter.Rotation = new Vector3(0, Mathf.Pi, 0);
        // Wer sitzt, hat die Hüfte auf Sitzhöhe des Menschen (kleine Völker sitzen höher, Orks tiefer); wer liegt oder fechtend steht, wird nicht angehoben.
        halter.Position = platz.Sitzend ? new Vector3(0, SitzKniehoehe - wache.Figur.Kniehoehe, 0) : Vector3.Zero;
        var tempo = platz.Bewegung == "Schleifen" ? 1f : 0.85f + (Streuung(wache.Nummer, 17) % 31) / 100f; // 0,85 … 1,15: nicht im Gleichschritt
        BewegungSetzen(wache.Figur, platz.Bewegung, tempo);
        // Klickfläche: stehend und sitzend eine Kapsel, liegend ein flacher Quader über der Länge der Figur
        if (platz.Bewegung == "Schlafen")
        {
            wache.Klick.Shape = new BoxShape3D { Size = new Vector3(0.7f, 0.4f, 1.8f) };
            wache.Klick.Position = new Vector3(0, 0.3f, 0);
        }
        else
        {
            var hoehe = platz.Sitzend ? 1.3f : 1.75f;
            wache.Klick.Shape = new CapsuleShape3D { Radius = platz.Sitzend ? 0.36f : 0.38f, Height = hoehe };
            wache.Klick.Position = new Vector3(0, hoehe / 2 + (platz.Sitzend ? 0.1f : 0), 0);
        }
        wache.Ring.Scale = platz.Bewegung == "Schlafen" ? new Vector3(1.2f, 1, 2.2f) : Vector3.One;
        RaumEbeneUebernehmen(halter);
    }

    /// <summary>
    /// Setzt die Gefangenen in die Zelle: wer schon sitzt (gleiche Spur), bleibt auf seinem Platz, wer nicht mehr in der Liste steht, geht, neue nehmen den ersten freien Platz.
    /// Die Figur wird beim ersten Kommen gebaut; kommt auf einen Platz ein anderer Gauner, bekommt der Platz eine neue Figur.
    /// </summary>
    private void GefangeneSetzen(BetriebsAnsicht a, ModellRaumDaten md, IReadOnlyList<Gefangener> gefangene)
    {
        foreach (var platz in md.Zellenplaetze)
            if (platz.Gefangener is { } alt && gefangene.All(g => g.Spur != alt.Spur))
            {
                platz.Gefangener = null;
                if (platz.Halter is { } halter) halter.Visible = false;
                if (platz.Klick is { } klick) klick.Disabled = true;
            }
        foreach (var g in gefangene)
        {
            if (md.Zellenplaetze.Any(p => p.Gefangener?.Spur == g.Spur))
            {
                md.Zellenplaetze.First(p => p.Gefangener?.Spur == g.Spur).Gefangener = g;
                continue;
            }
            var frei = md.Zellenplaetze.FirstOrDefault(p => p.Gefangener is null);
            if (frei is null) break;
            frei.Gefangener = g;
            GefangenBauen(a, frei, g);
        }
    }

    private void GefangenBauen(BetriebsAnsicht a, GefangenPlatz platz, Gefangener g)
    {
        if (platz.Halter is { } alt) // ein anderer Gauner sitzt jetzt hier: die alte Figur geht
        {
            a.Raumklicks.Remove(platz.Klick!);
            alt.GetParent()?.RemoveChild(alt);
            alt.QueueFree();
            platz.Halter = null;
            platz.Figur = null;
            platz.Klick = null;
        }
        var nummer = 400 + g.Spur; // Aussehen und Tempo bleiben je Gauner gleich
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0) };
        platz.Anker.AddChild(halter);
        if (FigurEinsetzen(halter, GefangenAussehen(nummer), nummer) is not { } figur)
        {
            halter.QueueFree();
            return;
        }
        if (platz.Sitzend) halter.Position = new Vector3(0, SitzKniehoehe - figur.Kniehoehe, 0);
        BewegungSetzen(figur, "Sitzen", 0.85f + (Streuung(nummer, 17) % 31) / 100f);
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", "gefangener:" + ZellenNummer(a, platz));
        const float hoehe = 1.3f;
        var klick = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.36f, Height = hoehe }, Position = new Vector3(0, hoehe / 2 + 0.1f, 0) };
        koerper.AddChild(klick);
        halter.AddChild(koerper);
        a.Raumklicks.Add(klick);
        RaumEbeneUebernehmen(halter);
        platz.Halter = halter;
        platz.Figur = figur;
        platz.Klick = klick;
    }

    /// <summary>Die Nummer des Platzes in der Zelle (für das Tag der Klickfläche; <see cref="GefangenerText"/> schlägt damit den Gauner nach).</summary>
    private static int ZellenNummer(BetriebsAnsicht a, GefangenPlatz platz) => a.Modellraum!.Zellenplaetze.IndexOf(platz);

    /// <summary>
    /// Aussehen eines Wachmanns, fest je Arbeiter: der Wappenrock der Stadt in Rot (Lehrlinge in blassem, ungefärbtem Rot), meist barhäuptig, manche mit dunklem Barett, Orks grün;
    /// der Hauptmann trägt ein dunkles Weinrot mit schwarzem Barett über grauem Haar.
    /// </summary>
    private static FigurAussehen WacheAussehen(int nummer, string arbeitskraft, bool hauptmann)
    {
        var volk = arbeitskraft == "Ork" ? Volk.Ork : Volk.Mensch;
        if (hauptmann)
            return new FigurAussehen(volk, false, new Color(0.46f, 0.07f, 0.09f), Beinkleidfarben[1], HautVon(volk), GrauesHaar, Schwarz, Kopfbedeckung.Barett);
        var rock = IstAnfaenger(arbeitskraft) ? new Color(0.62f, 0.34f, 0.30f) : Stadtrot.Lerp(new Color(0.55f, 0.10f, 0.10f), (Streuung(nummer, 7) % 5) / 8f);
        var haare = Haarfarben(volk);
        var frau = volk == Volk.Mensch && Streuung(nummer, 53) % 6 == 0;
        var kopf = Streuung(nummer, 29) % 4 == 0 ? Kopfbedeckung.Barett : Kopfbedeckung.Keine;
        return new FigurAussehen(volk, frau, rock, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk), haare[nummer * 5 % haare.Length], new Color(0.20f, 0.14f, 0.12f), kopf);
    }

    /// <summary>Aussehen eines Gefangenen: ein Taschendieb in ärmlichem, geflicktem Lumpengewand ohne Kapuze (wie die Lehrlinge der Diebe), mancher ein Ork.</summary>
    private static FigurAussehen GefangenAussehen(int nummer)
    {
        var volk = Streuung(nummer, 5) % 6 == 0 ? Volk.Ork : Volk.Mensch;
        var haare = Haarfarben(volk);
        var lumpen = new[] { new Color(0.32f, 0.29f, 0.25f), new Color(0.36f, 0.31f, 0.24f), new Color(0.28f, 0.28f, 0.27f), new Color(0.38f, 0.30f, 0.26f) };
        var gewand = lumpen[Streuung(nummer, 7) % (uint)lumpen.Length];
        return new FigurAussehen(volk, false, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk), haare[nummer * 5 % haare.Length], gewand);
    }

    /// <summary>Name und Hinweis unter der Maus bei einem Wachmann: Rang und Nummer, darunter was er gerade tut und seine Laune.</summary>
    private static (string Name, string Hinweis) WacheText(ModellRaumDaten md, ArbeiterZeile zeile, bool gewaehlt)
    {
        var wache = md.Wachen.GetValueOrDefault(zeile.Nummer);
        var was = wache?.Taetigkeit switch
        {
            Wachtaetigkeit.Putzen => "putzt Rüstung und Helm",
            Wachtaetigkeit.Fechten => "übt das Fechten an der Strohpuppe",
            Wachtaetigkeit.Wuerfeln => "würfelt am Tisch",
            Wachtaetigkeit.Schaerfen => "schärft Klingen und Hellebarden",
            Wachtaetigkeit.Wachbuch => "führt das Wachbuch",
            Wachtaetigkeit.Schlafen => "schläft",
            Wachtaetigkeit.Befehligen => "befehligt die Wache",
            _ => "ist im Haus",
        };
        return ($"{(wache?.Hauptmann == true ? "Hauptmann" : zeile.Arbeitskraft)} Nr. {zeile.Nummer}", $"{was} · Laune {Zahlenformat.Ganz(zeile.Laune)}" + (gewaehlt ? " · ausgewählt" : ""));
    }

    /// <summary>Name und Hinweis unter der Maus bei einem Gefangenen: Vergehen und Betroffener, darunter das Haus, dem er dient, und die Beute.</summary>
    private (string Name, string Hinweis) GefangenerText(BetriebsAnsicht a, int platz)
    {
        if (a.Modellraum is not { } md || platz < 0 || platz >= md.Zellenplaetze.Count || md.Zellenplaetze[platz].Gefangener is not { } g) return ("Gefangener", "");
        var waehrung = _simulation?.Wirtschaft?.Daten.Allgemein.Waehrung ?? "";
        var vergehen = g.Vergehen == Vergehen.Taschendiebstahl ? "Taschendiebstahl" : g.Vergehen.ToString();
        return ($"Gefangener – {vergehen} bei {g.Betroffener}", $"im Dienst von {g.Herkunft.Inhaber} · Beute {Zahlenformat.Ganz(g.Betrag)} {waehrung} · Beweis liegt vor");
    }
}
