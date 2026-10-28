using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Talentbaum der Einrichtung des eigenen Wohnhauses (PR #224): Haushalt, Ansehen, Schutz × die ersten drei Wohnhausstufen (siehe <see cref="TalentbaumAnsicht"/>).
/// Öffnet über „Einrichtung“ in der Wohnhaus-Tafel. Wie die Tafeln der Werkstatt ändert sie nichts selbst: Ein Klick schickt <see cref="WohnhausEinrichtungKaufen"/>.
/// </summary>
public sealed class WohnhausBaumAnzeige
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);

    public PanelContainer Tafel { get; }
    private readonly VBoxContainer _inhalt;
    private Simulation? _sim;
    private Konto? _spieler;
    private string _stand = "";
    private double _seitAufbau = 1;

    /// <summary>Ergebnis eines Befehls (Erfolg oder Ablehnung) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public WohnhausBaumAnzeige()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(960);
        Tafel = tafel;
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(_inhalt);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        Fensterrahmen.Anmelden(Tafel, "WohnhausBaumAnzeige");
        Tafel.Hide();
    }

    public bool Visible => Tafel.Visible;
    public void Hide() => Tafel.Hide();

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _sim = simulation;
        _spieler = spieler;
    }

    public void Oeffnen()
    {
        _stand = "";
        _seitAufbau = 1;
        Tafel.Show();
        Aktualisieren(0);
    }

    public void Aktualisieren(double delta)
    {
        if (!Tafel.Visible || _sim?.Wirtschaft is not { } w || _spieler is null) return;
        _seitAufbau += delta;
        var baum = w.Wohnen.Baum(_spieler);
        var stand = $"{baum.Stufe}|{(int)_spieler.Vermoegen / 10}|" + string.Join(";", baum.Stuecke.Select(e => $"{e.Kennung}:{e.Zustand}:{(int)e.MehrgewinnJeJahr}"));
        if (stand == _stand || _seitAufbau < 0.2) return;
        _stand = stand;
        _seitAufbau = 0;
        UiHilfe.KinderEntfernen(_inhalt);
        Aufbauen(w, baum);
    }

    private void Aufbauen(Stadtwirtschaft w, Einrichtungsbaum baum)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        Hauptmenue.Text(_inhalt, "Einrichtung des Wohnhauses", 24, Gold, zentriert: true);
        var stufen = w.Wohnen.Daten?.Stufen;
        Hauptmenue.Text(_inhalt, $"{_spieler!.Inhaber} · bar {Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}", 14, new Color(0.92f, 0.88f, 0.80f), zentriert: true);
        _inhalt.AddChild(new HSeparator());
        if (baum.Stuecke.Count == 0) { Hauptmenue.Text(_inhalt, "Für das Wohnhaus gibt es nichts zu kaufen.", 14, Grau); return; }
        _inhalt.AddChild(TalentbaumAnsicht.Bauen(baum, waehrung, r => stufen is { } l && r - 1 < l.Count ? l[r - 1].Name : $"Stufe {r}", s =>
        {
            var ergebnis = w.Ausfuehren(_spieler, new WohnhausEinrichtungKaufen(s.Kennung));
            Gemeldet?.Invoke(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
            _stand = "";
            _seitAufbau = 1;
        }));
        Hauptmenue.Text(_inhalt, "Ein Klick kauft das Stück (bunt: kaufbar, grüner Haken: gekauft, dunkel: gesperrt). Reihen über dem Wohnhaus, das Ihr habt, sind gesperrt, bis Ihr es ausgebaut habt.", 12, Grau, umbruchBreite: 840);
    }
}
