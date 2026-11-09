using System.Diagnostics;
using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Probe „Weg A“ (Teil der <c>partial class Stadtansicht</c>): Passanten mit echtem Skelett aus MakeHuman/MPFB und Bewegungen von Quaternius
/// (<c>figuren/probe/passant_probe.glb</c> für Männer, <c>passant_probe_frau.glb</c> für Frauen, gebaut von <c>tools/modelle/passanten_probe.py</c>).
/// Nur mit der Startoption <c>--passantenprobe</c>.
///
/// <para>Zwei Stufen: nah (Zoom 25) ein echtes <see cref="Skeleton3D"/> mit <see cref="AnimationPlayer"/> je Figur – dieselbe <see cref="Figur"/>-Hülle
/// wie die Figuren aus Blender, also auch dieselben Sparregeln (<see cref="FigurImBildBewegen"/>) und die Oberflächendetails (Stoffe „tuch“ und
/// „leder“ der Material-Bibliothek, Schicht je Fläche in UV.y). Ab Zoom 50 (<see cref="FigurenHalbeRateAb"/>) zeichnet das Spiel die Probefiguren als
/// gebackene Vertex-Animation (VAT) in einem <see cref="MultiMesh"/> je Netz: Die Lage jeder Ecke in jedem Bild der sieben Clips liegt in einer
/// Textur, der Shader <c>figur_vat.gdshader</c> liest sie; die Skelette stehen dann still und unsichtbar. Gebacken wird beim Start im Spiel selbst
/// (eigene Lösung, kein Plugin), aus genau den Netzen, Gewichten und Clips der .glb.</para>
///
/// <para>Startoptionen: <c>--passantenprobe=1</c> ersetzt alle Passanten durch die Probefigur; <c>--passantenprobe=&lt;n&gt;</c> (n &gt; 1) zusätzlich
/// n Messfiguren, die um den Blickpunkt der Kamera beim Start hin und her gehen (zum Messen, z. B. 400); <c>--probestufe=auto|skelett|vat|vatfern</c>
/// erzwingt eine Stufe (Standard auto: Skelett bei Zoom 25, VAT nah ab 50, VAT fern ab 100).</para>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>
    /// Eine Probedatei: Varianten (Netze „Nah_&lt;variante&gt;“ und „Fern_&lt;variante&gt;“) und die Maße aus dem Bericht von passanten_probe.py:
    /// Hüfthöhe (Kopf des Oberschenkelknochens), Meter je Sekunde, die „Gehen“ und „Tragen“ bei Takt 1 schaffen (Standfuß: so rutschen die Füße
    /// nicht), Höhe des Gesäßes über den Sohlen beim Sitzen.
    /// </summary>
    private sealed record ProbeArtDaten(string Datei, string[] Varianten, float Huefte, float GehTempo, float TragTempo, float Sitzhoehe);

    private static readonly ProbeArtDaten[] ProbeArten =
    {
        new("figuren/probe/passant_probe", new[] { "cotte", "gugel", "rock", "bettler", "kutte" }, 0.933f, 1.287f, 0.898f, 0.446f),
        new("figuren/probe/passant_probe_frau", new[] { "haube", "gebende", "bettler" }, 0.822f, 1.118f, 0.786f, 0.389f),
    };

    /// <summary>Die Clips der Probe in der Reihenfolge ihrer Zeilen in der VAT-Textur (wie BEWEGUNGEN im Blender-Skript).</summary>
    private static readonly string[] ProbeKlips = { "Stehen", "Gehen", "Tragen", "Reden", "Sitzen", "Hacken", "Buecken", "Kauern", "Betteln" };

    /// <summary>Fußformen (eigene Netze „Fuss_&lt;form&gt;“, nah und fern dieselben): Halbschuh aus Leder, Holzschuh, barfuß (nur Bettler).</summary>
    private static readonly string[] ProbeFussformen = { "halb", "holz", "barfuss" };

    // „Gehen“ und „Tragen“ haben seit Runde 3 die Schrittweite für das Tempo der Passanten (im Blender-Skript verstärkt, SCHRITT): Takt nahe 1,
    // nicht gehetzt, und die Füße rutschen nicht (die Bremse ×0,7 aus Runde 2 ließ sie rutschen).

    /// <summary>Bilder je Sekunde der gebackenen Clips (wie in Blender).</summary>
    private const int ProbeVatBilder = 24;

    private static readonly string? ProbeOption = Wasserarten.Startoption("passantenprobe");
    /// <summary>Sind die Passanten Figuren mit Skelett (Weg A)? Seit Schritt 5 des Vollausbaus der Normalfall; <c>--passantenprobe=0</c> holt die
    /// Figuren aus Blender zurück (zum Vergleichen).</summary>
    private static readonly bool ProbeAktiv = ProbeOption is null or not ("0" or "aus");
    /// <summary>Mit ausdrücklichem <c>--passantenprobe</c> sind alle Passanten Probefiguren, auch die sonst vom KI-Schmied gestellten, und alle 10 s
    /// steht eine Messzeile im Protokoll.</summary>
    private static readonly bool ProbeErsetztAlle = ProbeOption is not null && ProbeAktiv;
    private static readonly int ProbeMessfigurenZahl = int.TryParse(ProbeOption, out var n) && n > 1 ? n : 0;
    private static readonly string ProbeStufe = Wasserarten.Startoption("probestufe") ?? "auto";
    /// <summary><c>--probeklip=&lt;Clip&gt;</c>: alle Messfiguren mit diesem Clip (z. B. Buecken, Kauern, Betteln), zum Ansehen einzelner Bewegungen.</summary>
    private static readonly string? ProbeKlipOption = Wasserarten.Startoption("probeklip");

    /// <summary>Zeichnet die Probe gerade gebacken (VAT) statt mit Skelett? Gilt für alle Probefiguren zugleich.</summary>
    private static bool _probeVat;
    private bool _probeFern, _probeVatVersucht;

    /// <summary>Wie eine Probefigur in der VAT-Stufe aussieht (Datei, Netz-Variante, Palettenplätze der fünf Farben).</summary>
    private sealed record ProbeTracht(int Art, int Variante, int Gewand, int Beinkleid, int Haut, int Haar, int Kopf, int Fuss, string[] Zubehoer);
    private readonly Dictionary<Figur, ProbeTracht> _probeTrachten = new();
    /// <summary>Alle Farben der Probefiguren (linear) für den VAT-Shader; Platz in der Liste = Index im Shader.</summary>
    private readonly List<Color> _probePalette = new();

    // Gedeckte Farben um 1400 (Pflanzenfarben, sRGB): Krapp, Waid, Wau, Walnuss, Eichenrinde, ungefärbte Wolle und Leinen, Loden, Waid mit Wau (grün)
    private static readonly Color[] ProbeGewandfarben =
    {
        new(0.45f, 0.22f, 0.17f), new(0.24f, 0.30f, 0.40f), new(0.56f, 0.48f, 0.27f), new(0.33f, 0.24f, 0.16f), new(0.40f, 0.33f, 0.22f),
        new(0.62f, 0.57f, 0.47f), new(0.40f, 0.39f, 0.35f), new(0.30f, 0.36f, 0.24f), new(0.32f, 0.22f, 0.26f), new(0.52f, 0.38f, 0.22f),
    };
    private static readonly Color[] ProbeBeinkleidfarben =
    {
        new(0.28f, 0.24f, 0.20f), new(0.22f, 0.22f, 0.23f), new(0.34f, 0.27f, 0.18f), new(0.25f, 0.27f, 0.32f), new(0.38f, 0.33f, 0.27f),
        new(0.42f, 0.24f, 0.19f),
    };
    /// <summary>Hauben und Gebende: ungebleichtes Leinen und gefärbtes Tuch. Klicktest NAS 09.11.: das Beige (0,62/0,56/0,44) lag zu nah an der Haut,
    /// die Haube las sich als Glatze; darum dunkler und kühler, dazu mehr gefärbte (Ocker, Blassblau, Rötlich, Moosgrün, Dunkelbraun). Am Modell
    /// setzen sich Hauben zusätzlich mit Saum und Haaransatz ab (passanten_probe.py).</summary>
    private static readonly Color[] ProbeLeinen =
    {
        new(0.50f, 0.47f, 0.38f), new(0.46f, 0.43f, 0.35f), new(0.55f, 0.52f, 0.43f),
        new(0.62f, 0.48f, 0.28f), new(0.40f, 0.50f, 0.60f), new(0.60f, 0.40f, 0.36f), new(0.38f, 0.46f, 0.32f), new(0.30f, 0.22f, 0.17f),
    };
    /// <summary>Natürliche Haarfarben (sRGB): dunkelbraun, braun, schwarz, hellbraun, dunkelblond, rotblond; Alte grau (Klicktest 08.11.: Haar wirkte weiß).</summary>
    private static readonly Color[] ProbeHaarfarben =
    {
        new(0.20f, 0.13f, 0.08f), new(0.30f, 0.20f, 0.12f), new(0.09f, 0.08f, 0.07f), new(0.40f, 0.28f, 0.16f), new(0.52f, 0.40f, 0.22f), new(0.48f, 0.26f, 0.13f),
    };
    private static readonly Color ProbeGrau = new(0.50f, 0.48f, 0.46f);

    /// <summary>Hautfarben je Volk (sRGB), bei Menschen etwas gestreut; kräftiger als die Hautfarbe der Blender-Figuren, die im Spiel fast weiß wirkte.</summary>
    private static Color ProbeHaut(Volk volk, int nummer) => volk switch
    {
        Volk.Elb => new Color(0.80f, 0.66f, 0.56f),
        Volk.Zwerg => new Color(0.72f, 0.50f, 0.40f),
        Volk.Halbling => new Color(0.75f, 0.55f, 0.43f),
        Volk.Ork => new Color(0.38f, 0.46f, 0.28f),
        Volk.Oger => new Color(0.48f, 0.45f, 0.40f),
        _ => (Streuung(nummer, 233) % 4) switch
        {
            0 => new Color(0.76f, 0.57f, 0.45f), 1 => new Color(0.70f, 0.51f, 0.40f), 2 => new Color(0.66f, 0.47f, 0.36f), _ => new Color(0.79f, 0.60f, 0.48f),
        },
    };

    /// <summary>
    /// Setzt eine Probefigur als Kind von <paramref name="eltern"/> ein: das Netz „Nah_&lt;variante&gt;“ und die Kiste („Last“, nur beim Tragen),
    /// eingefärbt über das Material der Tracht (<see cref="FigurStoff"/>, dieselben fünf Zonen) in gedeckten Pflanzenfarben. Frauen bekommen die
    /// Frauen-Datei; Völker sind vorerst nur gleichmäßig skaliert.
    /// </summary>
    private Figur? ProbefigurEinsetzen(Node3D eltern, FigurAussehen aussehen, int nummer, string? rolle = null)
    {
        var art = aussehen.Frau ? 1 : 0;
        var daten = ProbeArten[art];
        if (Modell(daten.Datei) is not { } szene) return null;
        var bettler = rolle == "Bettler";
        var variante = Array.IndexOf(daten.Varianten, bettler ? "bettler"
            : !aussehen.Frau && rolle == "Mönch" ? "kutte" : !aussehen.Frau && rolle == "Ratsherr" ? "rock" : "");
        if (variante < 0) variante = ProbeVarianteFuer(aussehen, nummer);
        var fuss = ProbeFussFuer(rolle, nummer);
        var zubehoer = ProbeZubehoerFuer(aussehen, daten.Varianten[variante]);
        // Woran man die Rolle erkennt, bleibt (Ratsherr rot, Mönch braun, Bote blau, Witwe schwarz, Bettler grau); sonst gedeckte Pflanzenfarben
        var eigeneFarbe = rolle is "Ratsherr" or "Bürgersohn" or "Mönch" or "Witwer" or "Bote" or "Bettler";
        // Kopffarbe: Schwarz (Barett, Witwe) und Weiß (Nonne) bleiben; Hauben und Schürzen in Leinen, Gugeln der Männer in Gewandfarben
        var festeKopffarbe = aussehen.Kopffarbe == Schwarz || aussehen.Kopffarbe.R > 0.85f;
        aussehen = aussehen with
        {
            Gewand = eigeneFarbe ? aussehen.Gewand : ProbeGewandfarben[Streuung(nummer, 211) % ProbeGewandfarben.Length],
            Beinkleid = ProbeBeinkleidfarben[Streuung(nummer, 223) % ProbeBeinkleidfarben.Length],
            Kopffarbe = festeKopffarbe ? aussehen.Kopffarbe
                : aussehen.Frau || aussehen.Schuerze ? ProbeLeinen[Streuung(nummer, 227) % ProbeLeinen.Length] : ProbeGewandfarben[Streuung(nummer, 229) % ProbeGewandfarben.Length],
            Haut = ProbeHaut(aussehen.Volk, nummer),
            Haar = aussehen.Haar == GrauesHaar ? ProbeGrau : ProbeHaarfarben[Streuung(nummer, 239) % ProbeHaarfarben.Length],
        };
        var (_, huefte, _, groesse) = FigurMasse(aussehen.Volk);
        // Der Frauenkörper ist schon kleiner gebaut: dieselben Völkerfaktoren, aber kein zweites Verkleinern
        var massstab = huefte / 0.92f * groesse.Y;
        var modell = szene.Instantiate<Node3D>();
        modell.Scale = Vector3.One * massstab;
        eltern.AddChild(modell);
        var spieler = modell.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().First();
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        if (_figurSchleifen.Add(daten.Datei))
            foreach (var name in spieler.GetAnimationList())
                spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        var figur = new Figur
        {
            Modell = modell, Spieler = spieler, Nummer = nummer, Probe = true, ProbeArt = art, ProbeBettler = bettler,
            ProbeRuecklast = zubehoer.Contains("kiepe") || zubehoer.Contains("buendel"),
            Beinfaktor = daten.Huefte / 0.92f * massstab, Kniehoehe = (daten.Sitzhoehe + 0.02f) * massstab,
        };
        var stoff = FigurStoff(aussehen);
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            if (name != $"Nah_{daten.Varianten[variante]}" && name != $"Fuss_{ProbeFussformen[fuss]}" && name != "Last" && name != "Werkzeug"
                && !(name.StartsWith("Zub_") && zubehoer.Contains(name[4..]))) { teil.QueueFree(); continue; }
            teil.MaterialOverride = stoff;
            if (name is "Last" or "Werkzeug") { teil.Visible = false; figur.Werkzeuge[name] = teil; }
            figur.Teile.Add(teil);
        }
        _probeTrachten[figur] = new ProbeTracht(art, variante, ProbeFarbe(aussehen.Gewand), ProbeFarbe(aussehen.Beinkleid), ProbeFarbe(aussehen.Haut),
            ProbeFarbe(aussehen.Haar), ProbeFarbe(aussehen.Kopffarbe), fuss, zubehoer);
        if (_probeVat) modell.Visible = false;
        BewegungSetzen(figur, "Stehen");
        return figur;
    }

    /// <summary>
    /// Fußform nach Rolle (Schritt 1 des Vollausbaus): barfuß nur der Bettler; Holzschuhe für Arme und Bauern (Tagelöhner, Knecht, Hirte,
    /// Feldarbeiter, Holzsammler); Halbschuhe aus Leder für Bürger, Krämer, Boten, Mönche, Ratsherren, Witwen und Torwächter. Ohne Rolle
    /// (Messfiguren) nach der Nummer Halb- oder Holzschuh.
    /// </summary>
    private static int ProbeFussFuer(string? rolle, int nummer) => rolle switch
    {
        "Bettler" => 2,
        "Tagelöhner" or "Knecht" or "Hirte" or "Feldarbeiter" or "Holzsammler" => 1,
        null => (int)(Streuung(nummer, 241) % 2),
        _ => 0,
    };

    /// <summary>Männer: Gugel bei Kapuze; mit Hut (Stroh, Barett, Eisen) Cotte oder langer Rock, sonst nach der Nummer Cotte, Gugel oder langer
    /// Rock. Frauen: Gebende bei Schleier, Haube bei Haube und unter dem Strohhut, sonst nach der Nummer. Bettler, Mönch und Ratsherr wählt die
    /// Rolle (<see cref="ProbefigurEinsetzen"/>).</summary>
    private static int ProbeVarianteFuer(FigurAussehen a, int nummer)
    {
        if (a.Frau) return a.Kopf == Kopfbedeckung.Schleier ? 1 : a.Kopf is Kopfbedeckung.Haube or Kopfbedeckung.Strohhut ? 0 : (int)(Streuung(nummer, 977) % 2);
        if (a.Kopf == Kopfbedeckung.Kapuze) return 1;
        if (a.Kopf is Kopfbedeckung.Strohhut or Kopfbedeckung.Barett or Kopfbedeckung.Eisenhut) return Streuung(nummer, 977) % 4 == 0 ? 2 : 0;
        return (int)(Streuung(nummer, 977) % 3);
    }

    /// <summary>
    /// Berufszubehör (Schritt 4 des Vollausbaus, eigene Netze „Zub_&lt;name&gt;“) nach der Tracht der Rolle (<see cref="TrachtVon"/>): Strohhut
    /// (Hirte, Feldarbeiter), Barett (Ratsherr, Bürgersohn), Eisenhut, Schürze (Knecht, Magd), Kiepe (Krämer), Reisigbündel (Holzsammler),
    /// Tasche (Bote), Stab (Hirte; auch für die Hellebarde, bis es sie gibt). Hüte nicht über Gugel und Gebende.
    /// </summary>
    private static string[] ProbeZubehoerFuer(FigurAussehen a, string variante)
    {
        var aus = new List<string>(3);
        if (variante is not ("gugel" or "gebende" or "kutte"))
        {
            if (a.Kopf == Kopfbedeckung.Strohhut) aus.Add("stroh");
            else if (a.Kopf == Kopfbedeckung.Barett) aus.Add("barett");
            else if (a.Kopf == Kopfbedeckung.Eisenhut) aus.Add("eisenhut");
        }
        if (a.Schuerze) aus.Add("schuerze");
        var last = a.Last switch
        {
            Last.Kiepe => "kiepe", Last.Buendel => "buendel", Last.Tasche => "tasche", Last.Stab or Last.Hellebarde => "stab", _ => null,
        };
        if (last is not null) aus.Add(last);
        return aus.ToArray();
    }

    private int ProbeFarbe(Color srgb)
    {
        // Godot rechnet Farben in Uniform-Feldern beim Hochladen selbst von sRGB nach linear (Messung 08.11.: mit SrgbToLinear hier waren
        // Gewand und Haut doppelt abgedunkelt); darum die sRGB-Farbe unverändert ablegen.
        var linear = srgb;
        var i = _probePalette.IndexOf(linear);
        if (i >= 0) return i;
        if (_probePalette.Count >= 63) return 0; // die Palette im Shader hat 64 Plätze
        _probePalette.Add(linear);
        _probePaletteNeu = true;
        return _probePalette.Count - 1;
    }

    private bool _probePaletteNeu;
    private Dictionary<Figur, double> _probeLetztePhase = new();

    /// <summary>Unter diesem Takt (Anteil des Gehtempos) steht eine gehende Probefigur: Wer auf dem Weg wartet, bekäme sonst Takt 0 und
    /// bliebe mitten im Schritt stehen.</summary>
    private const float ProbeStehenUnterTakt = 0.08f;

    /// <summary>
    /// <see cref="BewegungSetzen"/> für die Probe: Gehen, Eilen und Gebeugt nehmen „Gehen“, Tragen „Tragen“ (mit Kiste), Reden, Sitzen, Hacken und
    /// Buecken ihren Clip (Hacken mit Axt), alles andere „Stehen“. Der Takt gleicht das Tempo an, damit die Füße nicht rutschen.
    /// </summary>
    private static void ProbeBewegungSetzen(Figur f, string bewegung, float takt)
    {
        string klip;
        float probeTakt;
        var daten = ProbeArten[f.ProbeArt];
        if (bewegung is "Gehen" or "Eilen" or "Gebeugt" or "Tragen" && takt >= ProbeStehenUnterTakt)
        {
            var grund = bewegung switch { "Eilen" => EilenTempo, "Gebeugt" => GebeugtTempo, "Tragen" => TragenTempo, _ => GehenTempo };
            klip = bewegung == "Tragen" && !f.ProbeRuecklast ? "Tragen" : "Gehen"; // Kiepe und Bündel trägt man auf dem Rücken: die Arme gehen frei
            var modellTempo = (klip == "Tragen" ? daten.TragTempo : daten.GehTempo) * f.Modell.Scale.Y;
            probeTakt = takt * grund * f.Beinfaktor / modellTempo; // gewünschte Meter je Sekunde ÷ Meter je Schleife
        }
        else
        {
            klip = bewegung is "Reden" or "Sitzen" or "Hacken" or "Buecken" or "Kauern" or "Betteln" ? bewegung : "Stehen";
            if (f.ProbeBettler && klip is "Stehen" or "Reden" or "Sitzen") klip = "Betteln"; // Schritt 2: der Bettler kauert und streckt die Hand aus
            probeTakt = 1;
        }
        f.Takt = probeTakt;
        if (f.Bewegung == bewegung && klip == f.KiKlip) return;
        f.Bewegung = bewegung;
        if (f.Werkzeuge.TryGetValue("Last", out var last)) last.Visible = klip == "Tragen";
        if (f.Werkzeuge.TryGetValue("Werkzeug", out var axt)) axt.Visible = klip == "Hacken";
        if (klip == f.KiKlip) return; // Gehen ↔ Eilen: die Schleife läuft in derselben Phase weiter
        f.KiKlip = klip;
        f.Spieler.Play(klip);
        f.Anfang = 0;
        f.Dauer = Math.Max(0.1, f.Spieler.GetAnimation(klip).Length);
        f.Phase = (Streuung(f.Nummer, 131) & 0xFFFF) / 65536.0 * f.Dauer;
        f.Neu = true;
        f.Langsam = klip is "Stehen" or "Sitzen" or "Kauern";
    }


    // ═════════════════════════════════════════════════════════════════════════
    //  Stufe wählen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Einmal je Bild vor den Passanten: Skelett oder VAT (nah/fern) nach Zoom oder Startoption; beim Wechsel Skelette ein- oder ausblenden.</summary>
    private void ProbeStufeWaehlen()
    {
        if (!ProbeAktiv) return;
        // gleich beim Start backen (rund eine halbe Sekunde), nicht erst beim ersten Herauszoomen – sonst stockt dort ein Bild
        if (_probeVatNetze is null && !_probeVatVersucht && ProbeStufe != "skelett") { _probeVatVersucht = true; ProbeVatBacken(); }
        var vat = ProbeStufe switch
        {
            "skelett" => false,
            "vat" or "vatfern" => true,
            _ => _kamera.Size >= FigurenHalbeRateAb,
        };
        _probeFern = ProbeStufe == "vatfern" || ProbeStufe == "auto" && _kamera.Size >= FigurenDrittelRateAb;
        if (vat && _probeVatNetze is null) vat = false; // Modell fehlt: beim Skelett bleiben
        if (vat == _probeVat) return;
        _probeVat = vat;
        foreach (var f in _probeTrachten.Keys)
        {
            if (!IsInstanceValid(f.Modell)) continue;
            f.Modell.Visible = !vat;
            f.Neu = true; // beim Zurück zum Skelett gleich die richtige Pose
        }
        GD.Print($"[passantenprobe] Stufe {(vat ? "VAT" : "Skelett")} bei Zoom {_kamera.Size:0}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  VAT: backen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein gebackenes Netz: Netz mit Eckennummer in UV2, Lage- und Normalentextur, das MultiMesh und sein Knoten.</summary>
    private sealed class ProbeVatNetz
    {
        public required string Name { get; init; }
        public required MultiMeshInstance3D Knoten { get; init; }
        public required ShaderMaterial Stoff { get; init; }
        public float[] Puffer { get; set; } = Array.Empty<float>();
        public int Anzahl { get; set; }
    }

    /// <summary>Gebackene Netze, Schlüssel „&lt;Art&gt;:&lt;Netzname&gt;“ (Art 0 Mann, 1 Frau).</summary>
    private Dictionary<string, ProbeVatNetz>? _probeVatNetze;
    /// <summary>Je Art: erste Zeile und Bildzahl je Clip (Reihenfolge <see cref="ProbeKlips"/>).</summary>
    private readonly (int Start, int Bilder)[][] _probeKlipZeilen = { new (int, int)[9], new (int, int)[9] };

    /// <summary>Backt alle Netze beider Probedateien (je Variante nah und fern, dazu die Kiste) für alle Clips. False, wenn kein Modell da ist.</summary>
    private bool ProbeVatBacken()
    {
        var uhr = Stopwatch.StartNew();
        _probeVatNetze = new();
        for (var art = 0; art < ProbeArten.Length; art++) ProbeVatBacken(art);
        _probePaletteNeu = true;
        GD.Print($"[passantenprobe] VAT gebacken in {uhr.ElapsedMilliseconds} ms ({_probeVatNetze.Count} Netze, Clips {string.Join(", ", ProbeKlips.Select((k, i) => $"{k} {_probeKlipZeilen[0][i].Bilder}"))} Bilder)");
        return _probeVatNetze.Count > 0;
    }

    /// <summary>Werte der Material-Bibliothek für die Stoffe im VAT-Shader: Schicht, Kachel (Meter) und mittlere Farbe von „tuch“ und „leder“.</summary>
    private static (int Schicht, float Kachel, Vector3 Mittel)[] ProbeStoffschichten()
    {
        var aus = new (int, float, Vector3)[] { (-1, 1f, Vector3.One), (-1, 1f, Vector3.One) };
        if (Json.ParseString(Godot.FileAccess.GetFileAsString("res://daten/materialbibliothek.json")).AsGodotDictionary() is not { Count: > 0 } bib) return aus;
        var liste = bib["schichten"].AsGodotArray();
        for (var i = 0; i < liste.Count; i++)
        {
            var d = liste[i].AsGodotDictionary();
            var stelle = d["name"].AsString() switch { "tuch" => 0, "leder" => 1, _ => -1 };
            if (stelle < 0) continue;
            var m = d["mittel"].AsGodotArray();
            aus[stelle] = (i, (float)d["kachel"].AsDouble(), new Vector3((float)m[0].AsDouble(), (float)m[1].AsDouble(), (float)m[2].AsDouble()));
        }
        return aus;
    }

    private void ProbeVatBacken(int art)
    {
        if (Modell(ProbeArten[art].Datei) is not { } szene) return;
        var stoffe = ProbeStoffschichten();
        var bibFarbe = ResourceLoader.Exists("res://assets/texturen/modelle/modelle_farbe_hoehe.webp")
            ? Vorladen.Laden<TextureLayered>("res://assets/texturen/modelle/modelle_farbe_hoehe.webp") : null;
        var wurzel = szene.Instantiate<Node3D>();
        var skelett = wurzel.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().First();
        var spieler = wurzel.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().First();
        var zuWurzel = Transform3D.Identity; // Skelett → Modellwurzel
        for (Node? k = skelett; k is Node3D n && k != wurzel; k = k.GetParent()) zuWurzel = n.Transform * zuWurzel;

        // Knochenhaltungen je Bild (Modellraum), einmal für alle Netze
        var zeilen = 0;
        var haltungen = new List<Transform3D[]>();
        for (var c = 0; c < ProbeKlips.Length; c++)
        {
            var anim = spieler.GetAnimation(ProbeKlips[c]);
            var bilder = Math.Max(1, (int)Math.Round(anim.Length * ProbeVatBilder));
            _probeKlipZeilen[art][c] = (zeilen, bilder);
            for (var b = 0; b < bilder; b++) haltungen.Add(ProbeHaltung(skelett, anim, b / (double)ProbeVatBilder, zuWurzel));
            zeilen += bilder;
        }

        var shader = GD.Load<Shader>("res://assets/shader/figur_vat.gdshader");
        _passantenEbene ??= NeueEbene();
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not ArrayMesh netz || teil.Skin is not { } skin) continue;
            var arrays = netz.SurfaceGetArrays(0);
            var ecken = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normalen = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var knochen = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var gewichte = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            var jeEcke = knochen.Length / ecken.Length;
            var bindKnochen = new int[skin.GetBindCount()];
            for (var b = 0; b < bindKnochen.Length; b++)
                bindKnochen[b] = skin.GetBindBone(b) >= 0 ? skin.GetBindBone(b) : skelett.FindBone(skin.GetBindName(b));

            var lage = new byte[ecken.Length * zeilen * 8];   // RGBAH
            var richtung = new byte[ecken.Length * zeilen * 4]; // RGBA8
            var haut = new Transform3D[bindKnochen.Length];
            var kasten = new Aabb(ecken[0], Vector3.Zero);
            for (var z = 0; z < zeilen; z++)
            {
                for (var b = 0; b < haut.Length; b++) haut[b] = haltungen[z][bindKnochen[b]] * skin.GetBindPose(b);
                for (var e = 0; e < ecken.Length; e++)
                {
                    var p = Vector3.Zero;
                    var nr = Vector3.Zero;
                    for (var j = 0; j < jeEcke; j++)
                    {
                        var w = gewichte[e * jeEcke + j];
                        if (w <= 0) continue;
                        var t = haut[knochen[e * jeEcke + j]];
                        p += w * (t * ecken[e]);
                        nr += w * (t.Basis * normalen[e]);
                    }
                    nr = nr.Normalized();
                    kasten = kasten.Expand(p);
                    var o = (z * ecken.Length + e) * 8;
                    HalbSchreiben(lage, o, p.X); HalbSchreiben(lage, o + 2, p.Y); HalbSchreiben(lage, o + 4, p.Z); HalbSchreiben(lage, o + 6, 1f);
                    var q = (z * ecken.Length + e) * 4;
                    richtung[q] = (byte)Math.Clamp((int)Math.Round((nr.X * 0.5f + 0.5f) * 255), 0, 255);
                    richtung[q + 1] = (byte)Math.Clamp((int)Math.Round((nr.Y * 0.5f + 0.5f) * 255), 0, 255);
                    richtung[q + 2] = (byte)Math.Clamp((int)Math.Round((nr.Z * 0.5f + 0.5f) * 255), 0, 255);
                    richtung[q + 3] = 255;
                }
            }

            // Netz ohne Knochen, Eckennummer in UV2.x
            var neu = new Godot.Collections.Array();
            neu.Resize((int)Mesh.ArrayType.Max);
            neu[(int)Mesh.ArrayType.Vertex] = ecken;
            neu[(int)Mesh.ArrayType.Normal] = normalen;
            neu[(int)Mesh.ArrayType.Color] = arrays[(int)Mesh.ArrayType.Color];
            neu[(int)Mesh.ArrayType.TexUV] = arrays[(int)Mesh.ArrayType.TexUV];
            neu[(int)Mesh.ArrayType.TexUV2] = Enumerable.Range(0, ecken.Length).Select(i => new Vector2(i + 0.5f, 0)).ToArray();
            neu[(int)Mesh.ArrayType.Index] = arrays[(int)Mesh.ArrayType.Index];
            var vatNetz = new ArrayMesh();
            vatNetz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, neu);
            vatNetz.CustomAabb = kasten.Grow(0.1f);

            var stoff = new ShaderMaterial { Shader = shader };
            stoff.SetShaderParameter("lage", ImageTexture.CreateFromImage(Image.CreateFromData(ecken.Length, zeilen, false, Image.Format.Rgbah, lage)));
            stoff.SetShaderParameter("normale", ImageTexture.CreateFromImage(Image.CreateFromData(ecken.Length, zeilen, false, Image.Format.Rgba8, richtung)));
            stoff.SetShaderParameter("klips", _probeKlipZeilen[art].Select(k => new Vector2(k.Start, k.Bilder)).ToArray());
            if (bibFarbe is not null) stoff.SetShaderParameter("bib_farbe", bibFarbe);
            stoff.SetShaderParameter("stoff_schicht", stoffe.Select(s => s.Schicht).ToArray());
            stoff.SetShaderParameter("stoff_kachel", stoffe.Select(s => s.Kachel).ToArray());
            stoff.SetShaderParameter("stoff_mittel", stoffe.Select(s => s.Mittel).ToArray());
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = vatNetz,
                CustomAabb = new Aabb(new Vector3(-5000, -500, -5000), new Vector3(10000, 1000, 10000)), // alles im Bild (nur Sichtbare stehen drin)
            };
            var knotenMm = new MultiMeshInstance3D
            {
                Name = $"ProbeVat_{art}_{teil.Name}", Multimesh = mm, MaterialOverride = stoff, TopLevel = true,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(knotenMm);
            knotenMm.GlobalTransform = Transform3D.Identity;
            _probeVatNetze![$"{art}:{teil.Name}"] = new ProbeVatNetz { Name = teil.Name, Knoten = knotenMm, Stoff = stoff };
            GD.Print($"[passantenprobe] VAT {ProbeArten[art].Datei} {teil.Name}: {ecken.Length} Ecken, {netz.SurfaceGetArrayIndexLen(0) / 3} Dreiecke, {zeilen} Bilder, Textur {ecken.Length}×{zeilen}");
        }
        if (Wasserarten.Startoption("probepruefen") is "1") ProbeVatPruefen(wurzel, skelett, spieler, zuWurzel);
        wurzel.Free();
    }

    /// <summary>Haltung aller Knochen (Modellraum) zur Zeit <paramref name="sekunden"/> eines Clips: Spuren aus der Animation, sonst die Ruhelage.</summary>
    private static Transform3D[] ProbeHaltung(Skeleton3D sk, Animation anim, double sekunden, Transform3D zuWurzel)
    {
        var anzahl = sk.GetBoneCount();
        var dreh = new Quaternion[anzahl];
        var ort = new Vector3[anzahl];
        var mass = new Vector3[anzahl];
        for (var i = 0; i < anzahl; i++)
        {
            var ruhe = sk.GetBoneRest(i);
            dreh[i] = ruhe.Basis.GetRotationQuaternion();
            ort[i] = ruhe.Origin;
            mass[i] = ruhe.Basis.Scale;
        }
        for (var t = 0; t < anim.GetTrackCount(); t++)
        {
            var i = sk.FindBone(anim.TrackGetPath(t).GetConcatenatedSubNames());
            if (i < 0) continue;
            switch (anim.TrackGetType(t))
            {
                case Animation.TrackType.Rotation3D: dreh[i] = anim.RotationTrackInterpolate(t, sekunden); break;
                case Animation.TrackType.Position3D: ort[i] = anim.PositionTrackInterpolate(t, sekunden); break;
                case Animation.TrackType.Scale3D: mass[i] = anim.ScaleTrackInterpolate(t, sekunden); break;
            }
        }
        var global = new Transform3D[anzahl];
        var fertig = new bool[anzahl];
        Transform3D Global(int i)
        {
            if (fertig[i]) return global[i];
            var lokal = new Transform3D(new Basis(dreh[i]).Scaled(mass[i]), ort[i]);
            var eltern = sk.GetBoneParent(i);
            global[i] = eltern >= 0 ? Global(eltern) * lokal : lokal;
            fertig[i] = true;
            return global[i];
        }
        var aus = new Transform3D[anzahl];
        for (var i = 0; i < anzahl; i++) aus[i] = zuWurzel * Global(i);
        return aus;
    }

    /// <summary>
    /// Startoption <c>--probepruefen=1</c> (geht ohne Bildschirm): vergleicht die selbst gerechneten Knochenhaltungen mit denen, die Godots
    /// <see cref="AnimationPlayer"/> nach <see cref="AnimationPlayer.Seek"/> am <see cref="Skeleton3D"/> setzt, und prüft, dass die Ruhelage mit den
    /// Bindungen der Haut genau das importierte Netz ergibt. Ergebnis als <c>[passantenprobe] Prüfung …</c> im Protokoll.
    /// </summary>
    private void ProbeVatPruefen(Node3D wurzel, Skeleton3D sk, AnimationPlayer spieler, Transform3D zuWurzel)
    {
        AddChild(wurzel);
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        var groesste = 0f;
        foreach (var klip in ProbeKlips)
        {
            var anim = spieler.GetAnimation(klip);
            spieler.Play(klip);
            foreach (var t in new[] { 0.0, anim.Length * 0.37, anim.Length * 0.81 })
            {
                spieler.Seek(t, true);
                var eigen = ProbeHaltung(sk, anim, t, zuWurzel);
                for (var i = 0; i < sk.GetBoneCount(); i++)
                    groesste = Math.Max(groesste, (eigen[i].Origin - zuWurzel * sk.GetBoneGlobalPose(i).Origin).Length());
            }
        }
        var ruheFehler = 0f;
        var ruhe = new Transform3D[sk.GetBoneCount()];
        for (var i = 0; i < ruhe.Length; i++) ruhe[i] = sk.GetBoneGlobalRest(i);
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not ArrayMesh netz || teil.Skin is not { } skin) continue;
            // Das Backen nimmt an, dass jedes Netz direkt unter dem Skelett hängt, ohne eigene Verschiebung
            if (teil.GetParent() != sk || !teil.Transform.IsEqualApprox(Transform3D.Identity))
                GD.PushWarning($"[passantenprobe] Prüfung: {teil.Name} hängt nicht ohne Verschiebung unter dem Skelett ({teil.GetParent().Name}, {teil.Transform})");
            var arrays = netz.SurfaceGetArrays(0);
            var ecken = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var knochen = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var gewichte = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            var jeEcke = knochen.Length / ecken.Length;
            for (var e = 0; e < ecken.Length; e += 7)
            {
                var p = Vector3.Zero;
                for (var j = 0; j < jeEcke; j++)
                {
                    var b = knochen[e * jeEcke + j];
                    var bone = skin.GetBindBone(b) >= 0 ? skin.GetBindBone(b) : sk.FindBone(skin.GetBindName(b));
                    p += gewichte[e * jeEcke + j] * (ruhe[bone] * skin.GetBindPose(b) * ecken[e]);
                }
                ruheFehler = Math.Max(ruheFehler, (p - ecken[e]).Length());
            }
        }
        RemoveChild(wurzel);
        GD.Print($"[passantenprobe] Prüfung: Knochen eigene Rechnung gegen AnimationPlayer höchstens {groesste * 1000:0.000} mm, " +
                 $"Ruhelage mit Bindungen gegen Netz höchstens {ruheFehler * 1000:0.000} mm, Knochen {sk.GetBoneCount()}");
    }

    private static void HalbSchreiben(byte[] ziel, int stelle, float wert)
    {
        var bits = BitConverter.HalfToUInt16Bits((Half)wert);
        ziel[stelle] = (byte)(bits & 0xFF);
        ziel[stelle + 1] = (byte)(bits >> 8);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  VAT: zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    private int _probeGezeichnet, _probeSkelettSichtbar;

    // Pixel-Look: Die VAT-Netze liegen auf der gewöhnlichen Ebene 1 und erscheinen auch im zweiten Durchgang. Seit PR #385 zeichnet der
    // die Debug-Zeichnung „Unbeleuchtet“ (Eigenfarbe), und die behält das eigene Material samt Vertex-Shader (Godot-Quelltext,
    // render_forward_clustered.cpp: nur „Beleuchtung“, „Überzeichnung“ und „Schattenteilung“ tauschen das Material). Die Figuren stehen dort also in
    // ihrer Bewegung, ihre Eigenfarbe ist die richtige Grundlage für Stufenlicht und Glühen. Die Ausblendung aus Runde 3 (gegen die Ruhehaltung im
    // alten Beleuchtungsbild) ist darum zurückgenommen: Sie hätte jetzt die Eigenfarbe des Bodens unter die Figuren gelegt.

    /// <summary>Einmal je Bild nach den Passanten: alle sichtbaren Probefiguren im Bild in die MultiMeshes schreiben (Lage, Clip, Bild, Farben).</summary>
    private void ProbeVatZeichnen()
    {
        if (!ProbeAktiv || _probeVatNetze is null) return;
        foreach (var v in _probeVatNetze.Values) v.Anzahl = 0;
        _probeGezeichnet = 0;
        if (_probeVat && !_stadtVerdeckt)
        {
            foreach (var p in _passanten.Values)
                if (p.Figur is { Probe: true } f && p.Knoten.Visible) ProbeVatEintragen(f, p.Knoten.Position);
            foreach (var m in _probeMessfiguren) ProbeVatEintragen(m.Figur, m.Knoten.Position);
        }
        var schatten = _passantenSchatten ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (var v in _probeVatNetze.Values)
        {
            var mm = v.Knoten.Multimesh;
            if (_probePaletteNeu) v.Stoff.SetShaderParameter("farben", ProbePaletteFuerShader());
            if (v.Knoten.CastShadow != schatten) v.Knoten.CastShadow = schatten;
            mm.VisibleInstanceCount = Math.Min(v.Anzahl, mm.InstanceCount);
            if (v.Anzahl > 0) RenderingServer.MultimeshSetBuffer(mm.GetRid(), v.Puffer);
        }
        _probePaletteNeu = false;
    }

    private Color[] ProbePaletteFuerShader()
    {
        var aus = new Color[64];
        for (var i = 0; i < _probePalette.Count; i++) aus[i] = _probePalette[i];
        return aus;
    }

    private void ProbeVatEintragen(Figur f, Vector3 lage)
    {
        if (!_probeTrachten.TryGetValue(f, out var tracht) || !ImBild(lage, 2f)) return;
        var klip = Array.IndexOf(ProbeKlips, f.KiKlip);
        if (klip < 0) klip = 0;
        var netzName = $"{tracht.Art}:{(_probeFern ? "Fern" : "Nah")}_{ProbeArten[tracht.Art].Varianten[tracht.Variante]}";
        var (_, bilder) = _probeKlipZeilen[tracht.Art][klip];
        var bild = (float)(f.Phase / f.Dauer * bilder); // Stand in der Schleife, in Bildern
        var farben = tracht.Gewand + 64f * tracht.Beinkleid + 4096f * tracht.Haut + 262144f * tracht.Haar;
        var xf = f.Modell.GlobalTransform;
        Eintragen(netzName, xf, bild, klip + 16f * tracht.Kopf, farben);
        Eintragen($"{tracht.Art}:Fuss_{ProbeFussformen[tracht.Fuss]}", xf, bild, klip + 16f * tracht.Kopf, farben); // Füße (nah und fern dieselben)
        if (klip == 2) Eintragen($"{tracht.Art}:Last", xf, bild, klip, farben); // die Kiste beim Tragen
        if (klip == 5) Eintragen($"{tracht.Art}:Werkzeug", xf, bild, klip, farben); // die Axt beim Hacken
        foreach (var z in tracht.Zubehoer) Eintragen($"{tracht.Art}:Zub_{z}", xf, bild, klip + 16f * tracht.Kopf, farben); // Berufszubehör
        _probeGezeichnet++;
    }

    private void Eintragen(string netzName, Transform3D xf, float bild, float klipUndKopf, float farben)
    {
        if (_probeVatNetze is null || !_probeVatNetze.TryGetValue(netzName, out var v)) return;
        var mm = v.Knoten.Multimesh;
        if (v.Anzahl >= mm.InstanceCount)
        {
            mm.VisibleInstanceCount = 0;
            mm.InstanceCount = Math.Max(64, mm.InstanceCount * 2);
            var groesser = new float[mm.InstanceCount * 16];
            Array.Copy(v.Puffer, groesser, Math.Min(v.Puffer.Length, groesser.Length));
            v.Puffer = groesser;
        }
        var i = v.Anzahl++ * 16;
        var p = v.Puffer;
        var b = xf.Basis;
        p[i + 0] = b.X.X; p[i + 1] = b.Y.X; p[i + 2] = b.Z.X; p[i + 3] = xf.Origin.X;
        p[i + 4] = b.X.Y; p[i + 5] = b.Y.Y; p[i + 6] = b.Z.Y; p[i + 7] = xf.Origin.Y;
        p[i + 8] = b.X.Z; p[i + 9] = b.Y.Z; p[i + 10] = b.Z.Z; p[i + 11] = xf.Origin.Z;
        p[i + 12] = bild; p[i + 13] = klipUndKopf; p[i + 14] = farben; p[i + 15] = 0;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Messfiguren (--passantenprobe=<n>)
    // ═════════════════════════════════════════════════════════════════════════

    private sealed record ProbeMessfigur(Node3D Knoten, Figur Figur, Vector3 Anfang, Vector3 Richtung, float Strecke, float Tempo, string Art);
    private readonly List<ProbeMessfigur> _probeMessfiguren = new();
    private bool _probeMessfigurenGebaut, _probeMessSchatten = true;
    private double _probeMessZeit, _probeMeldeZeit;
    private readonly Stopwatch _probeUhr = new();
    private double _probeMs;
    private int _probeBilder;

    /// <summary>
    /// Legt einmal die Messfiguren an: ein Raster um den Punkt, auf den die Kamera beim Start blickt (1,6 m Abstand), sechs von zehn gehen auf einer
    /// 8-m-Strecke hin und her, eine steht, eine hackt, eine trägt, eine redet; Männer und Frauen gemischt. Sie sind keine Einwohner (kein Kern, keine Wege, kein Ausweichen) und
    /// stehen auch in Häusern; sie dienen nur der Messung der Zeichen- und Posenkosten.
    /// </summary>
    private void ProbeMessfigurenBauen()
    {
        _probeMessfigurenGebaut = true;
        var mitteBild = GetViewport().GetVisibleRect().Size / 2;
        var o = _kamera.ProjectRayOrigin(mitteBild);
        var r = _kamera.ProjectRayNormal(mitteBild);
        var hoehe = 0f;
        var welt = Mathf.Abs(r.Y) > 1e-4f ? o + r * ((hoehe - o.Y) / r.Y) : o;
        _passantenEbene ??= NeueEbene();
        var mitte = _passantenEbene.ToLocal(welt);
        // Mit --blick=x,z[,zoom] genau dort (die Kamera kann beim ersten Bild noch woanders stehen)
        if (Wasserarten.Startoption("blick")?.Split(',') is { Length: >= 2 } blick
            && float.TryParse(blick[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bx)
            && float.TryParse(blick[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bz))
            mitte = new Vector3(bx, 0, bz);
        var seite = (int)Math.Ceiling(Math.Sqrt(ProbeMessfigurenZahl));
        for (var i = 0; i < ProbeMessfigurenZahl; i++)
        {
            var nummer = 900000 + i;
            var zeile = i / seite;
            var spalte = i % seite;
            var anfang = mitte + new Vector3((spalte - seite / 2f) * 1.6f, 0, (zeile - seite / 2f) * 1.6f);
            var art = ProbeKlipOption ?? (i % 10) switch { 6 => "Stehen", 7 => "Hacken", 8 => "Tragen", 9 => "Reden", _ => "Gehen" };
            var richtung = zeile % 2 == 0 ? Vector3.Right : Vector3.Left;
            var knoten = new Node3D { Name = $"Probefigur_{i}" };
            _passantenEbene.AddChild(knoten);
            var aussehen = AussehenAusNummer(i * 4 + (i % 7 == 0 ? 1 : 0)); // meist Menschen, Farben gestreut
            aussehen = aussehen with { Volk = Volk.Mensch, Haut = HautVon(Volk.Mensch), Kopf = i % 3 == 0 ? Kopfbedeckung.Kapuze : aussehen.Kopf };
            if (ProbefigurEinsetzen(knoten, aussehen, nummer, i % 13 == 0 ? "Bettler" : null) is not { } figur) { knoten.QueueFree(); continue; }
            if (!_passantenSchatten) FigurSchatten(figur, false);
            var tempo = art == "Tragen" ? 0.9f : 1.3f;
            _probeMessfiguren.Add(new ProbeMessfigur(knoten, figur, anfang, richtung, 8f, tempo, art));
        }
        GD.Print($"[passantenprobe] {_probeMessfiguren.Count} Messfiguren um ({mitte.X:0}, {mitte.Z:0}) gesetzt");
    }

    /// <summary>Jedes Bild: Messfiguren bewegen (Lage auf der Strecke, Pose über die Sparregeln der Figuren) und alle 10 s eine Messzeile.</summary>
    private void ProbeMessfigurenAktualisieren(double sekunden, double delta)
    {
        if (!ProbeAktiv) return;
        _probeUhr.Restart();
        if (ProbeMessfigurenZahl > 0 && !_probeMessfigurenGebaut && _passantenEbene is not null) ProbeMessfigurenBauen();
        _probeMessZeit += sekunden;
        if (_probeMessSchatten != _passantenSchatten)
        {
            _probeMessSchatten = _passantenSchatten;
            foreach (var m in _probeMessfiguren) FigurSchatten(m.Figur, _passantenSchatten);
        }
        foreach (var m in _probeMessfiguren)
        {
            var bewegt = m.Art is "Gehen" or "Tragen";
            if (bewegt)
            {
                // hin und her: Dreieckskurve über die Strecke, an den Enden umdrehen
                var weg = (float)((_probeMessZeit * m.Tempo + m.Figur.Nummer * 0.7) % (2 * m.Strecke));
                var vor = weg < m.Strecke;
                var stelle = vor ? weg : 2 * m.Strecke - weg;
                var richtung = vor ? m.Richtung : -m.Richtung;
                var p = m.Anfang + m.Richtung * stelle;
                p.Y = BodenHoehe(p.X, p.Z);
                m.Knoten.Position = p;
                m.Knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);
                BewegungSetzen(m.Figur, m.Art, m.Tempo / ((m.Art == "Tragen" ? TragenTempo : GehenTempo) * m.Figur.Beinfaktor));
            }
            else
            {
                var p = m.Anfang;
                p.Y = BodenHoehe(p.X, p.Z);
                m.Knoten.Position = p;
                BewegungSetzen(m.Figur, m.Art);
            }
            if (!_probeVat) m.Figur.Modell.Visible = ImBild(m.Knoten.Position, 30f);
            FigurImBildBewegen(m.Figur, m.Knoten.Position, sekunden);
        }
        _probeMs += _probeUhr.Elapsed.TotalMilliseconds;
    }

    /// <summary>Nach dem Zeichnen: Zeit der Probe mitzählen und alle 10 s eine Zeile <c>[passantenprobe]</c> ins Protokoll.</summary>
    private void ProbeMelden(double delta)
    {
        if (!ProbeAktiv || ProbeOption is null && Wasserarten.Startoption("messung") is null) return; // Messzeilen nur zum Messen
        _probeBilder++;
        _probeMeldeZeit += delta;
        if (_probeMeldeZeit < 10) return;
        _probeSkelettSichtbar = _probeTrachten.Keys.Count(f => IsInstanceValid(f.Modell) && f.Modell.IsVisibleInTree());
        var gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid()) + (Fenster.PixelGpuZeit?.Invoke() ?? 0);
        GD.Print($"[passantenprobe] Stufe {(_probeVat ? (_probeFern ? "VAT fern" : "VAT nah") : "Skelett")}, Zoom {_kamera.Size:0}, Probefiguren {_probeTrachten.Count} " +
                 $"(Messfiguren {_probeMessfiguren.Count}), im Bild gezeichnet als VAT {_probeGezeichnet}, Skelette sichtbar {_probeSkelettSichtbar}, " +
                 $"Messfiguren-Rechnung {_probeMs / _probeBilder:0.00} ms je Bild, {Engine.GetFramesPerSecond()} Bilder/s, GPU (letztes Bild, nur mit --messung) {gpu:0.00} ms, " +
                 $"{RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} Zeichenaufrufe");
        // Stillstand (NAS 09.11.: eine nahe Figur blieb in der Gehpose stehen): im Bild, mit Gehclip, aber seit der letzten Zeile dieselbe Phase
        var still = 0;
        var gehend = 0;
        var jetzt = new Dictionary<Figur, double>();
        foreach (var p in _passanten.Values)
        {
            if (p.Figur is not { Probe: true } f || !p.Knoten.Visible || !ImBild(p.Knoten.Position)) continue;
            if (f.KiKlip is not ("Gehen" or "Tragen")) continue;
            gehend++;
            if (_probeLetztePhase.TryGetValue(f, out var vorher) && vorher == f.Phase) still++;
            jetzt[f] = f.Phase;
        }
        _probeLetztePhase = jetzt;
        GD.Print($"[passantenprobe] Im Bild gehend {gehend}, davon seit 10 s ohne Fortschritt {still}");
        var zubehoer = _probeTrachten.Values.SelectMany(z => z.Zubehoer).GroupBy(z => z).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}");
        var varianten = _probeTrachten.Values.GroupBy(z => ProbeArten[z.Art].Varianten[z.Variante]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}");
        GD.Print($"[passantenprobe] Varianten: {string.Join(", ", varianten)}; Zubehör: {string.Join(", ", zubehoer)}");
        _probeMeldeZeit = 0;
        _probeMs = 0;
        _probeBilder = 0;
    }
}
