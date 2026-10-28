using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Talentbaum der Einrichtung (PR #224), gemeinsam für Betriebe (<see cref="EinrichtenAnzeige"/>) und das Wohnhaus (<see cref="WohnhausBaumAnzeige"/>):
/// drei Spalten × drei Reihen (Reihe = Gebäudestufe), je Stück ein Knopf mit Icon und Rahmen nach Zustand (gekauft grün, kaufbar golden,
/// gerade nicht möglich blass golden, gesperrt grau), dazu Linien von der Voraussetzung zum Stück. Reihen über der Gebäudestufe liegen grau.
/// Ein Klick kauft (der Befehl geht über <paramref name="klick"/>); der Hinweis mit Wirkung, Preis, Voraussetzung und Amortisation ist ein gewöhnlicher
/// Tooltip, also festheftbar und mit Lexikon-Verweisen.
///
/// Icons: <c>res://assets/oberflaeche/icons/einrichtung_&lt;kennung&gt;.png</c>, solange es die nicht gibt, ein ähnliches Waren-Icon (<see cref="Ersatz"/>)
/// oder ein Buchstabe.
/// </summary>
public static partial class TalentbaumAnsicht
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);

    private const int Knopf = 64;
    private const int StueckBreite = 92;

    /// <summary>Waren-Icons als Platzhalter, bis es eigene Einrichtungs-Icons gibt (Kennung → Datei ohne Endung in assets/oberflaeche/icons).</summary>
    private static readonly Dictionary<string, string> Ersatz = new()
    {
        ["Werkzeug1"] = "werkzeug", ["Werkzeug2"] = "werkzeug", ["Regale"] = "schrank", ["Ladentheke"] = "truhe", ["Lehrlingsstube"] = "schemel",
        ["Schuerzen"] = "lumpen", ["Handschuhe"] = "lumpen", ["Eisentuer"] = "beschlag", ["Riegel"] = "beschlag", ["Loeschfass"] = "holz",
        ["Hausapotheke"] = "heilsalbe", ["Vorratskammer"] = "roggen", ["Kueche"] = "kraeuter", ["Stube"] = "schemel", ["Bibliothek"] = "psalter",
        ["Hauswaechter"] = "armbrust", ["Hauswappen"] = "prunkschwert", ["Hunde"] = "armbrust", ["Kettenhemd"] = "silberkette",
    };

    /// <summary>Baut den Baum. <paramref name="reihenName"/> nennt die Reihe (z. B. „Stufe 1“ oder der Name der Wohnhausstufe).</summary>
    public static Control Bauen(Einrichtungsbaum baum, string waehrung, Func<int, string> reihenName, Action<BaumStueck> klick)
    {
        var feld = new Linienfeld();
        var raster = new GridContainer { Columns = baum.Spalten.Count + 1 };
        raster.AddThemeConstantOverride("h_separation", 8);
        raster.AddThemeConstantOverride("v_separation", 8);
        feld.AddChild(raster);

        raster.AddChild(new Control { CustomMinimumSize = new Vector2(70, 0) });
        foreach (var spalte in baum.Spalten)
        {
            var kopf = Hauptmenue.Text(raster, spalte, 15, Gold, zentriert: true);
            kopf.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }

        for (var reihe = 1; reihe <= baum.Reihen; reihe++)
        {
            var frei = baum.Stufe >= reihe;
            var name = Hauptmenue.Text(raster, reihenName(reihe) + (frei ? "" : "\ngesperrt"), 12, frei ? Hell : Grau);
            name.CustomMinimumSize = new Vector2(70, 0);
            name.VerticalAlignment = VerticalAlignment.Center;
            foreach (var spalte in baum.Spalten)
            {
                var zelle = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(StueckBreite, Knopf + 44) };
                zelle.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = frei ? new Color(0.17f, 0.15f, 0.12f) : new Color(0.10f, 0.10f, 0.10f),
                    BorderColor = new Color(0.35f, 0.30f, 0.22f, frei ? 0.7f : 0.3f), BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1,
                    CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                    ContentMarginBottom = 4, ContentMarginTop = 4, ContentMarginLeft = 4, ContentMarginRight = 4,
                });
                raster.AddChild(zelle);
                var inhalt = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
                inhalt.AddThemeConstantOverride("separation", 6);
                zelle.AddChild(inhalt);
                foreach (var stueck in baum.Stuecke.Where(s => s.Spalte == spalte && s.Reihe == reihe))
                    feld.Merken(stueck.Kennung, Stueck(inhalt, baum, stueck, waehrung, klick), stueck.Voraussetzung,
                        baum.Stuecke.FirstOrDefault(x => x.Kennung == stueck.Voraussetzung)?.Zustand == Baumzustand.Gekauft);
            }
        }
        return feld;
    }

    private static Button Stueck(Container eltern, Einrichtungsbaum baum, BaumStueck s, string waehrung, Action<BaumStueck> klick)
    {
        var spalte = new VBoxContainer { CustomMinimumSize = new Vector2(StueckBreite - 8, 0) };
        spalte.AddThemeConstantOverride("separation", 2);
        eltern.AddChild(spalte);
        var farbe = s.Zustand switch
        {
            Baumzustand.Gekauft => Gruen,
            Baumzustand.Kaufbar => Gold,
            Baumzustand.NichtJetzt => Gold * new Color(1, 1, 1, 0.55f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };
        var breite = s.Zustand is Baumzustand.Gekauft or Baumzustand.Kaufbar ? 3 : 2;
        StyleBoxFlat Kasten(float hell) => new()
        {
            BgColor = (s.Zustand == Baumzustand.Gekauft ? new Color(0.16f, 0.24f, 0.13f) : new Color(0.24f, 0.19f, 0.12f)) * new Color(hell, hell, hell),
            BorderColor = farbe, BorderWidthBottom = breite, BorderWidthLeft = breite, BorderWidthRight = breite, BorderWidthTop = breite,
            CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
        };
        var knopf = new Button
        {
            CustomMinimumSize = new Vector2(Knopf, Knopf), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, FocusMode = Control.FocusModeEnum.None,
            ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Center,
            MouseDefaultCursorShape = s.Zustand == Baumzustand.Gekauft ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand,
            TooltipText = Tipp(baum, s, waehrung),
        };
        foreach (var art in new[] { "normal", "pressed", "disabled", "focus" }) knopf.AddThemeStyleboxOverride(art, Kasten(1f));
        knopf.AddThemeStyleboxOverride("hover", Kasten(1.35f));
        knopf.AddThemeConstantOverride("icon_max_width", Knopf - 14);
        var gesperrt = s.Zustand == Baumzustand.Gesperrt;
        var tonung = gesperrt ? new Color(0.45f, 0.45f, 0.45f) : s.Zustand == Baumzustand.NichtJetzt ? new Color(0.8f, 0.8f, 0.8f) : Colors.White;
        foreach (var art in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_disabled_color" }) knopf.AddThemeColorOverride(art, tonung);
        if (Icon(s.Kennung) is { } bild) knopf.Icon = bild;
        else
        {
            knopf.Text = s.Name.Length > 0 ? s.Name[..1] : "?"; // Platzhalter, bis das Icon da ist
            knopf.AddThemeFontSizeOverride("font_size", 30);
            knopf.AddThemeColorOverride("font_color", tonung * Hell);
        }
        knopf.Pressed += () => { if (s.Zustand != Baumzustand.Gekauft) klick(s); };
        spalte.AddChild(knopf);

        var name = Hauptmenue.Text(spalte, s.Name, 11, gesperrt ? Grau : Hell, zentriert: true, umbruchBreite: StueckBreite - 8);
        name.TooltipText = knopf.TooltipText;
        name.MouseFilter = Control.MouseFilterEnum.Pass;
        var preis = s.Zustand == Baumzustand.Gekauft ? "gekauft"
            : s.Jaehrlich ? $"{Zahlenformat.Ganz(s.Preis)} im Jahr" : Zahlenformat.Ganz(s.Preis) + " " + waehrung;
        Hauptmenue.Text(spalte, preis, 11, s.Zustand switch { Baumzustand.Gekauft => Gruen, Baumzustand.Kaufbar => Gold, Baumzustand.NichtJetzt => Rot, _ => Grau }, zentriert: true);
        return knopf;
    }

    private static Texture2D? Icon(string kennung) =>
        WarenIcons.Icon("einrichtung_" + kennung) ?? (Ersatz.TryGetValue(kennung, out var ersatz) ? WarenIcons.Icon(ersatz) : null);

    /// <summary>Hinweistext eines Stücks: Wirkung, Preis, Voraussetzung, Amortisation, Sperrgrund.</summary>
    public static string Tipp(Einrichtungsbaum baum, BaumStueck s, string waehrung)
    {
        var zeilen = new List<string> { s.Name, s.Wirkung };
        zeilen.Add(s.Jaehrlich ? $"Lohn: {Zahlenformat.Ganz(s.Preis)} {waehrung} im Jahr" : $"Preis: {Zahlenformat.Ganz(s.Preis)} {waehrung}");
        zeilen.Add($"Ab Gebäudestufe {s.Reihe}");
        if (s.Voraussetzung is { } v) zeilen.Add("Braucht: " + (baum.Stuecke.FirstOrDefault(x => x.Kennung == v)?.Name ?? v));
        if (s.Zustand != Baumzustand.Gekauft)
        {
            if (s.Amortisationsjahre is { } jahre && s.MehrgewinnJeJahr > 0)
                zeilen.Add($"Rechnet sich nach etwa {Zahlenformat.Kommazahl(jahre)} Jahren (bringt heute rund {Zahlenformat.Ganz(s.MehrgewinnJeJahr)} {waehrung} im Jahr mehr)");
            else if (!s.Jaehrlich) zeilen.Add("Bringt keine Gulden direkt, schützt oder erleichtert die Arbeit.");
        }
        zeilen.Add(s.Zustand == Baumzustand.Gekauft ? "Schon gekauft." : s.Verboten is { } g ? "Gerade nicht möglich: " + g : "Ein Klick kauft es.");
        return string.Join("\n", zeilen);
    }

    /// <summary>Hält den Baum und zeichnet die Linien von den Voraussetzungen zu den Stücken (in einer Schicht über den Zellen).</summary>
    private sealed partial class Linienfeld : Container
    {
        private readonly Dictionary<string, Control> _knoepfe = new();
        private readonly List<(string Von, string Nach, bool Erfuellt)> _linien = new();
        private readonly Control _ueber = new() { MouseFilter = MouseFilterEnum.Ignore };

        public Linienfeld()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            _ueber.Draw += Zeichnen;
        }

        public override void _Ready() => AddChild(_ueber);

        public void Merken(string kennung, Control knopf, string? voraussetzung, bool erfuellt)
        {
            _knoepfe[kennung] = knopf;
            if (voraussetzung is not null) _linien.Add((voraussetzung, kennung, erfuellt));
        }

        public override Vector2 _GetMinimumSize() => GetChildCount() > 0 && GetChild(0) is Control c ? c.GetCombinedMinimumSize() : Vector2.Zero;

        public override void _Notification(int was)
        {
            if (was != NotificationSortChildren) return;
            foreach (var kind in GetChildren()) if (kind is Control c) FitChildInRect(c, new Rect2(Vector2.Zero, Size));
        }

        public override void _Process(double delta) => _ueber.QueueRedraw();

        private void Zeichnen()
        {
            foreach (var (von, nach, erfuellt) in _linien)
            {
                if (!_knoepfe.TryGetValue(von, out var a) || !_knoepfe.TryGetValue(nach, out var b)) continue;
                var anfang = a.GlobalPosition - GlobalPosition + new Vector2(a.Size.X / 2, a.Size.Y);
                var ende = b.GlobalPosition - GlobalPosition + new Vector2(b.Size.X / 2, 0);
                var mitte = (anfang.Y + ende.Y) / 2;
                // Knick in halber Höhe, damit die Linie auch zwischen verschiedenen Spalten sauber läuft
                _ueber.DrawPolyline(new[] { anfang, new Vector2(anfang.X, mitte), new Vector2(ende.X, mitte), ende }, erfuellt ? Gold : new Color(0.5f, 0.5f, 0.5f), 2, true);
            }
        }
    }
}
