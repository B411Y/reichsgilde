using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Gäste und Barde in der Schänke – Teil der <c>partial class Stadtansicht</c> (PR #166).
///
/// Das Modell (<c>wirt_innen.glb</c>) stellt Sitzplätze: <c>Gast_&lt;n&gt;</c> in der Schankstube (Füllreihenfolge: erst an der Theke, dann zum Kamin, zuletzt vorn rechts),
/// <c>Gast_Wuerfel_&lt;n&gt;</c> am Würfeltisch (ab Stufe 2), <c>Gast_Stamm_&lt;n&gt;</c> am Stammtisch im Saal (ab Stufe 3) und <c>Barde_1</c> auf der Bühne. Wer dort sitzt,
/// sagt die Gastwirtschaft des Kerns (<see cref="Gastwirtschaft.GaesteJetzt"/> und Verwandte, nach der Spieluhr, ohne Zufall): Das Spiel setzt nur Figuren auf die ersten
/// <c>n</c> Plätze. Der Krug ist ein Teil der Figur (Bewegung „Trinken“); die Anker <c>Krug_&lt;n&gt;</c> im Modell zeigen nur, wo er auf dem Tisch steht.
/// </summary>
public partial class Stadtansicht
{
    private enum GastTisch { Raum, Wuerfel, Stamm, Barde }

    /// <summary>Ein Sitzplatz (oder die Bühne des Barden) und die Figur, die gerade darauf sitzt. Die Figur wird beim ersten Gebrauch gebaut und bleibt dann dieselbe.</summary>
    private sealed class GastPlatz
    {
        public required GastTisch Tisch { get; init; }
        public required int Nr { get; init; }
        public required Node3D Anker { get; init; }
        public Node3D? Halter { get; set; }
        public Figur? Figur { get; set; }
        public CollisionShape3D? Klick { get; set; }
        public bool Da { get; set; }
    }

    /// <summary>Wie viele Gäste in der Schankstube, am Würfeltisch und am Stammtisch sitzen und ob der Barde spielt (-1 bei der Schankstube: noch nie gefragt).</summary>
    private sealed record GastenStand(int Raum, int Wuerfel, int Stamm, bool Barde);

    /// <summary>So oft (Sekunden, echte Zeit) fragt die Darstellung den Kern nach den Gästen – die Zahlen ändern sich höchstens mit der Spielminute.</summary>
    private const double GaesteAbfrageAlle = 0.4;

    /// <summary>Am Stammtisch im Saal sitzen immer alle Ratsherren (sechs Plätze im Modell).</summary>
    private const int StammPlaetze = 6;

    /// <summary>Der Sitzplatz zu einem Knoten <c>Gast_&lt;n&gt;</c>, <c>Gast_Wuerfel_&lt;n&gt;</c>, <c>Gast_Stamm_&lt;n&gt;</c> oder <c>Barde_1</c>; null bei jedem anderen Namen.</summary>
    private static GastPlatz? GastPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        var nummer = teile.Length > 1 && int.TryParse(teile[^1], out var nr) ? nr : 0;
        if (nummer < 1) return null;
        GastTisch? tisch = (teile[0], teile.Length) switch
        {
            ("Gast", 2) => GastTisch.Raum,
            ("Gast", 3) when teile[1] == "Wuerfel" => GastTisch.Wuerfel,
            ("Gast", 3) when teile[1] == "Stamm" => GastTisch.Stamm,
            ("Barde", 2) => GastTisch.Barde,
            _ => null,
        };
        return tisch is { } t ? new GastPlatz { Tisch = t, Nr = nummer, Anker = knoten } : null;
    }

    /// <summary>
    /// Setzt die Gäste nach dem Kern: alle halbe Sekunde fragen, nur bei einer neuen Zahl Figuren zeigen oder verstecken; die Gäste trinken mit verschobener Phase
    /// und eigenem Tempo, der Barde spielt die Laute. Wer nicht im gezeigten Raum sitzt, wird nicht bewegt. Ohne Plätze im Modell tut die Funktion nichts.
    /// </summary>
    private void InnenGaesteAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, double delta)
    {
        if (md.Gaeste.Count == 0 || _simulation?.Wirtschaft?.Gastwirtschaft is not { } kern) return;
        md.GaesteZeit += delta;
        if (md.GaesteStand.Raum < 0 || md.GaesteZeit >= GaesteAbfrageAlle)
        {
            md.GaesteZeit = 0;
            var raum = kern.GaesteJetzt(b);
            var stand = new GastenStand(raum, kern.WuerfeltischBesetzt(b) ? Math.Clamp(raum / 3, 2, 4) : 0, kern.StammtischBesetzt(b) ? StammPlaetze : 0, kern.BardeSpielt(b));
            if (stand != md.GaesteStand)
            {
                md.GaesteStand = stand;
                foreach (var platz in md.Gaeste)
                    GastSetzen(a, platz, platz.Tisch switch
                    {
                        GastTisch.Raum => platz.Nr <= stand.Raum,
                        GastTisch.Wuerfel => platz.Nr <= stand.Wuerfel,
                        GastTisch.Stamm => platz.Nr <= stand.Stamm,
                        _ => stand.Barde,
                    });
            }
        }
        var bewegt = !_simulation.Takt.IstPausiert;
        foreach (var platz in md.Gaeste)
        {
            if (platz.Klick is { } klick && klick.Disabled == platz.Da) klick.Disabled = !platz.Da; // Einsteigen setzt alle Klickflächen an: abgleichen
            if (platz.Figur is not { } figur || !platz.Da) continue;
            if (_raum is null || !IstImGezeigtenRaum(platz.Anker)) continue;
            if (bewegt || figur.Neu) FigurImBildBewegen(figur, null, bewegt ? delta : 0);
        }
    }

    /// <summary>Lässt einen Gast kommen oder gehen (beim ersten Kommen wird die Figur gebaut).</summary>
    private void GastSetzen(BetriebsAnsicht a, GastPlatz platz, bool da)
    {
        platz.Da = da;
        if (da && platz.Halter is null) GastBauen(a, platz);
        if (platz.Halter is not { } halter) return;
        halter.Visible = da;
        if (platz.Klick is { } klick) klick.Disabled = !da;
    }

    private void GastBauen(BetriebsAnsicht a, GastPlatz platz)
    {
        var barde = platz.Tisch == GastTisch.Barde;
        var nummer = 200 + (int)platz.Tisch * 20 + platz.Nr; // Startphase, Tempo und Aussehen bleiben je Platz gleich
        // Der Anker blickt mit −Z zum Tisch (zur Saalmitte), die Figur mit +Z: um 180° gedreht einsetzen.
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0), Visible = false };
        platz.Anker.AddChild(halter);
        if (FigurEinsetzen(halter, GastAussehen(platz.Tisch, nummer), nummer) is not { } figur)
        {
            halter.QueueFree();
            return;
        }
        // Sitzende haben die Hüfte auf Sitzhöhe des Menschen (kleine Völker sitzen höher, Orks tiefer – wie bei der Feinarbeit).
        if (!barde) halter.Position = new Vector3(0, SitzKniehoehe - figur.Kniehoehe, 0);
        var tempo = 0.85f + (Streuung(nummer, 17) % 31) / 100f; // 0,85 … 1,15: die Gäste trinken nicht im Gleichschritt
        BewegungSetzen(figur, barde ? "Laute" : "Trinken", barde ? 1f : tempo);
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", barde ? "barde" : "gast");
        var hoehe = barde ? 1.75f : 1.3f;
        var klick = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.36f, Height = hoehe }, Position = new Vector3(0, hoehe / 2 + (barde ? 0 : 0.1f), 0), Disabled = true };
        koerper.AddChild(klick);
        halter.AddChild(koerper);
        a.Raumklicks.Add(klick);
        RaumEbeneUebernehmen(halter);
        platz.Halter = halter;
        platz.Figur = figur;
        platz.Klick = klick;
    }

    /// <summary>
    /// Aussehen eines Gastes, fest je Platz: in der Schankstube bunt gemischt (meist Menschen, dazu Zwerge, Halblinge, Elben und Orks, einfache Tracht, Männer oft mit Barett oder Strohhut),
    /// am Stammtisch die Ratsherren (rotes Gewand, Barett), der Barde ein Elb in Blaugrün.
    /// </summary>
    private static FigurAussehen GastAussehen(GastTisch tisch, int nummer)
    {
        if (tisch == GastTisch.Barde)
        {
            var elb = Haarfarben(Volk.Elb);
            return new FigurAussehen(Volk.Elb, false, new Color(0.22f, 0.36f, 0.34f), Beinkleidfarben[3], HautVon(Volk.Elb), elb[0], Schwarz, Kopfbedeckung.Barett);
        }
        var wahl = Streuung(nummer, 71) % 12;
        var volk = tisch == GastTisch.Stamm ? Volk.Mensch : wahl switch { 6 or 7 => Volk.Zwerg, 8 => Volk.Halbling, 9 => Volk.Elb, 10 => Volk.Ork, _ => Volk.Mensch };
        var frau = tisch != GastTisch.Stamm && Streuung(nummer, 53) % 4 == 0;
        var haare = Haarfarben(volk);
        var kopf = tisch == GastTisch.Stamm ? Kopfbedeckung.Barett
            : frau ? Kopfbedeckung.Haube
            : (Streuung(nummer, 29) % 5) switch { 0 => Kopfbedeckung.Barett, 1 => Kopfbedeckung.Strohhut, 2 => Kopfbedeckung.Kapuze, _ => Kopfbedeckung.Keine };
        var gewand = tisch == GastTisch.Stamm ? Ratsrot : Gewandfarben[(int)(Streuung(nummer, 7) % Gewandfarben.Length)];
        return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk), haare[nummer * 5 % haare.Length],
            frau ? Leinenfarbe : tisch == GastTisch.Stamm ? Schwarz : gewand, kopf);
    }
}
