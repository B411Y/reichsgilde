using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Raumansicht – vierter Teil der Stadtansicht.
///
/// Statt ein Gebäude in der Stadt aufzuschneiden (Diorama), betritt man es: Eine eigene Kamera mit Perspektive
/// blickt fest in einen Raum, wie auf eine Bühne. Die Vorderwand fehlt, Rückwand und Seitenwände stehen.
/// Zwischen den Räumen eines Gebäudes wechselt man mit der Raumleiste (Hauptszene) oder mit Q/E.
///
/// Wo stehen die Räume? Alle Räume eines Gebäudes hängen am Knoten „Innen", und der liegt 200 m unter der Stadt
/// (<see cref="RaumTiefe"/>). Die Räume eines Gebäudes stehen dort nebeneinander, je 40 m auseinander, damit die Kamera
/// nie zwei auf einmal sieht. So stört nichts aus der Stadt: Der Boden der Stadt ist von unten unsichtbar
/// (eine Ebene hat nur eine Vorderseite), und die Sonne wird in der Raumansicht abgeschaltet – ihr Licht käme
/// sonst ungehindert durch die Decke. Licht im Raum kommt von Laternen, Feuer und den Fenstern (Umgebungslicht nach der Uhrzeit).
///
/// Werkstätten: Werkraum (Einrichtung des Berufs, Stadtansicht.Innenraeume.cs) · Lager (Kistenstapel) ·
/// Kontor und Wohnstube stehen im Wohnhaus des Spielers (Stadtansicht.Spielhaeuser.cs); nur ohne Wohnhäuser
/// (Abschnitt „wohnen" fehlt) hängen sie wie früher an der eigenen Werkstatt (Kontor) bzw. am Stammhaus (Wohnstube).
/// Rathaus: Ratssaal, Gerichtssaal, Amtsstube, Kerker (Stadtansicht.Sonderbauten.cs). Der Dom ist vorerst noch ein Diorama.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Raum: Kennung (für Klicks), Name (Raumleiste), Mitte relativ zur Gebäudemitte, Innenmaße (Breite, Höhe, Tiefe).
    /// <paramref name="Zugaenglich"/>: nur betretbar, solange das gilt (z. B. nur im eigenen Haus).
    /// <paramref name="Blicke"/>: bei Räumen aus Modellen die festen Blickpunkte der Kamera (Stadtansicht.Innenkamera.cs), der erste ist der Standard.</summary>
    private sealed record Raum(string Kennung, string Name, Vector3 Mitte, Vector3 Masse, Func<bool>? Zugaenglich = null, List<Blickpunkt>? Blicke = null);

    /// <summary>Kennungen der Werkstatträume (Klick in der Raumansicht).</summary>
    public const string RaumWerk = "raum:werk", RaumLager = "raum:lager", RaumSaege = "raum:saege", RaumKontor = "raum:kontor", RaumWohnstube = "raum:wohnstube";

    /// <summary>So tief unter der Stadt liegen die Räume.</summary>
    private const float RaumTiefe = -200f;

    /// <summary>Abstand der Räume eines Gebäudes voneinander (entlang X).</summary>
    private const float RaumAbstand = 40f;

    /// <summary>Größe der Schilder im Raum (die Stadt skaliert ihre Schilder mit dem Zoom, im Raum gibt es keinen).</summary>
    private const float RaumSchildMassstab = 0.16f;

    // Lage der Werkstatträume relativ zur Gebäudemitte
    private static readonly Vector3 LagerOrt = new(RaumAbstand, 0, 0);
    private static readonly Vector3 KontorOrt = new(2 * RaumAbstand, 0, 0);
    private static readonly Vector3 WohnstubeOrt = new(3 * RaumAbstand, 0, 0);

    private Godot.Environment _raumUmgebung = null!;
    /// <summary>Fensterglas aller Räume: leuchtet in der Farbe des Himmels, nachts kaum.</summary>
    private StandardMaterial3D _fensterglas = null!;
    /// <summary>Helligkeit draußen (0 = Nacht, 1 = Tag): die Fenster der Modellräume werfen Tageslicht (Stadtansicht.Innenmodell.cs).</summary>
    private float _raumHell;

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera und Wechsel
    // ═════════════════════════════════════════════════════════════════════════

    private void RaumKameraAufbauen()
    {
        _raumUmgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.03f, 0.025f, 0.02f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.92f, 0.82f, 0.66f),
            AmbientLightEnergy = 0.45f,
        };
        // Perspektive (Fluchtpunkte) statt der Schrägansicht der Stadt: Man steht im Raum.
        _raumKamera = new Camera3D { Name = "Raumkamera", Fov = 50, Near = 0.1f, Far = 120, Environment = _raumUmgebung };
        AddChild(_raumKamera);
        _fensterglas = Material(new Color(0.2f, 0.22f, 0.26f));
        _fensterglas.EmissionEnabled = true;
        RaumOberflaecheAufbauen(); // Pfeile, Rahmen und Namensschild (Stadtansicht.Innenkamera.cs)
    }

    /// <summary>Tag und Nacht im Raum: Umgebungslicht und Fenster folgen dem Himmel draußen (<paramref name="hell"/> 0 = Nacht, 1 = Tag).</summary>
    private void RaumlichtSetzen(Color himmel, float hell)
    {
        _raumHell = hell;
        _raumUmgebung.AmbientLightEnergy = 0.2f + 0.35f * hell;
        _fensterglas.Emission = himmel;
        _fensterglas.EmissionEnergyMultiplier = 0.2f + 1.1f * hell;
    }

    private static IEnumerable<Raum> Zugaengliche(InnenAnsicht a) => a.Raeume.Where(r => r.Zugaenglich?.Invoke() ?? true);

    /// <summary>Raumansicht an oder aus: Kamera umschalten, Sonne aus, Räume zeigen und anklickbar machen.</summary>
    private void RaumansichtUmschalten(InnenAnsicht a, bool innen)
    {
        a.Innen!.Visible = innen;
        foreach (var klick in a.Raumklicks) klick.Disabled = !innen;
        _sonne.Visible = !innen;
        RaumOberflaecheUmschalten(innen);
        if (innen) _raumKamera.MakeCurrent();
        else
        {
            _kamera.MakeCurrent();
            _raum = null;
            if (a is BetriebsAnsicht { Modellraum: { } md }) InnenModellAnhalten(md); // die Animation „Arbeit“ läuft nicht im verlassenen Raum weiter
        }
    }

    /// <summary>
    /// Die Kamera blickt fest in den Raum: Sie steht vor der offenen Vorderseite, etwas erhöht, und schaut leicht nach unten
    /// auf die Raummitte. Der Abstand richtet sich nach der Breite, damit der ganze Raum ins Bild passt.
    /// </summary>
    private void RaumZeigen(Raum r, bool melden = true)
    {
        if (_innen?.Innen is not { } innen) return;
        _raum = r;
        if (r.Blicke is { Count: > 0 } blicke)
        {
            BlickSetzen(blicke[0], true); // Raum aus einem Modell: die Kamera steht auf dem Blickpunkt 0
            if (melden) RaumGewechselt?.Invoke(r.Kennung);
            return;
        }
        var mitte = innen.ToGlobal(_innen.Mitte + r.Mitte);
        var (breite, hoehe, tiefe) = (r.Masse.X, r.Masse.Y, r.Masse.Z);
        var abstand = breite * 0.62f + 0.8f;
        var auge = mitte + new Vector3(0, hoehe * 0.72f, tiefe / 2 + abstand);
        var ziel = mitte + new Vector3(0, 0.9f, -tiefe * 0.15f);
        _raumKamera.LookAtFromPosition(auge, ziel, Vector3.Up);
        if (melden) RaumGewechselt?.Invoke(r.Kennung);
    }

    /// <summary>Zeigt den Raum mit dieser Kennung (Raumleiste). Gibt false zurück, wenn es ihn hier nicht gibt.</summary>
    public bool RaumWaehlen(string kennung)
    {
        if (_innen is null || Zugaengliche(_innen).FirstOrDefault(r => r.Kennung == kennung) is not { } raum) return false;
        if (raum != _raum) RaumZeigen(raum);
        return true;
    }

    /// <summary>Zeigt den Raum an dieser Stelle der Raumleiste (0 = erster Raum, Startoption --raum).</summary>
    public bool RaumWaehlen(int nummer) =>
        _innen is not null && Zugaengliche(_innen).ElementAtOrDefault(nummer) is { } raum && RaumWaehlen(raum.Kennung);

    /// <summary>Nächster (+1) oder vorheriger (−1) Raum, am Ende geht es von vorn los.</summary>
    public void RaumBlaettern(int schritt)
    {
        if (_innen is null || _raum is null) return;
        var raeume = Zugaengliche(_innen).ToList();
        if (raeume.Count == 0) return;
        var stelle = Math.Max(0, raeume.IndexOf(_raum));
        RaumZeigen(raeume[((stelle + schritt) % raeume.Count + raeume.Count) % raeume.Count]);
    }

    /// <summary>Eingabe in der Raumansicht: Klick = Raum anklicken, Q/E = Raum wechseln. Kein Zoom, kein Verschieben.</summary>
    private void RaumEingabe(InputEvent eingabe)
    {
        if (_raum?.Blicke is not null && ModellRaumEingabe(eingabe)) // Raum aus einem Modell: Bedienung, Rahmen, Rechtsklick (Stadtansicht.Innenkamera.cs)
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (eingabe)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick when _raum?.Blicke is null:
                Auswaehlen(klick.Position, false);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                InnenansichtVerlassen(); // Rechtsklick verlässt das Haus, in jedem Raum
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q }:
                RaumBlaettern(-1);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.E }:
                RaumBlaettern(1);
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bühne: Wände, Boden, Fenster, Klickfläche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut die Bühne eines Raums: Rückwand (Norden), zwei Seitenwände, Deckenbalken, auf Wunsch Boden und Fenster
    /// in der Rückwand, eine Klickfläche über den ganzen Raum und ein Grundlicht. Die Vorderseite bleibt offen – dort steht die Kamera.
    /// </summary>
    private Raum RaumAnlegen(InnenAnsicht a, Raum raum, Color wand, Color? boden, bool fenster = true, float licht = 0.5f)
    {
        a.Raeume.Add(raum);
        var innen = a.Innen!;
        var m = a.Mitte + raum.Mitte;
        var (breite, hoehe, tiefe) = (raum.Masse.X, raum.Masse.Y, raum.Masse.Z);
        const float dicke = 0.3f;
        var wandMaterial = Material(wand);
        Quader(m + new Vector3(0, hoehe / 2, -tiefe / 2 - dicke / 2), new Vector3(breite + 2 * dicke, hoehe, dicke), wandMaterial, innen);
        Quader(m + new Vector3(-breite / 2 - dicke / 2, hoehe / 2, 0), new Vector3(dicke, hoehe, tiefe), wandMaterial, innen);
        Quader(m + new Vector3(breite / 2 + dicke / 2, hoehe / 2, 0), new Vector3(dicke, hoehe, tiefe), wandMaterial, innen);
        // Sockel aus dunklem Holz unten an den Wänden – gibt dem Raum eine Kante
        var sockel = Material(DunklesHolz);
        Quader(m + new Vector3(0, 0.25f, -tiefe / 2 + 0.03f), new Vector3(breite, 0.5f, 0.06f), sockel, innen);
        if (boden is { } bodenfarbe)
            Quader(m + new Vector3(0, 0.05f, 0), new Vector3(breite, 0.1f, tiefe), Material(bodenfarbe), innen);
        var balken = Material(DunklesHolz);
        for (var i = -1; i <= 1; i++)
            Quader(m + new Vector3(i * breite / 3, hoehe - 0.18f, 0), new Vector3(0.28f, 0.36f, tiefe), balken, innen);
        if (fenster)
            foreach (var x in new[] { -breite / 4, breite / 4 })
            {
                Quader(m + new Vector3(x, hoehe * 0.62f, -tiefe / 2 + 0.02f), new Vector3(1.0f, 1.3f, 0.05f), _fensterglas, innen);
                Quader(m + new Vector3(x, hoehe * 0.62f, -tiefe / 2 + 0.05f), new Vector3(0.08f, 1.3f, 0.04f), balken, innen); // Fensterkreuz
                Quader(m + new Vector3(x, hoehe * 0.62f, -tiefe / 2 + 0.05f), new Vector3(1.0f, 0.08f, 0.04f), balken, innen);
            }
        var klick = Klickflaeche(raum.Kennung, m + new Vector3(0, hoehe / 2, 0), new Vector3(breite, hoehe, tiefe), innen);
        klick.Disabled = true;
        a.Raumklicks.Add(klick);
        innen.AddChild(new OmniLight3D
        {
            LightColor = new Color(1f, 0.85f, 0.6f),
            OmniRange = Mathf.Max(breite, tiefe) * 1.1f,
            LightEnergy = licht,
            Position = m + new Vector3(0, hoehe * 0.8f, tiefe * 0.15f),
        });
        return raum;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkstätten: Werkraum, Lager, Kontor, Wohnstube
    // ═════════════════════════════════════════════════════════════════════════

    private static string WerkraumName(Werkstil stil) => stil switch
    {
        Werkstil.Schaenke => "Schankstube",
        Werkstil.Kirche => "Kirchenraum",
        Werkstil.Diebesnest => "Versteck",
        Werkstil.Wache => "Wachstube",
        Werkstil.Bank => "Wechselstube",
        Werkstil.Siederei => "Siedestube",
        Werkstil.Tinkturei => "Laboratorium",
        _ => "Werkstatt",
    };

    /// <summary>Gehört dieser Betrieb dem Spieler? (Abgefragt, nicht gemerkt – Betriebe wechseln den Besitzer.)</summary>
    private bool GehoertSpieler(BetriebsAnsicht a) => _spielerKonto is not null && a.Betrieb.Konto == _spielerKonto;

    /// <summary>Das Stammhaus ist die erste Werkstatt des Spielers in der Stadt – dort wohnt die Familie.</summary>
    private bool IstStammhaus(BetriebsAnsicht a) =>
        GehoertSpieler(a) && _betriebe.Values.FirstOrDefault(x => x.Raumansicht && GehoertSpieler(x)) == a;

    /// <summary>
    /// Wem Kontor und Wohnstube gehören – damit dieselbe Einrichtung in der Werkstatt und im Wohnhaus stehen kann.
    /// Alles als Abfrage (Func), nicht als fester Wert: Betriebe wechseln den Besitzer, das Wohnhaus entsteht erst nach dem Laden.
    /// </summary>
    /// <param name="Konto">das Haus, dessen Kasse und Familie gezeigt werden</param>
    /// <param name="Farbe">Hausfarbe (Wandbehang, Gewand des Oberhaupts)</param>
    /// <param name="Zugaenglich">ob der Raum in der Raumleiste steht</param>
    /// <param name="FamilieWohntHier">ob das Oberhaupt am Pult sitzt (sonst ein Schreiber) und die Familie in der Stube</param>
    /// <param name="Klickhinweis">letzte Zeile des Kontorschilds (leer = keine)</param>
    private sealed record Hausstand(Func<Konto?> Konto, Func<Color> Farbe, Func<bool> Zugaenglich, Func<bool> FamilieWohntHier, string Klickhinweis);

    /// <summary>Stehen Kontor und Wohnstube im Wohnhaus? Dann fehlen sie in den Werkstätten.</summary>
    private bool RaeumeImWohnhaus => _wohnhausAnsicht is not null;

    /// <summary>
    /// Legt die Räume einer Werkstatt an. Der Werkraum ist der bisherige Innenraum (Boden und Einrichtung baut
    /// <see cref="Innenraum"/>), hier kommen nur seine Wände dazu.
    /// </summary>
    private static Color WandVon(Werkstil stil) => stil switch
    {
        Werkstil.Maurerei or Werkstil.Kirche or Werkstil.Wache => Mauerstein,
        Werkstil.Tinkturei or Werkstil.Bank => Putz,
        Werkstil.Diebesnest => Morsch,
        _ => Fachwerk,
    };

    private void WerkstattRaeumeAnlegen(BetriebsAnsicht a, Werkstil stil, float breite, float tiefe)
    {
        var wand = WandVon(stil);
        RaumAnlegen(a, new Raum(RaumWerk, WerkraumName(stil), Vector3.Zero, new Vector3(breite, 6, tiefe)), wand, null,
            fenster: stil != Werkstil.Diebesnest, licht: stil == Werkstil.Diebesnest ? 0.15f : 0.35f);
        LagerEinrichten(a, wand);
        KontorUndWohnstubeAnlegen(a, wand);
    }

    /// <summary>Kontor und Wohnstube der Werkstatt (auch bei Werkstätten mit Modellraum): gebaut, aber ausgeblendet, sobald es das Wohnhaus gibt (siehe <see cref="RaeumeImWohnhaus"/>).</summary>
    private void KontorUndWohnstubeAnlegen(BetriebsAnsicht a, Color wand)
    {
        KontorEinrichten(a, KontorOrt, wand, new Hausstand(() => a.Betrieb.Konto, () => a.Farbe,
            () => GehoertSpieler(a) && !RaeumeImWohnhaus, () => IstStammhaus(a), "Klick: Verwaltung"));
        WohnstubeEinrichten(a, WohnstubeOrt, new Hausstand(() => a.Betrieb.Konto, () => a.Farbe,
            () => IstStammhaus(a) && !RaeumeImWohnhaus, () => true, ""));
    }

    /// <summary>Lager: Kistenstapel je Ware an der Rückwand (<see cref="KistenspalteBauen"/>), Säcke und eine Waage.</summary>
    private void LagerEinrichten(BetriebsAnsicht a, Color wand)
    {
        RaumAnlegen(a, new Raum(RaumLager, "Lager", LagerOrt, new Vector3(8.4f, 4.5f, 6.4f)), wand, Lehm, fenster: false, licht: 0.6f);
        var holz = Material(Holz);
        var sack = Material(new Color(0.62f, 0.54f, 0.40f));
        foreach (var (dx, dz) in new[] { (-3.4f, 2.2f), (-2.8f, 2.5f), (-3.3f, 1.5f) })
            Kugel(a, LagerOrt + new Vector3(dx, 0.4f, dz), 0.42f, sack, hoehe: 0.8f);
        // Waage am Stehpult (vorne rechts)
        Teil(a, LagerOrt + new Vector3(3.2f, 0.55f, 2.2f), new Vector3(1.0f, 1.1f, 0.7f), holz);
        var eisen = Metall(new Color(0.30f, 0.31f, 0.34f));
        Teil(a, LagerOrt + new Vector3(3.2f, 1.45f, 2.2f), new Vector3(0.06f, 0.7f, 0.06f), eisen);
        Teil(a, LagerOrt + new Vector3(3.2f, 1.8f, 2.2f), new Vector3(0.8f, 0.04f, 0.04f), eisen);
        foreach (var dx in new[] { -0.38f, 0.38f })
            Teil(a, LagerOrt + new Vector3(3.2f + dx, 1.55f, 2.2f), new Vector3(0.24f, 0.03f, 0.24f), eisen);
    }

    /// <summary>
    /// Kontor: Schreibpult mit Büchern und Kerze, Aktenregal, Geldtruhe, Wandbehang in der Hausfarbe.
    /// Am Pult sitzt tagsüber das Oberhaupt (wo die Familie wohnt), sonst ein Schreiber.
    /// </summary>
    private void KontorEinrichten(InnenAnsicht a, Vector3 k, Color wand, Hausstand haus)
    {
        RaumAnlegen(a, new Raum(RaumKontor, "Kontor", k, new Vector3(7.4f, 4.5f, 6.2f), haus.Zugaenglich), wand, Dielen);
        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var gold = Metall(Gold);
        var hausfarbe = Material(haus.Farbe());

        // Wandbehang in der Hausfarbe hinter dem Pult
        Teil(a, k + new Vector3(0, 2.9f, -3.05f), new Vector3(1.4f, 1.9f, 0.05f), hausfarbe);
        Teil(a, k + new Vector3(0, 3.9f, -3.03f), new Vector3(1.7f, 0.08f, 0.08f), dunkel);
        // Schreibpult mit Büchern, Tintenfass und Kerze
        Teil(a, k + new Vector3(0, 0.5f, -1.4f), new Vector3(2.4f, 1.0f, 0.9f), holz);
        Teil(a, k + new Vector3(0, 1.03f, -1.4f), new Vector3(2.5f, 0.06f, 1.0f), dunkel);
        Teil(a, k + new Vector3(-0.3f, 1.09f, -1.35f), new Vector3(0.7f, 0.05f, 0.5f), Material(Pergament));       // aufgeschlagenes Buch
        Teil(a, k + new Vector3(-0.85f, 1.16f, -1.5f), new Vector3(0.35f, 0.2f, 0.45f), Material(Stadtrot));         // Rechnungsbuch
        Teil(a, k + new Vector3(0.35f, 1.12f, -1.2f), new Vector3(0.1f, 0.12f, 0.1f), Material(new Color(0.08f, 0.08f, 0.1f)));
        var flamme = Leuchtend(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.7f, 0.3f));
        Rund(a, k + new Vector3(0.9f, 1.25f, -1.5f), 0.05f, 0.35f, Material(Leinen), segmente: 8);
        Teil(a, k + new Vector3(0.9f, 1.47f, -1.5f), new Vector3(0.06f, 0.1f, 0.06f), flamme);
        FeuerAnlegen(a, k + new Vector3(0.9f, 1.8f, -1.2f), flamme, lichtAn: 0.9f, lichtAus: 0.9f, glutAn: 2f, glutAus: 2f, an: () => true, reichweite: 6);
        // Stuhl hinter dem Pult
        Teil(a, k + new Vector3(0, 0.42f, -2.3f), new Vector3(0.6f, 0.08f, 0.6f), dunkel);
        Teil(a, k + new Vector3(0, 0.9f, -2.6f), new Vector3(0.6f, 1.0f, 0.08f), dunkel);
        // Aktenregal an der Westwand mit bunten Buchrücken
        Teil(a, k + new Vector3(-3.4f, 1.5f, -1.0f), new Vector3(0.5f, 3.0f, 2.6f), dunkel);
        var ruecken = new[] { new Color(0.45f, 0.16f, 0.14f), new Color(0.20f, 0.28f, 0.40f), new Color(0.36f, 0.40f, 0.22f), Pergament };
        for (var fach = 0; fach < 4; fach++)
            for (var i = 0; i < 6; i++)
                Teil(a, k + new Vector3(-3.12f, 0.45f + fach * 0.7f, -2.0f + i * 0.38f), new Vector3(0.08f, 0.5f, 0.3f), Material(ruecken[(fach + i) % ruecken.Length]));
        // Geldtruhe mit Münzen an der Ostwand
        Teil(a, k + new Vector3(2.9f, 0.4f, -0.6f), new Vector3(1.2f, 0.8f, 0.8f), holz);
        Teil(a, k + new Vector3(2.9f, 0.82f, -0.6f), new Vector3(1.25f, 0.06f, 0.85f), Metall(new Color(0.30f, 0.31f, 0.34f)));
        foreach (var (dx, hoch) in new[] { (-0.3f, 0.2f), (0f, 0.3f), (0.3f, 0.15f) })
            Rund(a, k + new Vector3(2.9f + dx, 0.85f + hoch / 2, 0.1f), 0.1f, hoch, gold, segmente: 10);

        var schild = Beschriftung("", a.Mitte + k + new Vector3(0, 3.9f, -1.4f), 30, a.Innen);
        Arbeiterfigur? figur = null;
        var figurStand = "";
        a.Raumzustaende.Add((zeit, arbeitszeit) =>
        {
            var farbe = haus.Farbe();
            hausfarbe.AlbedoColor = farbe;
            if (_raum?.Kennung != RaumKontor || haus.Konto() is not { } konto) return; // nur der gezeigte Raum braucht Figuren und Text
            var w = _simulation?.Wirtschaft;
            var oberhaupt = haus.FamilieWohntHier() ? w?.Familien.Von(konto)?.Oberhaupt : null;
            var stand = oberhaupt is null ? "Schreiber" : $"{oberhaupt.Nummer}:{farbe}";
            if (stand != figurStand)
            {
                if (figur is not null) { a.Besucher.Remove(figur); KnotenEntfernen(figur.Koerper); }
                var platz = new Arbeitsplatz(k + new Vector3(0, 0, -2.25f), Sued, Bewegung.Schreiben, Werkzeug.Feder, true);
                figur = oberhaupt is not null
                    ? PersonBauen(a, a.Innen!, platz, oberhaupt.Volk, farbe, Kopfputz.Barett, 0.3f)
                    : PersonBauen(a, a.Innen!, platz, Volk.Mensch, new Color(0.22f, 0.26f, 0.36f), Kopfputz.Barett, 0.3f);
                a.Besucher.Add(figur);
                figurStand = stand;
            }
            figur!.Koerper.Visible = arbeitszeit;
            var gulden = w?.Daten.Allgemein.Waehrung ?? "Gulden";
            schild.Text = $"Kontor · {konto.Inhaber}\n{Zahlenformat.Ganz(konto.Vermoegen)} {gulden} in der Kasse"
                        + (haus.Klickhinweis == "" ? "" : "\n" + haus.Klickhinweis);
        });
    }

    /// <summary>
    /// Wohnstube: Herd mit Feuer, Tisch mit Bänken, Bett, Truhe, Wiege. Die Familie aus dem Familienbuch
    /// sitzt hier: Ehepartner und Kinder immer, das Oberhaupt nach Feierabend (tagsüber sitzt es im Kontor). Klick öffnet die Familie.
    /// </summary>
    private void WohnstubeEinrichten(InnenAnsicht a, Vector3 s, Hausstand haus)
    {
        RaumAnlegen(a, new Raum(RaumWohnstube, "Wohnstube", s, new Vector3(8.4f, 4.5f, 6.6f), haus.Zugaenglich), Fachwerk, Dielen, licht: 0.4f);
        var holz = Material(Holz);
        var hell = Material(HellesHolz);
        var dunkel = Material(DunklesHolz);
        var stein = Material(Mauerstein);

        // Herd mit Rauchfang und Kessel (hinten links), das Feuer brennt immer
        Teil(a, s + new Vector3(-3.1f, 0.5f, -2.5f), new Vector3(1.8f, 1.0f, 1.3f), stein);
        Teil(a, s + new Vector3(-3.1f, 2.9f, -2.85f), new Vector3(1.6f, 3.2f, 0.6f), stein);
        var glut = Leuchtend(new Color(0.15f, 0.08f, 0.05f), new Color(1f, 0.45f, 0.1f));
        Teil(a, s + new Vector3(-3.1f, 1.03f, -2.4f), new Vector3(1.2f, 0.06f, 0.8f), glut);
        Kugel(a, s + new Vector3(-3.1f, 1.35f, -2.4f), 0.35f, Metall(new Color(0.18f, 0.18f, 0.2f)), hoehe: 0.55f);
        FeuerAnlegen(a, s + new Vector3(-2.6f, 1.6f, -1.6f), glut, lichtAn: 1.4f, lichtAus: 1.4f, glutAn: 1.5f, glutAus: 1.5f, an: () => true, reichweite: 7);
        // Tisch mit zwei Bänken in der Mitte
        Teil(a, s + new Vector3(0.4f, 0.78f, 0), new Vector3(2.6f, 0.1f, 1.1f), hell);
        Teil(a, s + new Vector3(0.4f, 0.38f, 0), new Vector3(2.2f, 0.7f, 0.5f), dunkel);
        foreach (var dz in new[] { -0.95f, 0.95f })
            Teil(a, s + new Vector3(0.4f, 0.42f, dz), new Vector3(2.4f, 0.1f, 0.35f), holz);
        Rund(a, s + new Vector3(0.1f, 0.88f, 0), 0.2f, 0.08f, Material(new Color(0.55f, 0.45f, 0.32f)));       // Brotschale
        Rund(a, s + new Vector3(0.9f, 0.93f, 0.1f), 0.08f, 0.2f, Metall(new Color(0.6f, 0.6f, 0.62f)), segmente: 8); // Krug
        // Bett (hinten rechts), Truhe, Wiege
        Teil(a, s + new Vector3(3.1f, 0.3f, -2.0f), new Vector3(1.7f, 0.5f, 2.4f), holz);
        Teil(a, s + new Vector3(3.1f, 0.62f, -1.75f), new Vector3(1.6f, 0.14f, 1.8f), Material(new Color(0.45f, 0.18f, 0.16f)));
        Teil(a, s + new Vector3(3.1f, 0.65f, -2.95f), new Vector3(1.4f, 0.2f, 0.4f), Material(Leinen));
        Teil(a, s + new Vector3(3.1f, 0.9f, -3.15f), new Vector3(1.7f, 1.3f, 0.12f), dunkel);
        Teil(a, s + new Vector3(3.3f, 0.35f, 1.4f), new Vector3(1.1f, 0.7f, 0.7f), holz);
        Teil(a, s + new Vector3(-1.9f, 0.3f, 1.9f), new Vector3(0.9f, 0.4f, 0.55f), hell);

        var schild = Beschriftung("", a.Mitte + s + new Vector3(0, 3.9f, -1.6f), 30, a.Innen);
        var figuren = new List<(Arbeiterfigur Figur, bool NurAbends)>();
        var familienStand = "";
        a.Raumzustaende.Add((zeit, arbeitszeit) =>
        {
            if (_raum?.Kennung != RaumWohnstube) return;
            var w = _simulation?.Wirtschaft;
            var farbe = haus.Farbe();
            var familie = haus.Konto() is { } konto ? w?.Familien.Von(konto) : null;
            var stand = familie is null ? "" : farbe + string.Join(",", familie.Lebende.Select(p => $"{p.Nummer}:{(w!.Familien.IstVolljaehrig(p) ? 1 : 0)}"));
            if (stand != familienStand)
            {
                foreach (var (f, _) in figuren) { a.Besucher.Remove(f); KnotenEntfernen(f.Koerper); }
                figuren.Clear();
                if (familie is not null) FamilieSetzen(a, s, familie, w!.Familien, figuren, farbe);
                familienStand = stand;
            }
            foreach (var (f, nurAbends) in figuren) f.Koerper.Visible = !nurAbends || !arbeitszeit;
            schild.Text = familie is null ? "Wohnstube"
                : $"Wohnstube der Familie {familie.Nachname}\n{PersonenText(familie.Lebende.Count())} im Haus\nKlick: Familie";
        });
    }

    private static string PersonenText(int anzahl) => anzahl == 1 ? "1 Person" : $"{anzahl} Personen";

    /// <summary>Setzt die Familie in die Wohnstube: Oberhaupt am Tisch (nur abends), Ehepartner am Herd, bis zu vier Kinder.</summary>
    private void FamilieSetzen(InnenAnsicht a, Vector3 s, Familie familie, Familienbuch buch, List<(Arbeiterfigur, bool)> figuren, Color farbe)
    {
        Arbeiterfigur Figur(Person p, Arbeitsplatz platz, Color gewand, int nummer)
        {
            var (groesse, breite, bart) = Koerperbau(p.Volk);
            if (!buch.IstVolljaehrig(p)) groesse *= 0.68f; // Kinder sind kleiner
            var erwachsen = buch.IstVolljaehrig(p);
            var kopf = p.Geschlecht == Geschlecht.Frau && erwachsen ? Kopfputz.Kappe : Kopfputz.Keiner; // Haube
            var haut = p.Volk is Volk.Ork or Volk.Oger ? new Color(0.36f, 0.50f, 0.28f) : Haut;
            var figur = FigurBauen(a.Innen!, a.Mitte + platz.Ort, platz.Drehung, new Vector3(groesse * breite, groesse, groesse * breite), gewand, haut,
                null, kopf, platz.Werkzeug, platz.Art, nummer * 0.8f, platz.Sitzt, bart && erwachsen && p.Geschlecht == Geschlecht.Mann);
            a.Besucher.Add(figur);
            return figur;
        }

        var nummer = 0;
        figuren.Add((Figur(familie.Oberhaupt, new Arbeitsplatz(s + new Vector3(0.4f, 0, -0.95f), Sued, Bewegung.Beten, Werkzeug.Keins, true), farbe, nummer++), true));
        if (familie.Ehepartner is { Lebt: true } partner)
            figuren.Add((Figur(partner, new Arbeitsplatz(s + new Vector3(-2.3f, 0, -1.5f), West, Bewegung.Ruehren, Werkzeug.Kelle), Gewandfarben[4], nummer++), false));
        var plaetze = new[]
        {
            new Arbeitsplatz(s + new Vector3(-0.3f, 0, 0.95f), Nord, Bewegung.Beten, Werkzeug.Keins, true),
            new Arbeitsplatz(s + new Vector3(1.1f, 0, 0.95f), Nord, Bewegung.Beten, Werkzeug.Keins, true),
            new Arbeitsplatz(s + new Vector3(1.4f, 0, -0.95f), Sued, Bewegung.Beten, Werkzeug.Keins, true),
            new Arbeitsplatz(s + new Vector3(-1.4f, 0, 1.2f), Ost, Bewegung.Spielen, Werkzeug.Keins),
        };
        var kinder = familie.Lebende.Where(p => p != familie.Oberhaupt && p != familie.Ehepartner).Take(plaetze.Length).ToList();
        for (var i = 0; i < kinder.Count; i++)
            figuren.Add((Figur(kinder[i], plaetze[i], Gewandfarben[(kinder[i].Nummer * 3 + 1) % Gewandfarben.Length], nummer++), false));
    }
}
