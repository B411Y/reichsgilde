using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Gläubige und Prediger im Kirchenschiff – Teil der <c>partial class Stadtansicht</c> (PR #167), Vorbild sind die Gäste der Schänke (Stadtansicht.Innengaeste.cs).
///
/// Das Modell (<c>prediger_innen.glb</c>) stellt Plätze: <c>Glaeubig_&lt;n&gt;</c> auf den Bänken (Füllreihenfolge vorn → hinten, Blick zum Altar), <c>Predigt_1</c> auf der Kanzel und
/// <c>Beichte_1</c> auf der Kniebank am Beichtstuhl. Wer dort sitzt, steht und kniet, sagt das Kirchenwesen des Kerns (<c>GlaeubigeJetzt</c>, <c>MesseLaeuft</c>, <c>BeichteJetzt</c>, nach der
/// Spieluhr, ohne Zufall): Das Spiel setzt nur Figuren auf die ersten <c>n</c> Bankplätze. Die Betenden sitzen mit gefalteten Händen (Bewegung „Beten“), der Beichtende kniet („Knien“).
///
/// <b>Der Prediger auf der Kanzel</b> ist eine eigene Figur (Kutte, wie ein Barde), kein Arbeiter des Hauses: Die Arbeiter bleiben an ihren Plätzen in Sakristei und Skriptorium, und ihre
/// Klickflächen, Auswahl und Werkstücke bleiben unberührt. Er steht, solange die Messe läuft (<c>MesseLaeuft</c>: zur Stunde der Messe, mit mindestens einem Arbeiter im Haus) und predigt („Predigen“).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wie viele Gläubige auf den Bänken sitzen, ob der Prediger auf der Kanzel steht und ob jemand beichtet (-1 bei den Bänken: noch nie gefragt).</summary>
    private sealed record GlaeubigenStand(int Baenke, bool Predigt, bool Beichte);

    /// <summary>So oft (Sekunden, echte Zeit) fragt die Darstellung den Kern nach den Gläubigen – die Zahlen ändern sich höchstens mit der Spielminute.</summary>
    private const double GlaeubigenAbfrageAlle = 0.4;

    /// <summary>Der Platz zu einem Knoten <c>Glaeubig_&lt;n&gt;</c>, <c>Predigt_&lt;n&gt;</c> oder <c>Beichte_&lt;n&gt;</c>; null bei jedem anderen Namen.</summary>
    private static GastPlatz? GlaeubigPlatzVon(Node3D knoten)
    {
        var teile = knoten.Name.ToString().Split('_');
        if (teile.Length != 2 || !int.TryParse(teile[1], out var nummer) || nummer < 1) return null;
        GastTisch? tisch = teile[0] switch { "Glaeubig" => GastTisch.Bank, "Predigt" => GastTisch.Kanzel, "Beichte" => GastTisch.Beichte, _ => null };
        return tisch is { } t ? new GastPlatz { Tisch = t, Nr = nummer, Anker = knoten } : null;
    }

    /// <summary>
    /// Setzt die Gläubigen nach dem Kern: alle halbe Sekunde fragen, nur bei einer neuen Zahl Figuren zeigen oder verstecken. Wer nicht im gezeigten Raum sitzt, wird nicht bewegt.
    /// Ohne Plätze im Modell tut die Funktion nichts.
    /// </summary>
    private void InnenGlaeubigeAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, double delta)
    {
        if (md.Glaeubige.Count == 0 || _simulation?.Wirtschaft?.Kirchenwesen is not { } kern) return;
        md.GlaeubigenZeit += delta;
        if (md.GlaeubigenStand.Baenke < 0 || md.GlaeubigenZeit >= GlaeubigenAbfrageAlle)
        {
            md.GlaeubigenZeit = 0;
            var stand = new GlaeubigenStand(kern.GlaeubigeJetzt(b), kern.MesseLaeuft(b), kern.BeichteJetzt(b));
            if (stand != md.GlaeubigenStand)
            {
                md.GlaeubigenStand = stand;
                foreach (var platz in md.Glaeubige)
                    GlaeubigSetzen(a, platz, platz.Tisch switch
                    {
                        GastTisch.Bank => platz.Nr <= stand.Baenke,
                        GastTisch.Kanzel => stand.Predigt,
                        _ => stand.Beichte,
                    });
            }
        }
        var bewegt = !_simulation.Takt.IstPausiert;
        foreach (var platz in md.Glaeubige)
        {
            if (platz.Klick is { } klick && klick.Disabled == platz.Da) klick.Disabled = !platz.Da; // Einsteigen setzt alle Klickflächen an: abgleichen
            if (platz.Figur is not { } figur || !platz.Da) continue;
            if (_raum is null || !IstImGezeigtenRaum(platz.Anker)) continue;
            if (bewegt || figur.Neu) FigurImBildBewegen(figur, null, bewegt ? delta : 0);
        }
    }

    /// <summary>Lässt einen Gläubigen kommen oder gehen (beim ersten Kommen wird die Figur gebaut).</summary>
    private void GlaeubigSetzen(BetriebsAnsicht a, GastPlatz platz, bool da)
    {
        platz.Da = da;
        if (da && platz.Halter is null) GlaeubigBauen(a, platz);
        if (platz.Halter is not { } halter) return;
        halter.Visible = da;
        if (platz.Klick is { } klick) klick.Disabled = !da;
    }

    private void GlaeubigBauen(BetriebsAnsicht a, GastPlatz platz)
    {
        var nummer = 300 + (int)platz.Tisch * 20 + platz.Nr; // Startphase, Tempo und Aussehen bleiben je Platz gleich
        // Der Anker blickt mit −Z zum Altar (zur Gemeinde), die Figur mit +Z: um 180° gedreht einsetzen.
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0), Visible = false };
        platz.Anker.AddChild(halter);
        if (FigurEinsetzen(halter, GlaeubigAussehen(platz.Tisch, nummer), nummer) is not { } figur)
        {
            halter.QueueFree();
            return;
        }
        // Wer auf der Bank sitzt, hat die Hüfte auf Sitzhöhe des Menschen (kleine Völker sitzen höher, Orks tiefer – wie die Gäste der Schänke); Kanzel und Kniebank brauchen keinen Ausgleich.
        if (platz.Tisch == GastTisch.Bank) halter.Position = new Vector3(0, SitzKniehoehe - figur.Kniehoehe, 0);
        var tempo = platz.Tisch == GastTisch.Bank ? 0.85f + (Streuung(nummer, 17) % 31) / 100f : 1f; // 0,85 … 1,15: die Betenden atmen nicht im Gleichschritt
        BewegungSetzen(figur, platz.Tisch switch { GastTisch.Kanzel => "Predigen", GastTisch.Beichte => "Knien", _ => "Beten" }, tempo);
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", platz.Tisch == GastTisch.Kanzel ? "prediger" : "glaeubiger");
        var hoehe = platz.Tisch switch { GastTisch.Kanzel => 1.75f, GastTisch.Bank => 1.3f, _ => 1.1f };
        var klick = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.36f, Height = hoehe }, Position = new Vector3(0, hoehe / 2 + (platz.Tisch == GastTisch.Kanzel ? 0 : 0.1f), 0), Disabled = true };
        koerper.AddChild(klick);
        halter.AddChild(koerper);
        a.Raumklicks.Add(klick);
        RaumEbeneUebernehmen(halter);
        platz.Halter = halter;
        platz.Figur = figur;
        platz.Klick = klick;
    }

    /// <summary>
    /// Aussehen, fest je Platz: der Prediger ein älterer Mensch in heller Kutte, die Gläubigen wie die Einwohner der Stadt (meist Menschen, dazu Zwerge, Halblinge, Elben und Orks) in einfacher
    /// Tracht, Frauen mit Haube oder Schleier, Männer meist barhäuptig (in der Kirche nimmt man den Hut ab), manche mit grauem Haar.
    /// </summary>
    private static FigurAussehen GlaeubigAussehen(GastTisch tisch, int nummer)
    {
        if (tisch == GastTisch.Kanzel)
        {
            var kutte = new Color(0.88f, 0.85f, 0.76f);
            return new FigurAussehen(Volk.Mensch, false, kutte, Beinkleidfarben[0], HautVon(Volk.Mensch), GrauesHaar, kutte);
        }
        var wahl = Streuung(nummer, 71) % 12;
        var volk = wahl switch { 6 or 7 => Volk.Zwerg, 8 => Volk.Halbling, 9 => Volk.Elb, 10 => Volk.Ork, _ => Volk.Mensch };
        var frau = Streuung(nummer, 53) % 5 < 2;
        var haare = Haarfarben(volk);
        var kopf = frau ? (Streuung(nummer, 29) % 2 == 0 ? Kopfbedeckung.Haube : Kopfbedeckung.Schleier)
            : (Streuung(nummer, 29) % 6) switch { 0 => Kopfbedeckung.Barett, 1 => Kopfbedeckung.Kapuze, _ => Kopfbedeckung.Keine };
        var gewand = Gewandfarben[(int)(Streuung(nummer, 7) % Gewandfarben.Length)];
        return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[nummer * 3 % Beinkleidfarben.Length], HautVon(volk),
            Streuung(nummer, 11) % 6 == 0 ? GrauesHaar : haare[nummer * 5 % haare.Length], frau ? Leinenfarbe : gewand, kopf);
    }
}
