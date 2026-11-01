using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafel rechts für das Felswerkzeug (Felspinsel F4) – ein Teil der <c>partial class Kartenwerkstatt</c>, im Stil der übrigen Werkstatt-Tafeln.
/// Oben Vorlage (Gruppe oder Einzelstücke mit ihren Formen), Gestein der neuen Stücke und das Grundgestein der Karte (ein Befehl mit Rückgängig),
/// darunter die Regler des Pinsels (Radius, Dichte, Größe mit Streuung, Einsinken, Neigung zum Hang) und der Filter wie bei Gelände und Boden.
/// Die Regler ändern nur die Einstellungen der Stadtansicht (<see cref="Stadtansicht.FelsPinsel"/>), erst Klick oder Strich ändern die Karte.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Steht in <see cref="_gezeigt"/>, solange die Tafel das Felswerkzeug zeigt.</summary>
    private static readonly object FelsMarke = new();

    private static string GesteinName(string kennung) => Gesteinsarten.Nach(kennung).Name;

    private void FelsTafel()
    {
        EigenschaftenLeeren();
        _gezeigt = FelsMarke;
        var modus = _stadt.FelsModus;
        var gruppe = Felsformen.Gruppe(_stadt.FelsGruppe);
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(kopf);
        var farbe = modus == Stadtansicht.FelsArt.Radieren ? new Color(0.33f, 0.40f, 0.24f) : Stadtansicht.FelsFarbe(_stadt.FelsGestein ?? _entwurf.Grundgestein.Wirksam);
        kopf.AddChild(new TextureRect { Texture = Farbfeld(farbe), StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
        var titel = modus switch
        {
            Stadtansicht.FelsArt.Radieren => "Fels-Radierer",
            Stadtansicht.FelsArt.Streuen => gruppe is null ? "Felsen streuen" : $"Streuen: {gruppe.Name}",
            _ => gruppe is null ? "Stempel: Felsstück" : $"Stempel: {gruppe.Name}",
        };
        var name = Hauptmenue.Text(kopf, titel, 17, Gold);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (modus == Stadtansicht.FelsArt.Stempeln)
        {
            var wuerfeln = new Button { Text = "Neu würfeln", FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Neue Drehungen, Größen und Kippwinkel für den nächsten Stempel – die Vorschau zeigt sie gleich" };
            wuerfeln.AddThemeFontSizeOverride("font_size", 13);
            wuerfeln.Pressed += () => _stadt.FelsStempelStartwert++;
            kopf.AddChild(wuerfeln);
        }

        if (modus != Stadtansicht.FelsArt.Radieren) FelsVorlage(gruppe);

        Hauptmenue.Text(_eigenschaften, "Pinsel", 15, Gold);
        var raster = ZweiSpalten();
        if (modus != Stadtansicht.FelsArt.Stempeln)
            Zeile(raster, "Radius", Schieber(Felspinsel.MinRadius, 60, 1, _stadt.FelsRadius, Breite, r => _stadt.FelsRadius = r, "Halbmesser des Kreises an der Maus"));
        if (modus == Stadtansicht.FelsArt.Radieren)
        {
            Hauptmenue.Text(_eigenschaften, "Nimmt alle Felsstücke weg, deren Mitte im Kreis liegt. Der Boden bleibt, wie er ist.", 12, Grau, umbruchBreite: Rechtsbreite - 30);
            FilterAbschnitt(() => _stadt.FelsFilter, f => _stadt.FelsFilter = f);
            return;
        }
        if (modus == Stadtansicht.FelsArt.Streuen)
            Zeile(raster, "Dichte", Schieber(0.02, 0.8, 0.02, _stadt.FelsDichte, d => $"{d * 100:0} %", d => _stadt.FelsDichte = d,
                "Wie viel des Kreises die Felsen bedecken sollen: Der Strich setzt Stücke, bis die Grundflächen diesen Anteil erreichen"));
        Zeile(raster, "Größe", Schieber(Felsformen.MassstabMin, 2.5, 0.05, _stadt.FelsGroesse, g => $"{Komma(g, "0.00")} ×", g => _stadt.FelsGroesse = g,
            "Maßstab der Stücke (1 = wie das Modell: eine Felswand ist 9 m breit und 6,6 m hoch)"));
        Zeile(raster, "Streuung", Schieber(0, 0.75, 0.05, _stadt.FelsStreuung, s => $"± {s * 100:0} %", s => _stadt.FelsStreuung = s,
            "So weit weicht die Größe je Stück (bzw. Gruppe) zufällig ab"));
        Zeile(raster, "Einsinken", Schieber(0, 0.6, 0.05, _stadt.FelsEinsinken, e => $"{e * 100:0} %", e => _stadt.FelsEinsinken = e,
            "Anteil der Höhe unter dem Boden – 20 bis 40 % lassen keine Unterkante schweben"));
        Zeile(raster, "Neigung", Schieber(0, 1, 0.05, _stadt.FelsNeigung, n => $"{n * 100:0} %", n => _stadt.FelsNeigung = n,
            "Wie weit sich ein Stück zum Hang legt: 0 % steht senkrecht, 100 % liegt ganz am Hang an"));
        var hang = new CheckBox { Text = "Schauseite hangabwärts", ButtonPressed = _stadt.FelsZumHang, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Wände, Rippen, Überhänge, Felsnasen und Gruppen drehen ihre Vorderseite hangabwärts (± 20°), sobald der Hang 3 % steigt; sonst ganz zufällig" };
        hang.AddThemeFontSizeOverride("font_size", 14);
        hang.Toggled += an => _stadt.FelsZumHang = an;
        _eigenschaften.AddChild(hang);
        var boden = new CheckBox { Text = "Boden mitmalen", ButtonPressed = _stadt.FelsBoden, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Unter jedem Stück wird der Boden zu Fels, hangabwärts liegt ein Schuttkegel aus Kies, am oberen Rand und im Norden ein Moossaum – " +
                          "alles ein Schritt mit den Stücken. Der Radierer der Felsen lässt den Boden (dafür der Radierer der Bodentexturen)." };
        boden.AddThemeFontSizeOverride("font_size", 14);
        boden.Toggled += an => _stadt.FelsBoden = an;
        _eigenschaften.AddChild(boden);
        Hauptmenue.Text(_eigenschaften, modus == Stadtansicht.FelsArt.Stempeln
                ? "Die Vorschau an der Maus zeigt genau die Stücke, die der Klick setzt. Felsen sperren nichts: Gebäude, Wege und Bauzonen dürfen darauf stehen."
                : "Ein Strich setzt alle halben Radius einen Tupfer; jedes Stück hält Abstand zu den anderen. Felsen sperren nichts.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
        FilterAbschnitt(() => _stadt.FelsFilter, f => _stadt.FelsFilter = f);
    }

    /// <summary>Der Abschnitt „Vorlage“: Gruppe oder Einzelstücke (mit Formen), Gestein der neuen Stücke, Grundgestein der Karte.</summary>
    private void FelsVorlage(Felsgruppe? gruppe)
    {
        Hauptmenue.Text(_eigenschaften, "Vorlage", 15, Gold);
        var raster = ZweiSpalten();
        var vorlage = new OptionButton { FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Eine feste Gruppe (Wand, Kuppe, Rippe aus 5 bis 6 Stücken) oder einzelne Stücke aus den Formen darunter" };
        vorlage.AddItem("Einzelstücke");
        foreach (var g in Felsformen.Gruppen) vorlage.AddItem($"Gruppe {g.Name} ({g.Stuecke.Count} Stücke)");
        vorlage.Selected = gruppe is null ? 0 : Felsformen.Gruppen.ToList().IndexOf(gruppe) + 1;
        vorlage.ItemSelected += i =>
        {
            _stadt.FelsGruppe = i == 0 ? null : Felsformen.Gruppen[(int)i - 1].Kennung;
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Vorlage", vorlage);

        var gestein = new OptionButton { FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Das Gestein der neuen Stücke – „wie die Karte“ folgt dem Grundgestein, auch wenn es sich später ändert" };
        gestein.AddItem($"wie die Karte ({GesteinName(_entwurf.Grundgestein.Wirksam)})");
        foreach (var g in Felsformen.Gesteine) gestein.AddItem(GesteinName(g));
        gestein.Selected = _stadt.FelsGestein is { } eigen ? Felsformen.Gesteine.ToList().IndexOf(eigen) + 1 : 0;
        gestein.ItemSelected += i =>
        {
            _stadt.FelsGestein = i == 0 ? null : Felsformen.Gesteine[(int)i - 1];
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Gestein", gestein);

        var grund = new OptionButton { FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Die Grundgesteinsart der Karte: Hang-Fels im Boden und alle Stücke ohne eigenes Gestein (ein Schritt für Strg+Z)" };
        foreach (var g in Felsformen.Gesteine) grund.AddItem(GesteinName(g));
        grund.Selected = Felsformen.Gesteine.ToList().IndexOf(_entwurf.Grundgestein.Wirksam);
        grund.ItemSelected += i =>
        {
            var neu = Felsformen.Gesteine[(int)i];
            _entwurf.GrundgesteinSetzen(neu == _entwurf.Grundgestein.Wirksam && _entwurf.Grundgestein.Gestein is null ? null : neu);
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Karte", grund);

        if (gruppe is not null) return;
        var formen = new GridContainer { Columns = 2 };
        formen.AddThemeConstantOverride("h_separation", 12);
        _eigenschaften.AddChild(formen);
        foreach (var f in Felsformen.Alle)
        {
            var form = f.Kennung;
            var haken = new CheckBox { Text = f.Name, ButtonPressed = _stadt.FelsFormen.Contains(form), FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{f.Name}: {Komma(f.Breite, "0.0")} × {Komma(f.Tiefe, "0.0")} m, {Komma(f.Hoehe, "0.0")} m hoch" + (f.Gerichtet ? " – mit Schauseite" : "") };
            haken.AddThemeFontSizeOverride("font_size", 13);
            haken.Toggled += an =>
            {
                if (an && !_stadt.FelsFormen.Contains(form)) _stadt.FelsFormen.Add(form);
                if (!an) _stadt.FelsFormen.Remove(form);
                if (_stadt.FelsFormen.Count == 0) _stadt.FelsFormen.Add(Felspinsel.FormVorgabe); // nie leer (sonst gälte die Vorgabe unsichtbar)
            };
            formen.AddChild(haken);
        }
    }
}
