using System.Globalization;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// <b>Ego-Perspektive</b> auf der Spielkarte – ein Teil der <c>partial class Stadtansicht</c> (Knopf im Entwicklerfenster, Nutzer 09.11.2026; Startoption
/// <c>--ego=x,z[,gier[,nick]]</c>). Reine Ansicht: kein Befehl, kein Kennzeichen, der Kern merkt nichts. Der Ablauf:
/// <list type="number">
/// <item><see cref="EgoWaehlen"/>: Ein Hinweis oben fordert zum Klick auf die Karte auf (Esc bricht ab). Der Klick wählt den Ort.</item>
/// <item><see cref="EgoStarten"/>: Eine <see cref="EgoKamera"/> wird die aktive Kamera, die Spielkamera und alles, was ihr folgt (Gras, Feinheiten, Figuren,
/// Regen, Lichter), richtet sich nach ihr (wie bei der freien Kamera, <see cref="FreieKameraBild"/>: Blickpunkt der Spielkamera nahe der Figur,
/// <see cref="EgoZoom"/> m sichtbare Höhe). Schatten und Dunst der freien Kamera gelten ebenfalls.</item>
/// <item><see cref="EgoBeenden"/> (Esc): alles zurück – Blickpunkt, Drehung und Zoom der Spielkamera, Schatten, Dunst und die freie Kamera, falls sie vorher
/// aktiv war (<c>--freiekamera=1</c>).</item>
/// </list>
/// <b>Kollision</b> (<see cref="EgoHindernisse"/>): Gebäude und Gruben sind ihre Klickflächen (<see cref="Klickflaeche"/>, Quader; der Marktplatz und die
/// Torburgen sind ausgenommen: dort geht man hindurch), Mauern, Mauertürme und Ecktürme ihre Netzmaße (<see cref="EgoMauerMerken"/>, die Tore als zwei
/// Seitenstücke), Wasser sperrt ohne Brückendeck (<see cref="DeckHoehe"/>). Der Boden unter den Füßen ist <see cref="FussHoehe(float, float)"/> (Gelände,
/// Fundamente, Decks). Die Spielzeit läuft weiter; die Oberfläche blendet die Hauptszene aus (<see cref="EgoGeaendert"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Sichtbare Höhe (m) der Spielkamera, solange man in der Ego-Perspektive steht: Gras, Figuren und Feinheiten reichen so weit um die Figur (Pixelgröße 2).</summary>
    private const float EgoZoom = 80f;

    /// <summary>Der Blickpunkt der Spielkamera liegt so viel (m) vor der Figur: nach vorn sieht man weiter als nach hinten.</summary>
    private const float EgoVoraus = 12f;

    /// <summary>Alle so viele Sekunden werden die Klickflächen neu gelesen (neue Gebäude, abgerissene).</summary>
    private const double EgoHindernisAlle = 3.0;

    private EgoKamera? _ego;
    private bool _egoWahl;
    private CanvasLayer? _egoWahlEbene;
    private EgoMerker? _egoMerker;

    /// <summary>Ein Hindernis: ein gedrehtes Rechteck auf dem Boden (Mitte, Achsen, halbe Maße) mit Höhenbereich.</summary>
    private readonly record struct EgoHindernis(Vector2 Mitte, Vector2 AchseX, Vector2 AchseZ, float Hx, float Hz, float Unten, float Oben);

    /// <summary>Der Stand der Ansicht vor der Ego-Perspektive, damit Esc sie genau wiederherstellt.</summary>
    private sealed record EgoMerker(
        FreieKamera? FreieKamera, Vector3 Blickpunkt, Vector3 Drehung, float ZielDrehung, float Zoom,
        DirectionalLight3D.ShadowMode SchattenArt, float SchattenWeite, bool Verschmelzen, float Ausblenden,
        float NebelLaenge, float NebelSpreizung);

    private readonly List<EgoHindernis> _egoKlickHindernisse = new();
    private readonly List<EgoHindernis> _egoMauern = new();
    private double _egoHindernisZeit;

    /// <summary>Wird true gerufen, wenn die Ego-Perspektive beginnt, und false, wenn sie endet (die Hauptszene blendet die Oberfläche aus und ein).</summary>
    public event Action<bool>? EgoGeaendert;

    /// <summary>Steht man gerade in der Ego-Perspektive?</summary>
    public bool EgoAktiv => _ego is not null;

    /// <summary>Wartet die Ansicht auf den Klick, der den Ort der Ego-Perspektive wählt?</summary>
    public bool EgoWahlLaeuft => _egoWahl;

    // ═════════════════════════════════════════════════════════════════════════
    //  Ort wählen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Beginnt die Wahl: Der nächste Klick auf die Karte setzt die Figur dorthin. Drinnen (Innenansicht, Raum, Werkstatt) geht es nicht; gibt dann false zurück.</summary>
    public bool EgoWaehlen()
    {
        if (_ego is not null || _raum is not null || _innen is not null || _entwurf is not null || _gelaende is null && _karte is null) return false;
        _egoWahl = true;
        EgoWahlHinweis(true);
        return true;
    }

    private void EgoWahlHinweis(bool an)
    {
        if (!an)
        {
            if (_egoWahlEbene is not null) _egoWahlEbene.Visible = false;
            return;
        }
        if (_egoWahlEbene is null)
        {
            _egoWahlEbene = new CanvasLayer { Layer = 90, Name = "EgoWahl" };
            AddChild(_egoWahlEbene);
            var text = new Label
            {
                Text = "Ego-Perspektive: Klick auf die Karte wählt den Ort · Esc: abbrechen", MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -280, OffsetRight = 280, OffsetTop = 64, HorizontalAlignment = HorizontalAlignment.Center,
            };
            text.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.06f, 0.04f, 0.75f), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 5, ContentMarginBottom = 5,
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            });
            text.AddThemeFontSizeOverride("font_size", 15);
            text.AddThemeColorOverride("font_color", new Color(1f, 0.97f, 0.88f));
            _egoWahlEbene.AddChild(text);
        }
        _egoWahlEbene.Visible = true;
    }

    /// <summary>Eingabe während der Wahl (aus <c>_UnhandledInput</c>, also nicht über Tafeln): linker Klick wählt, Esc bricht ab. True, wenn die Eingabe verbraucht ist.</summary>
    private bool EgoWahlEingabe(InputEvent eingabe)
    {
        if (!_egoWahl) return false;
        switch (eingabe)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                _egoWahl = false;
                EgoWahlHinweis(false);
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick:
                if (EgoBodenPunktBei(klick.Position) is { } punkt)
                {
                    _egoWahl = false;
                    EgoWahlHinweis(false);
                    EgoStarten(new Vector2(punkt.X, punkt.Z));
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>Wo trifft der Strahl der aktiven Kamera durch diese Bildschirmstelle den Boden (auf Gelände dreimal der Höhe nachgeführt)?</summary>
    private Vector3? EgoBodenPunktBei(Vector2 bildschirm)
    {
        var kamera = StadtKamera;
        var von = kamera.ProjectRayOrigin(bildschirm);
        var richtung = kamera.ProjectRayNormal(bildschirm);
        if (richtung.Y > -1e-3f) return null;
        var y = 0f;
        var p = von;
        for (var i = 0; i < 4; i++)
        {
            p = von + richtung * ((y - von.Y) / richtung.Y);
            y = BodenHoehe(p.X, p.Z);
        }
        return new Vector3(p.X, y, p.Z);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Starten und Beenden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Startet die Ego-Perspektive am Ort (x, z): Liegt er in einem Gebäude oder im Wasser, steht man am nächsten freien Ort. <paramref name="gier"/> ist die Blickrichtung in Grad
    /// (0 = nach −z; ohne Angabe die Richtung der Spielkamera). False, wenn es gerade nicht geht (schon in der Ego-Perspektive, drinnen).
    /// </summary>
    public bool EgoStarten(Vector2 ort, float? gier = null, float nick = 0f)
    {
        if (_ego is not null || _raum is not null || _innen is not null || _entwurf is not null) return false;
        EgoHindernisseLesen();
        var start = EgoFreierOrt(ort);
        var umgebung = _umgebung.Environment;
        _egoMerker = new EgoMerker(
            _freieKamera, _kameraDrehung.Position, _kameraDrehung.RotationDegrees, _zielDrehung, _kamera.Size,
            _sonne.DirectionalShadowMode, _sonne.DirectionalShadowMaxDistance, _sonne.DirectionalShadowBlendSplits, _sonne.DirectionalShadowFadeStart,
            umgebung.VolumetricFogLength, umgebung.VolumetricFogDetailSpread);

        _ego = new EgoKamera
        {
            Fusshoehe = FussHoehe,
            Gesperrt = EgoGesperrt,
            Herausschieben = EgoHerausschieben,
        };
        AddChild(_ego);
        _ego.Beendet += EgoBeenden;
        _ego.Aufstellen(start, gier ?? _kameraDrehung.RotationDegrees.Y, nick);
        _ego.MakeCurrent();
        _freieKamera = _ego;
        _freiGemeldet = true; // die Ortsmeldung der freien Kamera ist hier nicht gemeint
        _kamerafahrt?.Kill();
        GD.Print($"[ego] Start bei {start.X:0.0},{start.Y:0.0}, Boden {BodenHoehe(start.X, start.Y):0.0}, Fuß {FussHoehe(start.X, start.Y):0.0}; Hindernisse: {_egoKlickHindernisse.Count} Gebäude, {_egoMauern.Count} Mauerteile");
        EgoGeaendert?.Invoke(true);
        return true;
    }

    /// <summary>Beendet die Ego-Perspektive und stellt die Spielkamera her, wie sie vorher war (Blickpunkt, Drehung, Zoom, Schatten, Dunst).</summary>
    public void EgoBeenden()
    {
        _egoWahl = false;
        EgoWahlHinweis(false);
        if (_ego is not { } ego || _egoMerker is not { } m) return;
        _ego = null;
        _egoMerker = null;
        ego.Beendet -= EgoBeenden;
        ego.GetParent()?.RemoveChild(ego);
        ego.QueueFree();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _freieKamera = m.FreieKamera;
        _kamerafahrt?.Kill();
        _kameraDrehung.Position = m.Blickpunkt;
        _kameraDrehung.RotationDegrees = m.Drehung;
        _zielDrehung = m.ZielDrehung;
        _kamera.Size = m.Zoom;
        if (m.FreieKamera is { } frei) frei.MakeCurrent(); // --freiekamera=1: zurück in die freie Kamera, ihre Schatten und ihr Dunst bleiben
        else
        {
            _kamera.MakeCurrent();
            _sonne.DirectionalShadowMode = m.SchattenArt;
            _sonne.DirectionalShadowMaxDistance = m.SchattenWeite;
            _sonne.DirectionalShadowBlendSplits = m.Verschmelzen;
            _sonne.DirectionalShadowFadeStart = m.Ausblenden;
            _schattenFuer = null; // Bias, Normal-Bias und Weichzeichner neu rechnen (SchattenNachfuehren)
            var umgebung = _umgebung.Environment;
            umgebung.VolumetricFogLength = m.NebelLaenge;
            umgebung.VolumetricFogDetailSpread = m.NebelSpreizung;
        }
        GD.Print($"[ego] beendet bei {ego.Ort.X:0.00},{ego.Ort.Y:0.00}");
        EgoGeaendert?.Invoke(false);
    }

    /// <summary>
    /// Startoption <c>--ego=x,z[,gier[,nick]]</c> (Ort in Metern der Stadt, Blickrichtung und Neigung in Grad): beginnt gleich in der Ego-Perspektive. Für Klicktests.
    /// </summary>
    public void EgoVorgeben(string wert)
    {
        var z = wert.Split(',');
        float W(int i, float vorgabe) => i < z.Length && float.TryParse(z[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : vorgabe;
        if (z.Length < 2 || !float.TryParse(z[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !float.TryParse(z[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var zz))
        {
            GD.PushWarning($"--ego={wert}: erwartet x,z[,gier[,nick]]");
            return;
        }
        EgoStarten(new Vector2(x, zz), z.Length > 2 ? W(2, 0f) : null, W(3, 0f));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild (aus <see cref="FreieKameraBild"/>): Blickpunkt, Drehung und Zoom der Spielkamera nach der Figur; die Klickflächen alle paar Sekunden neu lesen; enden, wenn ein Raum oder eine Innenansicht beginnt.</summary>
    private void EgoBild(EgoKamera ego)
    {
        if (_raum is not null || _innen is not null) { EgoBeenden(); return; }
        _egoHindernisZeit += GetProcessDeltaTime();
        if (_egoHindernisZeit >= EgoHindernisAlle) EgoHindernisseLesen();
        var o = ego.GlobalPosition;
        var vor = new Vector2(-Mathf.Sin(Mathf.DegToRad(ego.Gier)), -Mathf.Cos(Mathf.DegToRad(ego.Gier)));
        _kamerafahrt?.Kill();
        _kameraDrehung.Position = new Vector3(o.X + vor.X * EgoVoraus, 0, o.Z + vor.Y * EgoVoraus);
        _zielDrehung = ego.Gier;
        _kameraDrehung.RotationDegrees = new Vector3(0, ego.Gier, 0);
        _kamera.Size = EgoZoom;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kollision
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Liest die Quader der Klickflächen der Stadt (sichtbar, nicht abgeschaltet, nicht als <c>ego_frei</c> markiert) als Hindernisse. Die Klickflächen sind eine
    /// Näherung der Gebäude (Quader um ihre Grundfläche und Höhe); für die Kollision genügt das.
    /// </summary>
    private void EgoHindernisseLesen()
    {
        _egoHindernisZeit = 0;
        _egoKlickHindernisse.Clear();
        foreach (var knoten in FindChildren("*", "StaticBody3D", true, false))
        {
            if (knoten is not StaticBody3D { Visible: true } koerper || !koerper.HasMeta("kennung") || koerper.HasMeta("ego_frei") || !koerper.IsVisibleInTree()) continue;
            foreach (var kind in koerper.GetChildren())
                if (kind is CollisionShape3D { Disabled: false, Shape: BoxShape3D kiste } form)
                    _egoKlickHindernisse.Add(EgoHindernisVon(koerper.GlobalTransform * form.Transform, kiste.Size / 2f));
        }
        // der Marktbrunnen (Netz „Brunnen_Wasser“: Becken und Trog) hat keine Klickfläche; er steht als Block von 3 m Höhe da (Stadtansicht.Brunnen.cs)
        _brunnen.RemoveAll(b => !IsInstanceValid(b.Wasser));
        foreach (var (wasser, _, _) in _brunnen)
        {
            var netz = wasser.GetAabb();
            var h = EgoHindernisVon(wasser.GlobalTransform * new Transform3D(Basis.Identity, netz.GetCenter()), netz.Size / 2f);
            _egoKlickHindernisse.Add(h with { Unten = wasser.GlobalPosition.Y - 2f, Oben = wasser.GlobalPosition.Y + 3f });
        }
    }

    /// <summary>Ein Hindernis aus der Lage eines Quaders (nur die Drehung um die Hochachse und die Streckung zählen) und seinen halben Maßen.</summary>
    private static EgoHindernis EgoHindernisVon(Transform3D lage, Vector3 halb)
    {
        var bx = lage.Basis.X * halb.X;
        var bz = lage.Basis.Z * halb.Z;
        var (ax, az) = (new Vector2(bx.X, bx.Z), new Vector2(bz.X, bz.Z));
        var halbY = lage.Basis.Y.Length() * halb.Y;
        return new EgoHindernis(new Vector2(lage.Origin.X, lage.Origin.Z), ax.Normalized(), az.Normalized(), ax.Length(), az.Length(), lage.Origin.Y - halbY, lage.Origin.Y + halbY);
    }

    /// <summary>Merkt die Teile einer Mauer als Hindernisse (Mauerstücke, Mauertürme, Ecktürme im Maß ihres Netzes; Tore nur als zwei Seitenstücke, die Durchfahrt bleibt frei).</summary>
    private void EgoMauerMerken(Mauerteile teile, Mauerplan plan)
    {
        foreach (var s in plan.Stuecke) EgoMauerTeil(teile.Stueck.GetAabb(), new Transform3D(MauerStueckBasis(s), MauerOrt(s)));
        foreach (var t in plan.Tuerme) EgoMauerTeil(teile.Turm.GetAabb(), new Transform3D(MauerBasis(t), MauerOrt(t)));
        foreach (var t in plan.Ecktuerme) EgoMauerTeil(teile.Eckturm.GetAabb(), new Transform3D(MauerBasis(t), MauerOrt(t)));
        foreach (var tor in plan.Tore)
        {
            var lage = new Transform3D(MauerBasis(tor.Lage), MauerOrt(tor.Lage));
            var a = teile.Tor.GetAabb();
            var seite = (a.Size.X - EgoTorDurchfahrt) / 2f;
            if (seite <= 0.1f) continue;
            foreach (var vorzeichen in new[] { -1f, 1f })
            {
                var mitte = new Vector3(a.GetCenter().X + vorzeichen * (EgoTorDurchfahrt / 2f + seite / 2f), a.GetCenter().Y, a.GetCenter().Z);
                EgoMauerTeil(new Aabb(mitte - new Vector3(seite / 2f, a.Size.Y / 2f, a.Size.Z / 2f), new Vector3(seite, a.Size.Y, a.Size.Z)), lage);
            }
        }
    }

    /// <summary>Breite (m) der Durchfahrt einer Torburg, die als Gasse frei bleibt.</summary>
    private const float EgoTorDurchfahrt = 5f;

    private void EgoMauerTeil(Aabb netz, Transform3D lage) => _egoMauern.Add(EgoHindernisVon(lage * new Transform3D(Basis.Identity, netz.GetCenter()), netz.Size / 2f));

    /// <summary>Gebäude oder Mauer an dieser Stelle (Kreis mit dem Halbmesser der Figur, Füße auf Höhe <paramref name="fussY"/>)?</summary>
    private bool EgoHindernisBei(Vector2 p, float fussY)
    {
        foreach (var h in _egoKlickHindernisse) if (EgoEindringen(h, p, fussY) is not null) return true;
        foreach (var h in _egoMauern) if (EgoEindringen(h, p, fussY) is not null) return true;
        return false;
    }

    /// <summary>Wie tief der Kreis der Füße in das Hindernis ragt und in welche Richtung er hinausgeschoben wird (null: kein Kontakt; auch wenn die Füße darüber oder die Köpfe darunter bleiben).</summary>
    private static (Vector2 Normale, float Tiefe)? EgoEindringen(EgoHindernis h, Vector2 p, float fussY)
    {
        if (fussY >= h.Oben - 0.05f || fussY + EgoKamera.Augenhoehe + 0.1f <= h.Unten) return null; // darüber stehen oder darunter durchgehen (Torbogen)
        var d = p - h.Mitte;
        var (lx, lz) = (d.Dot(h.AchseX), d.Dot(h.AchseZ));
        var (qx, qz) = (Mathf.Abs(lx) - h.Hx, Mathf.Abs(lz) - h.Hz);
        const float r = EgoKamera.Radius;
        if (qx > r || qz > r) return null;
        Vector2 lokal;
        float tiefe;
        if (qx <= 0 && qz <= 0)
        {
            // Mitte im Rechteck: zur nächsten Seite hinaus
            if (qx > qz) { lokal = new Vector2(Mathf.Sign(lx) == 0 ? 1 : Mathf.Sign(lx), 0); tiefe = r - qx; }
            else { lokal = new Vector2(0, Mathf.Sign(lz) == 0 ? 1 : Mathf.Sign(lz)); tiefe = r - qz; }
        }
        else
        {
            var (ex, ez) = (Mathf.Max(qx, 0f), Mathf.Max(qz, 0f));
            var abstand = Mathf.Sqrt(ex * ex + ez * ez);
            if (abstand >= r) return null;
            lokal = new Vector2(ex * (lx < 0 ? -1 : 1), ez * (lz < 0 ? -1 : 1)) / Mathf.Max(abstand, 1e-5f);
            tiefe = r - abstand;
        }
        return (h.AchseX * lokal.X + h.AchseZ * lokal.Y, tiefe);
    }

    /// <summary>Schiebt einen Ort aus allen Hindernissen heraus (bis zu vier Durchgänge, damit eine Ecke zwischen zwei Wänden hält).</summary>
    private Vector2 EgoHerausschieben(Vector2 p, float fussY)
    {
        for (var durchgang = 0; durchgang < 4; durchgang++)
        {
            var bewegt = false;
            foreach (var h in _egoKlickHindernisse) bewegt |= Verschieben(h);
            foreach (var h in _egoMauern) bewegt |= Verschieben(h);
            if (!bewegt) break;
        }
        return p;

        bool Verschieben(EgoHindernis h)
        {
            if (EgoEindringen(h, p, fussY) is not var (normale, tiefe)) return false;
            p += normale * (tiefe + 0.001f);
            return true;
        }
    }

    /// <summary>Gesperrt: Wasser (auch am Rand des Kreises der Füße) ohne Brückendeck darunter, und außerhalb der Karte (Spielfeld plus Rand).</summary>
    private bool EgoGesperrt(Vector2 p)
    {
        if (KameraRechteck() is { } r)
        {
            const float rand = 40f;
            if (p.X < r.X0 - rand || p.X > r.X1 + rand || p.Y < r.Z0 - rand || p.Y > r.Z1 + rand) return true;
        }
        if (GewaesserForm is not { IstLeer: false } form) return false;
        const float k = EgoKamera.Radius;
        foreach (var o in new[] { Vector2.Zero, new Vector2(k, 0), new Vector2(-k, 0), new Vector2(0, k), new Vector2(0, -k) })
        {
            var q = p + o;
            if (form.IstWasser(q.X, q.Y) && DeckHoehe(q.X, q.Y) is null) return true;
        }
        return false;
    }

    /// <summary>Der freie Ort nächst am Wunschort (spiralförmig suchen, bis 60 m): nicht in einem Gebäude, nicht im Wasser.</summary>
    private Vector2 EgoFreierOrt(Vector2 wunsch)
    {
        bool Frei(Vector2 p) => !EgoGesperrt(p) && !EgoHindernisBei(p, FussHoehe(p.X, p.Y));
        if (Frei(wunsch)) return wunsch;
        for (var ring = 1; ring <= 120; ring++)
        {
            var radius = ring * 0.5f;
            var schritte = Mathf.Max(8, (int)(radius * 6));
            for (var i = 0; i < schritte; i++)
            {
                var w = Mathf.Tau * i / schritte;
                var p = wunsch + new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * radius;
                if (Frei(p)) return p;
            }
        }
        return wunsch;
    }
}
