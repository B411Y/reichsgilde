using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Hühner, Hunde, Katzen und die Krähe – ein Teil der <c>partial class Stadtansicht</c> (Nutzer, 04.11.2026). Reine Darstellung wie die Vögel
/// (Stadtansicht.Voegel.cs): Die Tiere kennen den Kern nicht und kommen nicht in den Spielstand; sie lesen nur Uhrzeit, Wetter, Jahreszeit
/// und wo Passanten gehen. Wie viele es sind und wo sie leben, folgt aus der Karte (Lage der Höfe, das Gassennetz), nicht aus dem Zufall
/// der Sitzung; das Verhalten im Einzelnen würfelt eine feste Zufallsquelle.
/// <list type="bullet">
/// <item><b>Hühner</b> (3–6 Hennen in gemischten Farben und ein Hahn) leben auf den Höfen des Umlands (Bauernhof, Klosterhof) und an bis zu zwei
/// Schänken der Stadt. Sie picken, kratzen und laufen kurze Strecken um ihren Hof. Bei Nacht, Regen und Schnee sind sie im Stall: Sie gehen
/// zur Hofmitte und verschwinden dort, morgens kommen sie von dort wieder heraus. Passanten scheuchen sie ein Stück weg.</item>
/// <item><b>Hunde und Katzen</b> streunen frei (zwei Hunde, zwei Katzen je Stadt, nie alle zugleich: Jedes Tier ist nur zeitweise da und taucht
/// außerhalb des Bildes auf und ab). Sie laufen im Gassennetz von Ort zu Ort; Katzen sitzen gern an Hauswänden und weichen Passanten aus, Hunde
/// nicht. Nachts liegen die Hunde und sitzen die Katzen, nur selten ist eines unterwegs.</item>
/// <item><b>Krähen</b> sind die dritte Vogelart der Schwärme in Stadtansicht.Voegel.cs: Türme, Mauerkronen, Felder; im Herbst und Winter mehr.</item>
/// </list>
/// Weit herausgezoomt (ab <see cref="TiereAusAb"/> m sichtbarer Höhe) und im Innenraum sind alle Tiere aus; außerhalb des Bildes stehen die
/// Bewegungen still.
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _tierEbene;
    private readonly RandomNumberGenerator _tierZufall = new() { Seed = 4712 };
    private float _tierWand;
    /// <summary>Ab dieser sichtbaren Höhe der Kamera (Meter) sind die Tiere zu klein und bleiben weg (die dritte Pixel-Zoomstufe ist 140 m).</summary>
    private const float TiereAusAb = 100f;
    /// <summary>Wie hoch die Tiere sind im Verhältnis zum Modell: im Pixel-Look sonst nicht zu lesen.</summary>
    private const float TierMassstab = 1f;

    /// <summary>Startoption <c>--tiertest=1</c>: Hunde und Katzen erscheinen gleich, auch im Bild, nahe dem Markt, und bleiben lange (für Bildschirmfotos).</summary>
    private static readonly bool TierTest = Wasserarten.Startoption("tiertest") is "1" or "an" or "ja";

    /// <summary>Startoption <c>--tiere=0</c>: keine Hühner, Hunde, Katzen und Krähen (zum Vergleichen der Leistung).</summary>
    private static readonly bool TiereAus = Wasserarten.Startoption("tiere") is "0" or "aus" or "nein";

    private const float HuhnTempo = 0.5f, HuhnFlucht = 1.8f;
    private const float HundGehen = 1.1f, HundTraben = 2.4f, KatzeGehen = 0.8f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Krähen (Teil der Vogelschwärme)
    // ═════════════════════════════════════════════════════════════════════════

    private static float Flugtempo(Vogel v) => v.Kraehe ? 8f : v.Taube ? 7f : 6f;
    private static float Schritttempo(Vogel v) => v.Kraehe ? 0.6f : v.Taube ? 0.35f : 0.5f;

    /// <summary>Wie viele Krähen es gibt (0–1): im Sommer die wenigsten, im Winter alle.</summary>
    private float KraehenAnteil => _jahreszeit switch { Jahreszeit.Winter => 1f, Jahreszeit.Herbst => 0.7f, Jahreszeit.Fruehling => 0.2f, _ => 0f };

    /// <summary>
    /// Schaltet einen Krähenschwarm je nach Jahreszeit ein und aus (nie mitten im Flug). Gibt <c>true</c> zurück, solange der Schwarm weg ist –
    /// dann bewegt ihn <see cref="VoegelAktualisieren"/> nicht.
    /// </summary>
    private bool KraehenSchwarmSchalten(Schwarm s)
    {
        var da = KraehenAnteil >= s.Schwelle;
        if (da == s.Da) return !da;
        if (s.Voegel.Any(v => v.Zustand == Vogelzustand.Fliegt)) return false;
        s.Da = da;
        foreach (var v in s.Voegel) v.Knoten.Visible = da;
        return !da;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gemeinsames
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Spielt die Bewegung <paramref name="name"/>, wenn der Spieler sie kennt (weicher Übergang).</summary>
    private static void TierSpielen(AnimationPlayer? spieler, ref string aktuell, string name)
    {
        if (aktuell == name) return;
        aktuell = name;
        if (spieler is not null && spieler.HasAnimation(name)) spieler.Play(name, customBlend: 0.2);
    }

    private static AnimationPlayer? TierSpieler(Node3D knoten)
    {
        var sp = knoten.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (sp is not null)
            foreach (var name in sp.GetAnimationList()) sp.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        return sp;
    }

    /// <summary>Dreht den Knoten weich in die Richtung (waagerecht; das Modell schaut nach +Z).</summary>
    private static void TierDrehen(Node3D knoten, Vector3 richtung, float dt, float rate = 8f)
    {
        richtung.Y = 0;
        if (richtung.LengthSquared() < 1e-6f) return;
        var ziel = Mathf.Atan2(richtung.X, richtung.Z);
        knoten.Rotation = new Vector3(0, Mathf.LerpAngle(knoten.Rotation.Y, ziel, 1f - Mathf.Exp(-rate * dt)), 0);
    }

    /// <summary>Ist ein sichtbarer Passant näher als <paramref name="abstand"/> an <paramref name="p"/>? Dann zeigt <paramref name="weg"/> von ihm fort.</summary>
    private bool PassantNaheWeg(Vector3 p, float abstand, out Vector3 weg)
    {
        weg = Vector3.Zero;
        var best = abstand * abstand;
        var gefunden = false;
        foreach (var passant in _passanten.Values)
        {
            if (!passant.Knoten.Visible) continue;
            var d = p - passant.Knoten.GlobalPosition;
            d.Y = 0;
            var q = d.LengthSquared();
            if (q >= best) continue;
            best = q;
            weg = q > 1e-6f ? d / Mathf.Sqrt(q) : Vector3.Right;
            gefunden = true;
        }
        return gefunden;
    }

    private bool TiereSichtbar() => _raum is null && _innen is null && _kamera.Size <= TiereAusAb;

    private static ulong OrtsSaat(Vector3 p) => (ulong)(Math.Abs((long)MathF.Round(p.X * 10)) * 73856093L ^ Math.Abs((long)MathF.Round(p.Z * 10)) * 19349663L) + 1;

    private void TiereAnlegen()
    {
        _tierEbene = new Node3D { Name = "Tiere" };
        AddChild(_tierEbene);
        WandplaetzeSuchen();
        _tierWand = 150;
        HuehnerAnlegen();
        StreunerAnlegen();
        foreach (var h in _huehnerHoefe) GD.Print($"[tiere] Hühnerhof bei {h.Mitte.X:0.0}, {h.Mitte.Z:0.0}: {h.Huehner.Count} Tiere");
        GD.Print($"[tiere] {_huehnerHoefe.Count} Hühnerhöfe, {_huehnerHoefe.Sum(h => h.Huehner.Count)} Hühner, {_streuner.Count} Streuner, {_wandplaetze.Count} Wandplätze");
    }

    /// <summary>Wird jedes Bild von <see cref="VoegelAktualisieren"/> aus gerufen (Stadtansicht.cs).</summary>
    private void TiereAktualisieren(float uhrzeit, double delta, bool pausiert)
    {
        if (TiereAus) return;
        if (_tierEbene is null)
        {
            if (_vogelEbene is null) return; // erst, wenn auch die Vögel stehen (Häuser sind da)
            TiereAnlegen();
        }
        var sichtbar = TiereSichtbar();
        if (_tierEbene!.Visible != sichtbar) _tierEbene.Visible = sichtbar;
        if (!sichtbar) return;
        var dt = pausiert ? 0f : (float)delta;
        _tierWand -= dt;
        if (_tierWand <= 0) { _tierWand = 150; WandplaetzeSuchen(); }
        HuehnerAktualisieren(dt);
        StreunerAktualisieren(dt);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hühner
    // ═════════════════════════════════════════════════════════════════════════

    private enum HuhnZustand { Steht, Pickt, Kratzt, Geht, Flieht, Heim }

    private sealed class Huhn
    {
        public required Node3D Knoten { get; init; }
        public AnimationPlayer? Spieler { get; init; }
        public required bool Hahn { get; init; }
        public HuhnZustand Zustand { get; set; } = HuhnZustand.Steht;
        public Vector3 Ziel { get; set; }
        public float Rest { get; set; }
        public string Bewegung = "";
        public bool Da { get; set; }
    }

    private sealed class Huehnerhof
    {
        public required Vector3 Mitte { get; init; }
        public required float Radius { get; init; }
        public List<Huhn> Huehner { get; } = new();
        public bool Da { get; set; }
    }

    private readonly List<Huehnerhof> _huehnerHoefe = new();
    private static readonly string[] HuhnModelle = { "tiere/tier_huhn_braun", "tiere/tier_huhn_weiss", "tiere/tier_huhn_gesprenkelt" };

    /// <summary>Sind die Hühner draußen? Tags, trocken, ohne Schnee.</summary>
    private bool HuehnerDraussen => _daemmerung < VogelNachtAb && _wetter.Regen < 0.2f && _wetter.Schnee < 0.1f && _schneeDecke < 0.3f;

    private void HuehnerAnlegen()
    {
        var orte = new List<(Vector3 Mitte, float Radius)>();
        if (_karte is not null)
            foreach (var g in _karte.GebaeudeDerArt(Bausteinart.Umlandbetrieb).Where(g => g.Katalog is "bauernhof" or "klosterhof"))
                orte.Add((UmlandHof(g) ?? Punkt3(g.Punkt), 3.5f));
        // Stadthöfe: Hof der ersten zwei Schänken (Name als Reihenfolge, damit es bei jedem Laden gleich ist)
        var stadthoefe = _betriebe.Values.Where(b => b.Betrieb.Art is Betriebsart.Schaenke or Betriebsart.Wirtshaus or Betriebsart.Gasthaus)
            .OrderBy(b => b.Name, StringComparer.Ordinal).Take(2).Select(b => b.Hof).ToList();
        // gibt es (noch) keine Schänke: ein ruhiger Winkel an einer Hauswand abseits des Marktes (zwischen 35 und 80 % der Stadtweite, mindestens 25 m
        // vom anderen Hof), der Ort aus der Karte gewählt – nicht gewürfelt
        // Die Wand liegt vom Betrachter aus hinter den Hühnern (die Gasse schaut zur Kamera), sonst verdeckt das Haus sie.
        var zurKamera = _kamera.GlobalBasis.Z with { Y = 0 };
        zurKamera = zurKamera.LengthSquared() > 1e-6f ? zurKamera.Normalized() : Vector3.Back;
        foreach (var w in _wandplaetze.Where(w => w.Blick.Dot(zurKamera) > 0.5f && Waagerecht(w.Ort, _markt) is var d && d > 0.3f * StadtReichweite && d < 0.85f * StadtReichweite)
                     .OrderBy(w => OrtsSaat(w.Ort)))
        {
            if (stadthoefe.Count >= 2) break;
            if (stadthoefe.All(h => Waagerecht(h, w.Ort) >= 25)) stadthoefe.Add(w.Ort);
        }
        foreach (var hof in stadthoefe) orte.Add((hof, 2.2f));
        foreach (var (mitte, radius) in orte)
        {
            var hof = new Huehnerhof { Mitte = mitte with { Y = 0 }, Radius = radius };
            var saat = new RandomNumberGenerator { Seed = OrtsSaat(mitte) };
            var hennen = saat.RandiRange(3, 6);
            var farbe = saat.RandiRange(0, 2);
            for (var i = 0; i < hennen + 1; i++)
            {
                var hahn = i == hennen;
                if (Modell(hahn ? "tiere/tier_hahn" : HuhnModelle[(farbe + i) % 3]) is not { } szene) continue;
                var knoten = szene.Instantiate<Node3D>();
                var mergeUhr = Ladezeit.Uhr();
                FlaechenZusammenlegen(knoten); // Stadtansicht.Flaechenmerge.cs
                Ladezeit.Dazu("Flächen zusammenlegen", mergeUhr);
                knoten.Scale = Vector3.One * TierMassstab;
                knoten.Visible = false;
                _tierEbene!.AddChild(knoten);
                var huhn = new Huhn { Knoten = knoten, Spieler = TierSpieler(knoten), Hahn = hahn };
                hof.Huehner.Add(huhn);
            }
            if (hof.Huehner.Count > 0) _huehnerHoefe.Add(hof);
        }
    }

    private Vector3 HofPunkt(Huehnerhof hof, float haelfte = 1f)
    {
        var w = _tierZufall.RandfRange(0, Mathf.Tau);
        var r = hof.Radius * haelfte * Mathf.Sqrt(_tierZufall.Randf());
        return AufFuss(hof.Mitte + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * r);
    }

    private void HuehnerAktualisieren(float dt)
    {
        var draussen = HuehnerDraussen;
        foreach (var hof in _huehnerHoefe)
        {
            var imBild = ImBild(AufFuss(hof.Mitte));
            if (draussen && !hof.Da)
            {
                // morgens: aus dem Stall kommen, einzeln nacheinander
                hof.Da = true;
                var n = 0;
                foreach (var h in hof.Huehner)
                {
                    h.Da = true;
                    h.Knoten.Visible = true;
                    h.Knoten.Position = AufFuss(hof.Mitte);
                    h.Zustand = HuhnZustand.Steht;
                    h.Rest = 0.8f * n++ + _tierZufall.RandfRange(0, 1.5f);
                    h.Bewegung = "";
                }
            }
            else if (!draussen && hof.Da)
            {
                foreach (var h in hof.Huehner.Where(h => h.Da)) { h.Zustand = HuhnZustand.Heim; h.Ziel = hof.Mitte; }
                if (hof.Huehner.All(h => !h.Da)) hof.Da = false;
            }
            if (!hof.Da) continue;
            foreach (var h in hof.Huehner)
            {
                if (!h.Da) continue;
                if (h.Spieler is { } sp) sp.SpeedScale = imBild ? 1 : 0;
                if (imBild && dt > 0) HuhnBewegen(hof, h, dt);
                else if (!imBild && dt > 0 && h.Zustand == HuhnZustand.Heim) h.Da = h.Knoten.Visible = false; // außer Sicht: gleich im Stall
            }
            if (!hof.Huehner.Any(h => h.Da)) hof.Da = false;
        }
    }

    private void HuhnBewegen(Huehnerhof hof, Huhn h, float dt)
    {
        var pos = h.Knoten.Position;
        if (h.Zustand != HuhnZustand.Heim && h.Zustand != HuhnZustand.Flieht && PassantNaheWeg(pos, 2.3f, out var weg))
        {
            // Passant kommt zu nah: ein Stück davonrennen, aber im Hof bleiben
            var ziel = pos + weg * _tierZufall.RandfRange(2.5f, 3.5f);
            var mitteHoch = hof.Mitte with { Y = ziel.Y };
            var vomMittelpunkt = ziel - mitteHoch;
            if (vomMittelpunkt.Length() > hof.Radius) ziel = mitteHoch + vomMittelpunkt.Normalized() * hof.Radius;
            h.Ziel = ziel;
            h.Zustand = HuhnZustand.Flieht;
        }
        switch (h.Zustand)
        {
            case HuhnZustand.Steht:
            case HuhnZustand.Pickt:
            case HuhnZustand.Kratzt:
                h.Rest -= dt;
                if (h.Rest > 0) { TierSpielen(h.Spieler, ref h.Bewegung, h.Zustand switch { HuhnZustand.Pickt => "Picken", HuhnZustand.Kratzt => "Kratzen", _ => "Stehen" }); break; }
                var wurf = _tierZufall.Randf();
                if (wurf < (h.Hahn ? 0.25f : 0.5f)) { h.Zustand = HuhnZustand.Pickt; h.Rest = _tierZufall.RandfRange(1.5f, 4f); }
                else if (wurf < (h.Hahn ? 0.35f : 0.7f)) { h.Zustand = HuhnZustand.Kratzt; h.Rest = _tierZufall.RandfRange(1.5f, 3f); }
                else if (wurf < (h.Hahn ? 0.6f : 0.85f)) { h.Zustand = HuhnZustand.Steht; h.Rest = _tierZufall.RandfRange(1.5f, 4f); }
                else { h.Ziel = HofPunkt(hof); h.Zustand = HuhnZustand.Geht; }
                break;
            case HuhnZustand.Geht:
            case HuhnZustand.Flieht:
            case HuhnZustand.Heim:
                var flucht = h.Zustand == HuhnZustand.Flieht;
                var schritt = (flucht ? HuhnFlucht : HuhnTempo) * dt;
                var zu = h.Ziel - pos;
                zu.Y = 0;
                var weit = zu.Length();
                TierSpielen(h.Spieler, ref h.Bewegung, "Gehen");
                if (h.Spieler is { } sp) sp.SpeedScale = flucht ? HuhnFlucht / HuhnTempo : 1;
                TierDrehen(h.Knoten, zu, dt, flucht ? 14f : 8f);
                var arrivalDist = h.Zustand == HuhnZustand.Heim ? 0.3f : 0.1f;
                if (weit <= Mathf.Max(schritt, arrivalDist))
                {
                    if (h.Zustand == HuhnZustand.Heim) { h.Da = false; h.Knoten.Visible = false; break; }
                    h.Zustand = HuhnZustand.Steht;
                    h.Rest = _tierZufall.RandfRange(0.5f, 2.5f);
                    break;
                }
                h.Knoten.Position = AufFuss(pos + zu / weit * schritt);
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hunde und Katzen
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class Streuner
    {
        public required Node3D Knoten { get; init; }
        public AnimationPlayer? Spieler { get; init; }
        public required bool Hund { get; init; }
        public bool Da { get; set; }
        public string Bewegung = "";
        /// <summary>Wartet (nicht da, ruht oder bleibt stehen): Sekunden bis zur nächsten Entscheidung.</summary>
        public float Rest { get; set; } = 20;
        /// <summary>Sekunden, die das Tier noch bleibt, bevor es aus dem Bild geht.</summary>
        public float Bleibt { get; set; }
        public Vector3[] Pfad { get; set; } = Array.Empty<Vector3>();
        public float Laenge { get; set; }
        public float Weg { get; set; }
        public float Tempo { get; set; }
        public bool Geht => Pfad.Length > 0;
        public bool Weggehen { get; set; }
        /// <summary>Katze: seitlicher Versatz zum Ausweichen (Meter, nach rechts positiv) und was sie anstrebt.</summary>
        public float Seitlich { get; set; }
        public float SeitlichZiel { get; set; }
        /// <summary>Blickrichtung, in die sie nach dem Ankommen schaut (Katze am Wandplatz).</summary>
        public Vector3? Blick { get; set; }
    }

    private readonly List<Streuner> _streuner = new();
    private readonly List<(Vector3 Ort, Vector3 Blick)> _wandplaetze = new();

    private void StreunerAnlegen()
    {
        if (_netz.Knoten.Count < 12) return;
        for (var i = 0; i < 4; i++)
        {
            var hund = i % 2 == 0;
            // Fellfarbe: je Tier eine der drei Varianten, aus Karte und Platz gewählt (bei jedem Laden gleich)
            var farbe = (int)((OrtsSaat(_markt) + (ulong)i / 2) % 3);
            var modell = hund ? new[] { "tiere/tier_hund", "tiere/tier_hund_hell", "tiere/tier_hund_schwarz" }[farbe]
                : new[] { "tiere/tier_katze", "tiere/tier_katze_gefleckt", "tiere/tier_katze_schwarz" }[farbe];
            if (Modell(modell) is not { } szene) continue;
            var knoten = szene.Instantiate<Node3D>();
            var mergeUhr = Ladezeit.Uhr();
            FlaechenZusammenlegen(knoten); // Stadtansicht.Flaechenmerge.cs
            Ladezeit.Dazu("Flächen zusammenlegen", mergeUhr);
            knoten.Scale = Vector3.One * TierMassstab;
            knoten.Visible = false;
            _tierEbene!.AddChild(knoten);
            _streuner.Add(new Streuner { Knoten = knoten, Spieler = TierSpieler(knoten), Hund = hund, Rest = TierTest ? 1 + i : _tierZufall.RandfRange(3, 40) + i * 15 });
        }
        WandplaetzeSuchen();
    }

    /// <summary>
    /// Plätze für Katzen an Hauswänden: Von der Mitte einiger Wegstücke des Gassennetzes seitwärts tasten, bis ein Gebäude (Hülle eines Netzes,
    /// mindestens 2,5 m hoch) kommt; 0,8 m davor ist der Platz, die Katze schaut zur Gasse.
    /// </summary>
    private void WandplaetzeSuchen()
    {
        _wandplaetze.Clear();
        var huellen = new List<Aabb>();
        foreach (var teil in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz || !teil.IsVisibleInTree() || _vogelEbene?.IsAncestorOf(teil) == true || _tierEbene?.IsAncestorOf(teil) == true) continue;
            var huelle = teil.GlobalTransform * netz.GetAabb();
            if (huelle.Size.X > 40 || huelle.Size.Z > 40 || huelle.End.Y < 2.5f || huelle.Position.Y > 1.5f) continue;
            huellen.Add(huelle);
        }
        bool Getroffen(Vector3 q) { var p = new Vector3(q.X, 1.2f, q.Z); foreach (var h in huellen) if (h.HasPoint(p)) return true; return false; }
        foreach (var (von, bis) in _netz.Kanten)
        {
            var laenge = von.DistanceTo(bis);
            if (laenge < 5 || !InDerStadt(von)) continue;
            var richtung = (bis - von) with { Y = 0 };
            richtung = richtung.Normalized();
            var seite = new Vector3(-richtung.Z, 0, richtung.X);
            foreach (var t in new[] { 0.3f, 0.7f })
            {
                var mitte = von.Lerp(bis, t);
                foreach (var vorzeichen in new[] { 1f, -1f })
                {
                    for (var d = 1.6f; d <= 5f; d += 0.4f)
                    {
                        if (!Getroffen(mitte + seite * vorzeichen * d)) continue;
                        var platz = mitte + seite * vorzeichen * (d - 0.9f);
                        if (!Getroffen(platz) && !_wandplaetze.Any(w => w.Ort.DistanceSquaredTo(platz) < 9))
                            _wandplaetze.Add((AufFuss(platz), -seite * vorzeichen));
                        break;
                    }
                }
            }
        }
    }

    private bool Nacht => _daemmerung >= VogelNachtAb;

    private Vector3? ZufallsKnoten(Vector3 nahBei, float vonWeit, float bisWeit, bool ausserhalbBild)
    {
        var moeglich = new List<Vector3>();
        foreach (var k in _netz.Knoten)
        {
            var d = Waagerecht(k, nahBei);
            if (d < vonWeit || d > bisWeit || !InDerStadt(k)) continue;
            if (ausserhalbBild && ImBild(k)) continue;
            moeglich.Add(k);
        }
        return moeglich.Count == 0 ? null : moeglich[_tierZufall.RandiRange(0, moeglich.Count - 1)];
    }

    private void StreunerAktualisieren(float dt)
    {
        foreach (var s in _streuner)
        {
            if (!s.Da)
            {
                if (dt <= 0) continue;
                s.Rest -= dt;
                if (s.Rest <= 0) StreunerErscheinen(s);
                continue;
            }
            var imBild = ImBild(s.Knoten.Position);
            if (s.Spieler is { } sp && !s.Geht) sp.SpeedScale = imBild ? 1 : 0;
            if (dt <= 0) { if (s.Spieler is { } p0) p0.SpeedScale = 0; continue; }
            s.Bleibt -= dt;
            // Zeit um und außer Sicht: weg (taucht später an anderer Stelle wieder auf)
            if (s.Bleibt <= 0 && !imBild && !s.Geht) { StreunerVerschwinden(s); continue; }
            if (s.Geht) StreunerLaufen(s, dt, imBild);
            else StreunerRuhen(s, dt, imBild);
        }
    }

    private void StreunerVerschwinden(Streuner s)
    {
        s.Da = false;
        s.Knoten.Visible = false;
        s.Rest = _tierZufall.RandfRange(Nacht ? 120 : 45, Nacht ? 300 : 160);
    }

    private void StreunerErscheinen(Streuner s)
    {
        // Katzen gern an einer Hauswand, sonst ein Knoten des Netzes; beides außerhalb des Bildes und weit von Passanten
        Vector3? ort = null;
        Vector3? blick = null;
        if (!s.Hund && _wandplaetze.Count > 0 && _tierZufall.Randf() < 0.65f)
        {
            var frei = _wandplaetze.Where(w => (TierTest ? Waagerecht(w.Ort, _markt) < 30 : !ImBild(w.Ort)) && !PassantNaheWeg(w.Ort, 6, out _)).ToList();
            if (frei.Count > 0) { var w = frei[_tierZufall.RandiRange(0, frei.Count - 1)]; ort = w.Ort; blick = w.Blick; }
        }
        ort ??= ZufallsKnoten(_markt, TierTest ? 8 : 0, TierTest ? 30 : StadtReichweite, !TierTest);
        if (ort is not { } o || PassantNaheWeg(o, 6, out _)) { s.Rest = _tierZufall.RandfRange(4, 12); return; }
        s.Da = true;
        s.Knoten.Visible = true;
        s.Knoten.Position = AufFuss(o);
        s.Knoten.Rotation = new Vector3(0, blick is { } b ? Mathf.Atan2(b.X, b.Z) : _tierZufall.RandfRange(0, Mathf.Tau), 0);
        s.Blick = blick;
        s.Pfad = Array.Empty<Vector3>();
        s.Seitlich = s.SeitlichZiel = 0;
        s.Bleibt = TierTest ? 900 : _tierZufall.RandfRange(70, 200);
        s.Rest = _tierZufall.RandfRange(2, 8);
        s.Bewegung = "";
        TierSpielen(s.Spieler, ref s.Bewegung, s.Hund ? "Stehen" : "Sitzen");
        if (TierTest) GD.Print($"[tiere] {(s.Hund ? "Hund" : "Katze")} erscheint bei {o.X:0.0}, {o.Z:0.0}");
    }

    private void StreunerRuhen(Streuner s, float dt, bool imBild)
    {
        var pos = s.Knoten.Position;
        if (!s.Hund && PassantNaheWeg(pos, 1.8f, out _) && imBild)
        {
            // Katze: ein Passant kommt zu nah, sie huscht zu einem anderen Platz (oder aus dem Bild)
            StreunerWeg(s, 8, 35, 2.0f, s.Bleibt <= 0);
            return;
        }
        s.Rest -= dt;
        if (s.Rest > 0) return;
        var w = _tierZufall.Randf();
        var nacht = Nacht;
        if (s.Bleibt <= 0) { StreunerWeg(s, 20, 70, s.Hund ? HundGehen : KatzeGehen, true); return; } // bleibt nicht mehr: aus dem Bild gehen
        if (s.Hund)
        {
            if (w < (nacht ? 0.04f : 0.35f)) StreunerWeg(s, 12, 55, HundGehen, false);
            else if (nacht || w < 0.6f) { TierSpielen(s.Spieler, ref s.Bewegung, "Liegen"); s.Rest = _tierZufall.RandfRange(nacht ? 40 : 12, nacht ? 120 : 40); }
            else if (w < 0.8f) { TierSpielen(s.Spieler, ref s.Bewegung, "Sitzen"); s.Rest = _tierZufall.RandfRange(5, 15); }
            else if (w < 0.92f) { TierSpielen(s.Spieler, ref s.Bewegung, "Schnueffeln"); s.Rest = _tierZufall.RandfRange(3, 7); }
            else { TierSpielen(s.Spieler, ref s.Bewegung, "Stehen"); s.Rest = _tierZufall.RandfRange(3, 8); }
        }
        else
        {
            if (w < (nacht ? 0.08f : 0.3f)) StreunerWeg(s, 8, 45, KatzeGehen, false);
            else if (w < 0.55f) { TierSpielen(s.Spieler, ref s.Bewegung, "Sitzen"); s.Rest = _tierZufall.RandfRange(8, 30); }
            else if (w < 0.7f) { TierSpielen(s.Spieler, ref s.Bewegung, "Putzen"); s.Rest = _tierZufall.RandfRange(4, 9); }
            else if (w < 0.85f) { TierSpielen(s.Spieler, ref s.Bewegung, "Hocken"); s.Rest = _tierZufall.RandfRange(10, 40); }
            else { TierSpielen(s.Spieler, ref s.Bewegung, "Liegen"); s.Rest = _tierZufall.RandfRange(15, 60); }
        }
    }

    /// <summary>Sucht ein Ziel (Katze gern eine Hauswand) in <paramref name="vonWeit"/>–<paramref name="bisWeit"/> m und legt den Weg dorthin.</summary>
    private void StreunerWeg(Streuner s, float vonWeit, float bisWeit, float tempo, bool ausDemBild)
    {
        var pos = s.Knoten.Position;
        Vector3? ziel = null;
        Vector3? blick = null;
        if (!s.Hund && !ausDemBild && _tierZufall.Randf() < 0.6f)
        {
            var frei = _wandplaetze.Where(w => Waagerecht(w.Ort, pos) is var d && d >= vonWeit && d <= bisWeit && !PassantNaheWeg(w.Ort, 3, out _)).ToList();
            if (frei.Count > 0) { var w = frei[_tierZufall.RandiRange(0, frei.Count - 1)]; ziel = w.Ort; blick = w.Blick; }
        }
        ziel ??= ZufallsKnoten(pos, vonWeit, bisWeit, ausDemBild);
        if (ziel is not { } z) { s.Rest = _tierZufall.RandfRange(5, 15); return; }
        var von = _netz.KnotenBei(pos);
        var nach = _netz.KnotenBei(z);
        var weg = _netz.Pfad(von, nach);
        var punkte = new List<Vector3> { pos };
        punkte.AddRange(weg);
        if (punkte[^1].DistanceTo(z) > 0.3f) punkte.Add(z);
        if (punkte.Count < 2) { s.Rest = _tierZufall.RandfRange(5, 15); return; }
        s.Pfad = punkte.ToArray();
        s.Laenge = Gassennetz.Laenge(s.Pfad);
        s.Weg = 0;
        s.Weggehen = ausDemBild;
        s.Blick = blick;
        s.Tempo = s.Hund && s.Laenge > 25 && _tierZufall.Randf() < 0.5f && !Nacht ? HundTraben : tempo;
    }

    private void StreunerLaufen(Streuner s, float dt, bool imBild)
    {
        var tempo = s.Tempo;
        if (!s.Hund)
        {
            // Katze weicht Passanten aus: seitlich an der Gasse vorbei, bei großer Nähe schneller
            if (PassantNaheWeg(s.Knoten.Position, 3.2f, out var weg))
            {
                var richtung = Gassennetz.PunktAuf(s.Pfad, Mathf.Min(1, (s.Weg + 0.6f) / s.Laenge), s.Laenge) - s.Knoten.Position;
                richtung.Y = 0;
                var rechts = new Vector3(richtung.Z, 0, -richtung.X).Normalized();
                s.SeitlichZiel = rechts.Dot(weg) >= 0 ? 0.9f : -0.9f;
                if (PassantNaheWeg(s.Knoten.Position, 1.8f, out _)) tempo = Mathf.Max(tempo, 1.8f);
            }
            else s.SeitlichZiel = 0;
            s.Seitlich = Mathf.MoveToward(s.Seitlich, s.SeitlichZiel, 2f * dt);
        }
        s.Weg += tempo * dt;
        var anteil = Mathf.Min(1, s.Weg / s.Laenge);
        var hier = Gassennetz.PunktAuf(s.Pfad, anteil, s.Laenge);
        var voraus = Gassennetz.PunktAuf(s.Pfad, Mathf.Min(1, (s.Weg + 0.7f) / s.Laenge), s.Laenge);
        var vorn = voraus - hier;
        vorn.Y = 0;
        var rechtsV = vorn.LengthSquared() > 1e-6f ? new Vector3(vorn.Z, 0, -vorn.X).Normalized() : Vector3.Zero;
        s.Knoten.Position = AufFuss(hier + rechtsV * s.Seitlich);
        TierDrehen(s.Knoten, vorn, dt, 7f);
        var name = s.Hund && tempo >= HundTraben ? "Traben" : "Gehen";
        TierSpielen(s.Spieler, ref s.Bewegung, name);
        if (s.Spieler is { } sp)
            sp.SpeedScale = !imBild ? 0 : tempo / (s.Hund ? (tempo >= HundTraben ? HundTraben : HundGehen) : KatzeGehen);
        if (anteil < 1) return;
        // angekommen
        s.Pfad = Array.Empty<Vector3>();
        s.Seitlich = s.SeitlichZiel = 0;
        s.Rest = _tierZufall.RandfRange(1, 4);
        if (s.Weggehen) { s.Bleibt = 0; s.Weggehen = false; }
        if (s.Blick is { } b) s.Knoten.Rotation = new Vector3(0, Mathf.Atan2(b.X, b.Z), 0);
        TierSpielen(s.Spieler, ref s.Bewegung, s.Hund ? "Stehen" : "Sitzen");
    }
}
