using System.Text.Json;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Danksagung (Credits) im Hauptmenü: Wer am Spiel mitgewirkt hat und welches fremde Material in ihm steckt, mit Werk, Urheber,
/// Quelle und Lizenz. Gelesen wird <c>res://daten/danksagung.json</c>; die Datei baut <c>tools/assets/danksagung_bauen.py</c> aus
/// <c>docs/assets/protokoll.csv</c> und den <c>HERKUNFT.md</c>-Dateien, und <c>tools/assets/protokoll_pruefen.py</c> meldet, wenn etwas fehlt.
/// Ein zweiter Knopf zeigt die Lizenztexte der Engine und ihrer Bausteine (Pflicht der Godot-Lizenzen), von Godot selbst geliefert.
/// </summary>
public sealed class DanksagungAnzeige
{
    private const string Pfad = "res://daten/danksagung.json";
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private const int Breite = 700, Textbreite = 620;

    public PanelContainer Tafel { get; }

    private readonly ScrollContainer _rolle;
    private readonly VBoxContainer _inhalt;
    private readonly Button _lizenzKnopf;
    private bool _lizenzen;

    public DanksagungAnzeige(Action zurueck)
    {
        var (tafel, spalte) = Hauptmenue.Tafel(Breite);
        Tafel = tafel;
        Hauptmenue.Text(spalte, "Danksagung", 26, Gold, zentriert: true);
        Hauptmenue.Text(spalte, "Reichsgilde steht auf den Schultern vieler. Hab Dank, wer hier genannt ist.", 14, Grau, zentriert: true, umbruchBreite: Textbreite);
        spalte.AddChild(new HSeparator());

        _rolle = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(Textbreite + 24, 440),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        spalte.AddChild(_rolle);
        _inhalt = new VBoxContainer { CustomMinimumSize = new Vector2(Textbreite, 0) };
        _inhalt.AddThemeConstantOverride("separation", 4);
        _rolle.AddChild(_inhalt);

        var leiste = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        leiste.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(leiste);
        _lizenzKnopf = Hauptmenue.Knopf(leiste, "Lizenztexte der Engine", () => { _lizenzen = !_lizenzen; Auffrischen(); });
        _lizenzKnopf.CustomMinimumSize = new Vector2(240, 40);
        _lizenzKnopf.TooltipText = "Der Lizenztext von Godot und die Hinweise seiner Bausteine (Schrift-, Ton-, Bildbibliotheken …).";
        Hauptmenue.Knopf(leiste, "Zurück", zurueck);
    }

    /// <summary>Beim Öffnen: zuerst die Danksagung, oben beginnend.</summary>
    public void Oeffnen()
    {
        _lizenzen = false;
        Auffrischen();
    }

    /// <summary>Gleich die Lizenztexte der Engine zeigen (Startoption für Bildschirmfotos).</summary>
    public void LizenzenZeigen()
    {
        _lizenzen = true;
        Auffrischen();
    }

    private void Auffrischen()
    {
        foreach (var kind in _inhalt.GetChildren()) { _inhalt.RemoveChild(kind); kind.QueueFree(); }
        _rolle.ScrollVertical = 0;
        _lizenzKnopf.Text = _lizenzen ? "Zur Danksagung" : "Lizenztexte der Engine";
        if (_lizenzen) LizenztexteFuellen();
        else DanksagungFuellen();
    }

    private void Zeile(string text, int groesse, Color? farbe = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Hauptmenue.Text(_inhalt, text, groesse, farbe, umbruchBreite: Textbreite);
    }

    private void DanksagungFuellen()
    {
        try
        {
            using var f = Godot.FileAccess.Open(Pfad, Godot.FileAccess.ModeFlags.Read);
            if (f is null) { Zeile($"{Pfad} fehlt.", 14, Grau); return; }
            using var doc = JsonDocument.Parse(f.GetAsText());
            foreach (var gruppe in doc.RootElement.GetProperty("gruppen").EnumerateArray())
            {
                _inhalt.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
                Zeile(Text(gruppe, "name"), 21, Gold);
                _inhalt.AddChild(new HSeparator());
                Zeile(Text(gruppe, "text"), 13, Grau);
                foreach (var e in gruppe.GetProperty("eintraege").EnumerateArray())
                {
                    _inhalt.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
                    Zeile(Text(e, "werk"), 16);
                    Zeile(Text(e, "autor"), 13, Grau);
                    var lizenz = Text(e, "lizenz");
                    Zeile((lizenz != "" ? "Lizenz: " + lizenz + "   " : "") + Text(e, "quelle"), 12, Grau);
                    Zeile(Text(e, "hinweis"), 12, Grau);
                }
                var dank = Text(gruppe, "dank");
                if (dank != "")
                {
                    _inhalt.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
                    Zeile(dank, 13);
                }
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"DanksagungAnzeige: {Pfad} nicht lesbar ({e.Message}).");
            Zeile("Die Danksagung konnte nicht gelesen werden.", 14, Grau);
        }
    }

    private static string Text(JsonElement o, string name) => o.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

    /// <summary>Lizenztext der Engine, ihre Bausteine mit Urheberhinweis und die Lizenztexte, wie Godot sie mitbringt.</summary>
    private void LizenztexteFuellen()
    {
        Zeile("Godot Engine", 21, Gold);
        Zeile(Engine.GetLicenseText(), 12, Grau);
        Zeile("Bausteine der Engine und ihre Urheber", 21, Gold);
        foreach (var teil in Engine.GetCopyrightInfo())
        {
            var name = teil.GetValueOrDefault("name").AsString();
            var zeilen = new List<string>();
            foreach (var p in teil.GetValueOrDefault("parts").AsGodotArray())
            {
                var d = p.AsGodotDictionary();
                var urheber = string.Join("; ", d.GetValueOrDefault("copyright").AsStringArray());
                zeilen.Add($"{urheber} ({d.GetValueOrDefault("license").AsString()})");
            }
            Zeile(name, 14);
            Zeile(string.Join("\n", zeilen.Distinct()), 11, Grau);
        }
        Zeile("Lizenztexte", 21, Gold);
        foreach (var (name, texte) in Engine.GetLicenseInfo())
        {
            Zeile(name.AsString(), 14);
            Zeile(texte.AsString(), 11, Grau);
        }
    }
}
