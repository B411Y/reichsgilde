using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Reiter „Stammbaum“ im Familienbuch: Gründer oben, Ehepaare verbunden, Kinder darunter (nach dem Vorbild der Stammbäume in
/// Crusader Kings). Verstorbene stehen grau mit ihren Lebensjahren, das Oberhaupt trägt die Krone, der Erbe ist gekennzeichnet,
/// Eingeheiratete tragen das Wappen ihres Herkunftshauses. Ziehen verschiebt, das Mausrad zoomt, ein Klick auf eine Person öffnet das
/// Charakterfenster, die Knöpfchen unter einem Ehepaar klappen den Zweig ein. Über der Tafel wählt man das Haus: alle Häuser lassen sich ansehen.
///
/// Der Baum kommt fertig aus dem Kern (<see cref="Familienbuch.Stammbaum"/>, nur lesend, nichts wird gespeichert). Gezeichnet wird er in einem
/// einzigen Steuerelement (<see cref="Baumzeichnung"/>); nur lebende Personen bekommen ein richtiges Porträt – so bleibt auch ein Haus mit
/// vielen Generationen beim Öffnen flüssig, und Verschieben und Zoomen kosten nichts (die Zeichnung wird nur bei einem neuen Aufbau neu gemalt).
/// </summary>
public partial class StammbaumAnzeige : VBoxContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);

    private const float FensterBreite = 856, FensterHoehe = 480;
    private const float ZoomMin = 0.3f, ZoomMax = 1.6f;

    private Stadtwirtschaft? _w;
    private Konto? _spieler;
    private Konto? _haus;
    private readonly HashSet<int> _eingeklappt = new();
    private Label _kopf = null!;
    private Control _fenster = null!;
    private Baumzeichnung _baum = null!;
    private float _zoom = 1;
    private bool _zieht;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        _kopf = Hauptmenue.Text(this, "", 14, Grau, zentriert: true);

        _fenster = new Control
        {
            CustomMinimumSize = new Vector2(FensterBreite, FensterHoehe), ClipContents = true, MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.Move, // ✥ beim Verschieben (System-Zeiger; mit 3D-Handschuh die Faust)
        };
        _fenster.GuiInput += FensterEingabe;
        AddChild(_fenster);
        _baum = new Baumzeichnung();
        _baum.Umgeschaltet += nummer =>
        {
            if (!_eingeklappt.Remove(nummer)) _eingeklappt.Add(nummer);
            Neuzeichnen(einpassen: false);
        };
        _fenster.AddChild(_baum);

        var leiste = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        leiste.AddThemeConstantOverride("separation", 6);
        AddChild(leiste);
        Klein(leiste, "Einpassen", "Ganzen Stammbaum ins Bild holen.", () => Neuzeichnen(einpassen: true));
        Klein(leiste, "Zum Oberhaupt", "Auf das heutige Oberhaupt zentrieren.", ZumOberhaupt);
        Klein(leiste, "Alles ausklappen", "Alle eingeklappten Zweige wieder zeigen.", () => { _eingeklappt.Clear(); Neuzeichnen(einpassen: false); });
        Klein(leiste, "−", "Verkleinern (oder Mausrad).", () => ZoomUm(0.8f, null));
        Klein(leiste, "+", "Vergrößern (oder Mausrad).", () => ZoomUm(1.25f, null));
        Hauptmenue.Text(this, "Ziehen: verschieben · Mausrad: zoomen · Klick auf eine Person: Charakterfenster · Knöpfchen unter dem Ehepaar: Zweig einklappen",
            12, Grau, zentriert: true);
    }

    private static Button Klein(Container eltern, string text, string tipp, Action aktion)
    {
        var k = new Button { Text = text, FocusMode = FocusModeEnum.None, TooltipText = tipp };
        k.AddThemeFontSizeOverride("font_size", 13);
        k.Pressed += aktion;
        eltern.AddChild(k);
        return k;
    }

    public void Verbinden(Stadtwirtschaft w, Konto spieler)
    {
        _w = w;
        _spieler = spieler;
        _haus = null;
        _eingeklappt.Clear();
    }

    /// <summary>Baut den Baum dieses Hauses auf. Beim Wechsel des Hauses wird er neu eingepasst, sonst bleibt der Ausschnitt.</summary>
    public void Aufbauen(Konto haus)
    {
        if (_w is null || _spieler is null) return;
        var wechsel = haus != _haus;
        if (wechsel) { _haus = haus; _eingeklappt.Clear(); }
        Neuzeichnen(einpassen: wechsel || !_baum.HatInhalt);
    }

    private void Neuzeichnen(bool einpassen)
    {
        if (_w is null || _haus is null || _w.Familien.Von(_haus) is not { } f) { _kopf.Text = "Dieses Haus hat keine Familie."; return; }
        var buch = _w.Familien;
        var wurzeln = buch.Stammbaum(f);
        var lebende = f.Lebende.Count();
        _kopf.Text = $"Haus {f.Nachname} · {f.Volk.Mehrzahl()} · {lebende} lebende, {f.Mitglieder.Count - lebende} verstorbene Angehörige" +
                     (_haus.IstErloschen ? " · erloschen" : "");
        _baum.Setzen(_w, f, wurzeln, _eingeklappt, Herkunft);
        if (einpassen) Einpassen();
        else Anwenden();
    }

    /// <summary>Das Wappen des Hauses, aus dem eine Person eingeheiratet hat: das Haus der Stadt mit diesem Namen, sonst das aus dem Namen gewürfelte.</summary>
    private Wappen Herkunft(Person p)
    {
        var haus = _w!.Konten.FirstOrDefault(k => _w.Familien.Von(k)?.Nachname == p.Geburtsname);
        return haus?.Wappen ?? Wappen.AusName(p.Geburtsname);
    }

    // ── Verschieben und Zoomen ──────────────────────────────────────────────

    private void Einpassen()
    {
        var inhalt = _baum.Groesse;
        if (inhalt.X <= 0) return;
        _zoom = Mathf.Clamp(Mathf.Min(FensterBreite / inhalt.X, FensterHoehe / inhalt.Y), ZoomMin, 1f);
        _baum.Position = new Vector2((FensterBreite - inhalt.X * _zoom) / 2, 8);
        Anwenden();
    }

    private void ZumOberhaupt()
    {
        if (_baum.OberhauptMitte is not { } mitte) { Einpassen(); return; }
        _zoom = Mathf.Max(_zoom, 0.8f);
        _baum.Position = new Vector2(FensterBreite / 2, FensterHoehe / 2) - mitte * _zoom;
        Anwenden();
    }

    private void Anwenden()
    {
        _baum.Scale = new Vector2(_zoom, _zoom);
        // Der Baum darf nicht ganz aus dem Fenster geschoben werden.
        var g = _baum.Groesse * _zoom;
        var x = Mathf.Clamp(_baum.Position.X, 60 - g.X, FensterBreite - 60);
        var y = Mathf.Clamp(_baum.Position.Y, 60 - g.Y, FensterHoehe - 60);
        _baum.Position = new Vector2(x, y);
    }

    private void ZoomUm(float faktor, Vector2? bei)
    {
        var punkt = bei ?? new Vector2(FensterBreite / 2, FensterHoehe / 2);
        var neu = Mathf.Clamp(_zoom * faktor, ZoomMin, ZoomMax);
        if (Mathf.IsEqualApprox(neu, _zoom)) return;
        _baum.Position = punkt - (punkt - _baum.Position) * (neu / _zoom);
        _zoom = neu;
        Anwenden();
    }

    private void FensterEingabe(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } auf:
                ZoomUm(1.12f, auf.Position); _fenster.AcceptEvent(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } ab:
                ZoomUm(1 / 1.12f, ab.Position); _fenster.AcceptEvent(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle or MouseButton.Right } taste:
                _zieht = taste.Pressed; break;
            case InputEventMouseMotion bewegung when _zieht:
                _baum.Position += bewegung.Relative;
                Anwenden();
                break;
        }
    }

    public override void _Input(InputEvent e)
    {
        // Loslassen außerhalb des Fensters beendet das Ziehen ebenfalls.
        if (_zieht && e is InputEventMouseButton { Pressed: false }) _zieht = false;
    }
}

/// <summary>
/// Zeichnet den Stammbaum: Karten, Ehebänder, Linien zu den Kindern, Kronen und Einklapp-Knöpfchen. Der Platz kommt aus einem einfachen Raster
/// (jede Person ihre Breite, jede Generation eine Zeile, Eltern über der Mitte ihrer Kinder). Klicks auf Karten öffnen das Charakterfenster
/// (<see cref="PortraetAnzeige.PersonOeffnen"/>), Klicks auf ein Knöpfchen schalten den Zweig um.
/// </summary>
public partial class Baumzeichnung : Control
{
    private const float KarteB = 116, KarteH = 112, EheLuecke = 16, GeschwisterLuecke = 22, ZeilenLuecke = 58;
    private const float KnopfRadius = 9;

    private static readonly Color Pergament = new("e3d3a6");
    private static readonly Color PergamentTot = new("a8a294");
    private static readonly Color Tinte = new("1b1510");
    private static readonly Color Gold = new("d9b13c");
    private static readonly Color Rot = new("8e3a2c");

    private sealed record Karte(Person Person, Rect2 Raum, bool Eingeheiratet, bool IstOberhaupt, bool IstAhne, bool IstErbe, string Bezeichnung);
    private sealed record Knopf(Vector2 Mitte, int Nummer, int Versteckt, bool Zu);

    private readonly List<Karte> _karten = new();
    private readonly List<Knopf> _knoepfe = new();
    private readonly List<(Vector2 Von, Vector2 Nach)> _linien = new();
    private readonly List<(Vector2 Links, Vector2 Rechts)> _baender = new();
    private Familienbuch? _buch;
    private Familie? _familie;

    public event Action<int>? Umgeschaltet;
    public Vector2 Groesse => Size;
    public bool HatInhalt => _karten.Count > 0;
    public Vector2? OberhauptMitte { get; private set; }

    public Baumzeichnung() { MouseFilter = MouseFilterEnum.Pass; }

    public void Setzen(Stadtwirtschaft w, Familie f, IReadOnlyList<StammbaumKnoten> wurzeln, HashSet<int> eingeklappt, Func<Person, Wappen> herkunft)
    {
        _buch = w.Familien;
        _familie = f;
        UiHilfe.KinderEntfernen(this); // Porträts und Wappen der Karten
        _karten.Clear(); _knoepfe.Clear(); _linien.Clear(); _baender.Clear();
        OberhauptMitte = null;
        var erbe = f.Konto.IstErloschen ? null : w.Familien.Erbe(f);

        var breiten = new Dictionary<StammbaumKnoten, float>();
        float Block(StammbaumKnoten k) => (1 + k.Ehepartner.Count) * KarteB + k.Ehepartner.Count * EheLuecke;
        float Breite(StammbaumKnoten k)
        {
            if (breiten.TryGetValue(k, out var b)) return b;
            var kinder = eingeklappt.Contains(k.Person.Nummer) ? Array.Empty<StammbaumKnoten>() : k.Kinder.ToArray();
            var kb = kinder.Length == 0 ? 0 : kinder.Sum(Breite) + (kinder.Length - 1) * GeschwisterLuecke;
            return breiten[k] = Mathf.Max(Block(k), kb);
        }

        float maxX = 0, maxY = 0;
        // Gibt die Mitte des Blocks (Person und Ehepartner) zurück.
        float Platzieren(StammbaumKnoten k, float links, int zeile)
        {
            var breite = Breite(k);
            var kinder = eingeklappt.Contains(k.Person.Nummer) ? Array.Empty<StammbaumKnoten>() : k.Kinder.ToArray();
            var y = 24 + zeile * (KarteH + ZeilenLuecke);
            float mitte;
            var kinderMitten = new List<float>();
            if (kinder.Length > 0)
            {
                var kb = kinder.Sum(Breite) + (kinder.Length - 1) * GeschwisterLuecke;
                var x = links + (breite - kb) / 2;
                foreach (var kind in kinder)
                {
                    kinderMitten.Add(Platzieren(kind, x, zeile + 1));
                    x += Breite(kind) + GeschwisterLuecke;
                }
                mitte = (kinderMitten[0] + kinderMitten[^1]) / 2;
            }
            else mitte = links + breite / 2;

            var blockLinks = mitte - Block(k) / 2;
            var personen = new List<Person> { k.Person };
            personen.AddRange(k.Ehepartner);
            for (var i = 0; i < personen.Count; i++)
            {
                var p = personen[i];
                var x = blockLinks + i * (KarteB + EheLuecke);
                var eingeheiratet = i > 0;
                var ahne = _familie!.Ahnen.Contains(p);
                var istOberhaupt = p == f.Oberhaupt;
                var bez = istOberhaupt ? (p.Lebt ? p.OberhauptTitel : "letztes Oberhaupt")
                    : ahne ? "früher Oberhaupt"
                    : p == erbe ? "Erbe"
                    : eingeheiratet ? (p.Geburtsname == p.Familienname ? "eingeheiratet" : $"geb. {p.Geburtsname}")
                    : "";
                _karten.Add(new Karte(p, new Rect2(x, y, KarteB, KarteH), eingeheiratet, istOberhaupt, ahne, p == erbe, bez));
                if (istOberhaupt) OberhauptMitte = new Vector2(x + KarteB / 2, y + KarteH / 2);
                if (i > 0) _baender.Add((new Vector2(x - EheLuecke, y + 50), new Vector2(x, y + 50)));
                maxX = Mathf.Max(maxX, x + KarteB);
            }
            maxY = Mathf.Max(maxY, y + KarteH);

            if (k.Kinder.Count > 0)
            {
                var zu = eingeklappt.Contains(k.Person.Nummer);
                var fuss = new Vector2(mitte, y + KarteH);
                if (!zu)
                {
                    var bus = y + KarteH + ZeilenLuecke / 2f;
                    _linien.Add((fuss, new Vector2(mitte, bus)));
                    _linien.Add((new Vector2(kinderMitten[0], bus), new Vector2(kinderMitten[^1], bus)));
                    foreach (var km in kinderMitten) _linien.Add((new Vector2(km, bus), new Vector2(km, y + KarteH + ZeilenLuecke)));
                }
                _knoepfe.Add(new Knopf(new Vector2(mitte, y + KarteH + (zu ? 14 : 12)), k.Person.Nummer, Zaehlen(k) - 1, zu));
                if (zu) _linien.Add((fuss, new Vector2(mitte, y + KarteH + 14)));
            }
            return mitte;
        }

        var xs = 20f;
        foreach (var wurzel in wurzeln)
        {
            Platzieren(wurzel, xs, 0);
            xs += Breite(wurzel) + GeschwisterLuecke * 2;
        }
        Size = new Vector2(Mathf.Max(maxX + 20, 100), maxY + 40);
        CustomMinimumSize = Vector2.Zero;

        // Richtige Porträts nur für Lebende; Eingeheiratete tragen ihr Herkunftswappen.
        foreach (var k in _karten)
        {
            if (k.Person.Lebt)
            {
                var bild = PortraetAnzeige.Neu(52, lebendig: false);
                bild.Zeigen(w.Familien, k.Person);
                bild.Position = k.Raum.Position + new Vector2(6, 6);
                bild.Size = new Vector2(52, 52);
                AddChild(bild);
            }
            if (k.Eingeheiratet)
            {
                var wappen = WappenBild.Neu(herkunft(k.Person), 30);
                wappen.Position = k.Raum.Position + new Vector2(KarteB - 36, 12);
                wappen.TooltipText = $"Herkunft: Haus {k.Person.Geburtsname}";
                AddChild(wappen);
            }
        }
        QueueRedraw();
    }

    private static int Zaehlen(StammbaumKnoten k) => 1 + k.Ehepartner.Count + k.Kinder.Sum(Zaehlen);

    public override void _Draw()
    {
        var schrift = GetThemeDefaultFont();
        // Linien: breiter schwarzer Strich, darüber ein heller – wie ein Holzschnitt-Rand um die Wege.
        foreach (var (von, nach) in _linien) DrawLine(von, nach, Tinte, 3, true);
        foreach (var (von, nach) in _linien) DrawLine(von, nach, new Color("d8c48f"), 1.6f, true);
        foreach (var (links, rechts) in _baender)
        {
            DrawLine(links, rechts, Tinte, 5, true);
            DrawLine(links, rechts, Gold, 2.5f, true);
        }

        foreach (var k in _karten) KarteZeichnen(schrift, k);

        foreach (var kn in _knoepfe)
        {
            DrawCircle(kn.Mitte, KnopfRadius + 1.5f, Tinte);
            DrawCircle(kn.Mitte, KnopfRadius, kn.Zu ? Gold : Pergament);
            var text = kn.Zu ? $"+{kn.Versteckt}" : "−";
            var groesse = kn.Zu ? 10 : 15;
            var breite = schrift.GetStringSize(text, HorizontalAlignment.Left, -1, groesse).X;
            DrawString(schrift, kn.Mitte + new Vector2(-breite / 2, groesse * 0.36f), text, HorizontalAlignment.Left, -1, groesse, Tinte);
        }
    }

    private void KarteZeichnen(Font schrift, Karte k)
    {
        var p = k.Person;
        var tot = !p.Lebt;
        var r = k.Raum;
        var grund = tot ? PergamentTot : Pergament;
        var tinte = tot ? new Color("4a463e") : Tinte;
        // Schlagschatten, schwarzer Umriss, Papier – leicht versetzt, als läge die Farbe neben der Linie.
        DrawRect(new Rect2(r.Position + new Vector2(3, 3), r.Size), new Color(0, 0, 0, 0.35f));
        DrawRect(r.Grow(2), Tinte);
        DrawRect(r, grund);
        DrawRect(r.Grow(-3), tinte, false, 1);
        // Schraffur im Schatten (unten rechts)
        for (var i = 0; i < 5; i++)
        {
            var o = i * 5;
            DrawLine(new Vector2(r.End.X - 3 - o, r.End.Y - 4), new Vector2(r.End.X - 14 - o, r.End.Y - 16), tinte * new Color(1, 1, 1, 0.3f), 1);
        }
        if (k.IstOberhaupt || k.IstErbe) DrawRect(r.Grow(-1), k.IstOberhaupt ? Gold : new Color("6f8d4a"), false, 2);

        if (tot)
        {
            // Grabstein statt Porträt
            var b = r.Position + new Vector2(6, 6);
            DrawRect(new Rect2(b, new Vector2(52, 52)), new Color("85806f"));
            DrawRect(new Rect2(b, new Vector2(52, 52)), tinte, false, 1);
            var m = b + new Vector2(26, 28);
            DrawPolygon(new[] { m + new Vector2(-13, 22), m + new Vector2(-13, -6), m + new Vector2(-9, -14), m + new Vector2(0, -18), m + new Vector2(9, -14), m + new Vector2(13, -6), m + new Vector2(13, 22) },
                new[] { new Color("b3ae9f") });
            DrawLine(m + new Vector2(0, -10), m + new Vector2(0, 8), tinte, 2.2f);
            DrawLine(m + new Vector2(-6, -4), m + new Vector2(6, -4), tinte, 2.2f);
            DrawPolyline(new[] { m + new Vector2(-13, 22), m + new Vector2(-13, -6), m + new Vector2(-9, -14), m + new Vector2(0, -18), m + new Vector2(9, -14), m + new Vector2(13, -6), m + new Vector2(13, 22) }, tinte, 1.2f);
        }

        // Namen, Lebensjahre, Bezeichnung
        var farbe = tot ? new Color("4a463e") : Tinte;
        Beschriften(schrift, p.Vorname, r.Position + new Vector2(6, 76), KarteB - 12, 14, farbe);
        var jahre = tot ? $"{p.Geburtsjahr}–{p.Todesjahr}" : $"* {p.Geburtsjahr} · {p.Alter(_buch!.Jahr)} J.";
        Beschriften(schrift, jahre, r.Position + new Vector2(6, 91), KarteB - 12, 12, tinte * new Color(1, 1, 1, 0.85f));
        if (k.IstErbe)
        {
            // Erbe: grünes Band über die Kartenbreite, gut von der Krone des Oberhaupts zu unterscheiden
            DrawRect(new Rect2(r.Position + new Vector2(3, KarteH - 15), new Vector2(KarteB - 6, 12)), new Color("4f7a2f"));
            Beschriften(schrift, "ERBE", r.Position + new Vector2(10, KarteH - 5), KarteB - 20, 10, new Color("f2f0dc"));
        }
        else if (k.Bezeichnung != "")
            Beschriften(schrift, k.Bezeichnung, r.Position + new Vector2(6, 105), KarteB - 12, 10,
                k.IstOberhaupt ? new Color("7a5a10") : k.IstErbe ? new Color("3f5a22") : tinte * new Color(1, 1, 1, 0.75f));

        // Krone über dem Oberhaupt (golden), blass über früheren Oberhäuptern
        if (k.IstOberhaupt || k.IstAhne) Krone(r.Position + new Vector2(r.Size.X - 28, 34), k.IstOberhaupt && p.Lebt);
    }

    private void Krone(Vector2 oben, bool golden)
    {
        var f = golden ? Gold : new Color("8f8a78");
        var pts = new[]
        {
            oben + new Vector2(-12, 0), oben + new Vector2(-12, -14), oben + new Vector2(-6, -8), oben + new Vector2(0, -17),
            oben + new Vector2(6, -8), oben + new Vector2(12, -14), oben + new Vector2(12, 0),
        };
        DrawPolygon(pts, new[] { f });
        DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[4], pts[5], pts[6], pts[0] }, Tinte, 1.6f);
    }

    /// <summary>Text, der in die Breite passt: erst kleiner, dann abgeschnitten (mit Auslassungszeichen).</summary>
    private void Beschriften(Font schrift, string text, Vector2 grundlinie, float breite, int groesse, Color farbe)
    {
        var g = groesse;
        while (g > 9 && schrift.GetStringSize(text, HorizontalAlignment.Left, -1, g).X > breite) g--;
        while (text.Length > 2 && schrift.GetStringSize(text, HorizontalAlignment.Left, -1, g).X > breite) text = text[..^2] + "…";
        DrawString(schrift, grundlinie, text, HorizontalAlignment.Left, -1, g, farbe);
    }

    private Karte? KarteBei(Vector2 pos) => _karten.FirstOrDefault(k => k.Raum.HasPoint(pos));

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick) return;
        foreach (var kn in _knoepfe)
            if (kn.Mitte.DistanceTo(klick.Position) <= KnopfRadius + 3) { Umgeschaltet?.Invoke(kn.Nummer); AcceptEvent(); return; }
        if (KarteBei(klick.Position) is { } k && PortraetAnzeige.PersonOeffnen is { } oeffnen) { oeffnen(k.Person); AcceptEvent(); }
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (_buch is null) return "";
        if (_knoepfe.FirstOrDefault(kn => kn.Mitte.DistanceTo(atPosition) <= KnopfRadius + 3) is { } kn)
            return kn.Zu ? $"{kn.Versteckt} Nachkommen eingeklappt – klicken zum Aufklappen." : "Zweig einklappen.";
        if (KarteBei(atPosition) is not { } k) return "";
        var p = k.Person;
        var zeilen = new List<string> { p.NameMitHerkunft, $"{p.Volk.Einzahl()}, {(p.Lebt ? $"geboren {p.Geburtsjahr}, {p.Alter(_buch.Jahr)} Jahre" : $"{p.Geburtsjahr} bis {p.Todesjahr}, {p.Alter(_buch.Jahr)} Jahre alt geworden")}" };
        if (!p.Lebt && p.Todesursache is { } u) zeilen.Add($"gestorben {u}");
        if (p.Lebt) zeilen.Add(_buch.Verfassung(p));
        if (k.Bezeichnung != "") zeilen.Add(k.Bezeichnung);
        zeilen.Add("Klick: Charakterfenster");
        return string.Join("\n", zeilen);
    }
}
