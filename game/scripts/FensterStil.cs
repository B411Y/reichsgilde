using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Brücke zum Thema der Fenster und Tafeln (<c>res://scenes/fenster_thema.tres</c>, UI-Umbau „Zunftlade“, Teil 2), gebaut wie
/// <see cref="LadeStil"/>: Rand aus Stein, Kopf aus Holz, Leib aus Pergament, Schrift in dunkler Tinte. Aussehen steht nur im Thema; dieser Code
/// kennt nur die <b>Namen</b> der Maße, Farben und Typen.
///
/// <b>Tinte statt heller Schrift:</b> Die Tafeln des Spiels sind für dunklen Grund geschrieben (rund 800 feste Farben). Statt jede Stelle umzubauen,
/// rechnet <see cref="Tinte"/> eine helle Farbe in die passende Tintenfarbe des Themas um (Schrift → Tinte, Gold → Akzent, Rot → Warnung, Grün → gut),
/// und <see cref="Einrichten"/> wendet das auf alles an, was auf dem Pergament liegt – beim Eintritt in den Baum und bei jeder späteren Änderung der
/// Farbe. Was auf Holz liegt (Knöpfe, Fensterkopf, Hinweise), bleibt hell. Neue Tafeln nehmen ihre Farben gleich aus dem Thema.
/// </summary>
public static class FensterStil
{
    /// <summary>Merker an einem Steuerelement: der Stoff, auf dem seine Kinder liegen („pergament“ oder „dunkel“).</summary>
    public const string Stoff = "rg_stoff";
    public const string Pergament = "pergament";
    public const string Dunkel = "dunkel";

    private const string Verbunden = "rg_tinte";

    private static Theme? _thema;

    /// <summary>Das Thema, einmal geladen.</summary>
    public static Theme Thema => _thema ??= GD.Load<Theme>("res://scenes/fenster_thema.tres");

    private static readonly Dictionary<Theme, Theme> Gemischt = new();

    /// <summary>
    /// Das Thema für eine Tafel: ohne eigenes Thema das der Fenster. Bringt die Tafel ein eigenes mit (Rat: <c>rat_thema.tres</c>), bekommt sie eine
    /// Mischung – die Grundtypen des Pergaments, darüber alles aus ihrem eigenen Thema (das gewinnt). Je eigenem Thema einmal gebaut.
    /// </summary>
    public static Theme ThemaMit(Theme? eigenes)
    {
        if (eigenes is null || eigenes == Thema) return Thema;
        if (Gemischt.TryGetValue(eigenes, out var fertig)) return fertig;
        var mischung = (Theme)Thema.Duplicate();
        mischung.MergeWith(eigenes);
        if (eigenes.DefaultFont is { } schrift) mischung.DefaultFont = schrift;
        if (eigenes.DefaultFontSize > 0) mischung.DefaultFontSize = eigenes.DefaultFontSize;
        Gemischt[eigenes] = mischung;
        return mischung;
    }

    /// <summary>Ein Maß eines Pseudo-Typs (Grundwert „FensterMasse“), in Einheiten der Oberfläche.</summary>
    public static int Mass(string name, string typ = "FensterMasse") => Thema.GetConstant(name, typ);

    /// <summary>Eine Farbe eines Pseudo-Typs (Grundwert „FensterFarben“).</summary>
    public static Color Farbe(string name, string typ = "FensterFarben") => Thema.GetColor(name, typ);

    /// <summary>
    /// Zieht einer Tafel das Fensterkleid an (Steinrand, Pergament, Kopf aus Holz mit <paramref name="titel"/>), auch ohne <see cref="Fensterrahmen"/>.
    /// Ohne Titel sucht das Kleid die Überschrift der Tafel selbst (siehe <see cref="Fensterkleid"/>).
    /// </summary>
    public static Fensterkleid Bekleiden(PanelContainer tafel, string? titel = null)
    {
        if (tafel.GetNodeOrNull<Fensterkleid>("Fensterkleid") is { } vorhanden) return vorhanden;
        var kleid = new Fensterkleid(tafel) { Titel = titel };
        tafel.AddChild(kleid);
        return kleid;
    }

    /// <summary>Einmal beim Start (Autoload <see cref="Fenster"/>, nach <see cref="Schriften.Einrichten"/>): Alles, was auf Pergament in den Baum kommt, bekommt Tinte.</summary>
    public static void Einrichten(SceneTree baum)
    {
        baum.NodeAdded += knoten =>
        {
            if (knoten is Control steuer && AufPergament(steuer)) Wandeln(steuer);
        };
    }

    /// <summary>Beim Beenden: das Thema loslassen.</summary>
    public static void Freigeben()
    {
        foreach (var mischung in Gemischt.Values) mischung.Dispose();
        Gemischt.Clear();
        _thema = null;
    }

    /// <summary>
    /// Liegt das Steuerelement auf Pergament? Gesucht wird nach oben bis zum nächsten Merker <see cref="Stoff"/>; ein Knopf mit Fläche dazwischen
    /// ist Holz (seine Beschriftung bleibt hell), ein eigenes Fenster (Aufklappmenü) ebenso.
    /// </summary>
    public static bool AufPergament(Node knoten)
    {
        for (var n = knoten.GetParent(); n is not null; n = n.GetParent())
        {
            if (n.HasMeta(Stoff)) return n.GetMeta(Stoff).AsString() == Pergament;
            if (n is BaseButton { } knopf && knopf is not (CheckBox or CheckButton or LinkButton) && knopf is not Button { Flat: true }) return false;
            if (n is Window) return false;
        }
        return false;
    }

    /// <summary>Wendet die Umrechnung auf eine Tafel an, die schon Kinder hat (das Kleid kommt oft erst nach dem Aufbau).</summary>
    public static void AllesWandeln(Node wurzel)
    {
        foreach (var kind in wurzel.GetChildren())
        {
            if (kind.HasMeta(Stoff)) continue; // eigener Stoff: eigenes Kleid oder bewusst dunkel
            if (kind is Control steuer && AufPergament(steuer)) Wandeln(steuer);
            if (kind is not Window) AllesWandeln(kind);
        }
    }

    private static readonly string[] Schriftfarben = { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" };

    private static void Wandeln(Control steuer)
    {
        if (steuer.HasMeta(Stoff)) return;
        Action? tun = steuer switch
        {
            Label => () => FarbeWandeln(steuer, "font_color"),
            RichTextLabel => () => FarbeWandeln(steuer, "default_color"),
            Button { Flat: true } or CheckBox or CheckButton or LinkButton => () =>
            {
                if (steuer is Button { Flat: true } && steuer.ThemeTypeVariation.IsEmpty) steuer.ThemeTypeVariation = "FensterFlach";
                foreach (var name in Schriftfarben) FarbeWandeln(steuer, name);
            },
            PanelContainer or Panel => () => KastenWandeln(steuer),
            ColorRect rechteck => () =>
            {
                var neu = Fuellung(rechteck.Color);
                if (neu != rechteck.Color) rechteck.Color = neu;
            },
            _ => null,
        };
        if (tun is null) return;
        tun();
        if (steuer.HasMeta(Verbunden)) return;
        steuer.SetMeta(Verbunden, true);
        // Spätere Änderungen (eine Tafel färbt ihren Wert rot) laufen wieder hier durch; die Umrechnung ändert eine Tintenfarbe nicht mehr, also keine Schleife.
        steuer.ThemeChanged += () =>
        {
            if (steuer.IsInsideTree() && AufPergament(steuer)) tun();
        };
    }

    private static void FarbeWandeln(Control steuer, string name)
    {
        if (!steuer.HasThemeColorOverride(name)) return;
        var ist = steuer.GetThemeColor(name);
        var soll = Tinte(ist);
        if (soll != ist) steuer.AddThemeColorOverride(name, soll);
    }

    private static void KastenWandeln(Control steuer)
    {
        if (!steuer.HasThemeStyleboxOverride("panel") || steuer.GetThemeStylebox("panel") is not StyleBoxFlat kasten || kasten.HasMeta(Verbunden)) return;
        var neu = (StyleBoxFlat)kasten.Duplicate();
        neu.BgColor = Flaeche(kasten.BgColor);
        neu.BorderColor = Rand(kasten.BorderColor);
        neu.ShadowSize = 0;
        neu.SetMeta(Verbunden, true);
        steuer.AddThemeStyleboxOverride("panel", neu);
    }

    /// <summary>
    /// Die Tintenfarbe zu einer Farbe, die für dunklen Grund gedacht war. Was auf dem Pergament schon 4,5 : 1 erreicht, bleibt. Sonst nach Farbton:
    /// blass → Tinte (dunklere Grautöne → leise Tinte), Gold → Akzent, Rot → Warnung, Grün → gut; Blau und Violett behalten den Ton und werden dunkler.
    /// </summary>
    public static Color Tinte(Color farbe)
    {
        if (farbe.A < 0.05f) return farbe;
        var grund = Farbe("grund");
        if (Schriften.Kontrast(farbe, grund) >= 4.5f) return farbe;
        float ton = farbe.H * 360f, satt = farbe.S, hell = farbe.V;
        Color ziel;
        if (satt < 0.22f) ziel = Farbe(hell > 0.72f ? "tinte" : "tinte_leise");
        else if (ton is >= 25f and < 70f) ziel = Farbe(hell > 0.8f && satt > 0.35f ? "akzent" : "tinte_leise");
        else if (ton is < 25f or >= 330f) ziel = Farbe("warnung");
        else if (ton < 170f) ziel = Farbe("gut");
        else
        {
            ziel = farbe;
            for (var v = 0.5f; v >= 0.1f; v -= 0.05f)
            {
                ziel = Color.FromHsv(farbe.H, Math.Max(satt, 0.6f), v);
                if (Schriften.Kontrast(ziel, grund) >= 4.5f) break;
            }
        }
        ziel.A = Math.Max(farbe.A, 0.85f);
        return ziel;
    }

    /// <summary>Der Grund eines Kastens auf dem Pergament: aus Dunkel wird durchscheinendes Braun, eine Farbe bleibt als zarter Ton.</summary>
    public static Color Flaeche(Color farbe)
    {
        if (farbe.A < 0.02f) return farbe;
        var flaeche = Farbe("flaeche");
        if (farbe.S < 0.3f || farbe.V < 0.25f)
            return new Color(flaeche, farbe.A >= 0.5f ? flaeche.A : Math.Clamp(farbe.A * 1.6f, 0.06f, flaeche.A));
        var ton = Color.FromHsv(farbe.H, Math.Min(1f, farbe.S + 0.1f), 0.5f);
        return new Color(ton, farbe.A >= 0.5f ? 0.28f : farbe.A);
    }

    /// <summary>Der Rand eines Kastens: Gold bleibt eine kräftige Marke (Akzent), alles andere wird die feine braune Linie.</summary>
    public static Color Rand(Color farbe)
    {
        if (farbe.A < 0.02f) return farbe;
        if (farbe.S < 0.45f || farbe.V < 0.7f) return Farbe("flaeche_rand");
        return Tinte(farbe);
    }

    /// <summary>Ein Farbfeld (Linie, Balken): blass und dunkel → durchscheinendes Braun, blass und hell → Linie, farbig → kräftige Tintenfarbe.</summary>
    public static Color Fuellung(Color farbe)
    {
        if (farbe.A < 0.02f) return farbe;
        if (farbe.S >= 0.3f && farbe.V >= 0.25f) return Tinte(farbe);
        if (farbe.V < 0.4f) return Flaeche(farbe);
        var linie = Farbe("linie");
        return new Color(linie, Math.Clamp(farbe.A, linie.A, 0.6f));
    }
}
