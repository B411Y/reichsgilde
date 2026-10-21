using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Figuren aus Blender (Teil der <c>partial class Stadtansicht</c>): je Volk ein Modell <c>figur_&lt;volk&gt;.glb</c> mit Skelett,
/// Grundkörpern („Koerper_Mann“, „Koerper_Frau“), Trachtteilen und zehn Bewegungen als Schleife am Ort (PR #141).
/// Passanten, Marktverkäufer und die Betenden im Dom setzen ihre Figuren hierüber ein.
///
/// Abspielen: Jede Figur hat einen eigenen <see cref="AnimationPlayer"/>, der nicht selbst läuft (Modus „Manual“). Das Spiel
/// zählt die Phase je Figur selbst weiter und setzt die Pose mit <see cref="AnimationPlayer.Seek"/> – so kann es Figuren
/// außerhalb des Bildes auslassen und sehr kleine (ganz herausgezoomt) seltener bewegen.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Eine eingesetzte Figur und was zum Bewegen nötig ist.</summary>
    private sealed class Figur
    {
        /// <summary>Das Modell (Wurzel der .glb); seine Eltern setzen Lage und Blickrichtung (vorne ist +Z).</summary>
        public required Node3D Modell { get; init; }
        public required AnimationPlayer Spieler { get; init; }
        /// <summary>Beinlänge im Verhältnis zum Menschen (Hüfthöhe ÷ 0,92 m): So viel weiter kommt die Figur mit einem Schritt.</summary>
        public required float Beinfaktor { get; init; }
        /// <summary>Kniehöhe in Metern (mit Größe): Beim Sitzen ruht die Hüfte knapp darunter.</summary>
        public required float Kniehoehe { get; init; }
        /// <summary>Feste Nummer (Einwohner oder Platz): Startphase und Verteilung auf die Bilder.</summary>
        public required int Nummer { get; init; }
        /// <summary>Alle Teile, die gezeigt werden können – zum Hervorheben bekommen sie eine helle Schicht.</summary>
        public List<MeshInstance3D> Teile { get; } = new();
        public MeshInstance3D? Hacke { get; set; }
        /// <summary>Werkzeuge der Werkstatt, die nur bei ihrer Bewegung zu sehen sind (Name des Teils → Teil, siehe <see cref="WerkstattWerkzeug"/>); nur die, die das Figurenmodell hat.</summary>
        public Dictionary<string, MeshInstance3D> Werkzeuge { get; } = new();
        public MeshInstance3D? Stab { get; set; }
        /// <summary>Trägt die Figur den Stab immer (Hirte) oder nur beim Hüten?</summary>
        public bool StabImmer { get; init; }
        public string Bewegung { get; set; } = "";
        /// <summary>Länge einer Schleife (Sekunden), Stand in der Schleife und Abspieltempo (1 = wie in Blender).</summary>
        public double Dauer { get; set; } = 1;
        public double Phase { get; set; }
        public float Takt { get; set; } = 1;
        /// <summary>Zeit, die seit der letzten gesetzten Pose vergangen ist (Figuren außerhalb des Bildes oder in einem ausgelassenen Bild).</summary>
        public double Aufgespart { get; set; }
        /// <summary>Neue Bewegung: Die Pose muss im nächsten Bild gesetzt werden, auch wenn die Figur sonst ausgelassen würde.</summary>
        public bool Neu { get; set; } = true;
        /// <summary>Langsame Schleife (Stehen, Sitzen, Hüten: drei bis vier Sekunden, kaum Ausschlag) – sie braucht nicht jedes Bild eine neue Pose.</summary>
        public bool Langsam { get; set; }
        public bool Hervorgehoben { get; set; }
    }

    /// <summary>Wie eine Figur aussieht: Volk, Geschlecht, Farben und Tracht.</summary>
    private sealed record FigurAussehen(Volk Volk, bool Frau, Color Gewand, Color Beinkleid, Color Haut, Color Haar, Color Kopffarbe,
        Kopfbedeckung Kopf = Kopfbedeckung.Keine, Last Last = Last.Keine, bool Schuerze = false);

    /// <summary>Datei, Hüft- und Kniehöhe (Meter) und Größe je Volk – Hüfte und Knie wie in <c>tools/modelle/figuren.py</c>.
    /// Orks und Oger haben noch kein eigenes Modell: vorerst ein größerer Mensch.</summary>
    private static (string Datei, float Huefte, float Knie, Vector3 Groesse) FigurMasse(Volk volk) => volk switch
    {
        Volk.Zwerg => ("figuren/figur_zwerg", 0.58f, 0.31f, Vector3.One),
        Volk.Elb => ("figuren/figur_elb", 1.04f, 0.57f, Vector3.One),
        Volk.Halbling => ("figuren/figur_halbling", 0.53f, 0.29f, Vector3.One),
        Volk.Ork => ("figuren/figur_mensch", 0.92f, 0.50f, new Vector3(1.18f, 1.1f, 1.18f)),
        Volk.Oger => ("figuren/figur_mensch", 0.92f, 0.50f, new Vector3(1.45f, 1.35f, 1.45f)),
        _ => ("figuren/figur_mensch", 0.92f, 0.50f, Vector3.One),
    };

    /// <summary>
    /// Welche Teile der Figur zu welcher Bewegung der Werkstatt gehören (eines oder mehrere): Hammer beim Hämmern, Hobel beim Hobeln, Säge beim Sägen,
    /// Drechseleisen beim Drechseln, Schlägel und Meißel beim Behauen und Bildhauen, Stößel beim Mörsern, Kelle beim Rühren, Flakon beim Mischen (Destillieren, Pressen und der Blasebalg brauchen keins). Ein Arbeitsplatz im Innenraum kann sie mit seinen extras „requisit“ ersetzen.
    /// </summary>
    private static readonly Dictionary<string, string[]> WerkstattWerkzeug = new()
    {
        ["Haemmern"] = new[] { "Hammer" }, ["Hobeln"] = new[] { "Hobel" }, ["Saegen"] = new[] { "Saege" }, ["Drechseln"] = new[] { "Drechseleisen" },
        ["Behauen"] = new[] { "Schlaegel", "Meissel" }, ["Bildhauen"] = new[] { "Schlaegel", "Meissel" },
        ["Moersern"] = new[] { "Stoessel" }, ["Ruehren"] = new[] { "Kelle" }, ["Mischen"] = new[] { "Flakon" },
    };

    /// <summary>Namen aller Werkzeugteile der Figuren (die übrigen Teile, die kein Kleid und keine Last sind, werden gelöscht).</summary>
    private static readonly HashSet<string> WerkzeugNamen = WerkstattWerkzeug.Values.SelectMany(x => x).ToHashSet();

    /// <summary>Frauen sind 5 % kleiner als Männer desselben Volks (Wunsch des Nutzers).</summary>
    private const float FrauenGroesse = 0.95f;

    /// <summary>Der Export aus Blender hält die erste Pose ein Bild lang (Blender beginnt bei Bild 1): Die Schleife läuft erst ab hier.</summary>
    private const double SchleifenAnfang = 1.0 / 24;

    private const string FigurShaderPfad = "res://assets/shader/figur.gdshader";

    /// <summary>Das eine Material aller Figuren (PR #149: Zone in UV.x, feste Farbe in COLOR).</summary>
    private static ShaderMaterial? _figurStoff;

    /// <summary>Dateien, deren Bewegungen schon auf Schleife gestellt sind (die Animationen teilen sich alle Figuren einer Datei).</summary>
    private readonly HashSet<string> _figurSchleifen = new();

    /// <summary>
    /// Setzt eine Figur als Kind von <paramref name="eltern"/> ein (Lage und Drehung bestimmt der Elternknoten): Grundkörper nach
    /// Geschlecht, Trachtteile nach <paramref name="aussehen"/>, Farben für Gewand, Beinkleid, Haut, Haar und Kopfbedeckung über
    /// das gemeinsame Material <c>figur.gdshader</c> (Zonen im Modell, Farben je Teil als Instanz-Uniform). Hacke und Stab bleiben versteckt da, bis eine
    /// Bewegung sie braucht; alle übrigen Teile werden gelöscht. Null, wenn das Modell fehlt.
    /// </summary>
    private Figur? FigurEinsetzen(Node3D eltern, FigurAussehen aussehen, int nummer)
    {
        var (datei, huefte, knie, groesse) = FigurMasse(aussehen.Volk);
        if (Modell(datei) is not { } szene) return null;
        var modell = szene.Instantiate<Node3D>();
        modell.Scale = groesse * (aussehen.Frau ? FrauenGroesse : 1f);
        eltern.AddChild(modell);
        var spieler = modell.GetNode<AnimationPlayer>("AnimationPlayer");
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // das Spiel setzt die Pose selbst (FigurBewegen)
        if (_figurSchleifen.Add(datei))
            foreach (var name in spieler.GetAnimationList())
                spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;

        var figur = new Figur
        {
            Modell = modell, Spieler = spieler, Nummer = nummer, StabImmer = aussehen.Last == Last.Stab,
            Beinfaktor = huefte / 0.92f * modell.Scale.Y, Kniehoehe = knie * modell.Scale.Y,
        };
        var kopf = aussehen.Kopf switch
        {
            Kopfbedeckung.Strohhut => "Strohhut", Kopfbedeckung.Barett => "Barett", Kopfbedeckung.Kapuze => "Kapuze",
            Kopfbedeckung.Haube => "Haube", Kopfbedeckung.Schleier => "Schleier", _ => "",
        };
        var last = aussehen.Last switch { Last.Kiepe => "Kiepe", Last.Buendel => "Buendel", Last.Tasche => "Tasche", _ => "" };
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            var behalten = name switch
            {
                "Koerper_Mann" => !aussehen.Frau,
                "Koerper_Frau" => aussehen.Frau,
                "Schuerze" => aussehen.Schuerze,
                "Hacke" or "Stab" => true,
                _ => WerkzeugNamen.Contains(name) || name == kopf || name == last,
            };
            if (!behalten)
            {
                teil.QueueFree();
                continue;
            }
            if (name == "Hacke") { figur.Hacke = teil; teil.Visible = false; }
            if (WerkzeugNamen.Contains(name)) { figur.Werkzeuge[name] = teil; teil.Visible = false; }
            if (name == "Stab") { figur.Stab = teil; teil.Visible = figur.StabImmer; }
            figur.Teile.Add(teil);
            // Ein Material für alle Teile (figur.gdshader), die Farben je Figur als Instanz-Uniform: ein Zeichenaufruf je Teil.
            teil.MaterialOverride = _figurStoff ??= new ShaderMaterial { Shader = GD.Load<Shader>(FigurShaderPfad) };
            teil.SetInstanceShaderParameter("gewand", aussehen.Gewand);
            teil.SetInstanceShaderParameter("beinkleid", aussehen.Beinkleid);
            teil.SetInstanceShaderParameter("haut", aussehen.Haut);
            teil.SetInstanceShaderParameter("haar", aussehen.Haar);
            teil.SetInstanceShaderParameter("kopfbedeckung", aussehen.Kopffarbe);
        }
        BewegungSetzen(figur, "Stehen");
        return figur;
    }

    /// <summary>
    /// Wechselt die Bewegung (eine der zehn Schleifen). Die Startphase kommt aus der Nummer, damit nicht alle im Gleichschritt gehen.
    /// Die Hacke erscheint nur beim Hacken, der Stab beim Hüten (der Hirte trägt ihn immer), Hammer, Hobel, Säge, Drechseleisen, Schlägel und Meißel
    /// bei ihrer Werkstattbewegung. <paramref name="werkzeug"/> ersetzt deren Teile (leer: keines, z. B. an der Steinsäge der Steinmetzei); null: die der Bewegung.
    /// </summary>
    private static void BewegungSetzen(Figur f, string bewegung, float takt = 1, string[]? werkzeug = null)
    {
        f.Takt = takt;
        if (f.Bewegung == bewegung) return;
        f.Bewegung = bewegung;
        f.Spieler.Play(bewegung);
        f.Dauer = Math.Max(0.1, f.Spieler.GetAnimation(bewegung).Length - SchleifenAnfang);
        f.Phase = (Streuung(f.Nummer, 131) & 0xFFFF) / 65536.0 * f.Dauer;
        f.Neu = true;
        f.Langsam = bewegung is "Stehen" or "Sitzen" or "Hueten";
        if (f.Hacke is not null) f.Hacke.Visible = bewegung == "Hacken";
        var sichtbar = werkzeug ?? (WerkstattWerkzeug.TryGetValue(bewegung, out var teile) ? teile : Array.Empty<string>());
        foreach (var (name, teil) in f.Werkzeuge) teil.Visible = sichtbar.Contains(name);
        if (f.Stab is not null) f.Stab.Visible = f.StabImmer || bewegung == "Hueten";
    }

    /// <summary>Zählt die Phase um <paramref name="sekunden"/> (echte Zeit, mal Abspieltempo) weiter und setzt die Pose.</summary>
    private static void FigurBewegen(Figur f, double sekunden)
    {
        f.Phase = (f.Phase + sekunden * f.Takt) % f.Dauer;
        f.Spieler.Seek(SchleifenAnfang + f.Phase, true);
        f.Neu = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechenzeit: nur Figuren im Bild, ganz herausgezoomt seltener
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Unter so vielen Pixeln je Meter (Bildhöhe ÷ Kamerahöhe) bewegen sich die Figuren nur jedes zweite Bild …</summary>
    private const float FigurenHalbeRateUnter = 12f;
    /// <summary>… und darunter nur jedes dritte. Bei 900 Pixeln Bildhöhe: ab Zoom 75 bzw. 129.</summary>
    private const float FigurenDrittelRateUnter = 7f;

    /// <summary>Langsame Schleifen bekommen nur jedes dritte Bild eine neue Pose (bei 60 Bildern je Sekunde noch 20 – das Atmen sieht gleich aus).</summary>
    private const int LangsamTeiler = 3;

    private readonly Plane[] _bildEbenen = new Plane[6];
    private Transform3D _stadtLage = Transform3D.Identity;
    private bool _stadtVerdeckt;
    private int _figurenBild, _figurenTeiler = 1;
    private double _figurenZeit;
    private int _figurenBewegt;

    /// <summary>Einmal je Bild: Sichtfeld der Kamera und wie oft sich die Figuren bewegen.</summary>
    private void FigurenBildVorbereiten()
    {
        var ebenen = _kamera.GetFrustum();
        for (var i = 0; i < _bildEbenen.Length && i < ebenen.Count; i++) _bildEbenen[i] = ebenen[i];
        var pixelJeMeter = GetViewport().GetVisibleRect().Size.Y / _kamera.Size;
        _figurenTeiler = pixelJeMeter < FigurenDrittelRateUnter ? 3 : pixelJeMeter < FigurenHalbeRateUnter ? 2 : 1;
        _stadtLage = GlobalTransform;
        _stadtVerdeckt = _raum is not null; // in der Raumansicht sieht man die Stadt nicht
        _figurenBild++;
    }

    /// <summary>Liegt der Punkt (in Koordinaten der Stadt) im Bild – mit 1,5 m Rand, damit auch Kopf und Füße am Rand mitzählen?</summary>
    private bool ImBild(Vector3 punkt)
    {
        if (_stadtVerdeckt) return false;
        punkt = _stadtLage * punkt;
        foreach (var ebene in _bildEbenen)
            if (ebene.DistanceTo(punkt) > 1.5f) return false; // Die Ebenen zeigen nach außen
        return true;
    }

    /// <summary>
    /// Bewegt eine sichtbare Figur um <paramref name="sekunden"/> weiter, aber nur, wenn sie im Bild ist, und ganz herausgezoomt nur jedes
    /// zweite oder dritte Bild (über die Nummer verteilt), langsame Schleifen noch dreimal seltener. Die ausgelassene Zeit wird nachgeholt,
    /// damit das Tempo stimmt.
    /// </summary>
    private void FigurImBildBewegen(Figur f, Vector3? lage, double sekunden) // lage in Koordinaten der Stadt; null = ohne Prüfung (Innenraum)
    {
        f.Aufgespart += sekunden;
        if (lage is { } l && !ImBild(l)) return;
        if (!f.Neu && (_figurenBild + f.Nummer) % (_figurenTeiler * (f.Langsam ? LangsamTeiler : 1)) != 0) return;
        FigurBewegen(f, f.Aufgespart);
        f.Aufgespart = 0;
        _figurenBewegt++;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hervorheben
    // ═════════════════════════════════════════════════════════════════════════

    private static StandardMaterial3D? _hervorhebung;

    /// <summary>Hebt eine Figur hell hervor (Maus darüber, ausgewählt): eine warme Schicht über allen Teilen, für alle Figuren dieselbe.</summary>
    private static void FigurHervorheben(Figur f, bool an)
    {
        if (f.Hervorgehoben == an) return;
        f.Hervorgehoben = an;
        _hervorhebung ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = new Color(1f, 0.85f, 0.45f, 0.35f),
        };
        foreach (var teil in f.Teile)
            if (IsInstanceValid(teil)) teil.MaterialOverlay = an ? _hervorhebung : null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Farben
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Color[] Beinkleidfarben =
    {
        new(0.30f, 0.26f, 0.22f), new(0.24f, 0.24f, 0.26f), new(0.36f, 0.30f, 0.20f), new(0.20f, 0.22f, 0.28f), new(0.40f, 0.36f, 0.30f),
    };

    /// <summary>Haarfarben je Volk: Elben hell oder silbern, Zwerge oft rot, Halblinge braun.</summary>
    private static Color[] Haarfarben(Volk volk) => volk switch
    {
        Volk.Elb => new[] { new Color(0.85f, 0.78f, 0.55f), new Color(0.80f, 0.80f, 0.78f), new Color(0.70f, 0.60f, 0.38f), new Color(0.22f, 0.16f, 0.12f) },
        Volk.Zwerg => new[] { new Color(0.55f, 0.25f, 0.12f), new Color(0.38f, 0.25f, 0.14f), new Color(0.12f, 0.10f, 0.09f), new Color(0.62f, 0.38f, 0.18f) },
        Volk.Halbling => new[] { new Color(0.45f, 0.24f, 0.12f), new Color(0.38f, 0.25f, 0.14f), new Color(0.72f, 0.58f, 0.32f) },
        _ => new[] { new Color(0.25f, 0.17f, 0.10f), new Color(0.38f, 0.25f, 0.14f), new Color(0.72f, 0.58f, 0.32f), new Color(0.12f, 0.10f, 0.09f), new Color(0.55f, 0.25f, 0.12f) },
    };

    private static readonly Color GrauesHaar = new(0.62f, 0.60f, 0.58f);

    /// <summary>Hautfarbe: Elben etwas heller, Zwerge etwas rötlicher, Orks grün, Oger grau.</summary>
    private static Color HautVon(Volk volk) => volk switch
    {
        Volk.Elb => new Color(0.92f, 0.80f, 0.70f),
        Volk.Zwerg => new Color(0.82f, 0.60f, 0.48f),
        Volk.Ork => new Color(0.42f, 0.52f, 0.30f),
        Volk.Oger => new Color(0.55f, 0.52f, 0.46f),
        _ => Hautfarbe,
    };

    private static readonly Color Hautfarbe = new(0.85f, 0.68f, 0.55f);

    /// <summary>Aussehen einer Figur ohne Einwohner (Gläubige der Messe, Marktverkäufer): Volk, Geschlecht und Farben aus der Nummer.</summary>
    private static FigurAussehen AussehenAusNummer(int nummer, Color? gewand = null, bool schuerze = false)
    {
        var volk = (nummer % 4) switch { 1 => Volk.Zwerg, 2 => Volk.Elb, 3 => Volk.Halbling, _ => Volk.Mensch };
        var frau = Streuung(nummer, 53) % 2 == 0;
        var haare = Haarfarben(volk);
        return new FigurAussehen(volk, frau, gewand ?? Gewandfarben[nummer * 3 % Gewandfarben.Length], Beinkleidfarben[nummer % Beinkleidfarben.Length],
            HautVon(volk), haare[nummer * 5 % haare.Length], Leinenfarbe, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, schuerze);
    }
}
